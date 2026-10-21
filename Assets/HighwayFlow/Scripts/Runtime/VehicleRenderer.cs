using System.Collections.Generic;
using UnityEngine;

namespace HighwayFlow
{
    public enum ColorMode { Speed, Role, Lane }

    /// <summary>Draws the simulated vehicles with a pool of small mesh objects (interpolated between simulation steps).</summary>
    public class VehicleRenderer : MonoBehaviour
    {
        public ColorMode colorMode = ColorMode.Speed;

        class Item { public Transform tf; public MeshFilter mf; public MeshRenderer mr; public bool truck; public Color color = new Color(-1, 0, 0, 0); }

        readonly List<Item> pool = new List<Item>();
        Mesh carMesh, truckMesh;
        Material mat;
        MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");

        static readonly Color[] LaneColors =
        {
            new Color(0.30f, 0.65f, 1f), new Color(0.35f, 0.90f, 0.55f), new Color(1f, 0.85f, 0.25f), new Color(1f, 0.55f, 0.35f), new Color(0.85f, 0.45f, 0.95f)
        };

        void Ensure()
        {
            if (mat != null) return;
            mat = MeshUtil.Lit("Vehicle", Color.white, 0.35f);
            mpb = new MaterialPropertyBlock();
            var c = new MeshUtil();
            c.Box(new Vector3(0f, 0.62f, 0f), new Vector3(4.5f, 0.75f, 1.85f));
            c.Box(new Vector3(-0.2f, 1.2f, 0f), new Vector3(2.4f, 0.5f, 1.6f));
            carMesh = c.ToMesh("Car");
            var t = new MeshUtil();
            t.Box(new Vector3(-1.2f, 2.1f, 0f), new Vector3(9.6f, 3.0f, 2.5f));
            t.Box(new Vector3(5.0f, 1.4f, 0f), new Vector3(2.0f, 2.2f, 2.4f));
            truckMesh = t.ToMesh("Truck");
        }

        Item Get(int i)
        {
            while (pool.Count <= i)
            {
                var g = new GameObject("Veh" + pool.Count);
                g.transform.SetParent(transform, false);
                var it = new Item { tf = g.transform, mf = g.AddComponent<MeshFilter>(), mr = g.AddComponent<MeshRenderer>() };
                it.mr.sharedMaterial = mat;
                it.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pool.Add(it);
            }
            return pool[i];
        }

        public Color ColorFor(Vehicle v)
        {
            Color c;
            switch (colorMode)
            {
                case ColorMode.Role:
                    c = v.exitIntent ? new Color(0.90f, 0.25f, 0.85f) : v.missedExit ? new Color(1f, 0.9f, 0.2f) : v.fromRamp ? new Color(1f, 0.55f, 0.1f) : new Color(0.80f, 0.86f, 0.95f);
                    break;
                case ColorMode.Lane:
                    c = v.lane >= 0 ? LaneColors[Mathf.Clamp(v.lane, 0, 4)] : (v.lane == LaneId.Aux ? Color.white : new Color(0.7f, 0.7f, 0.7f));
                    break;
                default:
                    c = SpeedColor(v.v);
                    break;
            }
            return v.truck ? c * 0.72f : c;
        }

        public static Color SpeedColor(float speedMs)
        {
            float t = Mathf.Clamp01(speedMs / 28f);
            return Color.HSVToRGB(0.33f * t, 0.85f, 0.95f);
        }

        public void Clear() { foreach (var it in pool) if (it.tf != null) it.tf.gameObject.SetActive(false); }

        public void Draw(HighwaySim sim, RoadGeometry geo, float alpha)
        {
            Ensure();
            var list = sim.vehicles;
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                var v = list[i];
                var it = Get(i);
                if (!it.tf.gameObject.activeSelf) it.tf.gameObject.SetActive(true);
                if (it.mf.sharedMesh == null || it.truck != v.truck) { it.truck = v.truck; it.mf.sharedMesh = v.truck ? truckMesh : carMesh; }
                float x = Mathf.Lerp(v.px, v.x, alpha) - v.length * 0.5f;
                float lat = Mathf.Lerp(v.plat, v.lat, alpha);
                it.tf.position = geo.Position(v.lane, x, lat);
                it.tf.rotation = Quaternion.Euler(0f, geo.YawDeg(v.lane, x, v.v, v.latRate), 0f);
                var col = ColorFor(v);
                if (col != it.color)
                {
                    it.color = col;
                    mpb.SetColor(BaseColor, col); mpb.SetColor(ColorId, col);
                    it.mr.SetPropertyBlock(mpb);
                }
            }
            for (int i = n; i < pool.Count; i++) if (pool[i].tf.gameObject.activeSelf) pool[i].tf.gameObject.SetActive(false);
        }
    }
}
