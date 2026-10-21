using UnityEngine;

namespace HighwayFlow
{
    /// <summary>Maps simulation coordinates (lane, x along the road, lateral lane units) to world space.
    /// World: +X = driving direction, +Z = left, lane 0 is the rightmost highway lane, the auxiliary lane is to its right.</summary>
    public class RoadGeometry
    {
        public const float LaneW = 3.6f;
        public const float RampOffsetMax = 28f;     // lateral distance of a ramp far away from the highway (gentle S-curve)
        public readonly SimParams P;
        public readonly Layout Lay;

        public RoadGeometry(SimParams p, Layout lay) { P = p; Lay = lay; }

        public float LaneZ(float lat) => (lat - (P.lanes - 1) * 0.5f) * LaneW;

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        static float SmoothDeriv(float t) { t = Mathf.Clamp01(t); return 6f * t * (1f - t); }

        public float EntryOffset(float x) => RampOffsetMax * Smooth((Lay.enterX - x) / Mathf.Max(P.approachLen, 1f));
        public float ExitOffset(float x) => RampOffsetMax * Smooth((x - Lay.exitX) / Mathf.Max(P.exitRampLen, 1f));

        /// <summary>d(z)/d(x) of the road centre line of the ramp tracks (0 elsewhere).</summary>
        public float Slope(int lane, float x)
        {
            if (lane == LaneId.EntryRamp) return RampOffsetMax * SmoothDeriv((Lay.enterX - x) / Mathf.Max(P.approachLen, 1f)) / Mathf.Max(P.approachLen, 1f);
            if (lane == LaneId.ExitRamp) return -RampOffsetMax * SmoothDeriv((x - Lay.exitX) / Mathf.Max(P.exitRampLen, 1f)) / Mathf.Max(P.exitRampLen, 1f);
            return 0f;
        }

        public Vector3 Position(int lane, float x, float lat, float y = 0f)
        {
            float z = LaneZ(lat);
            if (lane == LaneId.EntryRamp) z -= EntryOffset(x);
            else if (lane == LaneId.ExitRamp) z -= ExitOffset(x);
            return new Vector3(x, y, z);
        }

        /// <summary>Yaw (degrees, Unity Y rotation) for a vehicle moving at speed v with lateral rate latRate.</summary>
        public float YawDeg(int lane, float x, float v, float latRate)
        {
            float dzdt = LaneW * latRate + Slope(lane, x) * v;
            return -Mathf.Atan2(dzdt, Mathf.Max(v, 2f)) * Mathf.Rad2Deg;
        }
    }
}
