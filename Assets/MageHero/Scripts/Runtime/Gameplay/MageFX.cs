using UnityEngine;

namespace MageHero
{
    /// Movimiento secundario del mago: capa, punta del sombrero, cristal y runas numéricas orbitando el báculo.
    public class MageFX : MonoBehaviour
    {
        public PlayerController controller;
        public Transform cape, hatTip1, hatTip2, crystal, orbit;
        Transform[] runes;
        Vector3 crystalScale = Vector3.one;

        void Start()
        {
            if (crystal != null) crystalScale = crystal.localScale;
            if (orbit == null) return;
            string[] syms = { "+", "−", "×", "÷" };
            runes = new Transform[syms.Length];
            for (int i = 0; i < syms.Length; i++)
            {
                var c = UIText.WorldLabel(orbit, syms[i], Vector3.zero, new Vector2(100f, 100f), 90, new Color(0.6f, 1f, 1f, 1f), new Color(0, 0, 0, 0), 0.0032f);
                runes[i] = c.transform;
            }
        }

        void LateUpdate()
        {
            float t = Time.time;
            float n = controller != null ? controller.NormalizedSpeed : 0f;
            if (cape != null) cape.localRotation = Quaternion.Euler((0.06f + n * 0.3f + Mathf.Sin(t * 2f) * 0.03f) * Mathf.Rad2Deg, 0f, 0f);
            if (hatTip1 != null) hatTip1.localRotation = Quaternion.Euler((-n * 0.35f + Mathf.Sin(t * 3f) * 0.03f) * Mathf.Rad2Deg, 0f, 0f);
            if (hatTip2 != null) hatTip2.localRotation = Quaternion.Euler((0.3f - n * 0.3f + Mathf.Sin(t * 3f + 1f) * 0.06f) * Mathf.Rad2Deg, 0f, 0f);
            if (crystal != null) crystal.localScale = new Vector3(crystalScale.x, crystalScale.y * (1f + Mathf.Sin(t * 3f) * 0.08f), crystalScale.z);
            if (orbit != null)
            {
                orbit.localRotation = Quaternion.Euler(0f, t * 1.4f * Mathf.Rad2Deg, 0f);
                if (runes != null)
                    for (int i = 0; i < runes.Length; i++)
                    {
                        float a = i * Mathf.PI * 0.5f;
                        runes[i].localPosition = new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(t * 2f + i * 1.6f) * 0.08f, Mathf.Sin(a) * 0.42f);
                    }
            }
        }
    }
}
