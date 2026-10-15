using UnityEngine;

namespace SmartCity
{
    /// <summary>Procedural textures (no image files needed): ground grid and building facades.</summary>
    public static class TextureFactory
    {
        public static Texture2D Create(string key)
        {
            switch (key)
            {
                case "groundGrid": return Grid(128, new Color(0.045f, 0.06f, 0.08f), new Color(0.08f, 0.12f, 0.16f), false);
                case "groundGridGlow": return Grid(128, Color.black, new Color(0.0f, 0.55f, 0.65f), true);
                case "facade": return Facade(false);
                case "facadeLights": return Facade(true);
                default: return null;
            }
        }

        private static Texture2D Grid(int size, Color background, Color line, bool glowOnly)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = glowOnly ? "GroundGridGlow" : "GroundGrid", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool major = x < 2 || y < 2;
                    bool minor = (x % 32 == 0) || (y % 32 == 0);
                    Color c = background;
                    if (major) c = line;
                    else if (minor) c = Color.Lerp(background, line, glowOnly ? 0.25f : 0.5f);
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>8x8 grid of windows. Base: frames + glass. Lights: random lit windows (emission map).</summary>
        private static Texture2D Facade(bool lights)
        {
            const int size = 256, cells = 8, cell = size / cells;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = lights ? "FacadeLights" : "Facade", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var rng = new System.Random(4242);
            var px = new Color[size * size];
            var frame = new Color(0.30f, 0.33f, 0.37f);
            var glass = new Color(0.62f, 0.70f, 0.78f);
            for (int cy = 0; cy < cells; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    bool lit = rng.NextDouble() < 0.55;
                    float warmth = (float)rng.NextDouble();
                    float level = 0.55f + 0.45f * (float)rng.NextDouble();
                    Color lightColor = Color.Lerp(new Color(1f, 0.82f, 0.55f), new Color(0.75f, 0.92f, 1f), warmth) * level;
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            bool window = x >= 5 && x < cell - 5 && y >= 7 && y < cell - 6;
                            Color c;
                            if (lights) c = window && lit ? lightColor : Color.black;
                            else c = window ? glass * (0.85f + 0.15f * (y / (float)cell)) : frame;
                            px[(cy * cell + y) * size + cx * cell + x] = c;
                        }
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }
    }
}
