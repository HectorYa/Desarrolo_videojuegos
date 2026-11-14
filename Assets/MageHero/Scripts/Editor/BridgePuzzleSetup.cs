#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero.Editor
{
    /// Tools > Mage Hero > Bridge Puzzle > Setup in Open Scene
    /// Configura en Level_primavera: apuntado por clic, puente destructible con quiz, pergamino de pistas y la UI.
    public static class BridgePuzzleSetup
    {
        const string MatFolder = "Assets/MageHero/Generated/Materials";
        const string ProjectilePath = "Assets/MageHero/Prefabs/Projectile_Magic.prefab";

        [MenuItem("Tools/Mage Hero/Bridge Puzzle/Setup in Open Scene")]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.ToLowerInvariant().Contains("primavera") &&
                !EditorUtility.DisplayDialog("Puente", $"La escena abierta es '{scene.name}', no Level_primavera. ¿Continuar?", "Sí", "Cancelar"))
                return;

            var glowCyan = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Glow_7ff4ff.mat");
            var glowGold = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Glow_ffd24a.mat");
            var toon = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Toon_3b6fb5.mat");
            var gapMat = CopyMat(toon, "Puzzle_GapWater", new Color(0.247f, 0.776f, 0.847f));
            var scrollMat = CopyMat(toon, "Puzzle_Scroll", new Color(0.96f, 0.9f, 0.72f));
            var darkMat = CopyMat(glowCyan, "Puzzle_GranCero_Dark", new Color(0.05f, 0.03f, 0.08f));
            var redMat = CopyMat(glowCyan, "Puzzle_GranCero_Red", new Color(1f, 0.15f, 0.1f));

            // ---------- 1. Jugador: apuntado por clic
            var player = GameObject.FindWithTag("Player");
            if (player == null) { Debug.LogError("[MageHero] No hay objeto con tag Player."); return; }
            var old = player.GetComponent<PlayerCombat3D>();
            var aim = player.GetComponent<ClickAimingCombat3D>();
            if (aim == null) aim = Undo.AddComponent<ClickAimingCombat3D>(player);
            aim.projectilePrefab = old != null && old.projectilePrefab != null ? old.projectilePrefab : AssetDatabase.LoadAssetAtPath<Projectile3D>(ProjectilePath);
            aim.firePoint = old != null ? old.firePoint : null;
            aim.fallbackMaterial = glowCyan;
            aim.markerMaterial = glowCyan;
            if (old != null) Undo.DestroyObjectImmediate(old);
            EditorUtility.SetDirty(player);

            // ---------- 2. Puente
            var manager = Object.FindAnyObjectByType<BridgePuzzleManager>();
            if (manager == null)
            {
                var deckGo = GameObject.Find("BridgeDeck");
                if (deckGo == null) { Debug.LogError("[MageHero] No se encontró 'BridgeDeck' en la escena."); return; }
                var deck = deckGo.transform;
                var root = new GameObject("PuenteDelRio");
                Undo.RegisterCreatedObjectUndo(root, "Bridge Puzzle");
                root.transform.SetParent(deck.parent, false);
                root.transform.position = deck.position;

                Vector3 half = deck.lossyScale * 0.5f + new Vector3(0.7f, 0f, 0.7f);
                var rails = new List<Transform>(); var pilings = new List<Transform>();
                var siblings = new List<Transform>();
                foreach (Transform c in deck.parent) siblings.Add(c);
                foreach (var c in siblings)
                {
                    if (c.name != "BridgeRail" && c.name != "Piling") continue;
                    Vector3 d = c.position - deck.position;
                    if (Mathf.Abs(d.x) > half.x || Mathf.Abs(d.z) > half.z) continue;
                    Undo.SetTransformParent(c, root.transform, "Bridge Puzzle");
                    (c.name == "BridgeRail" ? rails : pilings).Add(c);
                }
                Undo.SetTransformParent(deck, root.transform, "Bridge Puzzle");
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);      // las piezas se mueven en la cinemática

                manager = Undo.AddComponent<BridgePuzzleManager>(root);
                manager.deck = deck; manager.rails = rails.ToArray(); manager.pilings = pilings.ToArray();
                manager.gapMaterial = gapMat;
                manager.BuildHelpers();
            }
            manager.gapMaterial = gapMat; manager.villainDarkMaterial = darkMat; manager.villainRedMaterial = redMat; manager.repairGlowMaterial = glowCyan;
            if (manager.gapWater != null) manager.gapWater.GetComponent<Renderer>().sharedMaterial = gapMat;

            // ---------- 3. Pergamino de Athan en una zona alterna (del lado del jugador, sin cruzar agua)
            var clue = Object.FindAnyObjectByType<ClueItemSystem>();
            string zoneName = manager.clueZoneName;
            if (clue == null)
            {
                Vector3 pos = PickClueSpot(player.transform.position, manager.transform.position, out zoneName);
                var go = new GameObject("PergaminoDeAthan", typeof(SphereCollider));
                Undo.RegisterCreatedObjectUndo(go, "Bridge Puzzle");
                go.transform.position = pos;
                go.layer = 2;
                var col = go.GetComponent<SphereCollider>(); col.isTrigger = true; col.radius = 1.6f; col.center = Vector3.up * 0.8f;
                clue = go.AddComponent<ClueItemSystem>();
                clue.scrollMaterial = scrollMat; clue.glowMaterial = glowGold != null ? glowGold : glowCyan;
                ClueItemSystem.BuildVisual(clue);
            }
            clue.puzzle = manager;
            manager.clueZoneName = zoneName;
            EditorUtility.SetDirty(manager); EditorUtility.SetDirty(clue);

            // ---------- 4. UI (Screen Space - Overlay en el Canvas HUD)
            var hud = GameObject.Find("HUD");
            if (hud == null) { Debug.LogError("[MageHero] No se encontró el Canvas 'HUD'."); return; }
            var ui = hud.GetComponent<UIManager>();
            if (ui == null) ui = Undo.AddComponent<UIManager>(hud);
            ui.playerHealth = player.GetComponent<HealthSystem>();
            if (ui.playerHealthSlider == null) UIManager.BuildHud(ui, hud.transform);
            UIManager.BuildPuzzleUI(ui, hud.transform);
            var hint = hud.transform.Find("Hint");
            if (hint != null && hint.TryGetComponent(out Text hintText))
            {
                hintText.text = "WASD: caminar · Shift: correr · Clic: hechizo al punto · Espacio: hechizo al frente · F: interactuar · H: pista · Q/E: girar · M: mapa · Esc: menú";
                hintText.rectTransform.sizeDelta = new Vector2(1200, 24);
            }
            EditorUtility.SetDirty(hud);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MageHero] Puzzle del puente listo: '{manager.name}' ({manager.rails.Length} barandas, {manager.pilings.Length} pilotes), " +
                      $"pergamino en '{zoneName}' {clue.transform.position}, apuntado por clic activo.");
            Selection.activeGameObject = manager.gameObject;
        }

        static Vector3 PickClueSpot(Vector3 playerPos, Vector3 bridgePos, out string zoneName)
        {
            zoneName = "";
            var info = Object.FindAnyObjectByType<LevelInfo>();
            int waterMask = GameLayers.Exists(GameLayers.Water) ? GameLayers.Mask(GameLayers.Water) : 0;
            int groundMask = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : ~0;
            int obstacleMask = GameLayers.Exists(GameLayers.Obstacles) ? GameLayers.Mask(GameLayers.Obstacles) : 0;
            float best = -1f; Vector3 bestPos = playerPos + new Vector3(25f, 0f, 25f);
            if (info != null && info.zones != null)
                foreach (var z in info.zones)
                {
                    var c = new Vector3(z.position.x, 0f, z.position.y);
                    float fromPlayer = Flat(c - playerPos), fromBridge = Flat(c - bridgePos);
                    if (fromPlayer < 25f || fromPlayer > 75f || fromBridge < 45f) continue;
                    if (CrossesWater(playerPos, c, waterMask)) continue;           // debe ser alcanzable sin el puente
                    foreach (var off in new[] { new Vector3(5, 0, 5), new Vector3(-5, 0, 5), new Vector3(5, 0, -5), new Vector3(-5, 0, -5), new Vector3(8, 0, 0) })
                    {
                        Vector3 p = c + off;
                        if (!Physics.Raycast(p + Vector3.up * 300f, Vector3.down, out RaycastHit hit, 600f, groundMask | waterMask | obstacleMask, QueryTriggerInteraction.Collide)) continue;
                        if (((1 << hit.collider.gameObject.layer) & groundMask) == 0) continue;
                        if (obstacleMask != 0 && Physics.CheckSphere(hit.point + Vector3.up, 1.5f, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
                        if (fromBridge > best) { best = fromBridge; bestPos = hit.point; zoneName = z.name; }
                        break;
                    }
                }
            return bestPos;
        }

        static bool CrossesWater(Vector3 a, Vector3 b, int waterMask)
        {
            if (waterMask == 0) return false;
            for (float t = 0f; t <= 1f; t += 0.02f)
            {
                Vector3 p = Vector3.Lerp(a, b, t);
                if (Physics.Raycast(new Vector3(p.x, 80f, p.z), Vector3.down, 200f, waterMask, QueryTriggerInteraction.Collide)) return true;
            }
            return false;
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        static Material CopyMat(Material src, string name, Color color)
        {
            string path = $"{MatFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (src == null) return null;
            var m = new Material(src) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_ShadeColor")) m.SetColor("_ShadeColor", color * 0.65f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
#endif
