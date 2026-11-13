using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MageHero
{
    public class MenuController : MonoBehaviour
    {
        [Tooltip("Escenas que se cargan con las teclas 1, 2, 3 (solo en el menú principal).")]
        public string[] hotkeyScenes;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || hotkeyScenes == null) return;
            if (hotkeyScenes.Length > 0 && kb.digit1Key.wasPressedThisFrame) LoadLevel(hotkeyScenes[0]);
            if (hotkeyScenes.Length > 1 && kb.digit2Key.wasPressedThisFrame) LoadLevel(hotkeyScenes[1]);
            if (hotkeyScenes.Length > 2 && kb.digit3Key.wasPressedThisFrame) LoadLevel(hotkeyScenes[2]);
        }

        public void LoadLevel(string sceneName) { SceneManager.LoadScene(sceneName); }
        public void ReloadScene() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
