using System;
using System.Collections.Generic;

namespace SmartCity
{
    /// <summary>Result of a path search. When <see cref="Found"/> is false there is no route; nothing is invented.</summary>
    public sealed class PathResult
    {
        public bool Found;
        public readonly List<int> Nodes = new List<int>();
        public readonly List<int> Edges = new List<int>();
        /// <summary>Total cost in the units of the cost function (seconds for time cost, metres for distance cost).</summary>
        public float Cost;
        public float Length;
        public int NodesExpanded;

        public static PathResult NotFound(int expanded) => new PathResult { Found = false, NodesExpanded = expanded };
    }

    /// <summary>
    /// Classic A* search over <see cref="RoadGraph"/>.
    /// Blocked edges are skipped. Costs come from a pluggable cost function so the same search
    /// serves "shortest distance", "fastest free-flow" and "congestion-aware" routing.
    /// The heuristic is straight-line distance scaled so it never over-estimates (admissible).
    /// </summary>
    public static class AStarPathfinder
    {
        public delegate float EdgeCost(RoadGraph.Edge edge);

        /// <summary>Cost = metres. Heuristic scale 1.</summary>
        public static float DistanceCost(RoadGraph.Edge e) => e.Length * e.CostMultiplier;

        /// <summary>Cost = free-flow travel seconds, times incident multiplier.</summary>
        public static float FreeFlowTimeCost(RoadGraph.Edge e) => e.Length / Math.Max(0.1f, e.SpeedLimit) * e.CostMultiplier;

        /// <summary>Cost = travel seconds inflated by live congestion (0..1 -> up to 5x slower).</summary>
        public static float CongestionAwareTimeCost(RoadGraph.Edge e)
        {
            float c = e.Congestion < 0f ? 0f : (e.Congestion > 1f ? 1f : e.Congestion);
            return e.Length / Math.Max(0.1f, e.SpeedLimit) * e.CostMultiplier * (1f + 4f * c * c);
        }

        /// <summary>
        /// Finds the cheapest path from start to goal.
        /// heuristicScale converts straight-line metres into cost units; it must not exceed the
        /// cheapest possible cost per metre or optimality is lost (use 1 for distance, 1/maxSpeed for time).
        /// excludedEdge lets callers forbid one extra edge (e.g. the incident edge when computing a detour).
        /// </summary>
        public static PathResult FindPath(RoadGraph graph, int start, int goal, EdgeCost cost = null,
            float heuristicScale = 1f, int excludedEdge = -1, ISet<int> allowBlocked = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (start < 0 || start >= graph.NodeCount || goal < 0 || goal >= graph.NodeCount)
                return PathResult.NotFound(0);
            if (cost == null) cost = DistanceCost;

            if (start == goal)
            {
                var same = new PathResult { Found = true };
                same.Nodes.Add(start);
                return same;
            }

            int n = graph.NodeCount;
            var g = new float[n];
            var cameFromEdge = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.PositiveInfinity; cameFromEdge[i] = -1; }

            var open = new MinHeap(n);
            g[start] = 0f;
            open.Push(start, Heuristic(graph, start, goal, heuristicScale));
            int expanded = 0;

            while (open.Count > 0)
            {
                int current = open.Pop();
                if (closed[current]) continue;
                closed[current] = true;
                expanded++;
                if (current == goal) return Build(graph, start, goal, cameFromEdge, g[goal], expanded);

                var outs = graph.Outgoing(current);
                for (int k = 0; k < outs.Count; k++)
                {
                    var e = graph.GetEdge(outs[k]);
                    if (e.Id == excludedEdge) continue;
                    if (e.Blocked && (allowBlocked == null || !allowBlocked.Contains(e.Id))) continue;
                    if (closed[e.To]) continue;
                    float c = cost(e);
                    if (float.IsNaN(c) || float.IsInfinity(c) || c < 0f) continue;
                    float tentative = g[current] + c;
                    if (tentative < g[e.To])
                    {
                        g[e.To] = tentative;
                        cameFromEdge[e.To] = e.Id;
                        open.Push(e.To, tentative + Heuristic(graph, e.To, goal, heuristicScale));
                    }
                }
            }
            return PathResult.NotFound(expanded);
        }

        private static float Heuristic(RoadGraph graph, int a, int b, float scale) => graph.Distance(a, b) * scale;

        private static PathResult Build(RoadGraph graph, int start, int goal, int[] cameFromEdge, float cost, int expanded)
        {
            var result = new PathResult { Found = true, Cost = cost, NodesExpanded = expanded };
            int node = goal;
            var edges = new List<int>();
            while (node != start)
            {
                int eid = cameFromEdge[node];
                edges.Add(eid);
                node = graph.GetEdge(eid).From;
            }
            edges.Reverse();
            result.Nodes.Add(start);
            foreach (var eid in edges)
            {
                var e = graph.GetEdge(eid);
                result.Edges.Add(eid);
                result.Nodes.Add(e.To);
                result.Length += e.Length;
            }
            return result;
        }

        /// <summary>Small binary heap keyed by float priority (netstandard 2.1 has no PriorityQueue).</summary>
        private sealed class MinHeap
        {
            private int[] _items;
            private float[] _prio;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                capacity = Math.Max(4, capacity * 2);
                _items = new int[capacity];
                _prio = new float[capacity];
            }

            public void Push(int item, float priority)
            {
                if (Count == _items.Length)
                {
                    Array.Resize(ref _items, Count * 2);
                    Array.Resize(ref _prio, Count * 2);
                }
                int i = Count++;
                _items[i] = item; _prio[i] = priority;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (_prio[p] <= _prio[i]) break;
                    Swap(i, p); i = p;
                }
            }

            public int Pop()
            {
                int top = _items[0];
                Count--;
                if (Count > 0)
                {
                    _items[0] = _items[Count]; _prio[0] = _prio[Count];
                    int i = 0;
                    while (true)
                    {
                        int l = i * 2 + 1, r = l + 1, m = i;
                        if (l < Count && _prio[l] < _prio[m]) m = l;
                        if (r < Count && _prio[r] < _prio[m]) m = r;
                        if (m == i) break;
                        Swap(i, m); i = m;
                    }
                }
                return top;
            }

            private void Swap(int a, int b)
            {
                int ti = _items[a]; _items[a] = _items[b]; _items[b] = ti;
                float tp = _prio[a]; _prio[a] = _prio[b]; _prio[b] = tp;
            }
        }
    }
}
