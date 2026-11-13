#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MageHero.Editor
{
    /// Escena MainMenu: elegir uno de los tres niveles con vista previa del mapa.
    public static class MenuBuilder
    {
        public const string ScenePath = AssetLib.Root + "/Scenes/MainMenu.unity";

        static RectTransform R(string n, Transform p) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); return go.GetComponent<RectTransform>(); }
        static Image Img(string n, Transform p, Color c) { var rt = R(n, p); var im = rt.gameObject.AddComponent<Image>(); im.color = c; return im; }
        static void Anchor(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
        { rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size; }

        public static void Build(LevelDef[] defs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.055f, .102f, .169f, 1f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var root = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = root.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = .5f;
            var menu = root.AddComponent<MenuController>();

            var bg = Img("Background", root.transform, new Color(.055f, .102f, .169f, 1f));
            bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one; bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            bg.raycastTarget = false;

            var title = UIText.Make(root.transform, "Title", "Mago de los Números", 54, LevelBuilder.GoldC); title.fontStyle = FontStyle.Bold;
            Anchor(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(1000, 70));
            var sub = UIText.Make(root.transform, "Subtitle", "Elige tu mapa · cada nivel es un mundo distinto (teclas 1, 2, 3)", 20, new Color(1, 1, 1, .8f));
            Anchor(sub.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -90), new Vector2(1000, 30));

            var scenes = new string[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                scenes[i] = "Level_" + d.id;
                Color accent; ColorUtility.TryParseHtmlString(d.accent, out accent);
                float x = (i - (defs.Length - 1) / 2f) * 380f;

                var card = Img("Card_" + d.id, root.transform, LevelBuilder.Navy);
                Anchor(card.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, -42), new Vector2(350, 500));
                var bar = Img("Accent", card.transform, accent);
                Anchor(bar.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 7));

                var prev = R("Preview", card.transform); var raw = prev.gameObject.AddComponent<RawImage>();
                raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(LevelBuilder.PreviewPath(d.id));
                Anchor(prev, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(300, 300));
                var frame = Img("Frame", prev, accent); frame.raycastTarget = false;
                frame.rectTransform.anchorMin = Vector2.zero; frame.rectTransform.anchorMax = Vector2.one; frame.rectTransform.offsetMin = new Vector2(-4, -4); frame.rectTransform.offsetMax = new Vector2(4, 4);
                frame.transform.SetAsFirstSibling();

                var lv = UIText.Make(card.transform, "Lv", d.lv.ToUpperInvariant(), 13, accent);
                Anchor(lv.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -322), new Vector2(320, 18));
                var nm = UIText.Make(card.transform, "Name", d.name, 27, Color.white); nm.fontStyle = FontStyle.Bold;
                Anchor(nm.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -340), new Vector2(330, 36));
                var ds = UIText.Make(card.transform, "Desc", d.desc, 14, new Color(1, 1, 1, .85f), TextAnchor.UpperCenter);
                ds.horizontalOverflow = HorizontalWrapMode.Wrap; ds.verticalOverflow = VerticalWrapMode.Truncate;
                Anchor(ds.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -378), new Vector2(310, 66));
                var tp = UIText.Make(card.transform, "Topic", "Tema sugerido: " + d.topic, 13, accent);
                Anchor(tp.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 72), new Vector2(320, 20));

                var play = Img("PlayButton", card.transform, accent);
                var btn = play.gameObject.AddComponent<Button>(); btn.targetGraphic = play;
                Anchor(play.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(240, 46));
                var pl = UIText.Make(play.transform, "Label", "¡Jugar!", 24, LevelBuilder.Navy); pl.fontStyle = FontStyle.Bold;
                pl.rectTransform.anchorMin = Vector2.zero; pl.rectTransform.anchorMax = Vector2.one; pl.rectTransform.offsetMin = pl.rectTransform.offsetMax = Vector2.zero;
                UnityEventTools.AddStringPersistentListener(btn.onClick, menu.LoadLevel, scenes[i]);
            }
            menu.hotkeyScenes = scenes;

            var quit = Img("QuitButton", root.transform, LevelBuilder.Navy);
            var qb = quit.gameObject.AddComponent<Button>(); qb.targetGraphic = quit;
            Anchor(quit.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), new Vector2(120, 40));
            var ql = UIText.Make(quit.transform, "Label", "Salir", 18, Color.white);
            ql.rectTransform.anchorMin = Vector2.zero; ql.rectTransform.anchorMax = Vector2.one; ql.rectTransform.offsetMin = ql.rectTransform.offsetMax = Vector2.zero;
            UnityEventTools.AddPersistentListener(qb.onClick, menu.Quit);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
#endif
