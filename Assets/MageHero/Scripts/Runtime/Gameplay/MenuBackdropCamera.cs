using UnityEngine;

namespace MageHero
{
    /// Cámara del fondo del menú (escena MenuBackdrop): sobrevuela despacio el punto de interés (el río y el
    /// puente), siempre por encima del terreno, y lo encuadra un poco a la derecha porque el panel del menú
    /// ocupa la izquierda.
    public class MenuBackdropCamera : MonoBehaviour
    {
        public Vector3 focus;
        public float radius = 30f, height = 16f, degreesPerSecond = 2.2f, startAngle = 200f, frameOffset = 14f;
        float angle, y;
        bool first = true;

        void Awake()
        {
            // en cuanto aparece, ninguna otra cámara debe ver el bosque (ni un solo fotograma desde el origen)
            var self = GetComponent<Camera>();
            foreach (var c in Camera.allCameras) if (c != self) c.enabled = false;
        }

        void Start() => angle = startAngle;

        void LateUpdate()
        {
            angle += degreesPerSecond * Time.unscaledDeltaTime;
            float a = angle * Mathf.Deg2Rad;
            Vector3 p = focus + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            float want = Mathf.Max(focus.y + height, TerrainY(p) + 10f) + Mathf.Sin(Time.unscaledTime * 0.25f) * 0.6f;
            y = first ? want : Mathf.Lerp(y, want, 1f - Mathf.Exp(-1.5f * Time.unscaledDeltaTime));
            first = false;
            p.y = y;
            transform.position = p;
            // se mira un poco por encima del punto de interés: el río y el puente quedan bajo el título
            Quaternion look = Quaternion.LookRotation(focus + Vector3.up * 7f - p, Vector3.up);
            transform.rotation = look * Quaternion.Euler(0f, -frameOffset, 0f);
        }

        static float TerrainY(Vector3 p)
        {
            int mask = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : Physics.DefaultRaycastLayers;
            return Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out RaycastHit h, 400f, mask, QueryTriggerInteraction.Ignore) ? h.point.y : 0f;
        }
    }
}
