using UnityEngine;

namespace MageHero
{
    public class GroundSensor : MonoBehaviour
    {
        [SerializeField] float probeRadius = 0.25f;
        [SerializeField] float probeDistance = 0.6f;
        [SerializeField] LayerMask groundMask = ~0;

        public bool IsGrounded { get; private set; }
        public Vector3 Normal { get; private set; } = Vector3.up;
        public float SlopeAngle { get; private set; }

        void Awake()
        {
            // El suelo es solo lo sólido: Environment y Obstacles (nunca agua, triggers ni enemigos).
            if (GameLayers.Exists(GameLayers.Environment) && GameLayers.Exists(GameLayers.Obstacles))
                groundMask = GameLayers.Mask(GameLayers.Environment, GameLayers.Obstacles);
        }

        void FixedUpdate()
        {
            Vector3 origin = transform.position + Vector3.up * (probeRadius + 0.1f);
            if (Physics.SphereCast(origin, probeRadius, Vector3.down, out var hit, probeDistance,
                    groundMask, QueryTriggerInteraction.Ignore))
            {
                IsGrounded = true; Normal = hit.normal;
                SlopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            }
            else { IsGrounded = false; Normal = Vector3.up; SlopeAngle = 0f; }
        }
    }
}
