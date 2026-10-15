using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Simulated air-quality model per city block (zone). Values are MODEL OUTPUT, not sensor readings.
    ///   target AQI = zone base + 3.2 * traffic emission weight on the 4 bordering roads (x0.5 in low-emission zones)
    ///                - 12 per neighbouring park + active pollution spikes
    ///   AQI relaxes toward the target (time constant 20 s) and diffuses 15% toward its neighbours.
    /// Colour bands follow India's National AQI categories (Good, Satisfactory, Moderate, Poor, Very Poor, Severe).
    /// </summary>
    public class PollutionManager : MonoBehaviour
    {
        public float[,] Aqi { get; private set; }
        public float CityAverageAqi { get; private set; }
        public float MaxAqi { get; private set; }
        public Vector2Int MaxCell { get; private set; }
        public int CellsAboveWarning { get; private set; }

        private float[,] _spike;
        private bool[,] _alerted;
        private SimulationController _sim;
        private RoadNetworkManager _net;
        private float _timer;
        private Renderer[,] _tiles;
        private WorldLabel[,] _labels;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Transform _overlayRoot;

        public void Initialize(SimulationController sim)
        {
            _sim = sim; _net = sim.roadNetwork;
            var l = _net.Layout;
            Aqi = new float[l.CellsX, l.CellsY];
            _spike = new float[l.CellsX, l.CellsY];
            _alerted = new bool[l.CellsX, l.CellsY];
            BuildOverlay();
            ResetState();
        }

        public static float BaseAqi(ZoneType z)
        {
            switch (z)
            {
                case ZoneType.Industrial: return 125f;
                case ZoneType.Commercial: return 70f;
                case ZoneType.Parking: return 80f;
                case ZoneType.Residential: return 55f;
                case ZoneType.Park: return 28f;
                default: return 50f;
            }
        }

        public void ResetState()
        {
            var l = _net.Layout;
            for (int y = 0; y < l.CellsY; y++)
                for (int x = 0; x < l.CellsX; x++)
                {
                    Aqi[x, y] = BaseAqi(l.Zones[x, y]);
                    _spike[x, y] = 0f;
                    _alerted[x, y] = false;
                }
            UpdateAggregates();
            RefreshOverlay();
        }

        public void AddSpike(int cx, int cy, float amount)
        {
            _spike[cx, cy] += amount;
        }

        public void ClearSpike(int cx, int cy) { _spike[cx, cy] = 0f; }

        public float AqiAt(Vector3 worldPos)
        {
            int cx, cy;
            _net.CellOf(worldPos, out cx, out cy);
            return Aqi[cx, cy];
        }

        public void Step(float dt)
        {
            _timer += dt;
            if (_timer < 0.5f) return;
            float step = _timer;
            _timer = 0f;
            var l = _net.Layout;
            var next = new float[l.CellsX, l.CellsY];
            float relax = 1f - Mathf.Exp(-step / 20f);
            for (int y = 0; y < l.CellsY; y++)
                for (int x = 0; x < l.CellsX; x++)
                {
                    float emission = TrafficSimulationManager.EmissionWeight(_net.LanesAroundCell(x, y));
                    if (l.LowEmission[x, y]) emission *= 0.5f;
                    float target = BaseAqi(l.Zones[x, y]) + 3.2f * emission - 12f * ParkNeighbours(x, y) + _spike[x, y];
                    target = Mathf.Clamp(target, 10f, 500f);
                    float v = Aqi[x, y] + (target - Aqi[x, y]) * relax;
                    // diffusion
                    float sum = 0f; int n = 0;
                    if (x > 0) { sum += Aqi[x - 1, y]; n++; }
                    if (x < l.CellsX - 1) { sum += Aqi[x + 1, y]; n++; }
                    if (y > 0) { sum += Aqi[x, y - 1]; n++; }
                    if (y < l.CellsY - 1) { sum += Aqi[x, y + 1]; n++; }
                    if (n > 0) v = Mathf.Lerp(v, sum / n, 0.15f * Mathf.Clamp01(step));
                    next[x, y] = Mathf.Clamp(v, 0f, 500f);
                    // spikes decay with a 60 s half-life
                    _spike[x, y] *= Mathf.Pow(0.5f, step / 60f);
                    if (_spike[x, y] < 1f) _spike[x, y] = 0f;
                }
            Aqi = next;
            UpdateAggregates();
            CheckThresholds();
            if (_sim.layers != null && _sim.layers.Mode == LayerMode.Pollution) RefreshOverlay();
        }

        private int ParkNeighbours(int x, int y)
        {
            var l = _net.Layout; int n = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int px = x + dx, py = y + dy;
                    if (px >= 0 && py >= 0 && px < l.CellsX && py < l.CellsY && l.Zones[px, py] == ZoneType.Park) n++;
                }
            return n;
        }

        private void UpdateAggregates()
        {
            var l = _net.Layout;
            float sum = 0f, max = 0f; int above = 0;
            Vector2Int maxCell = Vector2Int.zero;
            for (int y = 0; y < l.CellsY; y++)
                for (int x = 0; x < l.CellsX; x++)
                {
                    float a = Aqi[x, y];
                    sum += a;
                    if (a > max) { max = a; maxCell = new Vector2Int(x, y); }
                    if (a >= _sim.settings.aqiWarningThreshold) above++;
                }
            CityAverageAqi = sum / (l.CellsX * l.CellsY);
            MaxAqi = max; MaxCell = maxCell; CellsAboveWarning = above;
        }

        private void CheckThresholds()
        {
            var s = _sim.settings; var l = _net.Layout;
            for (int y = 0; y < l.CellsY; y++)
                for (int x = 0; x < l.CellsX; x++)
                {
                    float a = Aqi[x, y];
                    if (!_alerted[x, y] && a >= s.aqiWarningThreshold)
                    {
                        _alerted[x, y] = true;
                        bool critical = a >= s.aqiCriticalThreshold;
                        CityEvents.RaiseAlert($"AQI ALERT: {ZoneLabel(x, y)} reached {a:0} (simulated){(critical ? " - CRITICAL" : "")}", critical ? AlertLevel.Critical : AlertLevel.Warning);
                    }
                    else if (_alerted[x, y] && a < s.aqiWarningThreshold - 20f)
                    {
                        _alerted[x, y] = false;
                        CityEvents.RaiseAlert($"AQI back below threshold in {ZoneLabel(x, y)} ({a:0}).", AlertLevel.Success);
                    }
                }
        }

        public string ZoneLabel(int x, int y) => $"{CityLayout.ZoneName(_net.Layout.Zones[x, y])} block {(char)('A' + x)}{y + 1}";

        public static string Category(float aqi)
        {
            if (aqi <= 50) return "Good";
            if (aqi <= 100) return "Satisfactory";
            if (aqi <= 200) return "Moderate";
            if (aqi <= 300) return "Poor";
            if (aqi <= 400) return "Very Poor";
            return "Severe";
        }

        public static Color CategoryColor(float aqi)
        {
            if (aqi <= 50) return new Color(0.0f, 0.78f, 0.33f);
            if (aqi <= 100) return new Color(0.6f, 0.85f, 0.2f);
            if (aqi <= 200) return new Color(1f, 0.84f, 0.0f);
            if (aqi <= 300) return new Color(1f, 0.55f, 0.0f);
            if (aqi <= 400) return new Color(0.95f, 0.15f, 0.15f);
            return new Color(0.6f, 0.05f, 0.25f);
        }

        // ------------------------------------------------------------------ heatmap overlay

        private void BuildOverlay()
        {
            var l = _net.Layout;
            _overlayRoot = new GameObject("Pollution Heatmap").transform;
            _overlayRoot.SetParent(transform, false);
            _tiles = new Renderer[l.CellsX, l.CellsY];
            _labels = new WorldLabel[l.CellsX, l.CellsY];
            var mat = MaterialLibrary.Get("overlay");
            for (int y = 0; y < l.CellsY; y++)
                for (int x = 0; x < l.CellsX; x++)
                {
                    var mb = new MeshBuilder();
                    mb.AddFlat(0, Vector3.zero, _net.Spacing - 0.5f, _net.Spacing - 0.5f);
                    var go = mb.Build($"AQI tile {x},{y}", _overlayRoot, mat);
                    go.isStatic = false;
                    go.transform.position = _net.CellCenter(x, y) + Vector3.up * 0.18f;
                    var r = go.GetComponent<MeshRenderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    _tiles[x, y] = r;
                    _labels[x, y] = WorldLabel.Create(_overlayRoot, _net.CellCenter(x, y) + Vector3.up * 30f, "", Color.white, 1.3f);
                    if (l.LowEmission[x, y])
                    {
                        var lez = new MeshBuilder();
                        float h = (_net.Spacing - _net.RoadWidth) * 0.5f;
                        lez.AddFlat(0, new Vector3(0, 0, h), 2 * h, 0.5f);
                        lez.AddFlat(0, new Vector3(0, 0, -h), 2 * h, 0.5f);
                        lez.AddFlat(0, new Vector3(h, 0, 0), 0.5f, 2 * h);
                        lez.AddFlat(0, new Vector3(-h, 0, 0), 0.5f, 2 * h);
                        var lgo = lez.Build("LEZ outline", go.transform, MaterialLibrary.Get("laneMarking"));
                        lgo.transform.localPosition = Vector3.up * 0.05f;
                    }
                }
            SetOverlayVisible(false);
        }

        public void SetOverlayVisible(bool visible)
        {
            if (_overlayRoot != null) _overlayRoot.gameObject.SetActive(visible);
            if (visible) RefreshOverlay();
        }

        public void RefreshOverlay()
        {
            if (_tiles == null || _overlayRoot == null || !_overlayRoot.gameObject.activeSelf) return;
            var l = _net.Layout;
            for (int y = 0; y < l.CellsY; y++)
                for (int x = 0; x < l.CellsX; x++)
                {
                    Color c = CategoryColor(Aqi[x, y]); c.a = 0.62f;
                    MaterialUtil.SetRendererColor(_tiles[x, y], _block, c);
                    var lab = _labels[x, y];
                    lab.Text.text = $"AQI {Aqi[x, y]:0}\n{Category(Aqi[x, y])}{(l.LowEmission[x, y] ? "\nLow-emission zone" : "")}";
                    lab.Text.color = Color.Lerp(CategoryColor(Aqi[x, y]), Color.white, 0.5f);
                }
        }
    }
}
