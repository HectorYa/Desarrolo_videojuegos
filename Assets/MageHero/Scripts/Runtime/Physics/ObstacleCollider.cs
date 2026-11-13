using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    public enum FitMode { Auto, MeshExact, MeshConvex, TrunkCapsule, Box }

    /// Genera colliders AJUSTADOS a la geometría visual de rocas, árboles, montañas, casas, etc.
    /// - Rocas pequeñas: MeshCollider convexo (envolvente). Montañas/acantilados/puentes: MeshCollider exacto (estático).
    /// - Árboles y cactus: cápsula SOLO en el tronco (la copa queda por encima de la cabeza del personaje).
    [DisallowMultipleComponent]
    public class ObstacleCollider : MonoBehaviour
    {
        public FitMode mode = FitMode.Auto;
        [Tooltip("true = suelo transitable (capa Environment). false = obstáculo infranqueable (capa Obstacles).")]
        public bool walkableGround;
        [Tooltip("Malla simplificada opcional para colisión (recomendado en montañas de muchos triángulos).")]
        public Mesh collisionMeshOverride;
        [Tooltip("Hijo con la malla del tronco. Si está vacío se busca un hijo llamado '*trunk*'.")]
        public Transform trunk;
        [Range(0.5f, 1f)] public float trunkFit = 0.95f;
        [SerializeField, HideInInspector] List<Collider> baked = new List<Collider>();

        const MeshColliderCookingOptions Cook = MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.CookForFasterSimulation;

        void Reset() => Bake();

        [ContextMenu("Bake Collider")]
        public void Bake()
        {
            Clear();
            GameLayers.Set(gameObject, walkableGround ? GameLayers.Environment : GameLayers.Obstacles, true);
#if UNITY_EDITOR
            gameObject.isStatic = true;
#endif
            var filters = GetComponentsInChildren<MeshFilter>();
            var m = ResolveMode(filters);

            if (m == FitMode.TrunkCapsule)
            {
                Transform tr = trunk != null ? trunk : FindTrunk();
                MeshFilter tmf = tr != null ? tr.GetComponent<MeshFilter>() : (filters.Length > 0 ? filters[0] : null);
                if (tmf != null && tmf.sharedMesh != null) AddCapsule(tmf, tr != null ? 1f : 0.25f);
                return;
            }

            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                switch (m)
                {
                    case FitMode.Box: AddBox(mf); break;
                    case FitMode.MeshConvex: AddMesh(mf, true); break;
                    default: AddMesh(mf, false); break;
                }
            }
        }

        [ContextMenu("Clear Baked Colliders")]
        public void Clear()
        {
            foreach (var c in baked) if (c != null) DestroySmart(c);
            baked.Clear();
        }

        FitMode ResolveMode(MeshFilter[] filters)
        {
            if (mode != FitMode.Auto) return mode;
            string n = name.ToLowerInvariant();
            foreach (var k in new[] { "tree", "pine", "palm", "cactus", "joshua", "trunk" })
                if (n.Contains(k)) return FitMode.TrunkCapsule;
            int tris = 0; float size = 0f;
            foreach (var mf in filters)
                if (mf.sharedMesh != null) { tris += Triangles(mf.sharedMesh); size = Mathf.Max(size, mf.sharedMesh.bounds.size.magnitude); }
            bool smallRock = (n.Contains("rock") || n.Contains("boulder") || n.Contains("barrel")) && tris <= 250 && size < 8f;
            return smallRock ? FitMode.MeshConvex : FitMode.MeshExact;
        }

        Transform FindTrunk()
        {
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name.ToLowerInvariant().Contains("trunk")) return t;
            return null;
        }

        static int Triangles(Mesh m)
        {
            int n = 0;
            for (int i = 0; i < m.subMeshCount; i++) n += (int)(m.GetIndexCount(i) / 3);
            return n;
        }

        void AddCapsule(MeshFilter mf, float k)
        {
            Bounds b = mf.sharedMesh.bounds;
            var c = mf.gameObject.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.center = b.center;
            c.radius = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.z) * trunkFit * k);
            c.height = Mathf.Max(b.size.y, c.radius * 2f);
            baked.Add(c);
        }

        void AddBox(MeshFilter mf)
        {
            Bounds b = mf.sharedMesh.bounds;
            var c = mf.gameObject.AddComponent<BoxCollider>();
            c.center = b.center; c.size = b.size;
            baked.Add(c);
        }

        void AddMesh(MeshFilter mf, bool convex)
        {
            var c = mf.gameObject.AddComponent<MeshCollider>();
            c.sharedMesh = collisionMeshOverride != null ? collisionMeshOverride : mf.sharedMesh;
            c.convex = convex;
            c.cookingOptions = convex ? Cook : Cook | MeshColliderCookingOptions.UseFastMidphase;
            baked.Add(c);
        }

        static void DestroySmart(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
