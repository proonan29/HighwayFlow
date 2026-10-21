using System;
using System.Collections.Generic;

namespace HighwayFlow
{
    public sealed partial class HighwaySim
    {
        sealed class Acc
        {
            public double speedSum, sumActual, sumFree;
            public long vehSteps, stoppedSteps, mainSteps, steps;
            public int arrivals, completed, laneChanges, missedExits;
            public int maxMain, maxRamp, maxExitQ;
        }

        readonly Acc all = new Acc(), win = new Acc();
        int minCompleted, minSteps, minStopped, minOnRoad;
        double minSpeedSum;

        void Count(Action<Acc> f)
        {
            f(all);
            if (time >= P.warmup) f(win);
        }

        /// <summary>Vehicles waiting to leave: stopped on the exit ramp or in the exit lane.</summary>
        public int ExitQueueLength
        {
            get
            {
                int n = 0;
                foreach (var v in LaneList(LaneId.ExitRamp)) if (v.v < 2f) n++;
                foreach (var v in LaneList(LaneId.Aux)) if (v.exitIntent && v.v < 2f) n++;
                return n;
            }
        }

        void Accumulate()
        {
            int n = vehicles.Count, stopped = 0, mainCount = 0; double sum = 0;
            for (int i = 0; i < n; i++)
            {
                var v = vehicles[i];
                sum += v.v;
                if (v.v < 5.56f) stopped++;
                if (v.lane >= 0) mainCount++;
            }
            int exitQ = ExitQueueLength;
            AddTo(all, n, sum, stopped, mainCount, exitQ);
            if (time >= P.warmup) AddTo(win, n, sum, stopped, mainCount, exitQ);

            minSteps++; minSpeedSum += sum; minVehSteps += n;
            if (time >= (minutes.Count + 1) * 60f - 1e-4f)
            {
                minutes.Add(new MinuteStat
                {
                    minute = minutes.Count + 1,
                    completed = minCompleted,
                    meanSpeedKmh = minVehSteps > 0 ? (float)(minSpeedSum / minVehSteps * 3.6) : 0f,
                    onRoad = n, queued = mainQ.Count + rampQ.Count, stopped = stopped
                });
                minCompleted = 0; minSteps = 0; minSpeedSum = 0; minVehSteps = 0;
            }
        }

        long minVehSteps;
        void AddTo(Acc a, int n, double sum, int stopped, int mainCount, int exitQ)
        {
            a.steps++; a.vehSteps += n; a.speedSum += sum; a.stoppedSteps += stopped; a.mainSteps += mainCount;
            if (mainQ.Count > a.maxMain) a.maxMain = mainQ.Count;
            if (rampQ.Count > a.maxRamp) a.maxRamp = rampQ.Count;
            if (exitQ > a.maxExitQ) a.maxExitQ = exitQ;
        }

        /// <summary>Efficiency indicators over the measurement window (after warm-up), or over the whole run if it is shorter than the warm-up.</summary>
        public SimResult Result()
        {
            bool useWin = time > P.warmup + 30f;
            var a = useWin ? win : all;
            float sec = useWin ? time - P.warmup : time;
            float min = Math.Max(sec / 60f, 1e-3f);
            var r = new SimResult { scenario = P.scenario, lanes = P.lanes, mainPerMin = P.mainPerMin, rampPerMin = Lay.hasEnter ? P.RampPerMin : 0f };
            r.arrivals = a.arrivals; r.completed = a.completed;
            r.demandPerMin = a.arrivals / min;
            r.throughput = a.completed / min;
            r.servedRatio = r.demandPerMin > 0f ? Math.Min(1f, r.throughput / r.demandPerMin) : 1f;
            r.meanSpeedKmh = a.vehSteps > 0 ? (float)(a.speedSum / a.vehSteps * 3.6) : 0f;
            r.meanDelaySec = a.completed > 0 ? (float)((a.sumActual - a.sumFree) / a.completed) : 0f;
            r.travelEfficiency = a.sumActual > 0 ? (float)Math.Min(1.0, a.sumFree / a.sumActual) : 1f;
            r.efficiency = r.travelEfficiency * r.servedRatio;
            r.stoppedShare = a.vehSteps > 0 ? (float)a.stoppedSteps / a.vehSteps : 0f;
            r.densityPerKmLane = a.steps > 0 ? (float)a.mainSteps / a.steps / (P.length / 1000f) / P.lanes : 0f;
            r.maxMainQueue = a.maxMain; r.maxRampQueue = a.maxRamp; r.maxExitQueue = a.maxExitQ;
            r.missedExits = a.missedExits; r.laneChanges = a.laneChanges;
            return r;
        }

        /// <summary>Vehicles that finished their trip during the last full minute (live display).</summary>
        public int LastMinuteCompleted => minutes.Count > 0 ? minutes[minutes.Count - 1].completed : 0;

        public float InstantMeanSpeedKmh
        {
            get
            {
                if (vehicles.Count == 0) return 0f;
                double s = 0; foreach (var v in vehicles) s += v.v;
                return (float)(s / vehicles.Count * 3.6);
            }
        }
    }
}
