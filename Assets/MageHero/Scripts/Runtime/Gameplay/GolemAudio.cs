using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Sonidos del Gólem de Piedra (Resources/Audio/Golem, los genera Tools > Mage Hero > Audio > Generar sonidos
    /// del Gólem): graves y rocosos. Sonidos sueltos en 3D (se oyen más cerca del gólem) y bucles (rodar, ambiente).
    public static class GolemAudio
    {
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { clips.Clear(); lastPlay.Clear(); }

        static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>("Audio/Golem/" + name);
            clips[name] = c;
            return c;
        }

        /// Sonido suelto en un punto. minInterval evita que se amontone el mismo sonido (p. ej. muchos pilares a la vez).
        public static void Play(string name, Vector3 position, float volume = 1f, float pitch = 1f, float minInterval = 0f)
        {
            var clip = Clip(name);
            if (clip == null) return;
            if (minInterval > 0f && lastPlay.TryGetValue(name, out float t) && Time.time - t < minInterval) return;
            lastPlay[name] = Time.time;
            var go = new GameObject("SFX_" + name);
            go.transform.position = position;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume) * GameSettings.Sfx;
            s.pitch = pitch * Random.Range(0.94f, 1.06f);
            s.spatialBlend = 0.75f;                          // casi 3D: se nota de dónde viene, sin desaparecer de lejos
            s.minDistance = 8f; s.maxDistance = 70f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.dopplerLevel = 0f;
            s.ignoreListenerPause = true;                    // también en las cinemáticas que pausan el resto del audio
            s.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, s.pitch) + 0.1f);
        }

        /// Bucle que sigue a un objeto (o en 2D si follow es null). Se detiene con StopLoop.
        public static AudioSource Loop(string name, Transform follow, float volume = 1f)
        {
            var clip = Clip(name);
            if (clip == null) return null;
            var go = new GameObject("Loop_" + name);
            if (follow != null) { go.transform.SetParent(follow, false); go.transform.localPosition = Vector3.up; }
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true;
            s.volume = 0f;
            s.spatialBlend = follow != null ? 0.75f : 0f;
            s.minDistance = 8f; s.maxDistance = 70f; s.rolloffMode = AudioRolloffMode.Linear; s.dopplerLevel = 0f;
            s.time = Random.Range(0f, clip.length * 0.9f);
            s.Play();
            var fader = go.AddComponent<LoopFader>();
            fader.target = Mathf.Clamp01(volume) * GameSettings.Sfx;
            return s;
        }

        public static void StopLoop(AudioSource s, float fadeSeconds = 0.6f)
        {
            if (s == null) return;
            var f = s.GetComponent<LoopFader>();
            if (f == null) { Object.Destroy(s.gameObject); return; }
            s.transform.SetParent(null, true);               // sigue sonando aunque el gólem desaparezca
            f.FadeOut(fadeSeconds);
        }

        /// Entrada y salida suaves de los bucles.
        class LoopFader : MonoBehaviour
        {
            public float target;
            float fadeOut = -1f;
            AudioSource s;

            void Awake() => s = GetComponent<AudioSource>();
            public void FadeOut(float seconds) { fadeOut = Mathf.Max(0.05f, seconds); }

            void Update()
            {
                if (s == null) return;
                if (fadeOut > 0f)
                {
                    s.volume = Mathf.MoveTowards(s.volume, 0f, target / fadeOut * Time.unscaledDeltaTime);
                    if (s.volume <= 0.001f) Destroy(gameObject);
                }
                else s.volume = Mathf.MoveTowards(s.volume, target, target / 0.6f * Time.unscaledDeltaTime);
            }
        }
    }
}
