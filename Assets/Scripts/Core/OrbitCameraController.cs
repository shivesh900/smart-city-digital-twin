using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Orbit / zoom / pan camera for the city.
    ///  Right-drag: orbit   Left-drag or middle-drag: pan   Wheel: zoom   WASD / arrows: pan   Q / E: rotate
    /// On the title screen it slowly circles the city.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitCameraController : MonoBehaviour
    {
        public float defaultYaw = 32f;
        public float defaultPitch = 50f;
        public float defaultDistance = 290f;
        public float minDistance = 25f;
        public float maxDistance = 600f;
        public float minPitch = 10f;
        public float maxPitch = 88f;
        public float orbitSpeed = 0.25f;
        public float keyPanSpeed = 120f;

        private SimulationController _sim;
        private Vector3 _pivot, _pivotTarget, _defaultPivot;
        private float _yaw, _pitch, _distance, _yawTarget, _pitchTarget, _distanceTarget;
        private Vector3 _lastMouse;
        private bool _dragging;
        private int _dragButton = -1;
        private Bounds _bounds = new Bounds(Vector3.zero, new Vector3(400, 10, 400));
        public float DragDistance { get; private set; }

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            _bounds = sim.roadNetwork.CityBounds;
            float extent = Mathf.Max(_bounds.extents.x, _bounds.extents.z);
            defaultDistance = extent * 2.3f;
            maxDistance = extent * 5f;
            // Shift the pivot slightly toward the camera so the city sits above the bottom panel.
            _defaultPivot = new Vector3(0f, 0f, 0f) + Quaternion.Euler(0, defaultYaw, 0) * new Vector3(0, 0, -extent * 0.12f);
            var cam = GetComponent<Camera>();
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, extent * 12f);
            cam.nearClipPlane = 0.5f;
            ResetView(true);
        }

        public void ResetView(bool instant)
        {
            _pivotTarget = _defaultPivot;
            _yawTarget = defaultYaw; _pitchTarget = defaultPitch; _distanceTarget = defaultDistance;
            if (instant) Snap();
        }

        private void Snap()
        {
            _pivot = _pivotTarget; _yaw = _yawTarget; _pitch = _pitchTarget; _distance = _distanceTarget;
            Apply();
        }

        public void FocusOn(Vector3 p, float distance = -1f)
        {
            _pivotTarget = new Vector3(p.x, 0f, p.z);
            if (distance > 0f) _distanceTarget = distance;
        }

        private void LateUpdate()
        {
            if (_sim == null) { Apply(); return; }
            if (_sim.State == SimulationState.Title)
            {
                _yawTarget += Time.unscaledDeltaTime * 5f;
                _pitchTarget = 34f;
                _distanceTarget = defaultDistance * 0.95f;
                _pivotTarget = Vector3.zero;
            }
            else HandleInput();

            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
            _pivot = Vector3.Lerp(_pivot, _pivotTarget, k);
            _yaw = Mathf.LerpAngle(_yaw, _yawTarget, k);
            _pitch = Mathf.Lerp(_pitch, _pitchTarget, k);
            _distance = Mathf.Lerp(_distance, _distanceTarget, k);
            Apply();
        }

        private void HandleInput()
        {
            if (!InputBridge.Available) return;
            Vector3 mouse = InputBridge.MousePosition;
            bool overUI = InputBridge.PointerOverUI();
            for (int b = 0; b < 3; b++)
            {
                if (InputBridge.MouseDown(b) && !overUI)
                {
                    _dragging = true; _dragButton = b; _lastMouse = mouse; DragDistance = 0f;
                }
            }
            if (_dragging && !InputBridge.MouseHeld(_dragButton)) _dragging = false;
            if (_dragging)
            {
                Vector3 delta = mouse - _lastMouse;
                _lastMouse = mouse;
                DragDistance += delta.magnitude;
                if (DragDistance > 4f)
                {
                    if (_dragButton == 1)
                    {
                        _yawTarget += delta.x * orbitSpeed;
                        _pitchTarget = Mathf.Clamp(_pitchTarget - delta.y * orbitSpeed, minPitch, maxPitch);
                    }
                    else
                    {
                        float scale = _distance * 0.0016f;
                        Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
                        Vector3 fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
                        _pivotTarget -= (right * delta.x + fwd * delta.y) * scale;
                    }
                }
            }
            if (!overUI)
            {
                float scroll = InputBridge.Scroll;
                if (Mathf.Abs(scroll) > 0.01f) _distanceTarget = Mathf.Clamp(_distanceTarget * (1f - scroll * 0.1f), minDistance, maxDistance);
            }
            Vector3 move = Vector3.zero;
            if (InputBridge.Key(KeyCode.W) || InputBridge.Key(KeyCode.UpArrow)) move += Vector3.forward;
            if (InputBridge.Key(KeyCode.S) || InputBridge.Key(KeyCode.DownArrow)) move += Vector3.back;
            if (InputBridge.Key(KeyCode.A) || InputBridge.Key(KeyCode.LeftArrow)) move += Vector3.left;
            if (InputBridge.Key(KeyCode.D) || InputBridge.Key(KeyCode.RightArrow)) move += Vector3.right;
            if (move != Vector3.zero)
                _pivotTarget += Quaternion.Euler(0, _yaw, 0) * move.normalized * keyPanSpeed * (_distance / defaultDistance) * Time.unscaledDeltaTime;
            if (InputBridge.Key(KeyCode.Q)) _yawTarget -= 60f * Time.unscaledDeltaTime;
            if (InputBridge.Key(KeyCode.E)) _yawTarget += 60f * Time.unscaledDeltaTime;
            if (InputBridge.Key(KeyCode.PageUp)) _distanceTarget = Mathf.Max(minDistance, _distanceTarget - 150f * Time.unscaledDeltaTime);
            if (InputBridge.Key(KeyCode.PageDown)) _distanceTarget = Mathf.Min(maxDistance, _distanceTarget + 150f * Time.unscaledDeltaTime);

            Vector3 e = _bounds.extents + new Vector3(40, 0, 40);
            _pivotTarget.x = Mathf.Clamp(_pivotTarget.x, -e.x, e.x);
            _pivotTarget.z = Mathf.Clamp(_pivotTarget.z, -e.z, e.z);
        }

        private void Apply()
        {
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _pivot + rot * new Vector3(0f, 0f, -_distance);
            transform.rotation = rot;
        }

        public bool IsDragging => _dragging && DragDistance > 4f;
        public Vector3 Pivot => _pivot;
        public float Yaw => _yaw;
    }
}
