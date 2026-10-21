using System;

namespace HighwayFlow
{
    public sealed class Vehicle
    {
        public int id;
        public bool truck;
        public float x, v, a;                   // front bumper position (m), speed (m/s), last acceleration
        public float length = 4.5f;
        public float v0 = 30f, T = 1.5f, s0 = 2f, aMax = 1.5f, b = 2f;   // IDM parameters
        public int lane;                        // see LaneId
        public float lat;                       // lateral position in lane units (smoothed, for drawing)
        public float latRate;                   // lane units / s
        public float px, plat;                  // previous step state (render interpolation)
        public bool exitIntent;                 // wants to leave at the exit
        public bool fromRamp;                   // entered via the on-ramp
        public bool missedExit;
        public float tArrive;                   // time the vehicle wanted to enter the system
        public float tEnter;                    // time it actually entered the road
        public float lastLaneChange = -100f;
        public float stopWait;                  // seconds spent (almost) stopped while waiting to merge / exit
        public float freeTime;                  // trip time at free flow (s)
        public float x0;                        // spawn position
        public float auxEnd;                    // end of the auxiliary-lane segment the vehicle is on
        public bool done;
    }

    /// <summary>One minute of the live time series.</summary>
    public struct MinuteStat
    {
        public int minute;
        public int completed;       // vehicles that finished their trip during this minute
        public float meanSpeedKmh;  // mean speed of vehicles on the road (vehicle-steps)
        public int onRoad;          // vehicles on the road at the end of the minute
        public int queued;          // vehicles waiting to enter (main + ramp)
        public int stopped;         // vehicles below 20 km/h at the end of the minute
    }

    /// <summary>Efficiency indicators of a run (or an average of several runs).</summary>
    [Serializable]
    public class SimResult
    {
        public Scenario scenario;
        public int lanes;
        public float mainPerMin, rampPerMin;
        public float demandPerMin;      // vehicles that wanted to enter per minute (measurement window)
        public float throughput;        // trips completed per minute (measurement window)
        public float servedRatio;       // throughput / demand, capped at 1
        public float meanSpeedKmh;      // space-mean speed of vehicles on the road
        public float meanDelaySec;      // mean (actual - free-flow) trip time of completed trips, includes waiting to enter
        public float travelEfficiency;  // sum(free-flow time) / sum(actual time) of completed trips
        public float efficiency;        // travelEfficiency * servedRatio (0..1)
        public float stoppedShare;      // share of vehicle-time below 20 km/h
        public float densityPerKmLane;  // mean vehicles per km per lane on the mainline
        public float maxMainQueue, maxRampQueue, maxExitQueue;
        public float missedExits, laneChanges, completed, arrivals;
        public int runs = 1;

        public static SimResult Average(System.Collections.Generic.IList<SimResult> list)
        {
            var r = new SimResult();
            if (list == null || list.Count == 0) return r;
            r.scenario = list[0].scenario; r.lanes = list[0].lanes; r.mainPerMin = list[0].mainPerMin; r.rampPerMin = list[0].rampPerMin;
            foreach (var s in list)
            {
                r.demandPerMin += s.demandPerMin; r.throughput += s.throughput; r.servedRatio += s.servedRatio; r.meanSpeedKmh += s.meanSpeedKmh;
                r.meanDelaySec += s.meanDelaySec; r.travelEfficiency += s.travelEfficiency; r.efficiency += s.efficiency; r.stoppedShare += s.stoppedShare;
                r.densityPerKmLane += s.densityPerKmLane; r.missedExits += s.missedExits; r.laneChanges += s.laneChanges; r.completed += s.completed; r.arrivals += s.arrivals;
                r.maxMainQueue = Math.Max(r.maxMainQueue, s.maxMainQueue); r.maxRampQueue = Math.Max(r.maxRampQueue, s.maxRampQueue); r.maxExitQueue = Math.Max(r.maxExitQueue, s.maxExitQueue);
            }
            float n = list.Count;
            r.demandPerMin /= n; r.throughput /= n; r.servedRatio /= n; r.meanSpeedKmh /= n; r.meanDelaySec /= n; r.travelEfficiency /= n; r.efficiency /= n;
            r.stoppedShare /= n; r.densityPerKmLane /= n; r.missedExits /= n; r.laneChanges /= n; r.completed /= n; r.arrivals /= n;
            r.runs = list.Count;
            return r;
        }
    }
}
