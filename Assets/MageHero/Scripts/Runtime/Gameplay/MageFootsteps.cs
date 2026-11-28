using UnityEngine;

namespace MageHero
{
    /// Sonido de pasos del mago: suena al caminar/correr sobre el suelo y al aterrizar de un salto.
    /// Los clips se cargan de Resources/Audio/Pasos (Paso_1..Paso_N).
    [RequireComponent(typeof(PlayerController))]
    public class MageFootsteps : MonoBehaviour
    {
        [SerializeField] float walkInterval = 0.48f;   // segundos entre pasos al caminar
        [SerializeField] float runInterval = 0.30f;    // segundos entre pasos al correr
        [SerializeField] float volume = 0.2f;           // antes 0.55: se oían demasiado fuertes
        [SerializeField] float minSpeed = 0.08f;       // NormalizedSpeed mínimo para que suenen

        static AudioClip[] clips;

        PlayerController player;
        AudioSource source;
        float timer;
        int lastIndex = -1;
        bool wasGrounded = true;
        float airTime;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            if (clips == null || clips.Length == 0) clips = Resources.LoadAll<AudioClip>("Audio/Pasos");
            if (clips.Length == 0) Debug.LogWarning("No hay clips de pasos en Resources/Audio/Pasos.", this);

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        void Update()
        {
            if (clips == null || clips.Length == 0) return;

            bool grounded = player.IsGrounded && !player.IsJumping;

            // Aterrizaje tras un salto/caída perceptible.
            if (grounded && !wasGrounded && airTime > 0.25f)
            {
                Play(1.0f, 0.85f);
                timer = 0f;
            }
            if (!grounded) airTime += Time.deltaTime; else airTime = 0f;
            wasGrounded = grounded;

            float speed = player.NormalizedSpeed;
            if (!grounded || speed < minSpeed || Time.timeScale == 0f)
            {
                // El primer paso suena enseguida al empezar a moverse.
                timer = Mathf.Min(timer, 0.1f);
                return;
            }

            // Interpola entre caminar (~0.57) y correr (1).
            float t = Mathf.InverseLerp(0.57f, 1f, speed);
            float interval = Mathf.Lerp(walkInterval, runInterval, t);
            if (speed < 0.57f) interval = walkInterval / Mathf.Max(0.5f, speed / 0.57f);

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                Play(Mathf.Lerp(0.8f, 1f, t), 1f);
                timer = interval;
            }
        }

        void Play(float volumeScale, float pitchBase)
        {
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == lastIndex) i = (i + 1) % clips.Length;
            lastIndex = i;
            source.pitch = pitchBase * Random.Range(0.92f, 1.08f);
            source.PlayOneShot(clips[i], volume * volumeScale * Random.Range(0.9f, 1f) * GameSettings.Sfx);
        }
    }
}
