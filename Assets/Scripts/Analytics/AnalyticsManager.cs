using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Collects simulated KPIs into time series (charts), runs the baseline congestion forecaster,
    /// evaluates rule-based recommendations, and measures before/after scenarios:
    ///  - Live comparison: metrics with fixed-time signals before optimization was enabled vs after.
    ///  - Automatic A/B test: same seed, same duration, fixed-time vs adaptive, run back-to-back.
    /// </summary>
    public class AnalyticsManager : MonoBehaviour
    {
        public TimeSeries CongestionSeries { get; private set; }
        public TimeSeries ForecastSeries { get; private set; }
        public TimeSeries SpeedSeries { get; private set; }
        public TimeSeries AqiSeries { get; private set; }
        public TimeSeries EnergySeries { get; private set; }
        public TimeSeries QueueSeries { get; private set; }
        public CongestionPredictor Predictor { get; private set; }
        public List<Recommendation> Recommendations { get; private set; } = new List<Recommendation>();
        public CityStateSnapshot Snapshot { get; private set; } = new CityStateSnapshot();

        public TrafficMetrics Before { get; private set; }
        public TrafficMetrics After { get; private set; }
        public bool HasComparison { get; private set; }
        public string ComparisonTitle { get; private set; } = "No comparison yet";
        public string ComparisonVerdict { get; private set; } = "Enable OPTIMIZATION after ~30 s, or run the A/B TEST.";
        public List<ComparisonRow> ComparisonRows { get; private set; } = new List<ComparisonRow>();
        public bool LastComparisonImproved { get; private set; }

        public bool ABRunning { get; private set; }
        public string ABStatus { get; private set; } = "";
        public float ABProgress { get; private set; }

        public string ExternalServiceStatus { get; set; } = "Not used (offline mode)";

        private SimulationController _sim;
        private float _sampleTimer;
        private readonly MetricsAccumulator _current = new MetricsAccumulator();
        private bool _comparingLive;

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            int n = sim.settings.chartPoints;
            CongestionSeries = new TimeSeries(n); ForecastSeries = new TimeSeries(n); SpeedSeries = new TimeSeries(n);
            AqiSeries = new TimeSeries(n); EnergySeries = new TimeSeries(n); QueueSeries = new TimeSeries(n);
            Predictor = new CongestionPredictor(30, sim.settings.forecastHorizonSeconds);
            sim.traffic.TripCompleted += t => _current.AddTrip(t);
        }

        public void ResetState()
        {
            CongestionSeries.Clear(); ForecastSeries.Clear(); SpeedSeries.Clear(); AqiSeries.Clear(); EnergySeries.Clear(); QueueSeries.Clear();
            Predictor.Reset();
            _current.Reset();
            _sampleTimer = 0f;
            _comparingLive = false;
            if (!ABRunning) { _sim.SetOptimizationSilently(false); }
            Sample();
        }

        public void Step(float dt)
        {
            var t = _sim.traffic;
            _current.AddSample(dt, t.AverageSpeedKmh, t.TotalQueue, t.StoppedShare, t.Congestion);
            _sampleTimer += dt;
            if (_sampleTimer >= _sim.settings.sampleInterval)
            {
                _sampleTimer = 0f;
                Sample();
                if (_comparingLive && _current.ElapsedSeconds >= 20f)
                {
                    After = _current.Snapshot();
                    UpdateComparison("Live: fixed-time (before) vs adaptive (after)");
                }
            }
        }

        private void Sample()
        {
            var t = _sim.traffic;
            CongestionSeries.Add(t.Congestion * 100f);
            SpeedSeries.Add(t.AverageSpeedKmh);
            AqiSeries.Add(_sim.pollution.CityAverageAqi);
            EnergySeries.Add(_sim.energy.TotalMW);
            QueueSeries.Add(t.TotalQueue);
            Predictor.AddSample(_sim.SimTime, t.Congestion);
            ForecastSeries.Add(Predictor.LastForecast * 100f);
            BuildSnapshot();
            Recommendations = RecommendationEngine.Evaluate(Snapshot);
        }

        private void BuildSnapshot()
        {
            var s = Snapshot;
            var t = _sim.traffic;
            s.Congestion = t.Congestion;
            s.ForecastCongestion = Predictor.LastForecast;
            s.ForecastHorizonSeconds = Predictor.HorizonSeconds;
            s.Trend = Predictor.TrendLabel();
            s.OptimizationEnabled = _sim.OptimizationEnabled;
            Intersection worst = null;
            foreach (var j in _sim.roadNetwork.Intersections)
                if (worst == null || j.Signal.TotalQueue > worst.Signal.TotalQueue) worst = j;
            s.WorstIntersection = worst != null ? worst.Name : "-";
            s.WorstIntersectionQueue = worst != null ? worst.Signal.TotalQueue : 0;
            var p = _sim.pollution;
            s.MaxAqi = p.MaxAqi;
            s.MaxAqiZone = p.ZoneLabel(p.MaxCell.x, p.MaxCell.y);
            s.AqiWarning = _sim.settings.aqiWarningThreshold;
            s.AqiCritical = _sim.settings.aqiCriticalThreshold;
            s.EnergyMW = _sim.energy.TotalMW;
            s.EnergyPeakMW = _sim.energy.PeakMW;
            s.StreetlightsOn = _sim.energy.StreetlightsOn;
            var inc = _sim.incidents;
            s.ActiveIncidents = inc.Active.Count;
            int waiting = 0; Incident worstInc = null;
            foreach (var i in inc.Active)
            {
                if (i.Status == IncidentStatus.Active) waiting++;
                if (worstInc == null || i.Severity.Score > worstInc.Severity.Score) worstInc = i;
            }
            s.UndispatchedIncidents = waiting;
            s.MostSevereIncident = worstInc != null ? worstInc.Title : "-";
            s.MostSevereLevel = worstInc != null ? worstInc.Severity.Level : SeverityLevel.Low;
            s.BlockedRoads = _sim.roadNetwork.BlockedSegmentCount();
            s.HasComparison = HasComparison;
            s.LastComparisonImproved = LastComparisonImproved;
            s.ActiveVehicles = t.ActiveVehicles;
            s.MaxVehicles = _sim.settings.maxVehicles;
        }

        /// <summary>Called when the user toggles optimization: freezes the "before" window and starts measuring "after".</summary>
        public void OnOptimizationChanged(bool enabled)
        {
            if (ABRunning) return;
            if (enabled)
            {
                Before = _current.Snapshot();
                _current.Reset();
                _comparingLive = Before.DurationSeconds >= 10f;
                if (!_comparingLive)
                {
                    ComparisonTitle = "Live comparison";
                    ComparisonVerdict = "Baseline window too short (<10 s). Run the simulation with optimization OFF first, or use the A/B TEST.";
                }
                else
                {
                    ComparisonTitle = "Live: fixed-time (before) vs adaptive (after)";
                    ComparisonVerdict = $"Measuring the adaptive scenario... (baseline: {Before.DurationSeconds:0} s)";
                }
            }
            else
            {
                _comparingLive = false;
                _current.Reset();
            }
        }

        private void UpdateComparison(string title)
        {
            ComparisonRows = ScenarioComparison.Compare(Before, After);
            ComparisonTitle = title;
            ComparisonVerdict = ScenarioComparison.Verdict(ComparisonRows, Before, After);
            int better = 0, worse = 0;
            foreach (var r in ComparisonRows) { if (r.Outcome > 0) better++; else if (r.Outcome < 0) worse++; }
            LastComparisonImproved = better > worse;
            HasComparison = true;
        }

        // ------------------------------------------------------------------ automatic A/B test

        public void CancelABTest()
        {
            if (!ABRunning) return;
            StopAllCoroutines();
            ABRunning = false;
            ABStatus = "A/B test cancelled";
            ABProgress = 0f;
            _sim.SetSimulationSpeed(_sim.settings.defaultSimSpeed);
        }

        public void StartABTest()
        {
            if (ABRunning) { CityEvents.RaiseAlert("A/B test already running.", AlertLevel.Info); return; }
            StartCoroutine(RunABTest());
        }

        private IEnumerator RunABTest()
        {
            ABRunning = true;
            var s = _sim.settings;
            float previousSpeed = _sim.SimSpeed;
            CityEvents.RaiseAlert($"A/B test started: {s.abTestSeconds:0} s fixed-time vs {s.abTestSeconds:0} s adaptive, same seed, {s.abTestSpeed:0}x speed.", AlertLevel.Info);

            TrafficMetrics[] results = new TrafficMetrics[2];
            for (int scenario = 0; scenario < 2; scenario++)
            {
                _sim.ResetSimulation(false);
                _sim.SetOptimizationSilently(scenario == 1);
                _sim.SetSimulationSpeed(s.abTestSpeed);
                _sim.SetState(SimulationState.Running);
                string name = scenario == 0 ? "A: fixed-time baseline" : "B: adaptive optimization";
                while (_sim.SimTime < s.abWarmupSeconds)
                {
                    ABStatus = $"{name} - warm-up {_sim.SimTime:0}/{s.abWarmupSeconds:0} s";
                    ABProgress = (scenario + 0.15f * _sim.SimTime / s.abWarmupSeconds) / 2f;
                    yield return null;
                }
                _current.Reset();
                while (_current.ElapsedSeconds < s.abTestSeconds)
                {
                    if (_sim.State == SimulationState.Paused) { ABStatus = name + " - paused"; yield return null; continue; }
                    ABStatus = $"{name} - measuring {_current.ElapsedSeconds:0}/{s.abTestSeconds:0} s";
                    ABProgress = (scenario + 0.15f + 0.85f * _current.ElapsedSeconds / s.abTestSeconds) / 2f;
                    yield return null;
                }
                results[scenario] = _current.Snapshot();
            }
            Before = results[0];
            After = results[1];
            UpdateComparison("A/B test: fixed-time (A) vs adaptive (B), same seed");
            ABRunning = false;
            ABProgress = 1f;
            ABStatus = "A/B test finished";
            _sim.SetSimulationSpeed(previousSpeed);
            _comparingLive = false;
            _current.Reset();
            CityEvents.RaiseAlert("A/B test finished: " + ComparisonVerdict, LastComparisonImproved ? AlertLevel.Success : AlertLevel.Warning);
            // Leave the adaptive scenario running so the audience sees the optimized city.
            _sim.SetOptimizationSilently(true);
        }
    }
}
