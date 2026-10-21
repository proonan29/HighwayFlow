using UnityEngine;
using UnityEngine.UIElements;

namespace HighwayFlow.UI
{
    /// <summary>Top-down "board" of the whole highway: one row per lane, every vehicle is a small bar coloured by speed.</summary>
    public class StripElement : VisualElement
    {
        public System.Func<HighwaySim> getSim;
        const float Pad = 8f;

        public StripElement()
        {
            generateVisualContent += OnGenerate;
            pickingMode = PickingMode.Ignore;
        }

        void OnGenerate(MeshGenerationContext mgc)
        {
            var sim = getSim?.Invoke();
            var r = contentRect;
            if (sim == null || r.width < 20f || r.height < 20f) return;
            var p = mgc.painter2D;
            var P = sim.P; var lay = sim.Lay;
            int rows = P.lanes + 3;                    // lanes (top = leftmost), aux, entry ramp, exit ramp
            float rowH = (r.height - Pad * 2f) / rows;
            float x0 = Pad, w = r.width - Pad * 2f;
            float sx = w / P.length;

            p.fillColor = new Color(0.05f, 0.06f, 0.08f, 0.82f);
            p.BeginPath(); p.MoveTo(new Vector2(0, 0)); p.LineTo(new Vector2(r.width, 0)); p.LineTo(new Vector2(r.width, r.height)); p.LineTo(new Vector2(0, r.height)); p.ClosePath(); p.Fill();

            // row backgrounds
            for (int i = 0; i < rows; i++)
            {
                float y = Pad + i * rowH;
                p.fillColor = i % 2 == 0 ? new Color(1f, 1f, 1f, 0.05f) : new Color(1f, 1f, 1f, 0.02f);
                p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x0 + w, y)); p.LineTo(new Vector2(x0 + w, y + rowH)); p.LineTo(new Vector2(x0, y + rowH)); p.ClosePath(); p.Fill();
            }
            // auxiliary lane extents and ramp extents
            p.fillColor = new Color(0.4f, 0.5f, 0.9f, 0.22f);
            foreach (var s in lay.aux) Rect(p, x0 + s[0] * sx, Pad + P.lanes * rowH, (s[1] - s[0]) * sx, rowH);
            if (lay.hasEnter) { p.fillColor = new Color(1f, 0.6f, 0.2f, 0.22f); Rect(p, x0 + lay.entryApproachStart * sx, Pad + (P.lanes + 1) * rowH, P.approachLen * sx, rowH); }
            if (lay.hasExit) { p.fillColor = new Color(0.9f, 0.3f, 0.85f, 0.22f); Rect(p, x0 + lay.exitX * sx, Pad + (P.lanes + 2) * rowH, P.exitRampLen * sx, rowH); }
            if (lay.hasSignal)
            {
                p.fillColor = sim.SignalGreen ? new Color(0.2f, 1f, 0.35f) : new Color(1f, 0.2f, 0.15f);
                Rect(p, x0 + lay.exitRampEnd * sx - 1.5f, Pad + (P.lanes + 2) * rowH, 3f, rowH);
            }

            // vehicles
            var list = sim.vehicles;
            float bh = Mathf.Max(2f, rowH - 3f);
            for (int i = 0; i < list.Count; i++)
            {
                var v = list[i];
                int row = v.lane >= 0 ? (P.lanes - 1 - v.lane) : (v.lane == LaneId.Aux ? P.lanes : (v.lane == LaneId.EntryRamp ? P.lanes + 1 : P.lanes + 2));
                float y = Pad + row * rowH + (rowH - bh) * 0.5f;
                float len = Mathf.Max(2.5f, v.length * sx);
                p.fillColor = VehicleRenderer.SpeedColor(v.v);
                Rect(p, x0 + (v.x - v.length) * sx, y, len, bh);
            }
        }

        static void Rect(Painter2D p, float x, float y, float w, float h)
        {
            p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath(); p.Fill();
        }
    }
}
