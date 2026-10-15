using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    public enum LightState { Red, Yellow, Green, FailedFlashing }

    /// <summary>
    /// Signal controller for one intersection: two signal groups (north-south and east-west) cycling
    /// green -> yellow -> all-red. Fixed-time by default; when optimization is enabled each cycle's green
    /// split comes from <see cref="TrafficOptimizer"/> and empty greens gap out early.
    /// Also supports failure (flashing yellow, treated as all-way stop) and emergency pre-emption.
    /// </summary>
    public class TrafficLightController : MonoBehaviour
    {
        public enum Phase { GreenNS, YellowNS, AllRedNS, GreenEW, YellowEW, AllRedEW }

        public Intersection Junction { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public float PhaseElapsed { get; private set; }
        public float PhaseDuration { get; private set; }
        public SignalTimingPlan CurrentPlan { get; private set; }
        public bool Failed { get; private set; }
        public bool LastCycleAdaptive { get; private set; }
        public bool Preempted => _preemptTimer > 0f;

        // Live measurements (written by TrafficSimulationManager)
        public int QueueNS, QueueEW, ApproachNS, ApproachEW;
        public float ThroughputPerMinute { get; private set; }
        public float AverageWaitSeconds { get; private set; }
        public int GapOuts { get; private set; }
        public int CyclesRun { get; private set; }

        public int TotalQueue => QueueNS + QueueEW;

        private SimulationSettings _s;
        private TrafficOptimizer _optimizer;
        private bool _optimize;
        private float _initialOffset;
        private SignalAxis _preemptAxis;
        private float _preemptTimer;
        private float _flashTimer;
        private bool _flashOn;
        private int _crossings;
        private float _crossWindow;

        private sealed class Head
        {
            public SignalAxis Axis;
            public Renderer Red, Yellow, Green;
        }
        private readonly List<Head> _heads = new List<Head>();
        private Material _redOn, _redOff, _yellowOn, _yellowOff, _greenOn, _greenOff;

        public void Initialize(Intersection junction, RoadNetworkManager net, SimulationSettings s, TrafficOptimizer optimizer, float startOffset)
        {
            Junction = junction;
            _s = s;
            _optimizer = optimizer;
            _initialOffset = startOffset;
            _redOn = MaterialLibrary.Get("sigRedOn"); _redOff = MaterialLibrary.Get("sigRedOff");
            _yellowOn = MaterialLibrary.Get("sigYellowOn"); _yellowOff = MaterialLibrary.Get("sigYellowOff");
            _greenOn = MaterialLibrary.Get("sigGreenOn"); _greenOff = MaterialLibrary.Get("sigGreenOff");
            BuildVisuals(net, s);
            ResetSignal();
        }

        public void ResetSignal()
        {
            Failed = false;
            _preemptTimer = 0f;
            CurrentPlan = _optimizer.Baseline();
            CurrentPhase = Phase.GreenNS;
            PhaseDuration = CurrentPlan.GreenNS;
            PhaseElapsed = 0f;
            QueueNS = QueueEW = ApproachNS = ApproachEW = 0;
            ThroughputPerMinute = 0f; AverageWaitSeconds = 0f; GapOuts = 0; CyclesRun = 0;
            _crossings = 0; _crossWindow = 0f;
            // Fast-forward through the start offset so the city's signals are staggered.
            float t = _initialOffset;
            while (t > 0f)
            {
                float left = PhaseDuration - PhaseElapsed;
                if (t < left) { PhaseElapsed += t; break; }
                t -= left;
                Advance(false);
            }
            ApplyVisuals();
        }

        public void Step(float dt, bool optimize)
        {
            _optimize = optimize;
            _crossWindow += dt;
            if (_crossWindow >= 10f)
            {
                ThroughputPerMinute = Mathf.Lerp(ThroughputPerMinute, _crossings * 6f, ThroughputPerMinute <= 0f ? 1f : 0.35f);
                _crossings = 0; _crossWindow = 0f;
            }

            if (Failed)
            {
                _flashTimer += dt;
                if (_flashTimer >= 0.5f) { _flashTimer = 0f; _flashOn = !_flashOn; ApplyVisuals(); }
                return;
            }

            PhaseElapsed += dt;

            if (_preemptTimer > 0f)
            {
                _preemptTimer -= dt;
                Phase wanted = _preemptAxis == SignalAxis.NorthSouth ? Phase.GreenNS : Phase.GreenEW;
                Phase other = _preemptAxis == SignalAxis.NorthSouth ? Phase.GreenEW : Phase.GreenNS;
                if (CurrentPhase == wanted)
                {
                    if (PhaseElapsed > PhaseDuration - 0.5f) PhaseElapsed = PhaseDuration - 0.5f; // hold green
                    return;
                }
                if (CurrentPhase == other && PhaseElapsed > 1f) { Advance(optimize); return; }
            }
            else if (optimize && (CurrentPhase == Phase.GreenNS || CurrentPhase == Phase.GreenEW))
            {
                bool ns = CurrentPhase == Phase.GreenNS;
                if (_optimizer.ShouldGapOut(PhaseElapsed, ns ? ApproachNS : ApproachEW, ns ? QueueEW : QueueNS))
                {
                    GapOuts++;
                    Advance(optimize);
                    return;
                }
            }

            if (PhaseElapsed >= PhaseDuration) Advance(optimize);
        }

        private void Advance(bool optimize)
        {
            CurrentPhase = (Phase)(((int)CurrentPhase + 1) % 6);
            PhaseElapsed = 0f;
            switch (CurrentPhase)
            {
                case Phase.GreenNS:
                    CyclesRun++;
                    LastCycleAdaptive = optimize;
                    CurrentPlan = optimize ? _optimizer.ComputePlan(ApproachNS, ApproachEW) : _optimizer.Baseline();
                    PhaseDuration = CurrentPlan.GreenNS;
                    break;
                case Phase.GreenEW:
                    if (optimize)
                    {
                        // Re-balance the second half of the cycle with fresh measurements.
                        var p = _optimizer.ComputePlan(ApproachNS, ApproachEW);
                        CurrentPlan = new SignalTimingPlan { GreenNS = CurrentPlan.GreenNS, GreenEW = p.GreenEW, Reason = p.Reason };
                    }
                    PhaseDuration = CurrentPlan.GreenEW;
                    break;
                case Phase.YellowNS:
                case Phase.YellowEW:
                    PhaseDuration = _s.yellowTime;
                    break;
                default:
                    PhaseDuration = _s.allRedTime;
                    break;
            }
            ApplyVisuals();
        }

        public LightState StateFor(SignalAxis axis)
        {
            if (Failed) return LightState.FailedFlashing;
            switch (CurrentPhase)
            {
                case Phase.GreenNS: return axis == SignalAxis.NorthSouth ? LightState.Green : LightState.Red;
                case Phase.YellowNS: return axis == SignalAxis.NorthSouth ? LightState.Yellow : LightState.Red;
                case Phase.GreenEW: return axis == SignalAxis.EastWest ? LightState.Green : LightState.Red;
                case Phase.YellowEW: return axis == SignalAxis.EastWest ? LightState.Yellow : LightState.Red;
                default: return LightState.Red;
            }
        }

        public float RemainingInPhase => Mathf.Max(0f, PhaseDuration - PhaseElapsed);

        public string PhaseLabel
        {
            get
            {
                if (Failed) return "FAILED - flashing yellow (all-way stop)";
                switch (CurrentPhase)
                {
                    case Phase.GreenNS: return "Green north-south";
                    case Phase.YellowNS: return "Yellow north-south";
                    case Phase.GreenEW: return "Green east-west";
                    case Phase.YellowEW: return "Yellow east-west";
                    default: return "All red (clearance)";
                }
            }
        }

        public void SetFailed(bool failed)
        {
            Failed = failed;
            _flashTimer = 0f; _flashOn = true;
            if (!failed) { CurrentPhase = Phase.AllRedEW; PhaseElapsed = 0f; PhaseDuration = _s.allRedTime; }
            ApplyVisuals();
        }

        public void RequestPreemption(SignalAxis axis, float seconds)
        {
            if (Failed) return;
            _preemptAxis = axis;
            _preemptTimer = Mathf.Max(_preemptTimer, seconds);
        }

        public void RegisterCrossing(float waitSeconds)
        {
            _crossings++;
            AverageWaitSeconds = Mathf.Lerp(AverageWaitSeconds, waitSeconds, 0.1f);
        }

        // ------------------------------------------------------------------ visuals

        private void BuildVisuals(RoadNetworkManager net, SimulationSettings s)
        {
            var poleMat = MaterialLibrary.Get("pole");
            var headMat = MaterialLibrary.Get("signalHead");
            var cyl = PrimitiveMeshes.Get(PrimitiveType.Cylinder);
            var cube = PrimitiveMeshes.Get(PrimitiveType.Cube);
            var sphere = PrimitiveMeshes.Get(PrimitiveType.Sphere);
            foreach (var lane in Junction.Incoming)
            {
                Vector3 dir = lane.Direction;
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                Vector3 side = s.driveOnLeft ? -right : right;
                Vector3 basePos = Junction.Position - dir * (net.RoadWidth * 0.5f + 1.2f) + side * (net.RoadWidth * 0.5f + 1.0f);
                var root = new GameObject($"Signal {lane.Axis}").transform;
                root.SetParent(transform, false);
                root.position = basePos;
                root.rotation = Quaternion.LookRotation(-dir, Vector3.up);

                Part(root, cyl, poleMat, new Vector3(0f, 2.6f, 0f), new Vector3(0.22f, 2.6f, 0.22f));
                Part(root, cube, headMat, new Vector3(0f, 5.6f, 0.05f), new Vector3(0.7f, 1.9f, 0.45f));
                var head = new Head { Axis = lane.Axis };
                head.Red = Part(root, sphere, _redOff, new Vector3(0f, 6.15f, 0.28f), Vector3.one * 0.42f);
                head.Yellow = Part(root, sphere, _yellowOff, new Vector3(0f, 5.6f, 0.28f), Vector3.one * 0.42f);
                head.Green = Part(root, sphere, _greenOff, new Vector3(0f, 5.05f, 0.28f), Vector3.one * 0.42f);
                _heads.Add(head);
            }
        }

        private static Renderer Part(Transform parent, Mesh mesh, Material mat, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        private void ApplyVisuals()
        {
            foreach (var h in _heads)
            {
                var st = StateFor(h.Axis);
                h.Red.sharedMaterial = st == LightState.Red ? _redOn : _redOff;
                h.Yellow.sharedMaterial = st == LightState.Yellow || (st == LightState.FailedFlashing && _flashOn) ? _yellowOn : _yellowOff;
                h.Green.sharedMaterial = st == LightState.Green ? _greenOn : _greenOff;
            }
        }
    }
}
