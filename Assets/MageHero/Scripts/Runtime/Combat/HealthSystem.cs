using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MageHero
{
    public enum Team { Player, Enemy }

    /// Componente de vida reutilizable para el mago y para los enemigos.
    /// - TakeDamage / Heal / Revive
    /// - Eventos C# (para código) y UnityEvents (para conectar en el Inspector).
    /// - Registro estático de todas las entidades vivas (lo usa la puntería automática).
    public class HealthSystem : MonoBehaviour
    {
        [Header("Identidad")]
        public Team team = Team.Enemy;

        [Header("Vida")]
        [Min(1f)] public float maxHealth = 100f;
        [Tooltip("Segundos de invulnerabilidad tras recibir un golpe (evita daño múltiple en el mismo frame).")]
        [Min(0f)] public float hitInvulnerability = 0.15f;

        [Header("Muerte")]
        [Tooltip("Enemigos: true (se destruyen). Jugador: false (lo maneja PlayerRespawn3D).")]
        public bool destroyOnDeath = true;
        [Min(0f)] public float destroyDelay = 0f;

        [Serializable] public class HealthChangedEvent : UnityEvent<float, float> { }   // (actual, máximo)

        [Header("Eventos (Inspector)")]
        public HealthChangedEvent onHealthChanged = new HealthChangedEvent();
        public UnityEvent onDamaged = new UnityEvent();
        public UnityEvent onDied = new UnityEvent();
        public UnityEvent onRevived = new UnityEvent();

        // Eventos para código
        public event Action<HealthSystem, float> Damaged;      // (quién, cantidad)
        public event Action<HealthSystem> Died;
        public event Action<HealthSystem> Changed;

        public static readonly List<HealthSystem> Alive = new List<HealthSystem>();

        public float Current { get; private set; }
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        float invulnerableUntil;

        void Awake() { Current = maxHealth; }

        void OnEnable() { if (!IsDead && !Alive.Contains(this)) Alive.Add(this); }
        void OnDisable() { Alive.Remove(this); }

        /// Resta vida. Devuelve true si el golpe se aplicó.
        public bool TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || IsInvulnerable) return false;
            Current = Mathf.Max(0f, Current - amount);
            invulnerableUntil = Time.time + hitInvulnerability;
            Notify();
            Damaged?.Invoke(this, amount);
            onDamaged.Invoke();
            if (Current <= 0f) Die();
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Notify();
        }

        /// Restaura toda la vida (usado al reaparecer). invulnerability = segundos sin recibir daño.
        public void Revive(float invulnerability = 0f)
        {
            IsDead = false;
            Current = maxHealth;
            invulnerableUntil = Time.time + invulnerability;
            if (isActiveAndEnabled && !Alive.Contains(this)) Alive.Add(this);
            Notify();
            onRevived.Invoke();
        }

        /// Cambia la vida máxima (y opcionalmente rellena). Útil al configurar enemigos por código.
        public void SetMaxHealth(float value, bool refill = true)
        {
            maxHealth = Mathf.Max(1f, value);
            Current = refill ? maxHealth : Mathf.Min(Current, maxHealth);
            Notify();
        }

        /// Penalización que ignora la invulnerabilidad y nunca baja de 'minimum' (no mata). Útil en el quiz.
        public void Drain(float amount, float minimum = 1f)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Max(Mathf.Min(minimum, Current), Current - amount);
            Notify();
            Damaged?.Invoke(this, amount);
            onDamaged.Invoke();
        }

        public void SetInvulnerable(float seconds) => invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Alive.Remove(this);
            Died?.Invoke(this);
            onDied.Invoke();
            if (destroyOnDeath) Destroy(gameObject, destroyDelay);
        }

        void Notify()
        {
            onHealthChanged.Invoke(Current, maxHealth);
            Changed?.Invoke(this);
        }
    }
}
