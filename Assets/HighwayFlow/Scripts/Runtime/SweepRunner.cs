using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HighwayFlow
{
    /// <summary>Headless batch runs: the same road is driven with increasing vehicles-per-minute and every scenario is measured.</summary>
    public static class SweepRunner
    {
        public static readonly Scenario[] AllScenarios =
            { Scenario.Straight, Scenario.OnRamp, Scenario.ExitThenEnter, Scenario.EnterThenExit, Scenario.ExitSignal };

        public static SimResult RunOne(SimParams p)
        {
            var sim = new HighwaySim(p);
            sim.Run(p.duration);
            return sim.Result();
        }

        public static float[] Flows(float from, float to, int count)
        {
            count = Math.Max(2, count);
            var f = new float[count];
            for (int i = 0; i < count; i++) f[i] = from + (to - from) * i / (count - 1);
            return f;
        }

        /// <summary>Runs scenarios x flows x seeds (in parallel) and returns the seed-averaged result per (scenario, flow), ordered by scenario then flow.</summary>
        public static List<SimResult> Run(SimParams template, IList<Scenario> scenarios, float[] flows, int seeds,
            Action<float> progress = null, CancellationToken ct = default(CancellationToken))
        {
            seeds = Math.Max(1, seeds);
            var jobs = new List<(int si, int fi, int seed)>();
            for (int s = 0; s < scenarios.Count; s++)
                for (int f = 0; f < flows.Length; f++)
                    for (int k = 0; k < seeds; k++) jobs.Add((s, f, k));
            var results = new SimResult[jobs.Count];
            int done = 0;
            var opt = new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.For(0, jobs.Count, opt, i =>
            {
                var j = jobs[i];
                var p = template.Clone();
                p.scenario = scenarios[j.si]; p.mainPerMin = flows[j.fi]; p.seed = template.seed + j.seed * 7919 + j.fi * 31;
                results[i] = RunOne(p);
                int d = Interlocked.Increment(ref done);
                progress?.Invoke((float)d / jobs.Count);
            });
            var list = new List<SimResult>();
            for (int s = 0; s < scenarios.Count; s++)
                for (int f = 0; f < flows.Length; f++)
                {
                    var group = new List<SimResult>();
                    for (int k = 0; k < seeds; k++) group.Add(results[(s * flows.Length + f) * seeds + k]);
                    list.Add(SimResult.Average(group));
                }
            return list;
        }

        public static string ToCsv(IList<SimResult> rs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("scenario,lanes,main_per_min,ramp_per_min,demand_per_min,throughput_per_min,served_ratio,mean_speed_kmh,mean_delay_s,travel_efficiency,efficiency,stopped_share,density_veh_km_lane,max_main_queue,max_ramp_queue,max_exit_queue,missed_exits,lane_changes,runs");
            var ci = CultureInfo.InvariantCulture;
            foreach (var r in rs)
                sb.AppendLine(string.Join(",", (int)r.scenario, r.lanes, r.mainPerMin.ToString("0.#", ci), r.rampPerMin.ToString("0.#", ci), r.demandPerMin.ToString("0.##", ci),
                    r.throughput.ToString("0.##", ci), r.servedRatio.ToString("0.###", ci), r.meanSpeedKmh.ToString("0.#", ci), r.meanDelaySec.ToString("0.#", ci),
                    r.travelEfficiency.ToString("0.###", ci), r.efficiency.ToString("0.###", ci), r.stoppedShare.ToString("0.###", ci), r.densityPerKmLane.ToString("0.#", ci),
                    r.maxMainQueue.ToString("0", ci), r.maxRampQueue.ToString("0", ci), r.maxExitQueue.ToString("0", ci), r.missedExits.ToString("0.#", ci), r.laneChanges.ToString("0", ci), r.runs));
            return sb.ToString();
        }
    }
}
