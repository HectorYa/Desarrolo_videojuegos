using UnityEngine;

namespace MageHero
{
    /// Perímetro de seguridad INVISIBLE colocado detrás de las montañas (capa MapBounds).
    /// Las montañas son la barrera visible; esto solo garantiza que nada escape si hay un hueco.
    public class MapBoundary : MonoBehaviour
    {
        public Bounds area = new Bounds(Vector3.zero, new Vector3(200f, 60f, 200f));
        public float thickness = 20f, height = 80f;
        const string RootName = "MapBoundGenerated";

        [ContextMenu("Build Boundary")]
        public void Build()
        {
            Clear();
            var root = new GameObject(RootName).transform;
            root.SetParent(transform, false);
            Vector3 c = area.center, e = area.extents;
            Wall(root, "Wall+X", new Vector3(c.x + e.x + thickness * 0.5f, c.y, c.z), new Vector3(thickness, height, area.size.z + thickness * 2f));
            Wall(root, "Wall-X", new Vector3(c.x - e.x - thickness * 0.5f, c.y, c.z), new Vector3(thickness, height, area.size.z + thickness * 2f));
            Wall(root, "Wall+Z", new Vector3(c.x, c.y, c.z + e.z + thickness * 0.5f), new Vector3(area.size.x + thickness * 2f, height, thickness));
            Wall(root, "Wall-Z", new Vector3(c.x, c.y, c.z - e.z - thickness * 0.5f), new Vector3(area.size.x + thickness * 2f, height, thickness));
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            var old = transform.Find(RootName);
            if (old == null) return;
            if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
        }

        static void Wall(Transform parent, string n, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = size;
            GameLayers.Set(go, GameLayers.MapBounds);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(area.center, area.size);
        }
    }
}
