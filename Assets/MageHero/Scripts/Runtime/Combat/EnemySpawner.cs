using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MageHero
{
    /// Genera enemigos en level_primavera: en puntos estratégicos (hijos/array spawnPoints),
    /// en posiciones aleatorias válidas del terreno, o ambos. Mantiene un máximo de enemigos vivos
    /// y repone bajas cada cierto tiempo.
    /// Si no hay prefabs asignados usa como plantilla los enemigos numéricos que ya existen en la escena.
    public class EnemySpawner : MonoBehaviour
    {
        public enum Mode { SpawnPoints, RandomInArea, Both }

        [Header("Qué generar")]
        public GameObject[] enemyPrefabs;
        [Tooltip("Añade vida + IA + barra a los enemigos que ya están colocados en la escena.")]
        public bool upgradeSceneEnemies = true;
        public float enemyMaxHealth = 60f;

        [Header("Dónde")]
        public Mode mode = Mode.Both;
        public Transform[] spawnPoints;
        [Tooltip("Radio aleatorio alrededor de cada punto de spawn.")]
        public float pointScatter = 3f;
        [Tooltip("Centro = posición de este objeto.")]
        public float areaRadius = 90f;
        public float minDistanceFromPlayer = 14f;
        [Range(0f, 60f)] public float maxGroundSlope = 30f;

        [Header("Cuántos / cuándo")]
        public int spawnOnStart = 6;
        [Tooltip("Máximo de enemigos GENERADOS por este spawner vivos a la vez (no cuenta los que ya estaban en la escena).")]
        public int maxAlive = 10;
        [Tooltip("Segundos entre reposiciones. 0 = no reponer.")]
        public float respawnInterval = 12f;

        readonly List<HealthSystem> spawned = new List<HealthSystem>();
        readonly List<GameObject> templates = new List<GameObject>();
        Transform player, templateRoot;
        int groundMask, blockMask, nextPoint;
        float nextSpawn;

        void Start()
        {
            groundMask = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : Physics.DefaultRaycastLayers;
            blockMask = 0;
            if (GameLayers.Exists(GameLayers.Water)) blockMask |= GameLayers.Mask(GameLayers.Water);
            if (GameLayers.Exists(GameLayers.Obstacles)) blockMask |= GameLayers.Mask(GameLayers.Obstacles);
            var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform;
            if ((spawnPoints == null || spawnPoints.Length == 0) && transform.childCount > 0)
            {
                spawnPoints = new Transform[transform.childCount];
                for (int i = 0; i < transform.childCount; i++) spawnPoints[i] = transform.GetChild(i);
            }

            var sceneEnemies = FindObjectsByType<NumberEnemy>(FindObjectsSortMode.None);
            PrepareTemplates(sceneEnemies);
            if (upgradeSceneEnemies)
                foreach (var e in sceneEnemies) EnemyAI3D.MakeCombatant(e.gameObject, enemyMaxHealth);

            for (int i = 0; i < spawnOnStart; i++) SpawnOne();
            nextSpawn = Time.time + respawnInterval;
        }

        void Update()
        {
            spawned.RemoveAll(h => h == null || h.IsDead);
            if (respawnInterval <= 0f || Time.time < nextSpawn) return;
            nextSpawn = Time.time + respawnInterval;
            if (spawned.Count < maxAlive) SpawnOne();
        }

        void PrepareTemplates(NumberEnemy[] sceneEnemies)
        {
            templateRoot = new GameObject("_EnemyTemplates").transform;
            templateRoot.SetParent(transform, false);
            templateRoot.gameObject.SetActive(false);   // las plantillas nunca se ven ni actúan
            if (enemyPrefabs != null)
                foreach (var prefab in enemyPrefabs) if (prefab != null) templates.Add(prefab);
            if (templates.Count > 0) return;
            var seen = new HashSet<string>();
            foreach (var e in sceneEnemies)
                if (seen.Add(e.symbol)) templates.Add(Instantiate(e.gameObject, templateRoot));
            if (templates.Count == 0) Debug.LogWarning("[MageHero] EnemySpawner: no hay prefabs ni enemigos en escena para clonar.");
        }

        public HealthSystem SpawnOne()
        {
            if (templates.Count == 0 || spawned.Count >= maxAlive) return null;
            if (!TryFindPosition(out Vector3 pos)) return null;

            var template = templates[Random.Range(0, templates.Count)];
            var go = Instantiate(template, pos, Quaternion.Euler(0f, Random.value * 360f, 0f), transform.parent);
            go.name = template.name.Replace("(Clone)", "") + "_Spawned";
            go.SetActive(true);
            var ai = EnemyAI3D.MakeCombatant(go, enemyMaxHealth);
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null && NavMesh.SamplePosition(pos, out NavMeshHit nh, 2f, NavMesh.AllAreas)) agent.Warp(nh.position);
            var hs = ai.GetComponent<HealthSystem>();
            Track(hs);
            return hs;
        }

        void Track(HealthSystem hs)
        {
            if (hs == null || spawned.Contains(hs)) return;
            spawned.Add(hs);
        }

        bool TryFindPosition(out Vector3 result)
        {
            bool usePoints = spawnPoints != null && spawnPoints.Length > 0 && mode != Mode.RandomInArea;
            bool useArea = mode != Mode.SpawnPoints || !usePoints;
            for (int attempt = 0; attempt < 25; attempt++)
            {
                Vector3 guess;
                if (usePoints && (!useArea || Random.value < 0.6f))
                {
                    var sp = spawnPoints[nextPoint++ % spawnPoints.Length];
                    if (sp == null) continue;
                    Vector2 r = Random.insideUnitCircle * pointScatter;
                    guess = sp.position + new Vector3(r.x, 0f, r.y);
                }
                else
                {
                    Vector2 r = Random.insideUnitCircle * areaRadius;
                    guess = transform.position + new Vector3(r.x, 0f, r.y);
                }
                if (IsValid(guess, out result)) return true;
            }
            result = Vector3.zero;
            return false;
        }

        bool IsValid(Vector3 guess, out Vector3 ground)
        {
            ground = guess;
            if (!Physics.Raycast(new Vector3(guess.x, 300f, guess.z), Vector3.down, out RaycastHit hit, 600f,
                                 groundMask | blockMask, QueryTriggerInteraction.Collide)) return false;
            if (((1 << hit.collider.gameObject.layer) & groundMask) == 0) return false;          // cayó sobre agua u obstáculo
            if (Vector3.Angle(hit.normal, Vector3.up) > maxGroundSlope) return false;
            ground = hit.point;
            if (blockMask != 0 && Physics.CheckSphere(ground + Vector3.up * 1f, 1.2f, blockMask, QueryTriggerInteraction.Collide)) return false;
            if (player != null && Vector3.Distance(ground, player.position) < minDistanceFromPlayer) return false;
            foreach (var h in spawned)
                if (h != null && Vector3.Distance(h.transform.position, ground) < 2.5f) return false;
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            if (mode != Mode.SpawnPoints) Gizmos.DrawWireSphere(transform.position, areaRadius);
            var pts = spawnPoints;
            if (pts == null || pts.Length == 0) { pts = new Transform[transform.childCount]; for (int i = 0; i < pts.Length; i++) pts[i] = transform.GetChild(i); }
            foreach (var t in pts) if (t != null && t.name != "_EnemyTemplates") { Gizmos.DrawSphere(t.position, 0.6f); Gizmos.DrawWireSphere(t.position, pointScatter); }
        }
    }
}
