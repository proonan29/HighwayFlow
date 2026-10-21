using System;
using System.Collections.Generic;

namespace HighwayFlow
{
    public enum Scenario
    {
        Straight = 1,       // 1. plain straight highway
        OnRamp = 2,         // 2. highway + on-ramp
        ExitThenEnter = 3,  // 3. highway + exit, then on-ramp
        EnterThenExit = 4,  // 4. highway + on-ramp, then exit
        ExitSignal = 5      // 5. like 3/4 but the exit ramp ends in a traffic light
    }

    /// <summary>Lane ids used by the simulation: 0..N-1 = highway lanes (0 = rightmost), -1 = auxiliary (acceleration / deceleration) lane,
    /// -2 = on-ramp approach track, -3 = exit-ramp track.</summary>
    public static class LaneId
    {
        public const int Aux = -1, EntryRamp = -2, ExitRamp = -3;
    }

    [Serializable]
    public class SimParams
    {
        public Scenario scenario = Scenario.Straight;
        public int lanes = 3;                // 2..5
        public float length = 4000f;         // m
        public float mainPerMin = 60f;       // vehicles per minute entering the mainline (all lanes together)
        public float rampRatio = 0.25f;      // on-ramp flow = mainPerMin * rampRatio
        public float exitShare = 0.20f;      // share of vehicles that leave at the exit
        public float rampSpacing = 800f;     // m between the two ramp reference points (exit gore <-> on-ramp start)
        public float accelLen = 300f;        // acceleration lane length (m)
        public float decelLen = 300f;        // deceleration lane length (m)
        public float approachLen = 300f;     // on-ramp approach length (m)
        public float exitRampLen = 200f;     // exit ramp length after the gore (m); queue storage when signalised
        public bool exitFirst = true;        // scenario 5: exit before the on-ramp (true) or on-ramp before the exit (false)
        public float signalCycle = 60f;      // s
        public float signalGreenRatio = 0.35f;
        public float truckShare = 0.10f;
        public float desiredSpeed = 30f;     // m/s mean desired speed of cars (108 km/h)
        public int seed = 1;
        public float dt = 0.2f;
        public float warmup = 300f;          // s excluded from the efficiency window
        public float duration = 1800f;       // s total for headless runs

        public SimParams Clone() => (SimParams)MemberwiseClone();

        public float RampPerMin => mainPerMin * rampRatio;

        public Layout Resolve()
        {
            var l = new Layout();
            float mid = length * 0.5f;
            switch (scenario)
            {
                case Scenario.OnRamp:
                    l.hasEnter = true; l.enterX = mid - accelLen * 0.5f; break;
                case Scenario.ExitThenEnter:
                    l.hasExit = l.hasEnter = true; l.exitX = mid - rampSpacing * 0.5f; l.enterX = mid + rampSpacing * 0.5f; break;
                case Scenario.EnterThenExit:
                    l.hasExit = l.hasEnter = true; l.enterX = mid - rampSpacing * 0.5f; l.exitX = mid + rampSpacing * 0.5f; break;
                case Scenario.ExitSignal:
                    l.hasExit = l.hasEnter = true; l.hasSignal = true;
                    if (exitFirst) { l.exitX = mid - rampSpacing * 0.5f; l.enterX = mid + rampSpacing * 0.5f; }
                    else { l.enterX = mid - rampSpacing * 0.5f; l.exitX = mid + rampSpacing * 0.5f; }
                    break;
            }
            l.enterEnd = l.enterX + accelLen;
            l.decelStart = l.exitX - decelLen;
            l.exitRampEnd = l.exitX + exitRampLen;
            l.entryApproachStart = l.enterX - approachLen;
            l.aux = new List<float[]>();
            if (l.hasEnter) l.aux.Add(new[] { l.enterX, l.enterEnd });
            if (l.hasExit) l.aux.Add(new[] { l.decelStart, l.exitX });
            l.aux.Sort((a, b) => a[0].CompareTo(b[0]));
            for (int i = 0; i + 1 < l.aux.Count;)
            {
                if (l.aux[i + 1][0] <= l.aux[i][1] + 0.01f) { l.aux[i][1] = Math.Max(l.aux[i][1], l.aux[i + 1][1]); l.aux.RemoveAt(i + 1); }
                else i++;
            }
            return l;
        }
    }

    /// <summary>Resolved positions of the ramps along the highway (metres).</summary>
    public class Layout
    {
        public bool hasEnter, hasExit, hasSignal;
        public float enterX, enterEnd, exitX, decelStart, exitRampEnd, entryApproachStart;
        public List<float[]> aux = new List<float[]>();   // merged auxiliary-lane segments [start,end]

        public bool AuxCovers(float x) { foreach (var s in aux) if (x >= s[0] - 0.01f && x <= s[1] + 0.01f) return true; return false; }
        public float AuxEnd(float x) { foreach (var s in aux) if (x >= s[0] - 0.01f && x <= s[1] + 0.01f) return s[1]; return x; }
        public bool WeavingSegment { get { foreach (var s in aux) if (hasEnter && hasExit && s[0] <= enterX + 0.1f && s[1] >= exitX - 0.1f) return true; return false; } }
    }
}

