using UnityEngine;

namespace MageHero
{
    /// Música de persecución (Resources/Audio/Persecucion) para el nivel del volcán.
    /// Entra con fundido mientras el jefe pelea o algún enemigo persigue al jugador,
    /// y baja el ambiente del nivel mientras suena. Se apaga al terminar la persecución.
    public class ChaseMusic : MonoBehaviour
    {
        [SerializeField] float volume = 0.6f;
        [SerializeField] float fadeIn = 0.8f;      // segundos para subir
        [SerializeField] float fadeOut = 2.0f;     // segundos para bajar
        [SerializeField] float ambienceDuck = 0.35f;   // fracción del ambiente mientras suena
        [SerializeField] float releaseDelay = 2.5f;    // sigue sonando un poco tras perder al enemigo

        AudioSource music, ambience;
        float ambienceBase, lastChaseTime = -100f;
        ObsidianBoss boss;
        HealthSystem playerHealth;
        float nextLookup;

        public static void Attach(GameObject host, AudioSource ambienceSource)
        {
            var c = host.GetComponent<ChaseMusic>();
            if (c == null) c = host.AddComponent<ChaseMusic>();
            c.ambience = ambienceSource;
            c.ambienceBase = 0.35f;                 // volumen base del ambiente (se multiplica por el ajuste "Música")
        }

        void Awake()
        {
            var clip = Resources.Load<AudioClip>("Audio/Persecucion");
            if (clip == null) { Debug.LogError("Could not load clip 'Audio/Persecucion'.", this); enabled = false; return; }
            music = gameObject.AddComponent<AudioSource>();
            music.clip = clip;
            music.loop = true;
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.volume = 0f;
        }

        void Update()
        {
            if (Time.time >= nextLookup)
            {
                nextLookup = Time.time + 1f;
                if (boss == null) boss = FindAnyObjectByType<ObsidianBoss>();
                if (playerHealth == null)
                {
                    var p = GameObject.FindWithTag("Player");
                    if (p != null) playerHealth = p.GetComponentInParent<HealthSystem>();
                }
            }

            bool playerAlive = playerHealth == null || !playerHealth.IsDead;
            bool bossFighting = boss != null && (boss.CurrentState == ObsidianBoss.State.Waking || boss.CurrentState == ObsidianBoss.State.Fight);
            if (playerAlive && (bossFighting || EnemyAI3D.AnyChasing)) lastChaseTime = Time.time;
            bool chasing = Time.time - lastChaseTime < releaseDelay;

            float mv = GameSettings.Music;
            float target = chasing ? volume * mv : 0f;
            float rate = Mathf.Max(0.01f, volume * mv) / Mathf.Max(0.05f, chasing ? fadeIn : fadeOut);
            music.volume = Mathf.MoveTowards(music.volume, target, rate * Time.unscaledDeltaTime);

            if (music.volume > 0.001f) { if (!music.isPlaying) music.Play(); }
            else if (music.isPlaying) music.Stop();   // la próxima persecución empieza desde el inicio

            if (ambience != null)
                ambience.volume = ambienceBase * mv * Mathf.Lerp(1f, ambienceDuck, volume * mv > 0.001f ? music.volume / (volume * mv) : 0f);
        }
    }
}
