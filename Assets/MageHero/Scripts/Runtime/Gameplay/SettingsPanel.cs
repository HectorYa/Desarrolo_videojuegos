using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MageHero
{
    /// Ventana de Configuración (estilo GameGui), compartida por el menú principal y el menú de pausa.
    /// Solo tres ajustes: Sonido (volumen general) · Cámara (sensibilidad) · Pantalla (pantalla completa).
    /// Los cambios se ven al momento; "Listo" los confirma y "Cancelar" (o Esc) deshace todo lo cambiado
    /// desde que se abrió la ventana.
    public class SettingsPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        static int closedFrame = -1;
        /// Esc ya lo usó la configuración en este fotograma (la pausa / el menú no deben reaccionar también).
        public static bool BlocksEscape => IsOpen || closedFrame == Time.frameCount;

        Action onClose;
        readonly List<Action> refreshers = new List<Action>();
        // valores al abrir la ventana (para Cancelar)
        float savedMaster, savedSensitivity;
        bool savedFullscreen;

        /// <paramref name="allowDelete"/> se conserva por compatibilidad con quien la crea (ya no hay botón de borrar datos).
        public static SettingsPanel Create(Transform canvas, bool allowDelete, Action onClose)
        {
            var dim = GameGui.Dim(canvas, "ConfiguracionPanel", 0.62f);
            var p = dim.gameObject.AddComponent<SettingsPanel>();
            p.onClose = onClose;
            p.Build();
            dim.gameObject.SetActive(false);
            return p;
        }

        public void Show()
        {
            savedMaster = GameSettings.Master;
            savedSensitivity = GameSettings.Sensitivity;
            savedFullscreen = GameSettings.Fullscreen;
            foreach (var r in refreshers) r();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsOpen = true;
        }

        /// Confirma los cambios y cierra.
        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            IsOpen = false;
            closedFrame = Time.frameCount;
            onClose?.Invoke();
        }

        /// Deshace lo cambiado desde que se abrió la ventana y cierra.
        public void Cancel()
        {
            if (!gameObject.activeSelf) return;
            if (!Mathf.Approximately(GameSettings.Master, savedMaster)) GameSettings.Master = savedMaster;
            if (!Mathf.Approximately(GameSettings.Sensitivity, savedSensitivity)) GameSettings.Sensitivity = savedSensitivity;
            if (GameSettings.Fullscreen != savedFullscreen) GameSettings.Fullscreen = savedFullscreen;
            foreach (var r in refreshers) r();
            Hide();
        }

        void OnDisable() => IsOpen = false;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Cancel();
        }

        // ------------------------------------------------------------------ construcción
        void Build()
        {
            const float W = 640f, H = 430f, rowW = 560f;
            var panel = GameGui.Panel(transform, "Caja", new Vector2(W, H));
            MenuStyle.Place(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -16), new Vector2(W, H));
            var rib = GameGui.Ribbon(panel.transform, "Configuración", new Vector2(460, 66));
            MenuStyle.Place(rib.rectTransform, new Vector2(.5f, 1f), new Vector2(0, 8), new Vector2(460, 66));
            var c = panel.transform;

            GameGui.Section(c, "Sonido", GameGui.Icon.Sound, -66, GameGui.Blue);
            Volume(c, "Volumen general", -108, rowW, () => GameSettings.Master, v => GameSettings.Master = v, GameGui.Blue);

            GameGui.Section(c, "Cámara", GameGui.Icon.Camera, -160, GameGui.Teal);
            Volume(c, "Sensibilidad", -202, rowW, () => GameSettings.Sensitivity, v => GameSettings.Sensitivity = v, GameGui.Teal,
                   v => "x" + GameSettings.SensitivityMultiplier.ToString("0.0"));

            GameGui.Section(c, "Pantalla", GameGui.Icon.Screen, -254, GameGui.Yellow);
            Toggle(c, "Pantalla completa", -296, rowW, () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);

            // Botones inferiores: Cancelar · Listo
            const float bw = 230f, gap = 16f;
            var cancel = GameGui.Button(c, "Cancelar", "Cancelar", GameGui.Red, GameGui.Icon.Close, Cancel, 20, new Vector2(bw, 52));
            MenuStyle.Place((RectTransform)cancel.transform, new Vector2(.5f, 0f), new Vector2(-(bw + gap) * 0.5f, 46), new Vector2(bw, 52));
            var done = GameGui.Button(c, "Listo", "Listo", GameGui.Green, GameGui.Icon.Play, Hide, 20, new Vector2(bw, 52));
            MenuStyle.Place((RectTransform)done.transform, new Vector2(.5f, 0f), new Vector2((bw + gap) * 0.5f, 46), new Vector2(bw, 52));
        }

        void Volume(Transform parent, string label, float y, float w, Func<float> get, Action<float> set, Color color, Func<float, string> format = null)
        {
            GameGui.Row(parent, label, y, w, out var control, out var value);
            format = format ?? GameSettings.Percent;
            var s = GameGui.Slider(control, "Deslizador", color, get(), null, new Vector2(0, 26));
            var srt = (RectTransform)s.transform; MenuStyle.Stretch(srt); srt.offsetMin = new Vector2(0, 2); srt.offsetMax = new Vector2(0, -2);
            s.onValueChanged.AddListener(v => { set(v); value.text = format(v); });
            refreshers.Add(() => { float v = get(); s.SetValueWithoutNotify(v); value.text = format(v); });
            value.text = format(get());
        }

        void Toggle(Transform parent, string label, float y, float w, Func<bool> get, Action<bool> set)
        {
            GameGui.Row(parent, label, y, w, out var control, out var value);
            value.transform.parent.gameObject.SetActive(false);             // el botón ya dice SÍ / NO
            Button b = null;
            b = GameGui.Button(control, "Interruptor", "", GameGui.Green, GameGui.Icon.None, () => { set(!get()); Paint(b, get()); }, 18, new Vector2(150, 36));
            var rt = (RectTransform)b.transform; rt.anchorMin = rt.anchorMax = new Vector2(0f, .5f); rt.pivot = new Vector2(0f, .5f); rt.anchoredPosition = Vector2.zero;
            refreshers.Add(() => Paint(b, get()));
            Paint(b, get());
        }

        static void Paint(Button b, bool on)
        {
            b.GetComponent<Image>().color = on ? GameGui.Green : GameGui.Shade(GameGui.Red, 0.95f);
            var l = GameGui.Label(b); if (l != null) l.text = on ? "SÍ" : "NO";
        }

    }
}
