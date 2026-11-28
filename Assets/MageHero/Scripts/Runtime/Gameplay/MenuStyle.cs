using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MageHero
{
    /// Gráficos de la interfaz generados por código. Los botones, cintas y ventanas usan el kit GameGui
    /// (estilo "Low Poly Game GUI" de Diseños/MenuPrincipal); aquí quedan los paneles del HUD, barras y utilidades.
    public static class MenuStyle
    {
        public static readonly Color Orange = Hex(0xe8863a), OrangeDark = Hex(0xa9521d);
        public static readonly Color Red = Hex(0xd45a43), RedDark = Hex(0x8f3324);
        public static readonly Color Green = Hex(0x6da84e), GreenDark = Hex(0x3f6e2e);
        public static readonly Color Purple = Hex(0x8b62c2), PurpleDark = Hex(0x553a80);
        public static readonly Color Blue = Hex(0x4b8fc4), BlueDark = Hex(0x2b5c86);
        public static readonly Color Panel = new Color(0.10f, 0.13f, 0.17f, 0.88f);

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        public static Color Darker(Color c, float k = 0.62f) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        // ------------------------------------------------------------------ Sprites
        /// Panel con esquinas en bisel (low poly), degradado vertical facetado y borde. Sprite 9-slice
        /// (los parámetros se llaman igual que la versión redondeada anterior para no romper a nadie).
        public static Sprite RoundedRect(Color top, Color bottom, Color border, int radius = 26, int borderWidth = 4, int w = 128, int h = 96)
        {
            string key = $"rr{top}{bottom}{border}{radius}{borderWidth}{w}{h}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            float c = Mathf.Max(4f, radius * 0.8f);
            float b = c + borderWidth + 1f;
            var sp = LowPolyUI.Paint(key, w, h, LowPolyUI.Chamfer(w, h, c), top, bottom, border, borderWidth, key.GetHashCode(), 0.06f, new Vector4(b, b, b, b), 26);
            return Cache[key] = sp;
        }

        /// Bisel iluminado de la mitad superior de un botón (una franja plana y tenue, no un brillo de plástico).
        public static Sprite Gloss(int radius = 20, int w = 96, int h = 48)
        {
            string key = $"gl{radius}{w}{h}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            float c = Mathf.Max(3f, radius * 0.7f);
            var sp = LowPolyUI.Paint(key, w, h, LowPolyUI.Chamfer(w, h, c), new Color(1, 1, 1, 0.14f), new Color(1, 1, 1, 0.03f), Color.white, 0, 3, 0.25f,
                                     new Vector4(c + 1, c + 1, c + 1, c + 1), 24);
            return Cache[key] = sp;
        }

        /// Botón redondo → octógono facetado (como una gema tallada).
        public static Sprite Circle(Color top, Color bottom, Color border, int size = 128, int borderWidth = 6)
        {
            string key = $"ci{top}{bottom}{border}{size}{borderWidth}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var sp = LowPolyUI.Paint(key, size, size, LowPolyUI.Polygon(size, 8, 22.5f), top, bottom, border, Mathf.Max(2, borderWidth - 2), 17, 0.09f, default, 30);
            return Cache[key] = sp;
        }

        /// Banderola del título: banda recta facetada con cola en V en los dos extremos.
        public static Sprite Ribbon(Color fill, Color border, int w = 512, int h = 128) => GameGui.RibbonSprite(fill);   // cinta con pliegues (GameGui)

        /// Fondo del menú: cielo de atardecer en facetas y tres capas de montañas low poly (como el borde de los mapas).
        public static Texture2D Background(int w = 640, int h = 360, int seed = 7)
        {
            var tex = NewTex(w, h);
            var px = new Color[w * h];
            var rnd = new System.Random(seed);
            Color skyTop = new Color(0.10f, 0.14f, 0.22f), skyLow = new Color(0.36f, 0.30f, 0.33f);

            // cielo facetado (triángulos grandes con tono ligeramente distinto)
            const int cell = 64;
            int cols = w / cell + 1, rows = h / cell + 1;
            var pts = new Vector2[(cols + 1) * (rows + 1)];
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= cols; x++)
                    pts[y * (cols + 1) + x] = new Vector2((x + (float)(rnd.NextDouble() - .5) * 0.8f) * cell, (y + (float)(rnd.NextDouble() - .5) * 0.8f) * cell);
            var tone = new float[w * h];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    Vector2 a = pts[y * (cols + 1) + x], b = pts[y * (cols + 1) + x + 1], c = pts[(y + 1) * (cols + 1) + x], d = pts[(y + 1) * (cols + 1) + x + 1];
                    Tri(tone, w, h, a, b, d, (float)(rnd.NextDouble() - .5) * 0.12f);
                    Tri(tone, w, h, a, d, c, (float)(rnd.NextDouble() - .5) * 0.12f);
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.Lerp(skyLow, skyTop, Mathf.Pow(y / (float)h, 0.8f));
                    float t = tone[y * w + x];
                    px[y * w + x] = new Color(c.r * (1 + t), c.g * (1 + t), c.b * (1 + t), 1f);
                }

            // montañas: tres capas, de la más lejana (clara) a la más cercana (oscura); cada cara con su tono
            Color[] layer = { new Color(0.27f, 0.30f, 0.38f), new Color(0.19f, 0.22f, 0.29f), new Color(0.11f, 0.14f, 0.19f) };
            float[] baseY = { 0.40f, 0.27f, 0.12f }, peak = { 0.28f, 0.24f, 0.2f };
            for (int L = 0; L < 3; L++)
            {
                // cresta en zigzag: cumbre, valle, cumbre… (picos afilados, no bloques)
                var ridge = new List<Vector2>();
                float x = -30f; bool up = rnd.NextDouble() < 0.5;
                while (x < w + 60f)
                {
                    float k = up ? 0.55f + (float)rnd.NextDouble() * 0.45f : (float)rnd.NextDouble() * 0.2f;
                    ridge.Add(new Vector2(x, h * (baseY[L] + k * peak[L])));
                    x += (L == 0 ? 55f : 45f) + (float)rnd.NextDouble() * 60f;
                    up = !up;
                }
                for (int i = 0; i < ridge.Count - 1; i++)
                {
                    Vector2 a = ridge[i], b = ridge[i + 1];
                    // cada tramo: dos triángulos hasta el suelo, cara iluminada (izquierda) y cara en sombra
                    Vector2 a0 = new Vector2(a.x, 0), b0 = new Vector2(b.x, 0);
                    Color lit = layer[L] * 1.12f, shade = layer[L] * 0.9f; lit.a = shade.a = 1f;
                    TriColor(px, w, h, a, b, a0, a.y > b.y ? shade : lit);
                    TriColor(px, w, h, b, b0, a0, a.y > b.y ? shade * 0.95f : lit * 0.95f);
                }
            }
            // viñeta suave
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w - 0.5f, v = y / (float)h - 0.5f;
                    float vig = Mathf.Clamp01((u * u + v * v) * 1.6f) * 0.55f;
                    Color c = px[y * w + x];
                    px[y * w + x] = new Color(c.r * (1 - vig), c.g * (1 - vig), c.b * (1 - vig), 1f);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        static void Tri(float[] buf, int w, int h, Vector2 a, Vector2 b, Vector2 c, float v)
        {
            int x0 = Mathf.Max(0, (int)Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.Min(w - 1, (int)Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1);
            int y0 = Mathf.Max(0, (int)Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.Min(h - 1, (int)Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (InTri(new Vector2(x + .5f, y + .5f), a, b, c)) buf[y * w + x] = v;
        }

        static void TriColor(Color[] px, int w, int h, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int x0 = Mathf.Max(0, (int)Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.Min(w - 1, (int)Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1);
            int y0 = Mathf.Max(0, (int)Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.Min(h - 1, (int)Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (InTri(new Vector2(x + .5f, y + .5f), a, b, c)) px[y * w + x] = col;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ------------------------------------------------------------------ Widgets
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color tint)
        {
            var rt = Rect(name, parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite; im.color = tint; im.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) im.type = UnityEngine.UI.Image.Type.Sliced;
            return im;
        }

        public static Text Label(Transform parent, string text, int size, Color color, Color outline, bool bold = true, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            // Un solo efecto: sombra suave hacia abajo con el color indicado (antes contorno + sombra = letras "dobles" y borrosas).
            var t = UIText.Make(parent, "Label", text, size, color, anchor);
            if (bold) t.fontStyle = FontStyle.Bold;
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(outline.r * 0.6f, outline.g * 0.6f, outline.b * 0.6f, Mathf.Min(0.75f, outline.a));
            s.effectDistance = new Vector2(0f, -Mathf.Clamp(size * 0.06f, 1.5f, 3f));
            return t;
        }

        /// Botón redondeado con brillo, sombra, rebote al pasar el ratón y texto con contorno.
        public static Button GlossyButton(string name, Transform parent, string text, Color color, int fontSize, System.Action onClick)
            => GameGui.Button(parent, name, text, color, GameGui.Icon.None, onClick, fontSize);

        public static Button RoundButton(string name, Transform parent, string symbol, Color color, int size, System.Action onClick)
            => GameGui.Button(parent, name, symbol, color, GameGui.Icon.None, onClick, (int)(size * 0.5f), new Vector2(size, size));

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        // ------------------------------------------------------------------ Tema del juego (HUD y ventanas)
        public static readonly Color Navy = new Color(0.16f, 0.21f, 0.28f, 0.97f), NavyDeep = new Color(0.09f, 0.12f, 0.17f, 0.97f);
        public static readonly Color Gold = Hex(0xf0b33e), Cream = Hex(0xfff1d6);

        /// Fondo de las ventanas: azul pizarra con degradado y borde del color indicado.
        public static Sprite PanelSprite(Color border) => RoundedRect(Navy, NavyDeep, border, 26, 5);
        /// Fondo de los paneles del HUD: más transparente y con borde fino.
        public static Sprite HudSprite(Color border) => RoundedRect(new Color(0.16f, 0.21f, 0.28f, 0.9f), new Color(0.08f, 0.11f, 0.16f, 0.9f), border, 18, 3, 96, 64);
        /// Botón base blanco con brillo: el color final se da con Image.color (permite cambiarlo en vivo: acierto/error).
        public static Sprite TintSprite() => RoundedRect(Color.white, new Color(0.82f, 0.82f, 0.82f), new Color(0.45f, 0.45f, 0.45f), 22, 4);

        /// Ventana emergente del juego: fondo oscuro, panel redondeado, cinta con el título y botón X (si onClose != null).
        /// Devuelve el panel, donde se coloca el contenido. root = objeto a activar/desactivar.
        public static RectTransform Window(Transform parent, string name, string title, Vector2 size, Color ribbon, System.Action onClose, out GameObject root, bool dim = true)
        {
            var overlay = Image(name, parent, null, new Color(0.02f, 0.07f, 0.09f, dim ? 0.62f : 0f));
            overlay.raycastTarget = true;                                    // bloquea los clics al juego
            Stretch(overlay.rectTransform);
            root = overlay.gameObject;

            var box = GameGui.Panel(overlay.transform, "Box", size, light: false);
            Place(box.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, -18f), size);

            if (!string.IsNullOrEmpty(title))
            {
                var r = GameGui.Ribbon(box.transform, title, new Vector2(Mathf.Min(size.x * 0.75f, 560f), 70f), ribbon, 32);
                r.name = "Ribbon";                                           // otros scripts buscan la cinta por este nombre (p. ej. el pergamino)
                Place(r.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, 10f), new Vector2(Mathf.Min(size.x * 0.75f, 560f), 70f));
            }
            if (onClose != null) CloseButton(box.transform, onClose);
            return box.rectTransform;
        }

        /// Botón redondo rojo con una X (arriba a la derecha del panel).
        public static Button CloseButton(Transform panel, System.Action onClick, float size = 58f)
        {
            var b = RoundButton("CloseX", panel, "X", Red, (int)size, onClick);
            Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(size, size));
            return b;
        }

        /// Botón brillante cuyo color se cambia con Image.color (respuestas del quiz, botones del HUD).
        public static Button TintButton(string name, Transform parent, string text, Color tint, int fontSize, System.Action onClick, Color? textColor = null)
        {
            var b = GameGui.Button(parent, name, text, tint, GameGui.Icon.None, onClick, fontSize);
            var l = GameGui.Label(b); if (l != null && textColor.HasValue) l.color = textColor.Value;
            return b;
        }

        /// Panel del HUD (vida, nivel, zona...).
        public static Image HudPanel(string name, Transform parent, Color border) => Image(name, parent, HudSprite(border), Color.white);

        /// Barra redondeada (fondo + relleno). Ajusta el relleno con fill.anchorMax.x (0..1) o con un Slider.
        public static Image Bar(string name, Transform parent, Color fillColor, out RectTransform fill)
        {
            var bg = Image(name, parent, RoundedRect(new Color(0.03f, 0.05f, 0.08f, 0.95f), new Color(0.08f, 0.1f, 0.14f, 0.95f), new Color(0f, 0f, 0f, 0.9f), 9, 2, 48, 24), Color.white);
            var inner = Rect("Inner", bg.transform);                         // margen interior: el relleno va de 0 a 1 dentro
            Stretch(inner, 3f);
            var f = Image("Fill", inner, RoundedRect(Color.white, new Color(0.78f, 0.78f, 0.78f), new Color(1f, 1f, 1f, 0f), 7, 0, 40, 18), fillColor);
            fill = f.rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            return bg;
        }

        /// Color de acento del nivel cargado (el de su tarjeta en el menú).
        public static Color LevelAccent(string levelId)
        {
            foreach (var d in LevelMenu.Entries())
                if (d.id == levelId && ColorUtility.TryParseHtmlString(d.accent, out var c)) return c;
            return Orange;
        }

        // ------------------------------------------------------------------ Internos
        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static float RoundedDist(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r), qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }

    /// Sonido de botón: "tic" al pasar el ratón (si está activo) y clic al pulsarlo.
    /// Usar ButtonSound.Add(boton) DESPUÉS de cualquier onClick.RemoveAllListeners().
    public class ButtonSound : MonoBehaviour, IPointerEnterHandler
    {
        Button button;

        public static void Add(Button b)
        {
            if (b == null || b.GetComponent<ButtonSound>() != null) return;
            b.gameObject.AddComponent<ButtonSound>().button = b;
            b.onClick.AddListener(MageAudio.PlayUIClick);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (button == null || button.interactable) MageAudio.PlayUIHover();
        }
    }

    /// Rebote suave del botón al pasar el ratón y al pulsarlo (funciona aunque el juego esté en pausa).
    public class ButtonBounce : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.07f, pressScale = 0.95f;
        float target = 1f;
        bool over;

        void Awake() => ButtonSound.Add(GetComponent<Button>());

        public void OnPointerEnter(PointerEventData e) { over = true; target = hoverScale; }
        public void OnPointerExit(PointerEventData e) { over = false; target = 1f; }
        public void OnPointerDown(PointerEventData e) { target = pressScale; }
        public void OnPointerUp(PointerEventData e) { target = over ? hoverScale : 1f; }
        void OnDisable() { over = false; target = 1f; transform.localScale = Vector3.one; }

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
