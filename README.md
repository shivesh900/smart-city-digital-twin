# AI-Powered Smart City Digital Twin (Unity 6)

A real Unity 6 project: a procedurally generated 3D city with a live traffic simulation, adaptive (queue-based) traffic-signal optimization, A* routing, incident simulation, emergency dispatch, pollution and energy monitoring, and a dark control-room dashboard. Everything runs offline in Unity. No paid assets, no API keys, no server.

> **Honesty note.** Every number on screen is produced by this simulation. None of it is real sensor data, and the dashboard says so ("SIMULATED DATA"). The "AI" here is transparent decision support: a moving-average plus linear-trend forecaster, documented rule-based severity scoring, A* search and a rule-based signal optimizer. **No machine-learning model is trained or used**, and the UI labels it that way.

---

## 1. Requirements

| Item | Version |
|---|---|
| Unity Editor | **Unity 6 LTS, 6000.0.84f1** (any 6000.0.x should work; Hub may offer to upgrade the project, accept) |
| Render pipeline | Universal Render Pipeline 17.0.4 (installed automatically from `Packages/manifest.json`) |
| Other packages | uGUI 2.0.0, Input System 1.20.0, Test Framework 1.6.0 (all automatic) |
| OS | Windows 10/11 (macOS/Linux also work) |
| Hardware | Any ordinary student laptop. Integrated graphics is fine |
| Internet | Only once, so Unity can download the packages on the first open |

## 2. Install Unity (once)

1. Install **Unity Hub** from https://unity.com/download and sign in (a free Personal license is enough).
2. Hub → **Installs** → **Install Editor** → pick **Unity 6 (6000.0.x LTS)**. If 6000.0.84f1 isn't listed, open the **Archive** tab and download it from the Unity archive page, or just use the newest 6000.0.x.
3. Modules: on Windows, **Windows Build Support (Mono)** is included. Tick **Microsoft Visual Studio Community** if you want a code editor.

## 3. Open the project

1. Unzip `SmartCityDigitalTwin.zip`, or clone https://github.com/shivesh900/smart-city-digital-twin. Use a short path without special characters, e.g. `C:\Projects\SmartCityDigitalTwin`.
2. Unity Hub → **Projects** → **Add** → **Add project from disk** → select the `SmartCityDigitalTwin` folder (the one containing `Assets`, `Packages` and `ProjectSettings`).
3. Click the project to open it. The first open takes a few minutes while Unity imports the packages and builds `Library/`.
4. If a different 6000.0.x editor version asks to upgrade, click **Continue / Upgrade**.

## 4. Generate the scene (one click)

In the top menu bar choose **Smart City → Build Scene**.

This menu does the following:

- creates `Assets/Scenes/SmartCity.unity` and opens it
- creates the materials (`Assets/Materials`), textures, the skybox and `Assets/Resources/CityPalette.asset`
- creates `Assets/ScriptableObjects/SimulationSettings.asset` with the default settings
- assigns the URP asset (`Assets/Settings/PC_RPAsset.asset`) in Graphics/Quality settings
- builds the camera (with orbit controller), sun light, URP post-processing volume, EventSystem, dashboard canvas and the `SimulationController` with every manager attached and wired together
- adds the scene to **File → Build Profiles / Build Settings**

The city itself (roads, buildings, vehicles, signals) is generated procedurally when you press Play. The same seed always gives the same city.

Other menu items:

| Menu | What it does |
|---|---|
| Smart City → Open Scene | Opens the generated scene, offering to build it first if it doesn't exist |
| Smart City → Rebuild Materials | Regenerates materials (use if anything looks pink) |
| Smart City → Validate Project | Checks the scene, settings, palette, materials and build list, and lists any problems |
| Smart City → Build Windows Player (x64) | Builds `Builds/Windows/SmartCityDigitalTwin.exe` |

## 5. Run

Press **Play** (▶). The **title screen** appears with the city in the background. Click **ENTER SIMULATION** or press Enter.

To demo again later, just reopen the project and press Play. The scene and assets are saved in the project, so there's no need to rebuild.

## 6. Controls

**Mouse**
- Left-drag: pan
- Right-drag: orbit
- Wheel: zoom
- Left-click: select an intersection, road, building, incident or emergency vehicle (details appear in **Selection details**)
- Click the minimap: fly the camera there

**Keyboard**

