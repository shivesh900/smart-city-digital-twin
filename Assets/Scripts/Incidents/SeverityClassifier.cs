using System.Collections.Generic;

namespace SmartCity
{
    public enum IncidentKind { Accident, BlockedRoad, SignalFailure, PollutionSpike, EmergencyRequest }

    public enum SeverityLevel { Low, Medium, High, Critical }

    /// <summary>Facts about an incident's surroundings, gathered from the simulation.</summary>
    public struct IncidentContext
    {
        public IncidentKind Kind;
        public int VehiclesNearby;
        public float LocalAqi;
        public bool NearCriticalFacility;
        public bool PeakHour;
        public bool ResponderRouteAvailable;
        public int NearbyBlockedRoads;
    }

    public sealed class SeverityResult
    {
        public SeverityLevel Level;
        public int Score;
        public readonly List<string> Reasons = new List<string>();
    }

    /// <summary>
    /// Explicit, documented, rule-based severity classification (no machine learning).
    ///
    ///  R1 Base score by type: Accident 3, Blocked road 2, Signal failure 2, Pollution spike 2, Emergency request 4.
    ///  R2 Vehicles within 60 m: >= 12 -> +2, >= 5 -> +1.
    ///  R3 Local simulated AQI: >= 300 -> +2, >= 200 -> +1.
    ///  R4 Within 90 m of a hospital, police or fire station -> +1 (critical access).
    ///  R5 Peak hour (08:00-10:00 or 17:00-20:00 simulated time) -> +1.
    ///  R6 No route for responders -> +2.
    ///  R7 Two or more blocked roads nearby -> +1.
    ///  Level: score <= 2 Low, 3-4 Medium, 5-6 High, >= 7 Critical.
    /// </summary>
    public static class SeverityClassifier
    {
        public static int BaseScore(IncidentKind kind)
        {
            switch (kind)
            {
                case IncidentKind.Accident: return 3;
                case IncidentKind.BlockedRoad: return 2;
                case IncidentKind.SignalFailure: return 2;
                case IncidentKind.PollutionSpike: return 2;
                case IncidentKind.EmergencyRequest: return 4;
                default: return 1;
            }
        }

        public static SeverityLevel LevelForScore(int score)
        {
            if (score >= 7) return SeverityLevel.Critical;
            if (score >= 5) return SeverityLevel.High;
            if (score >= 3) return SeverityLevel.Medium;
            return SeverityLevel.Low;
        }

        public static SeverityResult Classify(IncidentContext c)
        {
            var r = new SeverityResult();
            int score = BaseScore(c.Kind);
            r.Reasons.Add($"R1 base score for {Describe(c.Kind)}: {score}");

            if (c.VehiclesNearby >= 12) { score += 2; r.Reasons.Add($"R2 {c.VehiclesNearby} vehicles nearby: +2"); }
            else if (c.VehiclesNearby >= 5) { score += 1; r.Reasons.Add($"R2 {c.VehiclesNearby} vehicles nearby: +1"); }

            if (c.LocalAqi >= 300f) { score += 2; r.Reasons.Add($"R3 local AQI {c.LocalAqi:0} (>=300): +2"); }
            else if (c.LocalAqi >= 200f) { score += 1; r.Reasons.Add($"R3 local AQI {c.LocalAqi:0} (>=200): +1"); }

            if (c.NearCriticalFacility) { score += 1; r.Reasons.Add("R4 near hospital/police/fire station: +1"); }
            if (c.PeakHour) { score += 1; r.Reasons.Add("R5 peak hour: +1"); }
            if (!c.ResponderRouteAvailable) { score += 2; r.Reasons.Add("R6 no route for responders: +2"); }
            if (c.NearbyBlockedRoads >= 2) { score += 1; r.Reasons.Add($"R7 {c.NearbyBlockedRoads} blocked roads nearby: +1"); }

            r.Score = score;
            r.Level = LevelForScore(score);
            return r;
        }

        public static bool IsPeakHour(float hourOfDay)
        {
            float h = hourOfDay % 24f;
            if (h < 0) h += 24f;
            return (h >= 8f && h < 10f) || (h >= 17f && h < 20f);
        }

        public static string Describe(IncidentKind kind)
        {
            switch (kind)
            {
                case IncidentKind.Accident: return "Road accident";
                case IncidentKind.BlockedRoad: return "Blocked road";
                case IncidentKind.SignalFailure: return "Signal failure";
                case IncidentKind.PollutionSpike: return "Pollution spike";
                case IncidentKind.EmergencyRequest: return "Emergency request";
                default: return kind.ToString();
            }
        }
    }
}
