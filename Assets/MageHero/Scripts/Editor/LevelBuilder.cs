#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MageHero.Editor
{
    /// Genera una escena de nivel completa a partir de un LevelDef: terreno, montañas, agua, props, estructuras,
    /// enemigos numéricos, jugador, cámara, HUD, mapa y físicas.
    public static partial class LevelBuilder
    {
        const float HW = World.HW, MW = World.MW;
        static LevelDef L;
        static long seed;
        static bool mtnOk;
        static string id, meshDir;
        static Material vt;
        static WaterPolicy waterPolicy;
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static readonly List<Dot> dots = new List<Dot>();
        static Transform pTrees, pRocks, pPlants, pStruct, pEnemies, pStones;

        struct Dot { public float x, z; public Color32 col; }

        static float Rnd() { seed = (seed * 16807L) % 2147483647L; return seed / 2147483647f; }
        static float Side() { return Rnd() < 0.5f ? -1f : 1f; }
        static int RI(int a, int b) { return a + (int)Mathf.Floor(Rnd() * (b - a + 1)); }
        static T Pick<T>(T[] a) { return a[(int)Mathf.Floor(Rnd() * a.Length)]; }
        static float H(float x, float z) { return World.Height(x, z); }
        static Matrix4x4 M(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float sx = 1, float sy = 1, float sz = 1) { return MeshBuilder.TRS(x, y, z, rx, ry, rz, sx, sy, sz); }
        static Color32 C(int h) { return MeshBuilder.Col(h); }
        static float Yaw(float dxWeb, float dzWeb) { return Mathf.Atan2(dxWeb, -dzWeb) * Mathf.Rad2Deg; }

        // =============================================================== ENTRADA
        public static string ScenePath(string levelId) { return AssetLib.Root + "/Scenes/Level_" + levelId + ".unity"; }
        public static string PreviewPath(string levelId) { return AssetLib.Gen + "/" + levelId + "/Preview.png"; }
        public static string MapPath(string levelId) { return AssetLib.Gen + "/" + levelId + "/Map.png"; }
        const int MapResolution = 1024;

        /// Regenera solo la textura del mapa (mismo archivo y GUID), sin tocar la escena del nivel.
        public static void RegenerateMapTexture(LevelDef def)
        {
            World.Configure(def);
            MakeMapTexture(def, MapResolution, MapPath(def.id), null);
        }

        public static void BuildLevel(LevelDef def, WaterPolicy policy)
        {
            L = def; id = def.id; waterPolicy = policy; seed = 7; mtnOk = false;
            meshDir = AssetLib.Gen + "/" + id; meshCache.Clear(); dots.Clear();
            AssetLib.EnsureFolder(AssetLib.Root + "/Scenes");
            AssetLib.EnsureFolder(meshDir);
            World.Configure(def);
            vt = AssetLib.VertexToon();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = C(L.sky); RenderSettings.fogStartDistance = 90f; RenderSettings.fogEndDistance = 260f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f, .62f, .65f);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.shadows = LightShadows.Soft; light.intensity = 1f; light.color = new Color(1f, .95f, .85f);
            light.transform.rotation = Quaternion.LookRotation(new Vector3(26f, -48f, 20f));

            var world = new GameObject("World").transform;
            pTrees = Group(world, "Trees"); pRocks = Group(world, "Rocks"); pPlants = Group(world, "Plants");
            pStruct = Group(world, "Structures"); pEnemies = Group(world, "Enemies"); pStones = Group(world, "PathStones");

            BuildTerrain(world);
            BuildWaterVisual(world);
            BuildWaterVolumes(world);
            BuildProps();
            BuildStructures();
            BuildEnemies();

            // Los enemigos se dibujan en vivo en el mapa (MapOverlay); la textura solo lleva el terreno.
            var map = MakeMapTexture(def, MapResolution, MapPath(id), null);
            MakeMapTexture(def, 256, PreviewPath(id), null);
            var player = BuildPlayerAndCamera();
            BuildUI(player, map);

            var bounds = new GameObject("MapBoundary").AddComponent<MapBoundary>();
            bounds.area = new Bounds(Vector3.zero, new Vector3(300f, 60f, 300f));
            bounds.Build();

            EditorSceneManager.SaveScene(scene, ScenePath(id));
            AssetDatabase.SaveAssets();
            Debug.Log("[MageHero] Nivel generado: " + ScenePath(id));
        }

        static Transform Group(Transform parent, string name) { var g = new GameObject(name).transform; g.SetParent(parent, false); return g; }

        // =============================================================== TERRENO
        static void BuildTerrain(Transform parent)
        {
            const int N = 240;
            float S = MW + 84f, step = S / N, half = S / 2f;
            int V = N + 1;
            var verts = new Vector3[V * V];
            var cols = new Color32[V * V];
            for (int j = 0; j < V; j++)
                for (int i = 0; i < V; i++)
                {
                    float x = -half + i * step, zu = -half + j * step, zw = -zu;
                    float h = H(x, zw);
                    float sl = 0f;
                    if (World.Mountain(x, zw) <= .4f)
                    {
                        float gx = H(x + 1f, zw) - H(x - 1f, zw), gz = H(x, zw + 1f) - H(x, zw - 1f);
                        sl = Mathf.Sqrt(gx * gx + gz * gz) / 2f;
                    }
                    verts[j * V + i] = new Vector3(x, h, zu);
                    cols[j * V + i] = World.Hex(World.GroundColor(x, zw, sl, h));
                }

            var ground = new List<int>(); var mount = new List<int>(); var all = new List<int>();
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int a = j * V + i, b = (j + 1) * V + i, c = j * V + i + 1, d = (j + 1) * V + i + 1;
                    float cx = -half + (i + .5f) * step, cz = -(-half + (j + .5f) * step);
                    var list = World.Mountain(cx, cz) >= 3f ? mount : ground;
                    list.Add(a); list.Add(b); list.Add(c); list.Add(c); list.Add(b); list.Add(d);
                    all.Add(a); all.Add(b); all.Add(c); all.Add(c); all.Add(b); all.Add(d);
                }

            var full = new Mesh(); full.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            full.vertices = verts; full.SetTriangles(all, 0); full.RecalculateNormals();
            Vector3[] normals = full.normals;
            UnityEngine.Object.DestroyImmediate(full);

            MakeTerrainObject(parent, "Terrain_Ground", verts, normals, cols, ground, GameLayers.Environment);
            MakeTerrainObject(parent, "Terrain_Mountains", verts, normals, cols, mount, GameLayers.Obstacles);
        }

        static void MakeTerrainObject(Transform parent, string name, Vector3[] verts, Vector3[] normals, Color32[] cols, List<int> tris, string layer)
        {
            var m = new Mesh { name = name };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = verts; m.normals = normals; m.colors32 = cols; m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            AssetLib.ReplaceMesh(meshDir, name, m);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = vt;
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = m;
            GameLayers.Set(go, layer);
            go.isStatic = true;
        }

        static void BuildWaterVisual(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Water_Visual";
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0, -.5f, 0); go.transform.localScale = new Vector3(90f, 1f, 90f);
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetLib.Water(L.pal.water, id);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// Volúmenes de colisión sobre TODA el agua (río y mar), fusionados por filas. Los puentes quedan libres.
        static void BuildWaterVolumes(Transform parent)
        {
            var go = new GameObject("WaterVolumes");
            go.transform.SetParent(parent, false);
            const float st = 1.5f;
            float ext = HW + 20f;
            for (float zw = -ext; zw <= ext; zw += st)
            {
                float run = float.NaN;
                for (float x = -ext; x <= ext + st; x += st)
                {
                    bool w = x <= ext && H(x, zw) < -.2f;
                    if (w && float.IsNaN(run)) run = x;
                    else if (!w && !float.IsNaN(run))
                    {
                        float x0 = run - st / 2f, x1 = x - st / 2f;
                        var bc = go.AddComponent<BoxCollider>();
                        bc.center = new Vector3((x0 + x1) / 2f, -.2f, -zw);
                        bc.size = new Vector3(x1 - x0, 5.6f, st);
                        run = float.NaN;
                    }
                }
            }
            var zone = go.AddComponent<WaterZone>();
            zone.policy = waterPolicy; zone.damage = 1;
            GameLayers.Set(go, GameLayers.Water);
        }

        // =============================================================== MALLAS DE PROPS
        static Mesh Prop(string key, Action<MeshBuilder> build)
        {
            Mesh m;
            if (meshCache.TryGetValue(key, out m)) return m;
            m = AssetLib.ReplaceMesh(AssetLib.Gen + "/Props", key, Make(key, build));
            meshCache[key] = m;
            return m;
        }
        static Mesh Make(string key, Action<MeshBuilder> build) { var b = new MeshBuilder(); build(b); return b.ToMesh(key); }
        static string Hx(int c) { return c.ToString("x6"); }

        static Mesh TreeMesh(int col) { return Prop("Tree_" + Hx(col), b => {
            b.Cyl(M(0, 0, 0), 6, .09f, .15f, 4.4f, C(0xf0e6d2));
            b.Icosa(M(0, 4.6f, 0, 0, 0, 0, 1, .85f, 1), 1.5f, C(col));
            b.Icosa(M(1.1f, 5.2f, .3f), 1.1f, C(col));
            b.Icosa(M(-.9f, 4.9f, -.4f), .95f, C(col)); }); }
        static Mesh PineMesh(int col) { return Prop("Pine_" + Hx(col), b => {
            b.Cyl(M(0, 0, 0), 6, .1f, .16f, 1.6f, C(0x6b4a2b));
            b.Cone(M(0, 1.2f, 0), 7, 1.3f, 2f, C(col));
            b.Cone(M(0, 2.7f, 0), 7, .9f, 1.6f, C(col)); }); }
        static Mesh PalmMesh(int col) { return Prop("Palm_" + Hx(col), b => {
            b.Cyl(M(0, 0, 0), 6, .1f, .16f, 3.4f, C(0xa07a46));
            b.Icosa(M(0, 3.5f, 0, 0, 0, 0, 1, .35f, 1), 1.4f, C(col));
            b.Icosa(M(0, 3.7f, 0, 0, 46f, 0, .9f, .3f, .9f), 1f, C(col)); }); }
        static Mesh CactusMesh() { return Prop("Cactus", b => {
            b.Cyl(M(0, 0, 0), 6, .22f, .28f, 1.9f, C(0x4f9a4a));
            b.Cyl(M(.38f, .8f, 0), 5, .12f, .12f, .8f, C(0x4f9a4a));
            b.Cyl(M(-.38f, 1.1f, 0), 5, .12f, .12f, .8f, C(0x4f9a4a)); }); }
        static Mesh JoshuaMesh() { return Prop("Joshua", b => {
            b.Cyl(M(0, 0, 0), 5, .14f, .22f, 2.6f, C(0xd8623f));
            b.Cyl(M(-.6f, 2.15f, 0, 0, 0, 34f), 5, .1f, .14f, 1.7f, C(0xd8623f));
            b.Cyl(M(.6f, 2.15f, 0, 0, 0, -34f), 5, .1f, .14f, 1.7f, C(0xd8623f));
            b.Icosa(M(0, 3.7f, 0), .85f, C(0x2f5d3a)); b.Icosa(M(-1.2f, 4f, 0), .85f, C(0x2f5d3a)); b.Icosa(M(1.2f, 4f, 0), .85f, C(0x2f5d3a)); }); }
        static Mesh MesaMesh(int col) { return Prop("Mesa_" + Hx(col), b => b.Cyl(M(0, 0, 0), 7, 1.4f, 1.9f, 1f, C(col))); }
        static Mesh RockMesh(int col) { return Prop("Rock_" + Hx(col), b => b.Icosa(M(0, 0, 0), 1f, C(col))); }
        static Mesh BushMesh(int col) { return Prop("Bush_" + Hx(col), b => b.Icosa(M(0, 0, 0), .45f, C(col))); }
        static Mesh SpireMesh(int col) { return Prop("Spire_" + Hx(col), b => b.Cyl(M(0, 0, 0), 5, .35f, 1.3f, 1f, C(col))); }
        static Mesh StoneMesh() { return Prop("Stone_" + Hx(L.stoneCol), b => b.Cyl(M(0, 0, 0), 7, 1f, 1f, .12f, C(L.stoneCol))); }
        static Mesh TuftMesh() { return Prop("Tuft_" + Hx(L.tuftCol), b => {
            b.Cone(M(-.3f, 0, .1f, 6f, 0, 8f), 4, .12f, .9f, C(L.tuftCol));
            b.Cone(M(0, 0, -.2f, -8f, 0, -5f), 4, .12f, .9f, C(L.tuftCol));
            b.Cone(M(.3f, 0, .15f, 4f, 0, -9f), 4, .12f, .9f, C(L.tuftCol)); }); }
        static Mesh FlowerMesh(int col) { return Prop("Flowers_" + Hx(col), b => {
            float[] o = { -1.2f, .3f, .9f, -.5f, .2f, 1.5f, -.8f, -1.1f, 1.7f, .4f, -.3f, -1.6f, 1.2f, 1.1f, -1.4f, .9f, .1f, -.2f, -.6f, .6f };
            for (int k = 0; k < o.Length; k += 2) b.Icosa(M(o[k], .2f, o[k + 1]), .17f, C(col)); }); }

        static Mesh PickRock() { return RockMesh(Pick(L.rockCols)); }

        // =============================================================== COLOCACIÓN
        static bool Ok(float x, float z, float m)
        {
            float h = H(x, z);
            return h > .5f && (mtnOk || World.Mountain(x, z) < 1.5f) && Mathf.Abs(x) < HW - 4f && Mathf.Abs(z) < HW - 4f
                && Mathf.Sqrt(x * x + z * z) > 14f && World.RouteDist(x, z) > m && World.DistToPolyline(World.RIV, x, z) > 5.5f && !World.NearAnyBridge(x, z, 3f);
        }

        static GameObject Spawn(Transform parent, string name, Mesh mesh, float x, float y, float z, float yaw, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, -z);          // coordenadas web -> Unity (z invertida)
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = vt;
            go.isStatic = true;
            return go;
        }
        static void Solid(GameObject go) { GameLayers.Set(go, GameLayers.Obstacles); }
        static void Capsule(GameObject go, float r, float h, float cy)
        {
            var c = go.AddComponent<CapsuleCollider>(); c.direction = 1; c.radius = r; c.height = h; c.center = new Vector3(0, cy, 0); Solid(go);
        }
        static void Convex(GameObject go)
        {
            var c = go.AddComponent<MeshCollider>(); c.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; c.convex = true; Solid(go);
        }

        static void Plant(string k, float x, float z, float s0, float sR, int[] cols)
        {
            float sc = s0 + Rnd() * sR;
            if (k == "tree" || k == "pine" || k == "palm")
            {
                bool beach = L.sea != null && World.SeaS(x, z) > L.sea.beach;
                if (!Ok(x, z, 2.2f) || (beach && k != "palm")) return;
                int[] cc = cols != null ? cols : (k == "tree" ? L.treeCols : k == "pine" ? L.pineCols : L.palmCols);
                if (cc.Length == 0) return;
                float s = 1f + Rnd() * .7f, yaw = Rnd() * 360f; int col = Pick(cc);
                GameObject go;
                if (k == "tree") { go = Spawn(pTrees, "Tree", TreeMesh(col), x, H(x, z), z, yaw, Vector3.one * s); Capsule(go, .22f, 4.4f, 2.2f); }
                else if (k == "pine") { go = Spawn(pTrees, "Pine", PineMesh(col), x, H(x, z), z, yaw, Vector3.one * s); Capsule(go, .5f, 2.2f, 1.1f); }
                else { go = Spawn(pTrees, "Palm", PalmMesh(col), x, H(x, z), z, yaw, Vector3.one * s); Capsule(go, .22f, 3.4f, 1.7f); }
                return;
            }
            if (k == "joshua")
            {
                if (!Ok(x, z, 2f)) return;
                float s = .9f + Rnd() * .5f;
                var go = Spawn(pTrees, "JoshuaTree", JoshuaMesh(), x, H(x, z), z, Rnd() * 360f, Vector3.one * s); Capsule(go, .22f, 2.6f, 1.3f); return;
            }
            if (k == "tuft")
            {
                if (Ok(x, z, .8f)) Spawn(pPlants, "Tuft", TuftMesh(), x, H(x, z), z, Rnd() * 360f, Vector3.one * (.7f + Rnd() * .7f));
                return;
            }
            if (k == "rock")
            {
                if (!Ok(x, z, 2f)) return;
                var go = Spawn(pRocks, "Rock", PickRock(), x, H(x, z) + .4f * sc, z, Rnd() * 360f, new Vector3(sc * 2f, sc * 1.2f, sc * 1.5f));
                if (sc >= .5f) Convex(go);
                return;
            }
            if (k == "mesa")
            {
                if (!Ok(x, z, 3f)) return;
                float q = 1f + Rnd() * 1.5f;
                var go = Spawn(pRocks, "Mesa", MesaMesh(Pick(L.rockCols)), x, H(x, z), z, Rnd() * 360f, new Vector3(1f, q * 3f, 1f)); Convex(go); return;
            }
            if (k == "cactus")
            {
                if (!Ok(x, z, 2f)) return;
                var go = Spawn(pTrees, "Cactus", CactusMesh(), x, H(x, z), z, Rnd() * 360f, Vector3.one * (1.3f + Rnd() * .7f)); Capsule(go, .25f, 1.9f, .95f); return;
            }
            if (k == "bush")
            {
                if (Ok(x, z, 1.5f)) Spawn(pPlants, "Bush", BushMesh(Pick(L.bushCols)), x, H(x, z) + .2f, z, Rnd() * 360f, Vector3.one * (.7f + Rnd()));
                return;
            }
            if (k == "flower")
            {
                int c = Pick(L.flowerCols);
                if (Ok(x, z, 1f)) Spawn(pPlants, "Flowers", FlowerMesh(c), x, H(x, z), z, Rnd() * 360f, Vector3.one * (.8f + Rnd() * .5f));
            }
        }

        static void Along(Vector2[] q, float step, Action<float, float, float, float> fn)
        {
            float next = 0f, cum = 0f;
            for (int i = 0; i < q.Length - 1; i++)
            {
                Vector2 a = q[i], b = q[i + 1];
                float len = Mathf.Max(Vector2.Distance(a, b), 1e-6f), tx = (b.x - a.x) / len, tz = (b.y - a.y) / len;
                while (next <= cum + len) { float s = next - cum; fn(a.x + tx * s, a.y + tz * s, -tz, tx); next += step; }
                cum += len;
            }
        }

        static void Recipe(Vector2[] q, float w, RecipeItem[] items)
        {
            foreach (var it in items)
            {
                var item = it;
                Along(q, item.step, (x, z, nx, nz) =>
                {
                    if (item.two)
                    {
                        foreach (float sg in new[] { -1f, 1f })
                            if (Rnd() < item.p) { float o = sg * (w + item.o0 + Rnd() * item.oR); Plant(item.kind, x + nx * o, z + nz * o, item.s0, item.sR, item.cols); }
                    }
                    else if (Rnd() < item.p) { float o = Side() * (w + item.o0 + Rnd() * item.oR); Plant(item.kind, x + nx * o, z + nz * o, item.s0, item.sR, item.cols); }
                });
            }
        }

        static void BuildProps()
        {
            foreach (var r in World.RT)
            {
                var route = r;
                Recipe(route.q, route.w, LevelData.Recipes[route.theme]);
                Along(route.q, 4.5f, (x, z, nx, nz) =>
                {
                    if (Rnd() < .55f)
                    {
                        float o = (Rnd() - .5f) * 2f * (route.w + .6f), sx = x + nx * o, sz = z + nz * o, s = .35f + Rnd() * .35f;
                        Spawn(pStones, "Stone", StoneMesh(), sx, H(sx, sz) + .06f, sz, Rnd() * 360f, new Vector3(s * 1.3f, 1f, s));
                    }
                });
                Along(route.q, 2.2f, (x, z, nx, nz) =>
                {
                    if (Rnd() < .7f) { float o = Side() * (route.w + 1.4f + Rnd() * 16f); Plant("tuft", x + nx * o, z + nz * o, 0, 0, null); }
                });
            }
            Recipe(World.RIV, 0f, L.riverProps);

            for (int g = 0; g < L.groveN; g++)
            {
                float cx = (Rnd() - .5f) * (MW - 30f), cz = (Rnd() - .5f) * (MW - 30f);
                if (World.RouteDist(cx, cz) < 10f) continue;
                for (int i = 0; i < 16; i++)
                {
                    float a = Rnd() * 6.28f, d = Mathf.Sqrt(Rnd()) * 9f;
                    Plant(L.groveK[i % L.groveK.Length], cx + Mathf.Cos(a) * d, cz + Mathf.Sin(a) * d, .7f, .8f, null);
                }
            }
            for (int i = 0; i < 260; i++) Plant(Pick(L.scatter), (Rnd() - .5f) * MW, (Rnd() - .5f) * MW, .5f, 1.2f, null);
            for (int i = 0; i < 500; i++) Plant("tuft", (Rnd() - .5f) * MW, (Rnd() - .5f) * MW, 0, 0, null);

            if (L.circle > 0f)
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * 6.283f, x = Mathf.Cos(a) * L.circle, z = Mathf.Sin(a) * L.circle, s = 1f + Rnd() * .5f;
                    if (World.RouteDist(x, z) > 2.5f) { var go = Spawn(pRocks, "StoneCircle", PickRock(), x, H(x, z) + .4f * s, z, Rnd() * 360f, new Vector3(s * 2f, s * 1.2f, s * 1.5f)); Convex(go); }
                }

            // Pie de montaña: vegetación y roca antes de la cordillera
            mtnOk = true;
            for (int i = 0; i < 500; i++)
            {
                float x = (Rnd() - .5f) * (MW + 20f), z = (Rnd() - .5f) * (MW + 20f), m = World.Mountain(x, z);
                if (m > .3f && m < 10f) Plant(Pick(L.mtn.foot), x, z, .8f, 1.4f, null);
            }
            // Hileras de rocas escalonadas (tresbolillo) a lo largo de toda la cordillera
            float A = L.mtn.A;
            for (float gx = -HW - 24f; gx <= HW + 24f; gx += 5f)
                for (float gz = -HW - 24f; gz <= HW + 24f; gz += 5f)
                {
                    float jx = gx + (Mathf.Round(gz / 5f) % 2f) * 2.5f + (Rnd() - .5f) * 3f, jz = gz + (Rnd() - .5f) * 3f, m = World.Mountain(jx, jz);
                    if (m < .15f || Rnd() > .8f) continue;
                    if (m < 3.5f) { float s = .9f + Rnd() * 1.1f; var go = Spawn(pRocks, "FootRock", PickRock(), jx, H(jx, jz) + .4f * s, jz, Rnd() * 360f, new Vector3(s * 2f, s * 1.2f, s * 1.5f)); Convex(go); }
                    else if (m < A * .5f) { float s = 2f + Rnd() * 1.8f; Spawn(pRocks, "SlopeRock", PickRock(), jx, H(jx, jz) + .4f * s, jz, Rnd() * 360f, new Vector3(s * 2f, s * 1.2f, s * 1.5f)); }
                    else if (Rnd() < .6f) Spire(jx, jz, H(jx, jz), 3f + Rnd() * 7f);
                }
            // Farallones en el mar: ocultan el final del mapa donde el límite es el agua
            if (L.sea != null)
                for (float gx = -HW - 40f; gx <= HW + 40f; gx += 8f)
                    for (float gz = -HW - 40f; gz <= HW + 40f; gz += 8f)
                    {
                        float x = gx + (Rnd() - .5f) * 6f, z = gz + (Rnd() - .5f) * 6f;
                        if (World.SeaS(x, z) > L.sea.from + 6f && Rnd() < .22f && !World.NearAnyBridge(x, z, 8f)) Spire(x, z, -.8f, 5f + Rnd() * 9f);
                    }
            mtnOk = false;
        }

        static void Spire(float x, float z, float y, float h)
        {
            float r = Rnd();
            var go = Spawn(pRocks, "Spire", SpireMesh(L.mtn.cols[r > .5f ? 1 : 3]), x, y, z, r * 360f, new Vector3(1f + r * .8f, h, 1f + r * .8f));
            go.transform.rotation = Quaternion.Euler((r - .5f) * 14f, r * 360f, ((h * 7f) % 1f - .5f) * 11f);
        }
    }
}
#endif
