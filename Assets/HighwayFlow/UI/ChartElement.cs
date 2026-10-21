using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace HighwayFlow.UI
{
    /// <summary>Multi-series line chart drawn with Painter2D (tick labels are child Labels).</summary>
    public class ChartElement : VisualElement
    {
        public class Series { public string name; public Color color; public List<Vector2> pts = new List<Vector2>(); }

        public readonly List<Series> series = new List<Series>();
        public string xTitle = "", yTitle = "";
        public float markerX = -1f;
        float xMin, xMax = 1f, yMin, yMax = 1f;
        const float ML = 52f, MB = 34f, MT = 26f, MR = 14f;

        public ChartElement()
        {
            generateVisualContent += OnGenerate;
            RegisterCallback<GeometryChangedEvent>(_ => Relabel());
            style.flexGrow = 1;
        }

        public void SetData(IEnumerable<Series> data, string xt, string yt, float? fixedYMax = null)
        {
            series.Clear(); series.AddRange(data);
            xTitle = xt; yTitle = yt;
            float x0 = float.MaxValue, x1 = float.MinValue, y1 = 0f;
            foreach (var s in series) foreach (var p in s.pts) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
            if (x0 > x1) { x0 = 0; x1 = 1; }
            xMin = x0; xMax = Mathf.Max(x1, x0 + 1f); yMin = 0f;
            yMax = fixedYMax ?? NiceMax(y1);
            Relabel(); MarkDirtyRepaint();
        }

        static float NiceMax(float v)
        {
            if (v <= 0f) return 1f;
            float mag = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(v)));
            foreach (var m in new[] { 1f, 1.5f, 2f, 3f, 4f, 5f, 6f, 8f, 10f }) if (m * mag >= v * 1.02f) return m * mag;
            return 10f * mag;
        }

        static float NiceStep(float range, int target)
        {
            float raw = range / Mathf.Max(1, target);
            float mag = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
            foreach (var m in new[] { 1f, 2f, 5f, 10f }) if (m * mag >= raw) return m * mag;
            return 10f * mag;
        }

        Vector2 Map(Rect r, float x, float y)
            => new Vector2(r.x + ML + (x - xMin) / (xMax - xMin) * (r.width - ML - MR), r.y + r.height - MB - (y - yMin) / (yMax - yMin) * (r.height - MB - MT));

        void Relabel()
        {
            Clear();
            var r = contentRect;
            if (r.width < 10f || r.height < 10f) return;
            var col = new Color(0.72f, 0.75f, 0.80f);
            float xs = NiceStep(xMax - xMin, 6), ys = NiceStep(yMax - yMin, 5);
            for (float x = Mathf.Ceil(xMin / xs) * xs; x <= xMax + 1e-3f; x += xs) AddLabel(Map(new Rect(0, 0, r.width, r.height), x, 0f) + new Vector2(-14f, 4f), x.ToString("0.#", CultureInfo.InvariantCulture), col, 28f);
            for (float y = yMin; y <= yMax + 1e-3f; y += ys) AddLabel(Map(new Rect(0, 0, r.width, r.height), xMin, y) + new Vector2(-ML + 2f, -8f), y.ToString("0.#", CultureInfo.InvariantCulture), col, ML - 8f, TextAnchor.MiddleRight);
            AddLabel(new Vector2(ML, r.height - 14f), xTitle, col, r.width - ML - MR, TextAnchor.MiddleCenter);
            AddLabel(new Vector2(4f, 0f), yTitle, col, 200f, TextAnchor.UpperLeft);
        }

        void AddLabel(Vector2 pos, string text, Color c, float width, TextAnchor anchor = TextAnchor.UpperCenter)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute; l.style.left = pos.x; l.style.top = pos.y; l.style.width = width;
            l.style.fontSize = 11; l.style.color = c; l.style.unityTextAlign = anchor; l.style.whiteSpace = WhiteSpace.NoWrap;
            Add(l);
        }

        void OnGenerate(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 10f || r.height < 10f) return;
            var p = mgc.painter2D;
            var local = new Rect(0, 0, r.width, r.height);
            p.lineWidth = 1f; p.strokeColor = new Color(1f, 1f, 1f, 0.10f);
            float xs = NiceStep(xMax - xMin, 6), ys = NiceStep(yMax - yMin, 5);
            for (float y = yMin; y <= yMax + 1e-3f; y += ys) { p.BeginPath(); p.MoveTo(Map(local, xMin, y)); p.LineTo(Map(local, xMax, y)); p.Stroke(); }
            for (float x = Mathf.Ceil(xMin / xs) * xs; x <= xMax + 1e-3f; x += xs) { p.BeginPath(); p.MoveTo(Map(local, x, yMin)); p.LineTo(Map(local, x, yMax)); p.Stroke(); }
            p.strokeColor = new Color(1f, 1f, 1f, 0.45f); p.lineWidth = 1.5f;
            p.BeginPath(); p.MoveTo(Map(local, xMin, yMax)); p.LineTo(Map(local, xMin, yMin)); p.LineTo(Map(local, xMax, yMin)); p.Stroke();
            if (markerX >= xMin && markerX <= xMax)
            {
                p.strokeColor = new Color(1f, 1f, 1f, 0.55f); p.lineWidth = 1.2f;
                for (float y = yMin; y < yMax; y += (yMax - yMin) / 30f) { p.BeginPath(); p.MoveTo(Map(local, markerX, y)); p.LineTo(Map(local, markerX, Mathf.Min(y + (yMax - yMin) / 60f, yMax))); p.Stroke(); }
            }
            foreach (var s in series)
            {
                if (s.pts.Count == 0) continue;
                p.strokeColor = s.color; p.lineWidth = 2.4f; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
                p.BeginPath();
                for (int i = 0; i < s.pts.Count; i++) { var q = Map(local, s.pts[i].x, s.pts[i].y); if (i == 0) p.MoveTo(q); else p.LineTo(q); }
                p.Stroke();
                p.fillColor = s.color;
                foreach (var pt in s.pts) { p.BeginPath(); p.Arc(Map(local, pt.x, pt.y), 2.8f, 0f, 360f); p.Fill(); }
            }
        }
    }
}
