using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Cuadro de nivel/zona (arriba a la derecha) y aviso central al entrar a una zona nueva. Esc = volver al menú.
    public class ZoneHUD : MonoBehaviour
    {
        public LevelInfo info;
        public Transform player;
        public Text levelText, zoneText, toastText;
        public CanvasGroup toastGroup;
        public string menuScene = "MainMenu";
        public float detectRadius = 30f;
        string last = "";
        float toastTime;

        void Start()
        {
            if (levelText != null && info != null) levelText.text = info.levelName.ToUpperInvariant();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SceneManager.LoadScene(menuScene);
            if (info == null || player == null) return;

            string best = info.levelName; float bd = detectRadius;
            foreach (var z in info.zones)
            {
                float d = Vector2.Distance(new Vector2(player.position.x, player.position.z), z.position);
                if (d < bd) { bd = d; best = z.name; }
            }
            if (best != last)
            {
                last = best;
                if (zoneText != null) zoneText.text = best;
                if (toastText != null) { toastText.text = best; toastTime = 2.2f; }
            }
            if (toastGroup != null)
            {
                toastTime -= Time.deltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTime * 2f);
            }
        }
    }
}
