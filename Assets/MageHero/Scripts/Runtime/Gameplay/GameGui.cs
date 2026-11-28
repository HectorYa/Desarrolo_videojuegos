using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MageHero
{
    /// Kit visual "Low Poly Game GUI" (Diseños/MenuPrincipal/image.png): todo generado por código, sin imágenes.
    ///  · Esquinas escalonadas tipo pixel, franja clara arriba y canto oscuro abajo (relieve), sin brillos de plástico.
    ///  · Panel claro color menta con borde oscuro; variante oscura (azul petróleo) para ventanas con texto blanco.
    ///  · Cinta azul con pliegues en los extremos para los títulos.
    ///  · Botones de color sólido con un bloque de icono a la izquierda y el texto centrado en el resto.
    ///  · Deslizadores, interruptores y un fondo azul petróleo con facetas low poly.
    public static class GameGui
    {
        // Paleta del diseño
        public static readonly Color Yellow = Hex(0xf6b42c), Green = Hex(0x2fcf7a), Purple = Hex(0x9a62d8), Orange = Hex(0xf2913a);
        public static readonly Color Blue = Hex(0x3fb3f2), Red = Hex(0xe8574a), Teal = Hex(0x1aa7a1);
        public static readonly Color Ink = Hex(0x17323b);              // texto oscuro sobre el panel claro
        public static readonly Color InkSoft = Hex(0x4d6d75);
        public static readonly Color Mint = Hex(0xe4f5f2);
        public static readonly Color DeepTeal = Hex(0x0f2a33);

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        public static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        // ================================================================== sprites
        /// Relieve genérico: rectángulo con esquinas escalonadas, franja superior, cuerpo y canto inferior.
        static Sprite Bevel(string key, int w, int h, int notch, Color top, Color body, Color bottom, int topBand, int bottomBand,
                            Color outline, int outlineW, Vector4 border)
        {
            if (cache.TryGetValue(key, out var sp) && sp != null) return sp;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = key };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Inside(x, y, w, h, notch)) { px[y * w + x] = Color.clear; continue; }
                    bool edge = outlineW > 0 && (!Inside(x - outlineW, y, w, h, notch) || !Inside(x + outlineW, y, w, h, notch) ||
                                                 !Inside(x, y - outlineW, w, h, notch) || !Inside(x, y + outlineW, w, h, notch));
                    Color c = edge ? outline : (y < bottomBand + outlineW ? bottom : (y >= h - topBand - outlineW ? top : body));
                    px[y * w + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            return cache[key] = sp;
        }

        /// Esquina escalonada (dos peldaños) como en el diseño pixel.
        static bool Inside(int x, int y, int w, int h, int n)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y);
            if (dx < n / 2 && dy < n) return false;
            if (dx < n && dy < n / 2) return false;
            return true;
        }

        /// Botón en escala de grises (se tiñe con Image.color: así el reto puede pintarlo de verde/rojo al responder).
        public static Sprite ButtonSprite() =>
            Bevel("btn", 64, 40, 4, new Color(1f, 1f, 1f), new Color(0.86f, 0.86f, 0.86f), new Color(0.58f, 0.58f, 0.58f), 4, 6, Color.clear, 0, new Vector4(8, 9, 8, 7));

        /// Panel claro (menta) con borde oscuro, franja blanca arriba y canto verdoso abajo.
        public static Sprite PanelLight() =>
            Bevel("panelL", 96, 96, 6, Color.white, Mint, Hex(0xa9d3cc), 6, 10, Hex(0x153038), 3, new Vector4(14, 18, 14, 14));

        /// Panel oscuro (azul petróleo) para ventanas con texto claro.
        public static Sprite PanelDark() =>
            Bevel("panelD", 96, 96, 6, Hex(0x2b5866), Hex(0x183842), Hex(0x0c2128), 5, 9, Hex(0x07161b), 3, new Vector4(14, 18, 14, 14));

        /// Caja interior hundida (la operación del reto, la pista de un deslizador).
        public static Sprite Inset() =>
            Bevel("inset", 48, 32, 4, Hex(0x0b1f26), Hex(0x14323b), Hex(0x1f4651), 3, 3, Hex(0x07161b), 2, new Vector4(7, 7, 7, 7));

        /// Cinta de título con pliegues: banda principal y dos colas más oscuras y más bajas a los lados.
        public static Sprite RibbonSprite(Color fill)
        {
            string key = "rib" + ColorUtility.ToHtmlStringRGB(fill);
            if (cache.TryGetValue(key, out var sp) && sp != null) return sp;
            const int w = 160, h = 48, tail = 14, drop = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = key };
            var px = new Color[w * h];
            Color top = Color.Lerp(fill, Color.white, 0.35f), bottom = Shade(fill, 0.72f), tailC = Shade(fill, 0.6f), fold = Shade(fill, 0.42f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;
                    bool inTail = (x < tail + 4 || x >= w - tail - 4) && y < h - drop - 6 && y >= 0;
                    bool notchCut = (x < 4 || x >= w - 4) && (y < 4 || y > h - drop - 11);
                    if (inTail && !notchCut) c = (x >= tail && x < tail + 4) || (x < w - tail && x >= w - tail - 4) ? fold : tailC;
                    bool inBand = x >= tail && x < w - tail && y >= drop;
                    if (inBand)
                    {
                        int by = y - drop, bh = h - drop;
                        c = by < 5 ? bottom : (by >= bh - 5 ? top : fill);
                    }
                    px[y * w + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(tail + 6, drop + 6, tail + 6, 6));
            return cache[key] = sp;
        }

        /// Fondo azul petróleo con grandes facetas triangulares y viñeta (como el del diseño).
        public static Texture2D Background(int w = 480, int h = 270, int seed = 11)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            var rnd = new System.Random(seed);
            const int cell = 70;
            int cols = w / cell + 2, rows = h / cell + 2;
            var pts = new Vector2[(cols + 1) * (rows + 1)];
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= cols; x++)
                    pts[y * (cols + 1) + x] = new Vector2((x - 0.5f + (float)(rnd.NextDouble() - .5) * 0.9f) * cell, (y - 0.5f + (float)(rnd.NextDouble() - .5) * 0.9f) * cell);
            Color a0 = Hex(0x0d2730), a1 = Hex(0x14404a);
            for (int i = 0; i < px.Length; i++) px[i] = a0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    Vector2 a = pts[y * (cols + 1) + x], b = pts[y * (cols + 1) + x + 1], c = pts[(y + 1) * (cols + 1) + x], d = pts[(y + 1) * (cols + 1) + x + 1];
                    FillTri(px, w, h, a, b, d, Color.Lerp(a0, a1, (float)rnd.NextDouble()));
                    FillTri(px, w, h, a, d, c, Color.Lerp(a0, a1, (float)rnd.NextDouble()));
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w - 0.5f, v = y / (float)h - 0.5f;
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt((u - 0.18f) * (u - 0.18f) + v * v * 1.6f) * 1.6f) * 0.12f;   // halo detrás del título
                    float vig = Mathf.Clamp01((u * u + v * v) * 1.7f) * 0.55f;
                    Color c = px[y * w + x];
                    px[y * w + x] = new Color(c.r * (1 - vig) + glow * 0.3f, c.g * (1 - vig) + glow * 0.8f, c.b * (1 - vig) + glow, 1f);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        static void FillTri(Color[] px, int w, int h, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int x0 = Mathf.Max(0, (int)Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.Min(w - 1, (int)Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1);
            int y0 = Mathf.Max(0, (int)Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.Min(h - 1, (int)Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + .5f, y + .5f);
                    float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
                    float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
                    float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) px[y * w + x] = col;
                }
        }

        // ================================================================== iconos (dibujados en píxeles, blancos)
        public enum Icon { Play, Levels, Settings, Keys, Star, Close, Save, Back, Restart, Map, Home, Sound, Camera, Screen, Number1, Number2, Number3, Number4, None }

        public static Sprite IconSprite(Icon icon)
        {
            string key = icon.ToString();
            if (icons.TryGetValue(key, out var sp) && sp != null) return sp;
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ico" + key };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = On(icon, x, n - 1 - y) ? Color.white : Color.clear;    // (x, y) con y hacia abajo, como se dibuja
            tex.SetPixels(px); tex.Apply();
            sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
            return icons[key] = sp;
        }

        static bool On(Icon icon, int x, int y)
        {
            switch (icon)
            {
                case Icon.Play: return x >= 4 && x <= 12 && Mathf.Abs(y - 7.5f) <= (13 - x) * 0.75f;      // punta a la derecha
                case Icon.Back: return x >= 3 && x <= 11 && Mathf.Abs(y - 7.5f) <= (x - 2) * 0.75f;       // punta a la izquierda
                case Icon.Levels: return (x >= 2 && x <= 6 || x >= 9 && x <= 13) && (y >= 2 && y <= 6 || y >= 9 && y <= 13);
                case Icon.Settings:   // tres deslizadores
                    return (y == 3 || y == 8 || y == 13) && x >= 1 && x <= 14 ||
                           (x >= 9 && x <= 11 && y >= 1 && y <= 5) || (x >= 3 && x <= 5 && y >= 6 && y <= 10) || (x >= 7 && x <= 9 && y >= 11 && y <= 15);
                case Icon.Keys:       // teclado
                    return (y >= 4 && y <= 12 && (x == 1 || x == 14) && true) || ((y == 4 || y == 12) && x >= 1 && x <= 14) ||
                           ((y == 7 || y == 9) && (x == 4 || x == 7 || x == 10)) || (y == 10 && x >= 5 && x <= 10);
                case Icon.Star:
                {
                    float cx = x - 7.5f, cy = y - 8f, a = Mathf.Atan2(cy, cx), r = Mathf.Sqrt(cx * cx + cy * cy);
                    float k = Mathf.Cos(5f * (a + Mathf.PI / 2f)) * 0.5f + 0.5f;
                    return r <= Mathf.Lerp(3.2f, 7.5f, k);
                }
                case Icon.Close: return (Mathf.Abs(x - y) <= 1 || Mathf.Abs(x - (15 - y)) <= 1) && x >= 2 && x <= 13 && y >= 2 && y <= 13;
                case Icon.Save:       // disquete
                    return x >= 2 && x <= 13 && y >= 2 && y <= 13 && !(x >= 12 && y <= 3)
                           && !(x >= 4 && x <= 11 && y >= 9 && y <= 12) && !(x >= 5 && x <= 9 && y >= 3 && y <= 5);
                case Icon.Restart:
                {
                    float cx = x - 7.5f, cy = y - 7.5f, r = Mathf.Sqrt(cx * cx + cy * cy);
                    bool ring = r >= 4f && r <= 6.2f && !(cx > 0 && cy < 0 && cx > -cy * 0.2f);
                    bool head = x >= 10 && x <= 14 && y >= 2 && y <= 6 && (x - 10) + (6 - y) <= 4;
                    return ring || head;
                }
                case Icon.Map: return y >= 3 && y <= 12 && x >= 1 && x <= 14 && !(x == 5 || x == 10) && !(y == 3 && (x < 5 || x > 10)) || ((x == 5 || x == 10) && y >= 2 && y <= 13);
                case Icon.Home: return (y >= 8 && y <= 14 && x >= 3 && x <= 12 && !(x >= 6 && x <= 9 && y >= 10)) || (y >= 1 && y < 8 && Mathf.Abs(x - 7.5f) <= y);
                case Icon.Sound: return (x >= 2 && x <= 4 && y >= 6 && y <= 9) || (x >= 5 && x <= 8 && Mathf.Abs(y - 7.5f) <= (x - 3)) || (x == 11 && y >= 5 && y <= 10) || (x == 13 && y >= 3 && y <= 12);
                case Icon.Camera: return (x >= 1 && x <= 14 && y >= 5 && y <= 13 && !((x - 7.5f) * (x - 7.5f) + (y - 9f) * (y - 9f) < 6f)) || (x >= 5 && x <= 10 && y >= 3 && y < 5);
                case Icon.Screen: return (x >= 1 && x <= 14 && y >= 2 && y <= 11 && (x <= 2 || x >= 13 || y <= 3 || y >= 10)) || (x >= 6 && x <= 9 && y >= 12 && y <= 13) || (x >= 4 && x <= 11 && y == 14);
                default: return false;
            }
        }

        // ================================================================== widgets
        public static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>();
            im.sprite = sprite; im.color = color; im.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) im.type = Image.Type.Sliced;
            return im;
        }

        /// Texto centrado, en negrita, con una sola sombra suave.
        public static Text Text(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool shadow = true)
        {
            var t = UIText.Make(parent, name, text, size, color, anchor);
            t.fontStyle = FontStyle.Bold;
            if (shadow)
            {
                var s = t.gameObject.AddComponent<Shadow>();
                s.effectColor = color.grayscale > 0.6f ? new Color(0f, 0f, 0f, 0.45f) : new Color(1f, 1f, 1f, 0.35f);
                s.effectDistance = new Vector2(0f, -Mathf.Clamp(size * 0.07f, 1.5f, 3f));
            }
            return t;
        }

        /// Ajuste automático: el texto se reduce para caber (nunca se sale ni se corta).
        public static Text Fit(Text t, int min, int max)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = max; t.fontSize = max;
            return t;
        }

        public static Image Panel(Transform parent, string name, Vector2 size, bool light = true)
        {
            var p = Img(name, parent, light ? PanelLight() : PanelDark(), Color.white);
            p.rectTransform.sizeDelta = size;
            var sh = p.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .45f); sh.effectDistance = new Vector2(0, -8);
            return p;
        }

        /// Cinta de título (texto en mayúsculas, centrado).
        public static Image Ribbon(Transform parent, string text, Vector2 size, Color? color = null, int fontSize = 30)
        {
            var r = Img("Cinta", parent, RibbonSprite(color ?? Blue), Color.white);
            r.rectTransform.sizeDelta = size;
            var t = Fit(Text(r.transform, "Titulo", text.ToUpperInvariant(), fontSize, Color.white), 14, fontSize);
            MenuStyle.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(size.y * 0.55f, size.y * 0.2f);
            t.rectTransform.offsetMax = new Vector2(-size.y * 0.55f, -2f);
            return r;
        }

        /// Botón del diseño: color sólido con relieve, bloque de icono a la izquierda y texto centrado en el resto.
        public static Button Button(Transform parent, string name, string text, Color color, Icon icon, System.Action onClick, int fontSize = 24, Vector2? size = null)
        {
            Vector2 sz = size ?? new Vector2(320, 56);
            var img = Img(name, parent, ButtonSprite(), color);
            img.raycastTarget = true;
            img.rectTransform.sizeDelta = sz;
            var sh = img.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .35f); sh.effectDistance = new Vector2(0, -4);

            float iconW = 0f;
            if (icon != Icon.None)
            {
                iconW = Mathf.Round(sz.y * 1.05f);
                var block = Img("BloqueIcono", img.transform, ButtonSprite(), Shade(color, 0.8f));
                var brt = block.rectTransform;
                brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(0, 1); brt.pivot = new Vector2(0, .5f);
                brt.offsetMin = new Vector2(0, 0); brt.offsetMax = new Vector2(iconW, 0);
                block.gameObject.AddComponent<TintFollower>().source = img;
                if (icon >= Icon.Number1 && icon <= Icon.Number4)
                {
                    // respuestas del reto: el bloque muestra la tecla (1–4)
                    var num = Text(block.transform, "Tecla", ((int)(icon - Icon.Number1) + 1).ToString(), Mathf.RoundToInt(sz.y * 0.48f), Color.white);
                    MenuStyle.Stretch(num.rectTransform); num.rectTransform.offsetMin = new Vector2(0, 6); num.rectTransform.offsetMax = new Vector2(0, -2);
                }
                else
                {
                    var ic = Img("Icono", block.transform, IconSprite(icon), new Color(1f, 1f, 1f, 0.92f));
                    ic.preserveAspect = true;
                    var irt = ic.rectTransform; irt.anchorMin = irt.anchorMax = new Vector2(.5f, .5f);
                    irt.anchoredPosition = new Vector2(0, 2); irt.sizeDelta = Vector2.one * Mathf.Round(sz.y * 0.48f);
                }
            }
            var label = Fit(Text(img.transform, "Label", text, fontSize, Color.white), 12, fontSize);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(iconW + 10f, 6f);           // centrado en la parte libre (sin el icono) y sobre el canto inferior
            label.rectTransform.offsetMax = new Vector2(-10f, -3f);

            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f); cb.pressedColor = new Color(.8f, .8f, .8f);
            cb.selectedColor = Color.white; cb.disabledColor = new Color(0.75f, 0.75f, 0.75f); cb.colorMultiplier = 1.15f; cb.fadeDuration = 0.06f;
            b.colors = cb;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            img.gameObject.AddComponent<ButtonBounce>().hoverScale = 1.04f;
            return b;
        }

        public static Text Label(Button b) => b.transform.Find("Label")?.GetComponent<Text>();

        /// Deslizador 0..1 con pista hundida, relleno de color y tirador blanco.
        public static Slider Slider(Transform parent, string name, Color fill, float value, System.Action<float> onChange, Vector2 size)
        {
            var root = Img(name, parent, Inset(), Color.white);
            root.rectTransform.sizeDelta = size;
            root.raycastTarget = true;
            var s = root.gameObject.AddComponent<Slider>();
            var area = MenuStyle.Rect("Relleno", root.transform);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(5, 5); area.offsetMax = new Vector2(-5, -5);
            var f = Img("Barra", area, ButtonSprite(), fill);
            f.rectTransform.anchorMin = Vector2.zero; f.rectTransform.anchorMax = new Vector2(0, 1); f.rectTransform.offsetMin = f.rectTransform.offsetMax = Vector2.zero;
            var handleArea = MenuStyle.Rect("ZonaTirador", root.transform);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one; handleArea.offsetMin = new Vector2(10, 0); handleArea.offsetMax = new Vector2(-10, 0);
            var h = Img("Tirador", handleArea, ButtonSprite(), Color.white);
            h.raycastTarget = true;
            h.rectTransform.sizeDelta = new Vector2(16, size.y + 4);
            s.fillRect = f.rectTransform; s.handleRect = h.rectTransform; s.targetGraphic = h;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight; s.minValue = 0f; s.maxValue = 1f;
            var nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;
            s.SetValueWithoutNotify(value);
            if (onChange != null) s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        /// Fila de ajuste: etiqueta (izquierda), control (centro) y valor en una cajita (derecha), todo alineado en columnas.
        public static RectTransform Row(Transform parent, string label, float y, float width, out RectTransform control, out Text value, Color? textColor = null)
        {
            var row = MenuStyle.Rect("Fila_" + label, parent);
            MenuStyle.Place(row, new Vector2(.5f, 1f), new Vector2(0, y), new Vector2(width, 44));
            var l = Fit(Text(row, "Etiqueta", label, 22, textColor ?? Ink, TextAnchor.MiddleLeft, false), 14, 22);
            l.rectTransform.anchorMin = new Vector2(0, 0); l.rectTransform.anchorMax = new Vector2(0.36f, 1); l.rectTransform.offsetMin = new Vector2(8, 0); l.rectTransform.offsetMax = Vector2.zero;
            control = MenuStyle.Rect("Control", row);
            control.anchorMin = new Vector2(0.37f, 0.5f); control.anchorMax = new Vector2(0.83f, 0.5f); control.sizeDelta = new Vector2(0, 30); control.anchoredPosition = Vector2.zero;
            var box = Img("Valor", row, Inset(), Color.white);
            box.rectTransform.anchorMin = new Vector2(0.86f, 0.5f); box.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(0, 36); box.rectTransform.anchoredPosition = Vector2.zero;
            value = Fit(Text(box.transform, "Texto", "", 18, Color.white), 12, 18);
            MenuStyle.Stretch(value.rectTransform, 3f);
            return row;
        }

        /// Encabezado de sección centrado (pequeña cinta de color con icono).
        public static void Section(Transform parent, string text, Icon icon, float y, Color color)
        {
            var bar = Img("Seccion_" + text, parent, ButtonSprite(), color);
            MenuStyle.Place(bar.rectTransform, new Vector2(.5f, 1f), new Vector2(0, y), new Vector2(250, 34));
            var ic = Img("Icono", bar.transform, IconSprite(icon), Color.white);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0, .5f);
            ic.rectTransform.anchoredPosition = new Vector2(22, 2); ic.rectTransform.sizeDelta = new Vector2(18, 18);
            var t = Fit(Text(bar.transform, "Texto", text.ToUpperInvariant(), 18, Color.white), 11, 18);
            MenuStyle.Stretch(t.rectTransform); t.rectTransform.offsetMin = new Vector2(36, 4); t.rectTransform.offsetMax = new Vector2(-12, -2);
        }

        /// Fondo oscuro a pantalla completa que bloquea los clics al juego.
        public static Image Dim(Transform parent, string name, float alpha = 0.6f)
        {
            var d = Img(name, parent, null, new Color(0.02f, 0.07f, 0.09f, alpha));
            d.raycastTarget = true;
            MenuStyle.Stretch(d.rectTransform);
            return d;
        }
    }

    /// El bloque de icono copia el color del botón (más oscuro), también cuando el reto lo pinta de verde o rojo.
    public class TintFollower : MonoBehaviour
    {
        public Image source;
        Image self;
        void Awake() => self = GetComponent<Image>();
        void LateUpdate()
        {
            if (source == null || self == null) return;
            Color c = source.color;
            self.color = new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f, c.a);
        }
    }
}
