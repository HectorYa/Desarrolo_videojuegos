using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Menú principal de CLIPPER con el estilo "Low Poly Game GUI" (Diseños/MenuPrincipal/image.png):
    /// de fondo el bosque de otoño en 3D (MenuBackdrop), panel claro con la cinta azul "MENÚ PRINCIPAL" y botones de colores con icono.
    ///  1) Portada: Continuar (si hay partida guardada) · Jugar · Configuración · Controles · Ver historia · Salir.
    ///  2) Escenarios: una tarjeta por nivel con imagen, nombre, tema y botón ¡Jugar!.
    ///  3) Configuración y Controles (ventanas emergentes).
    /// Se construye en tiempo de ejecución sobre el Canvas de MenuController (1280×720 de referencia).
    /// Imágenes de los escenarios: Resources/MenuThumbs/<id>.png
    public class StyledMainMenu : MonoBehaviour
    {
        public const string ThumbFolder = "MenuThumbs";
        /// Si es true, el menú se abre directamente en la lista de escenarios (lo usa "Cambiar de escenario" de la pausa).
        public static bool OpenLevelsOnStart;

        MenuController menu;
        GameObject mainPage, levelsPage, controlsPanel, loadingPanel;
        SettingsPanel settings;
        Text loadingTitle, loadingInfo;
        RectTransform loadingFill;
        bool loading;

        public bool IsLoading => loading;

        void Start()
        {
            // Seguridad: nunca construir el menú dentro de un nivel (borraría el HUD del juego).
            if (!MenuController.IsMainMenuScene) { Destroy(this); return; }
            menu = GetComponent<MenuController>();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);   // menú antiguo
            EnsureClickable();
            Build();
            if (OpenLevelsOnStart) { OpenLevelsOnStart = false; ShowLevels(); }
            else ShowMain();
        }

        /// Sin EventSystem o sin GraphicRaycaster los botones no reciben clics.
        void EnsureClickable()
        {
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (loading || kb == null || !kb.escapeKey.wasPressedThisFrame || SettingsPanel.BlocksEscape) return;
            if (controlsPanel.activeSelf) controlsPanel.SetActive(false);
            else if (levelsPage.activeSelf) ShowMain();
        }

        void ShowMain() { mainPage.SetActive(true); levelsPage.SetActive(false); controlsPanel.SetActive(false); }
        void ShowLevels() { mainPage.SetActive(false); levelsPage.SetActive(true); controlsPanel.SetActive(false); }
        void ShowControls() { controlsPanel.SetActive(true); controlsPanel.transform.SetAsLastSibling(); }
        void ShowSettings() => settings.Show();
        void Continue() { if (SaveGame.HasSave) LoadScene(SaveGame.BeginContinue()); }
        void Load(string scene) => LoadScene(scene);
        void Quit() { if (menu != null) menu.Quit(); }

        // ------------------------------------------------------------------ Carga del escenario
        /// Carga el nivel mostrando una pantalla de "Cargando..." con barra de progreso.
        public void LoadScene(string scene)
        {
            if (loading) return;
            // Progreso: hay que terminar el nivel anterior (derrotar a su jefe) para entrar.
            if (!LevelProgress.IsUnlocked(scene))
            {
                ShowLoading("Nivel bloqueado", $"Termina «{LevelProgress.RequiredLevelName(scene)}» derrotando a su jefe para desbloquearlo.", 0f);
                StartCoroutine(HideLoadingLater(2.5f));
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"[MageHero] La escena '{scene}' no está en la lista de escenas del build (File > Build Profiles).");
                ShowLoading("No se pudo abrir", $"Falta la escena '{scene}' en File > Build Profiles.", 0f);
                StartCoroutine(HideLoadingLater(3f));
                return;
            }
            StartCoroutine(LoadRoutine(scene));
        }

        IEnumerator LoadRoutine(string scene)
        {
            loading = true;
            Time.timeScale = 1f;
            string niceName = scene;
            foreach (var d in LevelMenu.Entries()) if (LevelMenu.SceneName(d) == scene) niceName = d.name;
            ShowLoading("Cargando...", niceName, 0f);
            Debug.Log($"[MageHero] Cargando escena '{scene}'...");
            yield return null;                                   // dibuja la pantalla de carga antes de empezar
            yield return null;

            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (op == null) { loading = false; ShowLoading("No se pudo abrir", scene, 0f); StartCoroutine(HideLoadingLater(3f)); yield break; }
            while (!op.isDone)
            {
                SetProgress(Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }
        }

        void ShowLoading(string title, string info, float progress)
        {
            loadingPanel.SetActive(true);
            loadingPanel.transform.SetAsLastSibling();
            loadingTitle.text = title.ToUpperInvariant();
            loadingInfo.text = info;
            SetProgress(progress);
        }

        void SetProgress(float p) { if (loadingFill != null) loadingFill.anchorMax = new Vector2(Mathf.Max(0.02f, p), 1f); }

        IEnumerator HideLoadingLater(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (!loading) loadingPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ Construcción
        void Build()
        {
            // Fondo: el bosque de otoño en 3D (escena MenuBackdrop, cámara sobrevolando). Mientras carga, o si la
            // escena no está en el build, se ve el fondo facetado de siempre, que luego se desvanece.
            var bg = MenuStyle.Rect("Background", transform).gameObject.AddComponent<RawImage>();
            bg.texture = GameGui.Background();
            bg.raycastTarget = false;
            MenuStyle.Stretch(bg.rectTransform);
            // velo oscuro suave sobre el bosque para que el menú y el título se lean bien
            var veil = MenuStyle.Rect("Velo", transform).gameObject.AddComponent<Image>();
            veil.color = new Color(0.02f, 0.06f, 0.08f, 0.22f);
            veil.raycastTarget = false;
            MenuStyle.Stretch(veil.rectTransform);
            MenuBackdrop.Load(this, ok => { if (ok && bg != null) StartCoroutine(FadeOut(bg, 0.8f)); });

            BuildMainPage();
            BuildLevelsPage();
            BuildControlsPanel();
            settings = SettingsPanel.Create(transform, true, RebuildMain);
            BuildLoadingPanel();
        }

        /// Tras borrar datos o cambiar ajustes: rehace la portada (Continuar aparece o desaparece).
        void RebuildMain()
        {
            bool wasActive = mainPage != null && mainPage.activeSelf;
            if (mainPage != null) Destroy(mainPage);
            BuildMainPage();
            mainPage.transform.SetSiblingIndex(2);                     // sobre el fondo y el velo, detrás de las ventanas emergentes
            mainPage.SetActive(wasActive || !levelsPage.activeSelf);
        }

        void BuildLoadingPanel()
        {
            var overlay = GameGui.Dim(transform, "LoadingPanel", 0.92f);
            loadingPanel = overlay.gameObject;
            var rib = GameGui.Ribbon(overlay.transform, "Cargando...", new Vector2(560, 80), GameGui.Blue, 40);
            MenuStyle.Place(rib.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 90), new Vector2(560, 80));
            loadingTitle = rib.GetComponentInChildren<Text>();
            loadingInfo = GameGui.Fit(GameGui.Text(overlay.transform, "Info", "", 26, Color.white), 14, 26);
            MenuStyle.Place(loadingInfo.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 4), new Vector2(900, 70));
            var bar = GameGui.Img("Barra", overlay.transform, GameGui.Inset(), Color.white);
            MenuStyle.Place(bar.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -70), new Vector2(520, 34));
            var fill = GameGui.Img("Relleno", bar.transform, GameGui.ButtonSprite(), GameGui.Green);
            loadingFill = fill.rectTransform;
            loadingFill.anchorMin = Vector2.zero; loadingFill.anchorMax = new Vector2(0.02f, 1f);
            loadingFill.offsetMin = new Vector2(5, 5); loadingFill.offsetMax = new Vector2(-5, -5);
            loadingPanel.SetActive(false);
        }

        void BuildMainPage()
        {
            mainPage = MenuStyle.Rect("MainPage", transform).gameObject;
            MenuStyle.Stretch((RectTransform)mainPage.transform);
            var page = mainPage.transform;

            // --- Panel del menú (izquierda): su alto se ajusta al número de botones
            bool hasSave = SaveGame.HasSave;
            int count = 4 + (hasSave ? 1 : 0);
#if !UNITY_WEBGL || UNITY_EDITOR
            count++;
#endif
            float panelH = 96f + count * 72f + 14f;
            var panel = GameGui.Panel(page, "PanelMenu", new Vector2(430, panelH));
            MenuStyle.Place(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(-330, -16), new Vector2(430, panelH));
            var rib = GameGui.Ribbon(panel.transform, "Menú principal", new Vector2(480, 72), GameGui.Blue, 32);
            MenuStyle.Place(rib.rectTransform, new Vector2(.5f, 1f), new Vector2(0, 14), new Vector2(480, 72));

            float y = -92f; const float step = 72f;
            var bsz = new Vector2(350, 60);
            void Add(string text, Color color, GameGui.Icon icon, System.Action onClick)
            {
                var b = GameGui.Button(panel.transform, "Btn_" + text, text, color, icon, onClick, 26, bsz);
                MenuStyle.Place((RectTransform)b.transform, new Vector2(.5f, 1f), new Vector2(0, y), bsz);
                y -= step;
            }
            if (hasSave) Add("CONTINUAR", GameGui.Yellow, GameGui.Icon.Play, Continue);
            Add(hasSave ? "ESCENARIOS" : "JUGAR", hasSave ? GameGui.Green : GameGui.Yellow, hasSave ? GameGui.Icon.Levels : GameGui.Icon.Play, ShowLevels);
            Add("CONFIGURACIÓN", GameGui.Purple, GameGui.Icon.Settings, ShowSettings);
            Add("CONTROLES", GameGui.Orange, GameGui.Icon.Keys, ShowControls);
            Add("VER HISTORIA", GameGui.Blue, GameGui.Icon.Star, () => { IntroCinematic.ForceNext = true; LoadScene(IntroCinematic.LevelScene); });
#if !UNITY_WEBGL || UNITY_EDITOR
            Add("SALIR", GameGui.Red, GameGui.Icon.Close, Quit);           // en el navegador no se puede cerrar el juego
#endif

            // --- Título y lema (derecha)
            var t1 = GameGui.Text(page, "Titulo", "CLIPPER", 128, GameGui.Hex(0x8fe9ff));
            Glow(t1);
            MenuStyle.Place(t1.rectTransform, new Vector2(.5f, .5f), new Vector2(250, 130), new Vector2(700, 150));

            var pill = GameGui.Img("Lema", page, GameGui.ButtonSprite(), GameGui.Blue);
            MenuStyle.Place(pill.rectTransform, new Vector2(.5f, .5f), new Vector2(250, 20), new Vector2(520, 60));
            var pt = GameGui.Fit(GameGui.Text(pill.transform, "Texto", "¡APRENDIENDO MATEMÁTICAS CON MAGIA!", 22, Color.white), 12, 22);
            MenuStyle.Stretch(pt.rectTransform); pt.rectTransform.offsetMin = new Vector2(16, 6); pt.rectTransform.offsetMax = new Vector2(-16, -2);

            var defs = LevelMenu.Entries();
            var hint = GameGui.Text(page, "Atajos", "Teclas 1–" + defs.Length + ": abrir un escenario   ·   Esc: volver", 16, new Color(0.75f, 0.9f, 0.92f, 0.75f), TextAnchor.MiddleCenter, false);
            MenuStyle.Place(hint.rectTransform, new Vector2(.5f, 0f), new Vector2(0, 22), new Vector2(900, 24));
        }

        static IEnumerator FadeOut(Graphic g, float seconds)
        {
            for (float t = 0; t < 1f && g != null; t += Time.unscaledDeltaTime / seconds)
            {
                var c = g.color; c.a = 1f - t; g.color = c;
                yield return null;
            }
            if (g != null) g.gameObject.SetActive(false);
        }

        /// Título con contorno oscuro y sombra violeta (aire del texto "LOW POLY" del diseño).
        static void Glow(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.04f, 0.13f, 0.16f, 0.9f); o.effectDistance = new Vector2(3, -3);
            var s = t.GetComponent<Shadow>(); if (s != null && !(s is Outline)) { s.effectColor = new Color(0.45f, 0.3f, 0.9f, 0.55f); s.effectDistance = new Vector2(0, -7); }
        }

        void BuildLevelsPage()
        {
            levelsPage = MenuStyle.Rect("LevelsPage", transform).gameObject;
            MenuStyle.Stretch((RectTransform)levelsPage.transform);

            var rib = GameGui.Ribbon(levelsPage.transform, "Elige un escenario", new Vector2(600, 76), GameGui.Blue, 34);
            MenuStyle.Place(rib.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -56), new Vector2(600, 76));
            var defs = LevelMenu.Entries();
            string keys = defs.Length <= 1 ? "1" : string.Join(", ", System.Linq.Enumerable.Range(1, defs.Length - 1)) + " o " + defs.Length;
            var hint = GameGui.Text(levelsPage.transform, "Pista", "Haz clic en una tarjeta o pulsa " + keys, 18, new Color(0.8f, 0.93f, 0.95f, 0.9f), TextAnchor.MiddleCenter, false);
            MenuStyle.Place(hint.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -116), new Vector2(800, 28));

            // Con más de 3 escenarios las tarjetas se encogen para caber en el ancho de referencia (1280).
            const float step = 395f, maxRow = 1240f;
            float scale = Mathf.Min(1f, maxRow / (defs.Length * step));
            for (int i = 0; i < defs.Length; i++)
            {
                float x = (i - (defs.Length - 1) / 2f) * step * scale;
                var slot = MenuStyle.Rect("Slot_" + defs[i].id, levelsPage.transform);
                MenuStyle.Place(slot, new Vector2(.5f, .5f), new Vector2(x, -40 + (1f - scale) * 60f), new Vector2(365, 480));
                slot.localScale = new Vector3(scale, scale, 1f);
                LevelCard(slot, defs[i], i);
            }

            var back = GameGui.Button(levelsPage.transform, "Volver", "VOLVER", GameGui.Green, GameGui.Icon.Back, ShowMain, 22, new Vector2(210, 56));
            MenuStyle.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(135, 46), new Vector2(210, 56));
        }

        void LevelCard(Transform parent, LevelDef d, int index)
        {
            Color accent; if (!ColorUtility.TryParseHtmlString(d.accent, out accent)) accent = GameGui.Orange;
            string scene = LevelMenu.SceneName(d);

            var card = GameGui.Panel(parent, "Card_" + d.id, new Vector2(365, 480));
            MenuStyle.Stretch(card.rectTransform);
            card.raycastTarget = true;
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.None;
            var cardNav = cardBtn.navigation; cardNav.mode = Navigation.Mode.None; cardBtn.navigation = cardNav;
            cardBtn.onClick.AddListener(() => Load(scene));
            card.gameObject.AddComponent<ButtonBounce>().hoverScale = 1.03f;

            // Imagen del escenario con marco hundido.
            var frame = GameGui.Img("Marco", card.transform, GameGui.Inset(), Color.white);
            MenuStyle.Place(frame.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -122), new Vector2(325, 196));
            frame.raycastTarget = true;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var thumb = MenuStyle.Rect("Thumb", frame.transform).gameObject.AddComponent<RawImage>();
            MenuStyle.Stretch(thumb.rectTransform, 4f);
            thumb.raycastTarget = false;
            SetThumb(thumb, d, 317f / 188f);

            // Insignia "NIVEL n" sobre la imagen.
            var badge = GameGui.Img("Insignia", card.transform, GameGui.ButtonSprite(), accent);
            MenuStyle.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(86, -26), new Vector2(140, 36));
            var bl = GameGui.Fit(GameGui.Text(badge.transform, "Texto", $"{d.lv.ToUpperInvariant()} · {index + 1}", 17, Color.white), 10, 17);
            MenuStyle.Stretch(bl.rectTransform); bl.rectTransform.offsetMin = new Vector2(6, 6); bl.rectTransform.offsetMax = new Vector2(-6, -2);

            var name = GameGui.Fit(GameGui.Text(card.transform, "Nombre", d.name, 28, GameGui.Ink, TextAnchor.MiddleCenter, false), 16, 28);
            MenuStyle.Place(name.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -246), new Vector2(330, 36));
            var sub = GameGui.Fit(GameGui.Text(card.transform, "Sub", d.sub, 16, GameGui.InkSoft, TextAnchor.MiddleCenter, false), 11, 16);
            MenuStyle.Place(sub.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -276), new Vector2(330, 24));
            var tags = GameGui.Fit(GameGui.Text(card.transform, "Tags", d.tags != null ? string.Join("  ·  ", d.tags) : "", 14, GameGui.InkSoft, TextAnchor.MiddleCenter, false), 10, 14);
            MenuStyle.Place(tags.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -308), new Vector2(320, 40));

            var topic = GameGui.Img("Tema", card.transform, GameGui.Inset(), Color.white);
            MenuStyle.Place(topic.rectTransform, new Vector2(.5f, 0f), new Vector2(0, 118), new Vector2(300, 38));
            var tl = GameGui.Fit(GameGui.Text(topic.transform, "Texto", "Tema: " + d.topic, 17, Color.Lerp(accent, Color.white, 0.35f)), 10, 17);
            MenuStyle.Stretch(tl.rectTransform, 4f);

            var play = GameGui.Button(card.transform, "Jugar", "¡JUGAR!", accent, GameGui.Icon.Play, () => Load(scene), 26, new Vector2(250, 62));
            MenuStyle.Place((RectTransform)play.transform, new Vector2(.5f, 0f), new Vector2(0, 54), new Vector2(250, 62));

            // Tarjeta bloqueada: velo oscuro con el requisito para abrirla.
            if (!LevelProgress.IsUnlocked(scene))
            {
                var veil = GameGui.Img("Bloqueado", card.transform, GameGui.PanelDark(), new Color(1, 1, 1, 0.94f));
                MenuStyle.Stretch(veil.rectTransform);
                veil.raycastTarget = true;
                var lockBtn = veil.gameObject.AddComponent<Button>();
                lockBtn.transition = Selectable.Transition.None;
                lockBtn.onClick.AddListener(() => Load(scene));
                var lockIcon = GameGui.Text(veil.transform, "Candado", "BLOQUEADO", 34, GameGui.Yellow);
                MenuStyle.Place(lockIcon.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(330, 50));
                var req = GameGui.Fit(GameGui.Text(veil.transform, "Requisito", $"Termina «{LevelProgress.RequiredLevelName(scene)}»\npara desbloquear este nivel", 18, Color.white, TextAnchor.MiddleCenter, false), 12, 18);
                MenuStyle.Place(req.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -30), new Vector2(320, 60));
            }
        }

        /// Pone la imagen del escenario recortándola para llenar el hueco sin deformarse.
        static void SetThumb(RawImage img, LevelDef d, float slotAspect)
        {
            var tex = Resources.Load<Texture2D>(ThumbFolder + "/" + d.id);
            if (tex == null)
            {
                tex = new Texture2D(1, 2) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                tex.SetPixels(new[] { MenuStyle.Hex(d.pal != null ? d.pal.path : 0x8fb84e), MenuStyle.Hex(d.sky) });
                tex.Apply();
                img.texture = tex;
                img.uvRect = new Rect(0, 0, 1, 1);
                return;
            }
            img.texture = tex;
            float a = (float)tex.width / tex.height;
            img.uvRect = a > slotAspect
                ? new Rect((1f - slotAspect / a) * 0.5f, 0f, slotAspect / a, 1f)
                : new Rect(0f, (1f - a / slotAspect) * 0.5f, 1f, a / slotAspect);
        }

        void BuildControlsPanel()
        {
            var overlay = GameGui.Dim(transform, "ControlsPanel", 0.62f);
            controlsPanel = overlay.gameObject;
            var box = GameGui.Panel(overlay.transform, "Caja", new Vector2(700, 540));
            MenuStyle.Place(box.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -16), new Vector2(700, 540));
            var rib = GameGui.Ribbon(box.transform, "Controles", new Vector2(420, 66));
            MenuStyle.Place(rib.rectTransform, new Vector2(.5f, 1f), new Vector2(0, 8), new Vector2(420, 66));

            string[,] rows =
            {
                { "W A S D / Flechas", "Caminar" }, { "Shift", "Correr" }, { "Espacio", "Saltar" },
                { "Clic izquierdo / Enter", "Lanzar hechizo" }, { "Clic derecho + ratón", "Girar la cámara" },
                { "Q / E  ·  Z / X  ·  Rueda", "Girar · Acercar" }, { "F  ·  H", "Interactuar · Ver pista" }, { "M  ·  Esc", "Mapa · Pausa" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = -82 - i * 46;
                // tecla: cajita oscura con el texto centrado · acción: texto centrado en su columna
                var cap = GameGui.Img("Tecla", box.transform, GameGui.Inset(), Color.white);
                MenuStyle.Place(cap.rectTransform, new Vector2(.5f, 1f), new Vector2(-140, y), new Vector2(300, 38));
                var k = GameGui.Fit(GameGui.Text(cap.transform, "Texto", rows[i, 0], 18, GameGui.Yellow), 11, 18);
                MenuStyle.Stretch(k.rectTransform, 4f);
                var v = GameGui.Fit(GameGui.Text(box.transform, "Accion", rows[i, 1], 20, GameGui.Ink, TextAnchor.MiddleCenter, false), 12, 20);
                MenuStyle.Place(v.rectTransform, new Vector2(.5f, 1f), new Vector2(170, y), new Vector2(280, 38));
            }

            var close = GameGui.Button(box.transform, "Cerrar", "CERRAR", GameGui.Red, GameGui.Icon.Close, () => controlsPanel.SetActive(false), 22, new Vector2(230, 56));
            MenuStyle.Place((RectTransform)close.transform, new Vector2(.5f, 0f), new Vector2(0, 46), new Vector2(230, 56));
            controlsPanel.SetActive(false);
        }
    }
}
