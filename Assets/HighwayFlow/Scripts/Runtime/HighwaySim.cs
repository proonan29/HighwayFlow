using System;
using System.Collections.Generic;

namespace HighwayFlow
{
    /// <summary>Deterministic microscopic traffic simulation: IDM car following + MOBIL lane changing,
    /// auxiliary lanes for the on-ramp / exit, optional traffic light at the end of the exit ramp.
    /// Pure C# (no UnityEngine) so it can run headless and on worker threads.</summary>
    public sealed partial class HighwaySim
    {
        public readonly SimParams P;
        public readonly Layout Lay;
        public float time;
        public readonly List<Vehicle> vehicles = new List<Vehicle>();
        public readonly List<MinuteStat> minutes = new List<MinuteStat>();

        readonly List<Vehicle>[] lists;       // index = lane + 3, each sorted by x ascending
        readonly System.Random rng;
        readonly Queue<Vehicle> mainQ = new Queue<Vehicle>(), rampQ = new Queue<Vehicle>();
        double nextMain = double.PositiveInfinity, nextRamp = double.PositiveInfinity;
        int nextId, stepCount;

        const float EntryCap = 20f, DecelCap = 24f, ExitRampCap = 16f, MinGap = 0.3f;

        public int MainQueue => mainQ.Count;
        public int RampQueue => rampQ.Count;
        public bool SignalGreen => !Lay.hasSignal || (time % P.signalCycle) < P.signalCycle * P.signalGreenRatio;
        public List<Vehicle> LaneList(int lane) => lists[lane + 3];

        public HighwaySim(SimParams p)
        {
            P = p;
            Lay = p.Resolve();
            rng = new System.Random(p.seed);
            lists = new List<Vehicle>[p.lanes + 3];
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<Vehicle>();
        }

        // ------------------------------------------------------------------ random helpers

