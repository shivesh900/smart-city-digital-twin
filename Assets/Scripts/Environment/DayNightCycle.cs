using UnityEngine;
using UnityEngine.Rendering;

namespace SmartCity
{
    /// <summary>Moves the sun with the simulated clock, switches streetlights and adjusts ambient light and fog.</summary>
    public class DayNightCycle : MonoBehaviour
    {
        public Light sun;
        private SimulationController _sim;
        private Material _lampOn, _lampOff;
        private bool _lightsOn;
        private bool _initialized;

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            if (sun == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null)
            {
                var go = new GameObject("Sun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
            }
            _lampOn = MaterialLibrary.Get("lampOn");
            _lampOff = MaterialLibrary.Get("lampOff");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            _initialized = true;
            _lightsOn = !_lightsOn;
            Step(0f);
        }

        public void Step(float dt)
        {
            if (!_initialized) return;
            float hour = _sim.HourOfDay;
            // Sun: rises at 6, sets at 18.
            float sunAngle = (hour - 6f) / 12f * 180f;
            float dayFactor = Mathf.Clamp01(Mathf.Sin(sunAngle * Mathf.Deg2Rad) * 1.4f);
            float elevation = Mathf.Lerp(-10f, 60f, Mathf.Clamp01(Mathf.Sin(sunAngle * Mathf.Deg2Rad)));
            float azimuth = 120f + hour * 7.5f;
            if (dayFactor > 0.02f)
            {
                sun.transform.rotation = Quaternion.Euler(Mathf.Max(8f, elevation), azimuth, 0f);
                sun.intensity = Mathf.Lerp(0.25f, 1.25f, dayFactor);
                sun.color = Color.Lerp(new Color(1f, 0.55f, 0.35f), new Color(1f, 0.96f, 0.9f), dayFactor);
            }
            else
            {
                // Moonlight keeps the city readable at night.
                sun.transform.rotation = Quaternion.Euler(40f, azimuth + 180f, 0f);
                sun.intensity = 0.22f;
                sun.color = new Color(0.55f, 0.65f, 1f);
            }
            Color nightSky = new Color(0.10f, 0.13f, 0.24f), daySky = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientSkyColor = Color.Lerp(nightSky, daySky, dayFactor);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.08f, 0.1f, 0.16f), new Color(0.42f, 0.45f, 0.5f), dayFactor);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.03f, 0.03f, 0.05f), new Color(0.2f, 0.2f, 0.22f), dayFactor);
            RenderSettings.fogColor = Color.Lerp(new Color(0.04f, 0.06f, 0.11f), new Color(0.45f, 0.55f, 0.68f), dayFactor);
            RenderSettings.fogDensity = Mathf.Lerp(0.0028f, 0.0016f, dayFactor);
            var cam = Camera.main;
            if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = RenderSettings.fogColor;

            bool lightsOn = hour >= 18f || hour < 6f;
            if (lightsOn != _lightsOn)
            {
                _lightsOn = lightsOn;
                foreach (var r in _sim.cityGenerator.StreetlightRenderers)
                {
                    var mats = r.sharedMaterials;
                    if (mats.Length > 1) { mats[1] = lightsOn ? _lampOn : _lampOff; r.sharedMaterials = mats; }
                }
            }
        }
    }
}
