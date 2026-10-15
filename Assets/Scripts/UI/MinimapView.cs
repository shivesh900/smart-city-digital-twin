using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartCity
{
    /// <summary>Top-down orthographic city overview rendered into a texture. Click it to move the main camera.</summary>
    public class MinimapView : MonoBehaviour, IPointerClickHandler
    {
        private SimulationController _sim;
        private Camera _cam;
        private RenderTexture _rt;
        private RawImage _image;
        private RectTransform _marker;
        private float _extent;

        public void Build(SimulationController sim, RawImage image, RectTransform marker)
        {
            _sim = sim;
            _image = image;
            _marker = marker;
            var b = sim.roadNetwork.CityBounds;
            _extent = Mathf.Max(b.extents.x, b.extents.z) + 12f;
            _rt = new RenderTexture(512, 512, 16) { name = "Minimap RT", antiAliasing = 2 };
            var go = new GameObject("Minimap Camera");
            go.transform.SetParent(transform, false);
            _cam = go.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = _extent;
            _cam.transform.position = new Vector3(0, 400f, 0);
            _cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _cam.nearClipPlane = 1f; _cam.farClipPlane = 1000f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
            _cam.targetTexture = _rt;
            _cam.depth = -10;
            RenderPipelineHelper.ConfigureMinimapCamera(_cam);
            _image.texture = _rt;
        }

        private void LateUpdate()
        {
            if (_sim == null || _sim.cameraController == null || _marker == null) return;
            Vector3 p = _sim.cameraController.Pivot;
            var r = _image.rectTransform.rect;
            _marker.anchoredPosition = new Vector2(p.x / _extent * r.width * 0.5f, p.z / _extent * r.height * 0.5f);
            _marker.localRotation = Quaternion.Euler(0, 0, -_sim.cameraController.Yaw);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_image.rectTransform, eventData.position, eventData.pressEventCamera, out local)) return;
            var r = _image.rectTransform.rect;
            float u = (local.x - r.xMin) / r.width * 2f - 1f;
            float v = (local.y - r.yMin) / r.height * 2f - 1f;
            _sim.cameraController.FocusOn(new Vector3(u * _extent, 0f, v * _extent));
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
