using System.Collections.Generic;
using UnityEngine;

namespace HighwayFlow
{
    /// <summary>Swaps the lamps of the exit-ramp traffic light.</summary>
    public class TrafficLightView : MonoBehaviour
    {
        public Renderer red, green;
        public Material redOn, redOff, greenOn, greenOff;
        bool? last;
        public void Set(bool isGreen)
        {
            if (last == isGreen) return;
            last = isGreen;
            if (red != null) red.sharedMaterial = isGreen ? redOff : redOn;
            if (green != null) green.sharedMaterial = isGreen ? greenOn : greenOff;
        }
    }

    /// <summary>Builds the 3D road: carriageway, lane markings, auxiliary lanes, ramps, signs and the exit-ramp traffic light.</summary>
    public static class RoadBuilder
    {
        static readonly Color Asphalt = new Color(0.17f, 0.17f, 0.18f), Line = new Color(0.92f, 0.92f, 0.88f), Ground = new Color(0.34f, 0.42f, 0.27f);

        public static Transform Build(Transform parent, SimParams p, Layout lay, RoadGeometry geo, out TrafficLightView light)
        {
            light = null;
            var root = new GameObject("Road").transform;
            root.SetParent(parent, false);
            var asphalt = MeshUtil.Lit("Asphalt", Asphalt, 0.08f);
            var line = MeshUtil.Lit("Line", Line, 0.1f);
            float L = p.length, N = p.lanes, W = RoadGeometry.LaneW;

            MeshUtil.Cube("Ground", root, new Vector3(L * 0.5f, -0.13f, 0f), new Vector3(L + 600f, 0.2f, 700f), MeshUtil.Lit("Ground", Ground, 0.02f), false);
            float zCenter = geo.LaneZ((N - 1) * 0.5f);
            MeshUtil.Cube("Carriageway", root, new Vector3(L * 0.5f, -0.045f, zCenter), new Vector3(L, 0.1f, N * W), asphalt, false);

            // ---- lane markings (one combined mesh)
            var mk = new MeshUtil();
            const float dash = 3f, period = 12f, lw = 0.16f, y = 0.012f;
            for (int i = 0; i + 1 < p.lanes; i++)
            {
                float z = geo.LaneZ(i + 0.5f);
                for (float x = 0f; x < L; x += period) mk.Flat(x, Mathf.Min(x + dash, L), z - lw * 0.5f, z + lw * 0.5f, y);
            }
            float zl = geo.LaneZ(N - 0.5f) - 0.0f, zr = geo.LaneZ(-0.5f);
            mk.Flat(0f, L, zl - lw * 0.5f - 0.0f, zl + lw * 0.5f, y);
            // right edge line: solid except along auxiliary lanes (dashed there)
            var gaps = new List<float[]>();
            foreach (var s in lay.aux) gaps.Add(new[] { s[0], s[1] });
            float cur = 0f;
            foreach (var g in gaps) { if (g[0] > cur) mk.Flat(cur, g[0], zr - lw * 0.5f, zr + lw * 0.5f, y); cur = Mathf.Max(cur, g[1]); }
            if (cur < L) mk.Flat(cur, L, zr - lw * 0.5f, zr + lw * 0.5f, y);
            foreach (var g in gaps) for (float x = g[0]; x < g[1]; x += period) mk.Flat(x, Mathf.Min(x + dash, g[1]), zr - lw * 0.5f, zr + lw * 0.5f, y);

            // ---- auxiliary lanes and ramps (ribbons)
            var rb = new MeshUtil();
            float zAux = geo.LaneZ(-1f);
            foreach (var s in lay.aux)
            {
                bool startJoined = lay.hasEnter && Mathf.Abs(s[0] - lay.enterX) < 0.2f;
                bool endJoined = lay.hasExit && Mathf.Abs(s[1] - lay.exitX) < 0.2f;
                var pts = new List<Vector3>(); var ws = new List<float>();
                const float taper = 60f;
                if (!startJoined) { pts.Add(new Vector3(s[0] - taper, 0f, zAux)); ws.Add(0f); }
                pts.Add(new Vector3(s[0], 0f, zAux)); ws.Add(W);
                pts.Add(new Vector3(s[1], 0f, zAux)); ws.Add(W);
                if (!endJoined) { pts.Add(new Vector3(s[1] + taper, 0f, zAux)); ws.Add(0f); }
                rb.Ribbon(pts, ws, 0.006f);
            }
            if (lay.hasEnter)
            {
                var pts = new List<Vector3>(); var ws = new List<float>();
                for (float x = lay.entryApproachStart - 120f; x < lay.enterX; x += 10f) { pts.Add(geo.Position(LaneId.EntryRamp, x, -1f)); ws.Add(W); }
                pts.Add(geo.Position(LaneId.EntryRamp, lay.enterX, -1f)); ws.Add(W);
                rb.Ribbon(pts, ws, 0.006f);
            }
            if (lay.hasExit)
            {
                var pts = new List<Vector3>(); var ws = new List<float>();
                for (float x = lay.exitX; x < lay.exitRampEnd + 80f; x += 10f) { pts.Add(geo.Position(LaneId.ExitRamp, x, -1f)); ws.Add(W); }
                pts.Add(geo.Position(LaneId.ExitRamp, lay.exitRampEnd + 80f, -1f)); ws.Add(W);
                rb.Ribbon(pts, ws, 0.006f);
            }
            MeshUtil.MeshObject("Ramps", root, rb.ToMesh("Ramps"), asphalt);
            MeshUtil.MeshObject("Markings", root, mk.ToMesh("Markings"), line);

            // ---- gantry signs (blue = on-ramp, green = exit)
            if (lay.hasExit) Sign(root, geo, p, "EXIT", lay.decelStart - 20f, new Color(0.05f, 0.45f, 0.20f));
            if (lay.hasEnter) Sign(root, geo, p, "ON-RAMP", lay.entryApproachStart - 10f, new Color(0.10f, 0.25f, 0.65f), true);

            // ---- traffic light at the end of the exit ramp
            if (lay.hasSignal) light = BuildSignal(root, geo, lay, line);

            // ---- distance posts every 500 m
            var post = MeshUtil.Lit("Post", new Color(0.85f, 0.85f, 0.85f), 0.2f);
            for (float x = 0f; x <= L; x += 500f)
                MeshUtil.Cube("Post" + (int)x, root, new Vector3(x, 0.6f, geo.LaneZ(N - 0.5f) + 3f), new Vector3(0.3f, 1.2f, 0.3f), post, false);
            return root;
        }

