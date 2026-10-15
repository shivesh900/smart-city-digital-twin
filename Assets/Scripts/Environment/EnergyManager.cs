using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Simulated electricity demand (model output, not metered data):
    ///   building kW = floor area (m2) x intensity (W/m2 by zone) x hourly profile (0..1) / 1000
    ///   streetlights 0.15 kW each between 18:00 and 06:00 (dimmed to 60% between 00:00 and 05:00)
    ///   traffic signals 0.3 kW each.
    /// </summary>
    public class EnergyManager : MonoBehaviour
    {
        public float TotalMW { get; private set; }
        public float BuildingsMW { get; private set; }
        public float StreetlightsKW { get; private set; }
        public float SignalsKW { get; private set; }
        public float PeakMW { get; private set; }
        public bool StreetlightsOn { get; private set; }
        public float[,] CellKw { get; private set; }

        private SimulationController _sim;
        private float _timer;
        private Material[] _energyMats;
        private WorldLabel[,] _labels;
        private Transform _labelRoot;
        private float _maxIntensity = 1f;

        public static float Intensity(ZoneType z)
        {
            switch (z)
            {
                case ZoneType.Commercial: return 22f;
                case ZoneType.Industrial: return 30f;
                case ZoneType.Hospital: return 38f;
                case ZoneType.Police: return 18f;
                case ZoneType.FireStation: return 14f;
                default: return 9f;
            }
        }

        /// <summary>Share of peak load for a zone at a given hour.</summary>
        public static float Profile(ZoneType z, float hour)
        {
            float Bell(float h, float center, float width)
            {
                float d = Mathf.DeltaAngle(h * 15f, center * 15f) / 15f;
                return Mathf.Exp(-(d * d) / (2f * width * width));
            }
            switch (z)
            {
                case ZoneType.Residential: return 0.3f + 0.35f * Bell(hour, 7.5f, 1.5f) + 0.6f * Bell(hour, 20.5f, 2.2f);
                case ZoneType.Commercial: return 0.2f + 0.8f * Bell(hour, 14f, 3.5f);
                case ZoneType.Industrial: return 0.6f + 0.4f * Bell(hour, 13f, 5f);
                case ZoneType.Hospital: return 0.85f + 0.15f * Bell(hour, 12f, 4f);
                default: return 0.6f + 0.2f * Bell(hour, 14f, 4f);
            }
        }

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            _energyMats = new Material[6];
            for (int i = 0; i < 6; i++) _energyMats[i] = MaterialLibrary.Get("energy" + i);
            var l = sim.roadNetwork.Layout;
            CellKw = new float[l.CellsX, l.CellsY];
            _labelRoot = new GameObject("Energy Labels").transform;
            _labelRoot.SetParent(transform, false);
            _labels = new WorldLabel[l.CellsX, l.CellsY];
            for (int y = 0; y < l.CellsY; y++)
                for (int x = 0; x < l.CellsX; x++)
                    _labels[x, y] = WorldLabel.Create(_labelRoot, sim.roadNetwork.CellCenter(x, y) + Vector3.up * 32f, "", Color.white, 1.2f);
            _labelRoot.gameObject.SetActive(false);
            ResetState();
        }

        public void ResetState()
        {
            PeakMW = 0f;
            _timer = 1f;
            Step(0f);
        }

        public void Step(float dt)
        {
            _timer += dt;
            if (_timer < 1f) return;
            _timer = 0f;
            float hour = _sim.HourOfDay;
            var l = _sim.roadNetwork.Layout;
            for (int y = 0; y < l.CellsY; y++) for (int x = 0; x < l.CellsX; x++) CellKw[x, y] = 0f;

            float buildingsKw = 0f;
            _maxIntensity = 0.001f;
            foreach (var b in _sim.cityGenerator.Buildings)
            {
                b.LoadKw = b.FloorArea * Intensity(b.Zone) * Profile(b.Zone, hour) / 1000f;
                buildingsKw += b.LoadKw;
                CellKw[b.CellX, b.CellY] += b.LoadKw;
                _maxIntensity = Mathf.Max(_maxIntensity, b.LoadKw / Mathf.Max(1f, b.Size.x * b.Size.z));
            }
            bool night = hour >= 18f || hour < 6f;
            StreetlightsOn = night;
            float dim = (hour >= 0f && hour < 5f) ? 0.6f : 1f;
            StreetlightsKW = night ? _sim.cityGenerator.StreetlightCount * 0.15f * dim : 0f;
            SignalsKW = _sim.roadNetwork.Intersections.Count * 0.3f;
            BuildingsMW = buildingsKw / 1000f;
            TotalMW = (buildingsKw + StreetlightsKW + SignalsKW) / 1000f;
            PeakMW = Mathf.Max(PeakMW, TotalMW);
            if (_sim.layers != null && _sim.layers.Mode == LayerMode.Energy) ApplyEnergyColors();
        }

        public void SetEnergyView(bool on)
        {
            _labelRoot.gameObject.SetActive(on);
            if (on) ApplyEnergyColors();
            else
                foreach (var b in _sim.cityGenerator.Buildings)
                    b.Renderer.sharedMaterials = b.OriginalMaterials;
        }

        private void ApplyEnergyColors()
        {
            foreach (var b in _sim.cityGenerator.Buildings)
            {
                float intensity = b.LoadKw / Mathf.Max(1f, b.Size.x * b.Size.z);
                int level = Mathf.Clamp(Mathf.FloorToInt(intensity / _maxIntensity * 5.99f), 0, 5);
                var mats = b.Renderer.sharedMaterials;
                if (mats.Length > 0 && mats[0] != _energyMats[level])
                {
                    mats[0] = _energyMats[level];
                    b.Renderer.sharedMaterials = mats;
                }
            }
            var l = _sim.roadNetwork.Layout;
            for (int y = 0; y < l.CellsY; y++)
                for (int x = 0; x < l.CellsX; x++)
                {
                    float kw = CellKw[x, y];
                    _labels[x, y].Text.text = kw > 0.5f ? $"{kw:0} kW" : "";
                }
        }
    }
}