| Key | Action |
|---|---|
| W A S D / arrows | Pan |
| Q / E | Rotate |
| PageUp / PageDown | Zoom |
| Space | Pause / resume |
| C | Reset camera |
| 1 2 3 4 | Normal / Traffic / Pollution / Energy layer |
| F | Focus on the selection |
| H | Hide / show the UI |
| Esc | Clear the selection |
| Enter | Enter simulation (title screen) |

**Dashboard buttons (all functional)**

| Panel | Buttons |
|---|---|
| Top bar | TITLE (back to title screen) |
| Simulation control | START, PAUSE, RESUME, RESET, speed slider (0.25x to 8x), +10 VEHICLES, −10 VEHICLES |
| Optimization | OPTIMIZATION ON/OFF, RUN A/B TEST |
| Visualization layer | NORMAL, TRAFFIC, POLLUTION, ENERGY |
| View | RESET CAMERA, HIDE UI |
| Incidents | ACCIDENT, BLOCK ROAD, SIGNAL FAIL, POLLUTION SPIKE, EMERGENCY, DISPATCH, RESOLVE SELECTED, and click an incident row to select it |
| Selection details | ALT ROUTE → HOSPITAL, FOCUS |

## 7. What's simulated and how

| Feature | How it works |
|---|---|
| City | `CityGenerator` builds a road grid with intersections, sidewalks, crosswalks, zoned buildings (residential, commercial, industrial, hospital), a hospital, police and fire stations, parks, trees, streetlights, road signs, parking and pedestrians, all from primitive meshes |
| Traffic | Vehicles (cars, buses, trucks, emergency units) follow lanes on the road graph. Left-hand traffic is the default (India); toggle `driveOnLeft` in settings. They keep a gap from the vehicle ahead (collision avoidance), stop at red/yellow signals, and are pooled (`VehiclePool`). Fixed-step simulation, deterministic per seed |
| Signals | `TrafficLightController`: NS/EW phases green → yellow → all-red |
| Optimization | `TrafficOptimizer` (rule-based, documented in code). **Baseline:** fixed time, equal green. **Adaptive:** green split proportional to measured queue lengths, cycle budget grows with demand, greens clamped to [min, max], plus gap-out when a queue empties |
| Before/after | Toggling optimization ON compares the window before with the window after, live. **RUN A/B TEST** runs 120 simulated seconds of fixed-time and 120 s of adaptive with the same seed and the same demand, then shows avg travel time, avg speed, avg queue, congestion and throughput side by side, with an honest verdict (it says so if adaptive is worse) |
| Routing | `AStarPathfinder` on `RoadGraph`, with distance or congestion-weighted travel-time costs and blocked-edge avoidance. `RouteAdvisor` draws the shortest route vs the least-congested route |
| Incidents | `IncidentManager`: accident, blocked road, signal failure, pollution spike, emergency request. Each one gets a 3D marker, updates the counters, changes local traffic (lane closure / slowdown / flashing signal / emissions), gets an A* detour avoiding it (or "no detour possible"), a rule-based severity and a suggested response |
| Severity | `SeverityClassifier`, explicit rules R1–R7: base score by type, vehicles nearby, local AQI, near a critical facility, peak hour, no responder route, nearby blocked roads. Score → Low / Medium / High / Critical. The reasons are shown in the UI |
| Dispatch | `EmergencyDispatchManager` picks the available unit (ambulance / fire truck / police) with the fastest A* route avoiding blocked roads, highlights the route, animates the vehicle with signal pre-emption, shows a simulated ETA and records the response time. If no route exists it says so and dispatches nothing |
| Pollution | `PollutionManager`: per-zone simulated AQI driven by traffic density, industry, green space decay and spikes. Colour heatmap layer, warning/critical thresholds (200 / 300, configurable) with alerts |
| Energy | `EnergyManager`: simulated building and streetlight load by zone and time of day (`DayNightCycle`), shown as a layer and a KPI |
| Forecast | `CongestionPredictor`: 30-sample moving average + least-squares trend, 30 s ahead. Each forecast is checked against what actually happened, and the measured error is displayed. **Not ML** |
| Decision support | `RecommendationEngine`: rules that fire on the measured state. Each recommendation shows the rule and the numbers behind it |
| Dashboard | `DashboardController` (uGUI, built in code): KPIs (vehicles, avg speed, congestion %, incidents, AQI, energy MW, avg emergency response time), procedural line charts (traffic, pollution, energy, forecast), minimap, incident list, selection details, recommendations, scenario comparison and alert toasts |

