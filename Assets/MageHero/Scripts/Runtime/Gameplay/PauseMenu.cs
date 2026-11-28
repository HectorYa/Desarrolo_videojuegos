using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Menú de pausa de los niveles (Esc o botón "Pausa" del HUD), con el estilo GameGui del menú principal.
    /// Opciones: Continuar · Guardar partida · Configuración · Ver mapa · Reiniciar nivel · Cambiar de escenario · Menú principal.
    /// Mientras está abierto el juego se congela (Time.timeScale = 0) y el sonido se pausa.
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

        public string menuScene = "MainMenu";
        GameObject root;
        Button mapButton, saveButton;
        SettingsPanel settings;
        float savedLabelUntil;
        float savedTimeScale = 1f;

        void Awake()
        {
            Instance = this;
            Build();
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (root != null && root.activeSelf) { Time.timeScale = 1f; AudioListener.pause = false; }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (savedLabelUntil > 0f && Time.unscaledTime > savedLabelUntil) { savedLabelUntil = 0f; SetSaveLabel("GUARDAR PARTIDA"); }
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || SettingsPanel.BlocksEscape) return;
            if (IsOpen) { Resume(); return; }
            // Esc primero cierra lo que esté abierto (quiz, pista, mapa); el Game Over y la victoria no se pausan.
            if (UIManager.BlocksEscape || MapOverlay.BlocksEscape || GameOverScreen.IsOpen || BossVictoryScreen.IsOpen) return;
            Open();
        }

        public void Open()
        {
            if (IsOpen || GameOverScreen.IsOpen || BossVictoryScreen.IsOpen) return;
            if (MapOverlay.IsOpen) FindAnyObjectByType<MapOverlay>()?.Toggle();
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (mapButton != null) mapButton.gameObject.SetActive(HasMap());
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Resume()
        {
            if (!IsOpen) return;
            if (settings != null && settings.gameObject.activeSelf) settings.Hide();
            root.SetActive(false);
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
        }

        void Restart() => Load(SceneManager.GetActiveScene().name);

        void SaveNow()
        {
            bool ok = SaveGame.Save();
            SetSaveLabel(ok ? "¡PARTIDA GUARDADA!" : "NO SE PUDO GUARDAR");
            savedLabelUntil = Time.unscaledTime + 2f;
        }

        void SetSaveLabel(string text) { var l = saveButton != null ? GameGui.Label(saveButton) : null; if (l != null) l.text = text; }
        void OpenSettings() => settings.Show();
        void ChangeLevel() { StyledMainMenu.OpenLevelsOnStart = true; Load(menuScene); }
        void MainMenu() => Load(menuScene);

        void ShowMap()
        {
            var map = FindAnyObjectByType<MapOverlay>();
            Resume();
            if (map != null && !MapOverlay.IsOpen) map.Toggle();
        }

        static bool HasMap()
        {
            var map = FindAnyObjectByType<MapOverlay>();
            return map != null && map.panel != null;
        }

        void Load(string scene)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(scene);
        }

        // ------------------------------------------------------------------ UI
        void Build()
        {
            var canvasGo = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;                                       // encima del HUD y del mapa
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = GameGui.Dim(canvasGo.transform, "PausePanel", 0.55f);
            root = dim.gameObject;
            var box = GameGui.Panel(dim.transform, "Caja", new Vector2(420, 590)).rectTransform;
            MenuStyle.Place(box, new Vector2(.5f, .5f), new Vector2(0, -20), new Vector2(420, 590));
            var rib = GameGui.Ribbon(box, "Pausa", new Vector2(360, 68));
            MenuStyle.Place(rib.rectTransform, new Vector2(.5f, 1f), new Vector2(0, 10), new Vector2(360, 68));
            var sub = GameGui.Text(box, "Sub", "El juego está detenido", 18, GameGui.InkSoft, TextAnchor.MiddleCenter, false);
            MenuStyle.Place(sub.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -60), new Vector2(380, 26));

            float y = -104f; const float step = 61f;
            Add(box, "CONTINUAR", GameGui.Green, GameGui.Icon.Play, Resume, ref y, step);
            saveButton = Add(box, "GUARDAR PARTIDA", GameGui.Yellow, GameGui.Icon.Save, SaveNow, ref y, step);
            Add(box, "CONFIGURACIÓN", GameGui.Purple, GameGui.Icon.Settings, OpenSettings, ref y, step);
            mapButton = Add(box, "VER MAPA (M)", GameGui.Blue, GameGui.Icon.Map, ShowMap, ref y, step);
            Add(box, "REINICIAR NIVEL", GameGui.Orange, GameGui.Icon.Restart, Restart, ref y, step);
            Add(box, "CAMBIAR ESCENARIO", GameGui.Teal, GameGui.Icon.Levels, ChangeLevel, ref y, step);
            Add(box, "MENÚ PRINCIPAL", GameGui.Red, GameGui.Icon.Home, MainMenu, ref y, step);

            var hint = GameGui.Text(box, "Hint", "Esc: continuar", 15, GameGui.InkSoft, TextAnchor.MiddleCenter, false);
            MenuStyle.Place(hint.rectTransform, new Vector2(.5f, 0f), new Vector2(0, 26), new Vector2(380, 22));

            settings = SettingsPanel.Create(canvasGo.transform, false, null);
        }

        static Button Add(RectTransform box, string text, Color color, GameGui.Icon icon, System.Action onClick, ref float y, float step)
        {
            var size = new Vector2(340, 52);
            var b = GameGui.Button(box, text, text, color, icon, onClick, 22, size);
            MenuStyle.Place((RectTransform)b.transform, new Vector2(.5f, 1f), new Vector2(0, y), size);
            y -= step;
            return b;
        }
    }
}
