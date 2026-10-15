using UnityEngine;
using UnityEngine.UI;

namespace SmartCity
{
    /// <summary>
    /// Procedurally drawn line chart (uGUI Graphic): grid, filled area, main series, optional dashed
    /// secondary series (forecast) and optional threshold line. No chart assets or packages needed.
    /// </summary>
    public class LineChartGraphic : MaskableGraphic
    {
        public Color lineColor = new Color(0f, 0.9f, 1f);
        public Color secondaryColor = new Color(1f, 0.8f, 0.2f);
        public Color gridColor = new Color(1f, 1f, 1f, 0.07f);
        public Color thresholdColor = new Color(1f, 0.2f, 0.3f, 0.7f);
        public float lineWidth = 2.2f;
        public float minValue;
        public float maxValue = 100f;
        public bool autoScale = true;
        public float threshold = float.NaN;

        private TimeSeries _series;
        private TimeSeries _secondary;
        private int _lastCount = -1;
        private float _lastValue = float.NaN;

        public void SetData(TimeSeries series, TimeSeries secondary = null)
        {
            _series = series;
            _secondary = secondary;
            Refresh();
        }

        public void Refresh()
        {
            if (_series == null) return;
            if (_series.Count == _lastCount && Mathf.Approximately(_series.Latest, _lastValue)) return;
            _lastCount = _series.Count;
            _lastValue = _series.Latest;
            if (autoScale && _series.Count > 0)
            {
                float lo = _series.Min(), hi = _series.Max();
                if (_secondary != null && _secondary.Count > 0) { lo = Mathf.Min(lo, _secondary.Min()); hi = Mathf.Max(hi, _secondary.Max()); }
                if (!float.IsNaN(threshold)) hi = Mathf.Max(hi, threshold * 1.05f);
                float pad = Mathf.Max(0.5f, (hi - lo) * 0.15f);
                minValue = Mathf.Max(0f, lo - pad);
                maxValue = hi + pad;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            // grid
            for (int i = 0; i <= 4; i++)
            {
                float y = r.yMin + r.height * i / 4f;
                Line(vh, new Vector2(r.xMin, y), new Vector2(r.xMax, y), 1f, gridColor);
            }
            if (_series == null || _series.Count < 2) return;
            float range = Mathf.Max(0.0001f, maxValue - minValue);
            int cap = _series.Capacity;

            // threshold
            if (!float.IsNaN(threshold) && threshold >= minValue && threshold <= maxValue)
            {
                float ty = r.yMin + (threshold - minValue) / range * r.height;
                for (float x = r.xMin; x < r.xMax; x += 10f)
                    Line(vh, new Vector2(x, ty), new Vector2(Mathf.Min(x + 5f, r.xMax), ty), 1.2f, thresholdColor);
            }

            // area + main line
            Vector2 prev = Vector2.zero;
            Color fillTop = new Color(lineColor.r, lineColor.g, lineColor.b, 0.28f);
            Color fillBottom = new Color(lineColor.r, lineColor.g, lineColor.b, 0.0f);
            for (int i = 0; i < _series.Count; i++)
            {
                float x = r.xMin + r.width * (i + (cap - _series.Count)) / (cap - 1f);
                float y = r.yMin + Mathf.Clamp01((_series[i] - minValue) / range) * r.height;
                var p = new Vector2(x, y);
                if (i > 0)
                {
                    int idx = vh.currentVertCount;
                    vh.AddVert(new Vector3(prev.x, r.yMin), fillBottom, Vector2.zero);
                    vh.AddVert(new Vector3(prev.x, prev.y), fillTop, Vector2.zero);
                    vh.AddVert(new Vector3(p.x, p.y), fillTop, Vector2.zero);
                    vh.AddVert(new Vector3(p.x, r.yMin), fillBottom, Vector2.zero);
                    vh.AddTriangle(idx, idx + 1, idx + 2);
                    vh.AddTriangle(idx, idx + 2, idx + 3);
                    Line(vh, prev, p, lineWidth, lineColor);
                }
                prev = p;
            }
            // latest point marker
            Line(vh, prev + new Vector2(-3, 0), prev + new Vector2(3, 0), 6f, Color.white);

            // secondary (dashed)
            if (_secondary != null && _secondary.Count >= 2)
            {
                Vector2 sp = Vector2.zero;
                for (int i = 0; i < _secondary.Count; i++)
                {
                    float x = r.xMin + r.width * (i + (cap - _secondary.Count)) / (cap - 1f);
                    float y = r.yMin + Mathf.Clamp01((_secondary[i] - minValue) / range) * r.height;
                    var p = new Vector2(x, y);
                    if (i > 0 && i % 2 == 0) Line(vh, sp, p, lineWidth * 0.7f, secondaryColor);
                    if (i % 2 == 0) sp = p;
                }
            }
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
            Vector2 n = new Vector2(-d.y, d.x).normalized * width * 0.5f;
            int idx = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero);
            vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero);
            vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx, idx + 2, idx + 3);
        }
    }
}
