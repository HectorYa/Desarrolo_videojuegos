using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace MageHero
{
    /// Objeto de ayuda escondido en una zona alterna del mapa ("Pergamino de Athan").
    /// Brilla con un haz de luz visible desde lejos; al tocarlo (o pulsar F si requireInteractKey)
    /// se recoge y muestra en pantalla las instrucciones para resolver la ecuación del puente.
    /// Después se puede volver a leer con la tecla H o desde el quiz ("Ver pista").
    [RequireComponent(typeof(SphereCollider))]
    public class ClueItemSystem : MonoBehaviour
    {
        public static ClueItemSystem Instance { get; private set; }

        [Header("Objeto")]
        public string itemName = "Pergamino de Athan";
        [TextArea(2, 4)]
        public string introText = "«Joven mago: el Gran Cero teme a quien conoce el orden de las operaciones. Sigue estos pasos y ningún número te detendrá.» — Maestro Athan";
        public bool requireInteractKey = false;
        public float pickupRadius = 1.6f;

        [Header("Puzzle asociado")]
        public BridgePuzzleManager puzzle;

        [Header("Visual")]
        public Material scrollMaterial;
        public Material glowMaterial;
        public float spinSpeed = 70f, bobHeight = 0.2f;

        public UnityEvent onCollected = new UnityEvent();

        public bool Collected { get; private set; }

        Transform visual;
        float baseY;
        bool playerInside;

        void Awake()
        {
            Instance = this;
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true; col.radius = pickupRadius; col.center = Vector3.up * 0.8f;
            gameObject.layer = 2;   // Ignore Raycast
            visual = transform.Find("Visual");
            if (visual == null) visual = BuildVisual(this).transform;
            baseY = visual.localPosition.y;
        }

        void Start() { if (puzzle == null) puzzle = BridgePuzzleManager.Instance; }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (Collected) return;
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            var p = visual.localPosition; p.y = baseY + Mathf.Sin(Time.time * 2f) * bobHeight; visual.localPosition = p;

            if (!requireInteractKey || !playerInside) return;
            var ui = UIManager.Instance;
            if (ui != null && !ui.IsModalOpen) ui.ShowPrompt($"Pulsa  F  para recoger: {itemName}", this);
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) Collect();
        }

        void OnTriggerEnter(Collider other)
        {
            if (Collected || !other.CompareTag("Player")) return;
            playerInside = true;
            if (!requireInteractKey) Collect();
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            playerInside = false;
            UIManager.Instance?.HidePrompt(this);
        }

        public void Collect()
        {
            if (Collected) return;
            Collected = true;
            var ui = UIManager.Instance;
            if (ui != null)
            {
                ui.HidePrompt(this);
                ui.SetClueAvailable(true, itemName);
                ui.ShowClue(itemName, GetInstructions());
                if (puzzle != null && puzzle.State == BridgePuzzleManager.BridgeState.Broken)
                    ui.SetObjective("Objetivo: vuelve al puente y repáralo (F).  Relee el pergamino con H.");
            }
            onCollected.Invoke();
            visual.gameObject.SetActive(false);
            GetComponent<Collider>().enabled = false;
        }

        /// Texto completo de ayuda: reglas generales + pasos para la ecuación actual del puente.
        public string GetInstructions()
        {
            string text = introText + "\n\n" + ArithmeticEquation.GeneralRules;
            var eq = puzzle != null ? puzzle.CurrentEquation : null;
            if (eq != null)
                text += $"\n\nTU ECUACIÓN:  {eq.text}\n{eq.guide}";
            return text;
        }

        /// Pergamino low-poly + haz de luz + luz puntual. Público para poder crearlo desde el editor.
        public static GameObject BuildVisual(ClueItemSystem item)
        {
            var root = new GameObject("Visual");
            root.transform.SetParent(item.transform, false);
            root.transform.localPosition = Vector3.up * 1.0f;

            var roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            roll.name = "Scroll";
            DestroyImmediate(roll.GetComponent<Collider>());
            roll.transform.SetParent(root.transform, false);
            roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            roll.transform.localScale = new Vector3(0.28f, 0.45f, 0.28f);
            if (item.scrollMaterial != null) roll.GetComponent<Renderer>().sharedMaterial = item.scrollMaterial;

            foreach (float x in new[] { -0.5f, 0.5f })
            {
                var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                knob.name = "Knob";
                DestroyImmediate(knob.GetComponent<Collider>());
                knob.transform.SetParent(root.transform, false);
                knob.transform.localPosition = new Vector3(x, 0f, 0f);
                knob.transform.localScale = Vector3.one * 0.18f;
                if (item.glowMaterial != null) knob.GetComponent<Renderer>().sharedMaterial = item.glowMaterial;
            }

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "LightBeam";
            DestroyImmediate(beam.GetComponent<Collider>());
            beam.transform.SetParent(item.transform, false);
            beam.transform.localPosition = Vector3.up * 5f;
            beam.transform.localScale = new Vector3(0.12f, 5f, 0.12f);
            beam.transform.SetParent(root.transform, true);
            var br = beam.GetComponent<Renderer>();
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (item.glowMaterial != null) br.sharedMaterial = item.glowMaterial;

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.4f); l.range = 6f; l.intensity = 2.5f;
            return root;
        }
    }
}
