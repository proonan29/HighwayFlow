using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace HighwayFlow
{
    [Serializable]
    public class SweepSettings
    {
        public float from = 10f, to = 0f;     // to <= 0: automatic (lanes * 36 vehicles/min)
        public int points = 9, seeds = 2;
        public float duration = 1500f, warmup = 300f;
        public bool[] scenarios = { true, true, true, true, true };
    }

    /// <summary>Owns the simulation, builds the 3D road, advances time in play mode and runs the vehicles-per-minute sweeps.</summary>
    public class AppController : MonoBehaviour
    {
        public SimParams P = new SimParams();
        public SweepSettings sweep = new SweepSettings();
        public VehicleRenderer cars;
        public CameraRig cameraRig;
        public float speed = 5f;
        public float preRoll = 120f;       // seconds simulated instantly when a run (re)starts, so the road is not empty
        public bool running;

        public HighwaySim Sim { get; private set; }
        public RoadGeometry Geo { get; private set; }
        public Layout Lay => Sim != null ? Sim.Lay : null;

        public event Action Rebuilt;
        public event Action SweepFinished;

        Transform roadRoot;
        TrafficLightView light;
        float accum;
        int followId = -1;

        // sweep state (written by a worker thread)
        public List<SimResult> SweepResults { get; private set; }
        public SimParams SweepParams { get; private set; }
        public bool SweepRunning { get; private set; }
        public float SweepProgress;
        public string SweepCsvPath { get; private set; }
        volatile bool sweepDone;
        List<SimResult> sweepPending;
        string sweepCsvPending;

        void Start() { if (Application.isPlaying) Rebuild(); }

        public void Normalize()
        {
            P.lanes = Mathf.Clamp(P.lanes, 2, 5);
            P.mainPerMin = Mathf.Max(0f, P.mainPerMin);
            P.rampRatio = Mathf.Clamp(P.rampRatio, 0f, 1f);
            P.exitShare = Mathf.Clamp01(P.exitShare);
            P.rampSpacing = Mathf.Clamp(P.rampSpacing, 200f, 2000f);
            P.signalCycle = Mathf.Clamp(P.signalCycle, 10f, 300f);
            P.signalGreenRatio = Mathf.Clamp(P.signalGreenRatio, 0.05f, 0.95f);
            P.exitRampLen = Mathf.Clamp(P.exitRampLen, 60f, 600f);
        }

        /// <summary>Creates a fresh simulation + road for the current parameters.</summary>
        public void Rebuild()
        {
            Normalize();
            if (roadRoot != null) { if (Application.isPlaying) Destroy(roadRoot.gameObject); else DestroyImmediate(roadRoot.gameObject); }
            Sim = new HighwaySim(P);
            Geo = new RoadGeometry(P, Sim.Lay);
            roadRoot = RoadBuilder.Build(transform, P, Sim.Lay, Geo, out light);
            if (cars == null) cars = GetComponentInChildren<VehicleRenderer>();
            if (cars != null) cars.Clear();
            accum = 0f; followId = -1;
            if (preRoll > 0f && Application.isPlaying) Sim.Run(preRoll);
            if (cameraRig != null)
            {
                cameraRig.roadLength = P.length;
                cameraRig.followProvider = FollowPosition;
                FrameDefault();
            }
            DrawNow(1f);
            Rebuilt?.Invoke();
        }

        public void FrameDefault()
        {
            if (cameraRig == null || Lay == null) return;
            float cx = P.length * 0.5f;
            if (Lay.hasEnter || Lay.hasExit)
            {
                // frame the first ramp structure (both ramps if they are close together)
                float first = Lay.hasEnter && Lay.hasExit ? Mathf.Min(Lay.enterX, Lay.exitX) : (Lay.hasEnter ? Lay.enterX : Lay.exitX);
                float second = Lay.hasEnter && Lay.hasExit ? Mathf.Max(Lay.enterX, Lay.exitX) : first;
                cx = second - first <= 450f ? (first + second) * 0.5f : first + 120f;
            }
            cameraRig.FrameZone(cx, Geo.LaneZ(-1f) - 4f);
        }

        // ------------------------------------------------------------------ time

        void Update()
        {
            if (sweepDone) { sweepDone = false; FinishSweep(); }
            if (Sim == null) return;
            if (running) Advance(Time.deltaTime * speed);
            DrawNow(accum / P.dt);
        }

        /// <summary>Advance by simulated seconds (frame-rate independent, bounded per frame).</summary>
        public void Advance(float simSeconds)
        {
            float dt = P.dt;
            accum += simSeconds;
            int steps = Mathf.FloorToInt(accum / dt);
            int maxSteps = 400;
            if (steps > maxSteps) { steps = maxSteps; accum = steps * dt; }
            for (int i = 0; i < steps; i++) Sim.Step();
            accum -= steps * dt;
        }

        /// <summary>Immediately simulate N seconds (no frame dependency), used by the CLI.</summary>
        public void AdvanceNow(float seconds)
        {
            Sim.Run(seconds);
            accum = 0f;
            DrawNow(1f);
        }

        void DrawNow(float alpha)
        {
            if (cars != null && Sim != null) cars.Draw(Sim, Geo, Mathf.Clamp01(alpha));
            if (light != null && Sim != null) light.Set(Sim.SignalGreen);
        }

        public void SetRunning(bool r) { running = r; }

        // ------------------------------------------------------------------ follow

        Vector3? FollowPosition()
        {
            if (Sim == null) return null;
            Vehicle target = null;
            foreach (var v in Sim.vehicles) if (v.id == followId) { target = v; break; }
            if (target == null || target.x > P.length - 150f)
            {
                // pick a vehicle that is in the middle of the road (prefer ramp / exit users)
                Vehicle best = null; float bestScore = float.MinValue;
                foreach (var v in Sim.vehicles)
                {
                    float score = (v.fromRamp || v.exitIntent ? 100f : 0f) - Mathf.Abs(v.x - P.length * 0.35f) * 0.05f + (v.id * 0.37f) % 3f;
                    if (v.x < 100f || v.x > P.length - 400f) continue;
                    if (score > bestScore) { bestScore = score; best = v; }
                }
                target = best;
                followId = target != null ? target.id : -1;
            }
            if (target == null) return null;
            return Geo.Position(target.lane, target.x - target.length * 0.5f, target.lat);
        }

        // ------------------------------------------------------------------ sweep

        public float[] SweepFlows()
        {
            float to = sweep.to > 0f ? sweep.to : P.lanes * 36f;
            return SweepRunner.Flows(Mathf.Max(1f, sweep.from), Mathf.Max(sweep.from + 1f, to), Mathf.Clamp(sweep.points, 2, 40));
        }

        public void StartSweep()
        {
            if (SweepRunning) return;
            Normalize();
            var tpl = P.Clone();
            tpl.duration = sweep.duration; tpl.warmup = Mathf.Min(sweep.warmup, sweep.duration * 0.5f);
            var scs = new List<Scenario>();
            for (int i = 0; i < SweepRunner.AllScenarios.Length; i++) if (sweep.scenarios == null || i >= sweep.scenarios.Length || sweep.scenarios[i]) scs.Add(SweepRunner.AllScenarios[i]);
            if (scs.Count == 0) return;
            var flows = SweepFlows();
            int seeds = Mathf.Max(1, sweep.seeds);
            SweepRunning = true; SweepProgress = 0f; SweepParams = tpl;
            Task.Run(() =>
            {
                List<SimResult> res = null;
                try { res = SweepRunner.Run(tpl, scs, flows, seeds, p => SweepProgress = p); }
                catch (Exception e) { Debug.LogError("sweep failed: " + e.Message); }
                sweepPending = res;
                sweepDone = true;
            });
        }

        void FinishSweep()
        {
            SweepRunning = false;
            if (sweepPending != null)
            {
                SweepResults = sweepPending; sweepPending = null;
                try
                {
                    string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports"));
                    Directory.CreateDirectory(dir);
                    SweepCsvPath = Path.Combine(dir, $"sweep_{DateTime.Now:yyyyMMdd_HHmmss}_{P.lanes}lanes.csv");
                    File.WriteAllText(SweepCsvPath, SweepRunner.ToCsv(SweepResults));
                }
                catch (Exception e) { Debug.LogWarning("could not write CSV: " + e.Message); }
            }
            SweepFinished?.Invoke();
        }

        /// <summary>Synchronous sweep (CLI / tests). Blocks the calling thread.</summary>
        public List<SimResult> RunSweepBlocking()
        {
            Normalize();
            var tpl = P.Clone(); tpl.duration = sweep.duration; tpl.warmup = Mathf.Min(sweep.warmup, sweep.duration * 0.5f);
            var scs = new List<Scenario>();
            for (int i = 0; i < SweepRunner.AllScenarios.Length; i++) if (sweep.scenarios == null || i >= sweep.scenarios.Length || sweep.scenarios[i]) scs.Add(SweepRunner.AllScenarios[i]);
            SweepParams = tpl;
            SweepResults = SweepRunner.Run(tpl, scs, SweepFlows(), Mathf.Max(1, sweep.seeds));
            SweepFinished?.Invoke();
            return SweepResults;
        }
    }
}
