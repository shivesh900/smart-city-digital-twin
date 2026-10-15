using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SmartCity
{
    /// <summary>Dark control-room colour palette.</summary>
    public static class UITheme
    {
        public static readonly Color Panel = new Color(0.035f, 0.06f, 0.10f, 0.86f);
        public static readonly Color PanelSolid = new Color(0.03f, 0.05f, 0.085f, 0.97f);
        public static readonly Color Card = new Color(0.07f, 0.11f, 0.17f, 0.92f);
        public static readonly Color Border = new Color(0.0f, 0.9f, 1f, 0.28f);
        public static readonly Color Accent = new Color(0.0f, 0.9f, 1f, 1f);
        public static readonly Color Accent2 = new Color(0.49f, 0.30f, 1f, 1f);
        public static readonly Color Text = new Color(0.90f, 0.95f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.56f, 0.64f, 0.75f, 1f);
        public static readonly Color Good = new Color(0.0f, 0.9f, 0.46f, 1f);
        public static readonly Color Warn = new Color(1f, 0.77f, 0.0f, 1f);
        public static readonly Color Bad = new Color(1f, 0.1f, 0.27f, 1f);
        public static readonly Color Button = new Color(0.09f, 0.16f, 0.25f, 1f);
        public static readonly Color ButtonActive = new Color(0.0f, 0.55f, 0.65f, 1f);

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static Color ForSeverity(SeverityLevel l)
        {
            switch (l)
            {
                case SeverityLevel.Critical: return new Color(1f, 0.1f, 0.35f);
                case SeverityLevel.High: return new Color(1f, 0.45f, 0.1f);
                case SeverityLevel.Medium: return Warn;
                default: return Good;
            }
        }
    }

    /// <summary>Creates uGUI elements in code (so the dashboard needs no prefabs and can never lose references).</summary>
    public static class UIFactory
    {
        private static Sprite _rounded, _outline, _circle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _rounded = null; _outline = null; _circle = null; }

        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = MakeRounded(64, 14, false));
        public static Sprite Outline => _outline != null ? _outline : (_outline = MakeRounded(64, 14, true));
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeRounded(64, 32, false, 0));

        private static Sprite MakeRounded(int size, int radius, bool outline, int border = -1)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = outline ? "UIOutline" : "UIRounded" };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    if (outline) a *= Mathf.Clamp01(d - (radius - 2.2f) + 0.5f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            int b = border < 0 ? radius + 2 : border;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float padding = 0f) => Place(rt, Vector2.zero, Vector2.one, new Vector2(padding, padding), new Vector2(-padding, -padding));

        public static Image Panel(Transform parent, string name, Color color, bool border = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            if (border)
            {
                var b = Rect("Border", rt);
                Stretch(b);
                var bi = b.gameObject.AddComponent<Image>();
                bi.sprite = Outline;
                bi.type = Image.Type.Sliced;
                bi.color = UITheme.Border;
                bi.raycastTarget = false;
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UIFonts.Default;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, UnityAction onClick, Color? color = null, int fontSize = 14)
        {
            var rt = Rect("Button " + label, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            var btn = rt.gameObject.AddComponent<Button>();
            var c = color ?? UITheme.Button;
            var colors = btn.colors;
            colors.normalColor = c;
            colors.highlightedColor = Color.Lerp(c, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(c, UITheme.Accent, 0.5f);
            colors.selectedColor = c;
            colors.disabledColor = new Color(c.r, c.g, c.b, 0.35f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(onClick);
            var t = Label(rt, label, fontSize, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 2f);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 9;
            t.resizeTextMaxSize = fontSize;
            return btn;
        }

        public static void SetButtonColor(Button b, Color c)
        {
            var colors = b.colors;
            colors.normalColor = c;
            colors.selectedColor = c;
            colors.highlightedColor = Color.Lerp(c, Color.white, 0.18f);
            b.colors = colors;
        }

        public static void SetButtonLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, UnityAction<float> onChange)
        {
            var root = Rect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();
            var bg = Rect("Background", root);
            Place(bg, new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);
            var bgi = bg.gameObject.AddComponent<Image>(); bgi.sprite = Rounded; bgi.type = Image.Type.Sliced; bgi.color = new Color(1, 1, 1, 0.12f);
            var fillArea = Rect("Fill Area", root);
            Place(fillArea, new Vector2(0, 0.35f), new Vector2(1, 0.65f), new Vector2(4, 0), new Vector2(-4, 0));
            var fill = Rect("Fill", fillArea);
            Stretch(fill);
            var fi = fill.gameObject.AddComponent<Image>(); fi.sprite = Rounded; fi.type = Image.Type.Sliced; fi.color = UITheme.Accent;
            var handleArea = Rect("Handle Slide Area", root);
            Place(handleArea, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            var handle = Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(18, 0);
            handle.anchorMin = new Vector2(0, 0); handle.anchorMax = new Vector2(0, 1);
            var hi = handle.gameObject.AddComponent<Image>(); hi.sprite = Circle; hi.color = Color.white;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = hi;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max;
            slider.value = value;
            if (onChange != null) slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        public static RawImage Icon(Transform parent, Texture2D tex, Color color)
        {
            var rt = Rect("Icon", parent);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding, bool expandHeight = false)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = expandHeight;
            return v;
        }

        public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing, RectOffset padding = null)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset(0, 0, 0, 0);
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            return h;
        }

        public static LayoutElement Size(Component c, float preferredHeight = -1, float preferredWidth = -1, float flexibleWidth = -1, float flexibleHeight = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
            if (preferredWidth >= 0) { le.preferredWidth = preferredWidth; le.minWidth = preferredWidth; }
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            return le;
        }

        /// <summary>Section header with an accent bar.</summary>
        public static Text Header(Transform parent, string text)
        {
            var row = Rect("Header " + text, parent);
            Size(row, 22);
            var bar = Rect("Bar", row);
            Place(bar, new Vector2(0, 0.15f), new Vector2(0, 0.85f), Vector2.zero, new Vector2(3, 0));
            var bi = bar.gameObject.AddComponent<Image>(); bi.color = UITheme.Accent;
            var t = Label(row, text.ToUpperInvariant(), 13, UITheme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 0), Vector2.zero);
            return t;
        }
    }
}
