using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageHero
{
    /// Configuración del jugador (se guarda en PlayerPrefs y se aplica al instante). El jugador solo ajusta:
    ///  · Sonido: volumen general.  · Cámara: sensibilidad del ratón.  · Pantalla: pantalla completa.
    /// Música, efectos, voz, eje invertido y calidad ya no se muestran: quedan fijos en su valor por defecto
    /// (así un valor antiguo guardado no se queda escondido y sin forma de cambiarlo).
    public static class GameSettings
    {
        const string P = "MageHero.Config.";
        public const float DefaultMaster = 0.9f, DefaultMusic = 0.7f, DefaultSfx = 0.85f, DefaultVoice = 1f, DefaultSensitivity = 0.5f;

        public static float Master { get => Get("Master", DefaultMaster); set { Set("Master", value); ApplyAudio(); } }
        public static float Music => DefaultMusic;
        public static float Sfx => DefaultSfx;
        public static float Voice => DefaultVoice;
        /// 0..1 → multiplicador 0.4x .. 2x (0.5 = normal).
        public static float Sensitivity { get => Get("Sens", DefaultSensitivity); set { Set("Sens", value); ApplyCamera(); } }
        public static bool InvertY => false;
        public static bool Fullscreen { get => PlayerPrefs.GetInt(P + "Full", Screen.fullScreen ? 1 : 0) == 1; set { PlayerPrefs.SetInt(P + "Full", value ? 1 : 0); PlayerPrefs.Save(); Screen.fullScreen = value; Changed?.Invoke(); } }
        /// 0 = Baja, 1 = Media, 2 = Alta (fija en Alta).
        public static int Quality => 2;

        public static readonly string[] QualityNames = { "Baja", "Media", "Alta" };

        /// Se dispara al cambiar cualquier ajuste (las fuentes de música actualizan su volumen).
        public static event Action Changed;

        public static float SensitivityMultiplier => Mathf.Lerp(0.4f, 2f, Sensitivity);

        static float Get(string k, float def) => Mathf.Clamp01(PlayerPrefs.GetFloat(P + k, def));
        static void Set(string k, float v) { PlayerPrefs.SetFloat(P + k, Mathf.Clamp01(v)); PlayerPrefs.Save(); Changed?.Invoke(); }

        public static void ResetToDefaults()
        {
            foreach (var k in new[] { "Master", "Music", "Sfx", "Voice", "Sens", "InvertY", "Quality" }) PlayerPrefs.DeleteKey(P + k);
            PlayerPrefs.Save();
            ApplyAll();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ aplicar
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyAudio();
            ApplyQuality();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) { ApplyAudio(); ApplyCamera(); }

        public static void ApplyAll() { ApplyAudio(); ApplyCamera(); ApplyQuality(); }

        public static void ApplyAudio() => AudioListener.volume = Master;

        const float BaseYaw = 0.25f, BasePitch = 0.18f;
        public static void ApplyCamera()
        {
            var cam = UnityEngine.Object.FindAnyObjectByType<IsoCameraFollow>();
            if (cam == null) return;
            cam.mouseYawSpeed = BaseYaw * SensitivityMultiplier;
            cam.mousePitchSpeed = BasePitch * SensitivityMultiplier;
            cam.invertY = InvertY;
        }

        public static void ApplyQuality()
        {
            int count = QualitySettings.names.Length;
            if (count == 0) return;
            int level = Quality == 0 ? 0 : (Quality == 1 ? (count - 1) / 2 : count - 1);
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
        }

        public static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
    }

    /// Mantiene el volumen de una fuente de música/ambiente ligado al ajuste "Música" (volumen base × ajuste).
    public class MusicChannel : MonoBehaviour
    {
        public AudioSource source;
        public float baseVolume = 0.35f;

        public static void Attach(AudioSource src, float baseVolume)
        {
            if (src == null) return;
            var c = src.GetComponent<MusicChannel>() ?? src.gameObject.AddComponent<MusicChannel>();
            c.source = src; c.baseVolume = baseVolume;
            c.Apply();
        }

        void OnEnable() => GameSettings.Changed += Apply;
        void OnDisable() => GameSettings.Changed -= Apply;
        void Apply() { if (source != null) source.volume = baseVolume * GameSettings.Music; }
    }
}
