using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartCity
{
    /// <summary>Every material the city uses, defined in code so it can be generated with no external assets.</summary>
    public static class MaterialDefinitions
    {
        public struct Def
        {
            public string Key;
            public Color Color;
            public float Smoothness;
            public float Metallic;
            public Color Emission;
            public bool Overlay;
            public string Texture;
            public string EmissionTexture;
            public Vector2 Tiling;
        }

        private static Def D(string key, string hex, float smooth = 0.3f, float metal = 0f, string emissionHex = null, float emissionIntensity = 0f,
            bool overlay = false, string tex = null, string emTex = null, float tiling = 1f)
        {
            Color c; ColorUtility.TryParseHtmlString(hex, out c);
            Color e = Color.black;
            if (emissionHex != null) { ColorUtility.TryParseHtmlString(emissionHex, out e); e *= emissionIntensity; }
            return new Def { Key = key, Color = c, Smoothness = smooth, Metallic = metal, Emission = e, Overlay = overlay, Texture = tex, EmissionTexture = emTex, Tiling = new Vector2(tiling, tiling) };
        }

        public static readonly Def[] All =
        {
            // Ground & roads
            D("ground", "#FFFFFF", 0.15f, 0f, "#FFFFFF", 0.55f, tex: "groundGrid", emTex: "groundGridGlow", tiling: 1f / 12f),
            D("road", "#1B2129", 0.35f),
            D("sidewalk", "#3B434E", 0.2f),
            D("curb", "#59636F", 0.2f),
            D("laneMarking", "#7DF9FF", 0.4f, 0f, "#00E5FF", 1.6f),
            D("edgeMarking", "#DDE6EE", 0.4f, 0f, "#DDE6EE", 0.35f),
            D("crosswalk", "#E8EEF2", 0.3f, 0f, "#E8EEF2", 0.25f),
            D("parkingLot", "#242A32", 0.25f),
            D("parkingStripe", "#C9D2DB", 0.3f, 0f, "#C9D2DB", 0.2f),
            // Buildings
            D("bldResidential", "#D7B48E", 0.45f, 0f, "#FFC77A", 1.5f, tex: "facade", emTex: "facadeLights", tiling: 1f / 24f),
            D("bldCommercial", "#86B5E6", 0.85f, 0.3f, "#BFE9FF", 1.8f, tex: "facade", emTex: "facadeLights", tiling: 1f / 24f),
            D("bldIndustrial", "#8C9097", 0.25f, 0.1f, "#FFB74D", 0.7f, tex: "facade", emTex: "facadeLights", tiling: 1f / 24f),
            D("bldHospital", "#EEF2F5", 0.6f, 0f, "#E0F7FA", 1.3f, tex: "facade", emTex: "facadeLights", tiling: 1f / 24f),
            D("bldPolice", "#4C79E0", 0.6f, 0.2f, "#BBDEFB", 1.3f, tex: "facade", emTex: "facadeLights", tiling: 1f / 24f),
            D("bldFire", "#D84343", 0.5f, 0.1f, "#FFE0B2", 1.2f, tex: "facade", emTex: "facadeLights", tiling: 1f / 24f),
            D("roof", "#20252C", 0.3f, 0.2f),
            D("roofAccent", "#00E5FF", 0.5f, 0f, "#00E5FF", 2.2f),
            D("medicalRed", "#E53935", 0.4f, 0f, "#FF1744", 1.4f),
            D("garageDoor", "#2B2F36", 0.5f, 0.6f),
            D("chimney", "#5D636B", 0.2f),
            D("helipad", "#2E7D32", 0.3f),
            D("white", "#F2F5F7", 0.3f, 0f, "#FFFFFF", 0.3f),
            // Nature
            D("grass", "#1E7A43", 0.15f),
            D("parkPath", "#B9A88A", 0.15f),
            D("water", "#1E88E5", 0.95f, 0.1f, "#0D47A1", 0.4f),
            D("treeTrunk", "#5D4037", 0.1f),
            D("treeLeaves", "#2E9E5B", 0.2f),
            D("treeLeaves2", "#3FB56A", 0.2f),
            // Street furniture
            D("pole", "#3A434F", 0.5f, 0.6f),
            D("lampOn", "#FFF3C4", 0.5f, 0f, "#FFE08A", 3.0f),
            D("lampOff", "#5A5F66", 0.5f),
            D("signalHead", "#101317", 0.5f, 0.3f),
            D("sigRedOn", "#FF1744", 0.6f, 0f, "#FF1744", 4f),
            D("sigRedOff", "#3A1010", 0.6f),
            D("sigYellowOn", "#FFD600", 0.6f, 0f, "#FFC400", 4f),
            D("sigYellowOff", "#3A3210", 0.6f),
            D("sigGreenOn", "#00E676", 0.6f, 0f, "#00E676", 4f),
            D("sigGreenOff", "#0F3320", 0.6f),
            D("signBlue", "#1565C0", 0.4f, 0f, "#1565C0", 0.6f),
            D("signRed", "#C62828", 0.4f, 0f, "#C62828", 0.6f),
            D("bench", "#6D4C41", 0.2f),
            D("pedestrian", "#B0BEC5", 0.3f),
            D("pedestrian2", "#FFB74D", 0.3f),
            D("pedestrian3", "#4DD0E1", 0.3f),
            // Vehicles
            D("car0", "#E53935", 0.85f, 0.5f), D("car1", "#1E88E5", 0.85f, 0.5f), D("car2", "#FDD835", 0.85f, 0.5f),
            D("car3", "#ECEFF1", 0.85f, 0.5f), D("car4", "#37474F", 0.85f, 0.5f), D("car5", "#43A047", 0.85f, 0.5f),
            D("bus", "#FB8C00", 0.7f, 0.3f), D("truck", "#8D6E63", 0.5f, 0.2f),
            D("ambulance", "#F5F5F5", 0.8f, 0.3f), D("fireTruck", "#D32F2F", 0.8f, 0.4f), D("police", "#0D47A1", 0.8f, 0.4f),
            D("vehicleGlass", "#0B1220", 0.95f, 0.2f), D("tire", "#111111", 0.2f),
            D("headlight", "#FFFDE7", 0.5f, 0f, "#FFF59D", 2.5f),
            D("sirenRed", "#FF1744", 0.5f, 0f, "#FF1744", 5f), D("sirenBlue", "#2979FF", 0.5f, 0f, "#2979FF", 5f),
            D("sirenOff", "#30343A", 0.5f),
            // Incident markers
            D("markerAccident", "#FF1744", 0.5f, 0f, "#FF1744", 3f),
            D("markerBlocked", "#FF9100", 0.5f, 0f, "#FF9100", 3f),
            D("markerSignal", "#FFEA00", 0.5f, 0f, "#FFEA00", 3f),
            D("markerPollution", "#D500F9", 0.5f, 0f, "#D500F9", 3f),
            D("markerEmergency", "#FF4081", 0.5f, 0f, "#FF4081", 3f),
            D("barrier", "#FF6D00", 0.4f, 0f, "#FF6D00", 1.2f),
            // Energy layer gradient (0 = low ... 5 = high)
            D("energy0", "#0D47A1", 0.5f, 0f, "#1565C0", 1.2f), D("energy1", "#00838F", 0.5f, 0f, "#00ACC1", 1.3f),
            D("energy2", "#2E7D32", 0.5f, 0f, "#43A047", 1.4f), D("energy3", "#F9A825", 0.5f, 0f, "#FDD835", 1.6f),
            D("energy4", "#EF6C00", 0.5f, 0f, "#FB8C00", 1.8f), D("energy5", "#C62828", 0.5f, 0f, "#E53935", 2.2f),
            // Transparent overlay (lines, heatmap tiles, selection) - vertex/tint coloured
            D("overlay", "#FFFFFF", 0f, 0f, overlay: true),
        };

        private static Dictionary<string, Def> _byKey;

        public static bool TryGet(string key, out Def def)
        {
            if (_byKey == null)
            {
                _byKey = new Dictionary<string, Def>();
                foreach (var d in All) _byKey[d.Key] = d;
            }
            return _byKey.TryGetValue(key, out def);
        }

        public static Material Create(string key, Func<string, Texture2D> textureProvider)
        {
            Def d;
            if (!TryGet(key, out d))
            {
                Debug.LogWarning($"[SmartCity] Unknown material key '{key}', using magenta debug colour.");
                d = new Def { Key = key, Color = Color.magenta, Smoothness = 0.3f };
            }
            return Create(d, textureProvider);
        }

        public static Material Create(Def d, Func<string, Texture2D> textureProvider)
        {
            Material m;
            if (d.Overlay)
            {
                m = new Material(PipelineShaders.Overlay()) { name = d.Key };
                m.color = d.Color;
                m.renderQueue = 3000;
                return m;
            }
            m = new Material(PipelineShaders.Lit()) { name = d.Key };
            MaterialUtil.SetBaseColor(m, d.Color);
            MaterialUtil.SetSurface(m, d.Smoothness, d.Metallic);
            if (d.Texture != null && textureProvider != null)
            {
                var tex = textureProvider(d.Texture);
                if (tex != null)
                {
                    MaterialUtil.SetMainTexture(m, tex, d.Tiling);
                }
            }
            if (d.Emission.maxColorComponent > 0.001f)
            {
                Texture2D emTex = d.EmissionTexture != null && textureProvider != null ? textureProvider(d.EmissionTexture) : null;
                MaterialUtil.SetEmission(m, d.Emission, emTex);
            }
            m.enableInstancing = true;
            return m;
        }
    }

    /// <summary>Picks shaders for whichever render pipeline is active (URP or the built-in pipeline).</summary>
    public static class PipelineShaders
    {
        public static bool UsingScriptablePipeline => GraphicsSettings.currentRenderPipeline != null;

        public static Shader Lit()
        {
            Shader s = null;
            if (UsingScriptablePipeline) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Unlit/Color");
            return s;
        }

        public static Shader Overlay()
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null && UsingScriptablePipeline) s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            return s;
        }
    }

    public static class MaterialUtil
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public static void SetBaseColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }

        public static void SetSurface(Material m, float smoothness, float metallic)
        {
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        }

        public static void SetMainTexture(Material m, Texture tex, Vector2 tiling)
        {
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tiling); }
            if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", tiling); }
        }

        public static void SetEmission(Material m, Color emission, Texture emissionMap)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, emission);
            if (emissionMap != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", emissionMap);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        public static Color GetEmission(Material m) => m.HasProperty(EmissionColorId) ? m.GetColor(EmissionColorId) : Color.black;

        public static void SetEmissionColor(Material m, Color c)
        {
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, c);
        }

        /// <summary>Colours a renderer without creating a material instance (keeps batching friendly).</summary>
        public static void SetRendererColor(Renderer r, MaterialPropertyBlock block, Color c)
        {
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }
    }
}
