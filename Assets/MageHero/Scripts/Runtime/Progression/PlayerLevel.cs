using System;
using UnityEngine;

namespace MageHero
{
    /// Nivel del mago (1-5). Sube automáticamente cada 4 enemigos derrotados (LevelStats.KillsPerPlayerLevel)
    /// o al superar un Reto matemático (ProgressionManager). Al subir: más vida máxima y más daño de hechizo.
    [RequireComponent(typeof(HealthSystem))]
    public class PlayerLevel : MonoBehaviour
    {
        public static PlayerLevel Instance { get; private set; }

        /// Multiplicador de daño que aplica Projectile3D a los hechizos del jugador.
        public static float DamageMultiplier => Instance != null ? LevelStats.PlayerDamageMultiplier(Instance.Level) : 1f;

        [SerializeField, Range(1, 5)] int startLevel = 1;

        public int Level { get; private set; }
        /// Enemigos derrotados desde la última subida por combate (0..3).
        public int KillProgress { get; private set; }
        public bool IsMax => Level >= LevelStats.MaxLevel;

        public event Action<int> LeveledUp;     // nuevo nivel
        public event Action Changed;

        HealthSystem health;

        void Awake()
        {
            Instance = this;
            health = GetComponent<HealthSystem>();
            Level = LevelStats.Clamp(startLevel);
        }

        void Start() => ApplyStats(refill: true);
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// Lo llama ProgressionManager por cada enemigo derrotado.
        public void RegisterKill()
        {
            if (IsMax) { KillProgress = 0; Changed?.Invoke(); return; }
            KillProgress++;
            if (KillProgress >= LevelStats.KillsPerPlayerLevel) { KillProgress = 0; LevelUp("por tus victorias"); }
            else Changed?.Invoke();
        }

        /// Partida guardada: vuelve al nivel y al progreso de victorias que tenía el mago (con la vida llena).
        public void Restore(int level, int killProgress)
        {
            Level = LevelStats.Clamp(level);
            KillProgress = Mathf.Max(0, killProgress);
            ApplyStats(refill: true);
            Changed?.Invoke();
        }

        /// Sube un nivel (si no está en el máximo). Devuelve false si ya era nivel 5.
        public bool LevelUp(string reason)
        {
            if (IsMax) return false;
            Level++;
            ApplyStats(refill: false);
            health.Heal(health.maxHealth * 0.3f);          // pequeña recompensa de vida al subir
            LeveledUp?.Invoke(Level);
            Changed?.Invoke();
            UIManager.Instance?.ShowMessage($"¡Subiste a Nivel {Level} {reason}!  Vida {health.maxHealth:0} · Daño x{LevelStats.PlayerDamageMultiplier(Level):0.00}", 3.5f);
            return true;
        }

        void ApplyStats(bool refill)
        {
            float ratio = health.Normalized;
            health.SetMaxHealth(LevelStats.PlayerMaxHealth(Level), refill);
            if (!refill) health.Heal(Mathf.Max(0f, ratio * health.maxHealth - health.Current));   // conserva el % de vida
        }
    }
}
