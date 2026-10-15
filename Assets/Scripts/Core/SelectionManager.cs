using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>Click-to-select for intersections, roads, buildings, incidents and emergency units, with a 3D outline.</summary>
    public class SelectionManager : MonoBehaviour
    {
        public SelectableKind SelectedKind { get; private set; } = SelectableKind.None;
        public int SelectedId { get; private set; } = -1;
        public Vector3? SelectedPosition { get; private set; }
        public bool HasSelection => SelectedKind != SelectableKind.None;
        public event Action Changed;

        private SimulationController _sim;
        private LineRenderer _outline;
        private Vector3 _downPos;
        private bool _down;
        private float _t;

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            var go = new GameObject("Selection Outline");
            go.transform.SetParent(transform, false);
            _outline = go.AddComponent<LineRenderer>();
            _outline.loop = true;
            _outline.widthMultiplier = 0.6f;
            _outline.sharedMaterial = MaterialLibrary.Get("overlay");
            _outline.startColor = _outline.endColor = UITheme.Accent;
            _outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outline.enabled = false;
            CityEvents.SimulationReset += () => { if (SelectedKind == SelectableKind.Incident) Clear(); };
        }

        private void Update()
        {
            if (_sim == null || _sim.State == SimulationState.Title || !InputBridge.Available) return;
            if (InputBridge.MouseDown(0) && !InputBridge.PointerOverUI()) { _down = true; _downPos = InputBridge.MousePosition; }
            if (_down && InputBridge.MouseUp(0))
            {
                _down = false;
                if ((InputBridge.MousePosition - _downPos).magnitude < 6f && !InputBridge.PointerOverUI()) Pick(InputBridge.MousePosition);
            }
            if (_outline.enabled)
            {
                _t += Time.unscaledDeltaTime;
                Color c = UITheme.Accent; c.a = 0.6f + 0.4f * Mathf.Sin(_t * 4f);
                _outline.startColor = _outline.endColor = c;
                if (SelectedKind == SelectableKind.EmergencyUnit) DrawOutline();
            }
        }

        private void Pick(Vector3 screen)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var ray = cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 5000f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var s = h.collider.GetComponentInParent<Selectable>();
                if (s == null) continue;
                Select(s.kind, s.id);
                return;
            }
            Clear();
        }

        public void Select(SelectableKind kind, int id)
        {
            SelectedKind = kind;
            SelectedId = id;
            SelectedPosition = PositionOf(kind, id);
            if (kind == SelectableKind.Incident)
            {
                var inc = _sim.incidents.Find(id);
                if (inc != null && _sim.incidents.Selected != inc) _sim.incidents.Select(inc);
            }
            DrawOutline();
            Changed?.Invoke();
        }

        public void SelectIncident(Incident inc) => Select(SelectableKind.Incident, inc.Id);

        public void Clear()
        {
            SelectedKind = SelectableKind.None;
            SelectedId = -1;
            SelectedPosition = null;
            if (_outline != null) _outline.enabled = false;
            Changed?.Invoke();
        }

        private Vector3? PositionOf(SelectableKind kind, int id)
        {
            var net = _sim.roadNetwork;
            switch (kind)
            {
                case SelectableKind.Intersection: return net.NodePosition(id);
                case SelectableKind.Road: return net.Segments[id].Center;
                case SelectableKind.Building: return _sim.cityGenerator.Buildings[id].Position;
                case SelectableKind.Incident: var inc = _sim.incidents.Find(id); return inc != null ? inc.Position : (Vector3?)null;
                case SelectableKind.EmergencyUnit: return _sim.dispatch.Units[id].Position;
                default: return null;
            }
        }

        private void DrawOutline()
        {
            if (!HasSelection) { _outline.enabled = false; return; }
            var pts = new List<Vector3>();
            var net = _sim.roadNetwork;
            float y = 0.6f;
            switch (SelectedKind)
            {
                case SelectableKind.Intersection:
                    Rect(pts, net.NodePosition(SelectedId), net.RoadWidth * 0.5f + 1.5f, net.RoadWidth * 0.5f + 1.5f, y);
                    break;
                case SelectableKind.Road:
                    var s = net.Segments[SelectedId];
                    float half = (net.Spacing - net.RoadWidth) * 0.5f;
                    if (s.Horizontal) Rect(pts, s.Center, half, net.RoadWidth * 0.5f, y); else Rect(pts, s.Center, net.RoadWidth * 0.5f, half, y);
                    break;
                case SelectableKind.Building:
                    var b = _sim.cityGenerator.Buildings[SelectedId];
                    Rect(pts, b.Position, b.Size.x * 0.5f + 1f, b.Size.z * 0.5f + 1f, 0.4f);
                    break;
                case SelectableKind.EmergencyUnit:
                    Circle(pts, _sim.dispatch.Units[SelectedId].Position, 4f, y);
                    break;
                default:
                    var p = SelectedPosition ?? Vector3.zero;
                    Circle(pts, p, 6f, y);
                    break;
            }
            _outline.positionCount = pts.Count;
            _outline.SetPositions(pts.ToArray());
            _outline.enabled = true;
        }

        private static void Rect(List<Vector3> pts, Vector3 c, float hx, float hz, float y)
        {
            pts.Add(c + new Vector3(-hx, y, -hz)); pts.Add(c + new Vector3(-hx, y, hz));
            pts.Add(c + new Vector3(hx, y, hz)); pts.Add(c + new Vector3(hx, y, -hz));
        }

        private static void Circle(List<Vector3> pts, Vector3 c, float r, float y)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                pts.Add(c + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
            }
        }
    }
}
