using UnityEngine;

namespace MageHero
{
    /// En el jugador. Consulta cada FixedUpdate si los pies están dentro de un WaterZone
    /// y delega la consecuencia (penalización o derrota) en PlayerSafety.
    [RequireComponent(typeof(CharacterController))]
    public class WaterSensor : MonoBehaviour
    {
        [SerializeField] float footRadius = 0.25f;
        [SerializeField] float footHeight = 0.15f;
        [SerializeField] float cooldown = 1f;

        PlayerSafety safety;
        int waterMask;
        float nextTime;
        readonly Collider[] hits = new Collider[8];

        void Awake()
        {
            safety = GetComponent<PlayerSafety>();
            waterMask = GameLayers.Mask(GameLayers.Water);
            if (waterMask == 0) Debug.LogWarning("[MageHero] La capa Water no existe; el sensor de agua está inactivo.");
        }

        void FixedUpdate()
        {
            if (waterMask == 0 || safety == null || Time.time < nextTime) return;
            Vector3 p = transform.position + Vector3.up * footHeight;
            int n = Physics.OverlapSphereNonAlloc(p, footRadius, hits, waterMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var zone = hits[i].GetComponentInParent<WaterZone>();
                if (zone == null || zone.policy == WaterPolicy.Block) continue;
                nextTime = Time.time + cooldown;
                zone.onPlayerEntered?.Invoke(gameObject);
                safety.OnWater(zone);
                break;
            }
        }
    }
}
