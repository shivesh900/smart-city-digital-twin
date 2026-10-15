using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Builds low-poly vehicle meshes from boxes (no imported models) and hands out pooled instances.
    /// Every vehicle mesh has 8 sub-meshes: body, glass, tyres, headlights, tail lights, siren A, siren B, accent.
    /// </summary>
    public static class VehicleFactory
    {
        public const int SubBody = 0, SubGlass = 1, SubTire = 2, SubHead = 3, SubTail = 4, SubSirenA = 5, SubSirenB = 6, SubAccent = 7;
        private static readonly Dictionary<VehicleKind, Mesh> Meshes = new Dictionary<VehicleKind, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Meshes.Clear(); }

        public static float LengthOf(VehicleKind k)
        {
            switch (k)
            {
                case VehicleKind.Bus: return 10f;
                case VehicleKind.Truck: return 8f;
                case VehicleKind.FireTruck: return 8f;
                case VehicleKind.Ambulance: return 5.8f;
                case VehicleKind.Police: return 4.6f;
                default: return 4.2f;
            }
        }

        public static Mesh GetMesh(VehicleKind kind)
        {
            Mesh m;
            if (Meshes.TryGetValue(kind, out m) && m != null) return m;
            var b = new MeshBuilder(8);
            float L = LengthOf(kind);
            switch (kind)
            {
                case VehicleKind.Bus:
                    b.AddBox(SubBody, new Vector3(0, 1.75f, 0), new Vector3(2.5f, 2.7f, L));
                    b.AddBox(SubGlass, new Vector3(0, 2.25f, -0.3f), new Vector3(2.54f, 0.95f, L - 1.4f));
                    b.AddBox(SubGlass, new Vector3(0, 2.0f, L * 0.5f + 0.01f), new Vector3(2.2f, 1.4f, 0.04f));
                    b.AddBox(SubAccent, new Vector3(0, 3.15f, 0), new Vector3(2.3f, 0.12f, L - 0.6f));
                    Wheels(b, 1.2f, 0.42f, new[] { L * 0.32f, -L * 0.32f });
                    Lights(b, 1.0f, 0.9f, L);
                    break;
                case VehicleKind.Truck:
                    b.AddBox(SubBody, new Vector3(0, 1.45f, L * 0.5f - 1.1f), new Vector3(2.3f, 2.1f, 2.2f));
                    b.AddBox(SubGlass, new Vector3(0, 1.95f, L * 0.5f + 0.01f), new Vector3(2.0f, 0.8f, 0.04f));
                    b.AddBox(SubAccent, new Vector3(0, 1.95f, -1.15f), new Vector3(2.45f, 2.9f, L - 2.4f));
                    Wheels(b, 1.1f, 0.45f, new[] { L * 0.32f, -L * 0.15f, -L * 0.38f });
                    Lights(b, 0.9f, 0.85f, L);
                    break;
                case VehicleKind.Ambulance:
                    b.AddBox(SubBody, new Vector3(0, 0.75f, 0.0f), new Vector3(2.1f, 0.9f, L));
                    b.AddBox(SubBody, new Vector3(0, 1.75f, -0.6f), new Vector3(2.1f, 1.3f, L - 1.3f));
                    b.AddBox(SubGlass, new Vector3(0, 1.45f, L * 0.5f - 0.75f), new Vector3(1.9f, 0.7f, 0.6f));
                    b.AddBox(SubAccent, new Vector3(0, 1.35f, -0.6f), new Vector3(2.14f, 0.3f, L - 1.4f));
                    b.AddBox(SubSirenA, new Vector3(-0.45f, 2.5f, 0.4f), new Vector3(0.7f, 0.22f, 0.4f));
                    b.AddBox(SubSirenB, new Vector3(0.45f, 2.5f, 0.4f), new Vector3(0.7f, 0.22f, 0.4f));
                    Wheels(b, 0.95f, 0.38f, new[] { L * 0.3f, -L * 0.3f });
                    Lights(b, 0.7f, 0.7f, L);
                    break;
                case VehicleKind.FireTruck:
                    b.AddBox(SubBody, new Vector3(0, 1.45f, L * 0.5f - 1.1f), new Vector3(2.4f, 2.1f, 2.2f));
                    b.AddBox(SubGlass, new Vector3(0, 1.95f, L * 0.5f + 0.01f), new Vector3(2.1f, 0.8f, 0.04f));
                    b.AddBox(SubBody, new Vector3(0, 1.4f, -1.2f), new Vector3(2.45f, 2.0f, L - 2.4f));
                    b.AddBox(SubAccent, new Vector3(0, 2.6f, -1.2f), new Vector3(1.2f, 0.25f, L - 2.0f));
                    b.AddBox(SubSirenA, new Vector3(-0.5f, 2.65f, L * 0.5f - 0.9f), new Vector3(0.8f, 0.22f, 0.4f));
                    b.AddBox(SubSirenB, new Vector3(0.5f, 2.65f, L * 0.5f - 0.9f), new Vector3(0.8f, 0.22f, 0.4f));
                    Wheels(b, 1.15f, 0.48f, new[] { L * 0.3f, -L * 0.18f, -L * 0.36f });
                    Lights(b, 0.9f, 0.85f, L);
                    break;
                default: // Car and Police
                    b.AddBox(SubBody, new Vector3(0, 0.62f, 0), new Vector3(1.9f, 0.65f, L));
                    b.AddBox(SubGlass, new Vector3(0, 1.17f, -0.25f), new Vector3(1.7f, 0.48f, L * 0.52f));
                    if (kind == VehicleKind.Police)
                    {
                        b.AddBox(SubAccent, new Vector3(0, 0.7f, 0), new Vector3(1.94f, 0.18f, L - 0.4f));
                        b.AddBox(SubSirenA, new Vector3(-0.35f, 1.5f, -0.2f), new Vector3(0.6f, 0.18f, 0.35f));
                        b.AddBox(SubSirenB, new Vector3(0.35f, 1.5f, -0.2f), new Vector3(0.6f, 0.18f, 0.35f));
                    }
                    Wheels(b, 0.88f, 0.34f, new[] { L * 0.31f, -L * 0.31f });
                    Lights(b, 0.6f, 0.68f, L);
                    break;
            }
            m = b.ToMesh(kind + " Mesh");
            Meshes[kind] = m;
            return m;
        }

        private static void Wheels(MeshBuilder b, float x, float radius, float[] zs)
        {
            foreach (float z in zs)
            {
                b.AddBox(SubTire, new Vector3(x, radius, z), new Vector3(0.32f, radius * 2f, radius * 2f));
                b.AddBox(SubTire, new Vector3(-x, radius, z), new Vector3(0.32f, radius * 2f, radius * 2f));
            }
        }

        private static void Lights(MeshBuilder b, float x, float y, float L)
        {
            b.AddBox(SubHead, new Vector3(x, y, L * 0.5f + 0.02f), new Vector3(0.38f, 0.18f, 0.06f));
            b.AddBox(SubHead, new Vector3(-x, y, L * 0.5f + 0.02f), new Vector3(0.38f, 0.18f, 0.06f));
            b.AddBox(SubTail, new Vector3(x, y, -L * 0.5f - 0.02f), new Vector3(0.36f, 0.16f, 0.06f));
            b.AddBox(SubTail, new Vector3(-x, y, -L * 0.5f - 0.02f), new Vector3(0.36f, 0.16f, 0.06f));
        }

        public static Material[] MaterialsFor(VehicleKind kind, int colorIndex)
        {
            string body;
            string accent = "white";
            switch (kind)
            {
                case VehicleKind.Bus: body = "bus"; accent = "roofAccent"; break;
                case VehicleKind.Truck: body = "truck"; accent = "truck"; break;
                case VehicleKind.Ambulance: body = "ambulance"; accent = "medicalRed"; break;
                case VehicleKind.FireTruck: body = "fireTruck"; accent = "pole"; break;
                case VehicleKind.Police: body = "police"; accent = "white"; break;
                default: body = "car" + Mathf.Abs(colorIndex % 6); break;
            }
            return new[]
            {
                MaterialLibrary.Get(body), MaterialLibrary.Get("vehicleGlass"), MaterialLibrary.Get("tire"),
                MaterialLibrary.Get("headlight"), MaterialLibrary.Get("sigRedOn"),
                MaterialLibrary.Get("sirenOff"), MaterialLibrary.Get("sirenOff"), MaterialLibrary.Get(accent)
            };
        }

        public static GameObject CreateVisual(VehicleKind kind, int colorIndex, Transform parent)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = GetMesh(kind);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = MaterialsFor(kind, colorIndex);
            return go;
        }
    }

    /// <summary>Object pool for traffic vehicles (avoids Instantiate/Destroy garbage while spawning).</summary>
    public sealed class VehiclePool
    {
        private readonly Dictionary<int, Stack<VehicleController>> _free = new Dictionary<int, Stack<VehicleController>>();
        private readonly Transform _parent;
        public int CreatedCount { get; private set; }

        public VehiclePool(Transform parent) { _parent = parent; }

        private static int Key(VehicleKind kind, int color) => (int)kind * 16 + (kind == VehicleKind.Car ? color % 6 : 0);

        public VehicleController Get(VehicleKind kind, int colorIndex)
        {
            Stack<VehicleController> stack;
            int key = Key(kind, colorIndex);
            VehicleController v = null;
            if (_free.TryGetValue(key, out stack) && stack.Count > 0) v = stack.Pop();
            if (v == null)
            {
                var go = VehicleFactory.CreateVisual(kind, colorIndex, _parent);
                v = go.AddComponent<VehicleController>();
                v.Kind = kind;
                v.Length = VehicleFactory.LengthOf(kind);
                v.ColorIndex = colorIndex;
                CreatedCount++;
            }
            v.ResetState();
            v.gameObject.SetActive(true);
            return v;
        }

        public void Return(VehicleController v)
        {
            v.gameObject.SetActive(false);
            v.ResetState();
            int key = Key(v.Kind, v.ColorIndex);
            Stack<VehicleController> stack;
            if (!_free.TryGetValue(key, out stack)) { stack = new Stack<VehicleController>(); _free[key] = stack; }
            stack.Push(v);
        }
    }
}
