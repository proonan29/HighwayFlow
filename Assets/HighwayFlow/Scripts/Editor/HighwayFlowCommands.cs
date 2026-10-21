using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HighwayFlow.UI;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace HighwayFlow.EditorTools
{
    /// <summary>CLI commands for the HighwayFlow simulator: `unity command hf_*`.</summary>
    public static class HighwayFlowCommands
    {
        const string ScenePath = "Assets/HighwayFlow/Scenes/HighwayFlow.unity";
        const string UiFolder = "Assets/HighwayFlow/UI";
        const string PanelSettingsPath = UiFolder + "/HighwayFlowPanelSettings.asset";
        const string ThemePath = UiFolder + "/HighwayFlowTheme.tss";
        const string TextSettingsPath = UiFolder + "/HighwayFlowTextSettings.asset";
        const string FontPath = UiFolder + "/Fonts/NotoSansKR-Regular.otf";
        const string FontAssetPath = UiFolder + "/Fonts/NotoSansKR-Regular SDF.asset";

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ------------------------------------------------------------------ scene

        [CliCommand("hf_setup_scene", "Create the HighwayFlow scene: sun, camera rig, App (simulation + road at play time), vehicle renderer and UI. Saves the scene.")]
        public static string SetupScene([CliArg("scene_path", "Scene asset path")] string scenePath = ScenePath)
        {
            if (Application.isPlaying) return "error: exit play mode first";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f; light.color = new Color(1f, 0.96f, 0.9f); light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(52f, -40f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.52f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.30f, 0.28f);
            RenderSettings.sun = light;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f; cam.nearClipPlane = 1f; cam.farClipPlane = 6000f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.58f, 0.74f, 0.92f);
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rig = camGo.AddComponent<CameraRig>();
            rig.viewportLeft = HighwayFlowUI.PanelWidth / 1920f;

            var appGo = new GameObject("App");
            var app = appGo.AddComponent<AppController>();
            var carsGo = new GameObject("Vehicles");
            carsGo.transform.SetParent(appGo.transform, false);
            app.cars = carsGo.AddComponent<VehicleRenderer>();
            app.cameraRig = rig;

            var uiGo = new GameObject("UI");
            var doc = uiGo.AddComponent<UIDocument>();
            var ui = uiGo.AddComponent<HighwayFlowUI>();
            ui.app = app;
            doc.panelSettings = EnsurePanelSettings();
            SetupFont(false);

            EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, scenePath);
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == scenePath)) { list.Insert(0, new EditorBuildSettingsScene(scenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
            AssetDatabase.SaveAssets();
            return "scene=" + scenePath;
        }

        static PanelSettings EnsurePanelSettings()
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (ps != null) return ps;
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            return ps;
        }

        [CliCommand("hf_setup_font", "Create the Noto Sans KR dynamic font asset and register it as a fallback for the UI.")]
        public static string SetupFont([CliArg("force", "Recreate the font asset")] bool force = false)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) return $"error: font not found: {FontPath}";
            var fa = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath);
            if (fa != null && force) { AssetDatabase.DeleteAsset(FontAssetPath); fa = null; }
            if (fa == null)
            {
                fa = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (fa == null) return "error: CreateFontAsset failed";
                fa.name = "NotoSansKR-Regular SDF";
                fa.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(fa, FontAssetPath);
                if (fa.atlasTextures != null)
                    foreach (var tex in fa.atlasTextures) if (tex != null) { tex.name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
                if (fa.material != null) { fa.material.name = fa.name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
                EditorUtility.SetDirty(fa);
            }
            var ts = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextSettingsPath);
            if (ts == null) { ts = ScriptableObject.CreateInstance<PanelTextSettings>(); AssetDatabase.CreateAsset(ts, TextSettingsPath); }
            if (ts.fallbackFontAssets == null) ts.fallbackFontAssets = new List<FontAsset>();
            if (!ts.fallbackFontAssets.Contains(fa)) ts.fallbackFontAssets.Add(fa);
            ts.clearDynamicDataOnBuild = true;
            EditorUtility.SetDirty(ts);
            var ps = EnsurePanelSettings();
            ps.textSettings = ts;
            EditorUtility.SetDirty(ps);
            AssetDatabase.SaveAssets();
            return $"font={FontAssetPath} hangul={fa.HasCharacter('한', true, true)}";
        }

        // ------------------------------------------------------------------ parameters

        static AppController App(out string error)
        {
            error = null;
            var app = Object.FindAnyObjectByType<AppController>();
            if (app == null) error = "error: no AppController in the active scene (hf_setup_scene)";
            return app;
        }

        static string Describe(SimParams p) =>
            $"scenario={(int)p.scenario} lanes={p.lanes} flow={F(p.mainPerMin)}/min ramp={F(p.RampPerMin)}/min exit={F(p.exitShare * 100)}% spacing={F(p.rampSpacing)}m exitRamp={F(p.exitRampLen)}m cycle={F(p.signalCycle)}s green={F(p.signalGreenRatio * 100)}%";

        static void Apply(SimParams p, int scenario, int lanes, float flow, float rampPct, float exitPct, float spacing, float exitRampLen, float cycle, float greenPct, int order)
        {
            if (scenario >= 1 && scenario <= 5) p.scenario = (Scenario)scenario;
            if (lanes >= 2) p.lanes = Mathf.Clamp(lanes, 2, 5);
            if (flow >= 0f) p.mainPerMin = flow;
            if (rampPct >= 0f) p.rampRatio = rampPct / 100f;
            if (exitPct >= 0f) p.exitShare = exitPct / 100f;
            if (spacing > 0f) p.rampSpacing = spacing;
            if (exitRampLen > 0f) p.exitRampLen = exitRampLen;
            if (cycle > 0f) p.signalCycle = cycle;
            if (greenPct > 0f) p.signalGreenRatio = greenPct / 100f;
            if (order == 0) p.exitFirst = false; else if (order == 1) p.exitFirst = true;
        }

        [CliCommand("hf_set", "Set simulation parameters (-1 / 0 = keep). In play mode the road is rebuilt; in edit mode the values are stored in the scene.")]
        public static string Set(
            [CliArg("scenario", "1..5 (0 keeps)")] int scenario = 0,
            [CliArg("lanes", "2..5 (0 keeps)")] int lanes = 0,
            [CliArg("flow", "Vehicles per minute on the mainline (-1 keeps)")] float flow = -1f,
            [CliArg("ramp_pct", "On-ramp flow as % of the mainline (-1 keeps)")] float rampPct = -1f,
            [CliArg("exit_pct", "Share of vehicles taking the exit, % (-1 keeps)")] float exitPct = -1f,
            [CliArg("spacing", "Distance between the two ramps, m (0 keeps)")] float spacing = 0f,
            [CliArg("exit_ramp_len", "Exit ramp length, m (0 keeps)")] float exitRampLen = 0f,
            [CliArg("cycle", "Traffic light cycle, s (0 keeps)")] float cycle = 0f,
            [CliArg("green_pct", "Green share, % (0 keeps)")] float greenPct = 0f,
            [CliArg("order", "Scenario 5: 1 = exit first, 0 = on-ramp first, -1 keeps")] int order = -1)
        {
            var app = App(out var err); if (app == null) return err;
            Apply(app.P, scenario, lanes, flow, rampPct, exitPct, spacing, exitRampLen, cycle, greenPct, order);
            if (Application.isPlaying) app.Rebuild(); else { app.Normalize(); EditorUtility.SetDirty(app); EditorSceneManager.MarkSceneDirty(app.gameObject.scene); }
            return Describe(app.P);
        }

        // ------------------------------------------------------------------ headless

        [CliCommand("hf_sim_run", "Headless run of one scenario (or all five) at a given flow; prints the efficiency indicators.")]
        public static string SimRun(
            [CliArg("scenario", "1..5, or 0 for all")] int scenario = 0,
            [CliArg("lanes", "2..5")] int lanes = 3,
            [CliArg("flow", "Vehicles per minute on the mainline")] float flow = 60f,
            [CliArg("ramp_pct", "On-ramp flow as % of the mainline")] float rampPct = 25f,
            [CliArg("exit_pct", "Share of vehicles taking the exit, %")] float exitPct = 20f,
            [CliArg("spacing", "Distance between the ramps, m")] float spacing = 800f,
            [CliArg("minutes", "Simulated minutes")] float minutes = 30f,
            [CliArg("seed", "Random seed")] int seed = 1)
        {
            var sb = new StringBuilder();
            sb.AppendLine("sc  flow  dem  thr served speed delay  eff stop% dens qMain qRamp qExit missed");
            foreach (var s in SweepRunner.AllScenarios)
            {
                if (scenario != 0 && (int)s != scenario) continue;
                var p = new SimParams { scenario = s, lanes = Mathf.Clamp(lanes, 2, 5), mainPerMin = flow, rampRatio = rampPct / 100f, exitShare = exitPct / 100f, rampSpacing = spacing, seed = seed, duration = minutes * 60f };
                sb.AppendLine(Row(SweepRunner.RunOne(p)));
            }
            return sb.ToString();
        }

        static string Row(SimResult r) =>
            $"{(int)r.scenario} {r.mainPerMin,6:0.#} {r.demandPerMin,5:0.#} {r.throughput,5:0.#} {r.servedRatio,5:0.00} {r.meanSpeedKmh,5:0} {r.meanDelaySec,5:0} {r.efficiency,5:0.00} {r.stoppedShare * 100,4:0} {r.densityPerKmLane,5:0.0} {r.maxMainQueue,5:0} {r.maxRampQueue,5:0} {r.maxExitQueue,5:0} {r.missedExits,5:0}";

        [CliCommand("hf_sweep", "Headless vehicles-per-minute sweep over the scenarios; prints a table and writes Reports/sweep_*.csv.")]
        public static string Sweep(
            [CliArg("lanes", "2..5")] int lanes = 3,
            [CliArg("scenarios", "Comma list, e.g. 1,2,3,4,5")] string scenarios = "1,2,3,4,5",
            [CliArg("from", "First flow, vehicles per minute")] float from = 10f,
            [CliArg("to", "Last flow (0 = lanes * 36)")] float to = 0f,
            [CliArg("points", "Number of flow values")] int points = 9,
            [CliArg("seeds", "Runs averaged per point")] int seeds = 2,
            [CliArg("minutes", "Simulated minutes per run")] float minutes = 25f,
            [CliArg("ramp_pct", "On-ramp flow as % of the mainline")] float rampPct = 25f,
            [CliArg("exit_pct", "Share of vehicles taking the exit, %")] float exitPct = 20f,
            [CliArg("spacing", "Distance between the ramps, m")] float spacing = 800f,
            [CliArg("exit_ramp_len", "Exit ramp length, m")] float exitRampLen = 200f,
            [CliArg("cycle", "Traffic light cycle, s")] float cycle = 60f,
            [CliArg("green_pct", "Green share, %")] float greenPct = 35f)
        {
            var scs = scenarios.Split(',').Select(s => int.Parse(s.Trim())).Where(i => i >= 1 && i <= 5).Select(i => (Scenario)i).ToList();
            var tpl = new SimParams { lanes = Mathf.Clamp(lanes, 2, 5), rampRatio = rampPct / 100f, exitShare = exitPct / 100f, rampSpacing = spacing, exitRampLen = exitRampLen, signalCycle = cycle, signalGreenRatio = greenPct / 100f, duration = minutes * 60f };
            float last = to > 0f ? to : tpl.lanes * 36f;
            var res = SweepRunner.Run(tpl, scs, SweepRunner.Flows(from, last, points), seeds);
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports"));
            Directory.CreateDirectory(dir);
            string csv = Path.Combine(dir, $"sweep_{System.DateTime.Now:yyyyMMdd_HHmmss}_{tpl.lanes}lanes.csv");
            File.WriteAllText(csv, SweepRunner.ToCsv(res));
            var sb = new StringBuilder();
            sb.AppendLine("sc  flow  dem  thr served speed delay  eff stop% dens qMain qRamp qExit missed");
            foreach (var r in res) sb.AppendLine(Row(r));
            sb.AppendLine("csv=" + csv);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ play mode

        static AppController PlayApp(out string error)
        {
            error = null;
            if (!Application.isPlaying) { error = "error: enter play mode first (editor_play)"; return null; }
            return App(out error);
        }

        [CliCommand("hf_sim_start", "Play mode: start (or resume) the simulation.")]
        public static string SimStart([CliArg("speed", "Speed multiplier (<=0 keeps)")] float speed = 0f)
        {
            var app = PlayApp(out var err); if (app == null) return err;
            if (speed > 0f) app.speed = speed;
            app.running = true;
            return $"running speed=x{F(app.speed)}";
        }

        [CliCommand("hf_sim_pause", "Play mode: pause.")]
        public static string SimPause() { var app = PlayApp(out var err); if (app == null) return err; app.running = false; return "paused"; }

        [CliCommand("hf_sim_reset", "Play mode: rebuild the road and restart with an empty highway (then the pre-roll).")]
        public static string SimReset() { var app = PlayApp(out var err); if (app == null) return err; app.Rebuild(); return Status(); }

        [CliCommand("hf_sim_advance", "Play mode: advance N simulated seconds immediately (independent of window focus).")]
        public static string SimAdvance([CliArg("seconds", "Simulated seconds")] float seconds = 60f)
        {
            var app = PlayApp(out var err); if (app == null) return err;
            app.AdvanceNow(seconds);
            return Status();
        }

        [CliCommand("hf_sim_status", "Play mode: live status and statistics.")]
        public static string Status()
        {
            var app = PlayApp(out var err); if (app == null) return err;
            var s = app.Sim; var r = s.Result();
            return $"{Describe(app.P)}\n t={F(s.time)}s onRoad={s.vehicles.Count} instSpeed={F(s.InstantMeanSpeedKmh)}km/h lastMinThr={s.LastMinuteCompleted} queue main/ramp={s.MainQueue}/{s.RampQueue} exitQueue={s.ExitQueueLength} light={(s.Lay.hasSignal ? (s.SignalGreen ? "green" : "red") : "-")}\n eff={F(r.efficiency * 100)}% thr={F(r.throughput)}/min speed={F(r.meanSpeedKmh)}km/h delay={F(r.meanDelaySec)}s missed={F(r.missedExits)}";
        }

        [CliCommand("hf_camera", "Camera: zone | overview | follow | orbit (yaw, pitch, distance, x).")]
        public static string Camera(
            [CliArg("mode", "zone | overview | follow")] string mode = "zone",
            [CliArg("yaw", "Yaw (degrees, -999 keeps)")] float yaw = -999f,
            [CliArg("pitch", "Pitch (degrees, -999 keeps)")] float pitch = -999f,
            [CliArg("distance", "Distance (m, 0 keeps)")] float distance = 0f,
            [CliArg("x", "Pivot x along the road (-1 keeps)")] float x = -1f)
        {
            var app = App(out var err); if (app == null) return err;
            var rig = app.cameraRig; if (rig == null) return "error: no camera rig";
            if (mode == "overview") rig.FrameOverview(app.P.length);
            else if (mode == "follow") rig.SetFollow();
            else app.FrameDefault();
            if (yaw > -900f) rig.yaw = yaw;
            if (pitch > -900f) rig.pitch = pitch;
            if (distance > 0f) { if (rig.mode == CameraRig.Mode.Follow) rig.followDistance = distance; else rig.distance = distance; }
            if (x >= 0f) rig.pivot = new Vector3(x, 0f, rig.pivot.z);
            rig.Place();
            return $"mode={rig.mode} yaw={F(rig.yaw)} pitch={F(rig.pitch)} dist={F(rig.distance)} pivot={rig.pivot}";
        }

        [CliCommand("hf_color_mode", "Vehicle colours: speed | role | lane.")]
        public static string ColorModeCmd([CliArg("mode", "speed | role | lane")] string mode = "speed")
        {
            var app = App(out var err); if (app == null) return err;
            var m = mode == "role" ? ColorMode.Role : mode == "lane" ? ColorMode.Lane : ColorMode.Speed;
            app.cars.colorMode = m;
            return "color=" + m;
        }

        [CliCommand("hf_sweep_play", "Play mode: run the sweep with the app's current road parameters and show the chart (blocks until done).")]
        public static string SweepPlay(
            [CliArg("points", "Number of flow values")] int points = 7,
            [CliArg("seeds", "Runs averaged per point")] int seeds = 1,
            [CliArg("minutes", "Simulated minutes per run")] float minutes = 20f)
        {
            var app = PlayApp(out var err); if (app == null) return err;
            app.sweep.points = points; app.sweep.seeds = seeds; app.sweep.duration = minutes * 60f;
            var res = app.RunSweepBlocking();
            return $"{res.Count} results";
        }
    }
}
