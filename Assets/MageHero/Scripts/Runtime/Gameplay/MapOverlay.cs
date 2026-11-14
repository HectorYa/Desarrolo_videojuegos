using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MageHero
{
    /// Mapa completo del nivel (tecla M). La interfaz se construye al iniciar a partir de LevelInfo,
    /// así funciona igual en las tres escenas sin tener que regenerarlas:
    /// marco redondeado con el color de la estación, nombres de zona sin encimarse, zona actual resaltada,
    /// marcador del jugador con dirección, enemigos en vivo, brújula y leyenda. M o Esc para cerrar.
    public class MapOverlay : MonoBehaviour
    {
        public GameObject panel;
        public RectTransform mapRect, arrow;
        public LevelInfo info;
        public Transform player;

        [Header("Diseño")]
        public float mapSize = 560f;
        public float zoneRadius = 30f;          // mismo radio que usa ZoneHUD para decidir la zona actual

        public static bool IsOpen { get; private set; }
        static int closedFrame = -1;
        /// true si el mapa está abierto o se acaba de cerrar con Esc en este frame (para que Esc no lleve al menú).
        public static bool BlocksEscape => IsOpen || closedFrame == Time.frameCount;

        static readonly Color Navy = new Color(0.114f, 0.208f, 0.341f, 1f);
        static readonly Color NavyDeep = new Color(0.06f, 0.11f, 0.19f, 1f);
        static readonly Color Gold = new Color(1f, 0.718f, 0.012f, 1f);
        static readonly Color EnemyCol = new Color(0.93f, 0.27f, 0.32f, 1f);

        Color accent = Gold;
        RectTransform card, pulse, enemyLayer, playerMarker;
        Text zoneTitle;
        readonly List<ZoneLabel> labels = new List<ZoneLabel>();
        readonly List<Image> enemyDots = new List<Image>();
        readonly List<Transform> enemyBuffer = new List<Transform>();
        float enemyTimer;
        string currentZone;

        class ZoneLabel { public string name; public Vector2 world; public Image pill, dot; public Text text; }

        static Sprite roundSprite, circleSprite, ringSprite, triSprite;

        // ------------------------------------------------------------------ ciclo de vida
        void Awake()
        {
            if (panel == null) return;
            ResolveTheme();
            Build();
            panel.SetActive(false);
            IsOpen = false;
        }

        void OnDestroy() { IsOpen = false; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && panel != null)
            {
                if (kb.mKey.wasPressedThisFrame) Toggle();
                else if (panel.activeSelf && kb.escapeKey.wasPressedThisFrame) { SetOpen(false); closedFrame = Time.frameCount; }
            }
            Refresh();
        }

        void Refresh()
        {
            if (panel == null || !panel.activeSelf || player == null || info == null) return;
            // Si Unity recompiló durante el Play se pierden las referencias internas: se reconstruye la interfaz.
            if (card == null || playerMarker == null || mapRect == null || (labels.Count == 0 && info.zones != null && info.zones.Length > 0))
            { Build(); currentZone = "\u0001"; }
            FitToScreen();

            // Jugador
            Vector2 p = WorldToMap(new Vector2(player.position.x, player.position.z));
            playerMarker.anchoredPosition = p;
            arrow.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
            float t = Time.unscaledTime;
            float k = Mathf.Repeat(t * 0.9f, 1f);
            pulse.localScale = Vector3.one * (1f + k * 1.6f);
            var pc = pulse.GetComponent<Image>().color; pc.a = (1f - k) * 0.8f; pulse.GetComponent<Image>().color = pc;

            // Zona actual
            string zone = NearestZone();
            if (zone != currentZone) { currentZone = zone; RefreshZoneHighlight(); }

            // Enemigos (4 veces por segundo es suficiente)
            enemyTimer -= Time.unscaledDeltaTime;
            if (enemyTimer <= 0f) { enemyTimer = 0.25f; RefreshEnemies(); }
        }

        public void Toggle() { if (panel != null) SetOpen(!panel.activeSelf); }

        /// Escala la tarjeta para que quepa entera en pantallas bajas o angostas.
        void FitToScreen()
        {
            var prt = (RectTransform)panel.transform;
            Vector2 avail = prt.rect.size - new Vector2(24f, 24f), need = card.sizeDelta;
            if (avail.x <= 0f || avail.y <= 0f) return;
            float s = Mathf.Min(1f, avail.x / need.x, avail.y / need.y);
            card.localScale = new Vector3(s, s, 1f);
        }

        void SetOpen(bool open)
        {
            panel.SetActive(open);
            IsOpen = open;
            if (open) { currentZone = "\u0001"; enemyTimer = 0f; Refresh(); }
        }

        // ------------------------------------------------------------------ datos
        void ResolveTheme()
        {
            if (info == null) return;
            foreach (var d in LevelData.All())
                if (d.id == info.levelId && ColorUtility.TryParseHtmlString(d.accent, out var c)) { accent = c; break; }
        }

        string LevelSubtitle()
        {
            if (info == null) return "";
            foreach (var d in LevelData.All())
                if (d.id == info.levelId) return d.lv + " · " + d.topic;
            return "";
        }

        float Half => info != null ? info.worldHalfSize : 108f;

        Vector2 WorldToMap(Vector2 xz)
        {
            float s = mapRect.rect.width, h = Half;
            return new Vector2(xz.x / (2f * h) * s, xz.y / (2f * h) * s);
        }

        string NearestZone()
        {
            string best = null; float bd = zoneRadius;
            var pp = new Vector2(player.position.x, player.position.z);
            foreach (var z in info.zones)
            {
                float d = Vector2.Distance(pp, z.position);
                if (d < bd) { bd = d; best = z.name; }
            }
            return best;
        }

        // ------------------------------------------------------------------ construcción de la interfaz
        void Build()
        {
            EnsureSprites();
            for (int i = panel.transform.childCount - 1; i >= 0; i--) Destroy(panel.transform.GetChild(i).gameObject);
            enemyDots.Clear();

            var bg = panel.GetComponent<Image>();
            if (bg == null) bg = panel.AddComponent<Image>();
            bg.sprite = null; bg.color = new Color(0.02f, 0.05f, 0.1f, 0.78f); bg.raycastTarget = true;
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;

            float S = mapSize, border = 10f, sideW = 210f, margin = 20f, gap = 24f;
            float frameW = S + 2 * border;
            float cardW = margin + frameW + gap + sideW + margin, cardH = frameW + 92f;

            // Tarjeta contenedora
            var cardImg = Img("Card", panel.transform, NavyDeep, roundSprite);
            card = cardImg.rectTransform;
            Place(card, new Vector2(.5f, .5f), Vector2.zero, new Vector2(cardW, cardH));
            var cardShadow = cardImg.gameObject.AddComponent<Shadow>(); cardShadow.effectColor = new Color(0, 0, 0, .45f); cardShadow.effectDistance = new Vector2(0, -6);

            // Encabezado
            var title = UIText.Make(card.transform, "Title", info != null ? info.levelName : "Mapa", 26, Color.white, TextAnchor.UpperLeft);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(520f, 32f), new Vector2(0f, 1f));
            var sub = UIText.Make(card.transform, "Subtitle", LevelSubtitle(), 14, new Color(accent.r, accent.g, accent.b, .95f), TextAnchor.UpperLeft);
            Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -50f), new Vector2(520f, 20f), new Vector2(0f, 1f));
            var close = UIText.Make(card.transform, "CloseHint", "M / Esc  cerrar", 14, new Color(1f, 1f, 1f, .6f), TextAnchor.UpperRight);
            Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(200f, 20f), new Vector2(1f, 1f));

            // Marco del mapa (detrás) + máscara redondeada + textura
            float mapX = -cardW / 2f + margin + frameW / 2f, mapY = -cardH / 2f + margin + frameW / 2f - 4f;
            var frame = Img("MapFrame", card.transform, accent, roundSprite);
            Place(frame.rectTransform, new Vector2(.5f, .5f), new Vector2(mapX, mapY), new Vector2(frameW, frameW));
            var maskImg = Img("MapMask", frame.transform, Color.white, roundSprite);
            Place(maskImg.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(S, S));
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var mapGo = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
            mapGo.transform.SetParent(maskImg.transform, false);
            var raw = mapGo.GetComponent<RawImage>(); raw.texture = info != null ? info.mapTexture : null; raw.raycastTarget = false;
            mapRect = (RectTransform)mapGo.transform;
            Place(mapRect, new Vector2(.5f, .5f), Vector2.zero, new Vector2(S, S));

            // Brújula
            var comp = Img("Compass", frame.transform, NavyDeep, circleSprite);
            Place(comp.rectTransform, new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(44f, 44f));
            var compRing = Img("Ring", comp.transform, accent, ringSprite); compRing.type = Image.Type.Simple;
            Place(compRing.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(44f, 44f));
            var needle = Img("Needle", comp.transform, EnemyCol, triSprite);
            Place(needle.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, 9f), new Vector2(12f, 12f));
            var n = UIText.Make(comp.transform, "N", "N", 15, Color.white); n.fontStyle = FontStyle.Bold;
            Place(n.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, -6f), new Vector2(30f, 20f));

            // Capas sobre el mapa
            enemyLayer = Rect("Enemies", mapRect); Stretch(enemyLayer);
            var labelLayer = Rect("Zones", mapRect); Stretch(labelLayer);
            BuildZoneLabels(labelLayer);

            // Marcador del jugador (círculo dorado + flecha de dirección + pulso)
            playerMarker = Rect("Player", mapRect);
            Place(playerMarker, new Vector2(.5f, .5f), Vector2.zero, new Vector2(30f, 30f));
            var pulseImg = Img("Pulse", playerMarker, Gold, ringSprite);
            pulse = pulseImg.rectTransform; Place(pulse, new Vector2(.5f, .5f), Vector2.zero, new Vector2(30f, 30f));
            arrow = Rect("Heading", playerMarker); Place(arrow, new Vector2(.5f, .5f), Vector2.zero, new Vector2(30f, 30f));
            var cone = Img("Cone", arrow, new Color(Gold.r, Gold.g, Gold.b, .35f), triSprite);
            Place(cone.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, 22f), new Vector2(30f, 24f));
            var tip = Img("Tip", arrow, NavyDeep, triSprite);
            Place(tip.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, 15f), new Vector2(14f, 12f));
            var body = Img("Body", playerMarker, NavyDeep, circleSprite); Place(body.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(24f, 24f));
            var fill = Img("Fill", body.transform, Gold, circleSprite); Place(fill.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(18f, 18f));
            var core = Img("Core", fill.transform, NavyDeep, circleSprite); Place(core.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(6f, 6f));

            // Panel lateral: zona actual + leyenda
            var side = Rect("Side", card.transform);
            Place(side, new Vector2(.5f, .5f), new Vector2(mapX + frameW / 2f + gap + sideW / 2f, mapY), new Vector2(sideW, S), new Vector2(.5f, .5f));

            var here = UIText.Make(side, "HereLabel", "ESTÁS EN", 12, new Color(1f, 1f, 1f, .55f), TextAnchor.UpperLeft);
            Place(here.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(210f, 18f), new Vector2(0f, 1f));
            zoneTitle = UIText.Make(side, "Zone", "", 20, accent, TextAnchor.UpperLeft);
            zoneTitle.fontStyle = FontStyle.Bold; zoneTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(zoneTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -20f), new Vector2(210f, 56f), new Vector2(0f, 1f));

            var sep = Img("Sep", side, new Color(1f, 1f, 1f, .12f), null);
            Place(sep.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -86f), new Vector2(210f, 2f), new Vector2(0f, 1f));

            var leg = UIText.Make(side, "LegendLabel", "LEYENDA", 12, new Color(1f, 1f, 1f, .55f), TextAnchor.UpperLeft);
            Place(leg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -100f), new Vector2(210f, 18f), new Vector2(0f, 1f));
            LegendRow(side, -128f, Gold, circleSprite, "Tú (la punta indica hacia dónde miras)", 14f);
            LegendRow(side, -164f, EnemyCol, circleSprite, "Enemigos numéricos", 10f);
            LegendRow(side, -200f, accent, roundSprite, "Zona donde estás", 14f);
            LegendRow(side, -236f, Navy, roundSprite, "Otras zonas", 14f);
        }

        void LegendRow(Transform parent, float y, Color c, Sprite sp, string label, float size)
        {
            var sw = Img("Swatch", parent, c, sp);
            Place(sw.rectTransform, new Vector2(0f, 1f), new Vector2(11f, y - 9f), new Vector2(size, size));
            if (c == Navy) { var o = sw.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1, 1, 1, .5f); }
            var t = UIText.Make(parent, "Legend", label, 13, new Color(1f, 1f, 1f, .85f), TextAnchor.MiddleLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(182f, 32f), new Vector2(0f, 1f));
        }

        void BuildZoneLabels(RectTransform layer)
        {
            labels.Clear();
            if (info == null || info.zones == null) return;
            float S = mapSize;
            var rects = new List<Rect>();
            foreach (var z in info.zones)
            {
                var lbl = new ZoneLabel { name = z.name, world = z.position };
                Vector2 at = new Vector2(z.position.x / (2f * Half) * S, z.position.y / (2f * Half) * S);

                lbl.dot = Img("Dot_" + z.name, layer, Color.white, circleSprite);
                Place(lbl.dot.rectTransform, new Vector2(.5f, .5f), at, new Vector2(8f, 8f));
                var dOut = lbl.dot.gameObject.AddComponent<Outline>(); dOut.effectColor = NavyDeep; dOut.effectDistance = new Vector2(1.5f, -1.5f);

                lbl.pill = Img("Zone_" + z.name, layer, new Color(Navy.r, Navy.g, Navy.b, .94f), roundSprite);
                lbl.text = UIText.Make(lbl.pill.transform, "Text", z.name, 14, Color.white);
                lbl.text.fontStyle = FontStyle.Bold;
                float w = lbl.text.preferredWidth + 20f, h = 24f;
                Stretch(lbl.text.rectTransform);
                var sh = lbl.pill.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .35f); sh.effectDistance = new Vector2(0, -2);
                labels.Add(lbl);
                rects.Add(new Rect(at.x - w / 2f, at.y + 8f, w, h));      // por defecto encima del punto
            }

            ResolveOverlaps(rects, S / 2f - 6f);
            for (int i = 0; i < labels.Count; i++)
            {
                var r = rects[i];
                Place(labels[i].pill.rectTransform, new Vector2(.5f, .5f), r.center, r.size);
            }
        }

        /// Separa las etiquetas que se enciman y las mantiene dentro del mapa.
        static void ResolveOverlaps(List<Rect> rects, float limit)
        {
            const float pad = 4f;
            for (int iter = 0; iter < 120; iter++)
            {
                bool moved = false;
                for (int i = 0; i < rects.Count; i++)
                    for (int j = i + 1; j < rects.Count; j++)
                    {
                        Rect a = rects[i], b = rects[j];
                        float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) + pad;
                        float oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) + pad;
                        if (ox <= 0f || oy <= 0f) continue;
                        moved = true;
                        // Empuja por el eje que requiere menos movimiento (casi siempre vertical)
                        if (oy <= ox * 1.5f)
                        {
                            float d = oy / 2f * (a.center.y >= b.center.y ? 1f : -1f);
                            a.y += d; b.y -= d;
                        }
                        else
                        {
                            float d = ox / 2f * (a.center.x >= b.center.x ? 1f : -1f);
                            a.x += d; b.x -= d;
                        }
                        rects[i] = a; rects[j] = b;
                    }
                for (int i = 0; i < rects.Count; i++)
                {
                    Rect r = rects[i];
                    r.x = Mathf.Clamp(r.x, -limit, limit - r.width);
                    r.y = Mathf.Clamp(r.y, -limit, limit - r.height);
                    rects[i] = r;
                }
                if (!moved) break;
            }
        }

        void RefreshZoneHighlight()
        {
            foreach (var l in labels)
            {
                bool on = l.name == currentZone;
                l.pill.color = on ? accent : new Color(Navy.r, Navy.g, Navy.b, .94f);
                l.text.color = on ? NavyDeep : Color.white;
                l.dot.color = on ? accent : Color.white;
                l.pill.transform.localScale = Vector3.one * (on ? 1.1f : 1f);
                var ring = l.pill.GetComponent<Outline>();
                if (ring == null) { ring = l.pill.gameObject.AddComponent<Outline>(); ring.effectColor = Color.white; ring.effectDistance = new Vector2(2f, -2f); }
                ring.enabled = on;
                if (on) l.pill.transform.SetAsLastSibling();
            }
            if (zoneTitle != null)
                zoneTitle.text = string.IsNullOrEmpty(currentZone) ? "Camino entre zonas" : currentZone;
        }

        void RefreshEnemies()
        {
            enemyBuffer.Clear();
            foreach (var hs in HealthSystem.Alive)
                if (hs != null && !hs.IsDead && hs.team == Team.Enemy) enemyBuffer.Add(hs.transform);
            // Niveles sin sistema de combate: enemigos numéricos sin HealthSystem
            foreach (var ne in FindObjectsByType<NumberEnemy>(FindObjectsSortMode.None))
                if (ne.GetComponent<HealthSystem>() == null) enemyBuffer.Add(ne.transform);

            int used = 0;
            float lim = mapSize / 2f;
            foreach (var et in enemyBuffer)
            {
                Vector2 p = WorldToMap(new Vector2(et.position.x, et.position.z));
                if (Mathf.Abs(p.x) > lim || Mathf.Abs(p.y) > lim) continue;
                if (used >= enemyDots.Count)
                {
                    var d = Img("Enemy", enemyLayer, EnemyCol, circleSprite);
                    var o = d.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1f, 1f, 1f, .9f); o.effectDistance = new Vector2(1f, -1f);
                    Place(d.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(10f, 10f));
                    enemyDots.Add(d);
                }
                var dot = enemyDots[used++];
                dot.gameObject.SetActive(true);
                dot.rectTransform.anchoredPosition = p;
            }
            for (int i = used; i < enemyDots.Count; i++) enemyDots[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ utilidades UI
        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image Img(string name, Transform parent, Color c, Sprite sp)
        {
            var rt = Rect(name, parent); var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sp; im.color = c; im.raycastTarget = false;
            if (sp != null && sp.border.sqrMagnitude > 0f) im.type = Image.Type.Sliced;
            return im;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot ?? new Vector2(.5f, .5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ sprites procedurales (sin assets externos)
        static void EnsureSprites()
        {
            if (roundSprite == null) roundSprite = MakeRounded(64, 18f, 18);
            if (ringSprite == null) ringSprite = MakeRing(64, 4f);
            if (circleSprite == null) circleSprite = MakeRounded(64, 32f, 0);
            if (triSprite == null) triSprite = MakeTriangle(64);
        }

        static Texture2D NewTex(int n, string name)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        static Sprite MakeRounded(int n, float radius, int border)
        {
            var t = NewTex(n, "MapRounded");
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + .5f, radius, n - radius), cy = Mathf.Clamp(y + .5f, radius, n - radius);
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(radius - d + .5f) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            t.SetPixels32(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static Sprite MakeRing(int n, float thick)
        {
            var t = NewTex(n, "MapRing");
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(r - d + .5f) * Mathf.Clamp01(d - (r - thick) + .5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static Sprite MakeTriangle(int n)
        {
            var t = NewTex(n, "MapTriangle");
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fy = (y + .5f) / n, fx = Mathf.Abs((x + .5f) / n - .5f) * 2f;   // punta arriba
                    float edge = (1f - fy) - fx;                                          // >0 dentro
                    byte a = (byte)(Mathf.Clamp01(edge * n * .5f + .5f) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            t.SetPixels32(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
