using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    public enum UnitState { Available, EnRoute, OnScene, Returning }

    /// <summary>An ambulance, fire truck or police car based at its station.</summary>
    public sealed class EmergencyUnit
    {
        public int Id;
        public string Name;
        public VehicleKind Kind;
        public int HomeNode;
        public Vector3 HomePosition;
        public UnitState State;
        public Incident Target;
        public GameObject Visual;
        public MeshRenderer Renderer;
        public Material[] MatsA, MatsB, MatsOff;
        public readonly List<Vector3> Path = new List<Vector3>();
        public readonly List<int> PathNodes = new List<int>();
        public float PathLength;
        public float Progress;
        public float Speed;
        public float Eta;
        public float OnSceneTimer;
        public LineRenderer RouteLine;
        public Vector3 Position;
        public Vector3 Forward = Vector3.forward;
    }

    /// <summary>
    /// Emergency dispatch: picks the available unit with the fastest A* route (blocked roads avoided),
    /// highlights the route, animates the vehicle with signal pre-emption, shows a simulated ETA and records
    /// the response time. When no valid route exists it reports that instead of inventing one.
    /// </summary>
    public class EmergencyDispatchManager : MonoBehaviour
    {
        public readonly List<EmergencyUnit> Units = new List<EmergencyUnit>();
        public float LastResponseSeconds { get; private set; } = -1f;
        public float AverageResponseSeconds { get; private set; } = -1f;
        public int ResponsesCompleted { get; private set; }
        public string LastDispatchMessage { get; private set; } = "No dispatch yet.";

        private SimulationController _sim;
        private RoadNetworkManager _net;
        private Transform _root;
        private float _responseSum;
        private float _sirenTimer;
        private bool _sirenPhase;

        public void Initialize(SimulationController sim)
        {
            _sim = sim; _net = sim.roadNetwork;
            _root = new GameObject("Emergency Units").transform;
            _root.SetParent(transform, false);
            var g = sim.cityGenerator;
            CreateUnits(VehicleKind.Ambulance, sim.settings.ambulances, g.HospitalNode >= 0 ? g.HospitalNode : 0, g.HospitalPosition, "Ambulance A");
            CreateUnits(VehicleKind.FireTruck, sim.settings.fireTrucks, g.FireNode >= 0 ? g.FireNode : 0, g.FirePosition, "Fire Engine F");
            CreateUnits(VehicleKind.Police, sim.settings.policeCars, g.PoliceNode >= 0 ? g.PoliceNode : 0, g.PolicePosition, "Police P");
            ResetAll();
        }

        private void CreateUnits(VehicleKind kind, int count, int node, Vector3 facility, string prefix)
        {
            Vector3 nodePos = _net.NodePosition(node);
            Vector3 toFacility = facility - nodePos; toFacility.y = 0f;
            Vector3 dir = Mathf.Abs(toFacility.x) > Mathf.Abs(toFacility.z) ? new Vector3(Mathf.Sign(toFacility.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toFacility.z));
            if (dir == Vector3.zero) dir = Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                var u = new EmergencyUnit
                {
                    Id = Units.Count,
                    Name = prefix + "-" + (i + 1),
                    Kind = kind,
                    HomeNode = node,
                    HomePosition = nodePos + dir * (_net.StopDistance + 4f + i * 7f),
                    Speed = _sim.settings.EmergencySpeed
                };
                u.Visual = VehicleFactory.CreateVisual(kind, 0, _root);
                u.Visual.name = u.Name;
                u.Renderer = u.Visual.GetComponent<MeshRenderer>();
                u.MatsOff = u.Renderer.sharedMaterials;
                u.MatsA = (Material[])u.MatsOff.Clone();
                u.MatsB = (Material[])u.MatsOff.Clone();
                u.MatsA[VehicleFactory.SubSirenA] = MaterialLibrary.Get("sirenRed");
                u.MatsB[VehicleFactory.SubSirenB] = MaterialLibrary.Get("sirenBlue");
                var col = u.Visual.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 1.2f, 0);
                col.size = new Vector3(2.4f, 2.4f, VehicleFactory.LengthOf(kind));
                var sel = u.Visual.AddComponent<Selectable>();
                sel.kind = SelectableKind.EmergencyUnit; sel.id = u.Id;
                u.Forward = -dir;
                Units.Add(u);
            }
        }

        public void ResetAll()
        {
            foreach (var u in Units)
            {
                u.State = UnitState.Available;
                u.Target = null;
                u.Path.Clear(); u.PathNodes.Clear();
                u.Progress = 0f; u.Eta = 0f;
                u.Position = u.HomePosition;
                ClearLine(u);
                u.Renderer.sharedMaterials = u.MatsOff;
                u.Visual.transform.SetPositionAndRotation(u.Position, Quaternion.LookRotation(u.Forward));
            }
            LastResponseSeconds = -1f; AverageResponseSeconds = -1f; ResponsesCompleted = 0; _responseSum = 0f;
            LastDispatchMessage = "No dispatch yet.";
        }

        public int AvailableCount(VehicleKind kind)
        {
            int n = 0;
            foreach (var u in Units) if (u.Kind == kind && u.State == UnitState.Available) n++;
            return n;
        }

        // ------------------------------------------------------------------ routing

        private int TargetNode(Incident inc, int fromNode, out PathResult best)
        {
            best = null;
            var targets = new List<int>();
            if (inc.Segment != null) { targets.Add(inc.Segment.NodeA); targets.Add(inc.Segment.NodeB); }
            else if (inc.NodeId >= 0) targets.Add(inc.NodeId);
            else
            {
                int n00 = inc.Cell.y * _net.Columns + inc.Cell.x;
                targets.Add(n00); targets.Add(n00 + 1); targets.Add(n00 + _net.Columns); targets.Add(n00 + _net.Columns + 1);
            }
            int bestNode = -1; float bestCost = float.MaxValue;
            foreach (var t in targets)
            {
                var p = _net.FindRoute(fromNode, t, RouteMode.LeastCongested);
                if (!p.Found) continue;
                float cost = p.Cost + (Vector3.Distance(_net.NodePosition(t), inc.Position) / _sim.settings.EmergencySpeed);
                if (cost < bestCost) { bestCost = cost; bestNode = t; best = p; }
            }
            return bestNode;
        }

        public bool HasRouteFromAnyUnit(Incident inc)
        {
            var nodes = new HashSet<int>();
            foreach (var u in Units) nodes.Add(u.HomeNode);
            foreach (var n in nodes)
            {
                PathResult p;
                if (TargetNode(inc, n, out p) >= 0) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ dispatch

        /// <summary>Dispatches to the selected incident, or the most severe one still waiting.</summary>
        public void DispatchToSelectedOrMostSevere()
        {
            var inc = _sim.incidents.Selected;
            if (inc == null || inc.Status != IncidentStatus.Active) inc = _sim.incidents.MostSevereUndispatched();
            if (inc == null)
            {
                Report("No incident is waiting for a responder. Trigger an incident first.", AlertLevel.Info);
                return;
            }
            Dispatch(inc);
        }

        public bool Dispatch(Incident inc)
        {
            if (inc.Status != IncidentStatus.Active)
            {
                Report($"{inc.Title} already has {inc.AssignedUnit?.Name ?? "a responder"} assigned.", AlertLevel.Info);
                return false;
            }
            var kind = inc.RequiredUnit;
            EmergencyUnit chosen = null; PathResult chosenPath = null; int chosenTarget = -1; float bestCost = float.MaxValue;
            bool anyAvailable = false;
            foreach (var u in Units)
            {
                if (u.Kind != kind || u.State != UnitState.Available) continue;
                anyAvailable = true;
                PathResult p;
                int t = TargetNode(inc, u.HomeNode, out p);
                if (t < 0) continue;
                if (p.Cost < bestCost) { bestCost = p.Cost; chosen = u; chosenPath = p; chosenTarget = t; }
            }
            if (!anyAvailable)
            {
                Report($"No {UnitLabel(kind)} available - all units are busy. {inc.Title} is queued; try again when a unit returns.", AlertLevel.Warning);
                return false;
            }
            if (chosen == null)
            {
                inc.RouteNote = "NO VALID ROUTE for responders: every road into the incident location is blocked.";
                _sim.incidents.Classify(inc);
                Report($"NO VALID ROUTE to {inc.Title} at {inc.Location(_net)}: all access roads are blocked. Dispatch not possible - resolve a blocked road first. (No route was invented.)", AlertLevel.Critical);
                CityEvents.RaiseIncidentsChanged();
                return false;
            }

            chosen.Path.Clear(); chosen.PathNodes.Clear();
            chosen.Path.Add(chosen.HomePosition);
            foreach (var n in chosenPath.Nodes) { chosen.Path.Add(_net.NodePosition(n)); chosen.PathNodes.Add(n); }
            chosen.Path.Add(inc.Position);
            chosen.PathLength = 0f;
            for (int i = 1; i < chosen.Path.Count; i++) chosen.PathLength += Vector3.Distance(chosen.Path[i - 1], chosen.Path[i]);
            chosen.Progress = 0f;
            chosen.State = UnitState.EnRoute;
            chosen.Target = inc;
            chosen.Eta = chosen.PathLength / chosen.Speed;
            inc.AssignedUnit = chosen;
            inc.Status = IncidentStatus.Dispatched;
            inc.DispatchedAt = _sim.SimTime;
            DrawRoute(chosen);
            Report($"{chosen.Name} dispatched to {inc.Title}: {chosenPath.Nodes.Count} junctions, {chosen.PathLength:0} m, simulated ETA {chosen.Eta:0} s (A*, blocked roads avoided).", AlertLevel.Success);
            CityEvents.RaiseIncidentsChanged();
            return true;
        }

        private void Report(string msg, AlertLevel level)
        {
            LastDispatchMessage = msg;
            CityEvents.RaiseAlert(msg, level);
        }

        public static string UnitLabel(VehicleKind k) => k == VehicleKind.Ambulance ? "ambulance" : k == VehicleKind.FireTruck ? "fire engine" : "police car";

        /// <summary>Sends a unit back to its station (called when its incident is resolved).</summary>
        public void Release(EmergencyUnit u)
        {
            if (u.State == UnitState.Available || u.State == UnitState.Returning) return;
            u.Target = null;
            // Reverse the travelled part of the path to return home.
            var back = new List<Vector3>();
            float travelled = u.Progress;
            back.Add(u.Position);
            for (int i = u.Path.Count - 1; i >= 0; i--)
            {
                float d = 0f;
                for (int k = 1; k <= i; k++) d += Vector3.Distance(u.Path[k - 1], u.Path[k]);
                if (d <= travelled + 0.01f) back.Add(u.Path[i]);
            }
            u.Path.Clear(); u.Path.AddRange(back);
            u.PathLength = 0f;
            for (int i = 1; i < u.Path.Count; i++) u.PathLength += Vector3.Distance(u.Path[i - 1], u.Path[i]);
            u.Progress = 0f;
            u.State = UnitState.Returning;
            ClearLine(u);
        }

        // ------------------------------------------------------------------ simulation

        public void Step(float dt)
        {
            foreach (var u in Units)
            {
                switch (u.State)
                {
                    case UnitState.EnRoute:
                        Move(u, dt);
                        Preempt(u);
                        u.Eta = Mathf.Max(0f, (u.PathLength - u.Progress) / u.Speed);
                        if (u.Progress >= u.PathLength) Arrive(u);
                        break;
                    case UnitState.OnScene:
                        u.OnSceneTimer += dt;
                        if (_sim.settings.autoResolveAfterResponse && u.OnSceneTimer >= _sim.settings.onSceneSeconds && u.Target != null)
                            _sim.incidents.Resolve(u.Target);
                        break;
                    case UnitState.Returning:
                        Move(u, dt);
                        if (u.Progress >= u.PathLength)
                        {
                            u.State = UnitState.Available;
                            u.Position = u.HomePosition;
                        }
                        break;
                }
            }
        }

        private void Move(EmergencyUnit u, float dt)
        {
            u.Progress = Mathf.Min(u.PathLength, u.Progress + u.Speed * dt);
            float d = u.Progress;
            for (int i = 1; i < u.Path.Count; i++)
            {
                float seg = Vector3.Distance(u.Path[i - 1], u.Path[i]);
                if (d <= seg || i == u.Path.Count - 1)
                {
                    float t = seg > 0.001f ? Mathf.Clamp01(d / seg) : 1f;
                    u.Position = Vector3.Lerp(u.Path[i - 1], u.Path[i], t);
                    Vector3 f = u.Path[i] - u.Path[i - 1];
                    if (f.sqrMagnitude > 0.01f) u.Forward = f.normalized;
                    return;
                }
                d -= seg;
            }
        }

        private void Preempt(EmergencyUnit u)
        {
            foreach (var n in u.PathNodes)
            {
                Vector3 p = _net.NodePosition(n);
                if ((p - u.Position).sqrMagnitude < 45f * 45f)
                {
                    var axis = Mathf.Abs(u.Forward.x) > Mathf.Abs(u.Forward.z) ? SignalAxis.EastWest : SignalAxis.NorthSouth;
                    _net.Intersections[n].Signal.RequestPreemption(axis, 3f);
                }
            }
        }

        private void Arrive(EmergencyUnit u)
        {
            u.State = UnitState.OnScene;
            u.OnSceneTimer = 0f;
            ClearLine(u);
            var inc = u.Target;
            if (inc == null) return;
            inc.Status = IncidentStatus.OnScene;
            inc.ArrivedAt = _sim.SimTime;
            inc.ResponseSeconds = inc.ArrivedAt - inc.CreatedAt;
            LastResponseSeconds = inc.ResponseSeconds;
            ResponsesCompleted++;
            _responseSum += inc.ResponseSeconds;
            AverageResponseSeconds = _responseSum / ResponsesCompleted;
            float travel = inc.ArrivedAt - inc.DispatchedAt;
            Report($"{u.Name} arrived at {inc.Title}. Response time {inc.ResponseSeconds:0} s (travel {travel:0} s). Press RESOLVE when the scene is cleared.", AlertLevel.Success);
            CityEvents.RaiseIncidentsChanged();
        }

        // ------------------------------------------------------------------ visuals

        private void DrawRoute(EmergencyUnit u)
        {
            ClearLine(u);
            var go = new GameObject(u.Name + " Route");
            go.transform.SetParent(_root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = u.Path.Count;
            for (int i = 0; i < u.Path.Count; i++) lr.SetPosition(i, u.Path[i] + Vector3.up * 0.8f);
            lr.widthMultiplier = 1.6f;
            lr.numCornerVertices = 4;
            lr.sharedMaterial = MaterialLibrary.Get("overlay");
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(0f, 0.9f, 1f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.6f), 1f) },
                         new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.95f, 1f) });
            lr.colorGradient = grad;
            u.RouteLine = lr;
        }

        private static void ClearLine(EmergencyUnit u)
        {
            if (u.RouteLine != null) Destroy(u.RouteLine.gameObject);
            u.RouteLine = null;
        }

        public void SyncVisuals(float dt)
        {
            _sirenTimer += dt;
            bool flip = false;
            if (_sirenTimer > 0.22f) { _sirenTimer = 0f; _sirenPhase = !_sirenPhase; flip = true; }
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var u in Units)
            {
                var t = u.Visual.transform;
                Vector3 pos = u.Position;
                Quaternion rot = Quaternion.LookRotation(u.Forward, Vector3.up);
                t.SetPositionAndRotation(Vector3.Lerp(t.position, pos, 0.5f), Quaternion.Slerp(t.rotation, rot, 0.25f));
                bool siren = u.State == UnitState.EnRoute || u.State == UnitState.OnScene;
                if (siren && flip) u.Renderer.sharedMaterials = _sirenPhase ? u.MatsA : u.MatsB;
                else if (!siren && u.Renderer.sharedMaterials[VehicleFactory.SubSirenA] != u.MatsOff[VehicleFactory.SubSirenA]) u.Renderer.sharedMaterials = u.MatsOff;
                if (u.RouteLine != null) u.RouteLine.widthMultiplier = 1.3f + 0.5f * pulse;
            }
        }
    }
}
