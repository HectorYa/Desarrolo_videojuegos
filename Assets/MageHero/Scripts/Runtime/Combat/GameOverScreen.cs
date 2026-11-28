using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Pantalla de GAME OVER cuando la vida del mago llega a 0.
    ///  - "Resolver problema": aparece una ecuación con 4 opciones (teclas 1-4 o clic).
    ///    Correcta → el mago revive donde cayó. Incorrecta → vuelve al menú de inicio.
    ///  - "Volver al menú": vuelve al menú de inicio directamente.
    /// Mientras está abierta el juego queda en pausa (Time.timeScale = 0).
    /// La crea CombatBootstrap en cada nivel; construye su propio Canvas, no necesita prefab.
    public class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

        public string menuScene = "MainMenu";
        [Tooltip("Segundos que se ve el resultado antes de revivir o volver al menú.")]
        public float resultDelay = 1.6f;

        static readonly Color Navy = new Color(0.114f, 0.208f, 0.341f, 0.97f), Gold = new Color(1f, 0.718f, 0.012f, 1f);
        static readonly Color Red = new Color(0.92f, 0.25f, 0.25f, 1f), Green = new Color(0.4f, 0.85f, 0.45f, 1f);
        static readonly Color AnswerBg = new Color(0.27f, 0.42f, 0.58f, 1f), ButtonBg = new Color(0.3f, 0.55f, 0.95f, 1f);

        GameObject root, choicePage, quizPage;
        Text equationText, feedbackText;
        readonly Button[] answerButtons = new Button[4];
        readonly Text[] answerLabels = new Text[4];
        ArithmeticEquation equation;
        PlayerRespawn3D player;
        bool answered;
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
            if (root != null && root.activeSelf) Time.timeScale = savedTimeScale;
        }

        // ------------------------------------------------------------------ API
        public void Show(PlayerRespawn3D who)
        {
            player = who;
            answered = false;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            root.SetActive(true);
            choicePage.SetActive(true);
            quizPage.SetActive(false);
        }

        void OpenQuiz()
        {
            equation = ArithmeticEquation.Random();
            equationText.text = UIText.SafeSymbols(equation.text);
            feedbackText.text = "Elige la respuesta correcta (clic o teclas 1-4). Solo tienes un intento.";
            feedbackText.color = Color.white;
            for (int i = 0; i < 4; i++)
            {
                answerLabels[i].text = equation.options[i].ToString();     // la tecla ya va en el bloque de color
                answerButtons[i].image.color = UIManager.AnswerColor(i);
                answerButtons[i].interactable = true;
            }
            choicePage.SetActive(false);
            quizPage.SetActive(true);
        }

        void Answer(int index)
        {
            if (answered || equation == null) return;
            answered = true;
            foreach (var b in answerButtons) b.interactable = false;
            answerButtons[equation.correctIndex].image.color = Green;

            if (index == equation.correctIndex)
            {
                feedbackText.text = "¡Correcto! El mago vuelve a la vida.";
                feedbackText.color = Green;
                StartCoroutine(After(resultDelay, Revive));
            }
            else
            {
                answerButtons[index].image.color = Red;
                feedbackText.text = $"Incorrecto. La respuesta era {equation.answer}. Volviendo al menú...";
                feedbackText.color = Red;
                StartCoroutine(After(resultDelay + 0.8f, GoToMenu));
            }
        }

        void Revive()
        {
            root.SetActive(false);
            Time.timeScale = savedTimeScale;
            if (player != null) player.ReviveHere();
        }

        void GoToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuScene);
        }

        IEnumerator After(float seconds, System.Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        void Update()
        {
            if (!IsOpen || !quizPage.activeSelf || answered) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Answer(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Answer(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Answer(2);
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Answer(3);
        }

        // ------------------------------------------------------------------ UI
        void Build()
        {
            var canvasGo = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;                                      // por encima del HUD, el mapa y la pausa
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // Ventana con el mismo estilo del menú: cinta roja con el título, panel redondeado.
            var box = MenuStyle.Window(canvasGo.transform, "GameOver", "GAME OVER", new Vector2(660, 470), MenuStyle.Red, null, out root);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            // --- Página 1: elegir
            choicePage = MenuStyle.Rect("Choice", box).gameObject;
            MenuStyle.Stretch((RectTransform)choicePage.transform);
            var lost = MenuStyle.Label(choicePage.transform, "¡Has perdido! Tu vida llegó a cero.", 28, Color.white, new Color(0, 0, 0, .6f));
            MenuStyle.Place(lost.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -110), new Vector2(620, 40));
            var offer = MenuStyle.Label(choicePage.transform,
                "Resuelve un problema matemático para revivir.\nSi fallas, volverás al menú de inicio.", 21, MenuStyle.Cream, new Color(0, 0, 0, .6f), false);
            MenuStyle.Place(offer.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -170), new Vector2(620, 60));
            var solve = MenuStyle.GlossyButton("SolveButton", choicePage.transform, "Resolver problema", MenuStyle.Green, 32, OpenQuiz);
            MenuStyle.Place((RectTransform)solve.transform, new Vector2(.5f, 0f), new Vector2(0, 130), new Vector2(400, 80));
            var menu = MenuStyle.GlossyButton("MenuButton", choicePage.transform, "Volver al menú", MenuStyle.Blue, 24, GoToMenu);
            MenuStyle.Place((RectTransform)menu.transform, new Vector2(.5f, 0f), new Vector2(0, 46), new Vector2(300, 60));

            // --- Página 2: problema
            quizPage = MenuStyle.Rect("Quiz", box).gameObject;
            MenuStyle.Stretch((RectTransform)quizPage.transform);
            var eqBg = MenuStyle.Image("EquationBg", quizPage.transform, MenuStyle.RoundedRect(new Color(0.04f, 0.07f, 0.12f, 1f), new Color(0.07f, 0.1f, 0.16f, 1f), MenuStyle.Gold, 20, 3), Color.white);
            MenuStyle.Place(eqBg.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -130), new Vector2(560, 92));
            equationText = MenuStyle.Label(eqBg.transform, "", 48, Color.white, new Color(0, 0, 0, .7f));
            MenuStyle.Stretch(equationText.rectTransform);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var b = GameGui.Button(quizPage.transform, "Answer" + (i + 1), "", UIManager.AnswerColor(i), GameGui.Icon.Number1 + i, () => Answer(idx), 28, new Vector2(270, 62));
                MenuStyle.Place((RectTransform)b.transform, new Vector2(.5f, 1f), new Vector2(i % 2 == 0 ? -145 : 145, i < 2 ? -236 : -310), new Vector2(270, 62));
                answerButtons[i] = b;
                answerLabels[i] = GameGui.Label(b);                         // el texto (no el número del bloque)
            }
            feedbackText = MenuStyle.Label(quizPage.transform, "", 19, Color.white, new Color(0, 0, 0, .6f), false);
            feedbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
            MenuStyle.Place(feedbackText.rectTransform, new Vector2(.5f, 0f), new Vector2(0, 40), new Vector2(610, 50));
        }

        static Image Img(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }

        static Button MakeButton(string name, Transform parent, string label, Color bg, Color fg, int size)
        {
            var img = Img(name, parent, bg);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f); colors.pressedColor = new Color(.75f, .75f, .75f, 1f);
            colors.disabledColor = Color.white; colors.colorMultiplier = 1f;
            b.colors = colors;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            var t = UIText.Make(img.transform, "Label", label, size, fg);
            t.fontStyle = FontStyle.Bold; Stretch(t.rectTransform);
            ButtonSound.Add(b);
            return b;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
