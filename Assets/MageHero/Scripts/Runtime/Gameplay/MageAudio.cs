using UnityEngine;

namespace MageHero
{
    public static class MageAudio
    {
        static AudioClip attackClip;

        public static void PlayLevelAmbience(LevelInfo levelInfo)
        {
            string resourceName;
            switch (levelInfo.levelId)
            {
                case "desierto": resourceName = "Desierto"; break;
                case "otono": resourceName = "Otono"; break;
                case "primavera": resourceName = "Primavera"; break;
                // Nivel del jefe: usa Audio/Volcan si se agrega; mientras tanto, el ambiente del desierto.
                case "volcan": resourceName = Resources.Load<AudioClip>("Audio/Volcan") != null ? "Volcan" : "Desierto"; break;
                default:
                    Debug.LogWarning($"No ambience is configured for level '{levelInfo.levelId}'.", levelInfo);
                    return;
            }

            var clip = Resources.Load<AudioClip>("Audio/" + resourceName);
            if (clip == null)
            {
                Debug.LogError($"Could not load ambience clip 'Audio/{resourceName}'.", levelInfo);
                return;
            }

            var source = levelInfo.GetComponent<AudioSource>();
            if (source == null) source = levelInfo.gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.35f * GameSettings.Music;
            source.Play();
            if (levelInfo.levelId != "volcan") MusicChannel.Attach(source, 0.35f);   // sigue el ajuste "Música" en vivo

            // Volcán: música de persecución mientras el jefe o los enemigos van a por el jugador.
            if (levelInfo.levelId == "volcan") ChaseMusic.Attach(levelInfo.gameObject, source);
        }

        static int exclusiveDepth;

        /// Cinemática con sonido exclusivo: pausa TODO el audio del juego (música, ambiente, pasos, enemigos…).
        /// Solo suenan las fuentes con ignoreListenerPause (las de la propia cinemática y la interfaz).
        /// Es una pausa, no bajar el volumen: al terminar todo continúa justo donde estaba.
        public static void BeginExclusiveAudio()
        {
            exclusiveDepth++;
            AudioListener.pause = true;
        }

        public static void EndExclusiveAudio()
        {
            exclusiveDepth = Mathf.Max(0, exclusiveDepth - 1);
            if (exclusiveDepth == 0 && !PauseMenu.IsOpen) AudioListener.pause = false;
        }

        /// Derrumbe del puente (Costa Chatarra). Suena en 2D porque la cámara enfoca el puente durante la cinemática.
        public static void PlayBridgeBreak(GameObject bridge)
        {
            var clip = Resources.Load<AudioClip>("Audio/PuenteRoto");
            if (clip == null)
            {
                Debug.LogError("Could not load bridge clip 'Audio/PuenteRoto'.", bridge);
                return;
            }

            var source = bridge.GetComponent<AudioSource>();
            if (source == null)
            {
                source = bridge.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.ignoreListenerPause = true;      // la cinemática pausa el resto del audio: este sonido sí debe oírse
            source.PlayOneShot(clip, 1f * GameSettings.Sfx);
        }

        static AudioClip enemyAlertClip, enemyDeathClip;
        static float lastEnemyAlertTime = -10f;

        /// Aviso cuando una criatura del bosque ve al jugador y va a por él (gorjeo corto, no un rugido de zombi).
        /// Pausa global corta para que varios enemigos que lo ven a la vez no se amontonen.
        public static void PlayEnemyAlert(Vector3 position)
        {
            if (Time.time - lastEnemyAlertTime < 0.8f) return;
            if (enemyAlertClip == null) enemyAlertClip = Resources.Load<AudioClip>("Audio/CriaturaAlerta");
            if (enemyAlertClip == null) { Debug.LogError("Could not load clip 'Audio/CriaturaAlerta'."); return; }
            lastEnemyAlertTime = Time.time;
            PlayDetached(enemyAlertClip, position, 0.45f, Random.Range(0.9f, 1.12f));
        }

        /// La criatura se esfuma al morir: "puf" suave de polvo (el enemigo se destruye al instante, por eso suena desde un objeto aparte).
        public static void PlayEnemyDeath(Vector3 position)
        {
            if (enemyDeathClip == null) enemyDeathClip = Resources.Load<AudioClip>("Audio/CriaturaEsfuma");
            if (enemyDeathClip == null) { Debug.LogError("Could not load clip 'Audio/CriaturaEsfuma'."); return; }
            PlayDetached(enemyDeathClip, position, 0.6f, Random.Range(0.92f, 1.08f));
        }

        // ------------------------------------------------------------------ UI (botones del menú)
        static AudioClip uiHoverClip, uiClickClip;
        static AudioSource uiSource;
        static float lastHoverTime = -10f;

        /// Fuente 2D que sobrevive a los cambios de escena: el clic de "Jugar" no se corta al cargar el nivel.
        static AudioSource UISource()
        {
            if (uiSource != null) return uiSource;
            var go = new GameObject("UIAudio");
            Object.DontDestroyOnLoad(go);
            uiSource = go.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
            uiSource.ignoreListenerPause = true;   // suena también con el juego en pausa
            return uiSource;
        }

        public static void PlayUIHover()
        {
            if (Time.unscaledTime - lastHoverTime < 0.05f) return;
            lastHoverTime = Time.unscaledTime;
            if (uiHoverClip == null) uiHoverClip = Resources.Load<AudioClip>("Audio/UI/UI_Hover");
            if (uiHoverClip != null) UISource().PlayOneShot(uiHoverClip, 0.35f * GameSettings.Sfx);
        }

        public static void PlayUIClick()
        {
            if (uiClickClip == null) uiClickClip = Resources.Load<AudioClip>("Audio/UI/UI_Click");
            if (uiClickClip != null) UISource().PlayOneShot(uiClickClip, 0.7f * GameSettings.Sfx);
        }

        static AudioClip bossRoarClip;

        /// Rugido del jefe final (Elemental de Obsidiana). pitch < 1 = más grave (segunda fase).
        public static void PlayBossRoar(Vector3 position, float pitch = 1f)
        {
            if (bossRoarClip == null) bossRoarClip = Resources.Load<AudioClip>("Audio/JefeFinal");
            if (bossRoarClip == null) { Debug.LogError("Could not load clip 'Audio/JefeFinal'."); return; }
            PlayDetached(bossRoarClip, position, 1f, pitch);
        }

        static void PlayDetached(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            var go = new GameObject("SFX_" + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume * GameSettings.Sfx;
            source.pitch = pitch;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        public static void PlayAttack(GameObject player)
        {
            if (attackClip == null) attackClip = Resources.Load<AudioClip>("Audio/AtaqueMago");
            if (attackClip == null)
            {
                Debug.LogError("Could not load attack clip 'Audio/AtaqueMago'.", player);
                return;
            }

            var source = player.GetComponent<AudioSource>();
            if (source == null)
            {
                source = player.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.PlayOneShot(attackClip, 0.85f * GameSettings.Sfx);
        }
    }
}
