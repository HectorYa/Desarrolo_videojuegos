using UnityEngine;

namespace MageHero
{
    /// Temblor de cámara. Se ejecuta DESPUÉS de IsoCameraFollow para sumar el desplazamiento a su posición.
    [DefaultExecutionOrder(1000)]
    public class CameraShake3D : MonoBehaviour
    {
        float amplitude, duration, endTime;

        public static void Shake(float amplitude, float duration)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var s = cam.GetComponent<CameraShake3D>();
            if (s == null) s = cam.gameObject.AddComponent<CameraShake3D>();
            s.amplitude = amplitude; s.duration = Mathf.Max(0.01f, duration); s.endTime = Time.time + duration;
        }

        void LateUpdate()
        {
            float left = endTime - Time.time;
            if (left <= 0f) return;
            transform.position += Random.insideUnitSphere * amplitude * (left / duration);
        }
    }
}
