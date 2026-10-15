using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    public enum SignalAxis { NorthSouth, EastWest }

    public enum RouteMode { Shortest, Fastest, LeastCongested }

    /// <summary>One directed lane (= one graph edge) with its geometry and the vehicles on it, front first.</summary>
    public sealed class Lane
    {
        public int EdgeId;
        public RoadGraph.Edge Edge;
        public int From;
        public int To;
        public Vector3 Start;
        public Vector3 End;
        public Vector3 Direction;
        public float Length;
        public SignalAxis Axis;
        public RoadSegment Segment;
        /// <summary>Index 0 is the vehicle closest to the end of the lane.</summary>
        public readonly List<VehicleController> Vehicles = new List<VehicleController>();

        // Incident speed restriction (accident): vehicles between CapFrom and CapTo (metres along lane) slow down.
        public bool CapActive;
        public float CapFrom;
        public float CapTo;
        public float CapSpeed;

        // Live statistics (updated by TrafficSimulationManager)
        public float Density;
        public float AverageSpeed;
        public int Queue;
        public int ApproachCount;
        public float Congestion;

        public Renderer OverlayRenderer;

        public Vector3 PointAt(float s) => Start + Direction * s;
        public bool Blocked => Edge.Blocked;
    }

    /// <summary>An undirected road between two intersections: the two opposite lanes.</summary>
    public sealed class RoadSegment
    {
        public int Index;
        public Lane Forward;
        public Lane Backward;
        public int NodeA;
        public int NodeB;
        public Vector3 Center;
        public bool Horizontal;
        public float Length;
        public string Name;
        public GameObject GameObject;
        public bool Blocked => Forward.Edge.Blocked;
        public float Congestion => Mathf.Max(Forward.Congestion, Backward.Congestion);
        public int VehicleCount => Forward.Vehicles.Count + Backward.Vehicles.Count;
    }

    /// <summary>A signalised intersection.</summary>
    public sealed class Intersection
    {
        public int NodeId;
        public int GridX;
        public int GridY;
        public Vector3 Position;
        public string Name;
        public TrafficLightController Signal;
        public readonly List<Lane> Incoming = new List<Lane>();
        public readonly List<Lane> Outgoing = new List<Lane>();
        public GameObject GameObject;
    }
}
