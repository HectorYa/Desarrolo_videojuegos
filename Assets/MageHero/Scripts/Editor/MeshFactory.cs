#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MageHero.Editor
{
    /// Constructor de mallas low-poly con sombreado plano y color por vértice.
    public class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Color32> c = new List<Color32>();
        readonly List<int> t = new List<int>();

        public static Matrix4x4 TRS(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float sx = 1, float sy = 1, float sz = 1)
        {
            return Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(rx, ry, rz), new Vector3(sx, sy, sz));
        }
        public static Color32 Col(int hex) { return World.Hex(hex); }

        // Cara convexa; refPt es un punto interior para orientar la normal hacia fuera.
        void Face(Vector3 refPt, Color32 col, params Vector3[] p)
        {
            Vector3 center = Vector3.zero;
            foreach (var q in p) center += q;
            center /= p.Length;
            Vector3 nrm = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
            if (nrm.sqrMagnitude < 1e-12f) return;
            nrm.Normalize();
            if (Vector3.Dot(nrm, center - refPt) < 0f) { Array.Reverse(p); nrm = -nrm; }
            int b = v.Count;
            foreach (var q in p) { v.Add(q); n.Add(nrm); c.Add(col); }
            for (int k = 1; k < p.Length - 1; k++) { t.Add(b); t.Add(b + k); t.Add(b + k + 1); }
        }

        static readonly Vector3[] IcoV;
        static readonly int[][] IcoF;
        static MeshBuilder()
        {
            float a = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] raw = {
                new Vector3(-1, a, 0), new Vector3(1, a, 0), new Vector3(-1, -a, 0), new Vector3(1, -a, 0),
                new Vector3(0, -1, a), new Vector3(0, 1, a), new Vector3(0, -1, -a), new Vector3(0, 1, -a),
                new Vector3(a, 0, -1), new Vector3(a, 0, 1), new Vector3(-a, 0, -1), new Vector3(-a, 0, 1) };
            IcoV = new Vector3[raw.Length];
            for (int i = 0; i < raw.Length; i++) IcoV[i] = raw[i].normalized;
            IcoF = new int[][] {
                new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
                new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
                new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
                new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1} };
        }

        /// Icosaedro (detail 0 = 20 caras, 1 = 80 caras), transformado por m.
        public void Icosa(Matrix4x4 m, float r, Color32 col, int detail = 0)
        {
            Vector3 refPt = m.MultiplyPoint3x4(Vector3.zero);
            foreach (var f in IcoF) Sub(m, r, col, refPt, IcoV[f[0]], IcoV[f[1]], IcoV[f[2]], detail);
        }
        void Sub(Matrix4x4 m, float r, Color32 col, Vector3 refPt, Vector3 a, Vector3 b, Vector3 cc, int d)
        {
            if (d <= 0)
            {
                Face(refPt, col, m.MultiplyPoint3x4(a * r), m.MultiplyPoint3x4(b * r), m.MultiplyPoint3x4(cc * r));
                return;
            }
            Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + cc) * 0.5f).normalized, ca = ((cc + a) * 0.5f).normalized;
            Sub(m, r, col, refPt, a, ab, ca, d - 1); Sub(m, r, col, refPt, b, bc, ab, d - 1);
            Sub(m, r, col, refPt, cc, ca, bc, d - 1); Sub(m, r, col, refPt, ab, bc, ca, d - 1);
        }

        public void Octa(Matrix4x4 m, float r, Color32 col)
        {
            Vector3[] p = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int[][] f = { new[]{2,4,0}, new[]{2,1,4}, new[]{2,5,1}, new[]{2,0,5}, new[]{3,0,4}, new[]{3,4,1}, new[]{3,1,5}, new[]{3,5,0} };
            Vector3 refPt = m.MultiplyPoint3x4(Vector3.zero);
            foreach (var q in f) Face(refPt, col, m.MultiplyPoint3x4(p[q[0]] * r), m.MultiplyPoint3x4(p[q[1]] * r), m.MultiplyPoint3x4(p[q[2]] * r));
        }

        /// Cono: base en y=0, punta en y=h.
        public void Cone(Matrix4x4 m, int sides, float r, float h, Color32 col)
        {
            Vector3 apex = m.MultiplyPoint3x4(new Vector3(0, h, 0)), baseC = m.MultiplyPoint3x4(Vector3.zero), refPt = m.MultiplyPoint3x4(new Vector3(0, h * 0.4f, 0));
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++) { float a = i * Mathf.PI * 2f / sides; ring[i] = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
            for (int i = 0; i < sides; i++) Face(refPt, col, apex, ring[i], ring[(i + 1) % sides]);
            Face(refPt, col, ring);
        }

        /// Cilindro/tronco de cono: de y=0 a y=h (rTop arriba, rBot abajo).
        public void Cyl(Matrix4x4 m, int sides, float rTop, float rBot, float h, Color32 col)
        {
            Vector3 refPt = m.MultiplyPoint3x4(new Vector3(0, h * 0.5f, 0));
            var top = new Vector3[sides]; var bot = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, cx = Mathf.Cos(a), sz = Mathf.Sin(a);
                top[i] = m.MultiplyPoint3x4(new Vector3(cx * rTop, h, sz * rTop));
                bot[i] = m.MultiplyPoint3x4(new Vector3(cx * rBot, 0, sz * rBot));
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Face(refPt, col, bot[i], top[i], top[j], bot[j]);
            }
            Face(refPt, col, top);
            Face(refPt, col, bot);
        }

        /// Caja centrada en el origen local.
        public void Box(Matrix4x4 m, float sx, float sy, float sz, Color32 col)
        {
            float x = sx / 2, y = sy / 2, z = sz / 2;
            Vector3[] p = { new Vector3(-x,-y,-z), new Vector3(x,-y,-z), new Vector3(x,y,-z), new Vector3(-x,y,-z),
                            new Vector3(-x,-y,z), new Vector3(x,-y,z), new Vector3(x,y,z), new Vector3(-x,y,z) };
            for (int i = 0; i < 8; i++) p[i] = m.MultiplyPoint3x4(p[i]);
            Vector3 refPt = m.MultiplyPoint3x4(Vector3.zero);
            Face(refPt, col, p[0], p[1], p[2], p[3]); Face(refPt, col, p[4], p[5], p[6], p[7]);
            Face(refPt, col, p[0], p[1], p[5], p[4]); Face(refPt, col, p[3], p[2], p[6], p[7]);
            Face(refPt, col, p[0], p[3], p[7], p[4]); Face(refPt, col, p[1], p[2], p[6], p[5]);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(c); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
