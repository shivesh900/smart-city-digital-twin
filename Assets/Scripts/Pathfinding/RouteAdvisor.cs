using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Alternative-route recommendation: compares the shortest route (distance A*) with the least-congested
    /// route (A* with congestion-weighted travel time) and draws both on the map.
    /// </summary>
    public class RouteAdvisor : MonoBehaviour
    {
        public string LastAdvice { get; private set; } = "";
        private LineRenderer _shortest, _recommended;
        private SimulationController _sim;

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            _shortest = MakeLine("Shortest Route", new Color(0.75f, 0.75f, 0.8f, 0.8f), 0.9f);
            _recommended = MakeLine("Recommended Route", new Color(0.1f, 1f, 0.5f, 0.95f), 1.4f);
            CityEvents.SimulationReset += Clear;
        }

        private LineRenderer MakeLine(string name, Color c, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = MaterialLibrary.Get("overlay");
            lr.startColor = lr.endColor = c;
            lr.widthMultiplier = width;
            lr.numCornerVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            return lr;
        }

        public void Clear()
        {
            if (_shortest != null) _shortest.enabled = false;
            if (_recommended != null) _recommended.enabled = false;
            LastAdvice = "";
        }

        public string Advise(int from, int to, string destinationName)
        {
            var net = _sim.roadNetwork;
            if (from == to) { LastAdvice = "Origin and destination are the same junction."; return LastAdvice; }
            var shortest = net.FindRoute(from, to, RouteMode.Shortest);
            var best = net.FindRoute(from, to, RouteMode.LeastCongested);
            if (!shortest.Found || !best.Found)
            {
                Clear();
                LastAdvice = $"No route from {net.NodeName(from)} to {destinationName}: blocked roads cut the network. (No route was invented.)";
                return LastAdvice;
            }
            float tShort = net.EstimateTravelTime(shortest), tBest = net.EstimateTravelTime(best);
            Draw(_shortest, net, shortest, 0.5f);
            Draw(_recommended, net, best, 1.0f);
            bool same = shortest.Edges.Count == best.Edges.Count && shortest.Edges.TrueForAll(e => best.Edges.Contains(e));
            if (same)
                LastAdvice = $"{net.NodeName(from)} -> {destinationName}: the shortest route ({shortest.Length:0} m, est. {tShort:0} s) is also the least congested.";
            else
            {
                float saving = tShort - tBest;
                LastAdvice = $"{net.NodeName(from)} -> {destinationName}\nShortest (grey): {shortest.Length:0} m, est. {tShort:0} s\nRecommended (green): {best.Length:0} m, est. {tBest:0} s" +
                             (saving > 0.5f ? $"\nAvoids congestion, saves ~{saving:0} s ({saving / Mathf.Max(1f, tShort) * 100f:0}%)." : "");
            }
            return LastAdvice;
        }

        private static void Draw(LineRenderer lr, RoadNetworkManager net, PathResult path, float height)
        {
            var pts = net.PathPoints(path, height);
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            lr.enabled = true;
        }
    }
}
