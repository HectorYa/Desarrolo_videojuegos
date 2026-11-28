using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageHero
{
    /// Viste los puentes y muelles generados (cubos y cilindros) con una construcción de madera detallada,
    /// al cargar cada nivel y sin tocar las escenas:
    ///  · Cubierta: tablones sueltos con separación, largos y tonos distintos, alguno desportillado, clavos,
    ///    y dos largueros de tronco debajo (partidos por la mitad: al derrumbarse el puente cuelgan hacia el río).
    ///  · Barandas: postes inclinados a mano, pasamanos de tronco, travesaño y riostras en diagonal.
    ///  · Pilotes: troncos facetados con abrazaderas de cuerda; la parte cercana al agua, mojada y con musgo.
    /// Los cubos originales se ocultan pero conservan sus colliders. Los tablones ceden un poco bajo el mago.
    public static class BridgeDresser
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var decks = new List<Transform>();
            var pilings = new List<Transform>();
            var rails = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "BridgeDeck" || t.name == "PierDeck") decks.Add(t);
                    else if (t.name == "Piling") pilings.Add(t);
                    else if (t.name == "BridgeRail") rails.Add(t);
                }
            if (decks.Count == 0) return;
            foreach (var d in decks) DressDeck(d);
            foreach (var r in rails) DressRail(r);
            foreach (var p in pilings) DressPiling(p);
        }

        // ------------------------------------------------------------------ utilidades
        static readonly Dictionary<Transform, Material> woodByDeck = new Dictionary<Transform, Material>();

        static float WaterLevelNear(Vector3 p, float fallback)
        {
            int water = GameLayers.Exists(GameLayers.Water) ? GameLayers.Mask(GameLayers.Water) : 0;
            if (water != 0 && Physics.Raycast(p + Vector3.up * 10f, Vector3.down, out RaycastHit h, 30f, water, QueryTriggerInteraction.Collide))
                if (h.point.y < fallback + 1.2f) return h.point.y;
            return fallback;
        }

        static Material WoodFor(Vector3 near)
        {
            // el material de la cubierta más cercana (un material por puente: misma altura del agua)
            Transform best = null; float bd = float.MaxValue;
            foreach (var kv in woodByDeck)
            {
                if (kv.Key == null) continue;
                float d = (kv.Key.position - near).sqrMagnitude;
                if (d < bd) { bd = d; best = kv.Key; }
            }
            if (best != null) return woodByDeck[best];
            return DetailMaterials.Wood(near.y - 1.5f);
        }

        static Color ColorOf(Renderer r, Color fallback)
        {
            if (r == null || r.sharedMaterial == null) return fallback;
            var m = r.sharedMaterial;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            return m.HasProperty("_Color") ? m.color : fallback;
        }

        static Color Vary(Color c, System.Random rng, float amount)
        {
            float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * amount;
            float warm = ((float)rng.NextDouble() - 0.5f) * amount * 0.4f;
            return new Color(c.r * k + warm, c.g * k, c.b * k - warm, 1f);
        }

        static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// Hijo con la escala del padre compensada: la geometría se escribe en metros, en el espacio
        /// (rotado, no escalado) del padre. Si el padre se anima en el derrumbe, el detalle le sigue.
        static Transform Unscaled(Transform parent, string name)
        {
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            Vector3 s = parent.lossyScale;
            go.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));
            return go;
        }

        // ------------------------------------------------------------------ cubierta
        static void DressDeck(Transform deck)
        {
            var renderer = deck.GetComponent<Renderer>();
            Vector3 sc = deck.lossyScale;
            bool alongZ = sc.z >= sc.x;
            float len = alongZ ? sc.z : sc.x, wid = alongZ ? sc.x : sc.z;
            Vector3 axis = alongZ ? deck.forward : deck.right; axis.y = 0f; axis.Normalize();
            float top = deck.position.y + sc.y * 0.5f;
            float waterY = WaterLevelNear(deck.position, deck.position.y - 1.6f);
            var wood = DetailMaterials.Wood(waterY);
            woodByDeck[deck] = wood;
            Color baseCol = ColorOf(renderer, new Color(0.62f, 0.45f, 0.3f));
            var rng = new System.Random(Mathf.RoundToInt(deck.position.x * 31f + deck.position.z * 17f));

            var container = new GameObject(deck.name + "_Tablones").transform;
            container.SetPositionAndRotation(deck.position, Quaternion.LookRotation(axis, Vector3.up));
            container.SetParent(deck.parent, true);
            var dress = deck.gameObject.AddComponent<BridgeDress>();
            dress.container = container; dress.length = len; dress.width = wid;

            const float thick = 0.1f;
            float yTop = top - deck.position.y;
            int n = Mathf.Max(3, Mathf.RoundToInt(len / 0.5f));
            float step = len / n;
            for (int i = 0; i < n; i++)
            {
                float a = -len * 0.5f + step * (i + 0.5f);
                float pw = step - R(rng, 0.04f, 0.08f);
                float l = wid * R(rng, 0.94f, 1f);
                Color col = Vary(baseCol, rng, 0.12f);
                if (rng.NextDouble() < 0.12) col *= 0.78f;                              // tablón viejo, más oscuro
                col.a = 1f;
                var lp = new LowPoly().Paint(col).Grain(Vector3.right);
                if (rng.NextDouble() < 0.15)
                {
                    // desportillado: le falta una esquina
                    float cut = R(rng, 0.25f, 0.6f), sgn = rng.NextDouble() < 0.5 ? -1f : 1f;
                    lp.At(new Vector3(-sgn * cut * 0.5f, 0, 0)).Box(new Vector3(l - cut, thick, pw));
                    lp.At(new Vector3(sgn * (l * 0.5f - cut * 0.5f), 0, -pw * 0.2f)).Box(new Vector3(cut, thick, pw * 0.6f));
                }
                else lp.Box(new Vector3(l, thick, pw));
                lp.Paint(new Color(0.2f, 0.18f, 0.17f)).Grain(Vector3.up);               // clavos sobre los largueros
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -0.22f, 0.22f })
                        lp.At(new Vector3(sx * wid * 0.32f, thick * 0.5f + 0.004f, sz * pw)).Box(new Vector3(0.035f, 0.012f, 0.035f));
                var go = lp.Spawn("Tablon", container, wood, default, true);
                go.transform.localPosition = new Vector3(R(rng, -0.06f, 0.06f), yTop - thick * 0.5f + R(rng, -0.012f, 0.012f), a);
                go.transform.localRotation = Quaternion.Euler(R(rng, -1f, 1f), R(rng, -1.6f, 1.6f), R(rng, -0.8f, 0.8f));
                dress.planks.Add(go.transform);
            }

            // Largueros (dos troncos a lo largo, partidos por la mitad) y vigas transversales en los extremos
            Color beam = Vary(baseCol * 0.8f, rng, 0.05f);
            float yBeam = yTop - thick - 0.13f;
            foreach (float s in new[] { -1f, 1f })
                foreach (float half in new[] { -1f, 1f })
                {
                    var lp = new LowPoly().Paint(beam).Grain(Vector3.up);
                    lp.At(Vector3.zero, Quaternion.Euler(half < 0 ? 90f : -90f, 0, 0), Vector3.one).Prism(6, 0.14f, 0.13f, len * 0.5f + 0.04f, true, 0.2f);
                    var go = lp.Spawn("Larguero", container, wood, default, true);
                    go.transform.localPosition = new Vector3(s * wid * 0.32f, yBeam, half * len * 0.5f);
                    dress.stringers.Add(go.transform);
                }
            foreach (float e in new[] { -1f, 1f })
            {
                var lp = new LowPoly().Paint(beam * 0.9f).Grain(Vector3.up);
                lp.At(new Vector3(-wid * 0.5f, 0, 0), Quaternion.Euler(0, 0, -90f), Vector3.one).Prism(6, 0.12f, 0.12f, wid, true, 0.15f);
                var go = lp.Spawn("Viga", container, wood, default, true);
                go.transform.localPosition = new Vector3(0, yBeam - 0.05f, e * (len * 0.5f - 0.35f));
            }

            if (renderer != null) renderer.enabled = false;
        }

        // ------------------------------------------------------------------ barandas
        static void DressRail(Transform rail)
        {
            var renderer = rail.GetComponent<Renderer>();
            Vector3 sc = rail.lossyScale;
            bool alongZ = sc.z >= sc.x;
            float len = alongZ ? sc.z : sc.x;
            Quaternion inv = Quaternion.Inverse(rail.rotation);
            Vector3 ax = inv * (alongZ ? rail.forward : rail.right), up = inv * Vector3.up;
            Color col = ColorOf(renderer, new Color(0.5f, 0.35f, 0.22f));
            var rng = new System.Random(Mathf.RoundToInt(rail.position.x * 13f + rail.position.z * 7f));
            var wood = WoodFor(rail.position);

            float bottom = -sc.y * 0.5f;                                                // a ras de la cubierta
            var lp = new LowPoly();
            Quaternion along = Quaternion.LookRotation(ax, up);
            int posts = Mathf.Max(2, Mathf.RoundToInt(len / 1.75f) + 1);
            float a0 = -len * 0.5f + 0.15f, a1 = len * 0.5f - 0.15f;
            float prevA = 0f;
            for (int i = 0; i < posts; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)(posts - 1));
                Vector3 basePt = ax * a + up * bottom;
                lp.Paint(Vary(col, rng, 0.1f)).Grain(Vector3.up);
                lp.At(basePt + up * 0.47f, along * Quaternion.Euler(R(rng, -2.5f, 2.5f), R(rng, -8f, 8f), R(rng, -2.5f, 2.5f)), Vector3.one).Box(new Vector3(0.13f, 0.95f, 0.13f));
                lp.At(basePt + up * 0.945f, along, Vector3.one).Prism(4, 0.09f, 0.0f, 0.07f);           // remate
                if (i > 0)
                {
                    // riostra en diagonal, alternando el sentido en cada tramo
                    bool rising = i % 2 == 0;
                    Vector3 p0 = ax * prevA + up * (bottom + (rising ? 0.08f : 0.62f));
                    Vector3 p1 = ax * a + up * (bottom + (rising ? 0.62f : 0.08f));
                    Vector3 d = p1 - p0;
                    lp.Paint(Vary(col * 0.92f, rng, 0.08f)).Grain(Vector3.forward);
                    Vector3 planeNormal = Vector3.Cross(ax, up);                       // la riostra queda en el plano de la baranda
                    lp.At((p0 + p1) * 0.5f, Quaternion.LookRotation(d, Vector3.Cross(planeNormal, d)), Vector3.one)
                      .Box(new Vector3(0.05f, 0.07f, d.magnitude));
                }
                prevA = a;
            }
            // pasamanos de tronco y travesaño intermedio
            lp.Paint(Vary(col * 1.05f, rng, 0.05f)).Grain(Vector3.up);
            lp.At(ax * (-len * 0.5f) + up * (bottom + 0.9f), Quaternion.FromToRotation(Vector3.up, ax), Vector3.one).Prism(6, 0.06f, 0.055f, len, true, 0.3f);
            lp.Paint(Vary(col * 0.95f, rng, 0.05f)).Grain(Vector3.forward);
            lp.At(ax * 0f + up * (bottom + 0.48f), along, Vector3.one).Box(new Vector3(0.05f, 0.09f, len - 0.1f));

            var holder = Unscaled(rail, "Baranda_Detalle");
            lp.Spawn("Baranda", holder, wood, default, true);
            rail.gameObject.AddComponent<BridgeDressPart>();
            if (renderer != null) renderer.enabled = false;
        }

        // ------------------------------------------------------------------ pilotes
        static void DressPiling(Transform piling)
        {
            var renderer = piling.GetComponent<Renderer>();
            Vector3 sc = piling.lossyScale;
            float h = sc.y * 2f, r = Mathf.Max(sc.x, sc.z) * 0.5f;                     // cilindro de Unity: altura 2
            Color col = ColorOf(renderer, new Color(0.45f, 0.32f, 0.2f)) * 0.9f; col.a = 1f;
            var rng = new System.Random(Mathf.RoundToInt(piling.position.x * 11f + piling.position.z * 5f));
            var wood = WoodFor(piling.position);
            Quaternion inv = Quaternion.Inverse(piling.rotation);
            Vector3 up = inv * Vector3.up;
            Quaternion toUp = Quaternion.FromToRotation(Vector3.up, up);

            var lp = new LowPoly().Paint(Vary(col, rng, 0.08f)).Grain(Vector3.up);
            lp.At(-up * (h * 0.5f), toUp * Quaternion.Euler(0, R(rng, 0, 360f), 0), Vector3.one).Prism(7, r * 1.25f, r * 1.1f, h, true, R(rng, -0.25f, 0.25f));
            // corte superior más claro (anillos) y abrazaderas de cuerda
            lp.Paint(Vary(col * 1.35f, rng, 0.05f));
            lp.At(up * (h * 0.5f + 0.005f), toUp, Vector3.one).Prism(7, r * 1.08f, r * 1.0f, 0.02f);
            lp.Paint(new Color(0.55f, 0.47f, 0.33f));
            foreach (float y in new[] { h * 0.5f - 0.25f, h * 0.5f - 0.42f })
                lp.At(up * y, toUp, Vector3.one).Prism(8, r * 1.32f, r * 1.32f, 0.06f);

            var holder = Unscaled(piling, "Pilote_Detalle");
            lp.Spawn("Pilote", holder, wood, default, true);
            piling.gameObject.AddComponent<BridgeDressPart>();
            if (renderer != null) renderer.enabled = false;
        }
    }

    /// Marca las piezas (barandas, pilotes) cuya forma visible es el detalle hijo y no su propio cubo.
    public class BridgeDressPart : MonoBehaviour { }

    /// Datos del puente vestido: tablones y largueros (los usa BridgePuzzleManager para el derrumbe) y la
    /// flexión de los tablones bajo los pies del mago.
    public class BridgeDress : MonoBehaviour
    {
        public Transform container;
        public float length, width;
        public readonly List<Transform> planks = new List<Transform>();
        public readonly List<Transform> stringers = new List<Transform>();
        /// Mientras el puente se derrumba o se repara, los tablones los mueve el BridgePuzzleManager.
        public bool animating;

        Vector3[] basePos;
        float[] dip;
        Transform player;

        void Start()
        {
            basePos = new Vector3[planks.Count];
            dip = new float[planks.Count];
            for (int i = 0; i < planks.Count; i++) basePos[i] = planks[i].localPosition;
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        void LateUpdate()
        {
            if (animating || player == null || container == null || basePos == null) return;
            Vector3 local = container.InverseTransformPoint(player.position);
            bool on = Mathf.Abs(local.x) < width * 0.5f + 0.2f && Mathf.Abs(local.z) < length * 0.5f && local.y > -0.6f && local.y < 1.5f;
            float dt = Time.deltaTime;
            for (int i = 0; i < planks.Count; i++)
            {
                var t = planks[i];
                if (t == null) continue;
                float d = basePos[i].z - local.z;
                float want = on ? 0.035f * Mathf.Exp(-d * d / 0.35f) : 0f;           // cede el tablón pisado y un poco los vecinos
                dip[i] = Mathf.Lerp(dip[i], want, 1f - Mathf.Exp(-(want > dip[i] ? 18f : 6f) * dt));
                t.localPosition = basePos[i] + Vector3.down * dip[i];
            }
        }

        /// Vuelve a fijar la posición de reposo (tras la reparación los tablones regresan a su sitio).
        public void Rest()
        {
            if (basePos == null) return;
            for (int i = 0; i < planks.Count; i++) if (planks[i] != null) { planks[i].localPosition = basePos[i]; dip[i] = 0f; }
        }
    }
}
