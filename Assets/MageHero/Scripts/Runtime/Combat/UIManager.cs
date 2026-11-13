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
        public class QuizData { public string title, subtitle, question; public string[] options; }

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
        static readonly Color AnswerBg = new Color(1f, 0.83f, 0.35f, 1f), WrongBg = new Color(0.85f, 0.3f, 0.3f, 1f), RightBg = new Color(0.4f, 0.85f, 0.45f, 1f);

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
                if (playerHealthSlider == null) BuildHud(this, canvas.transform);
                if (quizPanel == null) BuildPuzzleUI(this, canvas.transform);
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

            // --- Teclado
            var kb = Keyboard.current;
            if (kb == null) return;
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
            RefreshQuiz(data, "Elige la respuesta correcta (clic o teclas 1-4).");
        }

        public void RefreshQuiz(QuizData data, string feedback)
        {
            quizLocked = false;
            quizTitle.text = data.title;
            quizSubtitle.text = data.subtitle;
            quizQuestion.text = data.question;
            for (int i = 0; i < answerButtons.Length; i++)
            {
                var b = answerButtons[i];
                if (b == null) continue;
                bool has = data.options != null && i < data.options.Length;
                b.gameObject.SetActive(has);
                if (!has) continue;
                b.interactable = true;
                b.GetComponent<Image>().color = AnswerBg;
                b.GetComponentInChildren<Text>().text = $"{i + 1})   {data.options[i]}";
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
            clueBody.text = body;
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
        public static void BuildHud(UIManager ui, Transform canvas)
        {
            var flashImg = Img("DamageFlash", canvas, new Color(0.85f, 0.05f, 0.1f, 0f));
            Stretch(flashImg.rectTransform);
            flashImg.transform.SetAsFirstSibling();

            var panel = Img("PlayerHealthPanel", canvas, Navy);
            Anchor(panel.rectTransform, Vector2.zero, new Vector2(14f, 14f), new Vector2(330f, 58f));
            var line = Img("GoldLine", panel.transform, Gold);
            var lrt = line.rectTransform; lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = Vector2.one; lrt.pivot = new Vector2(.5f, 1f); lrt.sizeDelta = new Vector2(0, 3);
            var title = UIText.Make(panel.transform, "Title", "VIDA", 13, new Color(.6f, .8f, 1f, 1f), TextAnchor.UpperLeft);
            Place(title.rectTransform, new Vector2(12, -8), new Vector2(120, 18));
            var value = UIText.Make(panel.transform, "Value", "100 / 100", 14, Color.white, TextAnchor.UpperRight);
            value.fontStyle = FontStyle.Bold;
            var vrt = value.rectTransform; vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(1, 1); vrt.anchoredPosition = new Vector2(-12, -8); vrt.sizeDelta = new Vector2(140, 18);

            var sgo = new GameObject("PlayerHealthSlider", typeof(RectTransform), typeof(Slider));
            sgo.transform.SetParent(panel.transform, false);
            var srt = (RectTransform)sgo.transform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(.5f, 0);
            srt.offsetMin = new Vector2(12, 10); srt.offsetMax = new Vector2(-12, 28);
            Stretch(Img("Background", sgo.transform, new Color(0.25f, 0.05f, 0.08f, 1f)).rectTransform);
            var area = new GameObject("Fill Area", typeof(RectTransform));
            area.transform.SetParent(sgo.transform, false);
            Stretch((RectTransform)area.transform);
            var fill = Img("Fill", area.transform, new Color(0.36f, 0.85f, 0.42f));
            Stretch(fill.rectTransform);
            var slider = sgo.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = null; slider.targetGraphic = null;
            slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            slider.interactable = false; slider.transition = Selectable.Transition.None;
            var nav = slider.navigation; nav.mode = Navigation.Mode.None; slider.navigation = nav;

            var msg = UIText.Make(canvas, "CombatMessage", "", 30, Color.white);
            msg.fontStyle = FontStyle.Bold;
            var mrt = msg.rectTransform; mrt.anchorMin = mrt.anchorMax = new Vector2(.5f, .6f); mrt.pivot = new Vector2(.5f, .5f); mrt.sizeDelta = new Vector2(1000, 60);
            AddOutline(msg, 2f);

            ui.playerHealthSlider = slider; ui.playerHealthFill = fill; ui.playerHealthText = value;
            ui.damageFlash = flashImg; ui.messageText = msg;
        }

        // ================================================================== Construcción: UI del puzzle
        /// Crea en el Canvas (Screen Space - Overlay) los paneles del quiz y la pista, el aviso de interacción,
        /// el objetivo, la insignia del pergamino y las franjas de cinemática. Todo empieza oculto.
        public static void BuildPuzzleUI(UIManager ui, Transform canvas)
        {
            foreach (var n in new[] { "Letterbox", "Objective", "InteractPrompt", "ClueBadge", "QuizPanel", "CluePanel" })
            { var old = canvas.Find(n); if (old != null) DestroyImmediate(old.gameObject); }

            // Franjas de cinemática
            var lb = new GameObject("Letterbox", typeof(RectTransform)).GetComponent<RectTransform>();
            lb.SetParent(canvas, false); Stretch(lb);
            var top = Img("Top", lb, Color.black).rectTransform;
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one; top.pivot = new Vector2(.5f, 1f); top.sizeDelta = Vector2.zero;
            var bottom = Img("Bottom", lb, Color.black).rectTransform;
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(.5f, 0f); bottom.sizeDelta = Vector2.zero;
            var sub = UIText.Make(lb, "Subtitle", "", 24, Color.white);
            sub.fontStyle = FontStyle.Bold;
            var subrt = sub.rectTransform; subrt.anchorMin = subrt.anchorMax = new Vector2(.5f, 0f); subrt.pivot = new Vector2(.5f, 0f); subrt.anchoredPosition = new Vector2(0, 40); subrt.sizeDelta = new Vector2(1200, 40);
            AddOutline(sub, 2f);

            // Objetivo (debajo de la línea de ayuda, arriba a la izquierda)
            var obj = UIText.Make(canvas, "Objective", "", 16, new Color(1f, 0.88f, 0.45f), TextAnchor.UpperLeft);
            obj.fontStyle = FontStyle.Bold;
            Place(obj.rectTransform, new Vector2(12, -36), new Vector2(1000, 24));
            AddOutline(obj, 1f);

            // Aviso de interacción (abajo al centro)
            var prompt = Img("InteractPrompt", canvas, new Color(Navy.r, Navy.g, Navy.b, 0.92f));
            var prt = prompt.rectTransform; prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(.5f, 0f); prt.anchoredPosition = new Vector2(0, 86); prt.sizeDelta = new Vector2(560, 46);
            var pline = Img("GoldLine", prompt.transform, Gold).rectTransform;
            pline.anchorMin = Vector2.zero; pline.anchorMax = new Vector2(1, 0); pline.pivot = new Vector2(.5f, 0); pline.sizeDelta = new Vector2(0, 3);
            var ptext = UIText.Make(prompt.transform, "Text", "", 20, Color.white);
            ptext.fontStyle = FontStyle.Bold; Stretch(ptext.rectTransform);

            // Insignia del pergamino (sobre la barra de vida)
            var badge = Img("ClueBadge", canvas, new Color(0.95f, 0.88f, 0.68f, 0.95f));
            Anchor(badge.rectTransform, Vector2.zero, new Vector2(14f, 80f), new Vector2(330f, 30f));
            var btext = UIText.Make(badge.transform, "Text", "", 15, new Color(0.35f, 0.2f, 0.08f));
            btext.fontStyle = FontStyle.Bold; Stretch(btext.rectTransform);

            // ---------- Panel del quiz
            var quiz = Img("QuizPanel", canvas, new Color(0f, 0f, 0f, 0.6f));
            Stretch(quiz.rectTransform); quiz.raycastTarget = true;               // bloquea clics al mundo
            var border = Img("Border", quiz.transform, Gold);
            Center(border.rectTransform, new Vector2(688, 488));
            var box = Img("Box", quiz.transform, Navy);
            Center(box.rectTransform, new Vector2(680, 480));
            var qTitle = UIText.Make(box.transform, "Title", "", 30, Gold); qTitle.fontStyle = FontStyle.Bold;
            Top(qTitle.rectTransform, -18, new Vector2(640, 40));
            var qSub = UIText.Make(box.transform, "Subtitle", "", 16, new Color(.65f, .82f, 1f));
            Top(qSub.rectTransform, -60, new Vector2(640, 24));
            var eqBg = Img("EquationBox", box.transform, new Color(0.06f, 0.12f, 0.22f, 1f));
            Top(eqBg.rectTransform, -94, new Vector2(600, 92));
            var qQ = UIText.Make(eqBg.transform, "Equation", "", 46, Color.white); qQ.fontStyle = FontStyle.Bold;
            Stretch(qQ.rectTransform);
            var buttons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var b = MakeButton("Answer_" + (i + 1), box.transform, "", AnswerBg, Navy, 26);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 1f);
                rt.anchoredPosition = new Vector2(i % 2 == 0 ? -152 : 152, -206 - (i / 2) * 76);
                rt.sizeDelta = new Vector2(290, 64);
                buttons[i] = b;
            }
            var qFb = UIText.Make(box.transform, "Feedback", "", 17, Color.white);
            Top(qFb.rectTransform, -366, new Vector2(640, 40));
            qFb.horizontalOverflow = HorizontalWrapMode.Wrap;
            var hintB = MakeButton("HintButton", box.transform, "Ver pista (H)", new Color(0.95f, 0.88f, 0.68f), new Color(0.35f, 0.2f, 0.08f), 18);
            var hrt = (RectTransform)hintB.transform; hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(.5f, 0f); hrt.anchoredPosition = new Vector2(-130, 18); hrt.sizeDelta = new Vector2(250, 46);
            var exitB = MakeButton("ExitButton", box.transform, "Salir (Esc)", new Color(0.75f, 0.25f, 0.3f), Color.white, 18);
            var ert = (RectTransform)exitB.transform; ert.anchorMin = ert.anchorMax = ert.pivot = new Vector2(.5f, 0f); ert.anchoredPosition = new Vector2(150, 18); ert.sizeDelta = new Vector2(200, 46);
            quiz.gameObject.SetActive(false);

            // ---------- Panel del pergamino (pista)
            var clue = Img("CluePanel", canvas, new Color(0f, 0f, 0f, 0.6f));
            Stretch(clue.rectTransform); clue.raycastTarget = true;
            var cBorder = Img("Border", clue.transform, new Color(0.45f, 0.28f, 0.1f));
            Center(cBorder.rectTransform, new Vector2(768, 568));
            var paper = Img("Paper", clue.transform, new Color(0.96f, 0.9f, 0.72f));
            Center(paper.rectTransform, new Vector2(760, 560));
            var cTitle = UIText.Make(paper.transform, "Title", "", 28, new Color(0.45f, 0.22f, 0.05f)); cTitle.fontStyle = FontStyle.Bold;
            Top(cTitle.rectTransform, -16, new Vector2(700, 38));
            var cBody = UIText.Make(paper.transform, "Body", "", 16, new Color(0.22f, 0.13f, 0.05f), TextAnchor.UpperLeft);
            cBody.horizontalOverflow = HorizontalWrapMode.Wrap; cBody.verticalOverflow = VerticalWrapMode.Truncate; cBody.lineSpacing = 1.1f;
            var cbrt = cBody.rectTransform; cbrt.anchorMin = Vector2.zero; cbrt.anchorMax = Vector2.one; cbrt.offsetMin = new Vector2(34, 74); cbrt.offsetMax = new Vector2(-34, -62);
            var closeB = MakeButton("CloseButton", paper.transform, "Cerrar (H / Esc)", new Color(0.45f, 0.28f, 0.1f), Color.white, 18);
            var crt = (RectTransform)closeB.transform; crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(.5f, 0f); crt.anchoredPosition = new Vector2(0, 16); crt.sizeDelta = new Vector2(240, 46);
            clue.gameObject.SetActive(false);

            ui.letterboxTop = top; ui.letterboxBottom = bottom; ui.subtitleText = sub;
            ui.objectiveText = obj;
            ui.promptRoot = prompt.gameObject; ui.promptText = ptext; prompt.gameObject.SetActive(false);
            ui.clueBadge = badge.gameObject; badge.gameObject.SetActive(false);
            ui.quizPanel = quiz.gameObject; ui.quizTitle = qTitle; ui.quizSubtitle = qSub; ui.quizQuestion = qQ; ui.quizFeedback = qFb;
            ui.answerButtons = buttons; ui.quizHintButton = hintB; ui.quizExitButton = exitB;
            ui.cluePanel = clue.gameObject; ui.clueTitle = cTitle; ui.clueBody = cBody; ui.clueCloseButton = closeB;
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
