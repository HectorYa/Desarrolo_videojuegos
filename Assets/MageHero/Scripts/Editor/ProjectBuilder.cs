#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MageHero.Editor
{
    /// Orquesta toda la generación: capas, URP, mago, 3 niveles, menú y Build Settings.
    public static class ProjectBuilder
    {
        [MenuItem("Tools/Mage Hero/Build Everything (agua = bloqueo)")]
        public static void BuildAllBlock() { BuildAll(WaterPolicy.Block); }

        [MenuItem("Tools/Mage Hero/Build Everything (agua = penalización)")]
        public static void BuildAllPenalty() { BuildAll(WaterPolicy.Penalty); }

        [MenuItem("Tools/Mage Hero/Rebuild/Mago (prefab)")]
        public static void RebuildMage() { Prepare(); MageBuilder.BuildPrefab(); AssetDatabase.SaveAssets(); }

        [MenuItem("Tools/Mage Hero/Rebuild/Nivel Otoño")]
        public static void RebuildOtono() { RebuildLevel(LevelData.Otono()); }
        [MenuItem("Tools/Mage Hero/Rebuild/Nivel Desierto")]
        public static void RebuildDesierto() { RebuildLevel(LevelData.Desierto()); }
        [MenuItem("Tools/Mage Hero/Rebuild/Nivel Primavera")]
        public static void RebuildPrimavera() { RebuildLevel(LevelData.Primavera()); }

        /// Solo regenera las texturas de los mapas (tecla M) sin reconstruir las escenas ni perder sus cambios.
        [MenuItem("Tools/Mage Hero/Rebuild/Texturas de mapa (sin tocar escenas)")]
        public static void RebuildMapTextures()
        {
            foreach (var d in LevelData.All()) LevelBuilder.RegenerateMapTexture(d);
            AssetDatabase.SaveAssets();
            Debug.Log("[MageHero] Texturas de mapa regeneradas (" + LevelData.All().Length + " niveles).");
        }

        static void Prepare()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            PhysicsSetup.SetupLayersAndMatrix();
            SetupRenderPipeline();
            AssetLib.EnsureFolder(AssetLib.Gen);
        }

        static void RebuildLevel(LevelDef d)
        {
            Prepare();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MageBuilder.PrefabPath) == null) MageBuilder.BuildPrefab();
            LevelBuilder.BuildLevel(d, WaterPolicy.Block);
            RegisterScenes();
        }

        public static void BuildAll(WaterPolicy policy)
        {
            try
            {
                Prepare();
                EditorUtility.DisplayProgressBar("Mage Hero", "Construyendo el mago…", 0.05f);
                MageBuilder.BuildPrefab();
                var defs = LevelData.All();
                for (int i = 0; i < defs.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Mage Hero", "Generando nivel: " + defs[i].name + " (puede tardar un minuto)", 0.1f + 0.25f * i);
                    LevelBuilder.BuildLevel(defs[i], policy);
                }
                EditorUtility.DisplayProgressBar("Mage Hero", "Construyendo el menú…", 0.92f);
                MenuBuilder.Build(defs);
                RegisterScenes();
                EditorSceneManager.OpenScene(MenuBuilder.ScenePath);
                Debug.Log("[MageHero] Listo. Abre MainMenu y pulsa Play. Tema: " + policy);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void RegisterScenes()
        {
            var paths = new[] { MenuBuilder.ScenePath, LevelBuilder.ScenePath("otono"), LevelBuilder.ScenePath("desierto"), LevelBuilder.ScenePath("primavera") };
            EditorBuildSettings.scenes = paths.Where(File.Exists).Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        }

        /// Crea y asigna un URP Asset si el proyecto aún usa el pipeline integrado (sin depender de URP en compilación).
        static void SetupRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            var asm = "Unity.RenderPipelines.Universal.Runtime";
            var tAsset = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " + asm);
            var tData = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, " + asm);
            if (tAsset == null || tData == null)
            {
                Debug.LogWarning("[MageHero] URP no está instalado. Instálalo desde Window > Package Manager (Universal RP) y vuelve a ejecutar.");
                return;
            }
            AssetLib.EnsureFolder(AssetLib.Root + "/Settings");
            const string rdPath = AssetLib.Root + "/Settings/MageURP_Renderer.asset", rpPath = AssetLib.Root + "/Settings/MageURP.asset";
            var rd = ScriptableObject.CreateInstance(tData);
            AssetDatabase.CreateAsset(rd, rdPath);
            MethodInfo create = tAsset.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "Create");
            if (create == null) return;
            var asset = create.Invoke(null, new object[] { rd }) as RenderPipelineAsset;
            if (asset == null) return;
            AssetDatabase.CreateAsset(asset, rpPath);
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            AssetDatabase.SaveAssets();
            Debug.Log("[MageHero] URP Asset creado y asignado.");
        }
    }

    /// Al abrir el proyecto por primera vez ofrece generar todo.
    [InitializeOnLoad]
    static class FirstRunPrompt
    {
        static FirstRunPrompt() { EditorApplication.delayCall += Check; }

        static void Check()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(MenuBuilder.ScenePath) || SessionState.GetBool("MageHero_Asked", false)) return;
            SessionState.SetBool("MageHero_Asked", true);
            if (EditorUtility.DisplayDialog("Mage Hero",
                "Falta generar el contenido del juego (mago, 3 niveles y menú).\n\nEsto tarda unos minutos. También puedes hacerlo luego desde Tools > Mage Hero > Build Everything.",
                "Generar ahora", "Más tarde"))
                ProjectBuilder.BuildAll(WaterPolicy.Block);
        }
    }
}
#endif
