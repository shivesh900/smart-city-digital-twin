using System.Collections.Generic;

namespace SmartCity
{
    public enum RecommendationPriority { Info, Advisory, Warning, Critical }

    public sealed class Recommendation
    {
        public RecommendationPriority Priority;
        public string Title;
        public string Reason;
        public string Action;
    }

    /// <summary>One snapshot of the city state, assembled by AnalyticsManager.</summary>
    public sealed class CityStateSnapshot
    {
        public float Congestion;              // 0..1
        public float ForecastCongestion;      // 0..1
        public float ForecastHorizonSeconds;
        public string Trend = "Stable";
        public bool OptimizationEnabled;
        public string WorstIntersection;
        public int WorstIntersectionQueue;
        public float MaxAqi;
        public string MaxAqiZone;
        public float AqiWarning = 200f;
        public float AqiCritical = 300f;
        public float EnergyMW;
        public float EnergyPeakMW;
        public int ActiveIncidents;
        public int UndispatchedIncidents;
        public string MostSevereIncident;
        public SeverityLevel MostSevereLevel;
        public int BlockedRoads;
        public bool LastComparisonImproved;
        public bool HasComparison;
        public bool StreetlightsOn;
        public int ActiveVehicles;
        public int MaxVehicles;
    }

    /// <summary>
    /// Rule-based decision support. Every recommendation carries the rule that fired and the measured
    /// values behind it, so the panel can explain WHY an action is suggested.
    /// </summary>
    public static class RecommendationEngine
    {
        public static List<Recommendation> Evaluate(CityStateSnapshot s)
        {
            var list = new List<Recommendation>();

            if (s.UndispatchedIncidents > 0)
                list.Add(new Recommendation
                {
                    Priority = s.MostSevereLevel >= SeverityLevel.High ? RecommendationPriority.Critical : RecommendationPriority.Warning,
                    Title = "Dispatch emergency services",
                    Reason = $"{s.UndispatchedIncidents} incident(s) without a responder; most severe: {s.MostSevereIncident} ({s.MostSevereLevel}).",
                    Action = "Select the incident and press DISPATCH. The nearest available unit is routed with A*, avoiding blocked roads."
                });

            if (!s.OptimizationEnabled && (s.Congestion >= 0.35f || s.WorstIntersectionQueue >= 6))
                list.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Warning,
                    Title = "Enable adaptive signal timing",
                    Reason = $"Congestion {s.Congestion * 100f:0}% and the longest queue is {s.WorstIntersectionQueue} vehicles at {s.WorstIntersection} under fixed-time signals.",
                    Action = "Turn on OPTIMIZATION: green time is shifted toward the longer queues (queue-proportional split + gap-out)."
                });

            if (s.ForecastCongestion - s.Congestion >= 0.08f)
                list.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Advisory,
                    Title = "Congestion expected to rise",
                    Reason = $"Baseline forecast (moving average + linear trend) predicts {s.ForecastCongestion * 100f:0}% in {s.ForecastHorizonSeconds:0}s vs {s.Congestion * 100f:0}% now (trend: {s.Trend}).",
                    Action = s.OptimizationEnabled ? "Keep optimization on and watch the worst intersection." : "Enable optimization before queues build up."
                });

            if (s.MaxAqi >= s.AqiCritical)
                list.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Critical,
                    Title = "Air-quality emergency measures",
                    Reason = $"Simulated AQI {s.MaxAqi:0} in {s.MaxAqiZone} exceeds the critical threshold ({s.AqiCritical:0}).",
                    Action = "Divert heavy vehicles away from the zone, issue a health advisory, inspect industrial sources."
                });
            else if (s.MaxAqi >= s.AqiWarning)
                list.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Warning,
                    Title = "Reduce emissions in hotspot",
                    Reason = $"Simulated AQI {s.MaxAqi:0} in {s.MaxAqiZone} is above the warning threshold ({s.AqiWarning:0}).",
                    Action = "Reroute through traffic around the zone; adaptive signals reduce idling emissions."
                });

            if (s.BlockedRoads > 0)
                list.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Advisory,
                    Title = "Detours active",
                    Reason = $"{s.BlockedRoads} road segment(s) blocked; vehicles re-plan with A* at their next intersection.",
                    Action = "Resolve the blocking incident to restore the normal network."
                });

            if (s.EnergyPeakMW > 0f && s.EnergyMW >= 0.9f * s.EnergyPeakMW)
                list.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Advisory,
                    Title = "Energy demand near peak",
                    Reason = $"Simulated demand {s.EnergyMW:0.00} MW is within 10% of today's simulated peak ({s.EnergyPeakMW:0.00} MW).",
                    Action = s.StreetlightsOn ? "Dim streetlights on empty roads; shift non-critical commercial loads." : "Shift non-critical commercial loads off-peak."
                });

            if (s.HasComparison && s.LastComparisonImproved && !s.OptimizationEnabled)
                list.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Info,
                    Title = "Evidence supports optimization",
                    Reason = "The last before/after comparison measured an improvement with adaptive timing.",
                    Action = "Keep adaptive timing enabled during peaks."
                });

            if (list.Count == 0)
                list.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Info,
                    Title = "City operating normally",
                    Reason = $"Congestion {s.Congestion * 100f:0}%, max AQI {s.MaxAqi:0}, {s.ActiveIncidents} active incidents.",
                    Action = "No action needed. Try triggering an incident to see the response workflow."
                });

            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return list;
        }
    }
}
