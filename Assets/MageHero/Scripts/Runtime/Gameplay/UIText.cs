using UnityEngine;
using UnityEngine.UI;

namespace MageHero
{
    /// Utilidades para crear texto uGUI sin depender de TextMeshPro.
    public static class UIText
    {
        static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Text Make(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// Etiqueta en el mundo (burbuja de ejercicio, runas del báculo). Mira a la cámara si lleva Billboard.
        public static Canvas WorldLabel(Transform parent, string text, Vector3 localPos, Vector2 size, int fontSize, Color fg, Color bg, float scale)
        {
            var go = new GameObject("Label_" + text, typeof(RectTransform), typeof(Canvas), typeof(Billboard));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = size;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            if (bg.a > 0.001f)
            {
                var img = new GameObject("Bg", typeof(RectTransform), typeof(Image));
                img.transform.SetParent(go.transform, false);
                var irt = img.GetComponent<RectTransform>(); irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = irt.offsetMax = Vector2.zero;
                var im = img.GetComponent<Image>(); im.color = bg; im.raycastTarget = false;
            }
            var t = Make(go.transform, "Text", text, fontSize, fg);
            var trt = t.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            return go.GetComponent<Canvas>();
        }
    }

    /// Hace que el objeto mire siempre a la cámara principal.
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (c != null) transform.rotation = c.transform.rotation;
        }
    }
}
