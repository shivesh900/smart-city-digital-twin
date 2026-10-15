using System;
using System.Collections.Generic;

namespace SmartCity
{
    /// <summary>Aggregated traffic performance over a time window of the simulation.</summary>
    public struct TrafficMetrics
    {
        public float DurationSeconds;
        public float AvgSpeedKmh;
        public float AvgQueueLength;          // vehicles queued at stop lines (whole city, time-averaged)
        public float AvgStoppedShare;         // share of vehicles stopped (0..1)
        public float AvgTripSeconds;          // completed trips only
        public int CompletedTrips;
        public float TripsPerMinute;
        public float AvgCongestion;           // 0..1

        public bool HasData => DurationSeconds > 0.5f;
    }

    /// <summary>Time-weighted accumulator that produces <see cref="TrafficMetrics"/>.</summary>
    public sealed class MetricsAccumulator
    {
        private float _time, _speed, _queue, _stopped, _cong, _tripSum;
        private int _trips;

        public float ElapsedSeconds => _time;

        public void AddSample(float dt, float avgSpeedKmh, float queueLength, float stoppedShare, float congestion)
        {
            if (dt <= 0f) return;
            _time += dt;
            _speed += avgSpeedKmh * dt;
            _queue += queueLength * dt;
            _stopped += stoppedShare * dt;
            _cong += congestion * dt;
        }

        public void AddTrip(float seconds)
        {
            if (seconds <= 0f) return;
            _trips++;
            _tripSum += seconds;
        }

        public TrafficMetrics Snapshot()
        {
            var m = new TrafficMetrics { DurationSeconds = _time, CompletedTrips = _trips };
            if (_time > 0f)
            {
                m.AvgSpeedKmh = _speed / _time;
                m.AvgQueueLength = _queue / _time;
                m.AvgStoppedShare = _stopped / _time;
                m.AvgCongestion = _cong / _time;
                m.TripsPerMinute = _trips / (_time / 60f);
            }
            m.AvgTripSeconds = _trips > 0 ? _tripSum / _trips : 0f;
            return m;
        }

        public void Reset() { _time = _speed = _queue = _stopped = _cong = _tripSum = 0f; _trips = 0; }
    }

    public sealed class ComparisonRow
    {
        public string Name;
        public string Unit;
        public float Before;
        public float After;
        public bool LowerIsBetter;
        public float DeltaPercent => Math.Abs(Before) < 1e-4f ? 0f : (After - Before) / Math.Abs(Before) * 100f;
        /// <summary>+1 improved, -1 worse, 0 change within the +/-3% noise band.</summary>
        public int Outcome
        {
            get
            {
                float d = DeltaPercent;
                if (Math.Abs(d) < 3f) return 0;
                bool better = LowerIsBetter ? d < 0f : d > 0f;
                return better ? 1 : -1;
            }
        }
    }

    /// <summary>Before/after comparison of two scenarios with an honest, rule-based verdict.</summary>
    public static class ScenarioComparison
    {
        public static List<ComparisonRow> Compare(TrafficMetrics before, TrafficMetrics after)
        {
            return new List<ComparisonRow>
            {
                new ComparisonRow { Name = "Avg queue at signals", Unit = "veh", Before = before.AvgQueueLength, After = after.AvgQueueLength, LowerIsBetter = true },
                new ComparisonRow { Name = "Avg trip time", Unit = "s", Before = before.AvgTripSeconds, After = after.AvgTripSeconds, LowerIsBetter = true },
                new ComparisonRow { Name = "Avg speed", Unit = "km/h", Before = before.AvgSpeedKmh, After = after.AvgSpeedKmh, LowerIsBetter = false },
                new ComparisonRow { Name = "Vehicles stopped", Unit = "%", Before = before.AvgStoppedShare * 100f, After = after.AvgStoppedShare * 100f, LowerIsBetter = true },
                new ComparisonRow { Name = "Trips completed", Unit = "/min", Before = before.TripsPerMinute, After = after.TripsPerMinute, LowerIsBetter = false },
            };
        }

        public static string Verdict(List<ComparisonRow> rows, TrafficMetrics before, TrafficMetrics after)
        {
            if (!before.HasData || !after.HasData) return "Not enough data yet - let both scenarios run.";
            int better = 0, worse = 0;
            foreach (var r in rows) { if (r.Outcome > 0) better++; else if (r.Outcome < 0) worse++; }
            string sample = $"(based on {before.DurationSeconds:0}s vs {after.DurationSeconds:0}s of simulated time, {before.CompletedTrips}/{after.CompletedTrips} trips)";
            if (better > worse && better >= 2) return $"Optimization IMPROVED {better} of {rows.Count} metrics {sample}.";
            if (worse > better) return $"Optimization did NOT help in this run: {worse} metrics worse {sample}.";
            return $"No clear difference between scenarios {sample}.";
        }
    }
}
