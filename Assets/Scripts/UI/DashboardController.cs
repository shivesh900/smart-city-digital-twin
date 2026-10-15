using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartCity
{
    /// <summary>
    /// Builds and updates the in-game control-room dashboard (uGUI, created in code - no prefabs to break).
    /// Every button calls a real command on the simulation. All figures are labelled as simulated.
    /// </summary>
    public class DashboardController : MonoBehaviour
    {
        private SimulationController _sim;
        private Canvas _canvas;
        private CanvasGroup _dashGroup, _titleGroup;
        private float _refreshTimer, _chartTimer, _fpsTimer;
        private int _frames;
        private float _fps;
        private bool _uiHidden;

        // top bar
        private Text _stateText, _clockText, _fpsText;
        private Image _statePill;
        // KPIs
        private readonly Dictionary<string, Text> _kpiValue = new Dictionary<string, Text>();
        private readonly Dictionary<string, Text> _kpiSub = new Dictionary<string, Text>();
        // controls
        private Slider _speedSlider;
        private Text _speedLabel;
        private Button _optButton, _abButton;
        private readonly Dictionary<LayerMode, Button> _layerButtons = new Dictionary<LayerMode, Button>();
        // charts
        private LineChartGraphic _trafficChart, _aqiChart, _energyChart;
        private Text _trafficValue, _aqiValue, _energyValue, _forecastText;
        // comparison
        private Text _cmpTitle, _cmpVerdict;
        private readonly Text[] _cmpCols = new Text[4];
        private Image _abBar;
        // incidents / details / recommendations
        private readonly List<Button> _incidentRows = new List<Button>();
        private readonly List<int> _incidentRowIds = new List<int>();
        private Text _incidentHeader, _detailsText, _recoText;
        // alerts
        private readonly List<Text> _alertLines = new List<Text>();
        private readonly List<string> _alerts = new List<string>();
        private Text _inputWarning;

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            EnsureCanvas();
            BuildDashboard();
            BuildTitle();
            CityEvents.Alert += OnAlert;
            CityEvents.IncidentsChanged += RefreshIncidentList;
            sim.selection.Changed += () => RefreshDetails();
            ShowDashboard(false);
        }

        private void OnDestroy()
        {
            CityEvents.Alert -= OnAlert;
            CityEvents.IncidentsChanged -= RefreshIncidentList;
        }

        private void EnsureCanvas()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        public void ShowDashboard(bool show)
        {
            SetGroup(_dashGroup, show && !_uiHidden);
            SetGroup(_titleGroup, !show);
        }

        private static void SetGroup(CanvasGroup g, bool visible)
        {
            if (g == null) return;
            g.alpha = visible ? 1f : 0f;
            g.interactable = visible;
            g.blocksRaycasts = visible;
        }

        // ================================================================== layout

        private void BuildDashboard()
        {
            var root = UIFactory.Rect("Dashboard", transform);
            UIFactory.Stretch(root);
            _dashGroup = root.gameObject.AddComponent<CanvasGroup>();

            BuildTopBar(root);
            BuildLeftPanel(root);
            BuildRightPanel(root);
            BuildBottomPanel(root);
            BuildAlerts(root);
        }

        private void BuildTopBar(RectTransform root)
        {
            var bar = UIFactory.Panel(root, "Top Bar", UITheme.Panel).rectTransform;
            UIFactory.Place(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -66), new Vector2(-14, -10));
            var icon = UIFactory.Icon(bar, IconFactory.Get("signal"), UITheme.Accent);
            UIFactory.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, -18), new Vector2(50, 18));
            var title = UIFactory.Label(bar, "<b>SMART CITY DIGITAL TWIN</b>", 24, UITheme.Text);
            UIFactory.Place(title.rectTransform, new Vector2(0, 0.42f), new Vector2(0, 1), new Vector2(60, 0), new Vector2(520, 0));
            var sub = UIFactory.Label(bar, "Traffic - Incidents - Environment - Energy control room", 13, UITheme.TextDim);
            UIFactory.Place(sub.rectTransform, new Vector2(0, 0), new Vector2(0, 0.45f), new Vector2(60, 2), new Vector2(520, 0));

            _statePill = UIFactory.Panel(bar, "State", UITheme.Button, false);
            UIFactory.Place(_statePill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250, -16), new Vector2(-110, 16));
            _stateText = UIFactory.Label(_statePill.transform, "READY", 15, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_stateText.rectTransform);
            _clockText = UIFactory.Label(bar, "", 16, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Place(_clockText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(-96, 0), new Vector2(240, 0));

            var badge = UIFactory.Panel(bar, "Simulated Badge", new Color(0.35f, 0.2f, 0.0f, 0.9f), false);
            UIFactory.Place(badge.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-520, -15), new Vector2(-200, 15));
            var bt = UIFactory.Label(badge.transform, "SIMULATED DATA - not real sensor readings", 12, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(bt.rectTransform);
            _fpsText = UIFactory.Label(bar, "", 12, UITheme.TextDim, TextAnchor.MiddleRight);
            UIFactory.Place(_fpsText.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-196, 0), new Vector2(-112, 0));
            var titleBtn = UIFactory.Button(bar, "TITLE", () => _sim.ReturnToTitle(), null, 13);
            UIFactory.Place((RectTransform)titleBtn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-100, -16), new Vector2(-12, 16));
        }

        private RectTransform Column(RectTransform panel, float spacing = 8f)
        {
            var content = UIFactory.Rect("Content", panel);
            UIFactory.Stretch(content);
            UIFactory.Vertical(content.gameObject, spacing, new RectOffset(12, 12, 12, 12));
            return content;
        }

        private RectTransform Row(Transform parent, float height, float spacing = 6f)
        {
            var row = UIFactory.Rect("Row", parent);
            UIFactory.Size(row, height);
            UIFactory.Horizontal(row.gameObject, spacing);
            return row;
        }

        private void BuildLeftPanel(RectTransform root)
        {
            var panel = UIFactory.Panel(root, "Left Panel", UITheme.Panel).rectTransform;
            UIFactory.Place(panel, new Vector2(0, 0), new Vector2(0, 1), new Vector2(14, 14), new Vector2(386, -76));
            var col = Column(panel);

            UIFactory.Header(col, "Live KPIs (simulated)");
            var grid = UIFactory.Rect("KPI Grid", col);
            UIFactory.Size(grid, 4 * 74 + 3 * 8);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(168, 74);
            gl.spacing = new Vector2(8, 8);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            Kpi(grid, "vehicles", "Active vehicles", "vehicles");
            Kpi(grid, "speed", "Avg traffic speed", "speed");
            Kpi(grid, "congestion", "Congestion", "congestion");
            Kpi(grid, "incidents", "Active incidents", "incident");
            Kpi(grid, "aqi", "Air quality (AQI)", "aqi");
            Kpi(grid, "energy", "Energy demand", "energy");
            Kpi(grid, "response", "Emergency response", "response");
            Kpi(grid, "signals", "Signal control", "signal");

            UIFactory.Header(col, "Simulation control");
            var r1 = Row(col, 34);
            UIFactory.Button(r1, "START", () => _sim.StartSimulation(), new Color(0.0f, 0.45f, 0.25f));
            UIFactory.Button(r1, "PAUSE", () => _sim.PauseSimulation());
            UIFactory.Button(r1, "RESUME", () => _sim.ResumeSimulation());
            UIFactory.Button(r1, "RESET", () => { _sim.analytics.CancelABTest(); _sim.ResetSimulation(); }, new Color(0.4f, 0.12f, 0.15f));

            var r2 = Row(col, 30);
            _speedLabel = UIFactory.Label(r2, "Speed 1.0x", 14, UITheme.Text);
            UIFactory.Size(_speedLabel, -1, 92, 0);
            _speedSlider = UIFactory.Slider(r2, 0.25f, _sim.settings.maxSimSpeed, _sim.SimSpeed, v =>
            {
                float snapped = v < 1f ? Mathf.Round(v * 4f) / 4f : Mathf.Round(v * 2f) / 2f;
                _sim.SetSimulationSpeed(snapped);
            });
            UIFactory.Size(_speedSlider, -1, -1, 1);

            var r3 = Row(col, 34);
            UIFactory.Button(r3, $"+{_sim.settings.spawnBatch} VEHICLES", () =>
            {
                int n = _sim.traffic.SpawnVehicles(_sim.settings.spawnBatch);
                CityEvents.RaiseAlert(n > 0 ? $"Spawned {n} vehicles ({_sim.traffic.ActiveVehicles} active)." : $"Vehicle limit reached ({_sim.settings.maxVehicles}). Raise maxVehicles in SimulationSettings.", n > 0 ? AlertLevel.Info : AlertLevel.Warning);
            });
            UIFactory.Button(r3, $"-{_sim.settings.spawnBatch} VEHICLES", () =>
            {
                int n = _sim.traffic.RemoveVehicles(_sim.settings.spawnBatch);
                CityEvents.RaiseAlert($"Removed {n} vehicles ({_sim.traffic.ActiveVehicles} active).", AlertLevel.Info);
            });

            var r4 = Row(col, 34);
            _optButton = UIFactory.Button(r4, "OPTIMIZATION: OFF", () => _sim.ToggleOptimization());
            _abButton = UIFactory.Button(r4, "RUN A/B TEST", () => _sim.analytics.StartABTest(), new Color(0.25f, 0.15f, 0.45f));

            UIFactory.Header(col, "Visualization layer");
            var r5 = Row(col, 32, 4);
            LayerButton(r5, LayerMode.Normal, "NORMAL");
            LayerButton(r5, LayerMode.Traffic, "TRAFFIC");
            LayerButton(r5, LayerMode.Pollution, "POLLUTION");
            LayerButton(r5, LayerMode.Energy, "ENERGY");

            var r6 = Row(col, 32);
            UIFactory.Button(r6, "RESET CAMERA", () => _sim.cameraController.ResetView(false));
            UIFactory.Button(r6, "HIDE UI (H)", () => ToggleHideUI());

            var help = UIFactory.Label(col, "Mouse: left-drag pan, right-drag orbit, wheel zoom, click to select.\nKeys: WASD pan, Q/E rotate, Space pause, C camera, 1-4 layers, H hide UI.", 12, UITheme.TextDim, TextAnchor.UpperLeft);
            UIFactory.Size(help, 44);
            _inputWarning = UIFactory.Label(col, "", 12, UITheme.Warn, TextAnchor.UpperLeft);
            UIFactory.Size(_inputWarning, InputBridge.Available ? 0 : 40);
            if (!InputBridge.Available) _inputWarning.text = "Mouse/keyboard input is off: set Project Settings > Player > Active Input Handling to 'Both'.";
        }

        private void LayerButton(Transform row, LayerMode mode, string label)
        {
            var b = UIFactory.Button(row, label, () => { _sim.layers.SetMode(mode); RefreshLayerButtons(); }, null, 12);
            _layerButtons[mode] = b;
        }

        private void RefreshLayerButtons()
        {
            foreach (var kv in _layerButtons) UIFactory.SetButtonColor(kv.Value, kv.Key == _sim.layers.Mode ? UITheme.ButtonActive : UITheme.Button);
        }

        private void Kpi(Transform parent, string key, string label, string icon)
        {
            var card = UIFactory.Panel(parent, "KPI " + key, UITheme.Card, false).rectTransform;
            var ic = UIFactory.Icon(card, IconFactory.Get(icon), UITheme.Accent);
            UIFactory.Place(ic.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -30), new Vector2(30, -8));
            var l = UIFactory.Label(card, label, 12, UITheme.TextDim);
            UIFactory.Place(l.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(36, -30), new Vector2(-6, -6));
            var v = UIFactory.Label(card, "-", 22, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(v.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 18), new Vector2(-6, -28));
            var s = UIFactory.Label(card, "", 11, UITheme.TextDim);
            UIFactory.Place(s.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 3), new Vector2(-4, 20));
            _kpiValue[key] = v; _kpiSub[key] = s;
        }

        private void BuildRightPanel(RectTransform root)
        {
            var panel = UIFactory.Panel(root, "Right Panel", UITheme.Panel).rectTransform;
            UIFactory.Place(panel, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-434, 14), new Vector2(-14, -76));
            var col = Column(panel, 6f);

            UIFactory.Header(col, "City overview (click to fly)");
            var mapRow = UIFactory.Rect("Minimap Row", col);
            UIFactory.Size(mapRow, 236);
            var frame = UIFactory.Panel(mapRow, "Minimap Frame", new Color(0, 0, 0, 0.6f)).rectTransform;
            UIFactory.Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-118, -118), new Vector2(118, 118));
            var rawRt = UIFactory.Rect("Minimap", frame);
            UIFactory.Stretch(rawRt, 3f);
            var raw = rawRt.gameObject.AddComponent<RawImage>();
            var marker = UIFactory.Rect("Camera Marker", rawRt);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(14, 14);
            var mi = marker.gameObject.AddComponent<Image>(); mi.sprite = UIFactory.Circle; mi.color = UITheme.Accent; mi.raycastTarget = false;
            var minimap = rawRt.gameObject.AddComponent<MinimapView>();
            minimap.Build(_sim, raw, marker);

            UIFactory.Header(col, "Trends (simulated, last 2 min)");
            _trafficChart = Chart(col, "Congestion % (dashed: forecast)", new Color(0f, 0.9f, 1f), out _trafficValue);
            _trafficChart.secondaryColor = new Color(1f, 0.8f, 0.25f);
            _aqiChart = Chart(col, "City average AQI", new Color(0.85f, 0.45f, 1f), out _aqiValue);
            _aqiChart.threshold = _sim.settings.aqiWarningThreshold;
            _energyChart = Chart(col, "Energy demand (MW)", new Color(1f, 0.75f, 0.2f), out _energyValue);
            _trafficChart.SetData(_sim.analytics.CongestionSeries, _sim.analytics.ForecastSeries);
            _aqiChart.SetData(_sim.analytics.AqiSeries);
            _energyChart.SetData(_sim.analytics.EnergySeries);
            _forecastText = UIFactory.Label(col, "", 11, UITheme.TextDim, TextAnchor.UpperLeft);
            UIFactory.Size(_forecastText, 34);

            UIFactory.Header(col, "Scenario comparison");
            var cmp = UIFactory.Panel(col, "Comparison", UITheme.Card, false);
            UIFactory.Size(cmp, 196);
            var cc = UIFactory.Rect("Content", cmp.transform);
            UIFactory.Stretch(cc);
            UIFactory.Vertical(cc.gameObject, 2, new RectOffset(8, 8, 6, 6));
            _cmpTitle = UIFactory.Label(cc, "", 12, UITheme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Size(_cmpTitle, 18);
            var tableRow = Row(cc, 104, 4);
            for (int i = 0; i < 4; i++)
            {
                _cmpCols[i] = UIFactory.Label(tableRow, "", 12, UITheme.Text, i == 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
                _cmpCols[i].verticalOverflow = VerticalWrapMode.Overflow;
                _cmpCols[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                UIFactory.Size(_cmpCols[i], -1, -1, i == 0 ? 2.3f : 1f);
            }
            _cmpVerdict = UIFactory.Label(cc, "", 12, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Size(_cmpVerdict, 44);
            var barBg = UIFactory.Panel(cc, "AB Progress", new Color(1, 1, 1, 0.08f), false);
            UIFactory.Size(barBg, 6);
            var fill = UIFactory.Rect("Fill", barBg.transform);
            UIFactory.Place(fill, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            _abBar = fill.gameObject.AddComponent<Image>();
            _abBar.color = UITheme.Accent2;
        }

        private LineChartGraphic Chart(Transform parent, string title, Color color, out Text value)
        {
            var card = UIFactory.Panel(parent, "Chart " + title, UITheme.Card, false);
            UIFactory.Size(card, 104);
            var t = UIFactory.Label(card.transform, title, 12, UITheme.TextDim);
            UIFactory.Place(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -20), new Vector2(-90, -2));
            value = UIFactory.Label(card.transform, "-", 14, color, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place(value.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-110, -20), new Vector2(-8, -2));
            var area = UIFactory.Rect("Plot", card.transform);
            UIFactory.Place(area, Vector2.zero, Vector2.one, new Vector2(8, 6), new Vector2(-8, -22));
            var chart = area.gameObject.AddComponent<LineChartGraphic>();
            chart.lineColor = color;
            chart.raycastTarget = false;
            return chart;
        }

        private void BuildBottomPanel(RectTransform root)
        {
            var panel = UIFactory.Panel(root, "Bottom Panel", UITheme.Panel).rectTransform;
            UIFactory.Place(panel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(398, 14), new Vector2(-446, 282));
            var row = UIFactory.Rect("Columns", panel);
            UIFactory.Stretch(row);
            var h = UIFactory.Horizontal(row.gameObject, 12, new RectOffset(12, 12, 10, 10));
            h.childForceExpandWidth = false;

            // incidents
            var inc = UIFactory.Rect("Incidents", row);
            UIFactory.Size(inc, -1, -1, 1.15f);
            UIFactory.Vertical(inc.gameObject, 5, new RectOffset(0, 0, 0, 0));
            _incidentHeader = UIFactory.Header(inc, "Incidents");
            var t1 = Row(inc, 30, 4);
            UIFactory.Button(t1, "ACCIDENT", () => _sim.incidents.TriggerAccident(), new Color(0.45f, 0.08f, 0.14f), 12);
            UIFactory.Button(t1, "BLOCK ROAD", () => _sim.incidents.TriggerBlockedRoad(), new Color(0.45f, 0.25f, 0.02f), 12);
            UIFactory.Button(t1, "SIGNAL FAIL", () => _sim.incidents.TriggerSignalFailure(), new Color(0.4f, 0.35f, 0.02f), 12);
            var t2 = Row(inc, 30, 4);
            UIFactory.Button(t2, "POLLUTION SPIKE", () => _sim.incidents.TriggerPollutionSpike(), new Color(0.32f, 0.08f, 0.42f), 12);
            UIFactory.Button(t2, "EMERGENCY", () => _sim.incidents.TriggerEmergencyRequest(), new Color(0.45f, 0.08f, 0.3f), 12);
            var t3 = Row(inc, 32, 4);
            UIFactory.Button(t3, "DISPATCH", () => _sim.dispatch.DispatchToSelectedOrMostSevere(), new Color(0.0f, 0.42f, 0.55f), 13);
            UIFactory.Button(t3, "RESOLVE SELECTED", () => _sim.incidents.ResolveSelectedOrOldest(), new Color(0.0f, 0.4f, 0.22f), 13);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var b = UIFactory.Button(inc, "", () => OnIncidentRow(index), UITheme.Card, 12);
                UIFactory.Size(b, 24);
                var txt = b.GetComponentInChildren<Text>();
                txt.alignment = TextAnchor.MiddleLeft;
                txt.resizeTextForBestFit = false;
                txt.fontStyle = FontStyle.Normal;
                txt.rectTransform.offsetMin = new Vector2(8, 0);
                b.gameObject.SetActive(false);
                _incidentRows.Add(b);
                _incidentRowIds.Add(-1);
            }

            // details
            var det = UIFactory.Rect("Details", row);
            UIFactory.Size(det, -1, -1, 1.1f);
            UIFactory.Vertical(det.gameObject, 5, new RectOffset(0, 0, 0, 0));
            UIFactory.Header(det, "Selection details");
            _detailsText = UIFactory.Label(det, "", 13, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Size(_detailsText, -1, -1, -1, 1);
            var d2 = Row(det, 28, 4);
            UIFactory.Button(d2, "ALT ROUTE -> HOSPITAL", SuggestRoute, new Color(0.05f, 0.35f, 0.2f), 12);
            UIFactory.Button(d2, "FOCUS", () => { if (_sim.selection.SelectedPosition.HasValue) _sim.cameraController.FocusOn(_sim.selection.SelectedPosition.Value, 120f); }, null, 12);

            // recommendations
            var rec = UIFactory.Rect("Recommendations", row);
            UIFactory.Size(rec, -1, -1, 1f);
            UIFactory.Vertical(rec.gameObject, 5, new RectOffset(0, 0, 0, 0));
            UIFactory.Header(rec, "Decision support (rule-based)");
            _recoText = UIFactory.Label(rec, "", 12, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Size(_recoText, -1, -1, -1, 1);
        }

        private void BuildAlerts(RectTransform root)
        {
            var area = UIFactory.Rect("Alerts", root);
            UIFactory.Place(area, new Vector2(0, 1), new Vector2(1, 1), new Vector2(400, -170), new Vector2(-448, -80));
            UIFactory.Vertical(area.gameObject, 3, new RectOffset(0, 0, 0, 0));
            for (int i = 0; i < 3; i++)
            {
                var bg = UIFactory.Panel(area, "Alert", new Color(0.02f, 0.04f, 0.07f, 0.75f), false);
                UIFactory.Size(bg, 26);
                var t = UIFactory.Label(bg.transform, "", 13, UITheme.Text);
                UIFactory.Stretch(t.rectTransform, 6f);
                t.verticalOverflow = VerticalWrapMode.Truncate;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 10; t.resizeTextMaxSize = 13;
                bg.gameObject.SetActive(false);
                _alertLines.Add(t);
            }
        }

        // ================================================================== title screen

        private void BuildTitle()
        {
            var root = UIFactory.Rect("Title Screen", transform);
            UIFactory.Stretch(root);
            _titleGroup = root.gameObject.AddComponent<CanvasGroup>();
            var shade = UIFactory.Rect("Shade", root);
            UIFactory.Place(shade, Vector2.zero, new Vector2(0.55f, 1), Vector2.zero, Vector2.zero);
            var si = shade.gameObject.AddComponent<Image>();
            si.color = new Color(0.01f, 0.02f, 0.05f, 0.78f);
            var col = UIFactory.Rect("Title Column", root);
            UIFactory.Place(col, new Vector2(0, 0), new Vector2(0.55f, 1), new Vector2(110, 120), new Vector2(-40, -150));
            UIFactory.Vertical(col.gameObject, 14, new RectOffset(0, 0, 0, 0));

            var kicker = UIFactory.Label(col, "B.TECH CSE PROJECT  -  UNITY 6 SIMULATION", 16, UITheme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Size(kicker, 26);
            var title = UIFactory.Label(col, "AI-Powered\nSmart City Digital Twin", 64, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Size(title, 170);
            var sub = UIFactory.Label(col, "A live 3D model of a city: microscopic traffic simulation, adaptive signal optimization, A* routing, incident response with emergency dispatch, and air-quality and energy monitoring - all in one control room.", 20, UITheme.TextDim, TextAnchor.UpperLeft);
            UIFactory.Size(sub, 92);
            var feats = UIFactory.Label(col,
                "<color=#00E5FF>-</color>  Procedural city: roads, signals, 4 zones, hospital, police and fire stations\n" +
                "<color=#00E5FF>-</color>  Queue-based signal optimization with measured before/after results\n" +
                "<color=#00E5FF>-</color>  Incidents, severity rules, detours and emergency dispatch with ETA\n" +
                "<color=#00E5FF>-</color>  Pollution heatmap, energy layer, transparent congestion forecast", 18, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Size(feats, 120);
            var row = UIFactory.Rect("Buttons", col);
            UIFactory.Size(row, 60);
            var h = UIFactory.Horizontal(row.gameObject, 16);
            h.childForceExpandWidth = false;
            var enter = UIFactory.Button(row, "ENTER SIMULATION", () => _sim.EnterSimulation(), new Color(0.0f, 0.55f, 0.65f), 22);
            UIFactory.Size(enter, -1, 300);
            var quit = UIFactory.Button(row, "QUIT", Quit, UITheme.Button, 18);
            UIFactory.Size(quit, -1, 140);
            var note = UIFactory.Label(col, "All values shown are generated by this simulation - not real sensor data. Runs fully offline.", 14, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleLeft);
            UIFactory.Size(note, 24);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ================================================================== behaviour

        private void OnAlert(string msg, AlertLevel level)
        {
            Color c = level == AlertLevel.Critical ? UITheme.Bad : level == AlertLevel.Warning ? UITheme.Warn : level == AlertLevel.Success ? UITheme.Good : UITheme.Accent;
            string time = _sim != null ? FormatClock(_sim.HourOfDay) : "";
            _alerts.Insert(0, $"<color=#{UITheme.Hex(c)}>[{time}]</color> {msg}");
            if (_alerts.Count > 3) _alerts.RemoveAt(_alerts.Count - 1);
            for (int i = 0; i < _alertLines.Count; i++)
            {
                bool has = i < _alerts.Count;
                _alertLines[i].transform.parent.gameObject.SetActive(has);
                if (has) _alertLines[i].text = _alerts[i];
            }
        }

        private void OnIncidentRow(int index)
        {
            int id = _incidentRowIds[index];
            var inc = _sim.incidents.Find(id);
            if (inc == null) return;
            _sim.incidents.Select(inc);
            _sim.selection.SelectIncident(inc);
            _sim.cameraController.FocusOn(inc.Position, 140f);
        }

        private void SuggestRoute()
        {
            var sel = _sim.selection;
            int from;
            if (sel.SelectedKind == SelectableKind.Intersection) from = sel.SelectedId;
            else if (sel.SelectedPosition.HasValue) from = _sim.roadNetwork.NearestNode(sel.SelectedPosition.Value);
            else { CityEvents.RaiseAlert("Select a junction (or any object) first; the route is planned from there to City Hospital.", AlertLevel.Info); return; }
            int to = _sim.cityGenerator.HospitalNode >= 0 ? _sim.cityGenerator.HospitalNode : 0;
            string advice = _sim.routeAdvisor.Advise(from, to, "City Hospital");
            CityEvents.RaiseAlert(advice.Replace("\n", "  |  "), AlertLevel.Info);
            if (sel.SelectedKind != SelectableKind.Intersection) sel.Select(SelectableKind.Intersection, from);
            RefreshDetails();
        }

        private void ToggleHideUI()
        {
            _uiHidden = !_uiHidden;
            ShowDashboard(_sim.State != SimulationState.Title);
            if (_uiHidden) CityEvents.RaiseAlert("UI hidden - press H to show it again.", AlertLevel.Info);
        }

        private void Update()
        {
            if (_sim == null || !_sim.Initialized) return;
            _frames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f) { _fps = _frames / _fpsTimer; _frames = 0; _fpsTimer = 0f; }
            HandleKeys();
            if (_sim.State == SimulationState.Title) return;

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.2f) { _refreshTimer = 0f; RefreshTexts(); }
            _chartTimer += Time.unscaledDeltaTime;
            if (_chartTimer >= 0.5f)
            {
                _chartTimer = 0f;
                _trafficChart.Refresh(); _aqiChart.Refresh(); _energyChart.Refresh();
            }
        }

        private void HandleKeys()
        {
            if (!InputBridge.Available) return;
            if (_sim.State == SimulationState.Title)
            {
                if (InputBridge.KeyDown(KeyCode.Return)) _sim.EnterSimulation();
                return;
            }
            if (InputBridge.KeyDown(KeyCode.Space)) _sim.TogglePause();
            if (InputBridge.KeyDown(KeyCode.C)) _sim.cameraController.ResetView(false);
            if (InputBridge.KeyDown(KeyCode.H)) ToggleHideUI();
            if (InputBridge.KeyDown(KeyCode.Alpha1)) { _sim.layers.SetMode(LayerMode.Normal); RefreshLayerButtons(); }
            if (InputBridge.KeyDown(KeyCode.Alpha2)) { _sim.layers.SetMode(LayerMode.Traffic); RefreshLayerButtons(); }
            if (InputBridge.KeyDown(KeyCode.Alpha3)) { _sim.layers.SetMode(LayerMode.Pollution); RefreshLayerButtons(); }
            if (InputBridge.KeyDown(KeyCode.Alpha4)) { _sim.layers.SetMode(LayerMode.Energy); RefreshLayerButtons(); }
            if (InputBridge.KeyDown(KeyCode.F) && _sim.selection.SelectedPosition.HasValue) _sim.cameraController.FocusOn(_sim.selection.SelectedPosition.Value, 120f);
            if (InputBridge.KeyDown(KeyCode.Escape)) _sim.selection.Clear();
        }

        private static string FormatClock(float hour)
        {
            int h = Mathf.FloorToInt(hour) % 24;
            int m = Mathf.FloorToInt((hour - Mathf.Floor(hour)) * 60f);
            return $"{h:00}:{m:00}";
        }

        private void RefreshTexts()
        {
            var t = _sim.traffic;
            var s = _sim.settings;

            // top bar
            string state = _sim.State.ToString().ToUpper();
            Color sc = _sim.State == SimulationState.Running ? new Color(0f, 0.5f, 0.28f) : _sim.State == SimulationState.Paused ? new Color(0.55f, 0.4f, 0f) : UITheme.Button;
            _stateText.text = _sim.analytics.ABRunning ? "A/B TEST" : state;
            _statePill.color = _sim.analytics.ABRunning ? UITheme.Accent2 : sc;
            _clockText.text = $"<b>{FormatClock(_sim.HourOfDay)}</b> simulated   |   t = {_sim.SimTime:0} s   |   {_sim.SimSpeed:0.##}x";
            _fpsText.text = $"{_fps:0} FPS";
            _speedLabel.text = $"Speed {_sim.SimSpeed:0.##}x";
            if (!Mathf.Approximately(_speedSlider.value, _sim.SimSpeed) && !InputBridge.MouseHeld(0)) _speedSlider.SetValueWithoutNotify(_sim.SimSpeed);

            // KPIs
            SetKpi("vehicles", $"{t.ActiveVehicles}", $"max {s.maxVehicles} - trips {t.CompletedTrips}", UITheme.Text);
            SetKpi("speed", $"{t.AverageSpeedKmh:0.0} km/h", $"limit {s.carSpeedKmh:0} km/h", UITheme.Text);
            float cong = t.Congestion * 100f;
            SetKpi("congestion", $"{cong:0}%", $"forecast {_sim.analytics.Predictor.LastForecast * 100f:0}% in {s.forecastHorizonSeconds:0}s", cong > 55 ? UITheme.Bad : cong > 30 ? UITheme.Warn : UITheme.Good);
            int waiting = 0;
            foreach (var i in _sim.incidents.Active) if (i.Status == IncidentStatus.Active) waiting++;
            SetKpi("incidents", $"{_sim.incidents.Active.Count}", $"{waiting} awaiting dispatch", _sim.incidents.Active.Count > 0 ? UITheme.Warn : UITheme.Good);
            var p = _sim.pollution;
            SetKpi("aqi", $"{p.CityAverageAqi:0}", $"{PollutionManager.Category(p.CityAverageAqi)} - max {p.MaxAqi:0}", PollutionManager.CategoryColor(p.CityAverageAqi));
            var e = _sim.energy;
            SetKpi("energy", $"{e.TotalMW:0.00} MW", $"peak {e.PeakMW:0.00} - lights {(e.StreetlightsOn ? "ON" : "off")}", UITheme.Text);
            var d = _sim.dispatch;
            SetKpi("response", d.LastResponseSeconds >= 0 ? $"{d.LastResponseSeconds:0} s" : "--", d.ResponsesCompleted > 0 ? $"avg {d.AverageResponseSeconds:0} s (n={d.ResponsesCompleted})" : "no responses yet", UITheme.Text);
            int failed = 0, gapOuts = 0;
            foreach (var j in _sim.roadNetwork.Intersections) { if (j.Signal.Failed) failed++; gapOuts += j.Signal.GapOuts; }
            SetKpi("signals", _sim.OptimizationEnabled ? "ADAPTIVE" : "FIXED", failed > 0 ? $"{failed} failed signal(s)" : _sim.OptimizationEnabled ? $"queue-based, {gapOuts} gap-outs" : "fixed-time baseline",
                _sim.OptimizationEnabled ? UITheme.Good : UITheme.Warn);

            UIFactory.SetButtonLabel(_optButton, _sim.OptimizationEnabled ? "OPTIMIZATION: ON" : "OPTIMIZATION: OFF");
            UIFactory.SetButtonColor(_optButton, _sim.OptimizationEnabled ? new Color(0f, 0.45f, 0.25f) : UITheme.Button);
            UIFactory.SetButtonLabel(_abButton, _sim.analytics.ABRunning ? "A/B RUNNING..." : "RUN A/B TEST");
            RefreshLayerButtons();

            // charts
            var a = _sim.analytics;
            _trafficValue.text = $"{cong:0}%";
            _aqiValue.text = $"{p.CityAverageAqi:0}";
            _energyValue.text = $"{e.TotalMW:0.00} MW";
            float mae = a.Predictor.MeanAbsoluteError;
            _forecastText.text = $"Forecast +{a.Predictor.HorizonSeconds:0}s: <b>{a.Predictor.LastForecast * 100f:0}%</b>, trend {a.Predictor.TrendLabel()}, " +
                                 (float.IsNaN(mae) ? "error: not measured yet" : $"measured error {mae * 100f:0.0} pts (n={a.Predictor.EvaluatedForecasts})") +
                                 "\nBaseline model: moving average + linear trend (not machine learning)." +
                                 (_sim.settings.useExternalService ? "\n" + a.ExternalServiceStatus : "");

            RefreshComparison();
            RefreshIncidentList();
            RefreshDetails();
            RefreshRecommendations();
        }

        private void SetKpi(string key, string value, string sub, Color color)
        {
            _kpiValue[key].text = value;
            _kpiValue[key].color = color;
            _kpiSub[key].text = sub;
        }

        private void RefreshComparison()
        {
            var a = _sim.analytics;
            _cmpTitle.text = a.ABRunning ? a.ABStatus : a.ComparisonTitle;
            _abBar.rectTransform.anchorMax = new Vector2(a.ABRunning ? a.ABProgress : (a.HasComparison ? 1f : 0f), 1f);
            var c0 = new StringBuilder("<color=#8FA3BF>Metric</color>\n");
            var c1 = new StringBuilder("<color=#8FA3BF>Before</color>\n");
            var c2 = new StringBuilder("<color=#8FA3BF>After</color>\n");
            var c3 = new StringBuilder("<color=#8FA3BF>Change</color>\n");
            if (!a.HasComparison || a.ComparisonRows.Count == 0)
            {
                c0.Append("(no data yet)");
            }
            else
            {
                foreach (var r in a.ComparisonRows)
                {
                    Color c = r.Outcome > 0 ? UITheme.Good : r.Outcome < 0 ? UITheme.Bad : UITheme.TextDim;
                    c0.AppendLine($"{r.Name} ({r.Unit})");
                    c1.AppendLine($"{r.Before:0.0}");
                    c2.AppendLine($"{r.After:0.0}");
                    c3.AppendLine($"<color=#{UITheme.Hex(c)}>{r.DeltaPercent:+0;-0;0}%</color>");
                }
            }
            _cmpCols[0].text = c0.ToString(); _cmpCols[1].text = c1.ToString(); _cmpCols[2].text = c2.ToString(); _cmpCols[3].text = c3.ToString();
            _cmpVerdict.text = a.ComparisonVerdict;
        }

        private void RefreshIncidentList()
        {
            if (_sim == null || _incidentHeader == null) return;
            var list = _sim.incidents.Active;
            _incidentHeader.text = $"INCIDENTS  ({list.Count} active, {_sim.incidents.TotalCreated} total)";
            for (int i = 0; i < _incidentRows.Count; i++)
            {
                var b = _incidentRows[i];
                if (i < list.Count)
                {
                    var inc = list[list.Count - 1 - i];
                    _incidentRowIds[i] = inc.Id;
                    b.gameObject.SetActive(true);
                    Color sc = UITheme.ForSeverity(inc.Severity.Level);
                    string status = inc.Status == IncidentStatus.Dispatched && inc.AssignedUnit != null ? $"en route, ETA {inc.AssignedUnit.Eta:0}s" : inc.Status.ToString();
                    string sel = _sim.incidents.Selected == inc ? "> " : "";
                    UIFactory.SetButtonLabel(b, $"{sel}<color=#{UITheme.Hex(sc)}>{inc.Severity.Level.ToString().ToUpper()}</color>  {inc.Title} - {inc.Location(_sim.roadNetwork)} - {status}");
                }
                else
                {
                    _incidentRowIds[i] = -1;
                    b.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshDetails()
        {
            if (_detailsText == null || _sim == null) return;
            _detailsText.text = SelectionInfoBuilder.Build(_sim);
        }

        private void RefreshRecommendations()
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var r in _sim.analytics.Recommendations)
            {
                if (n++ >= 3) break;
                Color c = r.Priority == RecommendationPriority.Critical ? UITheme.Bad : r.Priority == RecommendationPriority.Warning ? UITheme.Warn : r.Priority == RecommendationPriority.Advisory ? UITheme.Accent : UITheme.Good;
                sb.AppendLine($"<color=#{UITheme.Hex(c)}><b>{r.Title}</b></color>");
                sb.AppendLine($"<size=11><color=#8FA3BF>Why:</color> {r.Reason}</size>");
                sb.AppendLine($"<size=11><color=#8FA3BF>Do:</color> {r.Action}</size>");
            }
            _recoText.text = sb.ToString();
        }
    }
}
