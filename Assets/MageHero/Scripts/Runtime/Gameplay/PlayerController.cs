using UnityEngine;
using UnityEngine.InputSystem;

namespace MageHero
{
    /// Movimiento del mago con CharacterController. Camina por defecto; Shift = correr.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] float walkSpeed = 2.4f;
        [SerializeField] float runSpeed = 4.2f;
        [SerializeField] float acceleration = 9f;
        [SerializeField] float turnSpeed = 540f;

        [Header("Gravedad / Pendientes")]
        [SerializeField] float gravity = -25f;
        [SerializeField] float stickToGroundForce = -4f;
        [SerializeField] float slideSpeed = 6f;

        [Header("Referencias")]
        [SerializeField] GroundSensor sensor;
        [SerializeField] Transform cameraTransform;

        InputAction move, runAction;
        CharacterController cc;
        Vector3 planarVelocity;
        float verticalVelocity;

        public float NormalizedSpeed { get; private set; }   // 0..1 (caminar ~0.57, correr 1)
        public bool IsGrounded { get { return cc.isGrounded || (sensor != null && sensor.IsGrounded); } }
        public float SlopeAngle { get { return sensor != null ? sensor.SlopeAngle : 0f; } }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            runAction = new InputAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
            runAction.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable() { move.Enable(); runAction.Enable(); }
        void OnDisable() { move.Disable(); runAction.Disable(); }

        void Update()
        {
            Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            float speed = input.sqrMagnitude < 0.01f ? 0f : (runAction.IsPressed() ? runSpeed : walkSpeed) * Mathf.Max(0.4f, input.magnitude);

            Vector3 wish = CameraRelative(input);
            if (sensor != null && sensor.IsGrounded) wish = Vector3.ProjectOnPlane(wish, sensor.Normal).normalized;

            planarVelocity = Vector3.MoveTowards(planarVelocity, wish * speed, acceleration * Time.deltaTime);

            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = stickToGroundForce;
            else verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = planarVelocity;
            velocity.y += verticalVelocity;

            if (sensor != null && sensor.IsGrounded && sensor.SlopeAngle > cc.slopeLimit)
                velocity += Vector3.ProjectOnPlane(Vector3.down, sensor.Normal).normalized * slideSpeed;

            cc.Move(velocity * Time.deltaTime);

            Vector3 flat = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
            if (flat.sqrMagnitude > 0.05f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);

            NormalizedSpeed = Mathf.Clamp01(flat.magnitude / runSpeed);
        }

        /// Llamado por PlayerSafety tras un respawn.
        public void ResetMotion() { planarVelocity = Vector3.zero; verticalVelocity = 0f; }

        Vector3 CameraRelative(Vector2 input)
        {
            if (cameraTransform == null) return new Vector3(input.x, 0f, input.y);
            Vector3 f = cameraTransform.forward; f.y = 0f; f.Normalize();
            Vector3 r = cameraTransform.right; r.y = 0f; r.Normalize();
            return f * input.y + r * input.x;
        }
    }
}