        double Exp(double mean) => -Math.Log(1.0 - rng.NextDouble()) * mean;
        float Gauss() { double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble(); return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)); }

        static int UpperBound(List<Vehicle> l, float x)
        {
            int lo = 0, hi = l.Count;
            while (lo < hi) { int m = (lo + hi) >> 1; if (l[m].x > x) hi = m; else lo = m + 1; }
            return lo;
        }

        int MaxLane(Vehicle v) => v.truck && P.lanes > 2 ? P.lanes - 2 : P.lanes - 1;

        // ------------------------------------------------------------------ vehicles

        Vehicle MakeVehicle(bool ramp)
        {
            var v = new Vehicle { id = nextId++, fromRamp = ramp, tArrive = time };
            v.truck = rng.NextDouble() < P.truckShare;
            if (v.truck)
            {
                v.length = 12f; v.v0 = Clamp(22f + Gauss() * 1.2f, 19f, 25f); v.aMax = 0.9f; v.T = 1.8f; v.b = 1.8f;
            }
            else
            {
                v.length = 4.5f; v.v0 = Clamp(P.desiredSpeed + Gauss() * 3f, P.desiredSpeed - 6f, P.desiredSpeed + 6f);
                v.aMax = 1.3f + (float)rng.NextDouble() * 0.4f; v.T = 1.3f + (float)rng.NextDouble() * 0.4f; v.b = 2.0f;
            }
            v.s0 = 2f;
            v.exitIntent = Lay.hasExit && rng.NextDouble() < P.exitShare && (!ramp || Lay.exitX > Lay.enterX + 50f);
            v.freeTime = FreeTime(v);
            return v;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        float FreeTime(Vehicle v)
        {
            float cur = v.fromRamp ? Lay.enterX : 0f, t = 0f;
            if (v.fromRamp) t += P.approachLen / Math.Min(v.v0, EntryCap);
            if (v.exitIntent)
            {
                float ds = Math.Max(cur, Lay.decelStart);
                t += (ds - cur) / v.v0;
                t += (Lay.exitX - ds) / Math.Min(v.v0, DecelCap);
                t += P.exitRampLen / Math.Min(v.v0, ExitRampCap);
            }
            else t += (P.length - cur) / v.v0;
            return t;
        }

        void SpawnArrivals()
        {
            double mainRate = P.mainPerMin, rampRate = Lay.hasEnter ? P.RampPerMin : 0.0;
            if (double.IsPositiveInfinity(nextMain) && mainRate > 0) nextMain = time + Exp(60.0 / mainRate);
            if (double.IsPositiveInfinity(nextRamp) && rampRate > 0) nextRamp = time + Exp(60.0 / rampRate);
            while (time >= nextMain)
            {
                mainQ.Enqueue(MakeVehicle(false)); Count(a => a.arrivals++);
                nextMain = mainRate > 0 ? nextMain + Exp(60.0 / mainRate) : double.PositiveInfinity;
            }
            while (time >= nextRamp)
            {
                rampQ.Enqueue(MakeVehicle(true)); Count(a => a.arrivals++);
                nextRamp = rampRate > 0 ? nextRamp + Exp(60.0 / rampRate) : double.PositiveInfinity;
            }
        }

        void TryInsert()
        {
            // mainline: choose the lane with the largest free gap at x = 0
            while (mainQ.Count > 0)
            {
                var v = mainQ.Peek();
                int best = -1; float bestGap = -1f, bestSpeed = 0f;
                for (int lane = 0; lane <= MaxLane(v); lane++)
                {
                    var l = lists[lane + 3];
                    float gap = 1e6f, vIn = v.v0;
                    if (l.Count > 0) { var f = l[0]; gap = f.x - f.length; vIn = Math.Min(v.v0, f.v + 2f); }
                    float need = v.s0 + vIn * v.T * 0.75f + 1f;
                    if (gap >= need && gap > bestGap) { best = lane; bestGap = gap; bestSpeed = vIn; }
                }
                if (best < 0) break;
                mainQ.Dequeue();
                v.x = 0f; v.v = bestSpeed; v.lane = best; v.lat = best; v.px = v.x; v.plat = v.lat; v.tEnter = time; v.x0 = 0f;
                lists[best + 3].Insert(0, v); vehicles.Add(v);
            }
            // on-ramp: enters the approach track
            while (rampQ.Count > 0 && Lay.hasEnter)
            {
                var v = rampQ.Peek();
                var l = lists[LaneId.EntryRamp + 3];
                float gap = 1e6f, vIn = Math.Min(v.v0, 18f);
                if (l.Count > 0) { var f = l[0]; gap = f.x - f.length - Lay.entryApproachStart; vIn = Math.Min(vIn, f.v + 2f); }
                if (gap < v.s0 + vIn * v.T * 0.75f + 1f) break;
                rampQ.Dequeue();
                v.x = Lay.entryApproachStart; v.v = vIn; v.lane = LaneId.EntryRamp; v.lat = -1f; v.px = v.x; v.plat = v.lat; v.tEnter = time; v.x0 = v.x;
                l.Insert(0, v); vehicles.Add(v);
            }
        }

        // ------------------------------------------------------------------ car following (IDM)

        static float Idm(Vehicle v, float gap, float vLead, float v0e)
        {
            if (gap < 0.1f) gap = 0.1f;
            float dv = v.v - vLead;
            float sStar = v.s0 + Math.Max(0f, v.v * v.T + v.v * dv / (2f * MathF.Sqrt(v.aMax * v.b)));
            float r = v.v / v0e, r2 = r * r, q = sStar / gap;
            float acc = v.aMax * (1f - r2 * r2 - q * q);
            return acc < -9f ? -9f : acc;
        }

        static float Free(Vehicle v, float v0e)
        {
            float r = v.v / v0e, r2 = r * r;
            float acc = v.aMax * (1f - r2 * r2);
            return acc < -9f ? -9f : acc;
        }

        /// <summary>Desired speed of a vehicle if it were in the given lane (zone speed caps, slowing for the exit gore).</summary>
        float EffV0(Vehicle v, int lane)
        {
            float v0 = v.v0;
            switch (lane)
            {
                case LaneId.EntryRamp: v0 = Math.Min(v0, EntryCap); break;
                case LaneId.ExitRamp: v0 = Math.Min(v0, ExitRampCap); break;
                case LaneId.Aux: { float ae = lane == v.lane ? v.auxEnd : Lay.AuxEnd(v.x); if (v.exitIntent && Math.Abs(ae - Lay.exitX) < 0.2f) v0 = Math.Min(v0, DecelCap); } break;
                default:
                    if (v.exitIntent && Lay.hasExit && v.x < Lay.exitX)
                    {
                        float dEx = Lay.exitX - v.x;
                        if (lane == 0 && v.x >= Lay.decelStart)
                            v0 = Math.Min(v0, 8f + MathF.Sqrt(2f * 2f * Math.Max(0f, dEx - 20f)));
                        else if (lane > 0 && dEx < 800f) v0 *= 0.8f;
                    }
                    break;
            }
            return Math.Max(v0, 1f);
        }

        bool StaysInAux(Vehicle v) => v.exitIntent && Math.Abs(v.auxEnd - Lay.exitX) < 0.2f;

        float Accel(Vehicle v, List<Vehicle> l, int i)
        {
            float v0e = EffV0(v, v.lane);
            Vehicle lead = i + 1 < l.Count ? l[i + 1] : null;
            float acc = lead != null ? Idm(v, lead.x - lead.length - v.x, lead.v, v0e) : Free(v, v0e);
            switch (v.lane)
            {
                case LaneId.EntryRamp:
                    if (Lay.hasEnter)
                    {
                        var aux = lists[LaneId.Aux + 3]; int k = UpperBound(aux, v.x);
                        if (k < aux.Count) { var q = aux[k]; acc = Math.Min(acc, Idm(v, q.x - q.length - v.x, q.v, v0e)); }
                    }
                    break;
                case LaneId.Aux:
                    if (StaysInAux(v))
                    {
                        var ex = lists[LaneId.ExitRamp + 3];
                        if (ex.Count > 0) { var q = ex[0]; acc = Math.Min(acc, Idm(v, q.x - q.length - v.x, q.v, v0e)); }
                    }
                    else
                    {
                        float end = v.auxEnd;
                        acc = Math.Min(acc, Idm(v, end - v.x, 0f, v0e));          // the lane ends: virtual stopped vehicle
                        var l0 = lists[3]; int k = UpperBound(l0, v.x);           // follow the vehicle we will merge behind
                        if (k < l0.Count) { var q = l0[k]; float g = q.x - q.length - v.x; if (g > 0f) acc = Math.Min(acc, Math.Max(Idm(v, g, q.v, v0e), -3f)); }
                    }
                    break;
                case LaneId.ExitRamp:
                    if (Lay.hasSignal && !SignalGreen)
                    {
                        float d = Lay.exitRampEnd - v.x;
                        if (d > 0f && d > v.v * v.v / 12f) acc = Math.Min(acc, Idm(v, d, 0f, v0e));    // red: stop at the line unless too close to stop
                    }
                    break;
            }
            return acc;
        }

        // ------------------------------------------------------------------ lane changes (MOBIL)

        bool Evaluate(Vehicle v, int target, float bSafe, float politeness, out float incentive)
        {
            incentive = 0f;
            var tl = lists[target + 3];
            int ub = UpperBound(tl, v.x);
            Vehicle lead = ub < tl.Count ? tl[ub] : null, foll = ub > 0 ? tl[ub - 1] : null;
            float v0e = EffV0(v, target);
            float gapL = lead != null ? lead.x - lead.length - v.x : 1e6f;
            float gapF = foll != null ? v.x - v.length - foll.x : 1e6f;
            if (gapL < 1.5f || gapF < 1.5f) return false;
            float aNew = lead != null ? Idm(v, gapL, lead.v, v0e) : Free(v, v0e);
            if (target == LaneId.Aux && v.exitIntent)
            {
                var ex = lists[LaneId.ExitRamp + 3];
                if (ex.Count > 0) aNew = Math.Min(aNew, Idm(v, ex[0].x - ex[0].length - v.x, ex[0].v, v0e));
            }
            if (aNew < -bSafe) return false;
            float dFoll = 0f;
            if (foll != null)
            {
                float aFollNew = Idm(foll, gapF, v.v, EffV0(foll, target));
                if (aFollNew < -bSafe) return false;
                dFoll = aFollNew - foll.a;
            }
            float dOld = 0f;
            var ol = lists[v.lane + 3]; int oi = ol.IndexOf(v);
            if (oi > 0)
            {
                var of = ol[oi - 1]; var ole = oi + 1 < ol.Count ? ol[oi + 1] : null;
                float ov0 = EffV0(of, of.lane);
                float aOldNew = ole != null ? Idm(of, ole.x - ole.length - of.x, ole.v, ov0) : Free(of, ov0);
                dOld = aOldNew - of.a;
            }
            incentive = (aNew - v.a) + politeness * (dFoll + dOld);
            return true;
        }

        void MoveLane(Vehicle v, int target)
        {
            var ol = lists[v.lane + 3]; ol.RemoveAt(ol.IndexOf(v));
            var nl = lists[target + 3]; nl.Insert(UpperBound(nl, v.x), v);
            if (target == LaneId.Aux) v.auxEnd = Lay.AuxEnd(v.x);
            v.lane = target; v.lastLaneChange = time;
            Count(a => a.laneChanges++);
        }

        void ConsiderLaneChange(Vehicle v)
        {
            if (v.lane <= LaneId.EntryRamp) return;
            if (time - v.lastLaneChange < 2.5f) return;
            int n = P.lanes;
            float inc;
            if (v.lane == LaneId.Aux)
            {
                if (StaysInAux(v)) return;
                // on-ramp vehicle: merge into lane 0 before the auxiliary lane ends
                float remain = v.auxEnd - v.x;
                float urgency = Clamp(1f - remain / 150f, 0f, 1f);
                float bSafe = 3f + 5f * urgency + (v.stopWait > 8f ? 4f : 0f);
                if (Evaluate(v, 0, bSafe, 0f, out inc) && inc + 0.5f + 6f * urgency > 0.1f) MoveLane(v, 0);
                return;
            }
            bool wantsExit = v.exitIntent && Lay.hasExit && v.x < Lay.exitX;
            if (wantsExit)
            {
                float dEx = Lay.exitX - v.x;
                if (v.lane == 0)
                {
                    if (Lay.AuxCovers(v.x) && Math.Abs(Lay.AuxEnd(v.x) - Lay.exitX) < 0.2f)
                    {
                        float u = Clamp(1f - dEx / Math.Max(P.decelLen, 1f), 0f, 1f);
                        if (Evaluate(v, LaneId.Aux, 3f + 4f * u + (v.stopWait > 5f ? 3f : 0f), 0f, out inc) && inc + 2f + 6f * u > 0.1f) MoveLane(v, LaneId.Aux);
                    }
                    return;
                }
                const float plan = 1800f;
                float uu = Clamp(1f - (dEx - 100f * v.lane) / plan, 0f, 1f);
                if (uu > 0f && Evaluate(v, v.lane - 1, 3f + 4f * uu, 0.1f, out inc) && inc + 0.3f + 6f * uu > 0.1f) { MoveLane(v, v.lane - 1); return; }
                if (uu > 0f) return;      // mandatory zone: do not move left
            }
            // discretionary MOBIL, keep-right bias
            const float politeness = 0.25f, threshold = 0.2f, bSafeStd = 3.5f, biasRight = 0.12f;
            int bestLane = -99; float bestInc = threshold;
            if (v.lane > 0 && Evaluate(v, v.lane - 1, bSafeStd, politeness, out inc) && inc + biasRight > bestInc) { bestInc = inc + biasRight; bestLane = v.lane - 1; }
            if (v.lane < MaxLane(v) && !wantsExit && Evaluate(v, v.lane + 1, bSafeStd, politeness, out inc) && inc - biasRight > bestInc) { bestInc = inc - biasRight; bestLane = v.lane + 1; }
            if (bestLane != -99) MoveLane(v, bestLane);
        }

        // ------------------------------------------------------------------ time step

        public void Step()
        {
            float dt = P.dt;
            time += dt; stepCount++;
            SpawnArrivals();
            TryInsert();

            for (int li = 0; li < lists.Length; li++)
            {
                var l = lists[li];
                for (int i = 0; i < l.Count; i++) l[i].a = Accel(l[i], l, i);
            }

            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if ((stepCount + v.id) % 3 == 0) ConsiderLaneChange(v);
            }

            // integrate
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                v.px = v.x; v.plat = v.lat;
                float nv = v.v + v.a * dt; if (nv < 0f) nv = 0f;
                v.x += 0.5f * (v.v + nv) * dt; v.v = nv;
                // waiting bookkeeping
                bool waiting = (v.lane == LaneId.Aux && !StaysInAux(v) && v.auxEnd - v.x < 40f) || (v.lane >= 0 && v.exitIntent && v.x > Lay.exitX - 150f && v.x < Lay.exitX);
                if (waiting && v.v < 2f) v.stopWait += dt; else if (v.v > 3f) v.stopWait = 0f;
                if (v.lane >= 0 && v.exitIntent && v.stopWait > 25f) { v.exitIntent = false; v.missedExit = true; v.stopWait = 0f; Count(a => a.missedExits++); }
                // visual lateral position
                float tgt = v.lane >= 0 ? v.lane : -1f;
                float d = tgt - v.lat, mx = dt / 2.5f;
                float dl = d > mx ? mx : (d < -mx ? -mx : d);
                v.lat += dl; v.latRate = dl / dt;
            }

            for (int li = 0; li < lists.Length; li++)
            {
                var l = lists[li];
                for (int i = 1; i < l.Count; i++)          // insertion sort (nearly sorted)
                {
                    var t = l[i]; int j = i - 1;
                    while (j >= 0 && l[j].x > t.x) { l[j + 1] = l[j]; j--; }
                    l[j + 1] = t;
                }
                for (int i = l.Count - 2; i >= 0; i--)     // never overlap the leader
                {
                    var f = l[i]; var le = l[i + 1];
                    float maxX = le.x - le.length - MinGap;
                    if (f.x > maxX) { f.x = maxX; if (f.v > le.v) f.v = le.v; }
                }
            }

            // transitions and removals
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                switch (v.lane)
                {
                    case LaneId.EntryRamp:
                        if (v.x >= Lay.enterX) MoveLaneKeep(v, LaneId.Aux);
                        break;
                    case LaneId.Aux:
                        if (StaysInAux(v) && v.x >= Lay.exitX - 0.01f) MoveLaneKeep(v, LaneId.ExitRamp);
                        else if (!StaysInAux(v)) { float end = v.auxEnd - 0.5f; if (v.x > end) { v.x = end; v.v = 0f; } }
                        break;
                    case LaneId.ExitRamp:
                        {
                            float end = Lay.hasSignal ? Lay.exitRampEnd + 3f : Lay.exitRampEnd;
                            if (v.x >= end) Complete(v);
                        }
                        break;
                    default:
                        if (v.exitIntent && Lay.hasExit && v.x >= Lay.exitX) { v.exitIntent = false; v.missedExit = true; Count(a => a.missedExits++); }
                        if (v.x >= P.length) Complete(v);
                        break;
                }
            }
            if (removedAny)
            {
                vehicles.RemoveAll(v => v.done);
                for (int li = 0; li < lists.Length; li++) lists[li].RemoveAll(v => v.done);
                removedAny = false;
            }

            Accumulate();
        }

        bool removedAny;

        void MoveLaneKeep(Vehicle v, int target)
        {
            var ol = lists[v.lane + 3]; ol.RemoveAt(ol.IndexOf(v));
            var nl = lists[target + 3]; nl.Insert(UpperBound(nl, v.x), v);
            if (target == LaneId.Aux) v.auxEnd = Lay.AuxEnd(Lay.enterX + 0.1f);
            v.lane = target;
        }

        void Complete(Vehicle v)
        {
            if (v.done) return;
            v.done = true; removedAny = true;
            double actual = time - v.tArrive;
            Count(a => { a.completed++; a.sumActual += actual; a.sumFree += v.freeTime; });
            minCompleted++;
        }

        public void Run(float seconds)
        {
            int steps = (int)Math.Round(seconds / P.dt);
            for (int i = 0; i < steps; i++) Step();
        }
    }
}

