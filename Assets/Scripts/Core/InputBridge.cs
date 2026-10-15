using UnityEngine;
using UnityEngine.EventSystems;

namespace SmartCity
{
    /// <summary>
    /// Thin wrapper over Unity's input so the rest of the code does not depend on the input backend.
    /// The project ships with Active Input Handling = "Both", which enables UnityEngine.Input.
    /// If the legacy backend is disabled, camera/keyboard input is unavailable and a warning is shown.
    /// </summary>
    public static class InputBridge
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        public static bool Available => true;
        public static Vector3 MousePosition => Input.mousePosition;
        public static bool MouseDown(int b) => Input.GetMouseButtonDown(b);
        public static bool MouseHeld(int b) => Input.GetMouseButton(b);
        public static bool MouseUp(int b) => Input.GetMouseButtonUp(b);
        public static float Scroll => Input.mouseScrollDelta.y;
        public static bool Key(KeyCode k) => Input.GetKey(k);
        public static bool KeyDown(KeyCode k) => Input.GetKeyDown(k);
#else
        public static bool Available => false;
        public static Vector3 MousePosition => Vector3.zero;
        public static bool MouseDown(int b) => false;
        public static bool MouseHeld(int b) => false;
        public static bool MouseUp(int b) => false;
        public static float Scroll => 0f;
        public static bool Key(KeyCode k) => false;
        public static bool KeyDown(KeyCode k) => false;
#endif

        public static bool PointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
