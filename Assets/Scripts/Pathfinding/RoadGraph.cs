using System;
using System.Collections.Generic;

namespace SmartCity
{
    /// <summary>
    /// Pure C# directed road graph (no UnityEngine dependency so it can be unit tested anywhere).
    /// Nodes are intersections, edges are one-way lanes. A two-way road is two edges.
    /// </summary>
    public sealed class RoadGraph
    {
        public struct Node
        {
            public int Id;
            public float X;
            public float Z;
            public int GridX;
            public int GridY;
        }

        public sealed class Edge
        {
            public int Id;
            public int From;
            public int To;
            public float Length;
            /// <summary>Speed limit in metres per second.</summary>
            public float SpeedLimit;
            /// <summary>Blocked edges are never used by the pathfinder.</summary>
            public bool Blocked;
            /// <summary>Extra cost factor (accidents, works). 1 = normal.</summary>
            public float CostMultiplier = 1f;
            /// <summary>Live congestion 0..1, written by the traffic simulation.</summary>
            public float Congestion;
            /// <summary>Id of the edge going the opposite way, or -1.</summary>
            public int ReverseId = -1;
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<Edge> _edges = new List<Edge>();
        private readonly List<List<int>> _outgoing = new List<List<int>>();
        private readonly Dictionary<long, int> _edgeLookup = new Dictionary<long, int>();

        /// <summary>Incremented whenever an edge is blocked/unblocked or its cost multiplier changes.</summary>
        public int Version { get; private set; }

        public int NodeCount => _nodes.Count;
        public int EdgeCount => _edges.Count;
        public IReadOnlyList<Node> Nodes => _nodes;
        public IReadOnlyList<Edge> Edges => _edges;

        public int AddNode(float x, float z, int gridX = 0, int gridY = 0)
        {
            int id = _nodes.Count;
            _nodes.Add(new Node { Id = id, X = x, Z = z, GridX = gridX, GridY = gridY });
            _outgoing.Add(new List<int>());
            return id;
        }

        public int AddEdge(int from, int to, float speedLimit, float length = -1f)
        {
            if (from < 0 || from >= _nodes.Count) throw new ArgumentOutOfRangeException(nameof(from));
            if (to < 0 || to >= _nodes.Count) throw new ArgumentOutOfRangeException(nameof(to));
            if (length < 0f) length = Distance(from, to);
            int id = _edges.Count;
            var e = new Edge { Id = id, From = from, To = to, Length = length, SpeedLimit = speedLimit };
            _edges.Add(e);
            _outgoing[from].Add(id);
            _edgeLookup[Key(from, to)] = id;
            int rev;
            if (_edgeLookup.TryGetValue(Key(to, from), out rev))
            {
                e.ReverseId = rev;
                _edges[rev].ReverseId = id;
            }
            return id;
        }

        /// <summary>Adds both directions and returns the forward edge id.</summary>
        public int AddTwoWay(int a, int b, float speedLimit)
        {
            int fwd = AddEdge(a, b, speedLimit);
            AddEdge(b, a, speedLimit);
            return fwd;
        }

        public Node GetNode(int id) => _nodes[id];
        public Edge GetEdge(int id) => _edges[id];
        public IReadOnlyList<int> Outgoing(int node) => _outgoing[node];

        public int FindEdge(int from, int to)
        {
            int id;
            return _edgeLookup.TryGetValue(Key(from, to), out id) ? id : -1;
        }

        public float Distance(int a, int b)
        {
            float dx = _nodes[a].X - _nodes[b].X;
            float dz = _nodes[a].Z - _nodes[b].Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public void SetBlocked(int edgeId, bool blocked)
        {
            if (_edges[edgeId].Blocked == blocked) return;
            _edges[edgeId].Blocked = blocked;
            Version++;
        }

        public void SetCostMultiplier(int edgeId, float multiplier)
        {
            _edges[edgeId].CostMultiplier = multiplier < 1f ? 1f : multiplier;
            Version++;
        }

        public void ClearIncidents()
        {
            foreach (var e in _edges) { e.Blocked = false; e.CostMultiplier = 1f; e.Congestion = 0f; }
            Version++;
        }

        /// <summary>Number of edges (in + out) touching a node that are not blocked.</summary>
        public int OpenDegree(int node)
        {
            int n = 0;
            foreach (var e in _edges)
                if ((e.From == node || e.To == node) && !e.Blocked) n++;
            return n;
        }

        /// <summary>Builds a rectangular grid of intersections connected by two-way roads.</summary>
        public static RoadGraph CreateGrid(int columns, int rows, float spacing, float speedLimit, float originX = 0f, float originZ = 0f)
        {
            if (columns < 2 || rows < 2) throw new ArgumentException("Grid needs at least 2x2 intersections");
            var g = new RoadGraph();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    g.AddNode(originX + x * spacing, originZ + y * spacing, x, y);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int id = y * columns + x;
                    if (x + 1 < columns) g.AddTwoWay(id, id + 1, speedLimit);
                    if (y + 1 < rows) g.AddTwoWay(id, id + columns, speedLimit);
                }
            return g;
        }

        private static long Key(int from, int to) => ((long)from << 32) | (uint)to;
    }
}
