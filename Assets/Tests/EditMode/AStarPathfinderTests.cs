using NUnit.Framework;

namespace SmartCity.Tests
{
    public class AStarPathfinderTests
    {
        private static RoadGraph Grid(int n = 4) => RoadGraph.CreateGrid(n, n, 10f, 10f);

        [Test]
        public void FindsShortestPathOnGrid()
        {
            var g = Grid();
            var r = AStarPathfinder.FindPath(g, 0, 15);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(6, r.Edges.Count, "Manhattan distance on a 4x4 grid is 6 edges");
            Assert.AreEqual(60f, r.Length, 1e-3f);
            Assert.AreEqual(0, r.Nodes[0]);
            Assert.AreEqual(15, r.Nodes[r.Nodes.Count - 1]);
        }

        [Test]
        public void SameStartAndGoalIsTrivialPath()
        {
            var r = AStarPathfinder.FindPath(Grid(), 5, 5);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(0, r.Edges.Count);
        }

        [Test]
        public void AvoidsBlockedEdges()
        {
            var g = Grid();
            int direct = g.FindEdge(0, 1);
            g.SetBlocked(direct, true);
            var r = AStarPathfinder.FindPath(g, 0, 1);
            Assert.IsTrue(r.Found);
            Assert.IsFalse(r.Edges.Contains(direct));
            Assert.AreEqual(3, r.Edges.Count, "Detour around one blocked edge takes 3 edges");
        }

        [Test]
        public void ReportsNoPathWhenNodeIsIsolated()
        {
            var g = Grid();
            // close every road touching node 5 (both directions)
            foreach (var e in g.Edges)
                if (e.From == 5 || e.To == 5) g.SetBlocked(e.Id, true);
            var r = AStarPathfinder.FindPath(g, 0, 5);
            Assert.IsFalse(r.Found, "An isolated node must be reported as unreachable, never invented");
            Assert.AreEqual(0, r.Edges.Count);
        }

        [Test]
        public void CostMultiplierDivertsRoute()
        {
            var g = Grid();
            int e01 = g.FindEdge(0, 1);
            g.SetCostMultiplier(e01, 10f);
            var r = AStarPathfinder.FindPath(g, 0, 1);
            Assert.IsTrue(r.Found);
            Assert.IsFalse(r.Edges.Contains(e01), "A 10x cost edge should be avoided when a 3-edge detour exists");
        }

        [Test]
        public void CongestionAwareCostPrefersFreeRoad()
        {
            var g = Grid();
            // congest the straight path along the bottom row 0->1->2->3
            g.GetEdge(g.FindEdge(0, 1)).Congestion = 1f;
            g.GetEdge(g.FindEdge(1, 2)).Congestion = 1f;
            var shortest = AStarPathfinder.FindPath(g, 0, 2, AStarPathfinder.DistanceCost);
            var smart = AStarPathfinder.FindPath(g, 0, 2, AStarPathfinder.CongestionAwareTimeCost, 1f / 10f);
            Assert.AreEqual(2, shortest.Edges.Count);
            Assert.Greater(smart.Edges.Count, 2, "Congestion-aware route takes the longer but free road");
        }

        [Test]
        public void MatchesBruteForceDijkstraOnRandomCosts()
        {
            var rng = new System.Random(7);
            for (int trial = 0; trial < 20; trial++)
            {
                var g = RoadGraph.CreateGrid(5, 5, 10f, 10f);
                foreach (var e in g.Edges) { e.CostMultiplier = 1f + (float)rng.NextDouble() * 3f; if (rng.NextDouble() < 0.1) e.Blocked = true; }
                int s = rng.Next(25), t = rng.Next(25);
                var a = AStarPathfinder.FindPath(g, s, t, AStarPathfinder.DistanceCost, 1f);
                float d = Dijkstra(g, s, t);
                if (float.IsInfinity(d)) Assert.IsFalse(a.Found);
                else { Assert.IsTrue(a.Found); Assert.AreEqual(d, a.Cost, 1e-3f); }
            }
        }

        private static float Dijkstra(RoadGraph g, int s, int t)
        {
            var dist = new float[g.NodeCount];
            var done = new bool[g.NodeCount];
            for (int i = 0; i < dist.Length; i++) dist[i] = float.PositiveInfinity;
            dist[s] = 0f;
            for (int k = 0; k < g.NodeCount; k++)
            {
                int u = -1;
                for (int i = 0; i < dist.Length; i++) if (!done[i] && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || float.IsInfinity(dist[u])) break;
                done[u] = true;
                foreach (var eid in g.Outgoing(u))
                {
                    var e = g.GetEdge(eid);
                    if (e.Blocked) continue;
                    float nd = dist[u] + AStarPathfinder.DistanceCost(e);
                    if (nd < dist[e.To]) dist[e.To] = nd;
                }
            }
            return dist[t];
        }
    }
}
