using UnityEngine;
using UnityEngine.UI;

namespace MageHero
{
    /// Barra de vida flotante en World Space sobre la cabeza del enemigo. Siempre mira a la cámara.
    /// Se construye sola en Start; no necesita prefab.
    public class WorldHealthBar3D : MonoBehaviour
    {
        public HealthSystem health;
        [Tooltip("Altura sobre el pivote. 0 = calcular automáticamente (encima del cartel del ejercicio).")]
        public float height = 0f;
        public Vector2 sizeInPixels = new Vector2(140f, 16f);
        public float pixelScale = 0.01f;
        public bool hideWhenFull = true;
        public Color fullColor = new Color(0.36f, 0.85f, 0.42f), lowColor = new Color(0.95f, 0.27f, 0.27f);

        Canvas canvas;
        RectTransform fill;
        Image fillImage;
        float shown = 1f;
        Camera cam;

        void Start()
        {
            if (health == null) health = GetComponentInParent<HealthSystem>();
            if (health == null) { enabled = false; return; }
            Build();
            health.Changed += _ => { if (canvas != null) canvas.enabled = true; };
            shown = health.Normalized;
            Refresh(true);
        }

        void Build()
        {
            var old = transform.Find("HealthBar");          // por si el objeto se clonó con su barra
            if (old != null) Destroy(old.gameObject);
            if (height <= 0f) height = AutoHeight();

            var go = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;
            go.transform.localScale = Vector3.one * pixelScale;
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;
            ((RectTransform)go.transform).sizeDelta = sizeInPixels;

            var border = NewImage("Border", go.transform, new Color(0.06f, 0.1f, 0.18f, 0.95f));
            Stretch(border.rectTransform, -3f);
            var bg = NewImage("Background", go.transform, new Color(0.25f, 0.05f, 0.08f, 0.9f));
            Stretch(bg.rectTransform, 0f);
            fillImage = NewImage("Fill", go.transform, fullColor);
            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        float AutoHeight()
        {
            // Busca el cartel del ejercicio (Canvas world-space hijo) y se coloca justo encima.
            float top = 0f;
            foreach (Transform c in transform)
            {
                var rt = c as RectTransform;
                if (rt == null || c.name == "HealthBar" || c.GetComponent<Canvas>() == null) continue;
                top = Mathf.Max(top, c.localPosition.y + rt.sizeDelta.y * c.localScale.y * 0.5f);
            }
            if (top > 0f) return top + 0.25f;
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 2.2f;
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b.max.y - transform.position.y + 0.35f;
        }

        void LateUpdate()
        {
            if (canvas == null) return;
            if (cam == null) cam = Camera.main;
            if (cam != null) canvas.transform.rotation = cam.transform.rotation;
            Refresh(false);
        }

        void Refresh(bool instant)
        {
            float target = health.Normalized;
            shown = instant ? target : Mathf.MoveTowards(shown, target, Time.deltaTime * 2.5f);
            fill.anchorMax = new Vector2(shown, 1f);
            fillImage.color = Color.Lerp(lowColor, fullColor, shown);
            if (hideWhenFull) canvas.enabled = target < 0.999f;
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rt, float pad)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        }
    }
}
