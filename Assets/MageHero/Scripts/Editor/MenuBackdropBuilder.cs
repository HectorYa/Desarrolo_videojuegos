#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MageHero.Editor
{
    /// Crea Assets/MageHero/Scenes/MenuBackdrop.unity: una copia solo visual del bosque de otoño (raíz World,
    /// el sol y los volúmenes de postproceso, con la luz, la niebla y el cielo del nivel) más una cámara que lo
    /// sobrevuela. Se quitan los scripts del juego y los sonidos: es solo decorado para el fondo del menú principal.
    /// La añade a la lista de escenas del build (justo después de MainMenu).
    public static class MenuBackdropBuilder
    {
        const string Source = "Assets/MageHero/Scenes/Level_otono.unity";
        const string Target = "Assets/MageHero/Scenes/MenuBackdrop.unity";

        [MenuItem("Tools/Mage Hero/Menú/Crear fondo 3D del menú (MenuBackdrop)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var level = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);

                // --- ajustes de luz del nivel
                var fog = RenderSettings.fog; var fogColor = RenderSettings.fogColor; var fogMode = RenderSettings.fogMode;
                float fogDensity = RenderSettings.fogDensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
                var ambientMode = RenderSettings.ambientMode; var ambient = RenderSettings.ambientLight;
                var sky = RenderSettings.ambientSkyColor; var equator = RenderSettings.ambientEquatorColor; var groundC = RenderSettings.ambientGroundColor;
                float ambientIntensity = RenderSettings.ambientIntensity; var skybox = RenderSettings.skybox;
                var reflMode = RenderSettings.defaultReflectionMode; float reflIntensity = RenderSettings.reflectionIntensity;
                var sunName = RenderSettings.sun != null ? RenderSettings.sun.name : null;

                var backdrop = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                Light sun = null;
                foreach (var root in level.GetRootGameObjects())
                {
                    bool keep = root.name == "World" || root.GetComponent<Light>() != null
                                || root.GetComponent<UnityEngine.Rendering.Volume>() != null;
                    if (!keep) continue;
                    var copy = Object.Instantiate(root);
                    copy.name = root.name;
                    SceneManager.MoveGameObjectToScene(copy, backdrop);
                    Strip(copy);
                    var l = copy.GetComponent<Light>();
                    if (l != null && l.type == LightType.Directional && (sun == null || copy.name == sunName)) sun = l;
                }

                // --- punto de interés: el puente (río y agua animada); si no hay, el centro del mundo
                var world = backdrop.GetRootGameObjects().FirstOrDefault(g => g.name == "World");
                Vector3 focus = Vector3.zero;
                var deck = world != null ? world.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BridgeDeck") : null;
                if (deck != null) focus = Vector3.Lerp(deck.position, new Vector3(0, deck.position.y, 0), 0.25f);

                var camGo = new GameObject("MenuCamera");
                SceneManager.MoveGameObjectToScene(camGo, backdrop);
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 42f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 400f;
                cam.clearFlags = skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                cam.backgroundColor = fogColor;
                var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
                urp.renderPostProcessing = true;
                var orbit = camGo.AddComponent<MenuBackdropCamera>();
                orbit.focus = focus;
                camGo.transform.position = focus + new Vector3(-20f, 12f, -12f);
                camGo.transform.LookAt(focus);

                // --- luz, niebla y cielo del nivel en la escena nueva
                SceneManager.SetActiveScene(backdrop);
                RenderSettings.fog = fog; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode;
                RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = groundC;
                RenderSettings.ambientIntensity = ambientIntensity; RenderSettings.skybox = skybox;
                RenderSettings.defaultReflectionMode = reflMode; RenderSettings.reflectionIntensity = reflIntensity;
                if (sun != null) RenderSettings.sun = sun;

                EditorSceneManager.SaveScene(backdrop, Target);
                AddToBuild();
                Debug.Log($"[MageHero] Fondo 3D del menú creado en {Target} (enfoque {focus}).");
            }
            finally
            {
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        /// Quita todo lo que no sea decorado: scripts del juego (namespace MageHero) y fuentes de sonido.
        static void Strip(GameObject root)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Namespace != null && mb.GetType().Namespace.StartsWith("MageHero")) Object.DestroyImmediate(mb);
            foreach (var a in root.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
            foreach (var l in root.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(l);
            foreach (var c in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        }

        static void AddToBuild()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Any(s => s.path == Target)) return;
            int menu = list.FindIndex(s => s.path.EndsWith("/MainMenu.unity"));
            list.Insert(menu >= 0 ? menu + 1 : list.Count, new EditorBuildSettingsScene(Target, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
