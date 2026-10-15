using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Creates, tracks and resolves incidents. Each incident: (1) is marked on the 3D map, (2) updates the
    /// dashboard counters, (3) changes traffic conditions, (4) gets a detour computed with A* that avoids it
    /// when possible, (5) gets a rule-based severity and a suggested response.
    /// </summary>
    public class IncidentManager : MonoBehaviour
    {
        public readonly List<Incident> Active = new List<Incident>();
        public readonly List<Incident> History = new List<Incident>();
        public Incident Selected { get; private set; }
        public int TotalCreated { get; private set; }

        private SimulationController _sim;
        private RoadNetworkManager _net;
        private Transform _root;
        private int _nextId = 1;
        private float _reclassifyTimer;

        public void Initialize(SimulationController sim)
        {
            _sim = sim; _net = sim.roadNetwork;
            _root = new GameObject("Incident Markers").transform;
            _root.SetParent(transform, false);
        }

        public Incident Find(int id)
        {
            foreach (var i in Active) if (i.Id == id) return i;
            foreach (var i in History) if (i.Id == id) return i;
            return null;
        }

        public void Select(Incident inc)
        {
            Selected = inc;
            CityEvents.RaiseIncidentsChanged();
        }

        // ------------------------------------------------------------------ triggers

        public Incident TriggerAccident()
        {
            var seg = PickSegment(true, false);
            if (seg == null) { CityEvents.RaiseAlert("No suitable road for an accident (all roads already have incidents).", AlertLevel.Warning); return null; }
            var inc = New(IncidentKind.Accident);
            inc.Segment = seg;
            var lane = seg.Forward;
            float mid = lane.Length * 0.5f;
            inc.Position = lane.PointAt(mid);
            lane.CapActive = true; lane.CapFrom = mid - 6f; lane.CapTo = mid + 6f; lane.CapSpeed = 1.5f;
            var back = seg.Backward;
            back.CapActive = true; back.CapFrom = mid - 8f; back.CapTo = mid + 8f; back.CapSpeed = 4f;   // rubber-necking
            _net.SetSegmentCost(seg, 6f);
            inc.Props = AccidentProps(inc.Position, lane.Direction);
            return Finish(inc, "markerAccident", new Color(1f, 0.1f, 0.25f));
        }

        public Incident TriggerBlockedRoad()
        {
            var seg = PickSegment(false, true);
            if (seg == null) { CityEvents.RaiseAlert("No open road left to block.", AlertLevel.Warning); return null; }
            var inc = New(IncidentKind.BlockedRoad);
            inc.Segment = seg;
            inc.Position = seg.Center;
            _net.SetSegmentBlocked(seg, true);
            inc.Props = Barriers(seg);
            return Finish(inc, "markerBlocked", new Color(1f, 0.57f, 0f));
        }

        public Incident TriggerSignalFailure()
        {
            int node = PickNodeForSignalFailure();
            if (node < 0) { CityEvents.RaiseAlert("All signals have already failed.", AlertLevel.Warning); return null; }
            var inc = New(IncidentKind.SignalFailure);
            inc.NodeId = node;
            inc.Position = _net.NodePosition(node);
            _net.Intersections[node].Signal.SetFailed(true);
            return Finish(inc, "markerSignal", new Color(1f, 0.92f, 0f));
        }

        public Incident TriggerPollutionSpike()
        {
            Vector2Int cell = PickPollutionCell();
            var inc = New(IncidentKind.PollutionSpike);
            inc.Cell = cell;
            inc.Position = _net.CellCenter(cell.x, cell.y);
            _sim.pollution.AddSpike(cell.x, cell.y, 260f);
            return Finish(inc, "markerPollution", new Color(0.84f, 0f, 0.98f));
        }

        public Incident TriggerEmergencyRequest()
        {
            int node = PickNodeForEmergency();
            var inc = New(IncidentKind.EmergencyRequest);
            inc.NodeId = node;
            inc.Position = _net.NodePosition(node) + new Vector3(_net.RoadWidth * 0.5f + 1.5f, 0f, _net.RoadWidth * 0.5f + 1.5f);
            return Finish(inc, "markerEmergency", new Color(1f, 0.25f, 0.5f));
        }

        private Incident New(IncidentKind kind)
        {
            var inc = new Incident { Id = _nextId++, Kind = kind, Status = IncidentStatus.Active, CreatedAt = _sim.SimTime };
            TotalCreated++;
            return inc;
        }

        private Incident Finish(Incident inc, string markerMat, Color color)
        {
            ComputeDetour(inc);
            Classify(inc);
            inc.Marker = IncidentMarker.Create(_root, inc.Position, MaterialLibrary.Get(markerMat), color, inc.Id, inc.Title).gameObject;
            Active.Add(inc);
            Selected = inc;
            _sim.selection.SelectIncident(inc);
            CityEvents.RaiseAlert($"{inc.Title} at {inc.Location(_net)} - severity {inc.Severity.Level}. {inc.RouteNote}",
                inc.Severity.Level >= SeverityLevel.High ? AlertLevel.Critical : AlertLevel.Warning);
            CityEvents.RaiseIncidentsChanged();
            return inc;
        }

        // ------------------------------------------------------------------ target selection (uses the current selection when possible)

        private bool SegmentHasIncident(RoadSegment s)
        {
            foreach (var i in Active) if (i.Segment == s) return true;
            return false;
        }

        private RoadSegment PickSegment(bool busiest, bool mustBeOpen)
        {
            var sel = _sim.selection;
            if (sel.SelectedKind == SelectableKind.Road)
            {
                var s = _net.Segments[sel.SelectedId];
                if (!SegmentHasIncident(s) && (!mustBeOpen || !s.Blocked)) return s;
            }
            if (sel.SelectedKind == SelectableKind.Intersection)
            {
                // Blocking from a selected junction closes its roads one after another (useful to demo "no route").
                foreach (var s in _net.SegmentsAtNode(sel.SelectedId))
                    if (!SegmentHasIncident(s) && !s.Blocked) return s;
            }
            RoadSegment best = null; float bestScore = float.MinValue;
            foreach (var s in _net.Segments)
            {
                if (SegmentHasIncident(s) || s.Blocked) continue;
                float score = (busiest ? s.VehicleCount * 2f : 0f) + (float)_sim.Random.NextDouble() * 4f;
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        private int PickNodeForSignalFailure()
        {
            var sel = _sim.selection;
            if (sel.SelectedKind == SelectableKind.Intersection && !_net.Intersections[sel.SelectedId].Signal.Failed) return sel.SelectedId;
            int best = -1; float bestScore = float.MinValue;
            foreach (var j in _net.Intersections)
            {
                if (j.Signal.Failed) continue;
                float score = j.Signal.ApproachNS + j.Signal.ApproachEW + (float)_sim.Random.NextDouble() * 3f;
                if (score > bestScore) { bestScore = score; best = j.NodeId; }
            }
            return best;
        }

        private int PickNodeForEmergency()
        {
            var sel = _sim.selection;
            if (sel.SelectedKind == SelectableKind.Intersection) return sel.SelectedId;
            if (sel.SelectedKind == SelectableKind.Building) return _net.NearestNode(_sim.cityGenerator.Buildings[sel.SelectedId].Position);
            if (sel.SelectedKind == SelectableKind.Road) return _net.Segments[sel.SelectedId].NodeA;
            return _sim.Random.Next(_net.Graph.NodeCount);
        }

        private Vector2Int PickPollutionCell()
        {
            var sel = _sim.selection;
            int cx, cy;
            if (sel.SelectedKind == SelectableKind.Building)
            {
                var b = _sim.cityGenerator.Buildings[sel.SelectedId];
                return new Vector2Int(b.CellX, b.CellY);
            }
            if (sel.HasSelection && sel.SelectedPosition.HasValue && _net.CellOf(sel.SelectedPosition.Value, out cx, out cy)) return new Vector2Int(cx, cy);
            var l = _net.Layout;
            var candidates = new List<Vector2Int>();
            for (int y = 0; y < l.CellsY; y++) for (int x = 0; x < l.CellsX; x++) if (l.Zones[x, y] == ZoneType.Industrial) candidates.Add(new Vector2Int(x, y));
            if (candidates.Count == 0) return new Vector2Int(_sim.Random.Next(l.CellsX), _sim.Random.Next(l.CellsY));
            return candidates[_sim.Random.Next(candidates.Count)];
        }

        // ------------------------------------------------------------------ analysis

        private void ComputeDetour(Incident inc)
        {
            if (inc.Segment == null)
            {
                inc.RouteNote = inc.Kind == IncidentKind.SignalFailure ? "Signal dark: vehicles treat the junction as an all-way stop." : "";
                return;
            }
            var seg = inc.Segment;
            var path = _net.FindRoute(seg.NodeA, seg.NodeB, RouteMode.Fastest, seg.Forward.EdgeId);
            inc.Detour = path;
            if (path.Found && path.Edges.Count > 1)
            {
                float extra = path.Length - seg.Length - _net.RoadWidth;
                inc.RouteNote = $"Detour {_net.NodeName(seg.NodeA)} -> {_net.NodeName(seg.NodeB)} via {path.Nodes.Count - 2} junction(s), +{Mathf.Max(0f, extra):0} m (A*).";
                DrawDetour(inc);
            }
            else
            {
                inc.RouteNote = "No detour exists around this road - the network is cut here.";
            }
        }

        private void DrawDetour(Incident inc)
        {
            var go = new GameObject($"Incident {inc.Id} Detour");
            go.transform.SetParent(_root, false);
            var lr = go.AddComponent<LineRenderer>();
            var pts = _net.PathPoints(inc.Detour, 0.7f);
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            lr.widthMultiplier = 1.1f;
            lr.sharedMaterial = MaterialLibrary.Get("overlay");
            lr.startColor = lr.endColor = new Color(1f, 0.6f, 0.1f, 0.85f);
            lr.numCornerVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            inc.DetourLine = lr;
        }

        public void Classify(Incident inc)
        {
            var ctx = new IncidentContext
            {
                Kind = inc.Kind,
                VehiclesNearby = _sim.traffic.CountVehiclesNear(inc.Position, 60f),
                LocalAqi = _sim.pollution.AqiAt(inc.Position),
                NearCriticalFacility = NearFacility(inc.Position, 90f),
                PeakHour = SeverityClassifier.IsPeakHour(_sim.HourOfDay),
                ResponderRouteAvailable = _sim.dispatch.HasRouteFromAnyUnit(inc),
                NearbyBlockedRoads = BlockedNear(inc.Position, 80f)
            };
            inc.Severity = SeverityClassifier.Classify(ctx);
            inc.SuggestedResponse = SuggestResponse(inc, ctx);
        }

        private bool NearFacility(Vector3 p, float r)
        {
            var g = _sim.cityGenerator;
            float r2 = r * r;
            return (g.HospitalNode >= 0 && (g.HospitalPosition - p).sqrMagnitude < r2)
                || (g.PoliceNode >= 0 && (g.PolicePosition - p).sqrMagnitude < r2)
                || (g.FireNode >= 0 && (g.FirePosition - p).sqrMagnitude < r2);
        }

        private int BlockedNear(Vector3 p, float r)
        {
            int n = 0;
            foreach (var s in _net.Segments) if (s.Blocked && (s.Center - p).sqrMagnitude < r * r) n++;
            return n;
        }

        private string SuggestResponse(Incident inc, IncidentContext ctx)
        {
            string unit = inc.RequiredUnit == VehicleKind.Ambulance ? "ambulance" : inc.RequiredUnit == VehicleKind.FireTruck ? "fire/hazmat unit" : "police unit";
            string routing = ctx.ResponderRouteAvailable ? $"Dispatch the nearest {unit} (A* route avoids blocked roads)." : $"No responder route: clear a blocked road before dispatching the {unit}.";
            switch (inc.Kind)
            {
                case IncidentKind.Accident:
                    return $"{routing} Close the affected lane, divert traffic via the detour, add police if severity is High or above.";
                case IncidentKind.BlockedRoad:
                    return $"{routing} Vehicles are re-routed automatically; post diversion signs at both ends.";
                case IncidentKind.SignalFailure:
                    return $"{routing} Manual traffic control until repair; enable adaptive timing on neighbouring junctions.";
                case IncidentKind.PollutionSpike:
                    return $"{routing} Inspect industrial sources, divert heavy vehicles, issue a health advisory (local AQI {ctx.LocalAqi:0}).";
                default:
                    return $"{routing} Signals along the route are pre-empted to green for the responder.";
            }
        }

        // ------------------------------------------------------------------ resolution

        public void ResolveSelectedOrOldest()
        {
            Incident target = Selected != null && Selected.Status != IncidentStatus.Resolved ? Selected : null;
            if (target == null && Active.Count > 0) target = Active[0];
            if (target == null) { CityEvents.RaiseAlert("No active incident to resolve.", AlertLevel.Info); return; }
            Resolve(target);
        }

        public void Resolve(Incident inc)
        {
            if (inc == null || inc.Status == IncidentStatus.Resolved) return;
            Undo(inc);
            inc.Status = IncidentStatus.Resolved;
            Active.Remove(inc);
            History.Insert(0, inc);
            if (History.Count > 30) History.RemoveAt(History.Count - 1);
            if (inc.AssignedUnit != null) _sim.dispatch.Release(inc.AssignedUnit);
            if (Selected == inc) Selected = Active.Count > 0 ? Active[Active.Count - 1] : null;
            CityEvents.RaiseAlert($"{inc.Title} resolved - normal traffic restored at {inc.Location(_net)}.", AlertLevel.Success);
            CityEvents.RaiseIncidentsChanged();
        }

        private void Undo(Incident inc)
        {
            switch (inc.Kind)
            {
                case IncidentKind.Accident:
                    inc.Segment.Forward.CapActive = false;
                    inc.Segment.Backward.CapActive = false;
                    if (!AnotherIncidentOn(inc.Segment, inc)) _net.SetSegmentCost(inc.Segment, 1f);
                    break;
                case IncidentKind.BlockedRoad:
                    if (!AnotherIncidentOn(inc.Segment, inc)) _net.SetSegmentBlocked(inc.Segment, false);
                    break;
                case IncidentKind.SignalFailure:
                    _net.Intersections[inc.NodeId].Signal.SetFailed(false);
                    break;
                case IncidentKind.PollutionSpike:
                    _sim.pollution.ClearSpike(inc.Cell.x, inc.Cell.y);
                    break;
            }
            DestroyVisuals(inc);
        }

        private bool AnotherIncidentOn(RoadSegment seg, Incident except)
        {
            foreach (var i in Active) if (i != except && i.Segment == seg && i.Kind == except.Kind) return true;
            return false;
        }

        private static void DestroyVisuals(Incident inc)
        {
            if (inc.Marker != null) Destroy(inc.Marker);
            if (inc.Props != null) Destroy(inc.Props);
            if (inc.DetourLine != null) Destroy(inc.DetourLine.gameObject);
            inc.Marker = null; inc.Props = null; inc.DetourLine = null;
        }

        public void ResetAll()
        {
            foreach (var inc in Active) DestroyVisuals(inc);
            Active.Clear();
            History.Clear();
            Selected = null;
            _nextId = 1;
            TotalCreated = 0;
            CityEvents.RaiseIncidentsChanged();
        }

        public void Step(float dt)
        {
            _reclassifyTimer += dt;
            if (_reclassifyTimer < 3f) return;
            _reclassifyTimer = 0f;
            foreach (var inc in Active) Classify(inc);   // severity is live: it follows traffic and AQI
        }

        public Incident MostSevereUndispatched()
        {
            Incident best = null;
            foreach (var i in Active)
            {
                if (i.Status != IncidentStatus.Active) continue;
                if (best == null || i.Severity.Score > best.Severity.Score) best = i;
            }
            return best;
        }

        // ------------------------------------------------------------------ props

        private GameObject AccidentProps(Vector3 pos, Vector3 dir)
        {
            var root = new GameObject("Accident Props");
            root.transform.SetParent(_root, false);
            var a = VehicleFactory.CreateVisual(VehicleKind.Car, 0, root.transform);
            a.transform.position = pos + dir * 1.5f;
            a.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 35f, 0);
            var b = VehicleFactory.CreateVisual(VehicleKind.Car, 3, root.transform);
            b.transform.position = pos - dir * 2.5f + new Vector3(dir.z, 0, -dir.x) * 0.8f;
            b.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, -20f, 8f);
            return root;
        }

        private GameObject Barriers(RoadSegment seg)
        {
            var root = new GameObject("Barriers");
            root.transform.SetParent(_root, false);
            var mb = new MeshBuilder();
            Vector3 a = _net.NodePosition(seg.NodeA), b = _net.NodePosition(seg.NodeB);
            Vector3 dir = (b - a).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 90f;
            foreach (var p in new[] { a + dir * (_net.StopDistance + 1f), b - dir * (_net.StopDistance + 1f), seg.Center })
            {
                mb.AddBox(0, p + Vector3.up * 0.6f, new Vector3(_net.RoadWidth - 1f, 0.35f, 0.3f), yaw);
                mb.AddBox(0, p + Vector3.up * 0.3f + Quaternion.Euler(0, yaw, 0) * Vector3.right * (_net.RoadWidth * 0.4f), new Vector3(0.3f, 0.6f, 0.3f), yaw);
                mb.AddBox(0, p + Vector3.up * 0.3f - Quaternion.Euler(0, yaw, 0) * Vector3.right * (_net.RoadWidth * 0.4f), new Vector3(0.3f, 0.6f, 0.3f), yaw);
            }
            var go = mb.Build("Barrier Mesh", root.transform, MaterialLibrary.Get("barrier"));
            go.isStatic = false;
            return root;
        }
    }
}
