using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Builds and owns the road network: the pure <see cref="RoadGraph"/> used by A*, the lane geometry
    /// vehicles drive on, the road segments (selectable), and the signalised intersections.
    /// </summary>
    public class RoadNetworkManager : MonoBehaviour
    {
        public RoadGraph Graph { get; private set; }
        public CityLayout Layout { get; private set; }
        public readonly List<Lane> Lanes = new List<Lane>();
        public readonly List<Intersection> Intersections = new List<Intersection>();
        public readonly List<RoadSegment> Segments = new List<RoadSegment>();
        public readonly List<int> DowntownNodes = new List<int>();
        public TrafficOptimizer Optimizer { get; private set; }

        public float Spacing { get; private set; }
        public float RoadWidth { get; private set; }
        public float StopDistance { get; private set; }
        public float JunctionLength { get; private set; }
        public Vector3 Origin { get; private set; }
        public Bounds CityBounds { get; private set; }
        public float MaxSpeed { get; private set; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }

        private SimulationController _sim;
        private List<Lane>[,] _cellLanes;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        public void Build(SimulationController sim)
        {
            _sim = sim;
            var s = sim.settings;
            Columns = s.gridColumns; Rows = s.gridRows;
            Spacing = s.blockSpacing;
            RoadWidth = s.roadWidth;
            StopDistance = s.roadWidth * 0.5f + 3.5f;   // stop line just before the crosswalk
            JunctionLength = StopDistance * 2f;
            MaxSpeed = Mathf.Max(s.CarSpeed, s.EmergencySpeed);
            Origin = new Vector3(-(Columns - 1) * Spacing * 0.5f, 0f, -(Rows - 1) * Spacing * 0.5f);
            var size = new Vector3((Columns - 1) * Spacing + Spacing, 1f, (Rows - 1) * Spacing + Spacing);
            CityBounds = new Bounds(Vector3.zero, size);

            Graph = RoadGraph.CreateGrid(Columns, Rows, Spacing, s.CarSpeed, Origin.x, Origin.z);
            Layout = CityLayout.Create(Columns, Rows);
            Optimizer = new TrafficOptimizer(s.minGreen, s.maxGreen, s.baselineGreen);

            BuildLanes(s);
            BuildSegments();
            BuildIntersections(s);
            BuildCellIndex();
            BuildDowntown();
        }

        public string NodeName(int node)
        {
            var n = Graph.GetNode(node);
            return $"{(char)('A' + n.GridX)}{n.GridY + 1}";
        }

        public Vector3 NodePosition(int node)
        {
            var n = Graph.GetNode(node);
            return new Vector3(n.X, 0f, n.Z);
        }

        public Lane GetLane(int edgeId) => edgeId >= 0 && edgeId < Lanes.Count ? Lanes[edgeId] : null;

        public Lane GetLane(int from, int to)
        {
            int e = Graph.FindEdge(from, to);
            return e < 0 ? null : Lanes[e];
        }

        private void BuildLanes(SimulationSettings s)
        {
            Lanes.Clear();
            var laneRoot = new GameObject("Traffic Overlay").transform;
            laneRoot.SetParent(transform, false);
            var overlay = MaterialLibrary.Get("overlay");
            for (int i = 0; i < Graph.EdgeCount; i++)
            {
                var e = Graph.GetEdge(i);
                Vector3 a = NodePosition(e.From), b = NodePosition(e.To);
                Vector3 dir = (b - a).normalized;
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                Vector3 side = (s.driveOnLeft ? -right : right) * s.laneOffset;
                var lane = new Lane
                {
                    EdgeId = i,
                    Edge = e,
                    From = e.From,
                    To = e.To,
                    Direction = dir,
                    Start = a + dir * StopDistance + side,
                    End = b - dir * StopDistance + side,
                    Axis = Graph.GetNode(e.From).GridX == Graph.GetNode(e.To).GridX ? SignalAxis.NorthSouth : SignalAxis.EastWest
                };
                lane.Length = Vector3.Distance(lane.Start, lane.End);

                // Coloured strip used by the TRAFFIC layer.
                var mb = new MeshBuilder();
                Vector3 mid = (a + b) * 0.5f + side + Vector3.up * 0.09f;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                mb.AddFlat(0, Vector3.zero, s.laneOffset * 1.1f, Vector3.Distance(a, b) - RoadWidth * 0.6f, 0f);
                var go = mb.Build($"Lane {i}", laneRoot, overlay);
                go.isStatic = false;
                go.transform.position = mid;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                lane.OverlayRenderer = go.GetComponent<MeshRenderer>();
                lane.OverlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lane.OverlayRenderer.receiveShadows = false;
                go.SetActive(false);
                Lanes.Add(lane);
            }
        }

        private void BuildSegments()
        {
            Segments.Clear();
            var root = new GameObject("Road Segments").transform;
            root.SetParent(transform, false);
            foreach (var lane in Lanes)
            {
                if (lane.From > lane.To) continue;
                var back = Lanes[lane.Edge.ReverseId];
                var seg = new RoadSegment
                {
                    Index = Segments.Count,
                    Forward = lane,
                    Backward = back,
                    NodeA = lane.From,
                    NodeB = lane.To,
                    Center = (NodePosition(lane.From) + NodePosition(lane.To)) * 0.5f,
                    Horizontal = lane.Axis == SignalAxis.EastWest,
                    Length = Spacing - RoadWidth,
                    Name = $"Road {NodeName(lane.From)}-{NodeName(lane.To)}"
                };
                lane.Segment = seg; back.Segment = seg;
                var go = new GameObject(seg.Name);
                go.transform.SetParent(root, false);
                go.transform.position = seg.Center + Vector3.up * 0.2f;
                var col = go.AddComponent<BoxCollider>();
                col.size = seg.Horizontal ? new Vector3(seg.Length, 0.4f, RoadWidth) : new Vector3(RoadWidth, 0.4f, seg.Length);
                var sel = go.AddComponent<Selectable>();
                sel.kind = SelectableKind.Road; sel.id = seg.Index;
                seg.GameObject = go;
                Segments.Add(seg);
            }
        }

        private void BuildIntersections(SimulationSettings s)
        {
            Intersections.Clear();
            var root = new GameObject("Intersections").transform;
            root.SetParent(transform, false);
            for (int n = 0; n < Graph.NodeCount; n++)
            {
                var node = Graph.GetNode(n);
                var j = new Intersection
                {
                    NodeId = n,
                    GridX = node.GridX,
                    GridY = node.GridY,
                    Position = NodePosition(n),
                    Name = "Junction " + NodeName(n)
                };
                foreach (var lane in Lanes)
                {
                    if (lane.To == n) j.Incoming.Add(lane);
                    if (lane.From == n) j.Outgoing.Add(lane);
                }
                var go = new GameObject(j.Name);
                go.transform.SetParent(root, false);
                go.transform.position = j.Position;
                var col = go.AddComponent<BoxCollider>();
                col.center = Vector3.up * 0.2f;
                col.size = new Vector3(RoadWidth, 0.4f, RoadWidth);
                var sel = go.AddComponent<Selectable>();
                sel.kind = SelectableKind.Intersection; sel.id = n;
                j.GameObject = go;
                j.Signal = go.AddComponent<TrafficLightController>();
                // Staggered start offsets so not every signal switches at the same moment.
                float offset = (float)new System.Random(s.seed + n * 31).NextDouble() * (2f * (s.baselineGreen + s.yellowTime + s.allRedTime));
                j.Signal.Initialize(j, this, s, Optimizer, offset);
                Intersections.Add(j);
            }
        }

        private void BuildCellIndex()
        {
            _cellLanes = new List<Lane>[Layout.CellsX, Layout.CellsY];
            for (int y = 0; y < Layout.CellsY; y++)
                for (int x = 0; x < Layout.CellsX; x++)
                {
                    var list = new List<Lane>();
                    int n00 = y * Columns + x, n10 = n00 + 1, n01 = n00 + Columns, n11 = n01 + 1;
                    AddPair(list, n00, n10); AddPair(list, n01, n11); AddPair(list, n00, n01); AddPair(list, n10, n11);
                    _cellLanes[x, y] = list;
                }
        }

        private void AddPair(List<Lane> list, int a, int b)
        {
            var l1 = GetLane(a, b); var l2 = GetLane(b, a);
            if (l1 != null) list.Add(l1);
            if (l2 != null) list.Add(l2);
        }

        private void BuildDowntown()
        {
            DowntownNodes.Clear();
            var set = new HashSet<int>();
            for (int y = 0; y < Layout.CellsY; y++)
                for (int x = 0; x < Layout.CellsX; x++)
                {
                    var z = Layout.Zones[x, y];
                    if (z != ZoneType.Commercial && z != ZoneType.Hospital && z != ZoneType.Parking) continue;
                    int n00 = y * Columns + x;
                    set.Add(n00); set.Add(n00 + 1); set.Add(n00 + Columns); set.Add(n00 + Columns + 1);
                }
            DowntownNodes.AddRange(set);
            DowntownNodes.Sort();
        }

        // ----------------------------------------------------------------- queries

        public IReadOnlyList<Lane> LanesAroundCell(int cx, int cy) => _cellLanes[cx, cy];

        public Vector3 CellCenter(int cx, int cy) => Origin + new Vector3((cx + 0.5f) * Spacing, 0f, (cy + 0.5f) * Spacing);

        public bool CellOf(Vector3 p, out int cx, out int cy)
        {
            cx = Mathf.FloorToInt((p.x - Origin.x) / Spacing);
            cy = Mathf.FloorToInt((p.z - Origin.z) / Spacing);
            bool inside = cx >= 0 && cy >= 0 && cx < Layout.CellsX && cy < Layout.CellsY;
            cx = Mathf.Clamp(cx, 0, Layout.CellsX - 1);
            cy = Mathf.Clamp(cy, 0, Layout.CellsY - 1);
            return inside;
        }

        public int NearestNode(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < Graph.NodeCount; i++)
            {
                float d = (NodePosition(i) - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public RoadSegment NearestSegment(Vector3 p)
        {
            RoadSegment best = null; float bd = float.MaxValue;
            foreach (var s in Segments)
            {
                float d = (s.Center - p).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        public List<RoadSegment> SegmentsAtNode(int node)
        {
            var list = new List<RoadSegment>();
            foreach (var s in Segments) if (s.NodeA == node || s.NodeB == node) list.Add(s);
            return list;
        }

        public PathResult FindRoute(int from, int to, RouteMode mode, int excludedEdge = -1, ISet<int> allowBlocked = null)
        {
            switch (mode)
            {
                case RouteMode.Shortest:
                    return AStarPathfinder.FindPath(Graph, from, to, AStarPathfinder.DistanceCost, 1f, excludedEdge, allowBlocked);
                case RouteMode.LeastCongested:
                    return AStarPathfinder.FindPath(Graph, from, to, AStarPathfinder.CongestionAwareTimeCost, 1f / MaxSpeed, excludedEdge, allowBlocked);
                default:
                    return AStarPathfinder.FindPath(Graph, from, to, AStarPathfinder.FreeFlowTimeCost, 1f / MaxSpeed, excludedEdge, allowBlocked);
            }
        }

        /// <summary>Expected travel time (s) along a path using current lane speeds/congestion.</summary>
        public float EstimateTravelTime(PathResult path)
        {
            if (path == null || !path.Found) return float.PositiveInfinity;
            float t = 0f;
            foreach (var eid in path.Edges) t += AStarPathfinder.CongestionAwareTimeCost(Graph.GetEdge(eid));
            return t;
        }

        public List<Vector3> PathPoints(PathResult path, float height)
        {
            var pts = new List<Vector3>();
            if (path == null) return pts;
            foreach (var n in path.Nodes) pts.Add(NodePosition(n) + Vector3.up * height);
            return pts;
        }

        // ----------------------------------------------------------------- incidents & state

        public void SetSegmentBlocked(RoadSegment seg, bool blocked)
        {
            Graph.SetBlocked(seg.Forward.EdgeId, blocked);
            Graph.SetBlocked(seg.Backward.EdgeId, blocked);
        }

        public void SetSegmentCost(RoadSegment seg, float multiplier)
        {
            Graph.SetCostMultiplier(seg.Forward.EdgeId, multiplier);
            Graph.SetCostMultiplier(seg.Backward.EdgeId, multiplier);
        }

        public int BlockedSegmentCount()
        {
            int n = 0;
            foreach (var s in Segments) if (s.Blocked) n++;
            return n;
        }

        public void StepSignals(float dt, bool optimize)
        {
            for (int i = 0; i < Intersections.Count; i++) Intersections[i].Signal.Step(dt, optimize);
        }

        public void ResetNetwork()
        {
            Graph.ClearIncidents();
            foreach (var l in Lanes) { l.CapActive = false; l.Congestion = 0f; l.Density = 0f; l.Queue = 0; l.ApproachCount = 0; }
            foreach (var j in Intersections) j.Signal.ResetSignal();
        }

        public void SetTrafficOverlayVisible(bool visible)
        {
            foreach (var l in Lanes) if (l.OverlayRenderer != null) l.OverlayRenderer.gameObject.SetActive(visible);
        }

        public void UpdateTrafficOverlayColors()
        {
            foreach (var l in Lanes)
            {
                if (l.OverlayRenderer == null || !l.OverlayRenderer.gameObject.activeSelf) continue;
                Color c = l.Blocked ? new Color(0.35f, 0.35f, 0.4f, 0.85f) : CongestionColor(l.Congestion);
                MaterialUtil.SetRendererColor(l.OverlayRenderer, _block, c);
            }
        }

        public static Color CongestionColor(float c)
        {
            c = Mathf.Clamp01(c);
            Color green = new Color(0.1f, 0.95f, 0.45f, 0.9f), yellow = new Color(1f, 0.85f, 0.1f, 0.9f), red = new Color(1f, 0.15f, 0.2f, 0.95f);
            return c < 0.5f ? Color.Lerp(green, yellow, c * 2f) : Color.Lerp(yellow, red, (c - 0.5f) * 2f);
        }
    }
}
