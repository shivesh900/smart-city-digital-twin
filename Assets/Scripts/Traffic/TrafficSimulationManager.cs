using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Microscopic traffic simulation.
    ///  - Car following: Intelligent Driver Model (keeps a safe gap -> collision avoidance).
    ///  - Signals: vehicles stop for red, decide on yellow by stopping distance, all-way stop at failed signals.
    ///  - Spill-back protection: a vehicle only enters an intersection if its exit lane has room.
    ///  - Routing: A* (fastest free-flow time, incident-aware); re-plans whenever the network changes.
    ///  - Statistics: speed, congestion, queues per lane and per signal group.
    /// </summary>
    public class TrafficSimulationManager : MonoBehaviour
    {
        public readonly List<VehicleController> Vehicles = new List<VehicleController>();

        public int ActiveVehicles => Vehicles.Count;
        public float AverageSpeedKmh { get; private set; }
        /// <summary>City congestion 0..1: mean of (1 - speed / desired speed) over all vehicles, smoothed.</summary>
        public float Congestion { get; private set; }
        public float StoppedShare { get; private set; }
        public int TotalQueue { get; private set; }
        public int CompletedTrips { get; private set; }
        public int Reroutes { get; private set; }
        public int Recycled { get; private set; }
        public float LastTripSeconds { get; private set; }

        public event Action<float> TripCompleted;

        private SimulationController _sim;
        private RoadNetworkManager _net;
        private SimulationSettings _s;
        private VehiclePool _pool;
        private Transform _root;
        private float _statsTimer;
        private float _congestionInstant;
        private bool _statsInitialized;

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            _net = sim.roadNetwork;
            _s = sim.settings;
            if (_root == null)
            {
                _root = new GameObject("Vehicles").transform;
                _root.SetParent(transform, false);
            }
            _pool = new VehiclePool(_root);
        }

        public void SpawnInitialVehicles()
        {
            SpawnVehicles(_s.initialVehicles, true);
            SyncVisuals();
            foreach (var v in Vehicles) v.SnapScale();
        }

        public void ResetTraffic()
        {
            for (int i = Vehicles.Count - 1; i >= 0; i--) _pool.Return(Vehicles[i]);
            Vehicles.Clear();
            foreach (var lane in _net.Lanes) lane.Vehicles.Clear();
            AverageSpeedKmh = 0f; Congestion = 0f; StoppedShare = 0f; TotalQueue = 0;
            CompletedTrips = 0; Reroutes = 0; Recycled = 0; LastTripSeconds = 0f;
            _statsInitialized = false;
            SpawnInitialVehicles();
            UpdateStatistics();
        }

        /// <summary>Spawns up to <paramref name="count"/> vehicles (respecting maxVehicles). Returns how many were placed.</summary>
        public int SpawnVehicles(int count, bool initial = false)
        {
            int placed = 0;
            int attempts = 0;
            while (placed < count && Vehicles.Count < _s.maxVehicles && attempts < count * 25)
            {
                attempts++;
                var lane = _net.Lanes[_sim.Random.Next(_net.Lanes.Count)];
                if (lane.Blocked) continue;
                VehicleKind kind = PickKind();
                int color = _sim.Random.Next(6);
                float len = VehicleFactory.LengthOf(kind);
                float s = initial ? _sim.RandomRange(len + 1f, lane.Length - 1f) : _sim.RandomRange(len + 1f, Mathf.Max(len + 1.5f, lane.Length * 0.5f));
                if (!HasRoom(lane, s, len)) continue;
                var v = _pool.Get(kind, color);
                v.ColorIndex = color;
                Configure(v);
                v.Lane = lane;
                v.S = s;
                v.Speed = v.DesiredSpeed * _sim.RandomRange(0.3f, 0.7f);
                v.TripStartTime = _sim.SimTime;
                v.Route.Add(lane.EdgeId);
                v.RouteIndex = 0;
                PlanTrip(v, lane.To, lane.EdgeId);
                Insert(lane, v);
                Vehicles.Add(v);
                placed++;
            }
            return placed;
        }

        private VehicleKind PickKind()
        {
            double r = _sim.Random.NextDouble();
            if (r < _s.busShare) return VehicleKind.Bus;
            if (r < _s.busShare + _s.truckShare) return VehicleKind.Truck;
            return VehicleKind.Car;
        }

        private void Configure(VehicleController v)
        {
            switch (v.Kind)
            {
                case VehicleKind.Bus: v.DesiredSpeed = _s.busSpeedKmh / 3.6f; v.MaxAccel = 1.0f; v.ComfortDecel = 2.0f; v.Headway = 1.5f; break;
                case VehicleKind.Truck: v.DesiredSpeed = _s.truckSpeedKmh / 3.6f; v.MaxAccel = 0.9f; v.ComfortDecel = 2.0f; v.Headway = 1.6f; break;
                default: v.DesiredSpeed = _s.CarSpeed * _sim.RandomRange(0.88f, 1.1f); v.MaxAccel = 1.7f; v.ComfortDecel = 2.8f; v.Headway = 1.15f; break;
            }
            v.MinGap = 2f;
        }

        private static bool HasRoom(Lane lane, float s, float len)
        {
            foreach (var o in lane.Vehicles)
            {
                // other occupies [o.S - o.Length, o.S]; new one [s - len, s]; need 3 m clearance
                if (s - len - 3f < o.S && o.S - o.Length - 3f < s) return false;
            }
            return true;
        }

        private static void Insert(Lane lane, VehicleController v)
        {
            int i = 0;
            while (i < lane.Vehicles.Count && lane.Vehicles[i].S > v.S) i++;
            lane.Vehicles.Insert(i, v);
        }

        /// <summary>Chooses a new destination from <paramref name="fromNode"/> and appends the A* route.</summary>
        private void PlanTrip(VehicleController v, int fromNode, int currentEdge)
        {
            var g = _net.Graph;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int dest;
                if (_net.DowntownNodes.Count > 0 && _sim.Random.NextDouble() < _s.downtownTripShare)
                    dest = _net.DowntownNodes[_sim.Random.Next(_net.DowntownNodes.Count)];
                else
                    dest = _sim.Random.Next(g.NodeCount);
                if (dest == fromNode) continue;
                var path = _net.FindRoute(fromNode, dest, RouteMode.Fastest);
                if (!path.Found || path.Edges.Count == 0) continue;
                SetRoute(v, currentEdge, path, dest);
                return;
            }
            UTurnOrStay(v, currentEdge);
        }

        private void SetRoute(VehicleController v, int currentEdge, PathResult path, int dest)
        {
            v.Route.Clear();
            v.Route.Add(currentEdge);
            v.Route.AddRange(path.Edges);
            v.RouteIndex = 0;
            v.Destination = dest;
            v.GraphVersion = _net.Graph.Version;
        }

        private void UTurnOrStay(VehicleController v, int currentEdge)
        {
            var e = _net.Graph.GetEdge(currentEdge);
            v.Route.Clear();
            v.Route.Add(currentEdge);
            if (e.ReverseId >= 0 && !_net.Graph.GetEdge(e.ReverseId).Blocked) v.Route.Add(e.ReverseId);
            v.RouteIndex = 0;
            v.Destination = e.From;
            v.GraphVersion = _net.Graph.Version;
        }

        private void Replan(VehicleController v, Lane lane)
        {
            Reroutes++;
            if (v.Destination >= 0 && v.Destination != lane.To)
            {
                var path = _net.FindRoute(lane.To, v.Destination, RouteMode.Fastest);
                if (path.Found && path.Edges.Count > 0) { SetRoute(v, lane.EdgeId, path, v.Destination); return; }
            }
            PlanTrip(v, lane.To, lane.EdgeId);
        }

        // ------------------------------------------------------------------ simulation step

        public void Step(float dt)
        {
            float J = _net.JunctionLength;
            var lanes = _net.Lanes;

            // 1) accelerations (IDM)
            for (int li = 0; li < lanes.Count; li++)
            {
                var lane = lanes[li];
                var list = lane.Vehicles;
                for (int i = 0; i < list.Count; i++)
                {
                    var v = list[i];
                    float vmax = v.DesiredSpeed;
                    if (lane.CapActive && v.S > lane.CapFrom - 25f && v.S - v.Length < lane.CapTo) vmax = Mathf.Min(vmax, lane.CapSpeed);
                    float gap = 1e4f, leaderSpeed = vmax;
                    if (i > 0)
                    {
                        var lead = list[i - 1];
                        gap = lead.S - lead.Length - v.S;
                        leaderSpeed = lead.Speed;
                    }
                    else
                    {
                        float dist = lane.Length - v.S;
                        Lane next;
                        if (!MayEnter(v, lane, dist, out next))
                        {
                            gap = dist; leaderSpeed = 0f;
                        }
                        else if (next != null && next.Vehicles.Count > 0)
                        {
                            var rear = next.Vehicles[next.Vehicles.Count - 1];
                            float g2 = dist + J + rear.S - rear.Length;
                            if (g2 < gap) { gap = g2; leaderSpeed = rear.Speed; }
                        }
                    }
                    v.Accel = Idm(v, vmax, gap, leaderSpeed);
                }
            }

            // 2) integrate, front to back, never overlapping the leader
            for (int li = 0; li < lanes.Count; li++)
            {
                var lane = lanes[li];
                var list = lane.Vehicles;
                for (int i = 0; i < list.Count; i++)
                {
                    var v = list[i];
                    v.Speed = Mathf.Max(0f, v.Speed + v.Accel * dt);
                    float newS = v.S + v.Speed * dt;
                    if (i > 0)
                    {
                        var lead = list[i - 1];
                        float maxS = lead.S - lead.Length - 0.5f;
                        if (newS > maxS) { newS = Mathf.Max(v.S, maxS); v.Speed = Mathf.Min(v.Speed, lead.Speed); }
                    }
                    v.S = newS;
                    if (v.Speed < 0.5f)
                    {
                        v.WaitTimer += dt; v.StuckTimer += dt;
                        if (i == 0 && lane.Length - v.S < 4f) v.StopHold += dt;
                    }
                    else v.StuckTimer = 0f;
                }
            }

            // 3) intersection crossings
            for (int li = 0; li < lanes.Count; li++)
            {
                var lane = lanes[li];
                int guard = 0;
                while (lane.Vehicles.Count > 0 && lane.Vehicles[0].S >= lane.Length && guard++ < 8)
                {
                    var v = lane.Vehicles[0];
                    Lane next;
                    if (MayEnter(v, lane, 0f, out next) && next != null)
                    {
                        Cross(v, lane, next, J);
                    }
                    else
                    {
                        v.S = lane.Length; v.Speed = 0f;
                        break;
                    }
                }
            }

            // 4) gridlock breaker: recycle a vehicle that has not moved for 90 simulated seconds
            for (int i = Vehicles.Count - 1; i >= 0; i--)
            {
                if (Vehicles[i].StuckTimer > 90f)
                {
                    Despawn(Vehicles[i]);
                    Recycled++;
                    SpawnVehicles(1);
                }
            }

            _statsTimer += dt;
            if (_statsTimer >= 0.25f) { _statsTimer = 0f; UpdateStatistics(); }
        }

        private static float Idm(VehicleController v, float vmax, float gap, float leaderSpeed)
        {
            float speed = v.Speed;
            float dv = speed - leaderSpeed;
            float sStar = v.MinGap + Mathf.Max(0f, speed * v.Headway + speed * dv / (2f * Mathf.Sqrt(v.MaxAccel * v.ComfortDecel)));
            float ratio = speed / Mathf.Max(0.1f, vmax);
            float free = 1f - ratio * ratio * ratio * ratio;
            float interaction = gap > 0.05f ? (sStar / gap) * (sStar / gap) : 400f;
            return Mathf.Clamp(v.MaxAccel * (free - interaction), -9f, v.MaxAccel);
        }

        /// <summary>Decides whether the front vehicle of a lane may enter the intersection now.</summary>
        private bool MayEnter(VehicleController v, Lane lane, float dist, out Lane next)
        {
            next = null;
            var g = _net.Graph;
            if (v.GraphVersion != g.Version) Replan(v, lane);
            if (v.RouteIndex >= v.Route.Count - 1 && !v.TripCompletePending)
            {
                // Arriving at the destination: the trip ends at this intersection and a new one begins.
                v.TripCompletePending = true;
                PlanTrip(v, lane.To, lane.EdgeId);
            }
            int nextEdge = v.NextEdge;
            if (nextEdge >= 0 && g.GetEdge(nextEdge).Blocked) { Replan(v, lane); nextEdge = v.NextEdge; }
            if (nextEdge < 0) return false;
            next = _net.Lanes[nextEdge];
            if (next.Blocked) return false;

            var signal = _net.Intersections[lane.To].Signal;
            switch (signal.StateFor(lane.Axis))
            {
                case LightState.Red:
                    return false;
                case LightState.Yellow:
                    float stopDistance = v.Speed * v.Speed / (2f * v.ComfortDecel);
                    if (dist > stopDistance + 0.5f) return false;
                    break;
                case LightState.FailedFlashing:
                    if (dist > 4f || v.StopHold < 1.0f) return false;
                    if (JunctionBusy(_net.Intersections[lane.To])) return false;
                    break;
            }

            if (next.Vehicles.Count > 0)
            {
                var rear = next.Vehicles[next.Vehicles.Count - 1];
                if (rear.S - rear.Length < -_net.JunctionLength + v.Length + 2f) return false; // do not block the box
            }
            return true;
        }

        private static bool JunctionBusy(Intersection j)
        {
            foreach (var l in j.Outgoing)
                for (int i = l.Vehicles.Count - 1; i >= 0; i--)
                {
                    if (l.Vehicles[i].S - l.Vehicles[i].Length < 0f) return true;
                    break;
                }
            return false;
        }

        private void Cross(VehicleController v, Lane lane, Lane next, float J)
        {
            lane.Vehicles.RemoveAt(0);
            _net.Intersections[lane.To].Signal.RegisterCrossing(v.WaitTimer);
            v.WaitTimer = 0f;
            v.StopHold = 0f;
            if (v.TripCompletePending)
            {
                v.TripCompletePending = false;
                float trip = _sim.SimTime - v.TripStartTime;
                v.TripStartTime = _sim.SimTime;
                CompletedTrips++;
                LastTripSeconds = trip;
                TripCompleted?.Invoke(trip);
            }
            // Quadratic curve through the intersection.
            v.CurveStart = lane.End;
            float dot = Vector3.Dot(lane.Direction, next.Direction);
            Vector3 nodePos = _net.NodePosition(lane.To);
            if (dot > 0.9f) v.CurveControl = (lane.End + next.Start) * 0.5f;
            else if (dot < -0.5f) v.CurveControl = nodePos + lane.Direction * (_net.RoadWidth * 0.6f);
            else v.CurveControl = lane.End + lane.Direction * Vector3.Dot(next.Start - lane.End, lane.Direction);
            v.HasCurve = true;
            v.S = -J + (v.S - lane.Length);
            v.Lane = next;
            v.RouteIndex++;
            next.Vehicles.Add(v);
        }

        public void Despawn(VehicleController v)
        {
            if (v.Lane != null) v.Lane.Vehicles.Remove(v);
            Vehicles.Remove(v);
            _pool.Return(v);
        }

        /// <summary>Removes up to <paramref name="count"/> vehicles (used when the user lowers density).</summary>
        public int RemoveVehicles(int count)
        {
            int removed = 0;
            for (int i = Vehicles.Count - 1; i >= 0 && removed < count; i--) { Despawn(Vehicles[i]); removed++; }
            return removed;
        }

        // ------------------------------------------------------------------ statistics & visuals

        private void UpdateStatistics()
        {
            float speedSum = 0f, congSum = 0f;
            int stopped = 0, total = 0, queue = 0;
            foreach (var lane in _net.Lanes)
            {
                int n = lane.Vehicles.Count;
                float laneSpeed = 0f, occupied = 0f;
                int q = 0;
                foreach (var v in lane.Vehicles)
                {
                    laneSpeed += v.Speed;
                    occupied += v.Length + v.MinGap;
                    if (v.Speed < 1f && lane.Length - v.S < 45f) q++;
                    if (v.Speed < 0.5f) stopped++;
                    speedSum += v.Speed;
                    congSum += Mathf.Clamp01(1f - v.Speed / Mathf.Max(0.1f, v.DesiredSpeed));
                    total++;
                }
                lane.Queue = q;
                lane.ApproachCount = n;
                lane.AverageSpeed = n > 0 ? laneSpeed / n : lane.Edge.SpeedLimit;
                lane.Density = Mathf.Clamp01(occupied / Mathf.Max(1f, lane.Length + _net.JunctionLength));
                lane.Congestion = n == 0 ? 0f : Mathf.Clamp01(0.55f * lane.Density + 0.45f * (1f - lane.AverageSpeed / Mathf.Max(0.1f, lane.Edge.SpeedLimit)));
                if (lane.Blocked) lane.Congestion = 1f;
                lane.Edge.Congestion = lane.Congestion;
                queue += q;
            }
            foreach (var j in _net.Intersections)
            {
                int qns = 0, qew = 0, ans = 0, aew = 0;
                foreach (var l in j.Incoming)
                {
                    if (l.Axis == SignalAxis.NorthSouth) { qns += l.Queue; ans += l.ApproachCount; }
                    else { qew += l.Queue; aew += l.ApproachCount; }
                }
                j.Signal.QueueNS = qns; j.Signal.QueueEW = qew; j.Signal.ApproachNS = ans; j.Signal.ApproachEW = aew;
            }
            TotalQueue = queue;
            AverageSpeedKmh = total > 0 ? speedSum / total * 3.6f : 0f;
            StoppedShare = total > 0 ? stopped / (float)total : 0f;
            _congestionInstant = total > 0 ? congSum / total : 0f;
            Congestion = _statsInitialized ? Mathf.Lerp(Congestion, _congestionInstant, 0.25f) : _congestionInstant;
            _statsInitialized = true;
            if (_sim.layers != null && _sim.layers.Mode == LayerMode.Traffic) _net.UpdateTrafficOverlayColors();
        }

        public void SyncVisuals()
        {
            float J = _net.JunctionLength;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < Vehicles.Count; i++) Vehicles[i].ApplyPose(J, dt);
        }

        public int CountVehiclesNear(Vector3 p, float radius)
        {
            float r2 = radius * radius; int n = 0;
            foreach (var v in Vehicles) if ((v.WorldPosition - p).sqrMagnitude < r2) n++;
            return n;
        }

        /// <summary>Total vehicle "emission weight" on the given lanes (car 1, bus 2.5, truck 3).</summary>
        public static float EmissionWeight(IReadOnlyList<Lane> lanes)
        {
            float w = 0f;
            for (int i = 0; i < lanes.Count; i++)
                foreach (var v in lanes[i].Vehicles)
                {
                    float f = v.Kind == VehicleKind.Bus ? 2.5f : (v.Kind == VehicleKind.Truck ? 3f : 1f);
                    // idling and stop-start traffic emit more per metre than free flow
                    w += f * (v.Speed < 1f ? 1.4f : 1f);
                }
            return w;
        }
    }
}
