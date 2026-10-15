using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Material asset collection created by Smart City > Build Scene (Assets/Resources/CityPalette.asset).
    /// Keeping materials as assets guarantees their shaders are included in Windows builds.
    /// </summary>
    [CreateAssetMenu(menuName = "Smart City/City Palette", fileName = "CityPalette")]
    public class CityPalette : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public Material material;
        }

        public List<Entry> entries = new List<Entry>();

        public Material Find(string key)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].key == key) return entries[i].material;
            return null;
        }

        public void Set(string key, Material material)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].key == key) { entries[i].material = material; return; }
            entries.Add(new Entry { key = key, material = material });
        }
    }

    /// <summary>
    /// Runtime material access. Palette assets are cloned on first use so runtime colour changes
    /// (traffic lights, night lighting) never modify the asset files in the Editor.
    /// Any material missing from the palette is generated from <see cref="MaterialDefinitions"/>.
    /// </summary>
    public static class MaterialLibrary
    {
        private static CityPalette _palette;
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Cache.Clear(); Textures.Clear(); _palette = null; }

        public static void Initialize(CityPalette palette)
        {
            _palette = palette;
            Cache.Clear();
        }

        public static Material Get(string key)
        {
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            Material source = _palette != null ? _palette.Find(key) : null;
            if (source != null && source.shader != null && source.shader.isSupported)
            {
                m = new Material(source) { name = source.name + " (runtime)" };
            }
            else
            {
                m = MaterialDefinitions.Create(key, GetTexture);
            }
            Cache[key] = m;
            return m;
        }

        public static Texture2D GetTexture(string key)
        {
            Texture2D t;
            if (Textures.TryGetValue(key, out t) && t != null) return t;
            t = TextureFactory.Create(key);
            Textures[key] = t;
            return t;
        }
    }
}
