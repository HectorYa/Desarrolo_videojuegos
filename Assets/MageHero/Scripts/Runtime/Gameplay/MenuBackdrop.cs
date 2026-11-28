using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageHero
{
    /// Fondo 3D del menú principal: carga en modo aditivo la escena MenuBackdrop (el bosque de otoño sin jugador,
    /// enemigos ni HUD; la crea Tools > Mage Hero > Menú > Crear fondo 3D del menú) y la deja como escena activa
    /// para que se usen su luz, su niebla y su cielo. Las cámaras del menú se apagan y su lienzo pasa a Overlay,
    /// así la interfaz queda encima del bosque. Al entrar a un nivel (carga Single) se descarga sola.
    public static class MenuBackdrop
    {
        public const string SceneName = "MenuBackdrop";

        public static void Load(MonoBehaviour host, Action<bool> done)
        {
            if (!Application.CanStreamedLevelBeLoaded(SceneName)) { done?.Invoke(false); return; }
            var menu = SceneManager.GetActiveScene();
            var existing = SceneManager.GetSceneByName(SceneName);
            if (existing.IsValid() && existing.isLoaded) { Activate(existing, menu); done?.Invoke(true); return; }
            host.StartCoroutine(Routine(menu, done));
        }

        static IEnumerator Routine(Scene menu, Action<bool> done)
        {
            var op = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            if (op == null) { done?.Invoke(false); yield break; }
            while (!op.isDone) yield return null;
            var s = SceneManager.GetSceneByName(SceneName);
            if (!s.IsValid() || !s.isLoaded || !menu.IsValid() || !menu.isLoaded) { done?.Invoke(false); yield break; }
            Activate(s, menu);
            done?.Invoke(true);
        }

        static void Activate(Scene backdrop, Scene menu)
        {
            SceneManager.SetActiveScene(backdrop);
            foreach (var root in menu.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true)) cam.enabled = false;   // su AudioListener sigue activo
                foreach (var cv in root.GetComponentsInChildren<Canvas>(true))
                    if (cv.isRootCanvas && cv.renderMode != RenderMode.ScreenSpaceOverlay) cv.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }

}
