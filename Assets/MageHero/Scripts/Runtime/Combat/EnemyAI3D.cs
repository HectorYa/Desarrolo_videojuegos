using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MageHero
{
    /// IA del enemigo numérico: patrulla su zona, detecta al mago (distancia + cono de visión + línea de visión),
    /// lo persigue y le hace daño a corta distancia con un temporizador (cooldown).
    /// Usa NavMeshAgent si existe y está sobre un NavMesh horneado; si no, movimiento directo pegado al suelo.
    [RequireComponent(typeof(HealthSystem))]
    public class EnemyAI3D : MonoBehaviour
    {
        public enum State { Idle, Chase, Attack, Return }

        [Header("Detección")]
        public float detectionRange = 11f;
        [Tooltip("A esta distancia siempre detecta al jugador, aunque esté de espaldas.")]
        public float closeSenseRange = 4f;
        [Range(10f, 360f)] public float viewAngle = 140f;
        [Tooltip("Deja de perseguir si el jugador se aleja más de esto.")]
        public float loseRange = 18f;
        public bool requireLineOfSight = true;
        [Tooltip("Distancia máxima a la que se aleja de su punto de origen.")]
        public float leashRange = 30f;

        [Header("Movimiento")]
        public float moveSpeed = 2.3f;
        public float turnSpeed = 6f;

        [Header("Ataque")]
        public float attackRange = 1.7f;
        public float attackDamage = 10f;
        public float attackCooldown = 1.2f;

        [Header("Visual")]
        [Tooltip("Hijo que rebota/se aplasta (el 'Body' del enemigo numérico).")]
        public Transform body;

        public State CurrentState { get; private set; }

        static readonly List<EnemyAI3D> all = new List<EnemyAI3D>();

        Transform player;
        HealthSystem playerHealth, self;
        NavMeshAgent agent;
        Vector3 home;
        float nextAttack, calmUntil, phase, lunge;
        int groundMask, obstacleMask;
        Vector3 bodyBasePos;

        void Awake()
        {
            self = GetComponent<HealthSystem>();
            self.team = Team.Enemy;
            if (GetComponent<WorldHealthBar3D>() == null) gameObject.AddComponent<WorldHealthBar3D>();
            if (body == null) body = transform.Find("Body");
            if (body != null) bodyBasePos = body.localPosition;
            // El NumberEnemy decorativo fija la altura y la rotación: la IA toma el control.
            var deco = GetComponent<NumberEnemy>();
            if (deco != null) deco.enabled = false;
            phase = Random.value * 6f;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void Start()
        {
            home = transform.position;
            groundMask = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : Physics.DefaultRaycastLayers;
            obstacleMask = GameLayers.Exists(GameLayers.Obstacles) ? GameLayers.Mask(GameLayers.Obstacles) : 0;

            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (!agent.isOnNavMesh) { agent.enabled = false; agent = null; }
                else { agent.speed = moveSpeed; agent.stoppingDistance = attackRange * 0.9f; agent.angularSpeed = 0f; agent.updateRotation = false; }
            }

            var p = GameObject.FindWithTag("Player");
            if (p != null) { player = p.transform; playerHealth = p.GetComponentInParent<HealthSystem>(); }
        }

        void Update()
        {
            if (self.IsDead) return;
            Think();
            Animate();
        }

        void Think()
        {
            if (player == null || playerHealth == null || playerHealth.IsDead || Time.time < calmUntil)
            {
                GoHome();
                return;
            }

            Vector3 to = player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            bool tooFarFromHome = Vector3.Distance(transform.position, home) > leashRange;

            if (dist <= attackRange)
            {
                CurrentState = State.Attack;
                Stop();
                Face(to);
                if (Time.time >= nextAttack)
                {
                    nextAttack = Time.time + attackCooldown;
                    if (playerHealth.TakeDamage(attackDamage)) lunge = 1f;
                }
                return;
            }

            bool engaged = CurrentState == State.Chase || CurrentState == State.Attack;
            if (!tooFarFromHome && ((engaged && dist < loseRange) || CanSee(to, dist)))
            {
                CurrentState = State.Chase;
                MoveTowards(player.position);
                return;
            }
            GoHome();
        }

        bool CanSee(Vector3 flatTo, float dist)
        {
            if (dist > detectionRange) return false;
            if (dist > closeSenseRange && Vector3.Angle(transform.forward, flatTo) > viewAngle * 0.5f) return false;
            if (!requireLineOfSight || obstacleMask == 0) return true;
            Vector3 eye = transform.position + Vector3.up * 1.2f, target = player.position + Vector3.up * 0.8f;
            return !Physics.Linecast(eye, target, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        void GoHome()
        {
            Vector3 d = home - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.5f) { CurrentState = State.Return; MoveTowards(home); }
            else { CurrentState = State.Idle; Stop(); }
        }

        void MoveTowards(Vector3 target)
        {
            Vector3 to = target - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            Face(to);

            if (agent != null) { agent.isStopped = false; agent.SetDestination(target); return; }

            Vector3 dir = to.normalized;
            dir += Separation() * 0.8f; dir.y = 0f; dir.Normalize();
            Vector3 chest = transform.position + Vector3.up * 0.9f;
            float step = moveSpeed * Time.deltaTime;

            // Rodea obstáculos simples (árboles, rocas) probando desvíos laterales.
            if (obstacleMask != 0 && Physics.SphereCast(chest, 0.5f, dir, out _, 0.8f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 left = Quaternion.Euler(0, -60f, 0) * dir, right = Quaternion.Euler(0, 60f, 0) * dir;
                if (!Physics.SphereCast(chest, 0.5f, right, out _, 0.8f, obstacleMask, QueryTriggerInteraction.Ignore)) dir = right;
                else if (!Physics.SphereCast(chest, 0.5f, left, out _, 0.8f, obstacleMask, QueryTriggerInteraction.Ignore)) dir = left;
                else return;
            }

            Vector3 next = transform.position + dir * step;
            if (Physics.Raycast(next + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < 0.6f) return;   // pendiente demasiado fuerte
                next.y = hit.point.y;
            }
            transform.position = next;
        }

        Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            foreach (var o in all)
            {
                if (o == this) continue;
                Vector3 d = transform.position - o.transform.position; d.y = 0f;
                float m = d.magnitude;
                if (m > 0.01f && m < 1.8f) push += d / m * (1.8f - m);
            }
            return push;
        }

        void Stop() { if (agent != null && agent.isOnNavMesh) agent.isStopped = true; }

        void Face(Vector3 flatDir)
        {
            if (flatDir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatDir), turnSpeed * Time.deltaTime);
        }

        void Animate()
        {
            if (body == null) return;
            float t = Time.time, speedFactor = CurrentState == State.Chase ? 2.2f : 1f;
            float w = Mathf.Sin(t * 3.2f * speedFactor + phase) * 0.03f;
            lunge = Mathf.MoveTowards(lunge, 0f, Time.deltaTime * 4f);
            body.localPosition = bodyBasePos + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 1.6f * speedFactor + phase)) * 0.12f, lunge * 0.45f);
            body.localScale = new Vector3(1f + w, 1f - w, 1f + w);
        }

        /// Hace que los enemigos cercanos se retiren unos segundos (lo usa el respawn del jugador).
        public static void CalmAround(Vector3 position, float radius, float seconds)
        {
            foreach (var e in all)
                if (Vector3.Distance(e.transform.position, position) < radius)
                {
                    e.calmUntil = Time.time + seconds;
                    e.CurrentState = State.Return;
                }
        }

        /// Añade todo lo necesario para que un objeto (p. ej. un enemigo del LevelBuilder) pueda combatir.
        public static EnemyAI3D MakeCombatant(GameObject go, float maxHealth = 60f)
        {
            var hs = go.GetComponent<HealthSystem>();
            if (hs == null) { hs = go.AddComponent<HealthSystem>(); hs.SetMaxHealth(maxHealth); }
            hs.team = Team.Enemy; hs.destroyOnDeath = true;
            if (go.GetComponent<Collider>() == null)
            {
                var c = go.AddComponent<CapsuleCollider>(); c.radius = 0.7f; c.height = 1.8f; c.center = new Vector3(0f, 0.9f, 0f);
            }
            if (GameLayers.Exists(GameLayers.Enemies)) GameLayers.Set(go, GameLayers.Enemies);
            var ai = go.GetComponent<EnemyAI3D>();
            return ai != null ? ai : go.AddComponent<EnemyAI3D>();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawWireSphere(transform.position, closeSenseRange);
        }
    }
}
