#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero.Editor
{
    /// Configura en un clic el sistema de combate en la escena abierta (pensado para Level_primavera):
    /// capas, prefab del hechizo, componentes del mago, HUD de vida, enemigos y spawner.
    public static class CombatSetup
    {
        const string PrefabFolder = "Assets/MageHero/Prefabs";
        const string ProjectilePath = PrefabFolder + "/Projectile_Magic.prefab";
        const string GlowMatPath = "Assets/MageHero/Generated/Materials/Glow_7ff4ff.mat";

        [MenuItem("Tools/Mage Hero/Combat/Setup Combat in Open Scene")]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.ToLowerInvariant().Contains("primavera") &&
                !EditorUtility.DisplayDialog("Combate", $"La escena abierta es '{scene.name}', no Level_primavera. ¿Configurar igualmente?", "Sí", "Cancelar"))
                return;

            PhysicsSetup.SetupLayersAndMatrix();          // crea la capa Projectiles y aplica la matriz
            var projectile = CreateProjectilePrefab();

            // ---------- Jugador
            var player = GameObject.FindWithTag("Player");
            if (player == null) { Debug.LogError("[MageHero] No hay ningún objeto con tag Player en la escena."); return; }
            var hs = Ensure<HealthSystem>(player);
            hs.team = Team.Player; hs.maxHealth = 100f; hs.destroyOnDeath = false; hs.hitInvulnerability = 0.4f;
            var combat = Ensure<PlayerCombat3D>(player);
            combat.projectilePrefab = projectile;
            combat.fallbackMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
            var crystal = FindDeep(player.transform, "Crystal");
            if (crystal != null) combat.firePoint = crystal;
            Ensure<PlayerRespawn3D>(player);
            EditorUtility.SetDirty(player);

            // ---------- HUD
            var hud = GameObject.Find("HUD");
            if (hud == null) { Debug.LogError("[MageHero] No se encontró el Canvas 'HUD'."); return; }
            var ui = Ensure<UIManager>(hud);
            ui.playerHealth = hs;
            if (ui.playerHealthSlider == null)
            {
                foreach (var n in new[] { "PlayerHealthPanel", "DamageFlash", "CombatMessage" })
                { var old = hud.transform.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }
                UIManager.BuildHud(ui, hud.transform);
            }
            var hint = hud.transform.Find("Hint");
            if (hint != null)
            {
                var t = hint.GetComponent<Text>();
                if (t != null && !t.text.Contains("Espacio"))
                {
                    t.text = "WASD: caminar · Shift: correr · Espacio/Clic: hechizo · Q/E: girar · Z/X: zoom · M: mapa · Esc: menú";
                    t.rectTransform.sizeDelta = new Vector2(900, 24);
                }
            }
            EditorUtility.SetDirty(hud);

            // ---------- Enemigos ya colocados por el LevelBuilder
            int enemies = 0;
            foreach (var ne in Object.FindObjectsByType<NumberEnemy>(FindObjectsSortMode.None))
            {
                var ehs = Ensure<HealthSystem>(ne.gameObject);
                ehs.team = Team.Enemy; ehs.maxHealth = 60f; ehs.destroyOnDeath = true;
                Ensure<EnemyAI3D>(ne.gameObject);
                Ensure<WorldHealthBar3D>(ne.gameObject);
                if (GameLayers.Exists(GameLayers.Enemies)) GameLayers.Set(ne.gameObject, GameLayers.Enemies);
                EditorUtility.SetDirty(ne.gameObject);
                enemies++;
            }

            // ---------- Spawner con puntos estratégicos en las zonas del nivel
            var spawnerGo = GameObject.Find("EnemySpawner") ?? new GameObject("EnemySpawner");
            var spawner = Ensure<EnemySpawner>(spawnerGo);
            spawnerGo.transform.position = Vector3.zero;
            if (spawnerGo.transform.childCount == 0)
            {
                var info = Object.FindAnyObjectByType<LevelInfo>();
                if (info != null && info.zones != null)
                    foreach (var z in info.zones)
                    {
                        var pt = new GameObject("Spawn_" + z.name).transform;
                        pt.SetParent(spawnerGo.transform, false);
                        // Un poco desplazado del centro de la zona para no aparecer encima de carteles/edificios.
                        Vector3 p = new Vector3(z.position.x + 6f, 0f, z.position.y + 6f);
                        if (Physics.Raycast(p + Vector3.up * 300f, Vector3.down, out RaycastHit hit, 600f)) p.y = hit.point.y;
                        pt.position = p;
                    }
                if (info != null) spawner.areaRadius = info.worldHalfSize * 0.8f;
            }
            EditorUtility.SetDirty(spawnerGo);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MageHero] Combate configurado: jugador (vida 100, hechizo con Espacio/Clic), HUD de vida, {enemies} enemigos con IA y barra, " +
                      $"EnemySpawner con {spawnerGo.transform.childCount} puntos. Prefab del hechizo: {ProjectilePath}");
        }

        static Projectile3D CreateProjectilePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Projectile3D>(ProjectilePath);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/MageHero", "Prefabs");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile_Magic";
            go.transform.localScale = Vector3.one * 0.35f;
            var col = go.GetComponent<SphereCollider>(); col.isTrigger = true; col.radius = 0.7f;   // algo mayor que la malla: más fácil acertar
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>(); rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<Projectile3D>();

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f; trail.startWidth = 0.3f; trail.endWidth = 0f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (mat != null) trail.sharedMaterial = mat;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0.5f, 1f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.5f, 1f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            var lightGo = new GameObject("Glow"); lightGo.transform.SetParent(go.transform, false);
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.range = 3f; light.intensity = 2f; light.color = new Color(0.5f, 1f, 1f);

            if (GameLayers.Exists(GameLayers.Projectiles)) GameLayers.Set(go, GameLayers.Projectiles, true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Projectile3D>();
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (Transform c in t)
            {
                if (c.name == name) return c;
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
#endif
