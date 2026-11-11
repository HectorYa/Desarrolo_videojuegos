using UnityEngine;

namespace MageHero
{
    public class PlayerVisuals : MonoBehaviour
    {
        [SerializeField] PlayerController controller;
        [SerializeField] GroundSensor sensor;
        [SerializeField] Animator animator;
        [SerializeField, Range(0f, 1f)] float slopeTiltAmount = 0.5f;
        [SerializeField] float tiltSmoothing = 10f;

        static readonly int SpeedHash    = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("IsGrounded");
        static readonly int SlopeHash    = Animator.StringToHash("Slope");
        float slope01;

        void Reset() => animator = GetComponent<Animator>();
        void Awake() { if (animator) animator.applyRootMotion = false; }

        void Update()
        {
            animator.SetFloat(SpeedHash, controller.NormalizedSpeed, 0.1f, Time.deltaTime);
            animator.SetBool(GroundedHash, controller.IsGrounded);
            float t = controller.IsGrounded ? Mathf.InverseLerp(5f, 40f, sensor.SlopeAngle) : 0f;
            slope01 = Mathf.MoveTowards(slope01, t, 4f * Time.deltaTime);
            animator.SetFloat(SlopeHash, slope01);

            Vector3 up = Vector3.Slerp(Vector3.up, sensor.Normal, slopeTiltAmount);
            Quaternion target = Quaternion.FromToRotation(transform.parent.up, up) * transform.parent.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, tiltSmoothing * Time.deltaTime);
        }
    }
}
