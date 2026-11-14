using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MageHero
{
    /// Ataque mágico dirigido con el ratón: al hacer clic se lanza un Raycast desde la cámara principal
    /// hacia la posición del cursor y el hechizo viaja EXACTAMENTE a ese punto del mundo 3D.
    /// Si el clic toca a un enemigo se apunta al centro de su cuerpo. Espacio sigue disparando al frente.
    /// Sustituye a PlayerCombat3D (no deben estar los dos activos).
    [RequireComponent(typeof(HealthSystem))]
    public class ClickAimingCombat3D : MonoBehaviour
    {
        [Header("Proyectil")]
        public Projectile3D projectilePrefab;
        public Transform firePoint;
        public float fireHeight = 1.1f, forwardOffset = 0.5f;
        [Min(0.05f)] public float cooldown = 0.3f;
        public Material fallbackMaterial;

        [Header("Apuntado con ratón")]
        [Tooltip("Capas que el rayo del ratón puede tocar. Vacío = Environment + Obstacles + Enemies + Default.")]
        public LayerMask aimMask;
        public float maxRayDistance = 500f;
        [Tooltip("Altura extra al apuntar a un enemigo (centro del cuerpo).")]
        public float enemyAimHeight = 0.9f;
        public bool turnTowardsTarget = true;
        public bool keyboardFiresForward = true;

        [Header("Marcador de objetivo")]
        public bool showMarker = true;
        public Material markerMaterial;

        public event System.Action<Vector3> Cast;

        HealthSystem health;
        Camera cam;
        float nextCast;

        void Awake()
        {
            health = GetComponent<HealthSystem>();
            health.team = Team.Player;
            health.destroyOnDeath = false;
            if (aimMask.value == 0)
            {
                int m = LayerMask.GetMask("Default");
                foreach (var n in new[] { GameLayers.Environment, GameLayers.Obstacles, GameLayers.Enemies })
                    if (GameLayers.Exists(n)) m |= GameLayers.Mask(n);
                aimMask = m;
            }
            var old = GetComponent<PlayerCombat3D>();
            if (old != null && old.enabled) { old.enabled = false; Debug.Log("[MageHero] ClickAimingCombat3D desactivó PlayerCombat3D."); }
        }

        void Update()
        {
            if (health.IsDead || Time.time < nextCast) return;
            var ui = UIManager.Instance;
            if (ui != null && (ui.IsModalOpen || ui.InCinematic)) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;   // clic sobre el HUD
                if (TryGetAimPoint(mouse.position.ReadValue(), out Vector3 point, out bool ground)) FireAt(point, ground);
                return;
            }

            var kb = Keyboard.current;
            if (keyboardFiresForward && kb != null && kb.spaceKey.wasPressedThisFrame)
                FireAt(Origin() + transform.forward * 20f, false);
        }

        /// Raycast cámara → cursor. Devuelve el punto del mundo al que apuntar.
        public bool TryGetAimPoint(Vector2 screenPos, out Vector3 point, out bool ground)
        {
            point = default; ground = false;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPos);
            var hits = Physics.RaycastAll(ray, maxRayDistance, aimMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;          // ignora al propio mago
                var target = hit.collider.GetComponentInParent<HealthSystem>();
                if (target != null && target.team != Team.Player)
                {
                    point = target.transform.position + Vector3.up * enemyAimHeight;
                    return true;
                }
                point = hit.point; ground = true;
                return true;
            }
            // Sin colisión (cielo): usa el plano horizontal a la altura del disparo.
            var plane = new Plane(Vector3.up, new Vector3(0f, Origin().y, 0f));
            if (plane.Raycast(ray, out float d)) { point = ray.GetPoint(d); return true; }
            return false;
        }

        public void FireAt(Vector3 point, bool showGroundMarker)
        {
            Vector3 origin = Origin();
            Vector3 dir = point - origin;
            if (dir.sqrMagnitude < 0.25f) return;
            dir.Normalize();
            nextCast = Time.time + cooldown;

            if (turnTowardsTarget)
            {
                Vector3 flat = new Vector3(dir.x, 0f, dir.z);
                if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat);
                origin = Origin();                                    // recalcula tras girar
                dir = (point - origin).normalized;
            }

            var p = projectilePrefab != null
                ? Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir))
                : CreateDefaultProjectile(origin, dir);
            p.Launch(dir, Team.Player);
            if (showMarker && showGroundMarker) SpawnMarker(point);
            Cast?.Invoke(point);
        }

        Vector3 Origin()
        {
            return firePoint != null ? firePoint.position
                : transform.position + Vector3.up * fireHeight + transform.forward * forwardOffset;
        }

        void SpawnMarker(Vector3 point)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            m.name = "AimMarker";
            Destroy(m.GetComponent<Collider>());
            m.transform.position = point + Vector3.up * 0.05f;
            m.transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);
            var r = m.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (markerMaterial != null) r.sharedMaterial = markerMaterial;
            m.AddComponent<ShrinkAndDie>();
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

    /// Encoge un objeto hasta desaparecer (marcador de clic).
    public class ShrinkAndDie : MonoBehaviour
    {
        public float life = 0.5f;
        Vector3 start; float t;
        void Start() => start = transform.localScale;
        void Update()
        {
            t += Time.deltaTime / life;
            transform.localScale = new Vector3(start.x * (1f - t), start.y, start.z * (1f - t));
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
