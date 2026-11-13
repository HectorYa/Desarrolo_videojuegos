using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MageHero
{
    /// Ataque mágico del mago: Espacio, clic izquierdo o botón Oeste del mando.
    /// Instancia un Projectile3D frente al personaje. Con puntería asistida apunta al enemigo
    /// más cercano dentro de un cono (útil con cámara isométrica y terreno con desniveles).
    [RequireComponent(typeof(HealthSystem))]
    public class PlayerCombat3D : MonoBehaviour
    {
        [Header("Proyectil")]
        public Projectile3D projectilePrefab;
        [Tooltip("Punto de salida (p. ej. el cristal del báculo). Si está vacío se usa el pecho del mago.")]
        public Transform firePoint;
        public float fireHeight = 1.0f;
        public float forwardOffset = 0.6f;
        [Min(0.05f)] public float cooldown = 0.35f;
        [Tooltip("Material del proyectil por defecto (solo si no hay prefab).")]
        public Material fallbackMaterial;

        [Header("Puntería asistida")]
        public bool autoAim = true;
        public float autoAimRange = 16f;
        [Range(0f, 180f)] public float autoAimAngle = 40f;
        public bool turnTowardsTarget = true;

        public event System.Action Cast;   // para animaciones / sonido

        InputAction fire;
        HealthSystem health;
        float nextCast;

        void Awake()
        {
            health = GetComponent<HealthSystem>();
            health.team = Team.Player;
            health.destroyOnDeath = false;
            fire = new InputAction("MagicAttack", InputActionType.Button, "<Keyboard>/space");
            fire.AddBinding("<Mouse>/leftButton");
            fire.AddBinding("<Gamepad>/buttonWest");
        }

        void OnEnable() => fire.Enable();
        void OnDisable() => fire.Disable();

        void Update()
        {
            if (health.IsDead || !fire.WasPressedThisFrame() || Time.time < nextCast) return;
            // Un clic sobre un botón del HUD (Mapa, Menú) no debe lanzar hechizos.
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            CastSpell();
        }

        public void CastSpell()
        {
            nextCast = Time.time + cooldown;
            Vector3 origin = firePoint != null ? firePoint.position
                : transform.position + Vector3.up * fireHeight + transform.forward * forwardOffset;
            Vector3 dir = transform.forward;

            var target = autoAim ? FindTarget(origin) : null;
            if (target != null)
            {
                Vector3 aimPoint = target.transform.position + Vector3.up * 0.9f;
                dir = (aimPoint - origin).normalized;
                if (turnTowardsTarget)
                {
                    Vector3 flat = new Vector3(dir.x, 0f, dir.z);
                    if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat);
                }
            }

            var p = projectilePrefab != null
                ? Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir))
                : CreateDefaultProjectile(origin, dir);
            p.Launch(dir, Team.Player);
            Cast?.Invoke();
        }

        HealthSystem FindTarget(Vector3 origin)
        {
            HealthSystem best = null;
            float bestScore = float.MaxValue, cosLimit = Mathf.Cos(autoAimAngle * Mathf.Deg2Rad);
            Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
            foreach (var h in HealthSystem.Alive)
            {
                if (h == null || h.team == Team.Player || h.IsDead) continue;
                Vector3 to = h.transform.position - transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d > autoAimRange || d < 0.01f) continue;
                if (Vector3.Dot(fwd, to / d) < cosLimit) continue;
                if (d < bestScore) { bestScore = d; best = h; }
            }
            return best;
        }

        Projectile3D CreateDefaultProjectile(Vector3 pos, Vector3 dir)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "MagicBolt";
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            go.transform.localScale = Vector3.one * 0.35f;
            if (fallbackMaterial != null) go.GetComponent<Renderer>().sharedMaterial = fallbackMaterial;
            go.AddComponent<Rigidbody>();
            return go.AddComponent<Projectile3D>();
        }
    }
}
