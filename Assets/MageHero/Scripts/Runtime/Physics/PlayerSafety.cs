using UnityEngine;
using UnityEngine.Events;

namespace MageHero
{
    /// Vida, última posición segura, penalización por agua, derrota y recuperación ante caídas.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerSafety : MonoBehaviour
    {
        [Min(1)] public int maxHealth = 3;
        public float killY = -30f;
        [SerializeField] float safeRecordInterval = 0.25f;
        [SerializeField] float waterMargin = 3f;
        [SerializeField] float maxSafeSlope = 25f;

        public UnityEvent onPenalty, onDefeated, onRespawn;
        public int Health { get; private set; }

        CharacterController cc;
        PlayerController pc;
        GroundSensor sensor;
        Vector3 lastSafe;
        float nextRecord;
        int waterMask;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            pc = GetComponent<PlayerController>();
            sensor = GetComponentInChildren<GroundSensor>();
            waterMask = GameLayers.Mask(GameLayers.Water);
            Health = maxHealth;
            lastSafe = transform.position;
        }

        void Update()
        {
            if (Time.time >= nextRecord && cc.isGrounded &&
                (sensor == null || sensor.SlopeAngle < maxSafeSlope) &&
                !Physics.CheckSphere(transform.position, waterMargin, waterMask, QueryTriggerInteraction.Collide))
            {
                lastSafe = transform.position;
                nextRecord = Time.time + safeRecordInterval;
            }
            if (transform.position.y < killY) RespawnAtSafe();
        }

        public void OnWater(WaterZone zone)
        {
            if (zone.policy == WaterPolicy.Defeat) { Defeat(); return; }
            Health = Mathf.Max(0, Health - zone.damage);
            onPenalty.Invoke();
            if (Health <= 0) Defeat(); else RespawnAtSafe();
        }

        public void Defeat()
        {
            Health = 0;
            onDefeated.Invoke(); // el GameManager decide: pantalla de derrota, reiniciar nivel, etc.
        }

        public void RespawnAtSafe()
        {
            cc.enabled = false;                       // el CharacterController no permite teletransporte con el componente activo
            transform.position = lastSafe + Vector3.up * 0.1f;
            cc.enabled = true;
            if (pc != null) pc.ResetMotion();
            onRespawn.Invoke();
        }

        public void ResetHealth() => Health = maxHealth;
    }
}
