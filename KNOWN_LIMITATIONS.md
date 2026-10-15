# Known Limitations and Manual Steps

Please read this before the demo. It separates what was actually verified from what wasn't.

## What WAS verified (on a Linux build machine, without opening the Unity editor)

1. **C# compilation.** Every script was compiled with zero errors using Unity's own C# compiler against the real Unity **6000.0.84f1** engine/editor assemblies, the UGUI package and NUnit. That covers:
   - the runtime assembly, in both editor and Windows-player define configurations
   - the Editor assembly (`SmartCitySceneBuilder`)
   - the EditMode test assembly
2. **Logic tests.** All 28 EditMode tests (A*, signal optimizer, severity rules, forecaster, scenario comparison, recommendations) **pass**. They were run with NUnit outside Unity against the same source files. They test the algorithms, not the scene.
3. **Packages.** The versions in `Packages/manifest.json` match the versions bundled with Unity 6000.0.84f1.
4. **Python stub.** The optional Python forecaster's logic test passes.

## What was NOT verified (you must do this on your PC)

1. **The Unity editor was never opened, and Play mode was never run.** No Unity license was available on the build machine. So none of the following has been seen working yet:
   - scene generation (Smart City → Build Scene)
   - the visuals: lighting, materials, layout
   - vehicle movement on screen
   - button clicks
   - UI layout at your resolution
   - frame rate

   The code compiles, but runtime errors, a squashed layout or wrong-looking materials can only show up in Play mode. **Please do a full run-through at least a day before the demo**, and send a screenshot of any red Console error so it can be fixed.
2. **No Windows .exe was built.** Building one needs the licensed editor. Use **Smart City → Build Windows Player (x64)** on your PC. It's untested.
3. **The Unity Test Runner** wasn't run inside Unity (the NUnit run above used the same test code).
4. **The Python Flask service** was not run as a server (Flask isn't installed on the build machine). Only its forecast function was tested.

## Steps you must perform manually

1. Install Unity Hub and **Unity 6 LTS (6000.0.x)**.
2. Open the folder in Hub and let the packages import. This needs internet the first time.
3. Run **Smart City → Build Scene** once.
4. Press **Play**, then go through DEMO_GUIDE.md once to check everything.
5. Optional: build the Windows .exe from the Smart City menu.

## Design limitations (by intent)

- **No machine learning.** The forecast is a moving average plus a linear trend. Severity, recommendations and signal optimization are explicit rules, all labelled as such in the UI. The optional Python service is also a linear trend, not ML.
- **All data is simulated.** There's no real city map, real traffic feed or real sensor data. AQI and energy numbers are model outputs on a plausible scale, not measurements.
- The city is a generated grid, not a real city layout (no GIS/OpenStreetMap import).
- The traffic model is a simplified microscopic simulation: lane following, gap keeping and signal obeying. There's no lane changing, overtaking or turning-conflict physics. Emergency vehicles get signal pre-emption but don't physically weave through traffic.
- Visuals are primitive meshes with URP lighting and post-processing. They're stylized, not photorealistic.
- The A/B test compares one seed per run. Results vary slightly with seed and demand, and the panel reports whatever the simulation measured, including when adaptive timing isn't better.
- The dashboard uses uGUI (Canvas) built in code, not UI Toolkit, and it's laid out for 16:9 screens.
