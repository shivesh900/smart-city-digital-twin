using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    public enum VehicleKind { Car, Bus, Truck, Ambulance, FireTruck, Police }

    /// <summary>
    /// State and visual pose of one road vehicle. Driving decisions (car-following with the Intelligent
    /// Driver Model, signals, lane changes at intersections) are made by <see cref="TrafficSimulationManager"/>
    /// so all vehicles update in one deterministic pass.
    /// S is the position of the FRONT bumper along the current lane; negative S means the vehicle is still
    /// crossing the intersection before that lane.
    /// </summary>
    public class VehicleController : MonoBehaviour
    {
        public VehicleKind Kind;
        public float Length = 4.2f;
        public float DesiredSpeed = 13.9f;
        public float MaxAccel = 1.6f;
        public float ComfortDecel = 2.6f;
        public float MinGap = 2.0f;
        public float Headway = 1.2f;
        public int ColorIndex;

        [System.NonSerialized] public Lane Lane;
        [System.NonSerialized] public float S;
        [System.NonSerialized] public float Speed;
        [System.NonSerialized] public float Accel;
        [System.NonSerialized] public readonly List<int> Route = new List<int>();
        [System.NonSerialized] public int RouteIndex;
        [System.NonSerialized] public int Destination;
        [System.NonSerialized] public bool TripCompletePending;
        [System.NonSerialized] public float TripStartTime;
        [System.NonSerialized] public float WaitTimer;
        [System.NonSerialized] public float StuckTimer;
        [System.NonSerialized] public float StopHold;
        [System.NonSerialized] public int GraphVersion = -1;
        [System.NonSerialized] public Vector3 CurveStart;
        [System.NonSerialized] public Vector3 CurveControl;
        [System.NonSerialized] public bool HasCurve;
        [System.NonSerialized] public float SpawnScale;

        public Vector3 WorldPosition { get; private set; }

        private Transform _t;

        private void Awake() { _t = transform; }

        public void ResetState()
        {
            Lane = null; S = 0f; Speed = 0f; Accel = 0f;
            Route.Clear(); RouteIndex = 0; Destination = -1;
            TripCompletePending = false; WaitTimer = 0f; StuckTimer = 0f; StopHold = 0f;
            GraphVersion = -1; HasCurve = false; SpawnScale = 0f;
        }

        public int NextEdge => RouteIndex + 1 < Route.Count ? Route[RouteIndex + 1] : -1;

        /// <summary>Places the transform on the lane (or on the curve through the intersection).</summary>
        public void ApplyPose(float junctionLength, float dt)
        {
            if (Lane == null) return;
            if (_t == null) _t = transform;
            float center = S - Length * 0.5f;
            Vector3 pos, fwd;
            if (center >= 0f || !HasCurve)
            {
                pos = Lane.Start + Lane.Direction * Mathf.Max(0f, center);
                fwd = Lane.Direction;
            }
            else
            {
                float t = Mathf.Clamp01((center + junctionLength) / junctionLength);
                Vector3 p0 = CurveStart, p1 = CurveControl, p2 = Lane.Start;
                float u = 1f - t;
                pos = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                fwd = 2f * u * (p1 - p0) + 2f * t * (p2 - p1);
                if (fwd.sqrMagnitude < 1e-4f) fwd = Lane.Direction;
            }
            WorldPosition = pos;
            _t.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd.normalized, Vector3.up));
            if (SpawnScale < 1f)
            {
                SpawnScale = Mathf.Min(1f, SpawnScale + dt * 2.5f);
                _t.localScale = Vector3.one * Mathf.SmoothStep(0.2f, 1f, SpawnScale);
            }
        }

        public void SnapScale() { SpawnScale = 1f; if (_t == null) _t = transform; _t.localScale = Vector3.one; }
    }
}
