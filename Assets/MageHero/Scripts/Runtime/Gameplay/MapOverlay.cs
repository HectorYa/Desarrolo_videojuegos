using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MageHero
{
    /// Mapa completo del nivel (tecla M). Muestra la posición y dirección del jugador.
    public class MapOverlay : MonoBehaviour
    {
        public GameObject panel;
        public RectTransform mapRect, arrow;
        public LevelInfo info;
        public Transform player;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame && panel != null) panel.SetActive(!panel.activeSelf);
            if (panel == null || !panel.activeSelf || player == null || info == null) return;

            float s = mapRect.rect.width, half = info.worldHalfSize;
            float nx = (player.position.x + half) / (half * 2f) - 0.5f;
            float nz = (player.position.z + half) / (half * 2f) - 0.5f;
            arrow.anchoredPosition = new Vector2(nx * s, nz * s);
            arrow.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
        }

        public void Toggle() { if (panel != null) panel.SetActive(!panel.activeSelf); }
    }
}
