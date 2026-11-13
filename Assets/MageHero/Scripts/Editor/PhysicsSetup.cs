#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageHero.Editor
{
    public static class PhysicsSetup
    {
        static readonly string[] ObstacleKeys = { "tree", "pine", "palm", "cactus", "joshua", "rock", "boulder", "mesa", "spire", "mountain", "cliff", "hut", "house", "barrel", "monument", "rail", "fence", "post" };
        static readonly string[] GroundKeys   = { "terrain", "ground", "bank", "riverbed", "hill", "bridge", "pier", "deck", "plaza" };
        static readonly string[] SkipKeys     = { "water", "path", "stone", "flower", "tuft", "grass", "sky", "river" };

        [MenuItem("Tools/Mage Hero/Physics/1 - Create Layers and Collision Matrix")]
        public static void SetupLayersAndMatrix()
        {
            EnsureLayers();
            GameLayers.ApplyMatrix();
            AssetDatabase.SaveAssets();
            Debug.Log("[MageHero] Capas creadas y Layer Collision Matrix aplicada (Player, Environment, Obstacles, Water, MapBounds, Enemies). " +
                      "Guarda el proyecto (File > Save Project) para persistir la matriz.");
        }

        static void EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            foreach (var name in GameLayers.All)
            {
                bool found = false;
                for (int i = 0; i < layers.arraySize; i++)
                    if (layers.GetArrayElementAtIndex(i).stringValue == name) { found = true; break; }
                if (found) continue;                                   // "Water" ya existe en la capa 4 de Unity
                for (int i = 8; i < layers.arraySize; i++)             // 0-7 son capas reservadas
                {
                    var p = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = name; break; }
                }
            }
            tm.ApplyModifiedProperties();
        }

        [MenuItem("Tools/Mage Hero/Physics/2 - Apply Fitted Colliders to Open Scene")]
        public static void ApplyColliders()
        {
            EnsureLayers();
            int count = 0;
            var done = new HashSet<GameObject>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var root = TopMatch(mf.transform, out bool ground);
                if (root == null || !done.Add(root)) continue;
                if (root.GetComponent<ObstacleCollider>() != null) continue;
                var oc = root.AddComponent<ObstacleCollider>();
                oc.walkableGround = ground;
                oc.mode = ground ? FitMode.MeshExact : FitMode.Auto;
                oc.Bake();
                EditorUtility.SetDirty(root);
                count++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[MageHero] Colliders ajustados en {count} objetos. Ejecuta '3 - Audit' para verificar.");
        }

        // Sube por la jerarquía y devuelve el ancestro MÁS ALTO cuyo nombre coincide con una regla.
        static GameObject TopMatch(Transform t, out bool ground)
        {
            GameObject best = null; ground = false;
            for (var c = t; c != null; c = c.parent)
            {
                string n = c.name.ToLowerInvariant();
                if (Has(SkipKeys, n)) { if (best == null) return null; continue; }
                bool g = Has(GroundKeys, n), o = Has(ObstacleKeys, n);
                if (g || o) { best = c.gameObject; ground = g && !o; }
            }
            return best;
        }

        static bool Has(string[] keys, string n)
        {
            foreach (var k in keys) if (n.Contains(k)) return true;
            return false;
        }

        [MenuItem("Tools/Mage Hero/Physics/3 - Audit Colliders")]
        public static void Audit()
        {
            var issues = ColliderAudit.Run();
            if (issues.Count == 0) { Debug.Log("[MageHero] Auditoría de colliders: sin problemas."); return; }
            Debug.LogWarning($"[MageHero] Auditoría: {issues.Count} problema(s):\n" + string.Join("\n", issues));
        }
    }
}
#endif
