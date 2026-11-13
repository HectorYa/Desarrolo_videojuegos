#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MageHero.Editor
{
    public static partial class LevelBuilder
    {
        static readonly string[] Syms = { "+", "×", "−", "÷" };

        // =============================================================== ESTRUCTURAS
        static GameObject Cube(Transform parent, string name, float x, float y, float z, Vector3 scale, float yaw, int hex, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, -z); go.transform.rotation = Quaternion.Euler(0f, yaw, 0f); go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetLib.Toon(hex);
            go.isStatic = true;
            if (solid) Solid(go); else UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject Cylinder(Transform parent, string name, float x, float y, float z, Vector3 scale, int hex, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, -z); go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetLib.Toon(hex);
            go.isStatic = true;
            if (solid) Solid(go); else UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void BuildStructures()
        {
            const int Wood = 0x8a5a33;

            // Vallas (postes + travesaños sólidos)
            foreach (var f in L.fences)
            {
                var fe = f; var r = World.RT[fe.route];
                Along(r.q, 1.9f, (x, z, nx, nz) =>
                {
                    float v = fe.axis == 'x' ? x : z;
                    if (v <= fe.lo || v >= fe.hi) return;
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        float o = sg * (r.w + 2.4f), px = x + nx * o, pz = z + nz * o, h = H(px, pz), tx = nz, tz = -nx;
                        Cube(pStruct, "FencePost", px, h + .45f, pz, new Vector3(.14f, .9f, .14f), 0f, Wood, true);
                        Cube(pStruct, "FenceRail", px + tx * .95f, h + .7f, pz + tz * .95f, new Vector3(.07f, .1f, 1.9f), Yaw(tx, tz), Wood, true);
                    }
                });
            }

            // Barriles
            foreach (var b in L.barrels) Cylinder(pStruct, "Barrel", b.x, H(b.x, b.z) + .42f, b.z, new Vector3(.76f, .425f, .76f), b.hex, true);

            // Puentes y muelles: cubierta visual (el suelo caminable es la malla del terreno), barandas sólidas, pilotes
            foreach (var b in World.BR)
            {
                bool xl = b.L >= b.W;
                Cube(pStruct, b.pier ? "PierDeck" : "BridgeDeck", b.x, b.y - .05f, b.z, new Vector3(b.L, .2f, b.W), 0f, L.pal.deck, false);
                if (!b.pier)
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        if (xl) Cube(pStruct, "BridgeRail", b.x, b.y + .3f, b.z + sg * (b.W / 2f - .06f), new Vector3(b.L, .5f, .12f), 0f, Wood, true);
                        else Cube(pStruct, "BridgeRail", b.x + sg * (b.L / 2f - .06f), b.y + .3f, b.z, new Vector3(.12f, .5f, b.W), 0f, Wood, true);
                    }
                float len = xl ? b.L : b.W;
                for (float u = -len / 2f + 1f; u <= len / 2f; u += 3f)
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        float px = xl ? b.x + u : b.x + sg * b.L / 2f, pz = xl ? b.z + sg * b.W / 2f : b.z + u;
                        Cylinder(pStruct, "Piling", px, b.y - 1.4f, pz, new Vector3(.28f, 1.5f, .28f), Wood, false);
                    }
            }

            // Casas
            Mesh roof = Prop("HutRoof", mb => mb.Cone(M(0, 2.2f, 0, 0, 45f, 0), 4, 2.6f, 1.6f, C(0xffffff)));
            foreach (var h in L.huts)
            {
                var g = new GameObject("Hut"); g.transform.SetParent(pStruct, false); g.transform.position = new Vector3(h.x, H(h.x, h.z), -h.z);
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.name = "Walls"; body.transform.SetParent(g.transform, false);
                body.transform.localPosition = new Vector3(0, 1.1f, 0); body.transform.localScale = new Vector3(3f, 2.2f, 3f);
                body.GetComponent<MeshRenderer>().sharedMaterial = AssetLib.Toon(h.wall); Solid(body);
                var rf = new GameObject("Roof"); rf.transform.SetParent(g.transform, false);
                rf.AddComponent<MeshFilter>().sharedMesh = roof; rf.AddComponent<MeshRenderer>().sharedMaterial = AssetLib.Toon(h.roof);
                g.isStatic = true; body.isStatic = true; rf.isStatic = true;
            }

            // Monumento (desierto)
            if (L.hasMonument)
            {
                Mesh mon = Prop("Monument", b =>
                {
                    float[,] seg = { { 1.8f, 1.2f, .6f }, { 1.3f, 2.6f, 2.2f }, { .9f, 2.4f, 4.2f }, { .55f, 1.4f, 6.2f } };
                    for (int i = 0; i < 4; i++) b.Cyl(M(0, seg[i, 2] - seg[i, 1] / 2f, 0), 8, seg[i, 0] * .8f, seg[i, 0], seg[i, 1], C(i > 1 ? 0x2f7f86 : 0x4fa7a0));
                    b.Icosa(M(0, 7.2f, 0), .7f, C(0xffb703), 1);
                    b.Box(M(-3.2f, 1.4f, 0, 0, 0, 28f), 2.2f, .12f, 1.5f, C(0x3a86ff));
                    b.Box(M(3.2f, 1.4f, 0, 0, 0, -28f), 2.2f, .12f, 1.5f, C(0x3a86ff));
                });
                var go = Spawn(pStruct, "Monument", mon, L.monument.x, H(L.monument.x, L.monument.y), L.monument.y, 0f, Vector3.one);
                Capsule(go, 2f, 7f, 3.5f);
            }
        }

        // =============================================================== ENEMIGOS NUMÉRICOS
        static string Exo(string s, out int ans)
        {
            if (s == "+") { int a = RI(3, 29), b = RI(3, 29); ans = a + b; return a + " + " + b; }
            if (s == "−") { int a = RI(12, 40), b = RI(3, a - 2); ans = a - b; return a + " − " + b; }
            if (s == "×") { int a = RI(2, 12), b = RI(2, 12); ans = a * b; return a + " × " + b; }
            int d = RI(2, 9), q = RI(2, 12); ans = q; return d * q + " ÷ " + d;
        }

        static string SymKey(string s) { return s == "+" ? "plus" : s == "×" ? "times" : s == "−" ? "minus" : "div"; }

        static Mesh EnemyMesh(string s)
        {
            return Prop("Enemy_" + SymKey(s), b =>
            {
                int white = 0xffffff, pupil = 0x1b1b2a;
                if (s == "+")
                {
                    b.Icosa(M(0, .6f, 0, 0, 0, 0, 1, .8f, 1), .75f, C(0x3fbf6a), 1);
                    b.Icosa(M(-.25f, .85f, .55f), .14f, C(white)); b.Icosa(M(.25f, .85f, .55f), .14f, C(white));
                    b.Icosa(M(-.25f, .85f, .68f), .07f, C(pupil)); b.Icosa(M(.25f, .85f, .68f), .07f, C(pupil));
                }
                else if (s == "×")
                {
                    b.Box(M(0, .7f, 0), 1.2f, 1.2f, 1.2f, C(0xffa31a));
                    b.Icosa(M(-.3f, .95f, .6f), .15f, C(white)); b.Icosa(M(.3f, .95f, .6f), .15f, C(white));
                    b.Icosa(M(-.3f, .95f, .72f), .075f, C(pupil)); b.Icosa(M(.3f, .95f, .72f), .075f, C(pupil));
                }
                else if (s == "−")
                {
                    b.Cyl(M(0, 0, 0), 6, .5f, .65f, 1.6f, C(0x7f93b8));
                    b.Icosa(M(-.2f, 1.1f, .5f), .13f, C(white)); b.Icosa(M(.2f, 1.1f, .5f), .13f, C(white));
                    b.Icosa(M(-.2f, 1.1f, .61f), .065f, C(pupil)); b.Icosa(M(.2f, 1.1f, .61f), .065f, C(pupil));
                }
                else
                {
                    b.Icosa(M(0, .7f, 0), .7f, C(0x9b6bd6), 1);
                    b.Icosa(M(0, 1.7f, 0), .48f, C(0xb58be8), 1);
                    b.Icosa(M(-.18f, 1.75f, .4f), .11f, C(white)); b.Icosa(M(.18f, 1.75f, .4f), .11f, C(white));
                    b.Icosa(M(-.18f, 1.75f, .5f), .055f, C(pupil)); b.Icosa(M(.18f, 1.75f, .5f), .055f, C(pupil));
                }
            });
        }

        static void MakeEnemy(float x, float z, float h, string s)
        {
            int ans; string ex = Exo(s, out ans);
            float top = s == "+" ? 2.1f : s == "×" ? 2.3f : s == "−" ? 2.5f : 2.9f;
            var root = new GameObject("Enemy_" + SymKey(s));
            root.transform.SetParent(pEnemies, false);
            root.transform.position = new Vector3(x, h, -z); root.transform.rotation = Quaternion.Euler(0f, Rnd() * 360f, 0f);
            var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = EnemyMesh(s); body.AddComponent<MeshRenderer>().sharedMaterial = vt;
            var col = root.AddComponent<CapsuleCollider>(); col.direction = 1; col.radius = .7f; col.height = 1.8f; col.center = new Vector3(0, .9f, 0);
            var ne = root.AddComponent<NumberEnemy>(); ne.symbol = s; ne.exercise = ex; ne.answer = ans;
            UIText.WorldLabel(root.transform, ex, new Vector3(0, top + .4f, 0), new Vector2(256f, 128f), 60, Color.white, new Color(0.114f, 0.208f, 0.341f, 1f), .011f);
            var belly = UIText.WorldLabel(root.transform, s, new Vector3(0, .5f, .76f), new Vector2(100f, 100f), 80, Color.white, new Color(0, 0, 0, 0), .006f);
            UnityEngine.Object.DestroyImmediate(belly.GetComponent<Billboard>());
            GameLayers.Set(root, GameLayers.Enemies);
            Color32 dc = s == "+" ? C(0x3fbf6a) : s == "×" ? C(0xffa31a) : s == "−" ? C(0x7f93b8) : C(0x9b6bd6);
            dots.Add(new Dot { x = x, z = z, col = dc });
        }

        static void BuildEnemies()
        {
            foreach (var r in World.RT)
            {
                var route = r;
                Along(route.q, 25f, (x, z, nx, nz) =>
                {
                    float o = (route.w + 3.5f + Rnd() * 2.5f) * Side(), ex = x + nx * o, ez = z + nz * o, h = H(ex, ez);
                    if (h < .6f || World.Mountain(ex, ez) > 1f || Mathf.Sqrt(ex * ex + ez * ez) < 18f || World.DistToPolyline(World.RIV, ex, ez) < 6f ||
                        World.SeaS(ex, ez) > (L.sea != null ? L.sea.beach + 2f : 99f) || World.NearAnyBridge(ex, ez, 4f)) return;
                    MakeEnemy(ex, ez, h, Pick(Syms));
                });
            }
        }

        // =============================================================== MAPA (textura)
        static Texture2D MakeMapTexture(LevelDef d, int N, string path, List<Dot> enemyDots)
        {
            AssetLib.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            int G = N + 3; float s = MW / N;
            var hg = new float[G * G];
            for (int j = 0; j < G; j++) for (int i = 0; i < G; i++) hg[j * G + i] = H(-HW + (i - 1) * s, -(-HW + (j - 1) * s));
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int i = 0; i < N; i++)
                {
                    float x = -HW + i * s, zu = -HW + y * s, h = hg[(y + 1) * G + i + 1];
                    Color32 c = World.Hex(World.GroundColor(x, -zu, 0f, h));
                    float f = 1f;
                    if (h > -.3f) f = 1f + Mathf.Clamp((hg[y * G + i + 2] - hg[(y + 2) * G + i]) * .12f, -.3f, .3f);
                    px[y * N + i] = new Color32((byte)Mathf.Min(255f, c.r * f), (byte)Mathf.Min(255f, c.g * f), (byte)Mathf.Min(255f, c.b * f), 255);
                }
            if (enemyDots != null)
                foreach (var dt in enemyDots)
                {
                    int cx = (int)((dt.x + HW) / s), cy = (int)((-dt.z + HW) / s), r = Mathf.Max(3, N / 128);
                    for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = cx + dx, yy = cy + dy;
                        if (xx < 0 || yy < 0 || xx >= N || yy >= N || dx * dx + dy * dy > r * r) continue;
                        px[yy * N + xx] = (dx * dx + dy * dy > (r - 1) * (r - 1)) ? new Color32(29, 53, 87, 255) : dt.col;
                    }
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.SetPixels32(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =============================================================== JUGADOR Y CÁMARA
        static Transform BuildPlayerAndCamera()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MageBuilder.PrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = new Vector3(0f, H(0f, L.spawn) + .1f, -L.spawn);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = C(L.sky); cam.farClipPlane = 500f; cam.fieldOfView = 46f;
            var follow = camGo.AddComponent<IsoCameraFollow>(); follow.target = player.transform;
            camGo.transform.position = player.transform.position + new Vector3(0f, 14f, -10f);
            camGo.transform.LookAt(player.transform.position);
            return player.transform;
        }

        // =============================================================== INTERFAZ (HUD + mapa)
        static RectTransform UIR(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.GetComponent<RectTransform>();
        }
        static Image UIImg(string name, Transform parent, Color c)
        {
            var rt = UIR(name, parent); var im = rt.gameObject.AddComponent<Image>(); im.color = c; return im;
        }
        static void Anchor(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
        static void Fill(RectTransform rt, float l, float t, float r, float b)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }
        static Button UIButton(string name, Transform parent, string label, Color bg, Color fg, int size)
        {
            var im = UIImg(name, parent, bg);
            var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im;
            var t = UIText.Make(im.transform, "Label", label, size, fg);
            Fill(t.rectTransform, 0, 0, 0, 0);
            return b;
        }

        public static readonly Color Navy = new Color(0.114f, 0.208f, 0.341f, 1f), GoldC = new Color(1f, 0.718f, 0.012f, 1f);

        static void BuildUI(Transform player, Texture2D map)
        {
            var info = new GameObject("LevelInfo").AddComponent<LevelInfo>();
            info.levelId = L.id; info.levelName = L.name; info.mapTexture = map; info.worldHalfSize = HW;
            info.zones = new ZoneInfo[L.zones.Length];
            for (int i = 0; i < L.zones.Length; i++) info.zones[i] = new ZoneInfo { name = L.zones[i].name, position = new Vector2(L.zones[i].x, -L.zones[i].z) };

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var hud = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var menu = hud.AddComponent<MenuController>();
            var zh = hud.AddComponent<ZoneHUD>(); var mo = hud.AddComponent<MapOverlay>();

            // Cuadro nivel/zona (arriba a la derecha)
            var panel = UIImg("LevelBanner", hud.transform, Navy);
            Anchor(panel.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-12, -12), new Vector2(320, 70));
            var line = UIImg("GoldLine", panel.transform, GoldC); Anchor(line.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 4));
            var lvl = UIText.Make(panel.transform, "Level", L.name.ToUpperInvariant(), 13, new Color(.6f, .8f, 1f, 1f), TextAnchor.UpperRight);
            Fill(lvl.rectTransform, 8, 8, 12, 30);
            var zone = UIText.Make(panel.transform, "Zone", L.name, 22, Color.white, TextAnchor.LowerRight);
            zone.fontStyle = FontStyle.Bold; Fill(zone.rectTransform, 8, 28, 12, 10);

            // Aviso central de zona
            var toastRt = UIR("ZoneToast", hud.transform); var tg = toastRt.gameObject.AddComponent<CanvasGroup>(); tg.alpha = 0f; tg.blocksRaycasts = false;
            Anchor(toastRt, new Vector2(.5f, .74f), new Vector2(.5f, .74f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 90));
            var toast = UIText.Make(toastRt, "Text", "", 48, Color.white); toast.fontStyle = FontStyle.Bold; Fill(toast.rectTransform, 0, 0, 0, 0);
            var ol = toast.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, .55f); ol.effectDistance = new Vector2(2, -2);

            // Ayuda y botones
            var hint = UIText.Make(hud.transform, "Hint", "WASD: caminar · Shift: correr · Q/E: girar · Z/X o rueda: zoom · M: mapa · Esc: menú", 14, Color.white, TextAnchor.UpperLeft);
            Anchor(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(760, 24));
            var hol = hint.gameObject.AddComponent<Outline>(); hol.effectColor = new Color(0, 0, 0, .6f); hol.effectDistance = new Vector2(1, -1);
            var mapBtn = UIButton("MapButton", hud.transform, "Mapa (M)", GoldC, Navy, 18);
            Anchor((RectTransform)mapBtn.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 14), new Vector2(150, 44));
            var menuBtn = UIButton("MenuButton", hud.transform, "Menú (Esc)", Navy, Color.white, 16);
            Anchor((RectTransform)menuBtn.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 66), new Vector2(150, 40));

            // Panel del mapa completo
            var mapPanel = UIImg("MapPanel", hud.transform, new Color(.04f, .08f, .14f, .8f)); Fill(mapPanel.rectTransform, 0, 0, 0, 0);
            var mapRaw = UIR("Map", mapPanel.transform); var raw = mapRaw.gameObject.AddComponent<RawImage>(); raw.texture = map;
            Anchor(mapRaw, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(640, 640));
            var frame = UIImg("Frame", mapRaw, GoldC); frame.raycastTarget = false; Fill(frame.rectTransform, -5, -5, -5, -5); frame.transform.SetAsFirstSibling();
            foreach (var z in L.zones)
            {
                var pill = UIImg("Zone_" + z.name, mapRaw, new Color(Navy.r, Navy.g, Navy.b, .92f)); pill.raycastTarget = false;
                Anchor(pill.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(z.x / MW * 640f, (-z.z) / MW * 640f), new Vector2(8f * z.name.Length + 18f, 24f));
                var t = UIText.Make(pill.transform, "Text", z.name, 14, Color.white); Fill(t.rectTransform, 0, 0, 0, 0);
            }
            var arrow = UIImg("PlayerArrow", mapRaw, GoldC); arrow.raycastTarget = false;
            Anchor(arrow.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(16, 16));
            var nose = UIImg("Nose", arrow.transform, Navy); nose.raycastTarget = false;
            Anchor(nose.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(6, 14));
            var cap = UIText.Make(mapPanel.transform, "Caption", "Los puntos de colores son enemigos numéricos · M para cerrar", 16, Color.white);
            Anchor(cap.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(900, 28));
            mapPanel.gameObject.SetActive(false);

            // Referencias
            zh.info = info; zh.player = player; zh.levelText = lvl; zh.zoneText = zone; zh.toastText = toast; zh.toastGroup = tg; zh.menuScene = "MainMenu";
            mo.panel = mapPanel.gameObject; mo.mapRect = mapRaw; mo.arrow = arrow.rectTransform; mo.info = info; mo.player = player;

            UnityEventTools.AddPersistentListener(mapBtn.onClick, mo.Toggle);
            UnityEventTools.AddStringPersistentListener(menuBtn.onClick, menu.LoadLevel, "MainMenu");
            var safety = player.GetComponent<PlayerSafety>();
            if (safety != null) UnityEventTools.AddPersistentListener(safety.onDefeated, menu.ReloadScene);
        }
    }
}
#endif
