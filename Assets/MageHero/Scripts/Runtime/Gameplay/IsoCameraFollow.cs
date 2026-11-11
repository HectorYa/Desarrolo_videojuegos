using UnityEngine;
using UnityEngine.InputSystem;

namespace MageHero
{
    /// Cámara en perspectiva, tercera persona, cercana y cenital (~54°). Q/E girar, Z/X o rueda: zoom.
    [RequireComponent(typeof(Camera))]
    public class IsoCameraFollow : MonoBehaviour
    {
        public Transform target;
        public float yaw = 0f, pitch = 54.4f, distance = 17f, minDistance = 9f, maxDistance = 48f;
        public float fieldOfView = 46f, followSharpness = 5f, lookHeight = 0.8f, groundClearance = 3f;
        Vector3 focus;
        Camera cam;

        void Start()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
            if (target != null) focus = target.position;
        }

        void LateUpdate()
        {
            if (target == null) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                yaw += ((kb.qKey.isPressed ? 1f : 0f) - (kb.eKey.isPressed ? 1f : 0f)) * 69f * Time.deltaTime;
                distance += ((kb.xKey.isPressed ? 1f : 0f) - (kb.zKey.isPressed ? 1f : 0f)) * 14f * Time.deltaTime;
            }
            if (Mouse.current != null) distance -= Mouse.current.scroll.ReadValue().y * 0.01f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            focus = Vector3.Lerp(focus, target.position, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 look = focus + Vector3.up * lookHeight;
            Vector3 pos = look - rot * Vector3.forward * distance;

            // Evita meterse dentro de montañas: mantiene la cámara por encima del suelo.
            RaycastHit hit;
            int mask = GameLayers.Mask(GameLayers.Environment, GameLayers.Obstacles);
            if (mask != 0 && Physics.Raycast(pos + Vector3.up * 80f, Vector3.down, out hit, 200f, mask, QueryTriggerInteraction.Ignore))
                if (pos.y < hit.point.y + groundClearance) pos.y = hit.point.y + groundClearance;

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        }
    }
}
