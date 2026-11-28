using UnityEngine.UI;
using UnityEngine;

namespace MageHero
{
    /// Nivel de un enemigo (1-5): ajusta su vida, daño, velocidad de ataque y movimiento (LevelStats),
    /// oscurece su color (más sombrío a mayor nivel) y muestra un cartel "Nv X" sobre la barra de vida.
    /// Lo añade ProgressionManager a cada enemigo; también puede ponerse a mano en un prefab.
    [RequireComponent(typeof(HealthSystem))]
    public class EnemyLevel : MonoBehaviour
    {
        [SerializeField, Range(1, 5)] int level = 1;
        [Tooltip("Jefe: no se reescalan sus estadísticas (las fija FinalBoss), solo el color y el cartel.")]
        public bool isBoss;
        [Tooltip("Nombre que se muestra en el cartel del jefe.")]
        public string displayName = "JEFE";
        [Tooltip("Mostrar el nombre también en enemigos normales (p. ej. Mago oscuro).")]
        public bool showName;
        [Tooltip("Oscurecer el color según el nivel. Desactivado en jefes con colores propios (Azhar, Hidrakhan…).")]
        public bool applyTint = true;

        public int Level => level;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        HealthSystem health;
        EnemyAI3D ai;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        Text label;
        Canvas labelCanvas;

        void Awake()
        {
            health = GetComponent<HealthSystem>();
            ai = GetComponent<EnemyAI3D>();
        }

        void Start() => SetLevel(level, refill: true);

        /// Cambia el nivel. refill = rellena la vida (al aparecer); si no, conserva el % de vida actual.
        public void SetLevel(int newLevel, bool refill)
        {
            level = LevelStats.Clamp(newLevel);
            if (!isBoss)
            {
                float ratio = health.Normalized;
                health.SetMaxHealth(LevelStats.EnemyMaxHealth(level), refill);
                if (!refill) health.Drain(health.Current - ratio * health.maxHealth, 1f);
                if (ai == null) ai = GetComponent<EnemyAI3D>();
                if (ai != null)
                {
                    ai.attackDamage = LevelStats.EnemyDamage(level);
                    ai.attackCooldown = LevelStats.EnemyAttackCooldown(level);
                    ai.moveSpeed = LevelStats.EnemyMoveSpeed(level);
                    // Persiguen desde más lejos (antes solo reaccionaban estando muy cerca).
                    ai.detectionRange = LevelStats.EnemyDetectionRange(level);
                    ai.loseRange = ai.detectionRange + 12f;
                    ai.closeSenseRange = 7f;
                    ai.viewAngle = 240f;
                    ai.leashRange = Mathf.Max(ai.leashRange, 50f);
                }
                var caster = GetComponent<EnemyCaster3D>();
                if (caster != null)
                {
                    caster.damage = LevelStats.RangedDamage(level);
                    caster.cooldown = LevelStats.RangedCooldown(level);
                }
            }
            ApplyTint();
            UpdateLabel();
        }

        void ApplyTint()
        {
            if (!applyTint) return;
            if (renderers == null)
                renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r is MeshRenderer || r is SkinnedMeshRenderer);
            mpb ??= new MaterialPropertyBlock();
            Color tint = LevelStats.EnemyTint(level);
            foreach (var r in renderers)
            {
                if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BaseColorId)) continue;
                Color baseCol = r.sharedMaterial.GetColor(BaseColorId);
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, baseCol * tint);
                r.SetPropertyBlock(mpb);
            }
        }

        void UpdateLabel()
        {
            if (label == null) return;                       // se crea en LateUpdate, cuando ya existe la barra de vida
            label.text = (isBoss || showName) ? $"{displayName} · Nv {level}" : $"Nv {level}";
            label.color = LevelStats.LevelColor(level);
        }

        float startTime = -1f;

        void LateUpdate()
        {
            if (labelCanvas != null) return;
            if (startTime < 0f) startTime = Time.time;
            // Espera a que WorldHealthBar3D cree su barra (así ella no cuenta este cartel al calcular su altura).
            var bar = transform.Find("HealthBar");
            if (bar == null && Time.time - startTime < 1f) return;
            Vector3 pos = bar != null ? bar.localPosition + Vector3.up * 0.32f : Vector3.up * 3f;
            labelCanvas = UIText.WorldLabel(transform, "Nv", pos, new Vector2(isBoss || showName ? 300f : 90f, 36f), 26,
                                            Color.white, new Color(0.05f, 0.06f, 0.12f, 0.85f), 0.011f / Mathf.Max(0.01f, transform.lossyScale.x));
            labelCanvas.name = "LevelLabel";
            label = labelCanvas.GetComponentInChildren<Text>();
            UpdateLabel();
        }
    }
}
