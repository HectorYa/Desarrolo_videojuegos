using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MageHero
{
    /// Interfaz con estilo low poly (a juego con el mundo del juego): formas de bordes rectos (esquinas en bisel,
    /// octógonos, banderolas con cola en V) rellenas de facetas triangulares de tonos ligeramente distintos,
    /// como las caras planas del terreno. Todo se genera por código (sin imágenes externas).
    public static class LowPolyUI
    {
        // Paleta: pizarra azul noche (paneles), ámbar otoñal (acentos), crema (texto), verde bosque / rojo arcilla (resultado).
        public static readonly Color Backdrop = new Color(0.03f, 0.05f, 0.09f, 0.34f);   // más transparente: se ve el juego detrás de la tarjeta
        public static readonly Color Slate = new Color(0.17f, 0.23f, 0.31f, 0.94f);
        public static readonly Color SlateDeep = new Color(0.09f, 0.12f, 0.17f, 0.96f);
        public static readonly Color Tile = new Color(0.27f, 0.42f, 0.58f, 1f);
        public static readonly Color Amber = new Color(0.95f, 0.64f, 0.24f, 1f);
        public static readonly Color AmberDeep = new Color(0.72f, 0.39f, 0.13f, 1f);
        public static readonly Color Cream = new Color(1f, 0.96f, 0.88f, 1f);
        public static readonly Color Right = new Color(0.33f, 0.68f, 0.38f, 1f);
        public static readonly Color Wrong = new Color(0.80f, 0.30f, 0.26f, 1f);

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        // ------------------------------------------------------------------ formas
        public static Vector2[] Chamfer(int w, int h, float c)
        {
            c = Mathf.Min(c, Mathf.Min(w, h) * 0.5f - 1f);
            return new[] { new Vector2(c, 0), new Vector2(w - c, 0), new Vector2(w, c), new Vector2(w, h - c), new Vector2(w - c, h), new Vector2(c, h), new Vector2(0, h - c), new Vector2(0, c) };
        }

        /// Hexágono alargado (laterales en punta).
        public static Vector2[] Pointy(int w, int h, float p)
        {
            p = Mathf.Min(p, h * 0.5f);
            return new[] { new Vector2(p, 0), new Vector2(w - p, 0), new Vector2(w, h / 2f), new Vector2(w - p, h), new Vector2(p, h), new Vector2(0, h / 2f) };
        }

        public static Vector2[] Polygon(int size, int sides, float rotDeg)
        {
            var pts = new Vector2[sides];
            float r = size * 0.5f - 1f;
            for (int i = 0; i < sides; i++)
            {
                float a = (rotDeg + i * 360f / sides) * Mathf.Deg2Rad;
                pts[i] = new Vector2(size * 0.5f + Mathf.Cos(a) * r, size * 0.5f + Mathf.Sin(a) * r);
            }
            return pts;
        }

        /// Banderola: banda horizontal con cola en V en los dos extremos.
        public static Vector2[] Banner(int w, int h, float notch)
        {
            float y0 = h * 0.1f, y1 = h * 0.92f, ym = (y0 + y1) * 0.5f;
            return new[] { new Vector2(0, y0), new Vector2(w, y0), new Vector2(w - notch, ym), new Vector2(w, y1), new Vector2(0, y1), new Vector2(notch, ym) };
        }

        // ------------------------------------------------------------------ pintor
        /// Rellena el polígono con facetas: color de <paramref name="bottom"/> (abajo) a <paramref name="top"/> (arriba),
        /// cada triángulo con un tono un poco distinto; borde de <paramref name="borderW"/> px del color <paramref name="border"/>.
        public static Sprite Paint(string key, int w, int h, Vector2[] shape, Color top, Color bottom, Color border, int borderW, int seed,
                                   float jitter = 0.07f, Vector4 slice = default, int cell = 40)
        {
            if (cache.TryGetValue(key, out var sp) && sp != null) return sp;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "LowPoly_" + key };
            var px = new Color[w * h];

            // facetas: rejilla de puntos desplazados → triángulos con su tono (se rellena triángulo a triángulo)
            var rng = new System.Random(seed);
            int cols = Mathf.Max(2, Mathf.RoundToInt(w / (float)cell)), rows = Mathf.Max(2, Mathf.RoundToInt(h / (float)cell));
            var pts = new Vector2[(cols + 1) * (rows + 1)];
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= cols; x++)
            {
                float jx = (x == 0 || x == cols) ? 0 : ((float)rng.NextDouble() - .5f) * 0.75f;
                float jy = (y == 0 || y == rows) ? 0 : ((float)rng.NextDouble() - .5f) * 0.75f;
                pts[y * (cols + 1) + x] = new Vector2((x + jx) * w / (float)cols, (y + jy) * h / (float)rows);
            }
            var tone = new float[w * h];
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                Vector2 p00 = pts[y * (cols + 1) + x], p10 = pts[y * (cols + 1) + x + 1], p01 = pts[(y + 1) * (cols + 1) + x], p11 = pts[(y + 1) * (cols + 1) + x + 1];
                if (rng.NextDouble() < 0.5) { FillTri(tone, w, h, p00, p10, p11, Jit(rng, jitter)); FillTri(tone, w, h, p00, p11, p01, Jit(rng, jitter)); }
                else { FillTri(tone, w, h, p00, p10, p01, Jit(rng, jitter)); FillTri(tone, w, h, p10, p11, p01, Jit(rng, jitter)); }
            }

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f);
                float d = EdgeDistance(shape, p);
                bool inside = PointInPoly(shape, p);
                float a = inside ? Mathf.Clamp01(d + 0.5f) : Mathf.Clamp01(0.5f - d);
                if (a <= 0f) { px[y * w + x] = new Color(1, 1, 1, 0); continue; }
                Color c = Color.Lerp(bottom, top, y / (float)(h - 1));
                float t = tone[y * w + x];
                c = new Color(Mathf.Clamp01(c.r * (1f + t)), Mathf.Clamp01(c.g * (1f + t)), Mathf.Clamp01(c.b * (1f + t)), c.a);
                if (borderW > 0 && inside && d < borderW + 0.5f) c = Color.Lerp(border, c, Mathf.Clamp01(d - borderW + 0.5f));
                c.a *= a;
                px[y * w + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, slice);
            cache[key] = sp;
            return sp;
        }

        static float Jit(System.Random r, float j) => ((float)r.NextDouble() - .5f) * 2f * j;

        static void FillTri(float[] buf, int w, int h, Vector2 a, Vector2 b, Vector2 c, float v)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)))), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (InTri(new Vector2(x + .5f, y + .5f), a, b, c)) buf[y * w + x] = v;
        }

        public static bool PointInPoly(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) c = !c;
            return c;
        }

        public static float EdgeDistance(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        // ------------------------------------------------------------------ piezas (problema matemático)
        /// Sprite facetado en gris claro (se tiñe con Image.color): un mismo sprite sirve para normal, acierto y fallo.
        public static Sprite Facet(int w, int h, int chamfer, int border, int seed, float jitter = 0.08f, bool pointy = false)
        {
            var shape = pointy ? Pointy(w, h, chamfer) : Chamfer(w, h, chamfer);
            Color g = new Color(0.9f, 0.9f, 0.9f, 1f), gTop = new Color(0.97f, 0.97f, 0.97f, 1f);
            return Paint($"facet{w}x{h}c{chamfer}b{border}s{seed}j{jitter}p{pointy}", w, h, shape, gTop, g, Color.white, border, seed, jitter, default, 56);
        }

        public static Image Panel(string name, Transform parent, int w, int h, Color tint, int chamfer = 22, int border = 3, int seed = 1, bool pointy = false, float jitter = 0.08f)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = Facet(w, h, chamfer, border, seed, jitter, pointy);
            img.color = tint;
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(w, h);
            return img;
        }

        /// Texto nítido: un solo efecto (sombra suave de 2 px). Sin contorno doble.
        public static Text Text(Transform parent, string name, string text, int size, Color color, bool bold = true, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var t = UIText.Make(parent, name, text, size, color, anchor);
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.45f);
            s.effectDistance = new Vector2(0f, -2f);
            return t;
        }

        /// Botón facetado hexagonal. El color de estado se aplica a la imagen.
        public static Button TileButton(string name, Transform parent, int w, int h, int seed, out Text label)
        {
            var img = Panel(name, parent, w, h, Tile, h / 2, 3, seed, true, 0.07f);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.selectedColor = Color.white; cb.disabledColor = Color.white; cb.colorMultiplier = 1.2f; cb.fadeDuration = 0.08f;
            b.colors = cb;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            label = Text(img.transform, "Label", "", 36, Cream);
            MenuStyle.Stretch(label.rectTransform);
            return b;
        }
    }
}
