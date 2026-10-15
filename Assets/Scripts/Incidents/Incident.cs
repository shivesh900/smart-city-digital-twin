using UnityEngine;

namespace SmartCity
{
    public enum IncidentStatus { Active, Dispatched, OnScene, Resolved }

    /// <summary>One simulated incident with its location, effects, severity and response state.</summary>
    public sealed class Incident
    {
        public int Id;
        public IncidentKind Kind;
        public IncidentStatus Status;
        public Vector3 Position;
        public RoadSegment Segment;         // accident / blocked road
        public int NodeId = -1;             // signal failure / emergency request
        public Vector2Int Cell;             // pollution spike
        public float CreatedAt;
        public float DispatchedAt = -1f;
        public float ArrivedAt = -1f;
        public float ResponseSeconds = -1f;
        public SeverityResult Severity;
        public string SuggestedResponse;
        public string RouteNote;
        public PathResult Detour;
        public EmergencyUnit AssignedUnit;
        public GameObject Marker;
        public GameObject Props;
        public LineRenderer DetourLine;

        public string Title => $"#{Id} {SeverityClassifier.Describe(Kind)}";

        public string Location(RoadNetworkManager net)
        {
            if (Segment != null) return Segment.Name;
            if (NodeId >= 0) return "Junction " + net.NodeName(NodeId);
            return $"Block {(char)('A' + Cell.x)}{Cell.y + 1}";
        }

        /// <summary>Emergency unit types needed for this incident (rule table, see README).</summary>
        public VehicleKind RequiredUnit
        {
            get
            {
                switch (Kind)
                {
                    case IncidentKind.Accident: return VehicleKind.Ambulance;
                    case IncidentKind.EmergencyRequest: return VehicleKind.Ambulance;
                    case IncidentKind.PollutionSpike: return VehicleKind.FireTruck;   // hazmat inspection
                    default: return VehicleKind.Police;                             // traffic control
                }
            }
        }
    }
}
