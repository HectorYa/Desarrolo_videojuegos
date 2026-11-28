using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace MageHero
{
    /// Puente del río de level_primavera:
    ///  1) Al acercarse, cinemática: El Gran Cero aparece y derrumba el puente (bloquea el camino principal).
    ///  2) Junto al puente roto, F abre el quiz: ecuación aritmética aleatoria con 4 opciones.
    ///  3) Correcta → el puente se repara (piezas vuelven a su sitio, se quita el bloqueo).
    ///     Incorrecta → −vida (sin morir) y reintento; tras 2 fallos el Gran Cero cambia la ecuación.
    ///  4) "Salir" cierra el quiz para explorar y buscar el Pergamino de Athan (ClueItemSystem).
    public class BridgePuzzleManager : MonoBehaviour
    {
        public enum BridgeState { Intact, Collapsing, Broken, Repairing, Repaired }

        public static BridgePuzzleManager Instance { get; private set; }

        [Header("Piezas del puente (las asigna Tools > Mage Hero > Bridge Puzzle)")]
        public Transform deck;
        public Transform[] rails;
        public Transform[] pilings;

        [Header("Zonas (Trigger Colliders hijos)")]
        public TriggerRelay3D collapseZone;
        public TriggerRelay3D repairZone;
        [Tooltip("Collider sólido (capa Obstacles) que bloquea el paso mientras el puente está roto.")]
        public BoxCollider blocker;
        [Tooltip("Agua visible en el hueco del puente roto (sin collider).")]
        public GameObject gapWater;

        [Header("Materiales")]
        public Material gapMaterial;
        public Material villainDarkMaterial;
        public Material villainRedMaterial;
        public Material repairGlowMaterial;

        [Header("Quiz")]
        [Min(0f)] public float wrongAnswerPenalty = 10f;
        [Min(1)] public int wrongAnswersBeforeNewEquation = 2;
        [Tooltip("Zona donde está el pergamino (solo para el mensaje de ayuda).")]
        public string clueZoneName = "";

        [Header("Eventos")]
        public UnityEvent onCollapsed = new UnityEvent();
        public UnityEvent onRepaired = new UnityEvent();

        public BridgeState State { get; private set; } = BridgeState.Intact;
        public ArithmeticEquation CurrentEquation { get; private set; }

        Transform player;
        HealthSystem playerHealth;
        int wrongCount;
        readonly List<Piece> pieces = new List<Piece>();
        readonly List<GameObject> deckSegments = new List<GameObject>();
        Renderer deckRenderer;
        BridgeDress dress;

        class Piece
        {
            public Transform t; public Vector3 pos; public Quaternion rot; public Vector3 brokenPos; public Quaternion brokenRot;
            public Renderer[] rs; public Material[] mats; public Renderer original;
            public float hangAngle = 24f, hangDrop = 0.35f;
            // derrumbe con física sencilla
            public bool falls, inWater, lean, hang; public float delay, seed, hingeSign; public Vector3 vel, angVel, hinge, leanAxis;
        }

        // ------------------------------------------------------------------ Ciclo de vida
        void Awake()
        {
            Instance = this;
            if (collapseZone == null || repairZone == null || blocker == null || gapWater == null) BuildHelpers();
            blocker.enabled = false;
            gapWater.SetActive(false);
            // El río real (shader ToonWater) ya se ve por el hueco: la antigua losa rectangular a la altura
            // de la cubierta se oculta en las escenas que todavía la tengan.
            var gapRenderer = gapWater.GetComponent<Renderer>();
            if (gapRenderer != null) gapRenderer.enabled = false;
            CurrentEquation = ArithmeticEquation.Random();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) { player = p.transform; playerHealth = p.GetComponentInParent<HealthSystem>(); }
            collapseZone.Entered += _ => { if (State == BridgeState.Intact) StartCoroutine(CollapseCinematic()); };
            FlowDirection();                                                   // la corriente del agua corre bajo el puente
        }

        void Update()
        {
            var ui = UIManager.Instance;
            if (ui == null || State != BridgeState.Broken) return;

            bool near = repairZone.PlayerInside && !ui.IsModalOpen && !ui.InCinematic;
            if (near) ui.ShowPrompt("Pulsa  F  para reparar el puente", this);
            else ui.HidePrompt(this);

            var kb = Keyboard.current; var gp = Gamepad.current;
            bool interact = (kb != null && kb.fKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
            if (near && interact) OpenQuiz();
        }

        // ------------------------------------------------------------------ Quiz
        public void OpenQuiz()
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            ui.HidePrompt(this);
            ui.OpenQuiz(BuildQuizData(), OnAnswer, OnQuizExit);
        }

        UIManager.QuizData BuildQuizData()
        {
            var labels = new string[4];
            for (int i = 0; i < 4; i++) labels[i] = CurrentEquation.options[i].ToString();
            return new UIManager.QuizData
            {
                title = "Repara el puente",
                subtitle = "Resuelve la ecuación del Gran Cero para reconstruirlo",
                question = CurrentEquation.text,
                options = labels
            };
        }

        void OnAnswer(int index)
        {
            var ui = UIManager.Instance;
            if (index == CurrentEquation.correctIndex)
            {
                ui.LockQuiz(index, $"¡Correcto! {CurrentEquation.text.Replace("?", CurrentEquation.answer.ToString())}");
                StartCoroutine(RepairSequence());
                return;
            }

            wrongCount++;
            if (playerHealth != null && wrongAnswerPenalty > 0f) playerHealth.Drain(wrongAnswerPenalty, 1f);
            if (wrongCount >= wrongAnswersBeforeNewEquation)
            {
                wrongCount = 0;
                CurrentEquation = ArithmeticEquation.Random();
                ui.RefreshQuiz(BuildQuizData(), $"¡El Gran Cero cambió la ecuación! (−{wrongAnswerPenalty:0} de vida)");
            }
            else
            {
                ui.MarkOptionWrong(index, $"Incorrecto (−{wrongAnswerPenalty:0} de vida). {CurrentEquation.tip} Inténtalo otra vez.");
            }
        }

        void OnQuizExit()
        {
            var ui = UIManager.Instance;
            if (ui == null || State != BridgeState.Broken) return;
            bool hasClue = ClueItemSystem.Instance != null && ClueItemSystem.Instance.Collected;
            ui.ShowMessage(hasClue ? "Repasa el pergamino (H) y vuelve a intentarlo." : "Explora el mapa: el Pergamino de Athan te enseñará a resolverla.", 3f);
        }

        // ------------------------------------------------------------------ Cinemática de destrucción
        // Planos: vista general del puente → aparece el Gran Cero (acercamiento) → carga energía (contrapicado)
        // → rayo e impacto (cámara lenta) → los tablones caen al río y flotan → el Gran Cero se esfuma → vuelve el juego.
        IEnumerator CollapseCinematic()
        {
            State = BridgeState.Collapsing;
            var ui = UIManager.Instance;
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<IsoCameraFollow>() : null;
            ui?.BeginCinematic();
            MageAudio.BeginExclusiveAudio();                                   // música, ambiente, pasos y enemigos en pausa
            if (follow != null) follow.enabled = false;
            BridgeAxes(out Vector3 axis, out Vector3 side, out float len);
            Vector3 c = transform.position;
            Vector3 toPlayer = player != null ? player.position - c : -axis; toPlayer.y = 0f;
            if (Vector3.Dot(toPlayer, side) < 0f) side = -side;                    // la cámara se queda del lado del jugador

            // 1) vista general lateral del puente
            Vector3 wideP = c + side * (len * 1.25f + 6f) + Vector3.up * 6.5f;
            yield return CamMove(cam, wideP, c + Vector3.up * 0.5f, 1.4f, 45f);

            // 2) el Gran Cero aparece al otro lado (estallido rojo y sube flotando)
            Vector3 away = -toPlayer.normalized; if (away.sqrMagnitude < 0.01f) away = axis;
            Vector3 villainPos = c + away * (LongHalf() + 3.5f) + Vector3.up * 3.4f;
            var villain = BuildGranCero(villainPos);
            HitFX.Explosion(villainPos, 2.2f, new Color(1f, 0.2f, 0.15f));
            CameraShake3D.Shake(0.2f, 0.5f);
            StartCoroutine(Scale(villain.transform, Vector3.zero, Vector3.one, 0.7f));
            StartCoroutine(CamMove(cam, villainPos - away * 7.5f + side * 3.2f + Vector3.up * 0.6f, villainPos, 3.2f, 38f));
            ui?.SetSubtitle("EL GRAN CERO:  ¡Ja, ja! Ningún mago cruzará mi río...  ¡Este puente ahora vale CERO!");
            float voice1 = VillainVoice.Say("grancero_01");                    // voz grave y grotesca
            yield return new WaitForSeconds(Mathf.Max(3.0f, voice1 - 2.4f));    // carga el rayo mientras termina la frase

            // 3) carga: la energía roja converge en su mano y el anillo gira más rápido (contrapicado desde el puente)
            var spin = villain.GetComponentInChildren<SpinRing>();
            if (spin != null) spin.speed = 260f;
            Vector3 hand = villainPos + Vector3.up * 0.6f - away * 0.9f;
            var charge = CineGlow.Motes("Carga", null, hand, new Color(3f, 0.4f, 0.25f), 90f, 0.18f, -2.2f, 0.7f, 1.6f, 0f);
            yield return CamCut(cam, c - away * (LongHalf() * 0.7f) + side * 1.6f + Vector3.up * 0.9f, villainPos + Vector3.up * 0.4f, 34f);
            yield return new WaitForSeconds(1.3f);
            charge.Stop(); Destroy(charge.gameObject, 1f);

            // 4) rayo sostenido contra el centro del puente; impacto en cámara lenta
            Vector3 hit = c + Vector3.up * 0.25f;
            StartCoroutine(CamMove(cam, c + side * (len * 0.9f + 4f) + Vector3.up * 3.2f - away * 1.5f, c + Vector3.up * 0.3f, 0.5f, 42f));
            yield return Beam(hand, hit, 0.55f);
            MageAudio.PlayBridgeBreak(gameObject);
            CameraShake3D.Shake(0.5f, 1.3f);
            HitFX.Explosion(hit, 2.6f, new Color(1f, 0.45f, 0.2f));
            BreakBridge();
            Time.timeScale = 0.35f;
            StartCoroutine(RestoreTime(0.9f));
            yield return SimulateCollapse(3.4f);
            StartCoroutine(FloatDebris());

            // 5) el Gran Cero se burla mientras la cámara rodea el hueco del puente
            StartCoroutine(CamMove(cam, c + side * (len * 0.8f + 5f) + axis * 4f + Vector3.up * 4.5f, c + Vector3.down * 0.6f, 2.8f, 44f));
            ui?.SetSubtitle("EL GRAN CERO:  Si quieres pasar, resuelve mi ecuación...  ¡si es que puedes!");
            float voice2 = VillainVoice.Say("grancero_02");
            yield return new WaitForSeconds(Mathf.Max(2.6f, voice2 + 0.3f));
            HitFX.Explosion(villainPos, 1.6f, new Color(0.6f, 0.15f, 0.6f));
            yield return Scale(villain.transform, Vector3.one, Vector3.zero, 0.45f);
            Destroy(villain);

            if (follow != null) follow.enabled = true;                                // la cámara de juego vuelve sola junto al mago
            ui?.EndCinematic();
            MageAudio.EndExclusiveAudio();                                     // vuelve todo el sonido donde se quedó
            EnemyAI3D.CalmAround(transform.position, 25f, 4f);

            State = BridgeState.Broken;
            onCollapsed.Invoke();
            string where = string.IsNullOrEmpty(clueZoneName) ? "en otra zona del mapa" : $"cerca de «{clueZoneName}»";
            ui?.ShowMessage("¡El puente se derrumbó! Acércate y pulsa F para repararlo.", 4f);
            ui?.SetObjective($"Objetivo: repara el puente (F).  ¿Atascado? Dicen que el Maestro Athan dejó un pergamino {where}.");
        }

        IEnumerator RestoreTime(float realSeconds)
        {
            yield return new WaitForSecondsRealtime(realSeconds);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.4f) { Time.timeScale = Mathf.Lerp(0.35f, 1f, t); yield return null; }
            Time.timeScale = 1f;
        }

        // ---- cámara de la cinemática (se mueve a mano; IsoCameraFollow está apagada)
        int camToken;
        IEnumerator CamMove(Camera cam, Vector3 pos, Vector3 look, float seconds, float fov)
        {
            if (cam == null) yield break;
            int token = ++camToken;
            Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            Quaternion r1 = Quaternion.LookRotation(look - pos, Vector3.up);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                if (token != camToken || cam == null) yield break;
                float k = t * t * (3f - 2f * t);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, pos, k), Quaternion.Slerp(r0, r1, k));
                cam.fieldOfView = Mathf.Lerp(f0, fov, k);
                yield return null;
            }
            if (token == camToken && cam != null) { cam.transform.SetPositionAndRotation(pos, r1); cam.fieldOfView = fov; }
        }

        IEnumerator CamCut(Camera cam, Vector3 pos, Vector3 look, float fov)
        {
            camToken++;
            if (cam != null) { cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up)); cam.fieldOfView = fov; }
            yield return null;
        }

        void BridgeAxes(out Vector3 axis, out Vector3 side, out float len)
        {
            bool alongZ = deck == null || deck.lossyScale.z >= deck.lossyScale.x;
            axis = deck == null ? Vector3.forward : (alongZ ? deck.forward : deck.right);
            axis.y = 0f; axis.Normalize();
            side = Vector3.Cross(Vector3.up, axis);
            len = LongHalf() * 2f;
        }

        /// Rayo rojo sostenido: un prisma brillante que crece desde la mano hasta el puente, con chispas en el impacto.
        IEnumerator Beam(Vector3 from, Vector3 to, float seconds)
        {
            var mat = CineGlow.Make(new Color(4f, 0.6f, 0.35f), 0f, 1f, 0.5f);
            var lp = new LowPoly().Paint(Color.white);
            lp.Prism(6, 0.22f, 0.16f, 1f, true);
            var beam = lp.Spawn("RayoGranCero", null, mat, from, false);
            Vector3 d = to - from;
            beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            var sparks = CineGlow.Motes("ChispasRayo", null, to, new Color(3f, 0.8f, 0.3f), 140f, 0.14f, 4f, 0.6f, 0.4f, 0.6f);
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                float grow = Mathf.Clamp01(t / 0.15f);
                float w = 1f + Mathf.Sin(t * 60f) * 0.25f;
                beam.transform.localScale = new Vector3(w, d.magnitude * grow, w);
                CameraShake3D.Shake(0.08f, 0.1f);
                yield return null;
            }
            sparks.Stop(); Destroy(sparks.gameObject, 1f);
            Destroy(beam); Destroy(mat);
        }

        void BreakBridge()
        {
            pieces.Clear();
            dress = deck != null ? deck.GetComponent<BridgeDress>() : null;
            if (dress != null)
            {
                // Puente vestido (BridgeDresser): se rompen los tablones de verdad. Los del centro saltan y caen al
                // río; cerca del borde del hueco unos quedan sueltos y colgando y otros caen (borde irregular);
                // los largueros se parten por la mitad y cada mitad cuelga hacia el agua.
                dress.animating = true;
                dress.Rest();
                BridgeAxes(out Vector3 ax, out _, out float dlen);
                foreach (var pl in dress.planks)
                {
                    if (pl == null) continue;
                    float f = Vector3.Dot(pl.position - transform.position, ax) / Mathf.Max(0.1f, dlen);
                    float af = Mathf.Abs(f), sgn = f < 0f ? -1f : 1f;
                    bool falls = af < 0.27f || (af < 0.36f && Random.value < 0.4f);
                    bool loose = !falls && af < 0.36f;
                    if (!falls && !loose) continue;                                          // sigue clavado
                    var p = AddPiece(pl, pl.position, pl.rotation);
                    p.hingeSign = sgn;
                    if (falls)
                    {
                        p.falls = true;
                        p.delay = af * 0.25f + Random.Range(0f, 0.15f);                     // primero el centro
                        p.vel = ax * sgn * Random.Range(0.6f, 2.6f) + Vector3.up * Random.Range(2f, 4.8f) + Random.insideUnitSphere * 0.6f;
                        p.angVel = new Vector3(Random.Range(-260f, 260f), Random.Range(-80f, 80f), Random.Range(-260f, 260f));
                    }
                    else { p.hang = true; p.hangAngle = Random.Range(8f, 38f); p.delay = Random.Range(0f, 0.2f); }
                }
                foreach (var st in dress.stringers)
                {
                    if (st == null) continue;
                    var p = AddPiece(st, st.position, st.rotation);
                    p.hang = true; p.hangAngle = Random.Range(26f, 36f); p.hangDrop = 0.06f; p.delay = Random.Range(0.05f, 0.2f);   // gira desde la orilla
                    p.hingeSign = Vector3.Dot(st.position - transform.position, ax) < 0f ? -1f : 1f;
                }
            }
            // La cubierta se parte en 6 tablones: los de los extremos quedan colgando del borde, los centrales caen al río.
            deckRenderer = dress == null && deck != null ? deck.GetComponent<Renderer>() : null;
            if (deck != null && dress == null)
            {
                bool alongZ = deck.lossyScale.z >= deck.lossyScale.x;
                float len = alongZ ? deck.lossyScale.z : deck.lossyScale.x, wid = alongZ ? deck.lossyScale.x : deck.lossyScale.z;
                Vector3 axis = alongZ ? deck.forward : deck.right;
                const int N = 6;
                for (int i = 0; i < N; i++)
                {
                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(seg.GetComponent<Collider>());
                    seg.name = "DeckSegment_" + i;
                    seg.transform.SetParent(transform, true);
                    float f = (i - (N - 1) * 0.5f) / N;
                    seg.transform.SetPositionAndRotation(deck.position + axis * (f * len), deck.rotation * Quaternion.Euler(0f, Random.Range(-3f, 3f), 0f));
                    seg.transform.localScale = alongZ ? new Vector3(wid * Random.Range(0.9f, 1f), deck.lossyScale.y, len / N - 0.12f) : new Vector3(len / N - 0.12f, deck.lossyScale.y, wid * Random.Range(0.9f, 1f));
                    if (deckRenderer != null) seg.GetComponent<Renderer>().sharedMaterial = deckRenderer.sharedMaterial;
                    deckSegments.Add(seg);
                    bool edge = i == 0 || i == N - 1;
                    float sgn = f < 0f ? -1f : 1f;
                    var p = AddPiece(seg.transform, seg.transform.position, seg.transform.rotation);
                    if (edge) p.hang = true;                                                                 // cuelga del borde
                    else
                    {
                        p.falls = true;
                        p.delay = (1f - Mathf.Abs(f) * 2f) * 0.05f + Random.Range(0f, 0.12f);           // primero el centro
                        p.vel = axis * sgn * Random.Range(1.2f, 3f) + Vector3.up * Random.Range(2.5f, 4.5f);
                        p.angVel = new Vector3(Random.Range(-220f, 220f), Random.Range(-60f, 60f), Random.Range(-220f, 220f));
                    }
                    p.hingeSign = sgn;
                }
                if (deckRenderer != null) deckRenderer.enabled = false;
            }
            if (rails != null)
                foreach (var r in rails)
                {
                    if (r == null) continue;
                    Vector3 outward = r.position - transform.position; outward.y = 0f; outward.Normalize();
                    var p = AddPiece(r, r.position, r.rotation);
                    p.falls = true; p.delay = Random.Range(0.05f, 0.25f);
                    p.vel = outward * Random.Range(1f, 2.2f) + Vector3.up * Random.Range(1f, 2.5f);
                    p.angVel = new Vector3(Random.Range(-90f, 90f), 0f, Random.Range(-90f, 90f));
                }
            if (pilings != null)
                foreach (var pl in pilings)
                {
                    if (pl == null) continue;
                    bool central = MiddleFraction(pl.position) < 0.45f;
                    var p = AddPiece(pl, pl.position, pl.rotation);
                    if (central) { p.lean = true; p.delay = Random.Range(0.2f, 0.5f); p.leanAxis = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.right; }
                }
            Splinters(transform.position + Vector3.up * 0.4f, 34);
            blocker.enabled = true;
            gapWater.SetActive(true);
            StartCoroutine(OpenRiverBed(0.5f));
            PushPlayerOffBlocker();
            PushEnemiesOffBridge();
        }

        /// Física sencilla del derrumbe: tiembla, los tablones salen despedidos girando, caen al agua (salpicadura)
        /// y quedan flotando a la deriva. Al final se guarda la pose de cada pieza (la reparación parte de ahí).
        IEnumerator SimulateCollapse(float seconds)
        {
            float waterY = WaterLevel();
            Vector3 flow = FlowDirection();
            AimIntoRiver(waterY);
            // crujido previo: todo el puente vibra un instante
            var basePos = new Dictionary<Piece, Vector3>();
            foreach (var p in pieces) basePos[p] = p.t.position;
            for (float t = 0; t < 0.25f; t += Time.deltaTime)
            {
                foreach (var p in pieces) p.t.position = basePos[p] + Random.insideUnitSphere * 0.05f;
                yield return null;
            }
            foreach (var p in pieces) p.t.position = basePos[p];

            float time = 0f;
            while (time < seconds)
            {
                float dt = Time.deltaTime; time += dt;
                foreach (var p in pieces)
                {
                    if (p.t == null) continue;
                    if (time < p.delay) continue;
                    if (p.falls)
                    {
                        if (!p.inWater)
                        {
                            p.vel += Physics.gravity * dt;
                            p.t.position += p.vel * dt;
                            p.t.rotation = Quaternion.Euler(p.angVel * dt) * p.t.rotation;
                            if (p.t.position.y <= waterY)
                            {
                                p.inWater = true;
                                Splash(new Vector3(p.t.position.x, waterY + 0.05f, p.t.position.z), Mathf.Clamp(p.vel.magnitude / 6f, 0.5f, 1.4f));
                                p.vel = new Vector3(p.vel.x * 0.15f, 0f, p.vel.z * 0.15f);
                                p.angVel *= 0.15f;
                            }
                        }
                        else
                        {
                            // flota: sube a la superficie, se aplana poco a poco y la corriente se lo lleva río abajo
                            p.vel += flow * (0.55f * dt);
                            Vector3 pos = p.t.position;
                            pos += p.vel * dt;
                            pos.y = Mathf.Lerp(pos.y, waterY - 0.08f + Mathf.Sin(time * 2.2f + p.seed) * 0.05f, 1f - Mathf.Exp(-3f * dt));
                            p.t.position = pos;
                            Quaternion flat = Quaternion.LookRotation(Vector3.ProjectOnPlane(p.t.forward, Vector3.up).normalized + Vector3.forward * 0.0001f, Vector3.up);
                            p.t.rotation = Quaternion.Slerp(p.t.rotation, flat * Quaternion.Euler(Mathf.Sin(time + p.seed) * 6f, 0, Mathf.Cos(time * 0.8f + p.seed) * 6f), 1f - Mathf.Exp(-1.5f * dt));
                            p.vel *= 1f - 0.6f * dt;
                        }
                    }
                    else if (p.lean)
                    {
                        // pilar que se inclina y se hunde (al empezar a caer rompe la superficie del agua)
                        float k = Mathf.Clamp01((time - p.delay) / 1.4f);
                        if (!p.inWater && k > 0.35f)
                        {
                            p.inWater = true;
                            Vector3 at = p.t.position + p.leanAxis * 0.6f; at.y = waterY + 0.05f;
                            Splash(at, 0.7f);
                        }
                        p.t.rotation = Quaternion.AngleAxis(Mathf.Lerp(0f, 55f, k * k), p.leanAxis) * p.rot;
                        p.t.position = p.pos + Vector3.down * (1.4f * k);
                    }
                    else if (p.hang)
                    {
                        // tablón del extremo: queda colgando, inclinado hacia el hueco
                        float k = Mathf.Clamp01((time - p.delay) / 0.5f);
                        float ease = 1f - Mathf.Pow(1f - k, 3f) + Mathf.Sin(k * Mathf.PI) * 0.15f;
                        BridgeAxes(out Vector3 ax, out _, out _);
                        p.t.rotation = Quaternion.AngleAxis(-p.hingeSign * p.hangAngle * ease, Vector3.Cross(Vector3.up, ax)) * p.rot;
                        p.t.position = p.pos + Vector3.down * p.hangDrop * ease - ax * p.hingeSign * p.hangDrop * 0.57f * ease;
                    }
                }
                yield return null;
            }
            foreach (var p in pieces) if (p.t != null) { p.brokenPos = p.t.position; p.brokenRot = p.t.rotation; }
        }

        /// Mientras el puente sigue roto, los tablones del río no se quedan congelados: la corriente los
        /// arrastra unos metros (cada vez más despacio), cabecean con las olas, giran poco a poco y de vez
        /// en cuando dejan una onda. La espuma a su alrededor la pinta el shader del agua.
        IEnumerator FloatDebris()
        {
            float waterY = WaterLevel();
            Vector3 flow = FlowDirection();
            var floating = new List<Piece>();
            var anchors = new List<Vector3>();
            var reach = new List<float>();                                     // cuánto puede bajar cada uno sin varar
            foreach (var p in pieces)
                if (p.falls && p.inWater && p.t != null)
                {
                    Vector3 a = KeepInRiver(p.t.position, waterY);
                    float r = 0f;
                    while (r < 14f && !IsShallow(a + flow * (r + 0.5f), waterY)) r += 0.5f;
                    floating.Add(p); anchors.Add(a); reach.Add(r);
                }
            if (floating.Count == 0) yield break;
            // cada pieza llega a una distancia distinta: la corriente las separa en fila río abajo
            var spread = new List<float>();
            float gapStep = Mathf.Clamp(12f / floating.Count, 0.7f, 2.6f);         // con muchos tablones, más juntos
            for (int i = 0; i < floating.Count; i++) spread.Add(1.2f + i * gapStep + Random.Range(-0.4f, 0.4f));
            for (int i = spread.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (spread[i], spread[j]) = (spread[j], spread[i]); }

            float time = 0f, nextRipple = 0.8f;
            while (State == BridgeState.Collapsing || State == BridgeState.Broken)
            {
                float dt = Time.deltaTime; time += dt;
                float travel = 1f - Mathf.Exp(-time / 9f);                       // se alejan y poco a poco se frenan
                for (int i = 0; i < floating.Count; i++)
                {
                    var p = floating[i];
                    if (p.t == null) continue;
                    Vector3 side = Vector3.Cross(Vector3.up, flow);
                    Vector3 target = anchors[i] + flow * Mathf.Min(reach[i], travel * spread[i])
                                   + side * Mathf.Sin(time * 0.35f + p.seed) * 0.2f;
                    target.y = waterY - 0.08f + Mathf.Sin(time * 2.2f + p.seed) * 0.05f;
                    p.t.position = Vector3.Lerp(p.t.position, target, 1f - Mathf.Exp(-1.2f * dt));
                    Quaternion bob = Quaternion.Euler(Mathf.Sin(time * 1.3f + p.seed) * 5f, 0f, Mathf.Cos(time * 1.1f + p.seed) * 5f);
                    Quaternion yaw = Quaternion.AngleAxis((p.seed % 2f - 1f) * 9f * dt, Vector3.up);
                    Vector3 fwd = Vector3.ProjectOnPlane(yaw * p.t.forward, Vector3.up);
                    if (fwd.sqrMagnitude < 1e-4f) fwd = flow;
                    p.t.rotation = Quaternion.Slerp(p.t.rotation, Quaternion.LookRotation(fwd.normalized, Vector3.up) * bob, 1f - Mathf.Exp(-2f * dt));
                }
                if (time >= nextRipple)
                {
                    var p = floating[Random.Range(0, floating.Count)];
                    if (p.t != null) WaterRipples.Add(new Vector3(p.t.position.x, waterY, p.t.position.z), Random.Range(0.35f, 0.6f), 0.45f);
                    nextRipple = time + Random.Range(0.9f, 2.2f);
                }
                yield return null;
            }
        }

        /// Dirección en la que corre el río bajo el puente (perpendicular al puente). La primera vez también
        /// orienta las líneas de corriente del shader del agua (Water_Visual) en esa dirección, para que los
        /// tablones floten hacia donde se ve correr el agua.
        Vector3 FlowDirection()
        {
            if (flowDir.HasValue) return flowDir.Value;
            BridgeAxes(out _, out Vector3 side, out _);
            var wv = GameObject.Find("Water_Visual");
            var r = wv != null ? wv.GetComponent<Renderer>() : null;
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_FlowDir"))
            {
                Vector4 f = r.sharedMaterial.GetVector("_FlowDir");
                if (f.x * side.x + f.z * side.z < 0f) side = -side;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetVector("_FlowDir", new Vector4(side.x, 0f, side.z, 0f));
                r.SetPropertyBlock(block);
            }
            flowDir = side;
            return side;
        }
        Vector3? flowDir;

        // ---- dónde hay agua de verdad bajo el puente (para que nada caiga ni flote sobre la orilla)
        bool spanReady; float spanMin, spanMax;

        /// ¿El fondo en ese punto está por encima (o casi) de la superficie? Toma el impacto más bajo del rayo
        /// (el lecho o el suelo), ignorando el agua y los triggers. Bajo la huella del puente el collider del
        /// terreno es plano (para caminar), así que ahí se mira el cauce justo antes y después del puente.
        bool IsShallow(Vector3 p, float waterY)
        {
            BridgeAxes(out _, out Vector3 side, out _);
            float off = Vector3.Dot(p - transform.position, side), half = ShortHalf() + 1.2f;
            if (Mathf.Abs(off) < half)
            {
                Vector3 q = p - side * off;
                return GroundShallow(q + side * half, waterY) || GroundShallow(q - side * half, waterY);
            }
            return GroundShallow(p, waterY);
        }

        float ShortHalf() => deck == null ? 4.5f : Mathf.Min(deck.lossyScale.x, deck.lossyScale.z) * 0.5f;

        bool GroundShallow(Vector3 p, float waterY)
        {
            int skip = GameLayers.Exists(GameLayers.Water) ? GameLayers.Mask(GameLayers.Water) : 0;
            float low = float.MaxValue;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, waterY + 40f, p.z), Vector3.down, 80f, ~skip, QueryTriggerInteraction.Ignore))
                low = Mathf.Min(low, h.point.y);
            return low > waterY - 0.35f;
        }

        /// Tramo (a lo largo del eje del puente, relativo a su centro) bajo el que hay agua, con margen de orilla.
        void RiverSpan(float waterY, out float min, out float max)
        {
            if (!spanReady)
            {
                BridgeAxes(out Vector3 axis, out _, out float len);
                spanMin = float.MaxValue; spanMax = float.MinValue;
                for (float a = -len; a <= len; a += 0.25f)
                    if (!IsShallow(transform.position + axis * a, waterY)) { spanMin = Mathf.Min(spanMin, a); spanMax = Mathf.Max(spanMax, a); }
                if (spanMin > spanMax) { spanMin = -len * 0.2f; spanMax = len * 0.2f; }
                else
                {
                    spanMin += 0.9f; spanMax -= 0.9f;
                    if (spanMin > spanMax) spanMin = spanMax = (spanMin + spanMax) * 0.5f;
                }
                spanReady = true;
            }
            min = spanMin; max = spanMax;
        }

        Vector3 KeepInRiver(Vector3 pos, float waterY)
        {
            RiverSpan(waterY, out float min, out float max);
            BridgeAxes(out Vector3 axis, out _, out _);
            float a = Vector3.Dot(pos - transform.position, axis);
            return pos + axis * (Mathf.Clamp(a, min, max) - a);
        }

        // ---- el cauce bajo el puente
        // El terreno generado pasa por debajo del puente como una franja de tierra plana (a la altura de la
        // cubierta). Al romperse el puente esa franja se hunde hasta el lecho del río (solo la malla visible:
        // el collider no cambia y el Blocker impide el paso), así el agua del río se ve continua bajo el hueco,
        // con sus orillas y su espuma. Al reparar el puente se restaura la malla original.
        MeshFilter bedFilter; Mesh bedOriginal, bedMesh; MeshRenderer bedHiddenRenderer; GameObject bedProxy;
        readonly List<Renderer> hiddenDecor = new List<Renderer>();

        IEnumerator OpenRiverBed(float seconds)
        {
            float waterY = WaterLevel();
            var mc = TerrainUnderBridge();
            var mf = mc != null ? mc.GetComponent<MeshFilter>() : null;
            var mr = mc != null ? mc.GetComponent<MeshRenderer>() : null;
            if (mf == null || mr == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) yield break;

            BridgeAxes(out Vector3 axis, out Vector3 side, out float len);
            float half = ShortHalf() + 0.6f, probe = ShortHalf() + 1.2f;
            // hasta donde se puede abrir el cauce: deja libre el metro y medio de cada extremo (donde el mago
            // queda al ser empujado fuera del Blocker), con 0.6 m de transición suave
            float axialMax = LongHalf() - 1.0f;
            Mesh src = mf.sharedMesh;
            Mesh colMesh = mc.sharedMesh;
            int[] colTris = colMesh != null && colMesh.isReadable ? colMesh.triangles : null;
            Color32[] colCols = colMesh != null && colMesh.isReadable ? colMesh.colors32 : null;
            var verts = src.vertices;
            var cols = src.colors32;
            bool hasCols = cols.Length == verts.Length;
            var target = (Vector3[])verts.Clone();
            var tr = mf.transform; Vector3 c = transform.position;
            var changed = new List<int>();
            float wetMin = float.MaxValue, wetMax = float.MinValue;

            bool Probe(Vector3 p, out float h, out Color32 col)
            {
                h = 0f; col = new Color32(128, 128, 128, 255);
                if (!mc.Raycast(new Ray(new Vector3(p.x, waterY + 50f, p.z), Vector3.down), out RaycastHit hit, 120f)) return false;
                h = hit.point.y;
                if (colTris != null && colCols != null && colCols.Length > 0)
                {
                    int ti = hit.triangleIndex * 3; Vector3 b = hit.barycentricCoordinate;
                    Color c0 = colCols[colTris[ti]], c1 = colCols[colTris[ti + 1]], c2 = colCols[colTris[ti + 2]];
                    col = c0 * b.x + c1 * b.y + c2 * b.z;
                }
                return true;
            }

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = tr.TransformPoint(verts[i]);
                Vector3 d = w - c;
                float s = Vector3.Dot(d, side), a = Vector3.Dot(d, axis);
                if (Mathf.Abs(s) >= half || Mathf.Abs(a) > axialMax) continue;
                Vector3 basePt = c + axis * a;
                if (!Probe(basePt - side * probe, out float hA, out Color32 cA) || !Probe(basePt + side * probe, out float hB, out Color32 cB)) continue;
                if (Mathf.Min(hA, hB) > waterY - 0.3f) continue;               // ningún lado tiene río: es orilla
                // río a un lado y orilla al otro (el río hace curva bajo el puente): la orilla queda en diagonal
                float k = Mathf.InverseLerp(-probe, probe, s);
                float taper = Mathf.Clamp01((axialMax - Mathf.Abs(a)) / 0.6f);
                float bed = Mathf.Lerp(w.y, Mathf.Lerp(hA, hB, k), taper);
                if (bed >= w.y - 0.05f) continue;
                w.y = bed;
                target[i] = tr.InverseTransformPoint(w);
                if (hasCols) cols[i] = Color32.Lerp(cols[i], Color32.Lerp(cA, cB, k), taper);
                changed.Add(i);
                if (bed < waterY) { wetMin = Mathf.Min(wetMin, a); wetMax = Mathf.Max(wetMax, a); }
            }
            if (changed.Count == 0) yield break;
            if (wetMin <= wetMax) FitBlocker(wetMin, wetMax);                   // que no se pueda caminar sobre el agua

            // copia de la malla (el collider sigue con la original)
            bedOriginal = src;
            bedMesh = Instantiate(src);
            bedMesh.name = src.name + "_CauceAbierto";
            bedMesh.MarkDynamic();
            if (mr.isPartOfStaticBatch)
            {
                bedProxy = new GameObject(mf.name + "_Cauce", typeof(MeshFilter), typeof(MeshRenderer));
                bedProxy.transform.SetPositionAndRotation(tr.position, tr.rotation);
                bedProxy.transform.localScale = tr.lossyScale;
                bedProxy.GetComponent<MeshFilter>().sharedMesh = bedMesh;
                bedProxy.GetComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
                bedHiddenRenderer = mr; mr.enabled = false;
            }
            else { bedFilter = mf; mf.sharedMesh = bedMesh; }
            if (hasCols) bedMesh.SetColors(cols);

            // normales nuevas solo alrededor de lo que se hundió (el resto del terreno queda idéntico)
            var origNormals = src.normals;
            var probeMesh = Instantiate(src); probeMesh.SetVertices(target); probeMesh.RecalculateNormals();
            var newNormals = probeMesh.normals; Destroy(probeMesh);
            var finalNormals = origNormals.Length == verts.Length ? (Vector3[])origNormals.Clone() : newNormals;
            if (origNormals.Length == verts.Length)
            {
                var isChanged = new HashSet<int>(changed);
                var tris = src.triangles;
                for (int t = 0; t < tris.Length; t += 3)
                    if (isChanged.Contains(tris[t]) || isChanged.Contains(tris[t + 1]) || isChanged.Contains(tris[t + 2]))
                        for (int j = 0; j < 3; j++) finalNormals[tris[t + j]] = newNormals[tris[t + j]];
            }

            HideDecorOverGap(c, axis, side, half, axialMax, waterY);

            // la franja se desploma con los tablones
            var cur = (Vector3[])verts.Clone();
            for (float t = 0f; t < 1f && bedMesh != null; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                float e = t * t;
                foreach (int i in changed) cur[i] = Vector3.LerpUnclamped(verts[i], target[i], e);
                bedMesh.SetVertices(cur);
                yield return null;
            }
            if (bedMesh == null) yield break;                                 // se reparó antes de terminar
            bedMesh.SetVertices(target);
            bedMesh.SetNormals(finalNormals);
            bedMesh.RecalculateBounds();
        }

        /// Piedras, hierba o flores que estaban sobre la franja de tierra: se ocultan mientras el puente está roto.
        void HideDecorOverGap(Vector3 c, Vector3 axis, Vector3 side, float half, float axialMax, float waterY)
        {
            Transform bridgeGroup = deck != null ? deck.parent : null;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.isStatic || r is ParticleSystemRenderer) continue;
                if (r.transform.IsChildOf(transform) || (bridgeGroup != null && r.transform.IsChildOf(bridgeGroup))) continue;
                Bounds b = r.bounds;
                if (b.size.magnitude > 5f || b.center.y < waterY + 0.3f) continue;
                Vector3 d = b.center - c;
                if (Mathf.Abs(Vector3.Dot(d, side)) >= half || Mathf.Abs(Vector3.Dot(d, axis)) > axialMax - 0.6f) continue;
                if (IsShallow(b.center, waterY)) continue;
                r.enabled = false;
                hiddenDecor.Add(r);
            }
        }

        /// Agranda el Blocker (solo a lo largo del puente) para cubrir todo el tramo donde ahora se ve agua,
        /// sin pasar del punto donde queda el mago al ser empujado. Al reparar se recupera el tamaño original.
        Vector3? blockerSize, blockerCenter;
        void FitBlocker(float minA, float maxA)
        {
            if (blocker == null) return;
            BridgeAxes(out Vector3 axis, out _, out _);
            Vector3 la = blocker.transform.InverseTransformDirection(axis);
            bool lx = Mathf.Abs(la.x) >= Mathf.Abs(la.z);
            float sign = Mathf.Sign(lx ? la.x : la.z);
            Vector3 size = blocker.size, center = blocker.center;
            blockerSize ??= size; blockerCenter ??= center;
            float cur = (lx ? center.x : center.z) * sign, half = (lx ? size.x : size.z) * 0.5f;
            float limit = LongHalf() - 1.6f;
            float lo = Mathf.Max(-limit, Mathf.Min(cur - half, minA - 0.4f));
            float hi = Mathf.Min(limit, Mathf.Max(cur + half, maxA + 0.4f));
            if (hi <= lo) return;
            if (lx) { size.x = hi - lo; center.x = (hi + lo) * 0.5f * sign; }
            else { size.z = hi - lo; center.z = (hi + lo) * 0.5f * sign; }
            blocker.size = size; blocker.center = center;
            PushPlayerOffBlocker();
            PushEnemiesOffBridge();
        }

        void CloseRiverBed()
        {
            if (blocker != null && blockerSize.HasValue) { blocker.size = blockerSize.Value; blocker.center = blockerCenter.Value; }
            if (bedProxy != null) { Destroy(bedProxy); if (bedHiddenRenderer != null) bedHiddenRenderer.enabled = true; }
            else if (bedFilter != null && bedOriginal != null) bedFilter.sharedMesh = bedOriginal;
            if (bedMesh != null) Destroy(bedMesh);
            foreach (var r in hiddenDecor) if (r != null) r.enabled = true;
            hiddenDecor.Clear();
            bedMesh = null; bedOriginal = null; bedFilter = null; bedProxy = null; bedHiddenRenderer = null;
        }

        /// El MeshCollider más grande bajo el centro del puente (el terreno), ignorando la propia estructura.
        MeshCollider TerrainUnderBridge()
        {
            MeshCollider best = null; float bestSize = 0f;
            foreach (var h in Physics.RaycastAll(transform.position + Vector3.up * 40f, Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!(h.collider is MeshCollider mc)) continue;
                float size = mc.bounds.size.sqrMagnitude;
                if (size > bestSize) { best = mc; bestSize = size; }
            }
            return best;
        }

        /// Ajusta el empuje a lo largo del puente de cada pieza que cae para que aterrice dentro del cauce
        /// (con su parábola: mismo salto vertical, distinto avance).
        void AimIntoRiver(float waterY)
        {
            RiverSpan(waterY, out float min, out float max);
            BridgeAxes(out Vector3 axis, out _, out _);
            float g = -Physics.gravity.y;
            foreach (var p in pieces)
            {
                if (!p.falls || p.t == null) continue;
                float drop = Mathf.Max(0.1f, p.t.position.y - waterY);
                float flight = (p.vel.y + Mathf.Sqrt(p.vel.y * p.vel.y + 2f * g * drop)) / g;
                float a0 = Vector3.Dot(p.t.position - transform.position, axis);
                float va = Vector3.Dot(p.vel, axis);
                float want = a0 + va * flight, land = want;
                // si iba a caer en la orilla, aterriza en la mitad del cauce de ese lado (sin apilarse en el borde)
                if (want > max) land = Random.Range(Mathf.Lerp(min, max, 0.55f), max);
                else if (want < min) land = Random.Range(min, Mathf.Lerp(min, max, 0.45f));
                p.vel += axis * ((land - a0) / flight - va);
            }
        }

        float WaterLevel()
        {
            float fallback = (deck != null ? deck.position.y : transform.position.y) - 1.6f;
            int water = GameLayers.Exists(GameLayers.Water) ? GameLayers.Mask(GameLayers.Water) : 0;
            if (water != 0 && Physics.Raycast(transform.position + Vector3.up * 10f, Vector3.down, out RaycastHit h, 30f, water, QueryTriggerInteraction.Collide))
                if (h.point.y < fallback + 1.2f) return h.point.y;
            return fallback;
        }

        void Splash(Vector3 at, float size)
        {
            var ps = CineGlow.Motes("Salpicadura", null, at, new Color(1.1f, 1.3f, 1.5f), 0f, 0.22f * size, 4.2f * size, 0.9f, 0.3f, 1.4f);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f;
            ps.transform.rotation = Quaternion.LookRotation(Vector3.up);
            ps.Emit(Mathf.RoundToInt(30 * size));
            Destroy(ps.gameObject, 2f);
            WaterRipples.Add(at, size * 1.1f);                                 // anillo de espuma en la superficie (shader)
            var ring = CineGlow.Motes("Ondas", null, at, new Color(0.7f, 0.9f, 1.1f), 0f, 0.5f * size, 1.6f, 0.8f, 0.1f, 0f);
            var rsh = ring.shape; rsh.shapeType = ParticleSystemShapeType.Circle; rsh.radius = 0.2f;
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.Emit(Mathf.RoundToInt(18 * size));
            Destroy(ring.gameObject, 2f);
        }

        /// Astillas de madera que saltan con el impacto (pequeños tablones que giran y caen al río).
        void Splinters(Vector3 at, int count)
        {
            float waterY = WaterLevel();
            for (int i = 0; i < count; i++)
            {
                var lp = new LowPoly().Paint(Color.Lerp(new Color(0.55f, 0.36f, 0.2f), new Color(0.8f, 0.62f, 0.4f), Random.value));
                lp.Box(new Vector3(Random.Range(0.08f, 0.22f), 0.04f, Random.Range(0.3f, 0.7f)));
                var go = lp.Spawn("Astilla", null, IntroSet.VertexToon(), at + Random.insideUnitSphere * 0.6f, false);
                StartCoroutine(Fly(go.transform, Random.insideUnitSphere * 4f + Vector3.up * Random.Range(3f, 7f), Random.insideUnitSphere * 720f, waterY));
            }
        }

        IEnumerator Fly(Transform t, Vector3 v, Vector3 spin, float waterY)
        {
            while (t != null && t.position.y > waterY)
            {
                v += Physics.gravity * Time.deltaTime;
                t.position += v * Time.deltaTime;
                t.Rotate(spin * Time.deltaTime, Space.World);
                yield return null;
            }
            if (t == null) yield break;
            if (Random.value < 0.3f) Splash(t.position, 0.4f);
            for (float k = 0; k < 1f && t != null; k += Time.deltaTime / 2.5f) { t.position += Vector3.down * Time.deltaTime * 0.15f; yield return null; }
            if (t != null) Destroy(t.gameObject);
        }

        Piece AddPiece(Transform t, Vector3 brokenPos, Quaternion brokenRot)
        {
            var r = t.GetComponent<Renderer>();
            Renderer original = null;
            // Piezas vestidas (BridgeDresser): lo que se ve son sus hijos, que siguen al transform al animarlo.
            bool dressed = t.GetComponent<BridgeDressPart>() != null || (dress != null && t.IsChildOf(dress.container));
            if (dressed)
            {
                var visible = new List<Renderer>();
                foreach (var cr in t.GetComponentsInChildren<Renderer>()) if (cr.enabled) visible.Add(cr);
                var dp = new Piece { t = t, pos = t.position, rot = t.rotation, brokenPos = brokenPos, brokenRot = brokenRot, rs = visible.ToArray(), seed = Random.value * 10f };
                dp.mats = System.Array.ConvertAll(dp.rs, x => x.sharedMaterial);
                pieces.Add(dp);
                return dp;
            }
            // En los niveles cuyas piezas son estáticas (static batching) moverlas no cambia lo que se ve:
            // se oculta la original y se anima una copia con la misma forma y material.
            if (r != null && r.isPartOfStaticBatch)
            {
                original = r;
                var mf = t.GetComponent<MeshFilter>();
                var proxy = new GameObject(t.name + "_Anim", typeof(MeshFilter), typeof(MeshRenderer));
                proxy.transform.SetParent(transform, true);
                proxy.transform.SetPositionAndRotation(t.position, t.rotation);
                proxy.transform.localScale = t.lossyScale;
                Mesh mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || mesh.name.StartsWith("Combined Mesh"))
                {
                    var tmp = GameObject.CreatePrimitive(t.name.Contains("Piling") ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                    mesh = tmp.GetComponent<MeshFilter>().sharedMesh; Destroy(tmp);
                }
                proxy.GetComponent<MeshFilter>().sharedMesh = mesh;
                var pr = proxy.GetComponent<MeshRenderer>(); pr.sharedMaterial = r.sharedMaterial;
                r.enabled = false;
                t = proxy.transform; r = pr;
            }
            var piece = new Piece { t = t, pos = t.position, rot = t.rotation, brokenPos = brokenPos, brokenRot = brokenRot, original = original, seed = Random.value * 10f,
                                    rs = r != null ? new[] { r } : new Renderer[0], mats = r != null ? new[] { r.sharedMaterial } : new Material[0] };
            pieces.Add(piece);
            return piece;
        }

        // ------------------------------------------------------------------ Reparación
        // Números dorados suben del agua y cada pieza vuela en arco de vuelta a su sitio, de las orillas hacia el centro,
        // con un destello al encajar. Al final una onda de luz recorre el puente.
        IEnumerator RepairSequence()
        {
            State = BridgeState.Repairing;
            var ui = UIManager.Instance;
            yield return new WaitForSecondsRealtime(1.1f);
            ui?.CloseQuiz(false);
            ui?.BeginCinematic();
            ui?.SetSubtitle("¡La ecuación es correcta! La magia de los números reconstruye el puente...");
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<IsoCameraFollow>() : null;
            if (follow != null) follow.enabled = false;
            BridgeAxes(out Vector3 axis, out Vector3 side, out float len);
            Vector3 c = transform.position;
            Vector3 toPlayer = player != null ? player.position - c : side; toPlayer.y = 0f;
            if (Vector3.Dot(toPlayer, side) < 0f) side = -side;
            StartCoroutine(CamMove(cam, c + side * (len * 1.05f + 5f) + Vector3.up * 4.5f - axis * 2f, c, 1.2f, 44f));

            if (repairGlowMaterial != null)
                foreach (var p in pieces) foreach (var pr in p.rs) if (pr != null) pr.sharedMaterial = repairGlowMaterial;
            var lightGo = new GameObject("RepairLight");
            lightGo.transform.position = transform.position + Vector3.up * 2f;
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.range = 14f; light.intensity = 4f; light.color = new Color(0.5f, 1f, 1f);
            StartCoroutine(RisingNumbers(c, len, 2.8f));
            yield return new WaitForSeconds(0.6f);

            // de las orillas al centro
            var order = new List<Piece>(pieces);
            order.Sort((a, b) => MiddleFraction(b.pos).CompareTo(MiddleFraction(a.pos)));
            const float flight = 0.9f, stagger = 0.16f;
            for (int i = 0; i < order.Count; i++) StartCoroutine(FlyBack(order[i], i * stagger, flight));
            StartCoroutine(CamMove(cam, c + side * (len * 0.75f + 4f) + Vector3.up * 3f + axis * 2f, c + Vector3.up * 0.3f, order.Count * stagger + flight, 40f));
            yield return new WaitForSeconds(order.Count * stagger + flight + 0.1f);

            // onda de luz a lo largo del puente
            for (int k = 0; k < 7; k++)
            {
                float f = (k / 6f - 0.5f);
                CineGlow.Burst(c + axis * f * len + Vector3.up * 0.4f, new Color(1.6f, 2.4f, 2.6f), 14, 1.6f, 0.1f, 1f, -0.3f);
            }
            CameraShake3D.Shake(0.12f, 0.3f);
            foreach (var p in pieces) for (int i = 0; i < p.rs.Length; i++) if (p.rs[i] != null) p.rs[i].sharedMaterial = p.mats[i];
            Destroy(lightGo);

            foreach (var p in pieces) if (p.original != null) { p.original.enabled = true; Destroy(p.t.gameObject); }
            foreach (var s in deckSegments) Destroy(s);
            deckSegments.Clear();
            if (deckRenderer != null) deckRenderer.enabled = true;
            if (dress != null) { dress.Rest(); dress.animating = false; }        // los tablones vuelven a ceder bajo los pies
            blocker.enabled = false;
            gapWater.SetActive(false);
            CloseRiverBed();
            yield return new WaitForSeconds(0.8f);

            if (follow != null) follow.enabled = true;
            ui?.EndCinematic();
            State = BridgeState.Repaired;
            ui?.SetObjective("");
            ui?.ShowMessage("¡Puente reparado! El camino está libre.", 3.5f);
            onRepaired.Invoke();
        }

        /// Una pieza sale del agua y vuela en arco hasta su sitio, girando, con estela dorada y destello al encajar.
        IEnumerator FlyBack(Piece p, float delay, float seconds)
        {
            yield return new WaitForSeconds(delay);
            if (p.t == null) yield break;
            if ((p.t.position - p.pos).sqrMagnitude < 0.01f) { CineGlow.Burst(p.pos, new Color(2.6f, 2.1f, 0.9f), 8, 0.8f, 0.07f, 0.6f, -0.2f); yield break; }   // no se movió
            Vector3 a = p.t.position; Quaternion ra = p.t.rotation;
            Vector3 mid = Vector3.Lerp(a, p.pos, 0.5f) + Vector3.up * 2.4f;
            var trail = CineGlow.Motes("Estela", p.t, Vector3.zero, new Color(2.2f, 1.7f, 0.6f), 50f, 0.1f, 0.2f, 0.6f, 0.25f, -0.1f);
            if (a.y < WaterLevel() + 0.3f) Splash(a, 0.6f);
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float k = t * t * (3f - 2f * t);
                Vector3 q = Vector3.Lerp(Vector3.Lerp(a, mid, k), Vector3.Lerp(mid, p.pos, k), k);      // curva de Bézier
                p.t.position = q;
                p.t.rotation = Quaternion.Slerp(ra, p.rot, k) * Quaternion.Euler(0f, (1f - k) * 180f, 0f);
                yield return null;
            }
            p.t.SetPositionAndRotation(p.pos, p.rot);
            trail.Stop(); Destroy(trail.gameObject, 1f);
            CineGlow.Burst(p.pos, new Color(2.6f, 2.1f, 0.9f), 18, 1.2f, 0.08f, 0.7f, -0.2f);
            CameraShake3D.Shake(0.04f, 0.1f);
        }

        /// Números dorados que suben del río mientras se repara el puente.
        IEnumerator RisingNumbers(Vector3 center, float len, float seconds)
        {
            string syms = "0123456789+−×÷=";
            BridgeAxes(out Vector3 axis, out Vector3 side, out _);
            float waterY = WaterLevel();
            for (float t = 0; t < seconds; t += 0.12f)
            {
                Vector3 p = center + axis * Random.Range(-0.5f, 0.5f) * len + side * Random.Range(-2.5f, 2.5f);
                p.y = waterY + 0.2f;
                var gn = CineGlow.Number(syms[Random.Range(0, syms.Length)].ToString(), null, p, new Color(2.6f, 2f, 0.7f), Random.Range(0.3f, 0.55f));
                gn.FadeTo(1f, 3f);
                StartCoroutine(RiseAndFade(gn));
                yield return new WaitForSeconds(0.12f);
            }
        }

        IEnumerator RiseAndFade(GlowNumber gn)
        {
            float speed = Random.Range(1.2f, 2.2f);
            for (float t = 0; t < 2.2f && gn != null; t += Time.deltaTime)
            {
                gn.transform.position += Vector3.up * speed * Time.deltaTime;
                if (t > 1.4f) gn.FadeTo(0f, 2f);
                yield return null;
            }
            if (gn != null) Destroy(gn.gameObject);
        }

        // ------------------------------------------------------------------ Ayudantes
        /// Crea (si faltan) las zonas trigger, el bloqueo y el agua del hueco a partir del tamaño de la cubierta.
        /// Lo usa el menú del editor para que queden visibles en la escena.
        public void BuildHelpers()
        {
            Vector3 size = deck != null ? deck.lossyScale : new Vector3(9f, 0.2f, 14f);
            bool alongZ = size.z >= size.x;
            float len = alongZ ? size.z : size.x, wid = alongZ ? size.x : size.z;
            Vector3 Box(float l, float h, float w) => alongZ ? new Vector3(w, h, l) : new Vector3(l, h, w);
            float topY = deck != null ? deck.position.y + size.y * 0.5f - transform.position.y : 0.1f;

            if (collapseZone == null)
            {
                var c = NewChild("CollapseTrigger").AddComponent<BoxCollider>();
                c.isTrigger = true; c.size = Box(len + 14f, 6f, wid + 10f); c.center = Vector3.up * 2f;
                collapseZone = c.gameObject.AddComponent<TriggerRelay3D>();
            }
            if (repairZone == null)
            {
                var c = NewChild("RepairZone").AddComponent<BoxCollider>();
                c.isTrigger = true; c.size = Box(len + 5f, 5f, wid + 3f); c.center = Vector3.up * 1.5f;
                repairZone = c.gameObject.AddComponent<TriggerRelay3D>();
            }
            if (blocker == null)
            {
                var go = NewChild("Blocker");
                if (GameLayers.Exists(GameLayers.Obstacles)) GameLayers.Set(go, GameLayers.Obstacles);
                blocker = go.AddComponent<BoxCollider>();
                blocker.size = Box(len * 0.55f, 5f, wid); blocker.center = Vector3.up * 2f;
                blocker.enabled = false;
            }
            if (gapWater == null)
            {
                // Solo un marcador del hueco (sin malla): el agua que se ve es la del río, con el shader ToonWater.
                gapWater = NewChild("GapWater");
                gapWater.transform.localPosition = Vector3.up * (topY + 0.02f);
                gapWater.SetActive(false);
            }
        }

        GameObject NewChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = 2;   // Ignore Raycast: los triggers no interceptan el clic de apuntado
            return go;
        }

        /// El Gran Cero (versión low poly para la cinemática del puente): torso de obsidiana facetada flotando,
        /// ojos y grietas de lava, un gran anillo "0" de energía roja que gira detrás, símbolos que orbitan y brasas.
        GameObject BuildGranCero(Vector3 pos)
        {
            var root = new GameObject("ElGranCero");
            root.transform.position = pos;
            var body = new GameObject("Cuerpo").transform; body.SetParent(root.transform, false);
            var obs = new LowPoly();
            Color o1 = new Color(0.17f, 0.15f, 0.22f), o2 = new Color(0.24f, 0.21f, 0.31f), o3 = new Color(0.31f, 0.27f, 0.4f);
            obs.Paint(o2).At(new Vector3(0, 0.2f, 0), Quaternion.Euler(0, 20, 0), new Vector3(1.5f, 1.7f, 1.1f)).Rock(0.6f, 3, 0.22f);       // torso
            obs.Paint(o3).At(new Vector3(0, 1.25f, 0.05f), Quaternion.Euler(0, 40, 0), new Vector3(0.9f, 0.85f, 0.8f)).Rock(0.5f, 4, 0.2f); // cabeza
            obs.Paint(o1).At(new Vector3(-0.95f, 0.65f, 0), Quaternion.Euler(0, 0, 30), new Vector3(0.7f, 0.6f, 0.7f)).Rock(0.5f, 5, 0.25f); // hombros
            obs.Paint(o1).At(new Vector3(0.95f, 0.65f, 0), Quaternion.Euler(0, 0, -30), new Vector3(0.7f, 0.6f, 0.7f)).Rock(0.5f, 6, 0.25f);
            obs.Paint(o2).At(new Vector3(-1.2f, -0.1f, 0.3f), Quaternion.Euler(20, 0, 15), new Vector3(0.45f, 1.1f, 0.45f)).Rock(0.5f, 7, 0.2f); // brazos
            obs.Paint(o2).At(new Vector3(1.2f, -0.1f, 0.3f), Quaternion.Euler(20, 0, -15), new Vector3(0.45f, 1.1f, 0.45f)).Rock(0.5f, 8, 0.2f);
            obs.Paint(o1).At(new Vector3(0, -0.9f, 0), Quaternion.Euler(180, 0, 0), Vector3.one).Prism(7, 0.55f, 0.05f, 1.4f, false, 0.6f); // cola de humo sólido
            obs.Spawn("Obsidiana", body, IntroSet.VertexToon());
            var lava = CineGlow.Make(new Color(4f, 0.7f, 0.25f), 0f, 1f, 0.35f);
            var cr = new LowPoly().Paint(Color.white);
            cr.At(new Vector3(0, 0.3f, 0.62f), Quaternion.Euler(0, 0, 20), Vector3.one).Box(new Vector3(0.05f, 0.8f, 0.04f));
            cr.At(new Vector3(0.2f, 0.1f, 0.6f), Quaternion.Euler(0, 0, -35), Vector3.one).Box(new Vector3(0.05f, 0.5f, 0.04f));
            cr.At(new Vector3(-0.17f, 1.3f, 0.43f), Quaternion.identity, new Vector3(1.4f, 0.6f, 0.6f)).Rock(0.07f, 1, 0f, 0);   // ojos
            cr.At(new Vector3(0.17f, 1.3f, 0.43f), Quaternion.identity, new Vector3(1.4f, 0.6f, 0.6f)).Rock(0.07f, 2, 0f, 0);
            cr.Spawn("Lava", body, lava, default, false);

            // Anillo "0" (óvalo de segmentos de energía) detrás del cuerpo, siempre de cara a la cámara y girando
            var ringRoot = new GameObject("AuraCero").transform; ringRoot.SetParent(root.transform, false);
            ringRoot.gameObject.AddComponent<Billboard>();
            var ring = new GameObject("Anillo").transform; ring.SetParent(ringRoot, false); ring.localPosition = new Vector3(0, 0.35f, 0.6f);
            var rm = CineGlow.Make(new Color(3.5f, 0.45f, 0.3f), 0f, 1f, 0.4f);
            var rl = new LowPoly().Paint(Color.white);
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                rl.At(new Vector3(Mathf.Cos(a) * 1.7f, Mathf.Sin(a) * 2.3f, 0), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg), Vector3.one).Box(new Vector3(0.12f, 0.5f, 0.06f));
            }
            rl.Spawn("Segmentos", ring, rm, default, false);
            ring.gameObject.AddComponent<SpinRing>();
            string[] vars = { "x", "y", "+", "÷", "0" };
            for (int i = 0; i < vars.Length; i++)
            {
                float a = i / (float)vars.Length * Mathf.PI * 2f;
                var gn = CineGlow.Number(vars[i], root.transform, new Vector3(Mathf.Cos(a) * 2.5f, 0.4f + Mathf.Sin(a) * 1.2f, Mathf.Sin(a) * 0.5f), new Color(3f, 0.5f, 0.35f), 0.45f);
                gn.FadeTo(1f, 2f);
            }
            CineGlow.Motes("Brasas", root.transform, Vector3.zero, new Color(3f, 0.6f, 0.2f), 25f, 0.12f, 0.8f, 1.8f, 1.2f, -0.15f);
            var l = new GameObject("RedAura").AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.type = LightType.Point; l.color = new Color(1f, 0.15f, 0.1f); l.range = 12f; l.intensity = 3.5f;
            root.AddComponent<FloatAndSpin>();
            root.transform.localScale = Vector3.zero;
            return root;
        }

        float LongHalf()
        {
            if (deck == null) return 7f;
            return Mathf.Max(deck.lossyScale.x, deck.lossyScale.z) * 0.5f;
        }

        /// 0 = centro del puente, 1 = extremo (a lo largo del eje largo).
        float MiddleFraction(Vector3 worldPos)
        {
            if (deck == null) return 1f;
            bool alongZ = deck.lossyScale.z >= deck.lossyScale.x;
            Vector3 local = worldPos - deck.position;
            float d = Mathf.Abs(alongZ ? Vector3.Dot(local, deck.forward) : Vector3.Dot(local, deck.right));
            return d / LongHalf();
        }

        /// Ningún enemigo debe quedarse flotando sobre el hueco del puente roto.
        void PushEnemiesOffBridge()
        {
            var b = blocker.bounds; b.Expand(1.5f);
            bool alongZ = deck == null || deck.lossyScale.z >= deck.lossyScale.x;
            Vector3 axis = alongZ ? (deck != null ? deck.forward : Vector3.forward) : deck.right;
            foreach (var hs in HealthSystem.Alive.ToArray())
            {
                if (hs == null || hs.team != Team.Enemy) continue;
                var t = hs.transform;
                if (!b.Contains(new Vector3(t.position.x, b.center.y, t.position.z))) continue;
                float s = Mathf.Sign(Vector3.Dot(t.position - transform.position, axis)); if (s == 0f) s = 1f;
                Vector3 target = transform.position + axis * s * (LongHalf() + 1.5f);
                target.y = t.position.y;
                var cc = t.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                t.position = target;
                if (cc != null) cc.enabled = true;
                HitFX.Spark(target + Vector3.up, new Color(0.6f, 0.2f, 1f));
            }
        }

        void PushPlayerOffBlocker()
        {
            if (player == null) return;
            var b = blocker.bounds; b.Expand(1f);
            if (!b.Contains(player.position + Vector3.up)) return;
            var cc = player.GetComponent<CharacterController>();
            Vector3 dir = player.position - transform.position; dir.y = 0f;
            bool alongZ = deck == null || deck.lossyScale.z >= deck.lossyScale.x;
            Vector3 axis = alongZ ? (deck != null ? deck.forward : Vector3.forward) : deck.right;
            float s = Mathf.Sign(Vector3.Dot(dir, axis)); if (s == 0f) s = -1f;
            Vector3 target = transform.position + axis * s * (LongHalf() - 1f);
            target.y = player.position.y;
            if (cc != null) cc.enabled = false;
            player.position = target;
            if (cc != null) cc.enabled = true;
        }

        static IEnumerator Scale(Transform t, Vector3 from, Vector3 to, float d)
        {
            for (float k = 0f; k < 1f; k += Time.deltaTime / d) { if (t == null) yield break; t.localScale = Vector3.LerpUnclamped(from, to, EaseOut(k)); yield return null; }
            if (t != null) t.localScale = to;
        }

        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        static float EaseInBack(float t) { t = Mathf.Clamp01(t); const float c = 1.7f; return t * t * ((c + 1f) * t - c); }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
            if (blocker != null) { Gizmos.matrix = blocker.transform.localToWorldMatrix; Gizmos.DrawCube(blocker.center, blocker.size); }
        }
    }

    /// Giro del anillo "0" del Gran Cero (acelera cuando carga el ataque).
    public class SpinRing : MonoBehaviour
    {
        public float speed = 40f;
        void Update() => transform.Rotate(0f, 0f, speed * Time.deltaTime, Space.Self);
    }

    /// Flotación y giro suave (antagonista, coleccionables).
    public class FloatAndSpin : MonoBehaviour
    {
        public float bob = 0.25f, bobSpeed = 2f, spin = 0f;
        Vector3 basePos; bool init;
        void Update()
        {
            if (!init) { basePos = transform.localPosition; init = true; }
            transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bob;
            if (spin != 0f) transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
        }
    }
}
