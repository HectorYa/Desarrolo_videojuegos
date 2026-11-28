using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Mallas low poly de caras planas construidas en tiempo de juego (decorado y personajes de la cinemática).
    /// Cada triángulo lleva sus propios vértices → normales planas, el mismo aspecto facetado que el resto del juego.
    public class LowPoly
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<int> t = new List<int>();
        readonly List<Color> c = new List<Color>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Vector3> grains = new List<Vector3>();
        Matrix4x4 m = Matrix4x4.identity;
        Color col = Color.white;
        Vector3 grain = Vector3.up;

        public LowPoly Paint(Color k) { col = k; return this; }
        /// Dirección de la fibra (local a la pieza, antes de At) para el shader MageHero/ToonDetail en modo madera.
        public LowPoly Grain(Vector3 localDir) { grain = localDir; return this; }
        /// Transformación que se aplica a las piezas que se añadan después.
        public LowPoly At(Vector3 pos, Quaternion rot, Vector3 scale) { m = Matrix4x4.TRS(pos, rot, scale); return this; }
        public LowPoly At(Vector3 pos) => At(pos, Quaternion.identity, Vector3.one);
        public LowPoly Reset() { m = Matrix4x4.identity; return this; }

        public void Tri(Vector3 a, Vector3 b, Vector3 d)
        {
            int i = v.Count;
            v.Add(m.MultiplyPoint3x4(a)); v.Add(m.MultiplyPoint3x4(b)); v.Add(m.MultiplyPoint3x4(d));
            c.Add(col); c.Add(col); c.Add(col);
            uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one);
            Vector3 g = m.MultiplyVector(grain).normalized;
            grains.Add(g); grains.Add(g); grains.Add(g);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e) { Tri(a, b, d); Tri(a, d, e); }

        /// Caja centrada en el origen local.
        public LowPoly Box(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] p =
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z),
            };
            Quad(p[0], p[3], p[2], p[1]); Quad(p[5], p[6], p[7], p[4]);
            Quad(p[4], p[7], p[3], p[0]); Quad(p[1], p[2], p[6], p[5]);
            Quad(p[3], p[7], p[6], p[2]); Quad(p[4], p[0], p[1], p[5]);
            return this;
        }

        /// Prisma (cilindro facetado) a lo largo del eje Y local, de y=0 a y=height.
        public LowPoly Prism(int sides, float r0, float r1, float height, bool caps = true, float twist = 0f)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f, a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0), b1 = new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
                Vector3 u0 = new Vector3(Mathf.Cos(a0 + twist) * r1, height, Mathf.Sin(a0 + twist) * r1), u1 = new Vector3(Mathf.Cos(a1 + twist) * r1, height, Mathf.Sin(a1 + twist) * r1);
                if (r1 > 0.0001f) Quad(b0, u0, u1, b1); else Tri(b0, u0, b1);
                if (caps)
                {
                    Tri(Vector3.zero, b0, b1);
                    if (r1 > 0.0001f) Tri(new Vector3(0, height, 0), u1, u0);
                }
            }
            return this;
        }

        /// Roca: icosaedro subdividido una vez y deformado con ruido estable (semilla).
        public LowPoly Rock(float radius, int seed, float rough = 0.22f, int subdiv = 1)
        {
            var ico = Icosphere(subdiv);
            var rng = new System.Random(seed);
            var off = new Dictionary<Vector3, float>();
            for (int i = 0; i < ico.Count; i++)
            {
                var p = ico[i];
                if (!off.ContainsKey(p)) off[p] = 1f + ((float)rng.NextDouble() * 2f - 1f) * rough;
            }
            for (int i = 0; i < ico.Count; i += 3)
                Tri(ico[i] * radius * off[ico[i]], ico[i + 1] * radius * off[ico[i + 1]], ico[i + 2] * radius * off[ico[i + 2]]);
            return this;
        }

        static List<Vector3> Icosphere(int subdiv)
        {
            float g = (1f + Mathf.Sqrt(5f)) / 2f;
            var p = new List<Vector3>
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var tris = new List<Vector3>();
            for (int i = 0; i < f.Length; i++) tris.Add(p[f[i]].normalized);
            for (int s = 0; s < subdiv; s++)
            {
                var n = new List<Vector3>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = tris[i], b = tris[i + 1], d = tris[i + 2];
                    Vector3 ab = ((a + b) * 0.5f).normalized, bd = ((b + d) * 0.5f).normalized, da = ((d + a) * 0.5f).normalized;
                    n.AddRange(new[] { a, ab, da, ab, b, bd, da, bd, d, ab, bd, da });
                }
                tris = n;
            }
            // Redondeo para que los vértices compartidos tengan la misma clave en el diccionario de ruido.
            for (int i = 0; i < tris.Count; i++)
                tris[i] = new Vector3(Mathf.Round(tris[i].x * 1e4f) / 1e4f, Mathf.Round(tris[i].y * 1e4f) / 1e4f, Mathf.Round(tris[i].z * 1e4f) / 1e4f);
            return tris;
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.SetColors(c); mesh.SetUVs(0, uv); mesh.SetUVs(1, grains);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// Crea un objeto con la malla y el material.
        public GameObject Spawn(string name, Transform parent, Material mat, Vector3 localPos = default, bool shadows = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.GetComponent<MeshFilter>().sharedMesh = Build(name);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// Pivote vacío (articulación) para animar piezas.
        public static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            go.localPosition = localPos;
            return go;
        }
    }
}