## 8. Architecture

```
Assets/
  Editor/            SmartCitySceneBuilder.cs   (menu Smart City > ...)
  Scripts/
    Core/            SimulationController (composition root, state machine), SimulationSettings (ScriptableObject),
                     OrbitCameraController, SelectionManager, InputBridge, CityEvents, MeshBuilder, palette/materials
    City/            CityGenerator, CityLayout, BuildingInfo, PedestrianSystem, WorldLabel
    Traffic/         RoadNetworkManager, RoadNetworkTypes, TrafficSimulationManager, TrafficLightController, TrafficOptimizer
    Vehicles/        VehicleController, VehicleFactory (+ pool)
    Pathfinding/     RoadGraph, AStarPathfinder, RouteAdvisor
    Incidents/       IncidentManager, Incident, IncidentMarker, SeverityClassifier
    Emergency/       EmergencyDispatchManager
    Environment/     PollutionManager, EnergyManager, LayerVisualizer, DayNightCycle
    Analytics/       AnalyticsManager, CongestionPredictor, ScenarioComparison, RecommendationEngine, TimeSeries,
                     ExternalAIServiceClient (optional, off)
    UI/              DashboardController, UIFactory, LineChartGraphic, MinimapView, IconFactory, SelectionInfoBuilder
  Settings/          URP assets (PC + Mobile), volume profiles
  Tests/EditMode/    28 NUnit tests
  (generated by Build Scene: Scenes/, Materials/, Resources/, ScriptableObjects/)
PythonService/       optional Flask forecaster (not needed)
```

`SimulationController` owns every manager and steps them in a fixed order. Managers talk through `CityEvents` (alerts, incident changes) rather than hard references, and the dashboard reads their public state. The pure algorithms (A*, optimizer, severity, forecaster, comparison, recommendations) have no scene dependency, which is why they can be unit-tested.

**Tuning:** select `Assets/ScriptableObjects/SimulationSettings.asset` to change the seed, city size, vehicle counts, speeds, signal timings, AQI thresholds, emergency fleet, A/B test length and more.

## 9. Tests

- **In Unity:** Window → General → **Test Runner** → **EditMode** → **Run All**. There are 28 tests covering A* (including a check against brute-force Dijkstra), the optimizer, severity rules, the forecaster, scenario comparison and the recommendation rules.
- **Optional Python stub:** `cd PythonService && python test_forecast.py`.

## 10. Optional Python service

`PythonService/app.py` is a tiny Flask server returning a linear-trend congestion forecast (also not ML). To try it:

1. `pip install -r requirements.txt`
2. `python app.py`
3. Tick **Use External Service** in SimulationSettings, then press Play.

The forecast panel shows the service status. With the service off, nothing changes.

## 11. Build a Windows .exe

Use **Smart City → Build Windows Player (x64)**, or File → Build Profiles → Windows → Build. The output goes to `Builds/Windows/`.

## 12. Troubleshooting

| Problem | Fix |
|---|---|
| No **Smart City** menu | The scripts haven't compiled. Open **Window → General → Console** and look for red errors. Usually a package didn't download: Window → Package Manager, then make sure Universal RP, uGUI, Input System and Test Framework are installed. Then Assets → Reimport All |
| "Enter Safe Mode?" on open | Same cause: compile errors. Click **Ignore**, check the Console, and send a screenshot of the first red error |
| Pink / magenta objects | Smart City → Rebuild Materials. Also check Edit → Project Settings → Graphics: the Default Render Pipeline should be `PC_RPAsset` (Build Scene sets this) |
| Mouse/keyboard does nothing, warning in the control panel | Edit → Project Settings → Player → Other Settings → **Active Input Handling = Both**, then let Unity restart |
| Black or empty Game view | Make sure `Assets/Scenes/SmartCity.unity` is the open scene (Smart City → Open Scene), then Play |
| Dashboard looks squashed | Set the Game view resolution to 1920×1080 (Full HD) or 16:9 |
| Low FPS | Lower `initialVehicles`/`maxVehicles` in SimulationSettings, set Quality to a lower level, or hide the UI (H) |
| Something broke after editing | Smart City → Build Scene again. It regenerates the scene and assets safely |

See **DEMO_GUIDE.md** for the faculty demo script and **KNOWN_LIMITATIONS.md** for what was and wasn't tested.
