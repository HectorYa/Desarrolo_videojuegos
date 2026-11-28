using UnityEngine;

namespace MageHero
{
    /// Materiales con detalle procedural (shader MageHero/ToonDetail) para piedra y madera.
    /// La plantilla vive en Resources/MageToonDetail.mat para que el shader entre en la build.
    public static class DetailMaterials
    {
        static Material template, sharedStone;

        static Material Template()
        {
            if (template != null) return template;
            template = Resources.Load<Material>("MageToonDetail");
            if (template == null)
            {
                var sh = Shader.Find("MageHero/ToonDetail");
                template = sh != null ? new Material(sh) : IntroSet.VertexToon();
            }
            return template;
        }

        /// Material de piedra propio (cada gólem tiene el suyo: destello y daño independientes).
        public static Material Stone(float detailScale = 2.6f, float moss = 0.3f)
        {
            var m = new Material(Template()) { name = "piedra_detalle" };
            m.SetFloat("_Mode", 0f);
            m.SetFloat("_DetailScale", detailScale);
            m.SetFloat("_DetailStrength", 0.6f);
            m.SetFloat("_MossAmount", moss);
            m.SetFloat("_Damage", 0f);
            return m;
        }

        /// Piedra compartida para escombros y esquirlas (no destella).
        public static Material SharedStone() => sharedStone != null ? sharedStone : (sharedStone = Stone(3f, 0.15f));

        /// Madera con fibra, nudos y la parte cercana al agua mojada y con musgo.
        public static Material Wood(float wetLevel, float moss = 0.4f)
        {
            var m = new Material(Template()) { name = "madera_detalle" };
            m.SetFloat("_Mode", 1f);
            m.SetFloat("_DetailScale", 2.2f);
            m.SetFloat("_DetailStrength", 0.7f);
            m.SetFloat("_MossAmount", moss);
            m.SetFloat("_WetLevel", wetLevel);
            m.SetFloat("_RimAmount", 0.2f);
            m.SetColor("_MossColor", new Color(0.3f, 0.42f, 0.18f));
            return m;
        }
    }

    /// Un trozo suelto (esquirla de roca, astilla, tablón) con física sencilla: cae girando, rebota en el
    /// suelo perdiendo energía, se queda quieto y al final se hunde en el suelo y desaparece.
    public class Debris : MonoBehaviour
    {
        Vector3 vel, spin;
        float life, age, bounce = 0.35f, radius, ground = float.NaN, lastProbe = -1f;
        bool resting;

        public static Debris Spawn(GameObject go, Vector3 velocity, Vector3 angularDeg, float life = 4f, float radius = 0.1f)
        {
            var d = go.AddComponent<Debris>();
            d.vel = velocity; d.spin = angularDeg; d.life = life; d.radius = radius;
            return d;
        }

        /// Esquirla de roca facetada nueva.
        public static Debris Chip(Vector3 at, Vector3 velocity, float size, Color color, float life = 3.5f)
        {
            var lp = new LowPoly().Paint(color);
            lp.Rock(size, Random.Range(0, 99999), 0.35f, 0);
            var go = lp.Spawn("Esquirla", null, DetailMaterials.SharedStone(), at, size > 0.12f);
            go.transform.rotation = Random.rotation;
            return Spawn(go, velocity, Random.insideUnitSphere * 540f, life, size);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            Vector3 p = transform.position;
            if (!resting && (age - lastProbe > 0.05f || float.IsNaN(ground))) { ground = TerrainY(p); lastProbe = age; }
            if (!resting)
            {
                vel += Physics.gravity * dt;
                p += vel * dt;
                transform.Rotate(spin * dt, Space.World);
                if (p.y - radius < ground)
                {
                    p.y = ground + radius;
                    if (Mathf.Abs(vel.y) < 1.2f) { resting = true; vel = Vector3.zero; }
                    else
                    {
                        vel.y = -vel.y * bounce;
                        vel.x *= 0.55f; vel.z *= 0.55f;
                        spin *= 0.6f;
                    }
                }
            }
            if (age > life - 1f) p.y -= dt * (radius * 2f + 0.25f);     // se hunde en el suelo antes de desaparecer
            transform.position = p;
            if (age > life) Destroy(gameObject);
        }

        /// Altura del terreno (solo la capa Environment: los volúmenes de agua son muros altos y no cuentan).
        static float TerrainY(Vector3 p)
        {
            int mask = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : Physics.DefaultRaycastLayers;
            return Physics.Raycast(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, out RaycastHit h, 60f, mask, QueryTriggerInteraction.Ignore) ? h.point.y : p.y - 50f;
        }
    }
}
