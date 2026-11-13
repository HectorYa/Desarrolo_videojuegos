using System;
using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    public class Route { public string name, theme; public float w; public Vector2[] q; public float minx, minz, maxx, maxz; }
    public class Bridge { public float x, z, L, W, y; public bool pier; }

    /// Matemática del mundo (altura, montañas, rutas, río, colores). Port directo del prototipo web.
    public static class World
    {
        public const float HW = 108f, MW = 216f;
        public static LevelDef L;
        public static readonly List<Route> RT = new List<Route>();
        public static readonly List<Bridge> BR = new List<Bridge>();
        public static Vector2[] RIV;
        static float mf;

        public static float Smooth(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3f - 2f * x); }
        public static float Hash(float x, float z)
        {
            double q = Math.Sin(x * 127.1 + z * 311.7) * 43758.5453;
            return (float)(q - Math.Floor(q));
        }
        static float Terr(float h, float st) { float f = h / st; float i = Mathf.Floor(f); return (i + Smooth(0.75f, 1f, f - i)) * st; }

        public static Vector2[] Spline(Vector2[] p)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < p.Length - 1; i++)
            {
                Vector2 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, p.Length - 1)];
                for (int j = 0; j < 6; j++)
                {
                    float t = j / 6f, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            o.Add(p[p.Length - 1]);
            return o.ToArray();
        }

        static Route MakeRoute(RouteDef d)
        {
            var q = Spline(d.pts);
            var r = new Route { name = d.name, theme = d.theme, w = d.w, q = q, minx = 1e9f, minz = 1e9f, maxx = -1e9f, maxz = -1e9f };
            foreach (var v in q) { r.minx = Mathf.Min(r.minx, v.x); r.minz = Mathf.Min(r.minz, v.y); r.maxx = Mathf.Max(r.maxx, v.x); r.maxz = Mathf.Max(r.maxz, v.y); }
            r.minx -= 30f; r.minz -= 30f; r.maxx += 30f; r.maxz += 30f;
            return r;
        }

        public static float DistToPolyline(Vector2[] q, float x, float z)
        {
            float m = 1e9f;
            for (int i = 0; i < q.Length - 1; i++)
            {
                Vector2 a = q[i], b = q[i + 1];
                float dx = b.x - a.x, dz = b.y - a.y;
                float den = dx * dx + dz * dz; if (den < 1e-9f) den = 1f;
                float t = Mathf.Clamp01(((x - a.x) * dx + (z - a.y) * dz) / den);
                float ex = x - a.x - t * dx, ez = z - a.y - t * dz;
                float d = Mathf.Sqrt(ex * ex + ez * ez);
                if (d < m) m = d;
            }
            return m;
        }

        /// Distancia al borde de la ruta más cercana (negativa dentro del camino) y la ruta.
        public static float RouteInfo(float x, float z, out Route best)
        {
            float bd = 99f; best = null;
            for (int i = 0; i < RT.Count; i++)
            {
                var r = RT[i];
                if (x < r.minx || x > r.maxx || z < r.minz || z > r.maxz) continue;
                float d = DistToPolyline(r.q, x, z) - r.w;
                if (d < bd) { bd = d; best = r; }
            }
            return bd;
        }
        public static float RouteDist(float x, float z) { Route r; return RouteInfo(x, z, out r); }

        public static bool InBridge(Bridge b, float x, float z, float m) { return Mathf.Abs(x - b.x) < b.L / 2f + m && Mathf.Abs(z - b.z) < b.W / 2f + m; }
        public static bool NearAnyBridge(float x, float z, float m) { for (int i = 0; i < BR.Count; i++) if (InBridge(BR[i], x, z, m)) return true; return false; }
        public static float SeaS(float x, float z) { return L.sea != null ? L.sea.a * x + L.sea.b * z : -99f; }

        /// Altura de la cordillera perimetral (con repisas rocosas).
        public static float Mountain(float x, float z)
        {
            mf = 0f;
            float ax = Mathf.Abs(x), az = Mathf.Abs(z);
            float e = HW - Mathf.Pow(ax * ax * ax + az * az * az, 1f / 3f);
            float n = (Mathf.Sin(x * .05f + 2f) * Mathf.Cos(z * .043f) + .6f * Mathf.Sin(x * .11f - z * .09f) + 1.6f) / 3.2f;
            float f = Smooth(0f, 1f, (21f - (e + 8f * n)) / 13f);
            if (f <= 0f) return 0f;
            float A = L.mtn.A, r;
            if (L.mtn.dune) r = .75f + .3f * Mathf.Sin((x + z) * .07f) * Mathf.Sin((x - z) * .05f) + .12f * Mathf.Sin(x * .2f + z * .13f);
            else r = .6f + .5f * Mathf.Abs(Mathf.Sin(x * .12f + 1.1f) * Mathf.Cos(z * .1f + .4f)) + .3f * Mathf.Abs(Mathf.Sin(x * .27f + z * .2f)) + .15f * Mathf.Sin(x * .5f) * Mathf.Sin(z * .47f);
            float m = A * f * r * (L.sea != null ? 1f - .7f * Smooth(L.sea.from - 14f, L.sea.from + 6f, SeaS(x, z)) : 1f);
            if (L.mtn.step > 0f && m > A * .3f)
            {
                float ex = m - A * .3f, st = L.mtn.step, q = ex / st, i = Mathf.Floor(q);
                mf = q - i;
                m = A * .3f + (i + Smooth(.55f, 1f, q - i)) * st * .85f + ex * .15f;
            }
            return m;
        }

        public static float BaseHeight(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z), hh = 0f;
            for (int i = 0; i < L.hills.Length; i++)
            {
                Vector4 g = L.hills[i];
                hh += g.z * Mathf.Exp(-((x - g.x) * (x - g.x) + (z - g.y) * (z - g.y)) / g.w);
            }
            Route rr;
            if (L.terr > 0f && hh > .3f) hh += (Terr(hh, L.terr) - hh) * Smooth(3f, 12f, RouteInfo(x, z, out rr));
            float h = L.h0 + L.roll * Mathf.Sin(x * .05f + 1f) * Mathf.Cos(z * .045f) + .3f * Mathf.Sin(x * .25f) * Mathf.Sin(z * .22f) + hh;
            if (L.rim > 0f && r < 30f) { float k = (r - 18f) / 3.5f; h += L.rim * Mathf.Exp(-(k * k)) * Smooth(2f, 9f, RouteInfo(x, z, out rr)); }
            if (L.sea != null) h -= 9f * Smooth(L.sea.from, L.sea.to, SeaS(x, z));
            return h + Mountain(x, z);
        }

        public static float Height(float x, float z)
        {
            for (int i = 0; i < BR.Count; i++) if (InBridge(BR[i], x, z, 0f)) return BR[i].y;
            return BaseHeight(x, z) - L.carve * (1f - Smooth(2.5f, 7.5f, DistToPolyline(RIV, x, z))) * (1f - Smooth(.3f, 2.5f, Mountain(x, z)));
        }

        /// Color de suelo (0xRRGGBB). sl = pendiente (acantilados); h = altura ya calculada.
        public static int GroundColor(float x, float z, float sl, float h)
        {
            PaletteDef P = L.pal;
            float n = Hash(Mathf.Floor(x / 3f), Mathf.Floor(z / 3f));
            if (h < -.3f) return P.water;
            for (int i = 0; i < BR.Count; i++) if (InBridge(BR[i], x, z, 0f)) return P.deck;
            float m = Mountain(x, z);
            if (m > .4f)
            {
                int[] c = L.mtn.cols;
                float nn = Hash(Mathf.Floor(x / 4f), Mathf.Floor(z / 4f));
                if (mf > .5f) return c[3];
                return m > L.mtn.A * (.62f + nn * .1f) ? c[2] : m > L.mtn.A * (.25f + nn * .08f) ? c[1] : c[0];
            }
            if (sl > L.cliff) return P.cliff;
            Route r; float d = RouteInfo(x, z, out r);
            if (Mathf.Sqrt(x * x + z * z) < 13f || d < 0f) return P.path;
            if (d < .8f) return P.edge;
            if (L.sea != null && SeaS(x, z) > L.sea.beach && h < 2.3f) return P.sand;
            int[] col = null;
            if (d < 26f && r != null && P.th.ContainsKey(r.theme)) col = P.th[r.theme];
            if (col == null) col = P.wild;
            return n > .55f ? col[0] : col[1];
        }

        public static Color32 Hex(int h) { return new Color32((byte)((h >> 16) & 255), (byte)((h >> 8) & 255), (byte)(h & 255), 255); }

        public static void Configure(LevelDef d)
        {
            L = d; RT.Clear(); BR.Clear();
            foreach (var r in d.routes) RT.Add(MakeRoute(r));
            RIV = Spline(d.riv);
            foreach (int i in d.bridgeRoutes)
            {
                Route r = RT[i];
                int bk = -1; float bd = 1e9f;
                for (int k = 0; k < r.q.Length; k++) { float dd = DistToPolyline(RIV, r.q[k].x, r.q[k].y); if (dd < bd) { bd = dd; bk = k; } }
                if (bd > 5f) continue;
                Vector2 a = r.q[Mathf.Max(bk - 1, 0)], b = r.q[Mathf.Min(bk + 1, r.q.Length - 1)], c = r.q[bk];
                bool xl = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.y - a.y);
                BR.Add(new Bridge { x = c.x, z = c.y, L = xl ? 14f : 9f, W = xl ? 9f : 14f, y = BaseHeight(c.x, c.y) + .12f });
            }
            foreach (var p in d.piers) BR.Add(new Bridge { x = p.x, z = p.z, L = p.L, W = p.W, y = p.y, pier = true });
        }
    }
}
