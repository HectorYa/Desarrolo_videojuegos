using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageHero
{
    /// Partida guardada (una ranura, en PlayerPrefs): escenario, posición del mago, nivel del mago y su progreso de victorias.
    /// El avance entre escenarios (niveles desbloqueados) ya se guarda solo al derrotar a cada jefe (LevelProgress).
    ///  · Guardar: botón "Guardar partida" del menú de pausa.
    ///  · Continuar: botón del menú principal; carga el escenario y deja al mago donde estaba (sin repetir la cinemática).
    public static class SaveGame
    {
        const string P = "MageHero.Save.";

        public static bool HasSave => PlayerPrefs.HasKey(P + "Scene") && Application.CanStreamedLevelBeLoaded(PlayerPrefs.GetString(P + "Scene"));
        public static string Scene => PlayerPrefs.GetString(P + "Scene", "");

        /// true mientras se carga una partida guardada (la cinemática de apertura no se reproduce).
        public static bool Restoring { get; private set; }

        public static bool Save()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return false;
            var t = player.transform;
            PlayerPrefs.SetString(P + "Scene", SceneManager.GetActiveScene().name);
            PlayerPrefs.SetFloat(P + "X", t.position.x); PlayerPrefs.SetFloat(P + "Y", t.position.y); PlayerPrefs.SetFloat(P + "Z", t.position.z);
            PlayerPrefs.SetFloat(P + "Yaw", t.eulerAngles.y);
            var pl = PlayerLevel.Instance;
            PlayerPrefs.SetInt(P + "Level", pl != null ? pl.Level : 1);
            PlayerPrefs.SetInt(P + "Kills", pl != null ? pl.KillProgress : 0);
            PlayerPrefs.SetString(P + "Time", DateTime.Now.ToString("dd/MM HH:mm"));
            PlayerPrefs.Save();
            return true;
        }

        public static void Delete()
        {
            foreach (var k in new[] { "Scene", "X", "Y", "Z", "Yaw", "Level", "Kills", "Time" }) PlayerPrefs.DeleteKey(P + k);
            PlayerPrefs.Save();
        }

        /// "Arboleda de Otoño · Nivel 2 · 27/11 01:30"
        public static string Summary()
        {
            if (!HasSave) return "";
            string scene = Scene, name = scene;
            foreach (var d in LevelMenu.Entries()) if (LevelMenu.SceneName(d) == scene) name = d.name;
            return $"{name}  ·  Nivel {PlayerPrefs.GetInt(P + "Level", 1)}  ·  {PlayerPrefs.GetString(P + "Time", "")}";
        }

        /// Prepara la restauración; quien llama carga la escena (con su pantalla de carga).
        public static string BeginContinue()
        {
            Restoring = true;
            return Scene;
        }

        // ------------------------------------------------------------------ restauración al cargar
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Restoring) return;
            if (scene.name != Scene) { Restoring = false; return; }
            new GameObject("RestaurarPartida").AddComponent<Restorer>();
        }

        class Restorer : MonoBehaviour
        {
            IEnumerator Start()
            {
                // espera a que el nivel monte al mago, el HUD y la progresión
                yield return null; yield return null; yield return null;
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.transform.position = new Vector3(PlayerPrefs.GetFloat(P + "X"), PlayerPrefs.GetFloat(P + "Y") + 0.1f, PlayerPrefs.GetFloat(P + "Z"));
                    player.transform.rotation = Quaternion.Euler(0f, PlayerPrefs.GetFloat(P + "Yaw"), 0f);
                    if (cc != null) cc.enabled = true;
                    var pc = player.GetComponent<PlayerController>(); if (pc != null) pc.ResetMotion();
                    var pl = PlayerLevel.Instance;
                    if (pl != null) pl.Restore(PlayerPrefs.GetInt(P + "Level", 1), PlayerPrefs.GetInt(P + "Kills", 0));
                    var cam = Camera.main != null ? Camera.main.GetComponent<IsoCameraFollow>() : null;
                    if (cam != null) cam.Snap();
                    UIManager.Instance?.ShowMessage("Partida cargada. ¡Sigue tu aventura!", 3f);
                }
                Restoring = false;
                Destroy(gameObject);
            }
        }
    }
}