        static void Sign(Transform root, RoadGeometry geo, SimParams p, string text, float x, Color col, bool ramp = false)
        {
            var mat = MeshUtil.Lit("Sign_" + text, col, 0.2f);
            var pole = MeshUtil.Lit("Pole", new Color(0.5f, 0.5f, 0.52f), 0.4f);
            float zRight = geo.LaneZ(-0.5f) - 2.5f - (ramp ? RoadGeometry.RampOffsetMax * 0.9f : 0f);
            float zLeft = zRight + 7f;
            MeshUtil.Cube("Pole", root, new Vector3(x, 3.5f, zRight), new Vector3(0.3f, 7f, 0.3f), pole, false);
            MeshUtil.Cube("Pole", root, new Vector3(x, 3.5f, zLeft), new Vector3(0.3f, 7f, 0.3f), pole, false);
            MeshUtil.Cube("Board", root, new Vector3(x, 6.2f, (zRight + zLeft) * 0.5f), new Vector3(0.3f, 2.2f, 7f), mat, true);
            var t = new GameObject("Text"); t.transform.SetParent(root, false);
            t.transform.position = new Vector3(x - 0.25f, 6.2f, (zRight + zLeft) * 0.5f);
            t.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var tm = t.AddComponent<TextMesh>();
            tm.text = text; tm.characterSize = 0.25f; tm.fontSize = 64; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) { tm.font = font; t.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
        }

        static TrafficLightView BuildSignal(Transform root, RoadGeometry geo, Layout lay, Material line)
        {
            float x = lay.exitRampEnd;
            Vector3 c = geo.Position(LaneId.ExitRamp, x, -1f);
            var stop = new MeshUtil(); stop.Flat(x - 0.3f, x + 0.3f, c.z - RoadGeometry.LaneW * 0.5f, c.z + RoadGeometry.LaneW * 0.5f, 0.014f);
            MeshUtil.MeshObject("StopLine", root, stop.ToMesh("StopLine"), line);
            var go = new GameObject("TrafficLight"); go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x + 1f, 0f, c.z - RoadGeometry.LaneW * 0.5f - 1.2f);
            var pole = MeshUtil.Lit("Pole", new Color(0.5f, 0.5f, 0.52f), 0.4f);
            MeshUtil.Cube("Pole", go.transform, new Vector3(0f, 2.5f, 0f), new Vector3(0.25f, 5f, 0.25f), pole, false);
            MeshUtil.Cube("Head", go.transform, new Vector3(0f, 5.3f, 0f), new Vector3(0.5f, 1.6f, 0.7f), MeshUtil.Lit("LightBody", new Color(0.08f, 0.08f, 0.09f), 0.2f), true);
            var v = go.AddComponent<TrafficLightView>();
            v.redOn = MeshUtil.Lit("RedOn", new Color(1f, 0.1f, 0.08f), 0.5f); v.redOff = MeshUtil.Lit("RedOff", new Color(0.25f, 0.04f, 0.04f), 0.2f);
            v.greenOn = MeshUtil.Lit("GreenOn", new Color(0.15f, 1f, 0.25f), 0.5f); v.greenOff = MeshUtil.Lit("GreenOff", new Color(0.04f, 0.2f, 0.06f), 0.2f);
            v.red = MeshUtil.Sphere("Red", go.transform, new Vector3(-0.3f, 5.7f, 0f), 0.5f, v.redOn).GetComponent<Renderer>();
            v.green = MeshUtil.Sphere("Green", go.transform, new Vector3(-0.3f, 4.9f, 0f), 0.5f, v.greenOff).GetComponent<Renderer>();
            return v;
        }
    }
}
