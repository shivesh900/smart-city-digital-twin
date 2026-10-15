using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>Procedurally rasterised UI icons (anti-aliased by super-sampling) - no image assets required.</summary>
    public static class IconFactory
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Cache.Clear(); }

        public static Texture2D Get(string name)
        {
            Texture2D t;
            if (Cache.TryGetValue(name, out t) && t != null) return t;
            t = Render(Shape(name));
            t.name = "Icon " + name;
            Cache[name] = t;
            return t;
        }

        private static Texture2D Render(Func<float, float, bool> inside, int size = 64, int ss = 4)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float inv = 1f / (size * ss);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                            if (inside((x * ss + sx + 0.5f) * inv, (y * ss + sy + 0.5f) * inv)) hit++;
                    px[y * size + x] = new Color(1, 1, 1, hit / (float)(ss * ss));
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static bool Circle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
        private static bool Box(float x, float y, float x0, float y0, float x1, float y1) => x >= x0 && x <= x1 && y >= y0 && y <= y1;

        private static bool Poly(float x, float y, float[] p)
        {
            bool c = false;
            int n = p.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
                if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) c = !c;
            }
            return c;
        }

        private static bool Segment(float x, float y, float ax, float ay, float bx, float by, float w)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy));
            float dx = ax + vx * t - x, dy = ay + vy * t - y;
            return dx * dx + dy * dy <= w * w;
        }

        private static Func<float, float, bool> Shape(string name)
        {
            switch (name)
            {
                case "vehicles":
                    return (x, y) =>
                    {
                        bool body = Box(x, y, 0.1f, 0.3f, 0.9f, 0.56f);
                        bool roof = Poly(x, y, new[] { 0.25f, 0.56f, 0.36f, 0.78f, 0.66f, 0.78f, 0.77f, 0.56f });
                        bool wheels = Circle(x, y, 0.3f, 0.3f, 0.11f) || Circle(x, y, 0.7f, 0.3f, 0.11f);
                        bool hubs = Circle(x, y, 0.3f, 0.3f, 0.045f) || Circle(x, y, 0.7f, 0.3f, 0.045f);
                        bool window = Box(x, y, 0.38f, 0.59f, 0.64f, 0.74f);
                        return (body || roof || wheels) && !hubs && !window;
                    };
                case "speed":
                    return (x, y) =>
                    {
                        float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.38f) * (y - 0.38f));
                        bool ring = d >= 0.33f && d <= 0.42f && y >= 0.38f;
                        return ring || Segment(x, y, 0.5f, 0.38f, 0.72f, 0.62f, 0.045f) || Circle(x, y, 0.5f, 0.38f, 0.08f);
                    };
                case "congestion":
                    return (x, y) => Box(x, y, 0.12f, 0.15f, 0.3f, 0.4f) || Box(x, y, 0.41f, 0.15f, 0.59f, 0.62f) || Box(x, y, 0.7f, 0.15f, 0.88f, 0.86f);
                case "incident":
                    return (x, y) =>
                    {
                        bool tri = Poly(x, y, new[] { 0.5f, 0.9f, 0.06f, 0.12f, 0.94f, 0.12f });
                        bool mark = Box(x, y, 0.455f, 0.38f, 0.545f, 0.68f) || Circle(x, y, 0.5f, 0.25f, 0.055f);
                        return tri && !mark;
                    };
                case "aqi":
                    return (x, y) => Circle(x, y, 0.33f, 0.45f, 0.17f) || Circle(x, y, 0.55f, 0.56f, 0.22f) || Circle(x, y, 0.74f, 0.44f, 0.15f) || Box(x, y, 0.2f, 0.28f, 0.85f, 0.45f);
                case "energy":
                    return (x, y) => Poly(x, y, new[] { 0.6f, 0.94f, 0.2f, 0.44f, 0.47f, 0.44f, 0.38f, 0.06f, 0.8f, 0.58f, 0.52f, 0.58f });
                case "response":
                    return (x, y) =>
                    {
                        float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.44f) * (y - 0.44f));
                        bool ring = d >= 0.29f && d <= 0.38f;
                        return ring || Box(x, y, 0.42f, 0.84f, 0.58f, 0.92f) || Box(x, y, 0.47f, 0.8f, 0.53f, 0.86f)
                               || Segment(x, y, 0.5f, 0.44f, 0.5f, 0.66f, 0.04f) || Segment(x, y, 0.5f, 0.44f, 0.64f, 0.38f, 0.04f);
                    };
                case "signal":
                    return (x, y) =>
                    {
                        bool body = Box(x, y, 0.32f, 0.06f, 0.68f, 0.94f);
                        bool lamps = Circle(x, y, 0.5f, 0.76f, 0.1f) || Circle(x, y, 0.5f, 0.5f, 0.1f) || Circle(x, y, 0.5f, 0.24f, 0.1f);
                        return body && !lamps;
                    };
                case "route":
                    return (x, y) => Circle(x, y, 0.25f, 0.25f, 0.1f) || Circle(x, y, 0.75f, 0.78f, 0.1f)
                                     || Segment(x, y, 0.25f, 0.25f, 0.25f, 0.6f, 0.04f) || Segment(x, y, 0.25f, 0.6f, 0.75f, 0.6f, 0.04f) || Segment(x, y, 0.75f, 0.6f, 0.75f, 0.78f, 0.04f);
                default:
                    return (x, y) => Circle(x, y, 0.5f, 0.5f, 0.35f);
            }
        }
    }
}
