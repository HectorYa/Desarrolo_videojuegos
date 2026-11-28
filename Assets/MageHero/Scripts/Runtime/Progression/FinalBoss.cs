using UnityEngine;

namespace MageHero
{
    /// Jefe final de cada nivel, al final del camino iluminado (BossPath):
    ///  - Nivel 1 Otoño:     Gólem de piedra que lanza rocas (GolemBoss).
    ///  - Nivel 2 Desierto:  Azhar, el Simún: wyvern de arena con fuego y coletazo (DragonBoss).
    ///  - Nivel 3 Primavera: Hidrakhan: dragón de dos cabezas, hielo y fuego (DragonBoss).
    ///  - Otros niveles:     enemigo numérico gigante (versión anterior).
    /// Duerme hasta que el mago se acerca. Al morir completa el nivel y desbloquea el siguiente (LevelProgress).
    [RequireComponent(typeof(HealthSystem))]
    public class FinalBoss : MonoBehaviour
    {
        public enum Kind { Numeric, Golem, Azhar, Hidrakhan }

        public string bossName = "Jefe final";
        public Kind kind = Kind.Numeric;
        public float maxHealth = 480f, damage = 24f, attackCooldown = 1.5f, attackRange = 3.2f, moveSpeed = 2.1f;
        public float wakeRadius = 15f, scale = 2.3f;

        public bool IsAwake { get; private set; }

        EnemyAI3D ai;
        HealthSystem health;
        Transform player;

        public static Kind KindForLevel(string levelId)
        {
            switch (levelId)
            {
                case "otono": return Kind.Golem;
                case "desierto": return Kind.Azhar;
                case "primavera": return Kind.Hidrakhan;
                default: return Kind.Numeric;
            }
        }

        public static string NameFor(Kind k)
        {
            switch (k)
            {
                case Kind.Golem: return "Gólem de Piedra";
                case Kind.Azhar: return "Azhar, el Simún";
                case Kind.Hidrakhan: return "Hidrakhan";
                default: return "Guardián del Caos";
            }
        }

        public static FinalBoss Spawn(Vector3 position, string name)
        {
            var info = FindAnyObjectByType<LevelInfo>();
            var kind = KindForLevel(info != null ? info.levelId : "");
            return kind == Kind.Numeric ? SpawnNumeric(position, name) : SpawnCustom(position, kind, NameFor(kind));
        }

        // ------------------------------------------------------------------ gólem y dragones (modelos por código)
        static FinalBoss SpawnCustom(Vector3 position, Kind kind, string name)
        {
            if (RuntimeMaterials.Ground(position, out var g)) position = g;
            var go = new GameObject("JefeFinal");
            go.SetActive(false);
            go.transform.position = position;
            var hs = go.AddComponent<HealthSystem>();
            hs.team = Team.Enemy; hs.destroyOnDeath = true; hs.destroyDelay = 0f;
            go.AddComponent<EnemyAI3D>();
            var boss = go.AddComponent<FinalBoss>();
            boss.kind = kind; boss.bossName = name;

            var col = go.AddComponent<CapsuleCollider>();
            switch (kind)
            {
                case Kind.Golem:
                    GolemBoss.Build(go, new Color(0.55f, 0.52f, 0.48f), new Color(1f, 0.55f, 0.15f));
                    boss.maxHealth = 420f; boss.moveSpeed = 1.8f; boss.wakeRadius = 16f;
                    col.radius = 1.1f; col.height = 3.8f; col.center = Vector3.up * 1.9f;
                    break;
                case Kind.Azhar:
                {
                    var d = DragonBoss.Build(go, AzharSpec());
                    d.fireballDamage = 14f; d.cooldown = 3.0f; d.fireballsPerVolley = 3; d.spreadDegrees = 11f;
                    d.landsInPhase2 = true; d.tailDamage = 20f; d.roarPitch = 1.1f;
                    boss.maxHealth = 600f; boss.moveSpeed = 2.4f; boss.wakeRadius = 18f;
                    col.direction = 2; col.radius = 1.3f * 1.15f; col.height = 4.6f * 1.15f; col.center = Vector3.up * DragonBoss.HoverHeight;
                    break;
                }
                default: // Hidrakhan
                {
                    var d = DragonBoss.Build(go, HidrakhanSpec());
                    d.fireballDamage = 16f; d.iceDamage = 10f; d.cooldown = 2.4f; d.fireballsPerVolley = 3; d.shardsPerVolley = 5; d.spreadDegrees = 12f;
                    d.headsTogetherLowHp = true; d.roarPitch = 0.85f;
                    boss.maxHealth = 750f; boss.moveSpeed = 2.5f; boss.wakeRadius = 18f;
                    col.direction = 2; col.radius = 1.3f * 1.3f; col.height = 4.6f * 1.3f; col.center = Vector3.up * DragonBoss.HoverHeight;
                    break;
                }
            }
            var el = go.AddComponent<EnemyLevel>();
            el.isBoss = true; el.applyTint = false; el.displayName = name.ToUpperInvariant();
            if (GameLayers.Exists(GameLayers.Enemies)) GameLayers.Set(go, GameLayers.Enemies);
            go.SetActive(true);
            hs.SetMaxHealth(boss.maxHealth);
            el.SetLevel(LevelStats.MaxLevel, refill: false);
            return boss;
        }

