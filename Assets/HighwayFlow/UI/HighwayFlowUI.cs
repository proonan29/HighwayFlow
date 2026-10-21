using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HighwayFlow.UI
{
    /// <summary>Left control panel, bottom highway strip and the sweep chart (UI Toolkit, built in code).</summary>
    [RequireComponent(typeof(UIDocument))]
    public class HighwayFlowUI : MonoBehaviour
    {
        public AppController app;
        public const float PanelWidth = 400f;

        enum Metric { Throughput, Speed, Delay, Efficiency, Stopped }

        static readonly Color Bg = new Color(0.07f, 0.08f, 0.10f, 0.94f), Card = new Color(0.13f, 0.14f, 0.17f, 1f), Accent = new Color(0.22f, 0.62f, 1f),
            Text = new Color(0.92f, 0.93f, 0.95f), Dim = new Color(0.62f, 0.65f, 0.70f), Btn = new Color(0.22f, 0.24f, 0.28f);
        public static readonly Color[] ScenarioColors =
        {
            new Color(0.55f, 0.75f, 1f), new Color(1f, 0.62f, 0.22f), new Color(0.42f, 0.88f, 0.48f), new Color(0.80f, 0.52f, 1f), new Color(1f, 0.36f, 0.36f)
        };
        static readonly float[] Speeds = { 1f, 2f, 5f, 10f, 20f, 50f, 100f };

        VisualElement root, panel, chartPanel, grpRamp, grpExit, grpSpacing, grpSignal, progressFill, sweepCard;
        StripElement strip;
        ChartElement chart;
        Button startBtn, langKo, langEn;
        Label sweepStatus, csvLabel, scenarioDesc;
        Slider mainSlider;
        Label mainValue;
        readonly Dictionary<string, Label> live = new Dictionary<string, Label>();
        readonly List<Button> metricBtns = new List<Button>(), colorBtns = new List<Button>(), camBtns = new List<Button>();
        VisualElement summaryBox;
        Metric metric = Metric.Throughput;
        float nextRefresh, rebuildAt = -1f;
        bool built, chartVisible = true;

        static string T(string s) => Loc.T(s);
        static string F0(float v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string F1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Hms(float s) { int t = Mathf.RoundToInt(s); return $"{t / 60}:{t % 60:00}"; }

        public static string ScenarioName(Scenario s)
        {
            switch (s)
            {
                case Scenario.Straight: return T("1. Straight highway");
                case Scenario.OnRamp: return T("2. Highway + on-ramp");
                case Scenario.ExitThenEnter: return T("3. Exit, then on-ramp");
                case Scenario.EnterThenExit: return T("4. On-ramp, then exit");
                default: return T("5. Exit with traffic light");
            }
        }

        static string ScenarioShort(Scenario s) => ((int)s).ToString();

        void OnEnable() { Loc.Changed += Rebuild; Build(); }
        void OnDisable() { Loc.Changed -= Rebuild; }

        void Start()
        {
            if (app == null) return;
            app.Rebuilt += OnRebuilt;
            app.SweepFinished += OnSweepFinished;
            if (app.cameraRig != null) { app.cameraRig.isOverUI = IsOverUI; app.cameraRig.viewportLeft = PanelWidth / 1920f; }
            OnRebuilt();
        }

        void Rebuild() { Build(); RefreshAll(); }

        bool IsOverUI(Vector2 screenPos)
        {
            if (root == null || root.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            if (panel != null && panel.worldBound.Contains(p)) return true;
            if (chartPanel != null && chartPanel.resolvedStyle.display != DisplayStyle.None && chartPanel.worldBound.Contains(p)) return true;
            return false;
        }

        // ------------------------------------------------------------------ construction helpers

        static void Pad(VisualElement e, float v) { e.style.paddingLeft = e.style.paddingRight = e.style.paddingTop = e.style.paddingBottom = v; }
        static void Radius(VisualElement e, float r) { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r; }

        static Label L(string text, float size = 13, Color? c = null, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size; l.style.color = c ?? Text; l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = 2;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        static Button B(string text, System.Action onClick, float grow = 1f)
        {
            var b = new Button(onClick) { text = text };
            b.style.flexGrow = grow; b.style.height = 28; b.style.fontSize = 12; b.style.color = Text; b.style.backgroundColor = Btn;
            b.style.borderLeftWidth = b.style.borderRightWidth = b.style.borderTopWidth = b.style.borderBottomWidth = 0;
            Radius(b, 5); b.style.marginLeft = b.style.marginRight = 2;
            return b;
        }

        static void Select(List<Button> list, int idx)
        {
            for (int i = 0; i < list.Count; i++) list[i].style.backgroundColor = i == idx ? Accent : Btn;
        }

        static VisualElement Row() { var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; r.style.marginBottom = 4; return r; }

        VisualElement Section(string header)
        {
            var c = new VisualElement(); c.style.backgroundColor = Card; Radius(c, 7); Pad(c, 9); c.style.marginBottom = 8;
            var h = L(header, 12, Dim, true); h.style.marginBottom = 5; c.Add(h);
            scroll.Add(c);
            return c;
        }

        ScrollView scroll;

        Slider Sld(VisualElement parent, string label, float min, float max, float val, System.Func<float, string> fmt, System.Action<float> onChange, out Label valueLabel)
        {
            var row = Row(); row.style.marginBottom = 0;
            var l = L(label, 12, Dim); l.style.flexGrow = 1;
            var v = L(fmt(val), 12, Text, true);
            row.Add(l); row.Add(v);
            var s = new Slider(min, max) { value = val };
            s.style.marginBottom = 4;
            s.RegisterValueChangedCallback(e => { v.text = fmt(e.newValue); onChange(e.newValue); });
            parent.Add(row); parent.Add(s);
            valueLabel = v;
            return s;
        }

        Label LiveRow(VisualElement parent, string key, string label)
        {
            var row = Row(); row.style.marginBottom = 1;
            var l = L(label, 12, Dim); l.style.flexGrow = 1;
            var v = L("-", 12, Text, true);
            row.Add(l); row.Add(v); parent.Add(row);
            live[key] = v;
            return v;
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement;
            if (root == null) return;
            root.Clear(); live.Clear(); metricBtns.Clear(); colorBtns.Clear(); camBtns.Clear();
            root.pickingMode = PickingMode.Ignore;

            panel = new VisualElement();
            panel.style.position = Position.Absolute; panel.style.left = 0; panel.style.top = 0; panel.style.bottom = 0; panel.style.width = PanelWidth;
            panel.style.backgroundColor = Bg;
            scroll = new ScrollView(); scroll.style.flexGrow = 1; Pad(scroll.contentContainer, 10);
            panel.Add(scroll); root.Add(panel);

            BuildHeader();
            BuildRunControls();
            BuildScenario();
            BuildTraffic();
            BuildLive();
            BuildView();
            BuildSweep();
            BuildStrip();
            BuildChartPanel();
            built = true;
            ApplyScenarioVisibility();
        }

        void BuildHeader()
        {
            var c = new VisualElement(); c.style.marginBottom = 8;
            c.Add(L("HighwayFlow", 22, Text, true));
            c.Add(L(T("Highway traffic flow simulator"), 12, Dim));
            var row = Row(); row.style.marginTop = 4;
            row.Add(L(T("Language"), 12, Dim));
            langKo = B("한국어", () => Loc.Korean = true, 0f); langEn = B("English", () => Loc.Korean = false, 0f);
            langKo.style.width = 70; langEn.style.width = 70;
            langKo.style.backgroundColor = Loc.Korean ? Accent : Btn; langEn.style.backgroundColor = Loc.Korean ? Btn : Accent;
            row.Add(langKo); row.Add(langEn);
            c.Add(row);
            scroll.Add(c);
        }

        void BuildRunControls()
        {
            var sec = Section(T("Simulation"));
            var row = Row();
            startBtn = B(app != null && app.running ? T("Pause") : T("Start"), ToggleRun, 2f);
            startBtn.style.backgroundColor = Accent;
            row.Add(startBtn);
            row.Add(B(T("Reset"), () => { RebuildApp(); }, 1f));
            row.Add(B(T("+1 min"), () => { app.AdvanceNow(60f); }, 1f));
            sec.Add(row);
            int si = 2;
            for (int i = 0; i < Speeds.Length; i++) if (Mathf.Approximately(Speeds[i], app.speed)) si = i;
            var sl = new SliderInt(0, Speeds.Length - 1) { value = si };
            var lbl = L(T("Speed") + ": x" + F0(Speeds[si]), 12, Dim);
            sl.RegisterValueChangedCallback(e => { app.speed = Speeds[e.newValue]; lbl.text = T("Speed") + ": x" + F0(Speeds[e.newValue]); });
            sec.Add(lbl); sec.Add(sl);
        }

        void ToggleRun()
        {
            app.SetRunning(!app.running);
            startBtn.text = app.running ? T("Pause") : T("Start");
        }

        void BuildScenario()
        {
            var sec = Section(T("Scenario"));
            var names = new List<string>();
            foreach (var s in SweepRunner.AllScenarios) names.Add(ScenarioName(s));
            var dd = new DropdownField(names, (int)app.P.scenario - 1);
            dd.RegisterValueChangedCallback(e =>
            {
                int idx = names.IndexOf(e.newValue);
                if (idx < 0) return;
                app.P.scenario = (Scenario)(idx + 1);
                ApplyScenarioVisibility(); QueueRebuild();
            });
            sec.Add(dd);
            scenarioDesc = L("", 11, Dim); scenarioDesc.style.marginTop = 4;
            sec.Add(scenarioDesc);
        }

        void BuildTraffic()
        {
            var sec = Section(T("Road and traffic"));
            var lanes = new SliderInt(2, 5) { value = app.P.lanes };
            var lanesLbl = L(T("Lanes") + ": " + app.P.lanes, 12, Dim);
            lanes.RegisterValueChangedCallback(e => { lanesLbl.text = T("Lanes") + ": " + e.newValue; app.P.lanes = e.newValue; QueueRebuild(); });
            sec.Add(lanesLbl); sec.Add(lanes);

            mainSlider = Sld(sec, T("Vehicles per minute (mainline)"), 5f, 200f, app.P.mainPerMin, v => F0(v) + " " + T("veh/min"), v => app.P.mainPerMin = Mathf.Round(v), out mainValue);

            grpRamp = new VisualElement(); sec.Add(grpRamp);
            Sld(grpRamp, T("On-ramp flow (% of mainline)"), 0f, 60f, app.P.rampRatio * 100f, v => F0(v) + "%", v => app.P.rampRatio = Mathf.Round(v) / 100f, out _);
            grpExit = new VisualElement(); sec.Add(grpExit);
            Sld(grpExit, T("Exit share (% of vehicles)"), 0f, 60f, app.P.exitShare * 100f, v => F0(v) + "%", v => app.P.exitShare = Mathf.Round(v) / 100f, out _);
            grpSpacing = new VisualElement(); sec.Add(grpSpacing);
            Sld(grpSpacing, T("Distance between the two ramps (m)"), 300f, 2000f, app.P.rampSpacing, v => F0(v) + " m", v => { app.P.rampSpacing = Mathf.Round(v / 50f) * 50f; QueueRebuild(); }, out _);
            Sld(grpSpacing, T("Exit ramp length (m)"), 60f, 500f, app.P.exitRampLen, v => F0(v) + " m", v => { app.P.exitRampLen = Mathf.Round(v / 10f) * 10f; QueueRebuild(); }, out _);

            grpSignal = new VisualElement(); sec.Add(grpSignal);
            var ord = Row();
            var b1 = B(T("Exit first"), null); var b2 = B(T("On-ramp first"), null);
            var ordBtns = new List<Button> { b1, b2 };
            b1.clicked += () => { app.P.exitFirst = true; Select(ordBtns, 0); QueueRebuild(); };
            b2.clicked += () => { app.P.exitFirst = false; Select(ordBtns, 1); QueueRebuild(); };
            Select(ordBtns, app.P.exitFirst ? 0 : 1);
            ord.Add(b1); ord.Add(b2);
            grpSignal.Add(L(T("Order of the ramps"), 12, Dim)); grpSignal.Add(ord);
            Sld(grpSignal, T("Traffic light cycle (s)"), 20f, 180f, app.P.signalCycle, v => F0(v) + " s", v => app.P.signalCycle = Mathf.Round(v / 5f) * 5f, out _);
            Sld(grpSignal, T("Green share (%)"), 10f, 90f, app.P.signalGreenRatio * 100f, v => F0(v) + "%", v => app.P.signalGreenRatio = Mathf.Round(v) / 100f, out _);
        }

        void BuildLive()
        {
            var sec = Section(T("Live"));
            LiveRow(sec, "time", T("Time"));
            LiveRow(sec, "onroad", T("Vehicles on the road"));
            LiveRow(sec, "speed", T("Mean speed"));
            LiveRow(sec, "thr", T("Trips completed (last min)"));
            LiveRow(sec, "eff", T("Travel-time efficiency"));
            LiveRow(sec, "delay", T("Mean delay per trip"));
            LiveRow(sec, "wait", T("Waiting to enter (main / ramp)"));
            LiveRow(sec, "exitq", T("Exit queue"));
            LiveRow(sec, "missed", T("Missed exits"));
            LiveRow(sec, "light", T("Traffic light"));
        }

        void BuildView()
        {
            var sec = Section(T("View"));
            sec.Add(L(T("Vehicle colour"), 12, Dim));
            var r1 = Row();
            string[] cn = { T("Speed"), T("Role"), T("Lane") };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = B(cn[i], () => { if (app.cars != null) app.cars.colorMode = (ColorMode)k; Select(colorBtns, k); });
                colorBtns.Add(b); r1.Add(b);
            }
            sec.Add(r1);
            Select(colorBtns, app.cars != null ? (int)app.cars.colorMode : 0);
            sec.Add(L(T("Camera"), 12, Dim));
            var r2 = Row();
            string[] cam = { T("Ramp area"), T("Whole road"), T("Follow a car") };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = B(cam[i], () =>
                {
                    if (app.cameraRig == null) return;
                    if (k == 0) app.FrameDefault(); else if (k == 1) app.cameraRig.FrameOverview(app.P.length); else app.cameraRig.SetFollow();
                    Select(camBtns, k);
                });
                camBtns.Add(b); r2.Add(b);
            }
            sec.Add(r2);
            Select(camBtns, app.cameraRig != null ? (int)app.cameraRig.mode : 0);
            sec.Add(L(T("Mouse: left-drag rotate, right-drag move, wheel zoom"), 11, Dim));
        }

        void BuildSweep()
        {
            sweepCard = Section(T("Efficiency sweep: vehicles per minute"));
            sweepCard.Add(L(T("Runs all selected scenarios headless with increasing vehicles per minute and compares the efficiency."), 11, Dim));
            var sw = app.sweep;
            Sld(sweepCard, T("From (veh/min)"), 5f, 100f, sw.from, v => F0(v), v => sw.from = Mathf.Round(v), out _);
            Sld(sweepCard, T("To (0 = automatic)"), 0f, 250f, sw.to, v => v < 1f ? T("auto") : F0(v), v => sw.to = Mathf.Round(v), out _);
            Sld(sweepCard, T("Points"), 3f, 20f, sw.points, v => F0(v), v => sw.points = Mathf.RoundToInt(v), out _);
            Sld(sweepCard, T("Seeds per point"), 1f, 5f, sw.seeds, v => F0(v), v => sw.seeds = Mathf.RoundToInt(v), out _);
            var tr = Row(); tr.style.flexWrap = Wrap.Wrap;
            foreach (var s in SweepRunner.AllScenarios)
            {
                int i = (int)s - 1;
                var t = new Toggle(ScenarioShort(s)) { value = sw.scenarios[i] };
                t.style.marginRight = 8; t.labelElement.style.color = ScenarioColors[i]; t.labelElement.style.minWidth = 0; t.labelElement.style.fontSize = 13;
                t.RegisterValueChangedCallback(e => sw.scenarios[i] = e.newValue);
                tr.Add(t);
            }
            sweepCard.Add(L(T("Scenarios"), 12, Dim)); sweepCard.Add(tr);
            var run = B(T("Run sweep"), () => { app.StartSweep(); }, 1f); run.style.backgroundColor = Accent; run.style.marginTop = 4;
            sweepCard.Add(run);
            var bar = new VisualElement(); bar.style.height = 6; bar.style.backgroundColor = Btn; bar.style.marginTop = 6; Radius(bar, 3);
            progressFill = new VisualElement(); progressFill.style.height = 6; progressFill.style.width = Length.Percent(0); progressFill.style.backgroundColor = Accent; Radius(progressFill, 3);
            bar.Add(progressFill); sweepCard.Add(bar);
            sweepStatus = L("", 11, Dim); sweepStatus.style.marginTop = 3; sweepCard.Add(sweepStatus);
            csvLabel = L("", 10, Dim); sweepCard.Add(csvLabel);
        }

        void BuildStrip()
        {
            var host = new VisualElement();
            host.style.position = Position.Absolute; host.style.left = PanelWidth + 10f; host.style.right = 10f; host.style.bottom = 10f; host.style.height = 150f;
            host.pickingMode = PickingMode.Ignore;
            strip = new StripElement { getSim = () => app != null ? app.Sim : null };
            strip.style.flexGrow = 1; Radius(strip, 6);
            host.Add(strip);
            root.Add(host);
        }

        void BuildChartPanel()
        {
            chartPanel = new VisualElement();
            var cp = chartPanel;
            cp.style.position = Position.Absolute; cp.style.right = 10f; cp.style.top = 10f; cp.style.width = 620f; cp.style.height = 545f;
            cp.style.backgroundColor = Bg; Radius(cp, 8); Pad(cp, 10);
            cp.style.display = chartVisible && app.SweepResults != null ? DisplayStyle.Flex : DisplayStyle.None;
            var head = Row();
            var title = L(T("Sweep result"), 14, Text, true); title.style.flexGrow = 1; head.Add(title);
            head.Add(B("x", () => { chartVisible = false; cp.style.display = DisplayStyle.None; }, 0f));
            cp.Add(head);
            var mr = Row();
            string[] mn = { T("Throughput"), T("Mean speed"), T("Delay"), T("Efficiency"), T("Stopped") };
            for (int i = 0; i < mn.Length; i++)
            {
                int k = i;
                var b = B(mn[i], () => { metric = (Metric)k; Select(metricBtns, k); RefreshChart(); }); b.style.height = 26;
                metricBtns.Add(b); mr.Add(b);
            }
            cp.Add(mr); Select(metricBtns, (int)metric);
            chart = new ChartElement(); chart.style.minHeight = 260; chart.style.flexGrow = 1;
            cp.Add(chart);
            var legend = Row(); legend.style.flexWrap = Wrap.Wrap;
            foreach (var s in SweepRunner.AllScenarios)
            {
                var l = L("■ " + ScenarioName(s), 11, ScenarioColors[(int)s - 1]); l.style.marginRight = 10; legend.Add(l);
            }
            cp.Add(legend);
            summaryBox = new VisualElement(); summaryBox.style.marginTop = 4; cp.Add(summaryBox);
            root.Add(cp);
            RefreshChart();
        }

        // ------------------------------------------------------------------ updates

        void QueueRebuild() { rebuildAt = Time.unscaledTime + 0.3f; }

        void ApplyScenarioVisibility()
        {
            if (!built || app == null) return;
            var s = app.P.scenario;
            bool hasEnter = s != Scenario.Straight, hasExit = s == Scenario.ExitThenEnter || s == Scenario.EnterThenExit || s == Scenario.ExitSignal;
            grpRamp.style.display = hasEnter ? DisplayStyle.Flex : DisplayStyle.None;
            grpExit.style.display = hasExit ? DisplayStyle.Flex : DisplayStyle.None;
            grpSpacing.style.display = hasExit ? DisplayStyle.Flex : DisplayStyle.None;
            grpSignal.style.display = s == Scenario.ExitSignal ? DisplayStyle.Flex : DisplayStyle.None;
            string d;
            switch (s)
            {
                case Scenario.Straight: d = T("Plain straight highway: only through traffic."); break;
                case Scenario.OnRamp: d = T("An on-ramp joins the highway gently through an acceleration lane."); break;
                case Scenario.ExitThenEnter: d = T("Vehicles leave at the exit first, then new vehicles join at the on-ramp."); break;
                case Scenario.EnterThenExit: d = T("Vehicles join first and the exit follows: short distances create a weaving section."); break;
                default: d = T("The exit ramp ends in a traffic light. When the queue is longer than the ramp it blocks the highway."); break;
            }
            scenarioDesc.text = d;
        }

        bool uiRebuild;

        void RebuildApp() { uiRebuild = true; bool run = app.running; app.Rebuild(); app.running = run; uiRebuild = false; }

        void OnRebuilt()
        {
            if (!built) return;
            if (uiRebuild) { RefreshAll(); return; }
            // changed from outside (CLI): rebuild the panel so every control shows the new values
            float off = scroll != null ? scroll.scrollOffset.y : 0f;
            Build(); RefreshAll();
            if (scroll != null) scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0f, off)).ExecuteLater(30);
        }

        void RefreshAll()
        {
            ApplyScenarioVisibility();
            if (startBtn != null && app != null) startBtn.text = app.running ? T("Pause") : T("Start");
            if (app != null && app.cameraRig != null) Select(camBtns, (int)app.cameraRig.mode);
            RefreshLive(); RefreshChart();
        }

        void OnSweepFinished()
        {
            chartVisible = true;
            if (chartPanel != null) chartPanel.style.display = DisplayStyle.Flex;
            RefreshChart();
        }

        void Update()
        {
            if (app == null || !built) return;
            if (rebuildAt > 0f && Time.unscaledTime >= rebuildAt) { rebuildAt = -1f; RebuildApp(); }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            RefreshLive();
            strip?.MarkDirtyRepaint();
            if (progressFill != null)
            {
                progressFill.style.width = Length.Percent(app.SweepRunning ? app.SweepProgress * 100f : (app.SweepResults != null ? 100f : 0f));
                sweepStatus.text = app.SweepRunning ? T("Running…") + " " + F0(app.SweepProgress * 100f) + "%" : (app.SweepResults != null ? T("Done") : "");
                csvLabel.text = !string.IsNullOrEmpty(app.SweepCsvPath) ? "CSV: " + app.SweepCsvPath : "";
            }
            if (chartPanel != null) { var want = chartVisible && app.SweepResults != null ? DisplayStyle.Flex : DisplayStyle.None; if (chartPanel.style.display != want) chartPanel.style.display = want; }
            if (chart != null) { float mk = app.P.mainPerMin; if (!Mathf.Approximately(chart.markerX, mk)) { chart.markerX = mk; chart.MarkDirtyRepaint(); } }
        }

        void RefreshLive()
        {
            var sim = app != null ? app.Sim : null;
            if (sim == null || live.Count == 0) return;
            var r = sim.Result();
            live["time"].text = Hms(sim.time);
            live["onroad"].text = sim.vehicles.Count.ToString();
            live["speed"].text = F0(sim.InstantMeanSpeedKmh) + " km/h";
            live["thr"].text = sim.LastMinuteCompleted + " " + T("veh/min");
            live["eff"].text = sim.time > 150f ? F0(r.travelEfficiency * 100f) + "%" : "-";
            live["eff"].style.color = Color.Lerp(new Color(1f, 0.35f, 0.3f), new Color(0.4f, 0.95f, 0.5f), Mathf.Clamp01((r.travelEfficiency - 0.2f) / 0.7f));
            live["delay"].text = F0(r.meanDelaySec) + " s";
            live["wait"].text = sim.MainQueue + " / " + sim.RampQueue;
            live["exitq"].text = sim.Lay.hasExit ? sim.ExitQueueLength.ToString() : "-";
            live["missed"].text = sim.Lay.hasExit ? F0(r.missedExits) : "-";
            live["light"].text = sim.Lay.hasSignal ? (sim.SignalGreen ? T("Green") : T("Red")) : "-";
            live["light"].style.color = sim.Lay.hasSignal ? (sim.SignalGreen ? new Color(0.4f, 0.95f, 0.5f) : new Color(1f, 0.4f, 0.35f)) : Text;
        }

        static float Val(SimResult r, Metric m)
        {
            switch (m)
            {
                case Metric.Throughput: return r.throughput;
                case Metric.Speed: return r.meanSpeedKmh;
                case Metric.Delay: return r.meanDelaySec;
                case Metric.Efficiency: return r.efficiency * 100f;
                default: return r.stoppedShare * 100f;
            }
        }

        void RefreshChart()
        {
            if (chart == null || app == null) return;
            var res = app.SweepResults;
            var data = new List<ChartElement.Series>();
            summaryBox?.Clear();
            if (res != null)
            {
                foreach (var g in res.GroupBy(r => r.scenario))
                {
                    var s = new ChartElement.Series { name = ScenarioName(g.Key), color = ScenarioColors[(int)g.Key - 1] };
                    foreach (var r in g.OrderBy(r => r.mainPerMin)) s.pts.Add(new Vector2(r.mainPerMin, Val(r, metric)));
                    data.Add(s);
                    if (summaryBox != null)
                    {
                        var ord = g.OrderBy(r => r.mainPerMin).ToList();
                        float peak = ord.Max(r => r.throughput);
                        var knee = ord.FirstOrDefault(r => r.efficiency < 0.7f);
                        string kneeTxt = knee != null ? T("efficiency < 70% from") + " " + F0(knee.mainPerMin) + " " + T("veh/min") : T("efficiency stays >= 70%");
                        summaryBox.Add(L(ScenarioShort(g.Key) + ": " + T("peak throughput") + " " + F0(peak) + " " + T("veh/min") + ", " + kneeTxt, 11, ScenarioColors[(int)g.Key - 1]));
                    }
                }
            }
            string[] units = { T("Trips completed (veh/min)"), T("Mean speed (km/h)"), T("Mean delay per trip (s)"), T("Efficiency (%)"), T("Stopped vehicle-time (%)") };
            chart.markerX = app.P.mainPerMin;
            chart.SetData(data, T("Vehicles per minute entering the mainline"), units[(int)metric], metric == Metric.Efficiency || metric == Metric.Stopped ? 100f : (float?)null);
        }
    }
}

