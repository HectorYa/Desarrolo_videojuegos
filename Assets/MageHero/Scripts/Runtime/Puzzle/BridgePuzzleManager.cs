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

        class Piece { public Transform t; public Vector3 pos; public Quaternion rot; public Vector3 brokenPos; public Quaternion brokenRot; public Renderer r; public Material mat; }

        // ------------------------------------------------------------------ Ciclo de vida
        void Awake()
        {
            Instance = this;
            if (collapseZone == null || repairZone == null || blocker == null || gapWater == null) BuildHelpers();
            blocker.enabled = false;
            gapWater.SetActive(false);
            CurrentEquation = ArithmeticEquation.Random();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) { player = p.transform; playerHealth = p.GetComponentInParent<HealthSystem>(); }
            collapseZone.Entered += _ => { if (State == BridgeState.Intact) StartCoroutine(CollapseCinematic()); };
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
        IEnumerator CollapseCinematic()
        {
            State = BridgeState.Collapsing;
            var ui = UIManager.Instance;
            var follow = Camera.main != null ? Camera.main.GetComponent<IsoCameraFollow>() : null;
            Transform oldTarget = follow != null ? follow.target : null;
            var focus = new GameObject("BridgeCameraFocus").transform;
            focus.position = transform.position;

            ui?.BeginCinematic();
            if (follow != null) follow.target = focus;
            yield return new WaitForSeconds(1.2f);

            // El Gran Cero aparece al otro lado del puente
            Vector3 away = transform.position - (player != null ? player.position : transform.position - transform.forward);
            away.y = 0f; away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            Vector3 villainPos = transform.position + away * (LongHalf() + 3f) + Vector3.up * 3.2f;
            var villain = BuildGranCero(villainPos);
            yield return Scale(villain.transform, Vector3.zero, Vector3.one, 0.6f);
            ui?.SetSubtitle("EL GRAN CERO:  ¡Ja, ja! Ningún mago cruzará mi río...  ¡Este puente ahora vale CERO!");
            yield return new WaitForSeconds(2.8f);

            // Rayo hacia el puente
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(bolt.GetComponent<Collider>());
            bolt.transform.localScale = Vector3.one * 0.6f;
            if (villainRedMaterial != null) bolt.GetComponent<Renderer>().sharedMaterial = villainRedMaterial;
            Vector3 from = villainPos, to = transform.position + Vector3.up * 0.3f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f) { bolt.transform.position = Vector3.Lerp(from, to, t); yield return null; }
            Destroy(bolt);

            CameraShake3D.Shake(0.45f, 1.2f);
            BreakBridge();
            yield return AnimatePieces(true, 1.3f);
            ui?.SetSubtitle("EL GRAN CERO:  Si quieres pasar, resuelve mi ecuación...  ¡si es que puedes!");
            yield return new WaitForSeconds(2.6f);
            yield return Scale(villain.transform, Vector3.one, Vector3.zero, 0.5f);
            Destroy(villain);

            if (follow != null) follow.target = oldTarget;
            Destroy(focus.gameObject);
            ui?.EndCinematic();
            EnemyAI3D.CalmAround(transform.position, 25f, 4f);

            State = BridgeState.Broken;
            onCollapsed.Invoke();
            string where = string.IsNullOrEmpty(clueZoneName) ? "en otra zona del mapa" : $"cerca de «{clueZoneName}»";
            ui?.ShowMessage("¡El puente se derrumbó! Acércate y pulsa F para repararlo.", 4f);
            ui?.SetObjective($"Objetivo: repara el puente (F).  ¿Atascado? Dicen que el Maestro Athan dejó un pergamino {where}.");
        }

        void BreakBridge()
        {
            pieces.Clear();
            // La cubierta se parte en 4 tramos: los extremos se agrietan, los centrales caen al río.
            deckRenderer = deck != null ? deck.GetComponent<Renderer>() : null;
            if (deck != null)
            {
                bool alongZ = deck.lossyScale.z >= deck.lossyScale.x;
                float len = alongZ ? deck.lossyScale.z : deck.lossyScale.x, wid = alongZ ? deck.lossyScale.x : deck.lossyScale.z;
                Vector3 axis = alongZ ? deck.forward : deck.right;
                for (int i = 0; i < 4; i++)
                {
                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(seg.GetComponent<Collider>());
                    seg.name = "DeckSegment_" + i;
                    seg.transform.SetParent(transform, true);
                    seg.transform.SetPositionAndRotation(deck.position + axis * ((i - 1.5f) * len / 4f), deck.rotation);
                    seg.transform.localScale = alongZ ? new Vector3(wid, deck.lossyScale.y, len / 4f - 0.08f) : new Vector3(len / 4f - 0.08f, deck.lossyScale.y, wid);
                    if (deckRenderer != null) seg.GetComponent<Renderer>().sharedMaterial = deckRenderer.sharedMaterial;
                    deckSegments.Add(seg);
                    bool middle = i == 1 || i == 2;
                    float side = i < 2 ? -1f : 1f;
                    var tilt = Quaternion.AngleAxis(side * (middle ? 32f : 8f), alongZ ? deck.right : -deck.forward) * Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f);
                    AddPiece(seg.transform, seg.transform.position + Vector3.down * (middle ? 2.4f : 0.15f), tilt * seg.transform.rotation);
                }
                if (deckRenderer != null) deckRenderer.enabled = false;
            }
            if (rails != null)
                foreach (var r in rails)
                {
                    if (r == null) continue;
                    Vector3 outward = r.position - transform.position; outward.y = 0f; outward.Normalize();
                    var axis = Vector3.Cross(Vector3.up, outward);
                    AddPiece(r, r.position + Vector3.down * 1.1f + outward * 0.6f, Quaternion.AngleAxis(-28f, axis) * r.rotation);
                }
            if (pilings != null)
                foreach (var p in pilings)
                {
                    if (p == null) continue;
                    bool central = MiddleFraction(p.position) < 0.3f;
                    AddPiece(p, p.position + Vector3.down * (central ? 1.6f : 0.3f), Quaternion.Euler(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f)) * p.rotation);
                }
            blocker.enabled = true;
            gapWater.SetActive(true);
            PushPlayerOffBlocker();
        }

        void AddPiece(Transform t, Vector3 brokenPos, Quaternion brokenRot)
        {
            var r = t.GetComponent<Renderer>();
            pieces.Add(new Piece { t = t, pos = t.position, rot = t.rotation, brokenPos = brokenPos, brokenRot = brokenRot, r = r, mat = r != null ? r.sharedMaterial : null });
        }

        // ------------------------------------------------------------------ Reparación
        IEnumerator RepairSequence()
        {
            State = BridgeState.Repairing;
            var ui = UIManager.Instance;
            yield return new WaitForSeconds(1.1f);
            ui?.CloseQuiz(false);
            ui?.BeginCinematic();
            ui?.SetSubtitle("¡La ecuación es correcta! La magia de los números reconstruye el puente...");

            foreach (var p in pieces) if (p.r != null && repairGlowMaterial != null) p.r.sharedMaterial = repairGlowMaterial;
            var lightGo = new GameObject("RepairLight");
            lightGo.transform.position = transform.position + Vector3.up * 2f;
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.range = 14f; light.intensity = 4f; light.color = new Color(0.5f, 1f, 1f);
            yield return AnimatePieces(false, 1.6f);
            foreach (var p in pieces) if (p.r != null) p.r.sharedMaterial = p.mat;
            Destroy(lightGo);

            foreach (var s in deckSegments) Destroy(s);
            deckSegments.Clear();
            if (deckRenderer != null) deckRenderer.enabled = true;
            blocker.enabled = false;
            gapWater.SetActive(false);
            yield return new WaitForSeconds(0.6f);

            ui?.EndCinematic();
            State = BridgeState.Repaired;
            ui?.SetObjective("");
            ui?.ShowMessage("¡Puente reparado! El camino está libre.", 3.5f);
            onRepaired.Invoke();
        }

        IEnumerator AnimatePieces(bool breaking, float duration)
        {
            for (float t = 0f; t <= 1f; t += Time.deltaTime / duration)
            {
                float k = breaking ? EaseInBack(t) : EaseOut(t);
                foreach (var p in pieces)
                {
                    if (p.t == null) continue;
                    p.t.position = breaking ? Vector3.LerpUnclamped(p.pos, p.brokenPos, k) : Vector3.LerpUnclamped(p.brokenPos, p.pos, k);
                    p.t.rotation = breaking ? Quaternion.SlerpUnclamped(p.rot, p.brokenRot, k) : Quaternion.SlerpUnclamped(p.brokenRot, p.rot, k);
                }
                yield return null;
            }
            foreach (var p in pieces)
            {
                if (p.t == null) continue;
                p.t.SetPositionAndRotation(breaking ? p.brokenPos : p.pos, breaking ? p.brokenRot : p.rot);
            }
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
                gapWater = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gapWater.name = "GapWater";
                DestroyImmediate(gapWater.GetComponent<Collider>());
                gapWater.transform.SetParent(transform, false);
                gapWater.transform.localPosition = Vector3.up * (topY + 0.02f);
                gapWater.transform.localScale = Box(len * 0.55f, 0.06f, wid - 0.3f);
                if (gapMaterial != null) gapWater.GetComponent<Renderer>().sharedMaterial = gapMaterial;
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

        GameObject BuildGranCero(Vector3 pos)
        {
            var root = new GameObject("ElGranCero");
            root.transform.position = pos;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1.1f, 1.3f, 1.1f);
            if (villainDarkMaterial != null) body.GetComponent<Renderer>().sharedMaterial = villainDarkMaterial;
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(eye.GetComponent<Collider>());
                eye.transform.SetParent(body.transform, false);
                eye.transform.localPosition = new Vector3(x, 0.45f, 0.42f); eye.transform.localScale = Vector3.one * 0.18f;
                if (villainRedMaterial != null) eye.GetComponent<Renderer>().sharedMaterial = villainRedMaterial;
            }
            // Aura roja con forma de "0" (anillo ovalado que siempre mira a la cámara)
            var ring = new GameObject("AuraCero").transform;
            ring.SetParent(root.transform, false);
            ring.gameObject.AddComponent<Billboard>();
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(ring, false);
                s.transform.localPosition = new Vector3(Mathf.Cos(a) * 1.5f, Mathf.Sin(a) * 2.1f, 0.3f);
                s.transform.localScale = Vector3.one * 0.32f;
                if (villainRedMaterial != null) s.GetComponent<Renderer>().sharedMaterial = villainRedMaterial;
            }
            string[] vars = { "x", "y", "+", "÷" };
            for (int i = 0; i < vars.Length; i++)
            {
                float a = i / (float)vars.Length * Mathf.PI * 2f;
                UIText.WorldLabel(root.transform, vars[i], new Vector3(Mathf.Cos(a) * 2.4f, 0.4f + Mathf.Sin(a) * 1.2f, Mathf.Sin(a) * 0.5f),
                                  new Vector2(80f, 80f), 70, new Color(1f, 0.25f, 0.2f), new Color(0, 0, 0, 0), 0.012f);
            }
            var l = new GameObject("RedAura").AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.type = LightType.Point; l.color = new Color(1f, 0.15f, 0.1f); l.range = 10f; l.intensity = 3f;
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
