using UnityEngine;

namespace MageHero
{
    /// Genera, a partir del eje del río, (1) un volumen de agua por tramo (capa Water) y
    /// (2) muros de margen (capa Obstacles) con huecos donde hay puentes.
    /// Se puede usar solo el trigger, solo los muros, o ambos (recomendado: ambos).
    public class RiverBuilder : MonoBehaviour
    {
        [Header("Cauce (puntos locales; la Y es la superficie del agua)")]
        public Vector3[] centerline = { new Vector3(0, 0, -15), new Vector3(0, 0, 15) };
        public float width = 8f;
        public float depth = 2.5f;
        public float surfaceMargin = 0.1f;

        [Header("Agua")]
        public WaterPolicy policy = WaterPolicy.Penalty;
        [Min(0)] public int damage = 1;

        [Header("Muros de margen (impiden el paso directo)")]
        public bool buildBankWalls = true;
        public float wallHeight = 5f, wallThickness = 0.6f, wallGap = 0.15f, wallBottom = -1f;
        [Tooltip("Colliders de los puentes: se deja libre el paso de la margen en esa zona.")]
        public Collider[] bridges;
        public float bridgePadding = 0.5f;

        const string RootName = "RiverGenerated";

        [ContextMenu("Build River Colliders")]
        public void Build()
        {
            Clear();
            var root = new GameObject(RootName).transform;
            root.SetParent(transform, false);

            for (int i = 0; i < centerline.Length - 1; i++)
            {
                Vector3 a = transform.TransformPoint(centerline[i]), b = transform.TransformPoint(centerline[i + 1]);
                Vector3 d = b - a; d.y = 0f;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                Vector3 dir = d / len;
                Quaternion rot = Quaternion.LookRotation(dir);
                Vector3 right = rot * Vector3.right;

                AddWater(root, (a + b) * 0.5f, rot, len + 0.2f);

                if (!buildBankWalls) continue;
                int n = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
                float pl = len / n;
                for (int k = 0; k < n; k++)
                {
                    float t = (k + 0.5f) / n;
                    Vector3 c = Vector3.Lerp(a, b, t);
                    foreach (int side in new[] { -1, 1 })
                    {
                        Vector3 p = c + right * side * (width * 0.5f + wallGap + wallThickness * 0.5f);
                        if (InsideBridge(p)) continue;
                        AddWall(root, p, c.y, rot, pl + wallThickness);
                    }
                }
            }
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            var old = transform.Find(RootName);
            if (old == null) return;
            if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
        }

        bool InsideBridge(Vector3 p)
        {
            if (bridges == null) return false;
            foreach (var br in bridges)
            {
                if (br == null) continue;
                Bounds bb = br.bounds;
                if (p.x >= bb.min.x - bridgePadding && p.x <= bb.max.x + bridgePadding &&
                    p.z >= bb.min.z - bridgePadding && p.z <= bb.max.z + bridgePadding) return true;
            }
            return false;
        }

        void AddWater(Transform parent, Vector3 mid, Quaternion rot, float length)
        {
            float top = mid.y + surfaceMargin, bottom = mid.y - depth;
            var go = new GameObject("WaterZone");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(mid.x, (top + bottom) * 0.5f, mid.z), rot);
            var box = go.AddComponent<BoxCollider>();            // el collider va primero: WaterZone lo requiere
            box.size = new Vector3(width, top - bottom, length);
            var zone = go.AddComponent<WaterZone>();
            zone.policy = policy; zone.damage = damage;
            box.isTrigger = policy != WaterPolicy.Block;
            GameLayers.Set(go, GameLayers.Water);
        }

        void AddWall(Transform parent, Vector3 p, float surfaceY, Quaternion rot, float length)
        {
            var go = new GameObject("RiverBankWall");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(p.x, surfaceY + wallBottom + wallHeight * 0.5f, p.z), rot);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(wallThickness, wallHeight, length);
            GameLayers.Set(go, GameLayers.Obstacles);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
            for (int i = 0; i < centerline.Length - 1; i++)
            {
                Vector3 a = transform.TransformPoint(centerline[i]), b = transform.TransformPoint(centerline[i + 1]);
                Gizmos.DrawLine(a, b);
                Vector3 r = Vector3.Cross(Vector3.up, (b - a).normalized) * width * 0.5f;
                Gizmos.DrawLine(a + r, b + r); Gizmos.DrawLine(a - r, b - r);
            }
        }
    }
}
