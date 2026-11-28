using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MageHero
{
    /// Tutorial que sigue a la cinemática de apertura. El Gólem de Piedra se queda quieto en el prado (lugar abierto,
    /// sin árboles) y el Maestro Athan —el orbe de luz— enseña, con su voz, los controles básicos:
    /// moverse con W A S D, saltar, mover la cámara y hacer clic para lanzar hechizos (dañar enemigos).
    /// Cada paso espera a que el jugador lo haga de verdad (con barra de progreso). Tab omite el tutorial.
    /// Al terminar, el Gólem ruge, huye al bosque (GolemRetreat) y vuelven los enemigos del nivel.
    public class GolemTutorial : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }

        StoneGolem golem;
        SpiritAthan spirit;
        Transform player;
        Vector3 forest;
        Action onFinished;

        PlayerController pc;
        IsoCameraFollow iso;
        HealthSystem playerHealth;
        CapsuleCollider hitbox;
        int hits;
        bool skip, finished, lookAtGolem;
        Vector3 spiritVel;

        // UI
        Canvas canvas;
        Image panel, fill;
        Text stepLabel, title, body, caption, done;
        RectTransform keyRow, fillRect;
        CanvasGroup group, captionGroup;
        float captionUntil;
        const float FillWidth = 780f;

        public static GolemTutorial Begin(StoneGolem golem, SpiritAthan spirit, Transform player, Vector3 forestPoint, Action onFinished)
        {
            var t = new GameObject("GolemTutorial").AddComponent<GolemTutorial>();
            t.golem = golem; t.spirit = spirit; t.player = player; t.forest = forestPoint; t.onFinished = onFinished;
            return t;
        }

        // ------------------------------------------------------------------ flujo
        IEnumerator Start()
        {
            IsRunning = true;
            pc = player != null ? player.GetComponent<PlayerController>() : null;
            playerHealth = player != null ? player.GetComponent<HealthSystem>() : null;
            iso = Camera.main != null ? Camera.main.GetComponent<IsoCameraFollow>() : null;
            BuildUI();
            Narrator.SubtitleSink = ShowCaption;
            Narrator.Speaking += OnSpeaking;
            SetupGolem();
            UIManager.Instance?.SetObjective("Tutorial: aprende los controles con el Maestro Athan");

            yield return Realtime(0.6f);
            yield return Narrator.SayAndWait("tut_00");

            steps = BuildSteps();
            yield return Flow(0);
        }

        List<Step> steps;
        int currentStep = -1;
        float lastAlive;

        /// Recorre los pasos desde <paramref name="from"/> y termina. El vigilante de Update lo relanza en el mismo
        /// paso si la corrutina deja de avanzar (así el tutorial nunca se queda congelado en un paso).
        IEnumerator Flow(int from)
        {
            for (int i = from; i < steps.Count && !skip; i++)
            {
                currentStep = i;
                yield return RunStep(steps[i], i + 1, steps.Count);
            }
            currentStep = -1;
            yield return Finish();
        }

        class Step
        {
            public string title, body, voice;
            public string[] keys;
            public Func<float> progress;      // 0..1 desde que empieza el paso
            public Action begin;
        }

        List<Step> BuildSteps()
        {
            var list = new List<Step>();

            // 1 · Moverse: recorrer unos metros
            float moved = 0f; Vector3 last = Vector3.zero;
            list.Add(new Step
            {
                title = "Moverse", voice = "tut_mover", keys = new[] { "W", "A", "S", "D" },
                body = "Usa W A S D (o las flechas) para caminar por el prado.",
                begin = () => { moved = 0f; last = player.position; },
                progress = () => { Vector3 d = player.position - last; d.y = 0; moved += d.magnitude; last = player.position; return moved / 5f; },
            });

            // 2 · Saltar
            int jumps = 0; bool wasJumping = false;
            list.Add(new Step
            {
                title = "Saltar", voice = "tut_saltar", keys = new[] { "Espacio" },
                body = "Pulsa la barra espaciadora para saltar. ¡Prueba dos veces!",
                begin = () => { jumps = 0; wasJumping = false; },
                progress = () =>
                {
                    if (pc == null) return 1f;
                    bool j = pc.IsJumping; if (j && !wasJumping) jumps++; wasJumping = j;
                    return jumps / 2f;
                },
            });

            if (iso != null)
            {
                // 3 · Mover la cámara
                float turned = 0f, yaw0 = 0f;
                list.Add(new Step
                {
                    title = "Mover la cámara", voice = "tut_girar", keys = new[] { "Q", "E", "Clic der. + ratón" },
                    body = "Gira la cámara con Q / E, o mantén el clic derecho y mueve el ratón.",
                    begin = () => { turned = 0f; yaw0 = iso.yaw; },
                    progress = () => { turned += Mathf.Abs(Mathf.DeltaAngle(yaw0, iso.yaw)); yaw0 = iso.yaw; return turned / 70f; },
                });
            }

            // 4 · Atacar al Gólem
            list.Add(new Step
            {
                title = "¡Ataca!", voice = "tut_atacar", keys = new[] { "Clic izq.", "Enter" },
                body = "Apunta al Gólem con el ratón y haz clic izquierdo (o Enter) para lanzar un hechizo. Acierta 3 veces.",
                begin = () => { hits = 0; lookAtGolem = true; if (spirit != null) spirit.Reach(golem.Head.position + Vector3.up * 0.3f); },
                progress = () => hits / 3f,
            });
            return list;
        }

        IEnumerator RunStep(Step s, int n, int total)
        {
            ShowStep(n, total, s);
            s.begin?.Invoke();
            Narrator.SayNow(s.voice);
            float p = 0f;
            while (!skip)
            {
                lastAlive = Time.unscaledTime;
                p = Mathf.Max(p, Mathf.Clamp01(s.progress()));
                SetProgress(p);
                if (p >= 1f) break;
                yield return null;
            }
            if (skip) yield break;
            yield return Completed();
        }

        IEnumerator Completed()
        {
            done.text = "¡Hecho!";
            panel.color = Color.Lerp(LowPolyUI.Slate, LowPolyUI.Right, 0.55f);
            MageAudio.PlayUIClick();
            if (spirit != null) StartCoroutine(spirit.Nod(1, 1.2f));
            lastAlive = Time.unscaledTime;
            yield return Realtime(0.9f);
            lastAlive = Time.unscaledTime;
            done.text = "";
            panel.color = LowPolyUI.Slate;
        }

        IEnumerator Finish()
        {
            if (finished) yield break;
            finished = true;
            lookAtGolem = true;
            if (spirit != null) spirit.Reach(null);
            if (!skip)
            {
                // El Gólem acusa los golpes, ruge y se prepara para huir
                ShowStep(0, 0, new Step { title = "¡Bien hecho!", body = "Para vencerlo tendrás que resolver la operación de su pecho. H: pista · F: interactuar.", keys = new[] { "H", "F" } });
                SetProgress(1f);
                yield return Narrator.SayAndWait("tut_final");
            }
            else Narrator.Stop();

            // limpia el tutorial
            if (hitbox != null) Destroy(hitbox);
            var gh = golem != null ? golem.GetComponent<GolemHitbox>() : null; if (gh != null) Destroy(gh);
            yield return FadeOut(0.5f);
            Narrator.Speaking -= OnSpeaking;
            if (Narrator.SubtitleSink == (Action<string, float>)ShowCaption) Narrator.SubtitleSink = null;
            if (spirit != null) spirit.SetTalking(false);

            IsRunning = false;
            onFinished?.Invoke();
            GolemRetreat.Begin(golem, spirit, player, forest);
            if (canvas != null) Destroy(canvas.gameObject);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ cada fotograma
        void Update()
        {
            if (playerHealth != null) playerHealth.SetInvulnerable(0.5f);    // nada hace daño durante el tutorial

            // Vigilante: si el paso actual deja de actualizarse (corrutina detenida), se reanuda ese mismo paso.
            if (steps != null && !finished && currentStep >= 0 && Time.unscaledTime - lastAlive > 2.5f)
            {
                Debug.LogWarning($"[MageHero] Tutorial: el paso {currentStep + 1} dejó de avanzar; se reanuda.");
                StopAllCoroutines();
                lastAlive = Time.unscaledTime;
                if (group != null) group.alpha = 1f;
                StartCoroutine(Flow(currentStep));
            }

            var kb = Keyboard.current;
            if (!skip && !finished && kb != null && kb.tabKey.wasPressedThisFrame && !PauseMenu.IsOpen) skip = true;

            if (golem != null && player != null) golem.LookAt(player.position + Vector3.up * 1.4f);

            // El orbe acompaña al jugador, un poco por delante y a su lado (visible desde la cámara de juego)
            if (spirit != null && player != null)
            {
                Transform cam = Camera.main != null ? Camera.main.transform : null;
                Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : player.right;
                Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : player.forward;
                Vector3 want = player.position + right * 1.4f - fwd * 0.2f;
                want.y = IntroCinematic.Ground(want);
                spirit.transform.position = Vector3.SmoothDamp(spirit.transform.position, want, ref spiritVel, 0.45f, 8f, Time.unscaledDeltaTime);
                Vector3 face = (lookAtGolem && golem != null ? golem.transform.position : player.position) - spirit.transform.position; face.y = 0;
                if (face.sqrMagnitude > 0.01f) spirit.transform.rotation = Quaternion.Slerp(spirit.transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
                spirit.Look(lookAtGolem && golem != null ? golem.Head.position : player.position + Vector3.up * 1.5f);
            }

            // Con una tarjeta de reto o una pista abierta, el tutorial se aparta para no taparla
            var uim = UIManager.Instance;
            if (canvas != null) canvas.enabled = !(uim != null && uim.IsModalOpen);

            if (captionGroup != null)
            {
                bool on = captionUntil > Time.unscaledTime && !string.IsNullOrEmpty(caption.text);
                captionGroup.alpha = Mathf.MoveTowards(captionGroup.alpha, on ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            }
        }

        void OnSpeaking(bool on) { if (spirit != null) spirit.SetTalking(on); }

        // ------------------------------------------------------------------ Gólem: blanco de práctica
        void SetupGolem()
        {
            if (golem == null) return;
            golem.StopAllCoroutines();
            golem.SetWalk(0f); golem.SetEyes(1f); golem.SetCracks(1f); golem.ShowOperation(true);
            // Colisión sólida: el mago no lo atraviesa y los hechizos chocan con él.
            hitbox = golem.gameObject.AddComponent<CapsuleCollider>();
            hitbox.center = new Vector3(0, 2.1f, 0); hitbox.radius = 1.05f; hitbox.height = 4.2f;
            if (GameLayers.Exists(GameLayers.Enemies)) golem.gameObject.layer = GameLayers.Index(GameLayers.Enemies);
            var gh = golem.gameObject.AddComponent<GolemHitbox>();
            gh.Hit += OnGolemHit;
        }

        void OnGolemHit()
        {
            if (finished || golem == null) return;
            hits++;
            golem.Stagger(0.9f);
            golem.Hit(0.9f);                                                   // esquirlas, destello y retroceso
            golem.SetDamage(hits * 0.12f);                                     // las venas de lava se abren un poco
            golem.SetCracks(2.4f);
            CancelInvoke(nameof(CracksBack)); Invoke(nameof(CracksBack), 0.35f);
            CameraShake3D.Shake(0.12f, 0.2f);
            CineGlow.Burst(golem.Chest.TransformPoint(new Vector3(0, 1.08f, 0.7f)), new Color(3f, 1.2f, 0.3f), 24, 2.2f, 0.1f, 0.8f, 0.4f);
            if (hits < 3) ShowCaption($"¡Impacto! {hits} / 3", 1.4f);
        }

        void CracksBack() { if (golem != null) golem.SetCracks(1f); }

        // ------------------------------------------------------------------ UI
        void BuildUI()
        {
            var go = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 420;                         // encima del HUD, del mapa y de la pausa (para guiar en esos pasos)
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;

            panel = LowPolyUI.Panel("Panel", go.transform, 860, 190, LowPolyUI.Slate, 24, 3, 31);
            MenuStyle.Place(panel.rectTransform, new Vector2(.5f, 0f), new Vector2(0, 250), new Vector2(860, 190));   // por encima de las cajas de vida y XP

            var chip = LowPolyUI.Panel("Chip", panel.transform, 250, 40, LowPolyUI.Amber, 20, 2, 32, true, 0.06f);
            MenuStyle.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(150, -6), new Vector2(250, 40));
            stepLabel = LowPolyUI.Text(chip.transform, "Paso", "TUTORIAL", 18, LowPolyUI.Cream);
            MenuStyle.Stretch(stepLabel.rectTransform);

            title = LowPolyUI.Text(panel.transform, "Titulo", "", 32, LowPolyUI.Cream, true, TextAnchor.MiddleLeft);
            MenuStyle.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(240, -58), new Vector2(420, 44));
            done = LowPolyUI.Text(panel.transform, "Hecho", "", 30, new Color(0.7f, 1f, 0.7f), true, TextAnchor.MiddleRight);
            MenuStyle.Place(done.rectTransform, new Vector2(1f, 1f), new Vector2(-130, -24), new Vector2(220, 40));

            body = LowPolyUI.Text(panel.transform, "Texto", "", 22, new Color(1f, 0.96f, 0.88f, 0.92f), false, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            MenuStyle.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(430, -118), new Vector2(800, 60));

            keyRow = MenuStyle.Rect("Teclas", panel.transform);
            MenuStyle.Place(keyRow, new Vector2(1f, 1f), new Vector2(-230, -58), new Vector2(420, 48));
            var hl = keyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childAlignment = TextAnchor.MiddleRight; hl.childControlWidth = false; hl.childControlHeight = false;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            var bar = LowPolyUI.Panel("Barra", panel.transform, 790, 14, LowPolyUI.SlateDeep, 7, 0, 33, true, 0.03f);
            MenuStyle.Place(bar.rectTransform, new Vector2(.5f, 0f), new Vector2(0, 18), new Vector2(790, 14));
            fill = LowPolyUI.Panel("Relleno", bar.transform, 780, 10, LowPolyUI.Amber, 5, 0, 34, true, 0.05f);
            fillRect = fill.rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, .5f); fillRect.pivot = new Vector2(0f, .5f);
            fillRect.anchoredPosition = new Vector2(5, 0); fillRect.sizeDelta = new Vector2(0, 10);

            var hint = LowPolyUI.Text(go.transform, "Omitir", "Tab: omitir tutorial", 16, new Color(1, 1, 1, .6f), false, TextAnchor.MiddleRight);
            MenuStyle.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-150, 30), new Vector2(260, 30));

            // Lo que dice el Maestro Athan (voz en off), encima del panel
            var cg = new GameObject("Voz", typeof(RectTransform), typeof(CanvasGroup));
            cg.transform.SetParent(go.transform, false);
            MenuStyle.Place((RectTransform)cg.transform, new Vector2(.5f, 0f), new Vector2(0, 390), new Vector2(1300, 70));
            captionGroup = cg.GetComponent<CanvasGroup>(); captionGroup.alpha = 0f;
            caption = LowPolyUI.Text(cg.transform, "Texto", "", 26, new Color(0.78f, 0.93f, 1f), false);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.75f);
            MenuStyle.Stretch(caption.rectTransform);

            StartCoroutine(FadeIn(0.6f));
        }

        void ShowStep(int n, int total, Step s)
        {
            stepLabel.text = total > 0 ? $"TUTORIAL  {n} / {total}" : "TUTORIAL";
            title.text = s.title;
            body.text = s.body;
            done.text = "";
            foreach (Transform c in keyRow) Destroy(c.gameObject);
            if (s.keys != null) foreach (var k in s.keys) KeyCap(k);
            SetProgress(0f);
        }

        void KeyCap(string label)
        {
            if (label == "+") { var plus = LowPolyUI.Text(keyRow, "Mas", "+", 26, LowPolyUI.Cream); plus.rectTransform.sizeDelta = new Vector2(22, 44); return; }
            int w = Mathf.Max(48, 22 + label.Length * 13);
            var cap = LowPolyUI.Panel("Tecla_" + label, keyRow, w, 44, LowPolyUI.Tile, 10, 2, 40 + label.Length, false, 0.05f);
            var t = LowPolyUI.Text(cap.transform, "Texto", label, label.Length > 3 ? 17 : 22, LowPolyUI.Cream);
            MenuStyle.Stretch(t.rectTransform);
        }

        void SetProgress(float p)
        {
            if (fillRect != null) fillRect.sizeDelta = new Vector2(FillWidth * Mathf.Clamp01(p), 10);
        }

        void ShowCaption(string text, float seconds)
        {
            if (caption == null) return;
            caption.text = text;
            captionUntil = string.IsNullOrEmpty(text) ? 0f : Time.unscaledTime + seconds;
        }

        IEnumerator FadeIn(float s) { for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / s) { group.alpha = t; yield return null; } group.alpha = 1f; }
        IEnumerator FadeOut(float s) { for (float t = 0; t < 1f && group != null; t += Time.unscaledDeltaTime / s) { group.alpha = 1f - t; yield return null; } }

        static IEnumerator Realtime(float s) { yield return new WaitForSecondsRealtime(s); }

        void OnDestroy()
        {
            IsRunning = false;
            Narrator.Speaking -= OnSpeaking;
            if (Narrator.SubtitleSink == (Action<string, float>)ShowCaption) Narrator.SubtitleSink = null;
        }
    }

    /// Avisa cuando un hechizo del jugador golpea al Gólem del tutorial (el proyectil choca con su cápsula).
    public class GolemHitbox : MonoBehaviour
    {
        public event Action Hit;
        float nextHit;

        void OnTriggerEnter(Collider other)
        {
            if (Time.time < nextHit || other.GetComponentInParent<Projectile3D>() == null) return;
            nextHit = Time.time + 0.15f;
            Hit?.Invoke();
        }
    }
}
