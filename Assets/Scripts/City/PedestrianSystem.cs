using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>Decorative pedestrians walking around block sidewalks (visual only, not part of the traffic model).</summary>
    public class PedestrianSystem : MonoBehaviour
    {
        private sealed class Walker
        {
            public Transform T;
            public Vector3 Center;
            public float Half;
            public float Perimeter;
            public float Pos;
            public float Speed;
            public float Phase;
        }

        private readonly List<Walker> _walkers = new List<Walker>();
        private SimulationController _sim;

        public void Build(SimulationController sim, RoadNetworkManager net, int count)
        {
            _sim = sim;
            var rng = new System.Random(sim.settings.seed + 99);
            var capsule = PrimitiveMeshes.Get(PrimitiveType.Capsule);
            var mats = new[] { MaterialLibrary.Get("pedestrian"), MaterialLibrary.Get("pedestrian2"), MaterialLibrary.Get("pedestrian3") };
            float half = (net.Spacing - net.RoadWidth) * 0.5f - 1.2f;
            for (int i = 0; i < count; i++)
            {
                int cx = rng.Next(net.Layout.CellsX), cy = rng.Next(net.Layout.CellsY);
                var go = new GameObject("Pedestrian");
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
                go.AddComponent<MeshFilter>().sharedMesh = capsule;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mats[rng.Next(mats.Length)];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var w = new Walker
                {
                    T = go.transform,
                    Center = net.CellCenter(cx, cy),
                    Half = half,
                    Perimeter = half * 8f,
                    Pos = (float)rng.NextDouble() * half * 8f,
                    Speed = (rng.NextDouble() < 0.5 ? -1f : 1f) * (1.0f + (float)rng.NextDouble() * 0.6f),
                    Phase = (float)rng.NextDouble() * 6f
                };
                _walkers.Add(w);
                Place(w, 0f);
            }
        }

        private void Update()
        {
            if (_sim == null) return;
            bool moving = _sim.State == SimulationState.Running || _sim.State == SimulationState.Title;
            if (!moving) return;
            float dt = Time.unscaledDeltaTime * (_sim.State == SimulationState.Title ? 1f : _sim.SimSpeed);
            foreach (var w in _walkers) Place(w, dt);
        }

        private static void Place(Walker w, float dt)
        {
            w.Pos = Mathf.Repeat(w.Pos + w.Speed * dt, w.Perimeter);
            float side = w.Half * 2f;
            float p = w.Pos;
            Vector3 a, dir;
            if (p < side) { a = w.Center + new Vector3(-w.Half + p, 0, -w.Half); dir = Vector3.right; }
            else if (p < 2 * side) { a = w.Center + new Vector3(w.Half, 0, -w.Half + (p - side)); dir = Vector3.forward; }
            else if (p < 3 * side) { a = w.Center + new Vector3(w.Half - (p - 2 * side), 0, w.Half); dir = Vector3.left; }
            else { a = w.Center + new Vector3(-w.Half, 0, w.Half - (p - 3 * side)); dir = Vector3.back; }
            w.Phase += dt * 8f;
            float bob = Mathf.Abs(Mathf.Sin(w.Phase)) * 0.06f;
            w.T.position = a + Vector3.up * (0.15f + 0.85f + bob);
            if (w.Speed < 0) dir = -dir;
            w.T.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
