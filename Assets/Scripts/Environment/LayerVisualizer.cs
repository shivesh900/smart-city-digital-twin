using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    public enum LayerMode { Normal, Traffic, Pollution, Energy }

    /// <summary>Switches the 3D view between normal, traffic (congestion), pollution (AQI heatmap) and energy layers.</summary>
    public class LayerVisualizer : MonoBehaviour
    {
        public LayerMode Mode { get; private set; } = LayerMode.Normal;
        private SimulationController _sim;
        private readonly List<WorldLabel> _junctionLabels = new List<WorldLabel>();
        private Transform _labelRoot;
        private float _timer;

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            _labelRoot = new GameObject("Junction Labels").transform;
            _labelRoot.SetParent(transform, false);
            foreach (var j in sim.roadNetwork.Intersections)
                _junctionLabels.Add(WorldLabel.Create(_labelRoot, j.Position + Vector3.up * 10f, "", Color.white, 0.9f));
            _labelRoot.gameObject.SetActive(false);
            SetMode(LayerMode.Normal, false);
        }

        public void SetMode(LayerMode mode, bool announce = true)
        {
            Mode = mode;
            _sim.roadNetwork.SetTrafficOverlayVisible(mode == LayerMode.Traffic);
            if (mode == LayerMode.Traffic) _sim.roadNetwork.UpdateTrafficOverlayColors();
            _labelRoot.gameObject.SetActive(mode == LayerMode.Traffic);
            _sim.pollution.SetOverlayVisible(mode == LayerMode.Pollution);
            _sim.energy.SetEnergyView(mode == LayerMode.Energy);
            if (announce)
            {
                string what = mode == LayerMode.Traffic ? "TRAFFIC layer: lanes coloured by congestion, labels show queues"
                    : mode == LayerMode.Pollution ? "POLLUTION layer: simulated AQI heatmap per block (India NAQI colour bands)"
                    : mode == LayerMode.Energy ? "ENERGY layer: buildings coloured by simulated electrical load"
                    : "Normal view";
                CityEvents.RaiseAlert(what, AlertLevel.Info);
            }
        }

        private void Update()
        {
            if (_sim == null || Mode != LayerMode.Traffic) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer < 0.5f) return;
            _timer = 0f;
            var js = _sim.roadNetwork.Intersections;
            for (int i = 0; i < js.Count; i++)
            {
                var sig = js[i].Signal;
                var lab = _junctionLabels[i];
                lab.Text.text = sig.Failed ? $"{_sim.roadNetwork.NodeName(js[i].NodeId)}\nFAILED" : $"{_sim.roadNetwork.NodeName(js[i].NodeId)}  Q {sig.TotalQueue}";
                lab.Text.color = sig.Failed ? new Color(1f, 0.85f, 0.2f) : RoadNetworkManager.CongestionColor(Mathf.Clamp01(sig.TotalQueue / 12f));
            }
        }
    }
}
