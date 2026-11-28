using UnityEngine;

namespace MageHero
{
    /// Materiales de brillo (shader MageHero/CineGlow) y números de luz flotantes para la cinemática.
    /// El material base vive en Resources/Cinematic/CineGlow.mat para que el shader se incluya en el build.
    public static class CineGlow
    {
        static Material baseMat;
        static Texture2D softDot;

        static Material Base
        {
            get
            {
                if (baseMat != null) return baseMat;
                baseMat = Resources.Load<Material>("Cinematic/CineGlow");
                if (baseMat == null)
                {
                    var sh = Shader.Find("MageHero/CineGlow");
                    baseMat = sh != null ? new Material(sh) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                }
                return baseMat;
            }
        }

        /// Material aditivo nuevo (uno por objeto, para poder animar su intensidad).
        /// <paramref name="hdr"/>: color con valores > 1 para que el bloom lo haga resplandecer.
        public static Material Make(Color hdr, float fresnel = 0f, float coreAlpha = 1f, float flicker = 0f, Texture tex = null)
        {
            var m = new Material(Base);
            m.SetColor("_Color", hdr);
            m.SetFloat("_Fresnel", fresnel);
            m.SetFloat("_CoreAlpha", coreAlpha);
            m.SetFloat("_Flicker", flicker);
            m.SetFloat("_Intensity", 1f);
            if (tex != null) m.SetTexture("_MainTex", tex);
            return m;
        }

        public static void SetIntensity(Material m, float k) { if (m != null) m.SetFloat("_Intensity", Mathf.Max(0f, k)); }

        /// Punto suave (degradado radial) para partículas de luz.
        public static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int n = 64;
                softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude / (n / 2f);
                    float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                    softDot.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                softDot.Apply();
                return softDot;
            }
        }

        /// Sistema de partículas de luz (motas que suben, chispas). Devuelve el sistema ya configurado y en reproducción.
        public static ParticleSystem Motes(string name, Transform parent, Vector3 localPos, Color hdr, float rate, float size, float speed, float life, float radius, float gravity = -0.05f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = UnityEngine.Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(UnityEngine.Color.white, 0f), new GradientColorKey(UnityEngine.Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Make(hdr, 0f, 1f, 0f, SoftDot);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// Ráfaga única de partículas (chispas al formar el 10, astillas mágicas…).
        public static void Burst(Vector3 pos, Color hdr, int count, float speed, float size, float life, float gravity = 0.2f)
        {
            var ps = Motes("Burst", null, pos, hdr, 0f, size, speed, life, 0.15f, gravity);
            var main = ps.main; main.loop = false;
            ps.Emit(count);
            Object.Destroy(ps.gameObject, life + 0.5f);
        }

        /// Texto de luz flotante en el mundo (números que dibuja el maestro, la operación del Gólem…).
        public static GlowNumber Number(string text, Transform parent, Vector3 localPos, Color hdr, float height = 0.6f, bool billboard = true)
        {
            var go = new GameObject("Glow_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = UIText.Font;
            tm.fontSize = 96;
            tm.characterSize = height / 9.6f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Make(hdr, 0f, 1f, 0.15f, UIText.Font.material.mainTexture);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (billboard) go.AddComponent<Billboard>();
            var gn = go.AddComponent<GlowNumber>();
            gn.Init(tm, mr.sharedMaterial);
            return gn;
        }
    }

    /// Número de luz: aparece, late y se desvanece.
    public class GlowNumber : MonoBehaviour
    {
        TextMesh tm;
        Material mat;
        float target = 1f, current, speed = 2.5f;
        public float Pulse = 0.12f;

        public void Init(TextMesh t, Material m)
        {
            tm = t; mat = m; current = 0f; CineGlow.SetIntensity(mat, 0f);
            tm.text = UIText.SafeSymbols(tm.text);
            Font.textureRebuilt += OnFontRebuilt;
            Refresh();
        }
        public string Text { get => tm.text; set { tm.text = UIText.SafeSymbols(value); Refresh(); } }

        /// La fuente dinámica rehace su textura al pedir caracteres nuevos; el material propio (aditivo) guardaba la
        /// textura vieja y los caracteres nuevos —p. ej. los signos— salían en blanco. Se pide el texto y se reasigna.
        void Refresh()
        {
            if (tm == null || tm.font == null) return;
            tm.font.RequestCharactersInTexture(tm.text, tm.fontSize, tm.fontStyle);
            if (mat != null) mat.SetTexture("_MainTex", tm.font.material.mainTexture);
        }

        void OnFontRebuilt(Font f) { if (tm != null && f == tm.font && mat != null) mat.SetTexture("_MainTex", f.material.mainTexture); }
        public void FadeTo(float k, float perSecond = 2.5f) { target = k; speed = perSecond; }
        public void Flash(float k = 3f) { current = k; }

        void Update()
        {
            current = Mathf.MoveTowards(current, target, speed * Time.deltaTime * Mathf.Max(1f, Mathf.Abs(current - target)));
            float p = 1f + Mathf.Sin(Time.time * 4f) * Pulse;
            CineGlow.SetIntensity(mat, current * p);
        }

        void OnDestroy() { Font.textureRebuilt -= OnFontRebuilt; if (mat != null) Destroy(mat); }
    }
}
