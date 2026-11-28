using UnityEngine;

namespace MageHero
{
    /// Ondas circulares sobre el agua (shader MageHero/ToonWater). Cualquier script puede llamar
    /// WaterRipples.Add(posición, tamaño) cuando algo cae al agua: el anillo crece y se desvanece solo.
    /// Se guardan en un búfer circular de 12 ondas que se envía al shader como variables globales.
    public static class WaterRipples
    {
        const int Max = 12;
        static readonly Vector4[] data = new Vector4[Max];
        static readonly Vector4[] prms = new Vector4[Max];
        static int next;
        static Runner runner;

        static readonly int RipplesId = Shader.PropertyToID("_MH_Ripples");
        static readonly int ParamsId = Shader.PropertyToID("_MH_RippleParams");
        static readonly int NowId = Shader.PropertyToID("_MH_Now");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            next = 0; runner = null;
            System.Array.Clear(data, 0, Max); System.Array.Clear(prms, 0, Max);
        }

        /// size ≈ 0.4 (astilla) … 1.5 (tablón grande). strength = cuánta espuma deja el anillo.
        public static void Add(Vector3 position, float size = 1f, float strength = 0.85f)
        {
            EnsureRunner();
            data[next] = new Vector4(position.x, position.y, position.z, Time.time);
            prms[next] = new Vector4(strength, Mathf.Max(0.1f, size), 0f, 0f);
            next = (next + 1) % Max;
            Push();
        }

        static void Push()
        {
            Shader.SetGlobalVectorArray(RipplesId, data);
            Shader.SetGlobalVectorArray(ParamsId, prms);
            Shader.SetGlobalFloat(NowId, Time.time);
        }

        static void EnsureRunner()
        {
            if (runner != null || !Application.isPlaying) return;
            var go = new GameObject("WaterRipples") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

        /// Avanza el reloj de las ondas cada frame y vacía las que ya terminaron.
        class Runner : MonoBehaviour
        {
            void Update()
            {
                bool changed = false;
                for (int i = 0; i < Max; i++)
                    if (prms[i].x > 0f && Time.time - data[i].w > 1.6f + prms[i].y * 0.9f + 0.1f) { prms[i].x = 0f; changed = true; }
                if (changed) Shader.SetGlobalVectorArray(ParamsId, prms);
                Shader.SetGlobalFloat(NowId, Time.time);
            }

            void OnDestroy()
            {
                System.Array.Clear(prms, 0, Max);
                Shader.SetGlobalVectorArray(ParamsId, prms);
            }
        }
    }
}
