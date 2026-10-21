using System.Collections.Generic;
using UnityEngine;

namespace HighwayFlow
{
    /// <summary>Tiny procedural mesh builder (boxes, flat quads, ribbons) and material helpers.</summary>
    public class MeshUtil
    {
        readonly List<Vector3> vs = new List<Vector3>(), ns = new List<Vector3>();
        readonly List<int> tris = new List<int>();

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            int i = vs.Count;
            vs.Add(a); vs.Add(b); vs.Add(c); vs.Add(d);
            ns.Add(n); ns.Add(n); ns.Add(n); ns.Add(n);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>Flat rectangle on the ground: x range [x0,x1], z range [z0,z1] at height y (facing up).</summary>
        public void Flat(float x0, float x1, float z0, float z1, float y)
            => Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up);

        public void Box(Vector3 c, Vector3 s)
        {
            Vector3 h = s * 0.5f;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z), p010 = c + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z), p011 = c + new Vector3(-h.x, h.y, h.z);
            Quad(p000, p010, p110, p100, Vector3.back);     // -Z
            Quad(p101, p111, p011, p001, Vector3.forward);  // +Z
            Quad(p001, p011, p010, p000, Vector3.left);     // -X
            Quad(p100, p110, p111, p101, Vector3.right);    // +X
            Quad(p010, p011, p111, p110, Vector3.up);       // top
            Quad(p000, p100, p101, p001, Vector3.down);     // bottom
        }

        /// <summary>Ribbon along +X with varying centre z and width.</summary>
        public void Ribbon(IList<Vector3> centres, IList<float> widths, float y)
        {
            for (int i = 0; i + 1 < centres.Count; i++)
            {
                var a = centres[i]; var b = centres[i + 1];
                float wa = widths[i] * 0.5f, wb = widths[i + 1] * 0.5f;
                Quad(new Vector3(a.x, y, a.z - wa), new Vector3(a.x, y, a.z + wa), new Vector3(b.x, y, b.z + wb), new Vector3(b.x, y, b.z - wb), Vector3.up);
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (vs.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(vs); m.SetNormals(ns); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // ---------------- materials / objects

        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static Material Lit(string key, Color c, float smooth = 0.15f)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = key, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            cache[key] = m;
            return m;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, bool shadows = false)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        public static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool shadows = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var c = g.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 pos, float diameter, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var c = g.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = Vector3.one * diameter;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }
    }
}
