# Faculty Demo Guide (about 10–12 minutes)

**Before the demo**
- Open the project, then Smart City → Open Scene.
- Set the Game view to **1920×1080** and **Maximize On Play**.
- Press Play once to warm up, then stop.
- Optional: press Play about 3 minutes early and let the simulation run with optimization OFF, so the live before/after has a baseline. Then go back to the title screen with the **TITLE** button.

---

### 1. Title screen (30 s)
Press **Play**. Point out the title, the city overview, and the note that all values are simulated and it runs offline.
> "This is a digital twin: a live, simulated model of a city we can monitor and run what-if scenarios on."

Click **ENTER SIMULATION**.

### 2. The city and the camera (1 min)
- Right-drag to orbit, scroll to zoom, left-drag to pan. Point out the zones: residential, the commercial centre, industrial, hospital, police and fire stations, parks.
- Click **RESET CAMERA**.
> "The whole city is generated procedurally from code. There are no paid assets, and the same seed always gives the same city."

### 3. Dashboard KPIs and charts (1 min)
Walk through the KPIs: active vehicles, average speed, congestion %, active incidents, AQI, energy (MW), emergency response time. Then the trend charts and the minimap (click it to fly).
Point at the **SIMULATED DATA** badge.
> "Nothing here pretends to be real sensor data."

### 4. Traffic simulation and an intersection (1.5 min)
- Zoom in on an intersection. Show the signals cycling green → yellow → red, cars stopping at red and queueing without collisions.
- **Click the intersection.** Selection details shows its queues, signal phase and timing.
- Click **+10 VEHICLES** two or three times and watch congestion and the charts rise. Move the **speed slider** to 4x, then back to 1x.

### 5. Optimization with before/after proof (2 min)
- Click **OPTIMIZATION: OFF** to turn it ON. Signal timing now adapts to queue lengths. The Scenario comparison panel starts a live before/after.
- For a fair comparison, click **RUN A/B TEST**. It runs the same city, seed and demand twice at 8x: fixed-time first, then adaptive. It takes about 40 s of real time. It then shows travel time, speed, queue length, congestion and throughput side by side, with the verdict.
> "The optimizer is a transparent rule: green time proportional to queue length, plus gap-out. It isn't machine learning, and if it ever does worse the panel says so."

### 6. Incident and rerouting (2 min)
- Click **ACCIDENT**. A marker appears, the incident counter goes up, and nearby traffic slows. The incident row shows its **severity**, and the Decision-support panel explains why (rules R1–R7, e.g. "vehicles nearby +2", "peak hour +1") and suggests a response.
- Click **BLOCK ROAD**. Vehicles detour, the detour is computed with A* avoiding the blocked road, and the alternative route is drawn.
- Select a road or intersection and click **ALT ROUTE → HOSPITAL**. It shows the shortest route vs the least-congested route.

### 7. Emergency dispatch (1.5 min)
- Click **EMERGENCY**, or select the accident row, then click **DISPATCH**.
- The nearest available ambulance or fire truck is chosen by A* travel time. Its route is highlighted, it drives there with signal pre-emption, and the ETA counts down. On arrival the **response time** is recorded in the KPI.
- Optional: block roads around an incident and dispatch again. If no route exists, the system says so and doesn't invent one.

### 8. Environment layers (1.5 min)
- Click **POLLUTION SPIKE**. The AQI rises and an alert fires when it crosses the threshold (200 warning / 300 critical).
- Switch layers: **TRAFFIC** (road congestion colours), **POLLUTION** (zone heatmap, with green zones visible), **ENERGY** (building and streetlight load). Then back to **NORMAL**.
> "Pollution is driven by the simulated traffic density, and green zones lower it."

### 9. Resolve and reset (1 min)
- Select each incident and click **RESOLVE SELECTED**. Traffic returns to normal.
- **PAUSE**, then **RESUME**, then **RESET**: the city restarts cleanly and can run again.

### 10. Code and tests (optional, 1 min)
- Show the folder structure (`Assets/Scripts/...`), then Window → General → Test Runner → EditMode → **Run All** (28 tests).
- Mention the **Smart City → Build Scene** utility, which regenerates the whole scene in one click.

---

**If a faculty member asks "Is this AI?"** Answer honestly: it's AI-inspired decision support. A* search, a rule-based adaptive signal controller, a transparent statistical forecaster with its measured error shown, and explicit severity rules. A trained ML model could be plugged in through the optional Python service, but none is used now.
