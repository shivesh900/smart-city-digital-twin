using UnityEngine;

namespace SmartCity
{
    /// <summary>Animated 3D marker: light beam, floating beacon and a pulsing ground ring.</summary>
    public class IncidentMarker : MonoBehaviour
    {
        private Transform _beacon;
        private LineRenderer _ring;
        private float _t;
        private Color _color;

        public static IncidentMarker Create(Transform parent, Vector3 position, Material emissive, Color color, int incidentId, string label)
        {
            var go = new GameObject($"Incident {incidentId} Marker");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var m = go.AddComponent<IncidentMarker>();
            m._color = color;

            var beam = new GameObject("Beam");
            beam.transform.SetParent(go.transform, false);
            beam.transform.localPosition = new Vector3(0, 9f, 0);
            beam.transform.localScale = new Vector3(0.35f, 9f, 0.35f);
            beam.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Get(PrimitiveType.Cylinder);
            var br = beam.AddComponent<MeshRenderer>();
            br.sharedMaterial = emissive;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var beacon = new GameObject("Beacon");
            beacon.transform.SetParent(go.transform, false);
            beacon.transform.localPosition = new Vector3(0, 19f, 0);
            beacon.transform.localScale = Vector3.one * 2.4f;
            beacon.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Get(PrimitiveType.Sphere);
            var bcr = beacon.AddComponent<MeshRenderer>();
            bcr.sharedMaterial = emissive;
            bcr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m._beacon = beacon.transform;

            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(go.transform, false);
            var lr = ringGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 32;
            lr.widthMultiplier = 0.5f;
            lr.sharedMaterial = MaterialLibrary.Get("overlay");
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)));
            }
            m._ring = lr;

            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 10f, 0);
            col.height = 22f;
            col.radius = 2.2f;
            var sel = go.AddComponent<Selectable>();
            sel.kind = SelectableKind.Incident; sel.id = incidentId;

            WorldLabel.Create(go.transform, position + Vector3.up * 23f, label, Color.Lerp(color, Color.white, 0.4f), 1.1f);
            return m;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_beacon != null) _beacon.localPosition = new Vector3(0, 19f + Mathf.Sin(_t * 2.5f) * 0.8f, 0);
            if (_ring != null)
            {
                float k = Mathf.Repeat(_t * 0.6f, 1f);
                _ring.transform.localScale = new Vector3(3f + k * 9f, 1f, 3f + k * 9f);
                Color c = _color; c.a = 1f - k;
                _ring.startColor = c; _ring.endColor = c;
            }
        }
    }
}
