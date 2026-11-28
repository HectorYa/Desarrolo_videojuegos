using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MageHero
{
    /// Controla toda la UI en Screen Space (Canvas "HUD"):
    ///  - Combate: barra de vida (Slider), destello de daño, mensajes.
    ///  - Puzzle: panel del quiz (ecuación + 4 opciones + Ver pista + Salir), panel del pergamino (pista),
    ///    aviso de interacción, objetivo actual, insignia del pergamino y franjas de cinemática.
    /// Mientras un panel está abierto bloquea el control del mago y lo hace invulnerable.
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Serializable]
        public class QuizData
        {
            public string title, subtitle, question; public string[] options;
            /// Segundos para responder. 0 = automático según la operación (suma/resta 10 s, × ÷ 15 s, combinadas 20 s); < 0 = sin límite.
            public float timeLimit;
            /// Qué hacer si se acaba el tiempo. Si es null, cuenta como salir del problema (exitCallback).
            [NonSerialized] public Action onTimeout;
        }

        [Header("Jugador")]
        public HealthSystem playerHealth;
        public Slider playerHealthSlider;
        public Text playerHealthText;
        public Image playerHealthFill;
        public Gradient fillColors = DefaultGradient();

        [Header("Feedback")]
        public Image damageFlash;
        public Text messageText;

        [Header("Quiz")]
        public GameObject quizPanel;
        public Text quizTitle, quizSubtitle, quizQuestion, quizFeedback;
        public Button[] answerButtons = new Button[4];
        public Button quizHintButton, quizExitButton;

        [Header("Pista")]
        public GameObject cluePanel;
        public Text clueTitle, clueBody;
        public Button clueCloseButton;
        public GameObject clueBadge;

        [Header("HUD de exploración")]
        public GameObject promptRoot;
        public Text promptText;
        public Text objectiveText;
        public RectTransform letterboxTop, letterboxBottom;
        public Text subtitleText;

        public bool IsModalOpen => (quizPanel != null && quizPanel.activeSelf) || (cluePanel != null && cluePanel.activeSelf);
        public bool InCinematic { get; private set; }
        /// ZoneHUD lo consulta para no volver al menú cuando Esc solo cierra un panel.
        public static bool BlocksEscape => (Instance != null && (Instance.IsModalOpen || Instance.InCinematic)) || escFrame == Time.frameCount;

        static int escFrame = -1;
        float shown = 1f, flash, messageUntil, letterbox, subtitleUntil;
        int lockCount;
        object promptOwner;
        Action<int> onAnswer;
        Action onExit;
        bool quizLocked, clueAvailable, clueOpenedFromQuiz;
        string clueName = "Pergamino";

        static readonly Color Navy = new Color(0.114f, 0.208f, 0.341f, 1f), Gold = new Color(1f, 0.718f, 0.012f, 1f);
        static Color AnswerBg => GameGui.Blue;
        /// Un color por respuesta, como los botones del diseño (sin verde ni rojo: se reservan para acierto y fallo).
        public static Color AnswerColor(int i) { switch (i) { case 0: return GameGui.Yellow; case 1: return GameGui.Blue; case 2: return GameGui.Purple; default: return GameGui.Orange; } }
        static Color WrongBg => GameGui.Red;
        static Color RightBg => GameGui.Green;

        // Tiempo límite del problema
        Image quizTimerFill; Text quizTimerText;
        float quizTimeLimit, quizTimeLeft;
        Action quizOnTimeout;

        void Awake()
        {
            Instance = this;
            if (fillColors == null) fillColors = DefaultGradient();
        }

        void Start()
        {
            if (playerHealth == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) playerHealth = p.GetComponentInParent<HealthSystem>();
            }
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) { var hud = GameObject.Find("HUD"); if (hud != null) canvas = hud.GetComponent<Canvas>(); }
            if (canvas != null)
            {
                // Siempre se reconstruye con el estilo del menú (sustituye la versión antigua guardada en la escena).
                BuildHud(this, canvas.transform);
                BuildPuzzleUI(this, canvas.transform);
            }
            WireButtons();
            if (quizPanel != null) quizPanel.SetActive(false);
            if (cluePanel != null) cluePanel.SetActive(false);
            if (promptRoot != null) promptRoot.SetActive(false);
            if (clueBadge != null) clueBadge.SetActive(false);
            if (objectiveText != null) objectiveText.text = "";
            if (subtitleText != null) subtitleText.text = "";
            if (messageText != null) messageText.text = "";

            if (playerHealth == null) { Debug.LogWarning("[MageHero] UIManager: no se encontró HealthSystem en el jugador."); return; }
            playerHealth.Damaged += (_, __) => flash = 0.45f;
            shown = playerHealth.Normalized;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void WireButtons()
        {
            for (int i = 0; i < answerButtons.Length; i++)
            {
                if (answerButtons[i] == null) continue;
                int idx = i;
                answerButtons[i].onClick.RemoveAllListeners();
                answerButtons[i].onClick.AddListener(() => Answer(idx));
            }
            if (quizExitButton != null) { quizExitButton.onClick.RemoveAllListeners(); quizExitButton.onClick.AddListener(() => CloseQuiz(true)); }
            if (quizHintButton != null) { quizHintButton.onClick.RemoveAllListeners(); quizHintButton.onClick.AddListener(OpenClueFromQuiz); }
            if (clueCloseButton != null) { clueCloseButton.onClick.RemoveAllListeners(); clueCloseButton.onClick.AddListener(CloseClue); }

            // Sonido (después de RemoveAllListeners para que no se borre).
            foreach (var b in answerButtons) ButtonSound.Add(b);
            ButtonSound.Add(quizExitButton);
            ButtonSound.Add(quizHintButton);
            ButtonSound.Add(clueCloseButton);
        }

        void Update()
        {
            // --- Vida
            if (playerHealth != null && playerHealthSlider != null)
            {
                shown = Mathf.MoveTowards(shown, playerHealth.Normalized, Time.deltaTime * 1.5f);
                playerHealthSlider.value = shown;
                if (playerHealthFill != null) playerHealthFill.color = fillColors.Evaluate(shown);
                if (playerHealthText != null)
                    playerHealthText.text = $"{Mathf.CeilToInt(playerHealth.Current)} / {Mathf.CeilToInt(playerHealth.maxHealth)}";
            }
            if (damageFlash != null)
            {
                flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime);
                damageFlash.color = new Color(0.85f, 0.05f, 0.1f, flash * 0.5f);
            }
            if (messageText != null && messageUntil > 0f && Time.time > messageUntil) { messageText.text = ""; messageUntil = 0f; }

            // --- Cinemática (franjas negras)
            letterbox = Mathf.MoveTowards(letterbox, InCinematic ? 1f : 0f, Time.deltaTime * 2.5f);
            if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(0f, 90f * letterbox);
            if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(0f, 110f * letterbox);
            if (subtitleText != null && subtitleUntil > 0f && Time.time > subtitleUntil) { subtitleText.text = ""; subtitleUntil = 0f; }

            // --- Invulnerable mientras lee o responde (los enemigos no deben castigar por pensar)
            if ((IsModalOpen || InCinematic) && playerHealth != null) playerHealth.SetInvulnerable(0.5f);

            TickQuizTimer();

            // --- Teclado
            var kb = Keyboard.current;
            if (kb == null || PauseMenu.IsOpen || GameOverScreen.IsOpen) return;
            if (kb.escapeKey.wasPressedThisFrame && IsModalOpen)
            {
                escFrame = Time.frameCount;
                if (cluePanel.activeSelf) CloseClue(); else CloseQuiz(true);
                return;
            }
            if (kb.hKey.wasPressedThisFrame && clueAvailable && !InCinematic)
            {
                if (cluePanel.activeSelf) CloseClue();
                else if (quizPanel != null && quizPanel.activeSelf) OpenClueFromQuiz();
                else ShowClue(clueName, ClueItemSystem.Instance != null ? ClueItemSystem.Instance.GetInstructions() : "");
                return;
            }
            if (quizPanel != null && quizPanel.activeSelf && !cluePanel.activeSelf)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Answer(0);
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Answer(1);
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Answer(2);
                else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Answer(3);
            }
        }

        // ================================================================== API general
        public void ShowMessage(string text, float seconds)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageUntil = Time.time + seconds;
        }

        public void ShowPrompt(string text, object owner = null)
        {
            if (promptRoot == null) return;
            promptOwner = owner;
            if (promptText.text != text) promptText.text = text;
            if (!promptRoot.activeSelf) promptRoot.SetActive(true);
        }

        public void HidePrompt(object owner = null)
        {
            if (promptRoot == null || !promptRoot.activeSelf) return;
            if (owner != null && promptOwner != null && owner != promptOwner) return;
            promptRoot.SetActive(false);
            promptOwner = null;
        }

        public void SetObjective(string text) { if (objectiveText != null) objectiveText.text = text; }

        public void BeginCinematic()
        {
            InCinematic = true;
            HidePrompt();
            SetPlayerLocked(true);
        }

        public void SetSubtitle(string text, float seconds = 3.5f)
        {
            if (subtitleText == null) return;
            subtitleText.text = text;
            subtitleUntil = Time.time + seconds;
        }

        public void EndCinematic()
        {
            InCinematic = false;
            if (subtitleText != null) subtitleText.text = "";
            SetPlayerLocked(false);
        }

        /// Bloquea/desbloquea movimiento y ataques del mago (con contador, admite llamadas anidadas).
        public void SetPlayerLocked(bool locked)
        {
            lockCount = Mathf.Max(0, lockCount + (locked ? 1 : -1));
            if (playerHealth == null) return;
            bool on = lockCount == 0;
            var go = playerHealth.gameObject;
            var pc = go.GetComponent<PlayerController>(); if (pc != null) { pc.enabled = on; if (!on) pc.ResetMotion(); }
            var aim = go.GetComponent<ClickAimingCombat3D>(); if (aim != null) aim.enabled = on;
            var pc3 = go.GetComponent<PlayerCombat3D>(); if (pc3 != null && aim == null) pc3.enabled = on;
            if (!on) EnemyAI3D.CalmAround(go.transform.position, 20f, 1f);
        }

        // ================================================================== Quiz
        public void OpenQuiz(QuizData data, Action<int> answerCallback, Action exitCallback)
        {
            if (quizPanel == null) return;
            onAnswer = answerCallback; onExit = exitCallback;
            HidePrompt();
            if (!quizPanel.activeSelf) SetPlayerLocked(true);
            quizPanel.SetActive(true);
            RefreshQuiz(data, "");
            StartQuizTimer(data);
        }

        /// Tiempo según la dificultad: suma/resta 10 s, multiplicación o división 15 s, operaciones combinadas 20 s.
        public static float AutoTimeLimit(string question)
        {
            if (string.IsNullOrEmpty(question)) return 10f;
            int add = 0, mul = 0;
            foreach (char c in question)
            {
                if (c == '+' || c == '−' || c == '-') add++;
                else if (c == '×' || c == '÷' || c == '*' || c == '/') mul++;
            }
            if (mul > 0 && add > 0) return 20f;              // combinadas (p. ej. 7 + 3 × 4)
            if (mul > 0) return 15f;
            if (add >= 2) return 15f;                        // sumas y restas encadenadas
            return 10f;
        }

        void StartQuizTimer(QuizData data)
        {
            quizTimeLimit = data.timeLimit < 0f ? 0f : (data.timeLimit > 0f ? data.timeLimit : AutoTimeLimit(data.question));
            quizTimeLeft = quizTimeLimit;
            quizOnTimeout = data.onTimeout;
            if (quizTimerFill != null) { quizTimerFill.transform.parent.gameObject.SetActive(quizTimeLimit > 0f); quizTimerFill.fillAmount = 1f; quizTimerFill.color = LowPolyUI.Amber; }
            if (quizTimerText != null) { quizTimerText.text = quizTimeLimit > 0f ? Mathf.CeilToInt(quizTimeLimit).ToString() : ""; quizTimerText.color = GameGui.Ink; }
        }

        /// Cuenta atrás en tiempo real (funciona también con el juego en cámara lenta). Se detiene al responder o al leer la pista.
        void TickQuizTimer()
        {
            if (quizTimeLimit <= 0f || quizPanel == null || !quizPanel.activeSelf || quizLocked) return;
            if (cluePanel != null && cluePanel.activeSelf) return;
            if (PauseMenu.IsOpen) return;
            quizTimeLeft -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(quizTimeLeft / quizTimeLimit);
            if (quizTimerFill != null)
            {
                quizTimerFill.fillAmount = k;
                quizTimerFill.color = k > 0.35f ? LowPolyUI.Amber : Color.Lerp(LowPolyUI.Wrong, LowPolyUI.Amber, k / 0.35f);
            }
            if (quizTimerText != null)
            {
                quizTimerText.text = Mathf.CeilToInt(Mathf.Max(0f, quizTimeLeft)).ToString();
                quizTimerText.color = k > 0.35f ? GameGui.Ink : GameGui.Red;
            }
            if (quizTimeLeft > 0f) return;

            // ¡Tiempo! Se bloquean las opciones y se avisa a quien abrió el problema.
            quizLocked = true;
            for (int i = 0; i < answerButtons.Length; i++) { var b = answerButtons[i]; if (b == null) continue; b.interactable = false; b.GetComponent<Image>().color = Color.Lerp(AnswerColor(i), Color.gray, 0.5f); }
            if (quizTitle != null) quizTitle.text = "¡Se acabó el tiempo!";
            var cb = quizOnTimeout; quizOnTimeout = null;
            if (cb != null) cb();
            else StartCoroutine(TimeoutClose());
        }

        System.Collections.IEnumerator TimeoutClose()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            if (quizPanel == null || !quizPanel.activeSelf) yield break;
            quizLocked = false;                                // para que CloseQuiz avise como "salir"
            CloseQuiz(true);
        }


        public void RefreshQuiz(QuizData data, string feedback)
        {
            quizLocked = false;
            quizTitle.text = data.title;
            quizSubtitle.text = data.subtitle;
            quizQuestion.text = UIText.SafeSymbols(data.question);
            for (int i = 0; i < answerButtons.Length; i++)
            {
                var b = answerButtons[i];
                if (b == null) continue;
                bool has = data.options != null && i < data.options.Length;
                b.gameObject.SetActive(has);
                if (!has) continue;
                b.interactable = true;
                b.GetComponent<Image>().color = AnswerColor(i);
                var label = b.transform.Find("Label");
                (label != null ? label.GetComponent<Text>() : b.GetComponentInChildren<Text>()).text = UIText.SafeSymbols(data.options[i]);
            }
            SetFeedback(feedback, Color.white);
            RefreshHintButton();
        }

        public void MarkOptionWrong(int index, string feedback)
        {
            var b = answerButtons[index];
            b.interactable = false;
            b.GetComponent<Image>().color = WrongBg;
            SetFeedback(feedback, new Color(1f, 0.6f, 0.55f));
            if (flash < 0.3f) flash = 0.35f;
        }

        public void LockQuiz(int correctIndex, string feedback)
        {
            quizLocked = true;
            foreach (var b in answerButtons) if (b != null) b.interactable = false;
            answerButtons[correctIndex].GetComponent<Image>().color = RightBg;
            SetFeedback(feedback, new Color(0.6f, 1f, 0.6f));
        }

        public void CloseQuiz(bool notifyExit)
        {
            if (quizPanel == null || !quizPanel.activeSelf) return;
            if (cluePanel.activeSelf) cluePanel.SetActive(false);
            quizPanel.SetActive(false);
            SetPlayerLocked(false);
            var cb = onExit; onExit = null; onAnswer = null;
            if (notifyExit && !quizLocked) cb?.Invoke();
        }

        void Answer(int index)
        {
            if (quizLocked || onAnswer == null || index >= answerButtons.Length || answerButtons[index] == null || !answerButtons[index].interactable) return;
            onAnswer(index);
        }

        void SetFeedback(string text, Color c) { if (quizFeedback != null) { quizFeedback.text = text; quizFeedback.color = c; } }

        void RefreshHintButton()
        {
            if (quizHintButton == null) return;
            quizHintButton.interactable = clueAvailable;
            quizHintButton.GetComponentInChildren<Text>().text = clueAvailable ? "Ver pista (H)" : "Sin pista: explora el mapa";
        }

        // ================================================================== Pista
        public void SetClueAvailable(bool available, string itemName)
        {
            clueAvailable = available;
            clueName = itemName;
            if (clueBadge != null)
            {
                clueBadge.SetActive(available);
                var t = clueBadge.GetComponentInChildren<Text>();
                if (t != null) t.text = $"{itemName}  ·  pulsa H para leer";
            }
            RefreshHintButton();
        }

        public void ShowClue(string title, string body)
        {
            if (cluePanel == null) return;
            clueTitle.text = title;
            clueBody.text = UIText.SafeSymbols(body);
            if (!IsModalOpen) SetPlayerLocked(true);
            cluePanel.SetActive(true);
            cluePanel.transform.SetAsLastSibling();
        }

        void OpenClueFromQuiz()
        {
            if (!clueAvailable) return;
            clueOpenedFromQuiz = true;
            ShowClue(clueName, ClueItemSystem.Instance != null ? ClueItemSystem.Instance.GetInstructions() : "");
        }

        public void CloseClue()
        {
            if (cluePanel == null || !cluePanel.activeSelf) return;
            cluePanel.SetActive(false);
            bool backToQuiz = clueOpenedFromQuiz && quizPanel != null && quizPanel.activeSelf;
            clueOpenedFromQuiz = false;
            if (!backToQuiz) SetPlayerLocked(false);
        }

        // ================================================================== Construcción: HUD de combate
        /// Barra de vida (abajo a la izquierda), destello de daño y mensajes centrales, con el estilo del menú.
        public static void BuildHud(UIManager ui, Transform canvas)
        {
            foreach (var n in new[] { "PlayerHealthPanel", "DamageFlash", "CombatMessage" }) RemoveOld(canvas, n);

            var flashImg = Img("DamageFlash", canvas, new Color(0.85f, 0.05f, 0.1f, 0f));
            Stretch(flashImg.rectTransform);
            flashImg.transform.SetAsFirstSibling();

            var panel = MenuStyle.HudPanel("PlayerHealthPanel", canvas, MenuStyle.Red);
            Anchor(panel.rectTransform, Vector2.zero, new Vector2(14f, 14f), new Vector2(330f, 64f));
            var psh = panel.gameObject.AddComponent<Shadow>(); psh.effectColor = new Color(0, 0, 0, .45f); psh.effectDistance = new Vector2(0, -5);

            var heart = MenuStyle.Image("Heart", panel.transform, MenuStyle.Circle(new Color(1f, .45f, .45f), MenuStyle.RedDark, Color.white, 64, 4), Color.white);
            var hrt0 = heart.rectTransform; hrt0.anchorMin = hrt0.anchorMax = hrt0.pivot = new Vector2(0f, .5f); hrt0.anchoredPosition = new Vector2(8f, 0f); hrt0.sizeDelta = new Vector2(46f, 46f);
            var hl = MenuStyle.Label(heart.transform, "♥", 26, Color.white, MenuStyle.RedDark);
            MenuStyle.Stretch(hl.rectTransform);

            var title = MenuStyle.Label(panel.transform, "VIDA", 15, MenuStyle.Cream, new Color(0, 0, 0, .6f), true, TextAnchor.UpperLeft);
            Place(title.rectTransform, new Vector2(62, -7), new Vector2(120, 20));
            var value = MenuStyle.Label(panel.transform, "100 / 100", 15, Color.white, new Color(0, 0, 0, .6f), true, TextAnchor.UpperRight);
            var vrt = value.rectTransform; vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(1, 1); vrt.anchoredPosition = new Vector2(-12, -7); vrt.sizeDelta = new Vector2(140, 20);

            var sgo = new GameObject("PlayerHealthSlider", typeof(RectTransform), typeof(Slider));
            sgo.transform.SetParent(panel.transform, false);
            var srt = (RectTransform)sgo.transform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(.5f, 0);
            srt.offsetMin = new Vector2(62, 9); srt.offsetMax = new Vector2(-12, 31);
            var barBg = MenuStyle.Bar("Background", sgo.transform, new Color(0.36f, 0.85f, 0.42f), out var fillRt);
            Stretch(barBg.rectTransform);
            var fill = fillRt.GetComponent<Image>();
            var slider = sgo.GetComponent<Slider>();
            slider.fillRect = fillRt; slider.handleRect = null; slider.targetGraphic = null;
            slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            slider.interactable = false; slider.transition = Selectable.Transition.None;
            var nav = slider.navigation; nav.mode = Navigation.Mode.None; slider.navigation = nav;

            var msg = MenuStyle.Label(canvas, "", 34, Color.white, MenuStyle.OrangeDark);
            msg.name = "CombatMessage";
            var mrt = msg.rectTransform; mrt.anchorMin = mrt.anchorMax = new Vector2(.5f, .6f); mrt.pivot = new Vector2(.5f, .5f); mrt.sizeDelta = new Vector2(1000, 60);

            ui.playerHealthSlider = slider; ui.playerHealthFill = fill; ui.playerHealthText = value;
            ui.damageFlash = flashImg; ui.messageText = msg;
        }

        // ================================================================== Construcción: UI del puzzle
        /// Crea en el Canvas los paneles del quiz y la pista (ventanas con cinta y botón X), el aviso de interacción,
        /// el objetivo, la insignia del pergamino y las franjas de cinemática. Todo empieza oculto.
        public static void BuildPuzzleUI(UIManager ui, Transform canvas)
        {
            foreach (var n in new[] { "Letterbox", "Objective", "InteractPrompt", "ClueBadge", "QuizPanel", "CluePanel" }) RemoveOld(canvas, n);

            // Franjas de cinemática
            var lb = new GameObject("Letterbox", typeof(RectTransform)).GetComponent<RectTransform>();
            lb.SetParent(canvas, false); Stretch(lb);
            var top = Img("Top", lb, Color.black).rectTransform;
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one; top.pivot = new Vector2(.5f, 1f); top.sizeDelta = Vector2.zero;
            var bottom = Img("Bottom", lb, Color.black).rectTransform;
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(.5f, 0f); bottom.sizeDelta = Vector2.zero;
            var sub = MenuStyle.Label(lb, "", 26, Color.white, new Color(0, 0, 0, .8f));
            sub.name = "Subtitle";
            var subrt = sub.rectTransform; subrt.anchorMin = subrt.anchorMax = new Vector2(.5f, 0f); subrt.pivot = new Vector2(.5f, 0f); subrt.anchoredPosition = new Vector2(0, 40); subrt.sizeDelta = new Vector2(1200, 40);

            // Objetivo (debajo de la línea de ayuda, arriba a la izquierda)
            var obj = MenuStyle.Label(canvas, "", 17, MenuStyle.Gold, new Color(0, 0, 0, .7f), true, TextAnchor.UpperLeft);
            obj.name = "Objective";
            Place(obj.rectTransform, new Vector2(14, -42), new Vector2(1000, 24));

            // Aviso de interacción (abajo al centro)
            var prompt = MenuStyle.HudPanel("InteractPrompt", canvas, MenuStyle.Gold);
            var prt = prompt.rectTransform; prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(.5f, 0f); prt.anchoredPosition = new Vector2(0, 90); prt.sizeDelta = new Vector2(580, 50);
            var ptext = MenuStyle.Label(prompt.transform, "", 21, MenuStyle.Cream, new Color(0, 0, 0, .6f));
            Stretch(ptext.rectTransform);

            // Insignia del pergamino (sobre la barra de vida)
            var badge = MenuStyle.HudPanel("ClueBadge", canvas, MenuStyle.Gold);
            Anchor(badge.rectTransform, Vector2.zero, new Vector2(14f, 86f), new Vector2(330f, 34f));
            var btext = MenuStyle.Label(badge.transform, "", 15, MenuStyle.Cream, new Color(0, 0, 0, .6f));
            Stretch(btext.rectTransform);

            // ---------- Ventana del problema (estilo low poly): fondo semitransparente, tarjeta facetada,
            // el problema grande, una barra de tiempo y 4 opciones. Nada más.
            var quiz = new GameObject("QuizPanel", typeof(RectTransform), typeof(Image));
            quiz.transform.SetParent(canvas, false);
            Stretch((RectTransform)quiz.transform);
            var backdrop = quiz.GetComponent<Image>(); backdrop.color = LowPolyUI.Backdrop; backdrop.raycastTarget = true;   // transparencia: se ve el juego detrás
            // Tarjeta del reto con el estilo GameGui (Diseños/MenuPrincipal): panel claro, cinta azul,
            // la operación en una caja hundida, barra de tiempo y 4 respuestas de colores con su número.
            var card = GameGui.Panel(quiz.transform, "Card", new Vector2(660, 440));
            card.rectTransform.anchoredPosition = new Vector2(0, 0);
            var qBox = card.rectTransform;

            var chip = GameGui.Ribbon(qBox, "Reto", new Vector2(440, 64), GameGui.Blue, 26);
            var chrt = chip.rectTransform; chrt.anchorMin = chrt.anchorMax = new Vector2(.5f, 1f); chrt.anchoredPosition = new Vector2(0, 10);
            var qTitle = chip.GetComponentInChildren<Text>();

            var eqBg = GameGui.Img("EquationBox", qBox, GameGui.Inset(), Color.white);
            Top(eqBg.rectTransform, -62, new Vector2(580, 118));
            var qQ = GameGui.Fit(GameGui.Text(eqBg.transform, "Question", "", 44, Color.white), 22, 44);
            Stretch(qQ.rectTransform);
            qQ.rectTransform.offsetMin = new Vector2(18, 10); qQ.rectTransform.offsetMax = new Vector2(-18, -10);
            // tamaño mediano que se reduce solo si la operación es larga (nunca se sale de la caja ni corta el signo)

            // barra de tiempo (se vacía; amarillo → rojo) y segundos restantes
            var barBg = GameGui.Img("TimerBar", qBox, GameGui.Inset(), Color.white);
            Top(barBg.rectTransform, -196, new Vector2(500, 20));
            var fill = GameGui.Img("Fill", barBg.transform, GameGui.ButtonSprite(), LowPolyUI.Amber);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1f;
            MenuStyle.Stretch(fill.rectTransform, 3f);
            var timerText = GameGui.Text(qBox, "TimerText", "", 20, GameGui.Ink, TextAnchor.MiddleCenter, false);
            Top(timerText.rectTransform, -192, new Vector2(60, 28));
            timerText.rectTransform.anchoredPosition = new Vector2(292, -192);

            var buttons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var size = new Vector2(288, 68);
                var b = GameGui.Button(qBox, "Answer_" + (i + 1), "", AnswerColor(i), GameGui.Icon.Number1 + i, null, 30, size);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 1f);
                rt.anchoredPosition = new Vector2(i % 2 == 0 ? -152 : 152, -236 - (i / 2) * 82);
                buttons[i] = b;
            }

            // Se conservan (ocultos) para no romper a quien los use: subtítulo, mensajes, pista y salir.
            var hidden = new GameObject("Hidden", typeof(RectTransform)); hidden.transform.SetParent(qBox, false); hidden.SetActive(false);
            var qSub = UIText.Make(hidden.transform, "Sub", "", 16, Color.white);
            var qFb = UIText.Make(hidden.transform, "Feedback", "", 16, Color.white);
            var hintB = MakeButton("HintButton", hidden.transform, "Pista", Color.white, Color.black, 14);
            var exitB = MakeButton("ExitButton", hidden.transform, "Salir", Color.white, Color.black, 14);
            ui.quizTimerFill = fill; ui.quizTimerText = timerText;
            quiz.SetActive(false);

            // ---------- Ventana del pergamino (pista)
            var cBox = MenuStyle.Window(canvas, "CluePanel", "Pergamino", new Vector2(780, 580), MenuStyle.Gold, ui.CloseClue, out var clue);
            var cTitle = cBox.Find("Ribbon").GetComponentInChildren<Text>();
            var paper = MenuStyle.Image("Paper", cBox, MenuStyle.RoundedRect(new Color(0.98f, 0.93f, 0.76f), new Color(0.93f, 0.85f, 0.64f), new Color(0.55f, 0.36f, 0.14f), 18, 4), Color.white);
            var pprt = paper.rectTransform; pprt.anchorMin = Vector2.zero; pprt.anchorMax = Vector2.one; pprt.offsetMin = new Vector2(30, 92); pprt.offsetMax = new Vector2(-30, -74);
            var cBody = UIText.Make(paper.transform, "Body", "", 17, new Color(0.22f, 0.13f, 0.05f), TextAnchor.UpperLeft);
            cBody.horizontalOverflow = HorizontalWrapMode.Wrap; cBody.verticalOverflow = VerticalWrapMode.Truncate; cBody.lineSpacing = 1.1f;
            var cbrt = cBody.rectTransform; cbrt.anchorMin = Vector2.zero; cbrt.anchorMax = Vector2.one; cbrt.offsetMin = new Vector2(22, 16); cbrt.offsetMax = new Vector2(-22, -16);
            var closeB = MenuStyle.GlossyButton("CloseButton", cBox, "Cerrar (H / Esc)", MenuStyle.Green, 22, null);
            var crt = (RectTransform)closeB.transform; crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(.5f, 0f); crt.anchoredPosition = new Vector2(0, 20); crt.sizeDelta = new Vector2(260, 56);
            clue.SetActive(false);

            ui.letterboxTop = top; ui.letterboxBottom = bottom; ui.subtitleText = sub;
            ui.objectiveText = obj;
            ui.promptRoot = prompt.gameObject; ui.promptText = ptext; prompt.gameObject.SetActive(false);
            ui.clueBadge = badge.gameObject; badge.gameObject.SetActive(false);
            ui.quizPanel = quiz; ui.quizTitle = qTitle; ui.quizSubtitle = qSub; ui.quizQuestion = qQ; ui.quizFeedback = qFb;
            ui.answerButtons = buttons; ui.quizHintButton = hintB; ui.quizExitButton = exitB;
            ui.cluePanel = clue; ui.clueTitle = cTitle; ui.clueBody = cBody; ui.clueCloseButton = closeB;
        }

        static void RemoveOld(Transform canvas, string name)
        {
            for (int i = canvas.childCount - 1; i >= 0; i--)
            {
                var c = canvas.GetChild(i);
                if (c.name != name) continue;
                c.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
            }
        }

        // ================================================================== Utilidades de UI
        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.95f, 0.27f, 0.27f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.2f), 0.5f), new GradientColorKey(new Color(0.36f, 0.85f, 0.42f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static Button MakeButton(string name, Transform parent, string label, Color bg, Color fg, int size)
        {
            var img = Img(name, parent, bg);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f); colors.pressedColor = new Color(.8f, .8f, .8f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.55f); colors.colorMultiplier = 1f;
            b.colors = colors;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            var t = UIText.Make(img.transform, "Label", label, size, fg);
            t.fontStyle = FontStyle.Bold; Stretch(t.rectTransform);
            return b;
        }

        static Image Img(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }

        static void AddOutline(Text t, float d)
        {
            var ol = t.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, .65f); ol.effectDistance = new Vector2(d, -d);
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1); rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void Center(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
        }

        static void Top(RectTransform rt, float y, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 1f); rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
