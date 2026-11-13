using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Auditoría de colliders: detecta geometría sólida sin collider y configuraciones peligrosas.
    public static class ColliderAudit
    {
        public static List<string> Run()
        {
            var issues = new List<string>();
            int obs = GameLayers.Index(GameLayers.Obstacles), env = GameLayers.Index(GameLayers.Environment), water = GameLayers.Index(GameLayers.Water);

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                int l = r.gameObject.layer;
                if (l != obs && l != env) continue;
                bool has = r.GetComponentInParent<Collider>() != null || r.GetComponentInChildren<Collider>() != null;
                bool designed = r.GetComponentInParent<ObstacleCollider>() != null; // p.ej. copa de árbol: sin collider a propósito
                if (!has && !designed) issues.Add($"SIN COLLIDER: '{Path(r.transform)}' está en una capa sólida pero se puede atravesar.");
            }

            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                int l = c.gameObject.layer;
                if ((l == obs || l == env) && c.isTrigger) issues.Add($"TRIGGER EN CAPA SÓLIDA: '{Path(c.transform)}' no bloquea el paso.");
                if (c is MeshCollider mc && !mc.convex && c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)
                    issues.Add($"MESHCOLLIDER NO CONVEXO CON RIGIDBODY DINÁMICO: '{Path(c.transform)}'.");
                if (l == water && !c.isTrigger && c.GetComponentInParent<WaterZone>() == null)
                    issues.Add($"AGUA SIN WaterZone: '{Path(c.transform)}'.");
                if (l == 0 && c.GetComponentInParent<CharacterController>() == null && c.GetComponentInParent<Camera>() == null)
                    issues.Add($"COLLIDER EN CAPA Default: '{Path(c.transform)}' (asígnale Environment u Obstacles).");
            }
            return issues;
        }

        static string Path(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
