using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartCity
{
    /// <summary>
    /// URP-specific camera settings applied through reflection, so the scripts compile and run with or
    /// without the Universal Render Pipeline package (built-in pipeline fallback).
    /// </summary>
    public static class RenderPipelineHelper
    {
        private const string CameraDataType = "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime";

        public static bool UsingURP
        {
            get
            {
                var rp = GraphicsSettings.currentRenderPipeline;
                return rp != null && rp.GetType().Name.Contains("Universal");
            }
        }

        /// <summary>Turns on post-processing (bloom, tonemapping) for the main camera when URP is active.</summary>
        public static void EnablePostProcessing(Camera cam, bool antialias = true)
        {
            var data = GetCameraData(cam);
            if (data == null) return;
            SetProperty(data, "renderPostProcessing", true);
            if (antialias)
            {
                var aaProp = data.GetType().GetProperty("antialiasing");
                if (aaProp != null)
                {
                    try { aaProp.SetValue(data, Enum.ToObject(aaProp.PropertyType, 1)); } catch { } // FXAA
                }
            }
        }

        public static void ConfigureMinimapCamera(Camera cam)
        {
            var data = GetCameraData(cam);
            if (data == null) return;
            SetProperty(data, "renderShadows", false);
            SetProperty(data, "renderPostProcessing", false);
        }

        private static Component GetCameraData(Camera cam)
        {
            if (cam == null || !UsingURP) return null;
            var type = Type.GetType(CameraDataType);
            if (type == null) return null;
            var c = cam.GetComponent(type);
            if (c == null) c = cam.gameObject.AddComponent(type);
            return c;
        }

        private static void SetProperty(object target, string name, object value)
        {
            var p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { try { p.SetValue(target, value); } catch { } }
        }
    }
}
