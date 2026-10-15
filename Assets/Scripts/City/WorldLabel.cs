using UnityEngine;

namespace SmartCity
{
    /// <summary>Floating 3D text that always faces the camera (facility names, layer values).</summary>
    public class WorldLabel : MonoBehaviour
    {
        public TextMesh Text { get; private set; }
        private Transform _cam;

        public bool Billboard = true;

        public static WorldLabel Create(Transform parent, Vector3 position, string text, Color color, float size = 1f, bool billboard = true)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var tm = go.AddComponent<TextMesh>();
            var font = UIFonts.Default;
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.12f * size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var label = go.AddComponent<WorldLabel>();
            label.Text = tm;
            label.Billboard = billboard;
            return label;
        }

        private void LateUpdate()
        {
            if (!Billboard) return;
            if (_cam == null)
            {
                var c = Camera.main;
                if (c == null) return;
                _cam = c.transform;
            }
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position, Vector3.up);
        }
    }

    /// <summary>Built-in font lookup that works across Unity versions (no font asset files needed).</summary>
    public static class UIFonts
    {
        private static Font _default;

        public static Font Default
        {
            get
            {
                if (_default != null) return _default;
                try { _default = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { _default = null; }
                if (_default == null) { try { _default = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { _default = null; } }
                if (_default == null) _default = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Liberation Sans", "DejaVu Sans" }, 16);
                return _default;
            }
        }
    }
}
