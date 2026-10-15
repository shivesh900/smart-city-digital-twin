using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SmartCity.EditorTools
{
    /// <summary>
    /// One-click project setup: menu "Smart City > Build Scene".
    /// Creates materials, textures, the settings asset and Assets/Scenes/SmartCity.unity with the camera,
    /// lighting, post-processing, dashboard canvas, event system and all managers wired together.
    /// The city itself is generated procedurally when you press Play (same seed = same city).
    /// </summary>
    public static class SmartCitySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/SmartCity.unity";
        public const string SettingsPath = "Assets/ScriptableObjects/SimulationSettings.asset";
        public const string PalettePath = "Assets/Resources/CityPalette.asset";
        public const string MaterialsFolder = "Assets/Materials";
        public const string TexturesFolder = "Assets/Materials/Textures";
        public const string SkyboxPath = "Assets/Materials/SmartCitySkybox.mat";
        public const string VolumeProfilePath = "Assets/Settings/SmartCityVolumeProfile.asset";
        public const string UrpAssetPath = "Assets/Settings/PC_RPAsset.asset";

        [MenuItem("Smart City/Build Scene", false, 0)]
        public static void BuildSceneMenu()
        {
            if (Application.isPlaying) { EditorUtility.DisplayDialog("Smart City", "Stop Play mode first.", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll(true);
        }

        /// <summary>Entry point for command-line use: Unity -batchmode -executeMethod SmartCity.EditorTools.SmartCitySceneBuilder.BuildSceneBatch -quit</summary>
        public static void BuildSceneBatch() => BuildAll(false);

        /// <summary>Command-line Windows build: -executeMethod SmartCity.EditorTools.SmartCitySceneBuilder.BuildWindows</summary>
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) BuildAll(false);
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Windows/SmartCityDigitalTwin.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log("[SmartCity] Windows build result: " + report.summary.result + ", size " + report.summary.totalSize + " bytes");
        }

        public static void BuildAll(bool interactive)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Smart City", "Preparing folders", 0.05f);
                EnsureFolders();
                EnsureRenderPipeline();
                EditorUtility.DisplayProgressBar("Smart City", "Settings asset", 0.15f);
                var settings = EnsureSettings();
                EditorUtility.DisplayProgressBar("Smart City", "Materials and textures", 0.3f);
                var palette = BuildPalette();
                var skybox = EnsureSkybox();
                EditorUtility.DisplayProgressBar("Smart City", "Scene objects", 0.6f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateSceneObjects(settings, palette, skybox);
                EditorUtility.DisplayProgressBar("Smart City", "Saving scene", 0.85f);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            var problems = Validate(false);
            string summary = problems.Count == 0
                ? "SmartCity scene created and validated.\n\nPress Play, then ENTER SIMULATION and START."
                : "SmartCity scene created, but validation found:\n- " + string.Join("\n- ", problems);
            Debug.Log("[SmartCity] " + summary);
            if (interactive) EditorUtility.DisplayDialog("Smart City Digital Twin", summary, "OK");
        }

        // ------------------------------------------------------------------ assets

        private static void EnsureFolders()
        {
            foreach (var f in new[] { "Assets/Scenes", "Assets/Materials", TexturesFolder, "Assets/ScriptableObjects", "Assets/Resources", "Assets/Prefabs", "Assets/UI", "Assets/Settings" })
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                string parent = Path.GetDirectoryName(f).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(f));
            }
        }

        private static void EnsureRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            var urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (urp != null)
            {
                GraphicsSettings.defaultRenderPipeline = urp;
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    if (QualitySettings.renderPipeline == null) QualitySettings.renderPipeline = urp;
                }
                Debug.Log("[SmartCity] Assigned the URP asset " + UrpAssetPath);
            }
            else
            {
                Debug.Log("[SmartCity] No URP asset found - using the built-in render pipeline (Standard shader).");
            }
        }

        private static SimulationSettings EnsureSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<SimulationSettings>(SettingsPath);
            if (s != null) return s;   // never overwrite the user's tuning
            s = ScriptableObject.CreateInstance<SimulationSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            return s;
        }

        public static CityPalette BuildPalette()
        {
            var textures = new Dictionary<string, Texture2D>();
            Func<string, Texture2D> provider = key =>
            {
                Texture2D t;
                if (textures.TryGetValue(key, out t)) return t;
                string path = $"{TexturesFolder}/{key}.asset";
                t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t == null)
                {
                    t = TextureFactory.Create(key);
                    if (t == null) return null;
                    AssetDatabase.CreateAsset(t, path);
                }
                textures[key] = t;
                return t;
            };

            var palette = AssetDatabase.LoadAssetAtPath<CityPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<CityPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }
            foreach (var def in MaterialDefinitions.All)
            {
                var fresh = MaterialDefinitions.Create(def, provider);
                string path = $"{MaterialsFolder}/{def.Key}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                Material mat;
                if (existing != null)
                {
                    existing.shader = fresh.shader;
                    existing.CopyPropertiesFromMaterial(fresh);
                    existing.shaderKeywords = fresh.shaderKeywords;
                    existing.globalIlluminationFlags = fresh.globalIlluminationFlags;
                    existing.enableInstancing = fresh.enableInstancing;
                    existing.renderQueue = fresh.renderQueue;
                    EditorUtility.SetDirty(existing);
                    UnityEngine.Object.DestroyImmediate(fresh);
                    mat = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(fresh, path);
                    mat = fresh;
                }
                palette.Set(def.Key, mat);
            }
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            return palette;
        }

        private static Material EnsureSkybox()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (sky != null) return sky;
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return null;
            sky = new Material(shader) { name = "SmartCitySkybox" };
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.32f, 0.38f, 0.55f));
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.05f, 0.06f, 0.09f));
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.75f);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 0.85f);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.03f);
            AssetDatabase.CreateAsset(sky, SkyboxPath);
            return sky;
        }

        // ------------------------------------------------------------------ scene

        private static void CreateSceneObjects(SimulationSettings settings, CityPalette palette, Material skybox)
        {
            // Lighting
            var sunGo = new GameObject("Sun (Directional Light)");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunGo.transform.rotation = Quaternion.Euler(42f, 140f, 0f);
            RenderSettings.sun = sun;
            if (skybox != null) RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.002f;

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 4000f;
            cam.clearFlags = skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.05f, 0.09f);
            camGo.AddComponent<AudioListener>();
            var orbit = camGo.AddComponent<OrbitCameraController>();
            camGo.transform.position = new Vector3(-150f, 220f, -220f);
            camGo.transform.LookAt(Vector3.zero);
            RenderPipelineHelper.EnablePostProcessing(cam);

            CreateGlobalVolume();

            // Simulation root and subsystems
            var root = new GameObject("Smart City");
            var sim = root.AddComponent<SimulationController>();
            sim.settings = settings;
            sim.palette = palette;
            sim.showTitleScreen = true;
            sim.roadNetwork = Child<RoadNetworkManager>(root, "Road Network");
            sim.cityGenerator = Child<CityGenerator>(root, "City Generator");
            sim.traffic = Child<TrafficSimulationManager>(root, "Traffic Simulation");
            sim.incidents = Child<IncidentManager>(root, "Incidents");
            sim.dispatch = Child<EmergencyDispatchManager>(root, "Emergency Dispatch");
            sim.pollution = Child<PollutionManager>(root, "Pollution");
            sim.energy = Child<EnergyManager>(root, "Energy");
            sim.analytics = Child<AnalyticsManager>(root, "Analytics");
            sim.layers = Child<LayerVisualizer>(root, "Layer Visualizer");
            sim.selection = Child<SelectionManager>(root, "Selection");
            sim.dayNight = Child<DayNightCycle>(root, "Day Night Cycle");
            sim.dayNight.sun = sun;
            sim.routeAdvisor = Child<RouteAdvisor>(root, "Route Advisor");
            sim.cameraController = orbit;

            // Dashboard canvas
            var canvasGo = new GameObject("Dashboard Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            sim.dashboard = canvasGo.AddComponent<DashboardController>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            EditorUtility.SetDirty(sim);
        }

        private static T Child<T>(GameObject root, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<T>();
        }

        /// <summary>Adds a URP global Volume (bloom, tonemapping, vignette) through reflection; skipped without URP.</summary>
        private static void CreateGlobalVolume()
        {
            var volumeType = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            if (volumeType == null || GraphicsSettings.currentRenderPipeline == null) return;
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(VolumeProfilePath);
            if (profile == null) return;
            var go = new GameObject("Global Volume (Bloom + Tonemapping)");
            var vol = go.AddComponent(volumeType);
            var isGlobal = volumeType.GetProperty("isGlobal");
            if (isGlobal != null) isGlobal.SetValue(vol, true);
            var shared = volumeType.GetField("sharedProfile");
            if (shared != null) shared.SetValue(vol, profile);
        }

        // ------------------------------------------------------------------ utilities

        [MenuItem("Smart City/Open Scene", false, 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { if (EditorUtility.DisplayDialog("Smart City", "The scene does not exist yet. Build it now?", "Build", "Cancel")) BuildSceneMenu(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Smart City/Rebuild Materials", false, 20)]
        public static void RebuildMaterials()
        {
            EnsureFolders();
            BuildPalette();
            EditorUtility.DisplayDialog("Smart City", "Materials regenerated for the active render pipeline.", "OK");
        }

        [MenuItem("Smart City/Build Windows Player (x64)", false, 40)]
        public static void BuildWindowsMenu()
        {
            if (!EditorUtility.DisplayDialog("Smart City", "Build a Windows 64-bit player into Builds/Windows?\n(Needs the Windows Build Support module, included with the Windows editor.) This can take several minutes.", "Build", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildWindows();
            if (File.Exists("Builds/Windows/SmartCityDigitalTwin.exe")) EditorUtility.RevealInFinder("Builds/Windows/SmartCityDigitalTwin.exe");
        }

        [MenuItem("Smart City/Validate Project", false, 21)]
        public static void ValidateMenu()
        {
            var problems = Validate(true);
            EditorUtility.DisplayDialog("Smart City validation", problems.Count == 0 ? "Everything looks good." : "Problems found:\n- " + string.Join("\n- ", problems), "OK");
        }

        public static List<string> Validate(bool log)
        {
            var problems = new List<string>();
            if (!File.Exists(ScenePath)) problems.Add("Scene missing: run Smart City > Build Scene.");
            if (AssetDatabase.LoadAssetAtPath<SimulationSettings>(SettingsPath) == null) problems.Add("SimulationSettings asset missing.");
            var palette = AssetDatabase.LoadAssetAtPath<CityPalette>(PalettePath);
            if (palette == null) problems.Add("CityPalette asset missing.");
            else
            {
                foreach (var def in MaterialDefinitions.All)
                {
                    var m = palette.Find(def.Key);
                    if (m == null) { problems.Add("Material missing: " + def.Key); continue; }
                    if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported)
                        problems.Add($"Material '{def.Key}' has an unsupported shader (pink). Run Smart City > Rebuild Materials.");
                }
            }
            bool sceneOpen = SceneManager.GetActiveScene().path == ScenePath;
            if (sceneOpen)
            {
                int missing = 0;
                foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0) problems.Add($"{missing} missing script reference(s) in the scene.");
                var sim = UnityEngine.Object.FindFirstObjectByType<SimulationController>();
                if (sim == null) problems.Add("SimulationController not found in the scene.");
                else
                {
                    if (sim.settings == null) problems.Add("SimulationController.settings is not assigned.");
                    if (sim.palette == null) problems.Add("SimulationController.palette is not assigned.");
                    if (sim.roadNetwork == null || sim.traffic == null || sim.dashboard == null || sim.incidents == null || sim.dispatch == null)
                        problems.Add("A subsystem reference is empty (it will be auto-created at runtime, but rebuilding the scene is recommended).");
                }
                if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null) problems.Add("No EventSystem: UI buttons would not respond.");
                if (Camera.main == null) problems.Add("No camera tagged MainCamera.");
            }
            bool inBuild = false;
            foreach (var s in EditorBuildSettings.scenes) if (s.path == ScenePath && s.enabled) inBuild = true;
            if (!inBuild) problems.Add("SmartCity.unity is not in Build Settings.");
            int input = ActiveInputHandling();
            if (input == 1) problems.Add("Active Input Handling is 'Input System Package (New)'. Set it to 'Both' (Project Settings > Player) so camera controls work.");
            if (log)
            {
                var sb = new StringBuilder("[SmartCity] Validation: ");
                sb.Append(problems.Count == 0 ? "OK" : string.Join(" | ", problems));
                Debug.Log(sb.ToString());
            }
            return problems;
        }

        /// <summary>0 = old Input Manager, 1 = new Input System, 2 = both.</summary>
        private static int ActiveInputHandling()
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0) return -1;
                var so = new SerializedObject(assets[0]);
                var prop = so.FindProperty("activeInputHandler");
                return prop != null ? prop.intValue : -1;
            }
            catch { return -1; }
        }
    }

    /// <summary>Offers to generate the scene the first time the project is opened.</summary>
    [InitializeOnLoad]
    public static class SmartCityFirstRun
    {
        private const string SessionKey = "SmartCity.FirstRunChecked";

        static SmartCityFirstRun()
        {
            EditorApplication.delayCall += Check;
        }

        private static void Check()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Check; return; }
            SessionState.SetBool(SessionKey, true);
            if (File.Exists(SmartCitySceneBuilder.ScenePath)) return;
            if (EditorUtility.DisplayDialog("Smart City Digital Twin",
                    "Welcome! The SmartCity scene has not been generated yet.\n\nGenerate it now? (Same as menu Smart City > Build Scene)",
                    "Generate scene", "Later"))
            {
                SmartCitySceneBuilder.BuildAll(true);
            }
        }
    }
}
