using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartCity
{
    /// <summary>
    /// Accumulates boxes, quads, cylinders and copies of primitive meshes into one mesh with several
    /// sub-meshes (one per material). Merging static city geometry like this keeps draw calls low on laptops.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<List<int>> _sub = new List<List<int>>();

        public int VertexCount => _v.Count;

        public MeshBuilder(int subMeshes = 1)
        {
            for (int i = 0; i < Mathf.Max(1, subMeshes); i++) _sub.Add(new List<int>());
        }

        private List<int> Sub(int s)
        {
            while (_sub.Count <= s) _sub.Add(new List<int>());
            return _sub[s];
        }

        public void AddQuad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            _n.Add(normal); _n.Add(normal); _n.Add(normal); _n.Add(normal);
            _uv.Add(uvA); _uv.Add(uvB); _uv.Add(uvC); _uv.Add(uvD);
            var t = Sub(sub);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        /// <summary>Flat horizontal rectangle facing up.</summary>
        public void AddFlat(int sub, Vector3 center, float sizeX, float sizeZ, float yawDegrees = 0f, bool worldUV = false)
        {
            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            Vector3 hx = rot * new Vector3(sizeX * 0.5f, 0f, 0f);
            Vector3 hz = rot * new Vector3(0f, 0f, sizeZ * 0.5f);
            Vector3 a = center - hx - hz, b = center - hx + hz, c = center + hx + hz, d = center + hx - hz;
            if (worldUV)
                AddQuad(sub, a, b, c, d, Vector3.up, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z));
            else
                AddQuad(sub, a, b, c, d, Vector3.up, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        /// <summary>
        /// Axis-aligned (optionally yawed) box. With metricUV the side faces get UVs in metres so a tiled
        /// facade texture keeps a constant window size on any building; the top face samples roofUV.
        /// </summary>
        public void AddBox(int sub, Vector3 center, Vector3 size, float yawDegrees = 0f, bool metricUV = false, bool bottom = false, Vector2? roofUV = null, int topSub = -1)
        {
            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            Vector3 h = size * 0.5f;
            Vector3 X = rot * Vector3.right, Y = Vector3.up, Z = rot * Vector3.forward;
            Vector3 P(float sx, float sy, float sz) => center + X * (h.x * sx) + Y * (h.y * sy) + Z * (h.z * sz);

            // sides: +Z, -Z, +X, -X
            Side(sub, P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), Z, size.x, size.y, metricUV);
            Side(sub, P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), -Z, size.x, size.y, metricUV);
            Side(sub, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), X, size.z, size.y, metricUV);
            Side(sub, P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), -X, size.z, size.y, metricUV);
            // top
            int ts = topSub >= 0 ? topSub : sub;
            if (roofUV.HasValue)
            {
                var r = roofUV.Value;
                AddQuad(ts, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Y, r, r, r, r);
            }
            else
            {
                AddQuad(ts, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Y, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            }
            if (bottom)
                AddQuad(sub, P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), -Y, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        private void Side(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, float width, float height, bool metric)
        {
            if (metric)
                AddQuad(sub, a, b, c, d, n, new Vector2(0, 0), new Vector2(0, height), new Vector2(width, height), new Vector2(width, 0));
            else
                AddQuad(sub, a, b, c, d, n, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        public void AddCylinder(int sub, Vector3 baseCenter, float radius, float height, int segments = 10, bool cap = true)
        {
            segments = Mathf.Max(3, segments);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 p0 = baseCenter + d0 * radius, p1 = baseCenter + d1 * radius;
                Vector3 up = Vector3.up * height;
                Vector3 n = ((d0 + d1) * 0.5f).normalized;
                AddQuad(sub, p0, p0 + up, p1 + up, p1, n, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                if (cap)
                {
                    int i0 = _v.Count;
                    Vector3 top = baseCenter + up;
                    _v.Add(top); _v.Add(p0 + up); _v.Add(p1 + up);
                    _n.Add(Vector3.up); _n.Add(Vector3.up); _n.Add(Vector3.up);
                    _uv.Add(new Vector2(0.5f, 0.5f)); _uv.Add(Vector2.zero); _uv.Add(Vector2.right);
                    var t = Sub(sub); t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1);
                }
            }
        }

        /// <summary>Copies all triangles of an existing mesh (e.g. Unity's built-in sphere) with a transform.</summary>
        public void AddMesh(int sub, Mesh mesh, Matrix4x4 matrix)
        {
            if (mesh == null) return;
            var verts = mesh.vertices; var norms = mesh.normals; var uvs = mesh.uv; var tris = mesh.triangles;
            int i0 = _v.Count;
            for (int i = 0; i < verts.Length; i++)
            {
                _v.Add(matrix.MultiplyPoint3x4(verts[i]));
                _n.Add(norms.Length == verts.Length ? matrix.MultiplyVector(norms[i]).normalized : Vector3.up);
                _uv.Add(uvs.Length == verts.Length ? uvs[i] : Vector2.zero);
            }
            var t = Sub(sub);
            for (int i = 0; i < tris.Length; i++) t.Add(i0 + tris[i]);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.subMeshCount = _sub.Count;
            for (int i = 0; i < _sub.Count; i++) m.SetTriangles(_sub[i], i, false);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Creates a GameObject rendering the built mesh with one material per sub-mesh.</summary>
        public GameObject Build(string name, Transform parent, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            go.isStatic = true;
            return go;
        }
    }

    /// <summary>Access to Unity's built-in primitive meshes without leaving GameObjects around.</summary>
    public static class PrimitiveMeshes
    {
        private static readonly System.Collections.Generic.Dictionary<PrimitiveType, Mesh> Cache = new System.Collections.Generic.Dictionary<PrimitiveType, Mesh>();

        public static Mesh Get(PrimitiveType type)
        {
            Mesh m;
            if (Cache.TryGetValue(type, out m) && m != null) return m;
            var go = GameObject.CreatePrimitive(type);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
            Cache[type] = m;
            return m;
        }
    }
}