        // ------------------------------------------------------------------ diseños de los dragones
        /// Azhar, el Simún: wyvern del desierto (alas = patas delanteras). Ocre y óxido, vientre color hueso,
        /// membrana de arena clara, púas oscuras y maza con púas en la cola.
        static DragonSpec AzharSpec() => new DragonSpec
        {
            body = new Color(0.74f, 0.47f, 0.25f), dark = new Color(0.42f, 0.22f, 0.13f), belly = new Color(0.92f, 0.8f, 0.6f),
            membrane = new Color(0.9f, 0.74f, 0.52f), spike = new Color(0.24f, 0.15f, 0.12f), bone = new Color(0.93f, 0.87f, 0.74f),
            wyvern = true, tailClub = true, wingSpikes = true, size = 1.15f,
            heads = new[]
            {
                new DragonHeadSpec
                {
                    skin = new Color(0.72f, 0.45f, 0.24f), dark = new Color(0.36f, 0.2f, 0.13f), crest = new Color(0.24f, 0.15f, 0.12f),
                    horn = new Color(0.9f, 0.84f, 0.7f), eye = new Color(1f, 0.78f, 0.22f),
                    breathCore = new Color(1f, 0.9f, 0.5f), breathGlow = new Color(1f, 0.5f, 0.12f),
                },
            },
        };

        /// Hidrakhan: cuerpo de pizarra oscura con alas de placas; cabeza de hielo (cresta de cristales) y
        /// cabeza de fuego (negra, cuernos naranjas y grietas de lava).
        static DragonSpec HidrakhanSpec() => new DragonSpec
        {
            body = new Color(0.2f, 0.24f, 0.3f), dark = new Color(0.1f, 0.11f, 0.15f), belly = new Color(0.42f, 0.46f, 0.53f),
            membrane = new Color(0.34f, 0.38f, 0.52f), spike = new Color(0.12f, 0.13f, 0.17f), bone = new Color(0.78f, 0.78f, 0.82f),
            wyvern = false, tailClub = false, wingSpikes = false, size = 1.3f,
            heads = new[]
            {
                new DragonHeadSpec
                {
                    skin = new Color(0.6f, 0.76f, 0.9f), dark = new Color(0.28f, 0.4f, 0.58f), crest = new Color(0.78f, 0.94f, 1f),
                    horn = new Color(0.86f, 0.96f, 1f), eye = new Color(0.35f, 0.9f, 1f),
                    breathCore = new Color(0.85f, 0.97f, 1f), breathGlow = new Color(0.3f, 0.75f, 1f),
                    ice = true, crystals = true,
                },
                new DragonHeadSpec
                {
                    skin = new Color(0.17f, 0.15f, 0.17f), dark = new Color(0.09f, 0.08f, 0.09f), crest = new Color(1f, 0.55f, 0.15f),
                    horn = new Color(1f, 0.52f, 0.12f), eye = new Color(1f, 0.45f, 0.1f),
                    breathCore = new Color(1f, 0.85f, 0.4f), breathGlow = new Color(1f, 0.4f, 0.08f),
                    bigHorns = true, lava = 0.9f,
                },
            },
        };

