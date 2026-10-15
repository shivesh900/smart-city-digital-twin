using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    public enum SimulationState { Title, Ready, Running, Paused }

    /// <summary>
    /// The heart of the digital twin. Owns the fixed-step simulation clock and drives every subsystem
    /// in a deterministic order, so pausing, speeding up and resetting affect everything consistently.
    /// Missing subsystems are created automatically, so the scene never ends up with broken references.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SimulationController : MonoBehaviour
    {
        public static SimulationController Instance { get; private set; }

        [Header("Configuration")]
        public SimulationSettings settings;
        public CityPalette palette;
        [Tooltip("Show the opening title screen first")]
        public bool showTitleScreen = true;

        [Header("Subsystems (wired by Smart City > Build Scene, auto-created if missing)")]
        public RoadNetworkManager roadNetwork;
        public CityGenerator cityGenerator;
        public TrafficSimulationManager traffic;
        public IncidentManager incidents;
        public EmergencyDispatchManager dispatch;
        public PollutionManager pollution;
        public EnergyManager energy;
        public AnalyticsManager analytics;
        public LayerVisualizer layers;
        public SelectionManager selection;
        public DayNightCycle dayNight;
        public RouteAdvisor routeAdvisor;
        public DashboardController dashboard;
        public OrbitCameraController cameraController;

        public SimulationState State { get; private set; } = SimulationState.Title;
        public float SimTime { get; private set; }
        public float SimSpeed { get; private set; } = 1f;
        public bool OptimizationEnabled { get; private set; }
        public System.Random Random { get; private set; }
        public int ResetCount { get; private set; }
        public bool Initialized { get; private set; }

        /// <summary>Simulated hour of day (0-24).</summary>
        public float HourOfDay
        {
            get
            {
                if (settings == null) return 12f;
                if (!settings.timeOfDayEnabled) return settings.startHour;
                float hoursPerSecond = 24f / (settings.dayLengthMinutes * 60f);
                return Mathf.Repeat(settings.startHour + SimTime * hoursPerSecond, 24f);
            }
        }

        private float _accumulator;
        private const int MaxStepsPerFrame = 400;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SmartCity] A second SimulationController was found and disabled.");
                enabled = false;
                return;
            }
            Instance = this;
            if (settings == null) settings = Resources.Load<SimulationSettings>("SimulationSettings");
            if (settings == null) settings = SimulationSettings.CreateDefault();
            if (palette == null) palette = Resources.Load<CityPalette>("CityPalette");
            MaterialLibrary.Initialize(palette);
            SimSpeed = settings.defaultSimSpeed;
            EnsureSubsystems();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            Random = new System.Random(settings.seed);
            roadNetwork.Build(this);
            cityGenerator.Generate(this);
            dayNight.Initialize(this);
            traffic.Initialize(this);
            pollution.Initialize(this);
            energy.Initialize(this);
            incidents.Initialize(this);
            dispatch.Initialize(this);
            analytics.Initialize(this);
            layers.Initialize(this);
            selection.Initialize(this);
            routeAdvisor.Initialize(this);
            if (cameraController != null) cameraController.Initialize(this);
            RenderPipelineHelper.EnablePostProcessing(cameraController != null ? cameraController.GetComponent<Camera>() : Camera.main);
            if (settings.useExternalService)
            {
                var client = GetComponent<ExternalAIServiceClient>();
                if (client == null) client = gameObject.AddComponent<ExternalAIServiceClient>();
                client.Initialize(this);
            }
            dashboard.Initialize(this);
            Initialized = true;

            traffic.SpawnInitialVehicles();
            if (showTitleScreen)
            {
                SetState(SimulationState.Title);   // attract mode: city runs behind the title
            }
            else
            {
                EnterSimulation();
            }
        }

        private void Update()
        {
            if (!Initialized) return;
            if (State == SimulationState.Running || State == SimulationState.Title)
            {
                float speed = State == SimulationState.Title ? 1f : SimSpeed;
                _accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f) * speed;
                float dt = settings.simulationStep;
                int steps = 0;
                while (_accumulator >= dt && steps < MaxStepsPerFrame)
                {
                    Tick(dt);
                    _accumulator -= dt;
                    steps++;
                }
                if (steps >= MaxStepsPerFrame) _accumulator = 0f; // machine too slow for this speed: drop time rather than spiral
            }
        }

        private void LateUpdate()
        {
            if (!Initialized) return;
            traffic.SyncVisuals();
            dispatch.SyncVisuals(Time.unscaledDeltaTime);
        }

        private void Tick(float dt)
        {
            SimTime += dt;
            roadNetwork.StepSignals(dt, OptimizationEnabled);
            traffic.Step(dt);
            dispatch.Step(dt);
            incidents.Step(dt);
            pollution.Step(dt);
            energy.Step(dt);
            dayNight.Step(dt);
            analytics.Step(dt);
        }

        // ------------------------------------------------------------------ commands (all UI buttons land here)

        public void EnterSimulation()
        {
            ResetSimulation(false);
            if (cameraController != null) cameraController.ResetView(false);
            dashboard.ShowDashboard(true);
            CityEvents.RaiseAlert("Welcome to the control room. Press START to run the simulation.", AlertLevel.Info);
        }

        public void ReturnToTitle()
        {
            ResetSimulation(false);
            SetState(SimulationState.Title);
            dashboard.ShowDashboard(false);
        }

        public void StartSimulation()
        {
            if (State == SimulationState.Running) { CityEvents.RaiseAlert("Simulation is already running.", AlertLevel.Info); return; }
            if (State == SimulationState.Paused) { ResumeSimulation(); return; }
            SetState(SimulationState.Running);
            CityEvents.RaiseAlert("Simulation started.", AlertLevel.Success);
        }

        public void PauseSimulation()
        {
            if (State != SimulationState.Running) { CityEvents.RaiseAlert("Nothing to pause - the simulation is not running.", AlertLevel.Info); return; }
            SetState(SimulationState.Paused);
            CityEvents.RaiseAlert("Simulation paused.", AlertLevel.Info);
        }

        public void ResumeSimulation()
        {
            if (State != SimulationState.Paused) { CityEvents.RaiseAlert("Simulation is not paused.", AlertLevel.Info); return; }
            SetState(SimulationState.Running);
            CityEvents.RaiseAlert("Simulation resumed.", AlertLevel.Success);
        }

        public void TogglePause()
        {
            if (State == SimulationState.Running) PauseSimulation();
            else if (State == SimulationState.Paused) ResumeSimulation();
            else if (State == SimulationState.Ready) StartSimulation();
        }

        /// <summary>Restores the initial state: same seed, same vehicles, no incidents, clocks at zero.</summary>
        public void ResetSimulation(bool announce = true)
        {
            Random = new System.Random(settings.seed);
            SimTime = 0f;
            _accumulator = 0f;
            ResetCount++;
            incidents.ResetAll();
            dispatch.ResetAll();
            roadNetwork.ResetNetwork();
            traffic.ResetTraffic();
            pollution.ResetState();
            energy.ResetState();
            dayNight.Step(0f);
            analytics.ResetState();
            CityEvents.RaiseSimulationReset();
            SetState(SimulationState.Ready);
            if (announce) CityEvents.RaiseAlert("Simulation reset to its initial state. Press START.", AlertLevel.Info);
        }

        public void SetSimulationSpeed(float speed)
        {
            SimSpeed = Mathf.Clamp(speed, 0.25f, settings.maxSimSpeed);
        }

        public void SetOptimization(bool enabled)
        {
            if (OptimizationEnabled == enabled) return;
            OptimizationEnabled = enabled;
            analytics.OnOptimizationChanged(enabled);
            CityEvents.RaiseAlert(enabled
                ? "Adaptive signal timing ON: green time now follows measured queue lengths."
                : "Adaptive signal timing OFF: back to the fixed-time baseline.", enabled ? AlertLevel.Success : AlertLevel.Info);
        }

        public void ToggleOptimization() => SetOptimization(!OptimizationEnabled);

        /// <summary>Used by the automatic A/B test to switch strategy without the before/after bookkeeping.</summary>
        internal void SetOptimizationSilently(bool enabled) => OptimizationEnabled = enabled;

        internal void SetState(SimulationState s)
        {
            State = s;
            CityEvents.RaiseStateChanged(s);
        }

        private void EnsureSubsystems()
        {
            roadNetwork = Ensure(roadNetwork, "Road Network");
            cityGenerator = Ensure(cityGenerator, "City Generator");
            traffic = Ensure(traffic, "Traffic Simulation");
            incidents = Ensure(incidents, "Incidents");
            dispatch = Ensure(dispatch, "Emergency Dispatch");
            pollution = Ensure(pollution, "Pollution");
            energy = Ensure(energy, "Energy");
            analytics = Ensure(analytics, "Analytics");
            layers = Ensure(layers, "Layer Visualizer");
            selection = Ensure(selection, "Selection");
            dayNight = Ensure(dayNight, "Day Night Cycle");
            routeAdvisor = Ensure(routeAdvisor, "Route Advisor");
            dashboard = Ensure(dashboard, "Dashboard");
            if (cameraController == null)
            {
                var cam = Camera.main;
                if (cam == null)
                {
                    var go = new GameObject("Main Camera");
                    go.tag = "MainCamera";
                    cam = go.AddComponent<Camera>();
                    go.AddComponent<AudioListener>();
                }
                cameraController = cam.GetComponent<OrbitCameraController>();
                if (cameraController == null) cameraController = cam.gameObject.AddComponent<OrbitCameraController>();
            }
        }

        private T Ensure<T>(T current, string childName) where T : Component
        {
            if (current != null) return current;
            var found = FindFirstObjectByType<T>();
            if (found != null) return found;
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            Debug.Log($"[SmartCity] {typeof(T).Name} was missing and has been created automatically.");
            return child.AddComponent<T>();
        }

        /// <summary>Convenience: random float in [min, max) from the deterministic simulation RNG.</summary>
        public float RandomRange(float min, float max) => min + (float)Random.NextDouble() * (max - min);

        public T RandomItem<T>(IList<T> list) => list[Random.Next(list.Count)];
    }
}
