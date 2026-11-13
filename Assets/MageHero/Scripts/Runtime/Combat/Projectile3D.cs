using UnityEngine;

namespace MageHero
{
    /// Proyectil mágico: viaja en línea recta con su Rigidbody y daña al primer HealthSystem
    /// de otro equipo que toque (OnTriggerEnter). Se destruye al chocar con terreno/obstáculos.
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile3D : MonoBehaviour
    {
        [Min(0.1f)] public float speed = 16f;
        [Min(0f)] public float damage = 25f;
        [Min(0.1f)] public float lifetime = 2.5f;
        public Team ownerTeam = Team.Player;
        [Tooltip("Opcional: efecto de partículas que se instancia al impactar.")]
        public GameObject hitEffect;

        Rigidbody rb;
        bool spent;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;   // los triggers no usan CCD
            foreach (var c in GetComponentsInChildren<Collider>()) c.isTrigger = true;
            if (GameLayers.Exists(GameLayers.Projectiles)) GameLayers.Set(gameObject, GameLayers.Projectiles, true);
        }

        public void Launch(Vector3 direction, Team owner)
        {
            ownerTeam = owner;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(direction);
            rb.linearVelocity = direction * speed;
            Destroy(gameObject, lifetime);
        }

        void OnTriggerEnter(Collider other)
        {
            if (spent || other.isTrigger) return;          // ignora agua, zonas y otros triggers

            var target = other.GetComponentInParent<HealthSystem>();
            if (target != null)
            {
                if (target.team == ownerTeam) return;      // no dañar al lanzador ni a aliados
                target.TakeDamage(damage);
            }
            Impact();
        }

        void Impact()
        {
            spent = true;
            if (hitEffect != null) Destroy(Instantiate(hitEffect, transform.position, Quaternion.identity), 2f);
            Destroy(gameObject);
        }
    }
}