        // ------------------------------------------------------------------ versión numérica (otros niveles)
        static FinalBoss SpawnNumeric(Vector3 position, string name)
        {
            GameObject go;
            var template = FindAnyObjectByType<NumberEnemy>();
            if (template != null)
            {
                go = Instantiate(template.gameObject, position, Quaternion.identity);
                foreach (var n in new[] { "HealthBar", "LevelLabel" }) { var c = go.transform.Find(n); if (c != null) Destroy(c.gameObject); }
                var oldLevel = go.GetComponent<EnemyLevel>(); if (oldLevel != null) DestroyImmediate(oldLevel);
                var ne = go.GetComponent<NumberEnemy>(); if (ne != null) Destroy(ne);      // que no lo copie el spawner ni el mapa
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.position = position + Vector3.up;
                go.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Toon(new Color(0.2f, 0.12f, 0.25f));
            }
            go.name = "JefeFinal";
            var boss = go.AddComponent<FinalBoss>();
            boss.bossName = name;
            go.transform.localScale = Vector3.one * boss.scale;
            EnemyAI3D.MakeCombatant(go, boss.maxHealth);
            go.GetComponent<HealthSystem>().SetMaxHealth(boss.maxHealth);
            var el = go.AddComponent<EnemyLevel>();
            el.isBoss = true; el.displayName = name.ToUpperInvariant();
            el.SetLevel(LevelStats.MaxLevel, refill: false);
            return boss;
        }

        // ------------------------------------------------------------------ comportamiento común
        void Start()
        {
            health = GetComponent<HealthSystem>();
            ai = GetComponent<EnemyAI3D>();
            var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform;
            if (ai != null)
            {
                if (kind == Kind.Numeric) { ai.attackDamage = damage; ai.attackCooldown = attackCooldown; ai.attackRange = attackRange; }
                ai.moveSpeed = moveSpeed; ai.detectionRange = wakeRadius + 6f; ai.closeSenseRange = wakeRadius;
                ai.loseRange = wakeRadius + 14f; ai.leashRange = 16f; ai.requireLineOfSight = false;
                ai.enabled = false;                                   // dormido hasta que llegue el mago
            }
            var l = new GameObject("AuraJefe").AddComponent<Light>();
            l.transform.SetParent(transform, false); l.transform.localPosition = Vector3.up * 2f;
            l.type = LightType.Point;
            l.color = kind == Kind.Azhar ? new Color(1f, 0.62f, 0.28f) : kind == Kind.Hidrakhan ? new Color(0.55f, 0.65f, 1f) : new Color(1f, 0.25f, 0.2f);
            l.range = 9f; l.intensity = 2.5f; l.shadows = LightShadows.None;
            health.Died += OnDied;
        }

        void Update()
        {
            if (IsAwake || player == null || ai == null) return;
            Vector3 d = player.position - transform.position; d.y = 0f;
            if (d.magnitude > wakeRadius) return;
            IsAwake = true;
            ai.enabled = true;
            CameraShake3D.Shake(0.3f, 0.7f);
            UIManager.Instance?.ShowMessage($"¡{bossName} despierta!  Derrótalo para completar el nivel", 3.5f);
        }

        void OnDied(HealthSystem _)
        {
            UIManager.Instance?.ShowMessage($"¡Derrotaste a {bossName}!", 4f);
            UIManager.Instance?.SetObjective("");
            var next = LevelProgress.CompleteCurrent();
            string n = bossName;
            DelayedCall.Run(2.5f, () => LevelProgress.ShowLevelComplete(n, next));
        }
    }

    /// Ejecuta una acción tras unos segundos aunque quien la pidió ya se haya destruido (p. ej. el jefe al morir).
    public class DelayedCall : MonoBehaviour
    {
        float at; System.Action action;
        public static void Run(float seconds, System.Action a)
        {
            var d = new GameObject("DelayedCall").AddComponent<DelayedCall>();
            d.at = Time.time + seconds; d.action = a;
        }
        void Update() { if (Time.time >= at) { action?.Invoke(); Destroy(gameObject); } }
    }
}
