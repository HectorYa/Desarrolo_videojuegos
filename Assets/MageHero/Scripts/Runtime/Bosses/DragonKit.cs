using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Colores y estilo de una cabeza de dragón.
    public class DragonHeadSpec
    {
        public Color skin, dark, crest, horn, eye, breathCore, breathGlow;
        public bool ice;        // dispara esquirlas de hielo en vez de bolas de fuego
        public bool crystals;   // cresta de cristales (hielo) en vez de cuernos curvos
        public bool bigHorns;   // cuernos grandes hacia delante y cuerno en la nariz (fuego)
        public float lava;      // 0..1 grietas de lava brillantes en la piel de la cabeza y el cuello
        public Color lavaColor = new Color(3.2f, 1.05f, 0.22f);
    }

    /// Diseño completo de un dragón.
    public class DragonSpec
    {
        public Color body, dark, belly, membrane, spike, bone;
        public bool wyvern = true;     // alas = patas delanteras (anda sobre 2 patas); false = 4 patas + alas
        public bool tailClub = true;   // maza con púas al final de la cola; si no, aleta en punta
        public bool wingSpikes = true; // púas oscuras a lo largo del antebrazo del ala
        public float size = 1f;
        public DragonHeadSpec[] heads;
    }

    public class DragonHeadRig
    {
        public DragonHeadSpec spec;
        public readonly List<Transform> neck = new List<Transform>();
        public Transform head, jaw, mouth;
    }

    /// Articulaciones del dragón ya construido (las anima DragonBoss).
    public class DragonRig
    {
        public Transform model, torso, wingL, wingR;
        public readonly List<DragonHeadRig> heads = new List<DragonHeadRig>();
        public readonly List<Transform> tail = new List<Transform>();
        public readonly List<Transform> legs = new List<Transform>();   // traseras I, D y (si hay) delanteras I, D
        public readonly List<Material> materials = new List<Material>();
        public float legLength;                                        // del centro del cuerpo al suelo (sin escala)
    }

    /// Construye dragones low poly con mallas facetadas (sin primitivas de Unity):
    /// cuerpo por secciones con placas en el vientre, hileras de púas, alas con dedos de hueso y membrana de dos
    /// caras con borde festoneado, cola articulada (maza o aleta), patas con codo invertido y garras.
    /// La piel usa el shader MageHero/ToonDetail en modo Escamas (el alfa del vértice marca la membrana).
    public static class DragonKit
    {
        const float MembraneAlpha = 0.5f;

        public static DragonRig Build(Transform root, DragonSpec s, float hoverHeight)
        {
            var rig = new DragonRig();
            var skin = DetailMaterials.Scales(4.2f);
            rig.materials.Add(skin);

            rig.model = LowPoly.Pivot("Modelo", root, new Vector3(0f, hoverHeight, 0f));
            rig.model.localScale = Vector3.one * s.size;
            int nHeads = Mathf.Max(1, s.heads != null ? s.heads.Length : 1);

            BuildTorso(rig, s, skin, nHeads);
            for (int h = 0; h < nHeads; h++) BuildNeckAndHead(rig, s, h, nHeads);
            rig.wingL = BuildWing(rig.model, s, -1f, skin);
            rig.wingR = BuildWing(rig.model, s, 1f, skin);
            BuildTail(rig, s, skin);
            BuildLegs(rig, s, skin);
            return rig;
        }

        // ------------------------------------------------------------------ cuerpo
        static void BuildTorso(DragonRig rig, DragonSpec s, Material skin, int nHeads)
        {
            rig.torso = LowPoly.Pivot("Torso", rig.model, Vector3.zero);
            float wide = (nHeads - 1) * 0.2f;
            var c = new[] { new Vector3(0, 0.05f, -1.55f), new Vector3(0, 0.1f, -0.95f), new Vector3(0, 0.15f, -0.2f), new Vector3(0, 0.18f, 0.5f), new Vector3(0, 0.28f, 1.1f), new Vector3(0, 0.42f, 1.55f) };
            var r = new[] { new Vector2(0.42f, 0.4f), new Vector2(0.72f, 0.66f), new Vector2(0.88f, 0.8f), new Vector2(0.84f + wide * 0.5f, 0.78f), new Vector2(0.68f + wide, 0.62f), new Vector2(0.46f + wide, 0.44f) };
            if (!s.wyvern) for (int i = 0; i < r.Length; i++) r[i] *= 1.06f;

            var lp = new LowPoly();
            Loft(lp, c, r, 8, s.body, s.belly, 11, plates: true);
            // Hombros: bultos donde nacen las alas
            foreach (float side in new[] { -1f, 1f })
                lp.Paint(Shade(s.body, 0.95f)).At(new Vector3(0.5f * side, 0.5f, 0.75f), Quaternion.Euler(0, 0, 20f * side), new Vector3(0.5f, 0.38f, 0.6f)).Rock(1f, 7 + (int)side, 0.12f, 1);
            lp.Reset();
            // Hilera de púas por el lomo (más grandes en el centro)
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                Vector3 top = Sample(c, t) + Vector3.up * (SampleR(r, t).y * 0.93f);
                float len = Mathf.Lerp(0.2f, 0.42f, Mathf.Sin(t * Mathf.PI));
                Spike(lp, top, new Vector3(0, 1f, -0.55f), 0.1f, len, s.spike);
            }
            lp.Spawn("Cuerpo", rig.torso, skin);
        }

        // ------------------------------------------------------------------ cuello y cabeza
        static void BuildNeckAndHead(DragonRig rig, DragonSpec s, int index, int count)
        {
            var hs = s.heads != null && s.heads.Length > index ? s.heads[index] : DefaultHead(s);
            var hr = new DragonHeadRig { spec = hs };
            var mat = DetailMaterials.Scales(5f, hs.lava, hs.lava > 0f ? hs.lavaColor : (Color?)null);
            rig.materials.Add(mat);

            float side = count == 1 ? 0f : Mathf.Lerp(-1f, 1f, index / (float)(count - 1));
            Transform parent = LowPoly.Pivot("Cuello" + index, rig.model, new Vector3(side * 0.48f, 0.48f, 1.4f));
            const int segs = 4;
            Vector3 step = new Vector3(side * 0.2f, 0.24f, 0.42f);
            for (int i = 0; i < segs; i++)
            {
                var seg = i == 0 ? parent : LowPoly.Pivot("Vertebra" + i, parent, step);
                float r0 = Mathf.Lerp(0.42f, 0.26f, i / (float)segs), r1 = Mathf.Lerp(0.42f, 0.26f, (i + 1) / (float)segs);
                Color col = Color.Lerp(s.body, hs.skin, (i + 1) / (float)segs);
                Color bel = Color.Lerp(s.belly, Color.Lerp(hs.skin, Color.white, 0.25f), (i + 1) / (float)segs);
                var lp = new LowPoly();
                Loft(lp, new[] { -step * 0.25f, step * 0.5f, step * 1.12f }, new[] { new Vector2(r0, r0 * 0.95f), new Vector2((r0 + r1) * 0.5f, (r0 + r1) * 0.48f), new Vector2(r1, r1 * 0.95f) }, 7, col, bel, 20 + i * 3 + index * 17, plates: true);
                Vector3 top = step * 0.5f + Vector3.up * (r0 + r1) * 0.45f;
                if (hs.crystals) { Crystal(lp, top, new Vector3(0, 1f, -0.4f), 0.07f, 0.34f - i * 0.03f, hs.crest); Crystal(lp, top + new Vector3(0.12f, -0.05f, -0.1f), new Vector3(0.6f, 1f, -0.5f), 0.05f, 0.22f, hs.crest); Crystal(lp, top + new Vector3(-0.12f, -0.05f, -0.1f), new Vector3(-0.6f, 1f, -0.5f), 0.05f, 0.22f, hs.crest); }
                else Spike(lp, top, new Vector3(0, 1f, -0.7f), 0.08f, 0.26f, hs.lava > 0f ? hs.dark : s.spike);
                lp.Spawn("Piel", seg, mat);
                hr.neck.Add(seg);
                parent = seg;
            }

            hr.head = LowPoly.Pivot("Cabeza", parent, step * 1.05f);
            hr.head.localRotation = Quaternion.Euler(18f, 0f, 0f);
            BuildHead(hr, s, hs, mat, index);
            rig.heads.Add(hr);
        }

        static void BuildHead(DragonHeadRig hr, DragonSpec s, DragonHeadSpec hs, Material mat, int seed)
        {
            var lp = new LowPoly();
            Color under = Color.Lerp(hs.skin, Color.white, 0.3f);
            // Cráneo y hocico (mandíbula superior)
            Loft(lp,
                new[] { new Vector3(0, 0.02f, -0.18f), new Vector3(0, 0.05f, 0.18f), new Vector3(0, 0.01f, 0.55f), new Vector3(0, -0.04f, 0.9f), new Vector3(0, -0.07f, 1.1f) },
                new[] { new Vector2(0.3f, 0.28f), new Vector2(0.36f, 0.3f), new Vector2(0.27f, 0.2f), new Vector2(0.18f, 0.13f), new Vector2(0.08f, 0.07f) },
                6, hs.skin, under, 40 + seed);
            // Cejas: arco óseo sobre los ojos
            foreach (float x in new[] { -1f, 1f })
                lp.Paint(Shade(hs.dark, 1.1f)).At(new Vector3(0.2f * x, 0.2f, 0.38f), Quaternion.Euler(-10f, 20f * x, 0f), new Vector3(0.14f, 0.07f, 0.3f)).Rock(1f, 50 + (int)x, 0.1f, 0);
            lp.Reset();
            // Dientes superiores
            for (int i = 0; i < 4; i++)
                foreach (float x in new[] { -1f, 1f })
                {
                    float z = 0.5f + i * 0.14f;
                    float halfW = Mathf.Lerp(0.2f, 0.08f, i / 3f);
                    Spike(lp, new Vector3(halfW * x, -0.12f, z), Vector3.down, 0.025f, 0.11f, s.bone, 3);
                }
            // Cresta / cuernos
            if (hs.crystals)
            {
                var dirs = new[] { new Vector3(0, 1f, -0.5f), new Vector3(0.5f, 1f, -0.6f), new Vector3(-0.5f, 1f, -0.6f), new Vector3(0.9f, 0.6f, -0.7f), new Vector3(-0.9f, 0.6f, -0.7f), new Vector3(1f, 0.1f, -0.6f), new Vector3(-1f, 0.1f, -0.6f) };
                var lens = new[] { 0.62f, 0.52f, 0.52f, 0.45f, 0.45f, 0.36f, 0.36f };
                for (int i = 0; i < dirs.Length; i++)
                    Crystal(lp, new Vector3(dirs[i].x * 0.16f, 0.2f, -0.05f), dirs[i], 0.075f, lens[i], i % 2 == 0 ? hs.crest : Color.Lerp(hs.crest, Color.white, 0.35f));
                // Mejillas en abanico
                foreach (float x in new[] { -1f, 1f })
                    for (int k = 0; k < 3; k++)
                        Crystal(lp, new Vector3(0.28f * x, -0.05f - k * 0.06f, 0.1f - k * 0.08f), new Vector3(x, -0.2f - k * 0.25f, -0.6f), 0.045f, 0.28f - k * 0.04f, hs.crest);
            }
            else if (hs.bigHorns)
            {
                foreach (float x in new[] { -1f, 1f })
                    Horn(lp, new[] { new Vector3(0.2f * x, 0.2f, 0.05f), new Vector3(0.38f * x, 0.48f, 0.05f), new Vector3(0.46f * x, 0.82f, 0.25f), new Vector3(0.4f * x, 1.0f, 0.5f) }, 0.1f, hs.dark, hs.horn);
                Horn(lp, new[] { new Vector3(0, 0.12f, 0.7f), new Vector3(0, 0.3f, 0.78f), new Vector3(0, 0.46f, 0.72f) }, 0.07f, hs.dark, hs.horn);
                // Placas oscuras en la nuca
                for (int k = 0; k < 3; k++) Spike(lp, new Vector3(0, 0.22f - k * 0.04f, -0.12f - k * 0.12f), new Vector3(0, 0.6f, -1f), 0.09f, 0.24f, hs.dark, 4);
            }
            else
            {
                foreach (float x in new[] { -1f, 1f })
                {
                    Horn(lp, new[] { new Vector3(0.17f * x, 0.22f, 0.0f), new Vector3(0.28f * x, 0.36f, -0.3f), new Vector3(0.32f * x, 0.32f, -0.62f), new Vector3(0.26f * x, 0.16f, -0.9f) }, 0.075f, hs.dark, hs.horn);
                    // Púas de la mejilla
                    Spike(lp, new Vector3(0.27f * x, -0.02f, 0.05f), new Vector3(x, -0.1f, -0.8f), 0.04f, 0.22f, hs.horn, 3);
                }
                for (int k = 0; k < 3; k++) Spike(lp, new Vector3(0, 0.3f, 0.3f - k * 0.18f), new Vector3(0, 1f, -0.6f), 0.05f, 0.16f + k * 0.03f, s.spike, 3);
            }
            lp.Spawn("Craneo", hr.head, mat);

            // Ojos brillantes (cuña inclinada)
            var eyes = new LowPoly();
            foreach (float x in new[] { -1f, 1f })
                eyes.Paint(hs.eye).At(new Vector3(0.24f * x, 0.1f, 0.42f), Quaternion.Euler(0f, 25f * x, 12f * x), new Vector3(0.05f, 0.06f, 0.16f)).Box(Vector3.one);
            eyes.Spawn("Ojos", hr.head, RuntimeMaterials.Glow(hs.eye), default, false);

            // Mandíbula inferior (se abre al cargar el aliento)
            hr.jaw = LowPoly.Pivot("Mandibula", hr.head, new Vector3(0f, -0.14f, 0.1f));
            var jaw = new LowPoly();
            Loft(jaw, new[] { new Vector3(0, -0.02f, -0.1f), new Vector3(0, -0.04f, 0.4f), new Vector3(0, -0.02f, 0.8f), new Vector3(0, 0f, 0.95f) },
                new[] { new Vector2(0.24f, 0.09f), new Vector2(0.2f, 0.08f), new Vector2(0.12f, 0.06f), new Vector2(0.05f, 0.04f) }, 6, under, Shade(under, 0.9f), 60 + seed);
            for (int i = 0; i < 3; i++)
                foreach (float x in new[] { -1f, 1f })
                    Spike(jaw, new Vector3(Mathf.Lerp(0.16f, 0.07f, i / 2f) * x, 0.02f, 0.4f + i * 0.15f), Vector3.up, 0.022f, 0.09f, s.bone, 3);
            // Púas bajo la barbilla
            foreach (float x in new[] { -1f, 1f }) Spike(jaw, new Vector3(0.15f * x, -0.08f, 0.05f), new Vector3(0.4f * x, -0.6f, -0.8f), 0.035f, 0.2f, hs.crystals ? hs.crest : s.spike, 3);
            jaw.Spawn("Quijada", hr.jaw, mat);
            // Interior de la boca (brilla un poco: el aliento se ve por dentro)
            var mouthIn = new LowPoly();
            mouthIn.Paint(Color.Lerp(hs.breathGlow, Color.black, 0.55f)).At(new Vector3(0f, -0.1f, 0.5f), Quaternion.identity, new Vector3(0.3f, 0.05f, 0.75f)).Box(Vector3.one);
            mouthIn.Spawn("Boca", hr.head, RuntimeMaterials.Toon(Color.white), default, false);

            hr.mouth = LowPoly.Pivot("Aliento", hr.head, new Vector3(0f, -0.12f, 1.15f));
        }

        // ------------------------------------------------------------------ alas
        static Transform BuildWing(Transform model, DragonSpec s, float side, Material skin)
        {
            var w = LowPoly.Pivot(side < 0 ? "AlaI" : "AlaD", model, new Vector3(0.55f * side, 0.55f, 0.75f));
            w.localScale = Vector3.one * (s.wyvern ? 1.15f : 1f);

            Vector3 sh = Vector3.zero;
            Vector3 elbow = new Vector3(1.25f * side, 0.35f, -0.15f);
            Vector3 wrist = new Vector3(2.4f * side, 0.75f, 0.25f);
            Vector3[] tips = { new Vector3(4.5f * side, 0.55f, -0.35f), new Vector3(4.15f * side, 0.05f, -1.55f), new Vector3(3.1f * side, -0.15f, -2.35f) };
            Vector3 rear = new Vector3(0.35f * side, -0.2f, -1.5f);

            var lp = new LowPoly();
            // Membrana: abanico desde la muñeca con borde festoneado entre los dedos
            var rim = new List<Vector3>();
            for (int i = 0; i < tips.Length; i++)
            {
                rim.Add(tips[i]);
                Vector3 next = i + 1 < tips.Length ? tips[i + 1] : rear;
                Vector3 mid = Vector3.Lerp(tips[i], next, 0.5f);
                rim.Add(mid + (wrist - mid) * (i + 1 < tips.Length ? 0.2f : 0.16f));
            }
            rim.Add(rear);
            Color memb = s.membrane; memb.a = MembraneAlpha;
            for (int i = 0; i + 1 < rim.Count; i++)
                TwoSided(lp, wrist, rim[i], rim[i + 1], Shade(memb, i % 2 == 0 ? 1f : 0.94f));
            TwoSided(lp, wrist, rear, elbow, memb);
            TwoSided(lp, elbow, rear, sh, Shade(memb, 0.96f));

            // Huesos del brazo y de los dedos
            Color boneCol = Color.Lerp(s.body, s.dark, 0.25f);
            Loft(lp, new[] { sh - (elbow - sh).normalized * 0.15f, elbow, wrist }, new[] { new Vector2(0.17f, 0.15f), new Vector2(0.13f, 0.12f), new Vector2(0.1f, 0.09f) }, 6, boneCol, Shade(boneCol, 0.85f), 70 + (int)side);
            for (int i = 0; i < tips.Length; i++)
            {
                Vector3 mid = Vector3.Lerp(wrist, tips[i], 0.5f) + Vector3.up * 0.12f;
                Loft(lp, new[] { wrist, mid, tips[i] }, new[] { new Vector2(0.075f, 0.07f), new Vector2(0.05f, 0.045f), new Vector2(0.014f, 0.014f) }, 5, boneCol, Shade(boneCol, 0.85f), 80 + i);
            }
            // Garra del pulgar en la muñeca
            Spike(lp, wrist + new Vector3(0, 0.05f, 0.05f), new Vector3(0.3f * side, -0.3f, 1f), 0.05f, 0.32f, s.bone, 4);
            // Púas oscuras sobre el antebrazo
            if (s.wingSpikes)
                for (int i = 0; i < 6; i++)
                {
                    Vector3 p = Vector3.Lerp(elbow, wrist, (i + 0.5f) / 6f) + Vector3.up * 0.09f;
                    Spike(lp, p, new Vector3(0.15f * side, 1f, -0.9f), 0.06f, 0.26f - i * 0.015f, s.spike, 4);
                }
            lp.Spawn("Ala", w, skin);
            return w;
        }

        // ------------------------------------------------------------------ cola
        static void BuildTail(DragonRig rig, DragonSpec s, Material skin)
        {
            Transform parent = LowPoly.Pivot("Cola", rig.model, new Vector3(0f, 0.05f, -1.45f));
            const int segs = 8;
            const float len = 0.55f;
            for (int i = 0; i < segs; i++)
            {
                var seg = i == 0 ? parent : LowPoly.Pivot("Cola" + i, parent, new Vector3(0f, -0.02f, -len));
                float r0 = Mathf.Lerp(0.42f, 0.08f, i / (float)segs), r1 = Mathf.Lerp(0.42f, 0.08f, (i + 1) / (float)segs);
                var lp = new LowPoly();
                Loft(lp, new[] { new Vector3(0, 0, 0.12f), new Vector3(0, -0.01f, -len * 0.5f), new Vector3(0, -0.02f, -len - 0.04f) },
                    new[] { new Vector2(r0, r0 * 0.92f), new Vector2((r0 + r1) * 0.5f, (r0 + r1) * 0.46f), new Vector2(r1, r1 * 0.92f) }, 7, s.body, s.belly, 90 + i, plates: true);
                Spike(lp, new Vector3(0, r0 * 0.85f, -len * 0.4f), new Vector3(0, 1f, -0.8f), Mathf.Max(0.04f, r0 * 0.25f), Mathf.Lerp(0.3f, 0.18f, i / (float)segs), s.spike, 4);
                if (s.tailClub && i >= segs - 3)
                    foreach (float x in new[] { -1f, 1f })
                        Spike(lp, new Vector3(r0 * 0.8f * x, 0.02f, -len * 0.4f), new Vector3(x, 0.15f, -0.5f), 0.05f, 0.3f, s.spike, 4);
                lp.Spawn("Piel", seg, skin);
                rig.tail.Add(seg);
                parent = seg;
            }
            var tip = new LowPoly();
            if (s.tailClub)
            {
                tip.Paint(Shade(s.dark, 1.1f)).At(new Vector3(0, 0, -len - 0.2f), Quaternion.identity, new Vector3(0.28f, 0.24f, 0.36f)).Rock(1f, 99, 0.18f, 1);
                tip.Reset();
                var dirs = new[] { Vector3.up, Vector3.left, Vector3.right, new Vector3(0.7f, 0.7f, 0), new Vector3(-0.7f, 0.7f, 0), new Vector3(0, -0.6f, 0), Vector3.back };
                foreach (var d in dirs) Spike(tip, new Vector3(0, 0, -len - 0.2f) + d * 0.2f, d + Vector3.back * 0.6f, 0.06f, 0.34f, s.spike, 4);
            }
            else
            {
                Color fin = s.membrane; fin.a = MembraneAlpha;
                Vector3 b = new Vector3(0, 0, -len + 0.1f), e = new Vector3(0, 0, -len - 0.9f);
                TwoSided(tip, b, new Vector3(0.38f, 0, -len - 0.3f), e, fin);
                TwoSided(tip, b, e, new Vector3(-0.38f, 0, -len - 0.3f), fin);
            }
            tip.Spawn("Punta", parent, skin);
        }

        // ------------------------------------------------------------------ patas
        static void BuildLegs(DragonRig rig, DragonSpec s, Material skin)
        {
            rig.legLength = 1.22f;
            foreach (float x in new[] { -1f, 1f })
                rig.legs.Add(Leg(rig.model, s, skin, new Vector3(0.55f * x, -0.25f, -0.7f), x, false));
            if (!s.wyvern)
                foreach (float x in new[] { -1f, 1f })
                    rig.legs.Add(Leg(rig.model, s, skin, new Vector3(0.52f * x, -0.22f, 0.95f), x, true));
        }

        static Transform Leg(Transform model, DragonSpec s, Material skin, Vector3 hip, float side, bool front)
        {
            var leg = LowPoly.Pivot(front ? "PataDelantera" : "PataTrasera", model, hip);
            var lp = new LowPoly();
            Vector3[] pts = front
                ? new[] { new Vector3(0, 0.05f, 0), new Vector3(0, -0.55f, -0.2f), new Vector3(0, -1.0f, 0.05f), new Vector3(0, -1.16f, 0.16f) }
                : new[] { new Vector3(0, 0.08f, 0), new Vector3(0, -0.5f, 0.38f), new Vector3(0, -0.95f, -0.12f), new Vector3(0, -1.16f, 0.08f) };
            Vector2[] rad = front
                ? new[] { new Vector2(0.27f, 0.3f), new Vector2(0.17f, 0.18f), new Vector2(0.11f, 0.11f), new Vector2(0.1f, 0.08f) }
                : new[] { new Vector2(0.31f, 0.38f), new Vector2(0.2f, 0.22f), new Vector2(0.12f, 0.12f), new Vector2(0.1f, 0.08f) };
            Loft(lp, pts, rad, 6, s.body, Shade(s.body, 0.85f), 120 + (int)side + (front ? 5 : 0));
            Vector3 foot = pts[pts.Length - 1];
            for (int k = -1; k <= 1; k++)
                Spike(lp, foot + new Vector3(k * 0.07f, -0.02f, 0.04f), new Vector3(k * 0.35f, -0.25f, 1f), 0.05f, 0.3f, s.bone, 4);
            Spike(lp, foot + new Vector3(0, 0, -0.05f), new Vector3(0, -0.2f, -1f), 0.04f, 0.18f, s.bone, 4);
            lp.Spawn("Pata", leg, skin);
            return leg;
        }

        // ------------------------------------------------------------------ mallas
        /// Tubo facetado que pasa por los centros 'c' con radios elípticos 'r' (x = ancho, y = alto).
        /// Las caras de abajo usan el color del vientre; con 'plates' el vientre alterna tonos (placas).
        static void Loft(LowPoly lp, IList<Vector3> c, IList<Vector2> r, int sides, Color top, Color belly, int seed, bool plates = false)
        {
            int n = c.Count;
            var rings = new Vector3[n, sides];
            for (int i = 0; i < n; i++)
            {
                Vector3 f = c[Mathf.Min(i + 1, n - 1)] - c[Mathf.Max(i - 1, 0)];
                if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;
                f.Normalize();
                Vector3 refUp = Mathf.Abs(Vector3.Dot(f, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                Vector3 right = Vector3.Cross(refUp, f).normalized;
                Vector3 up = Vector3.Cross(f, right);
                for (int k = 0; k < sides; k++)
                {
                    float a = (k + 0.5f) / sides * Mathf.PI * 2f;
                    rings[i, k] = c[i] + right * (Mathf.Cos(a) * r[i].x) + up * (Mathf.Sin(a) * r[i].y);
                }
            }
            for (int i = 0; i + 1 < n; i++)
                for (int k = 0; k < sides; k++)
                {
                    int k1 = (k + 1) % sides;
                    float am = (k + 1f) / sides * Mathf.PI * 2f;
                    bool isBelly = Mathf.Sin(am) < -0.35f;
                    Color col = isBelly ? belly : top;
                    if (isBelly && plates && i % 2 == 1) col = Shade(col, 0.9f);
                    lp.Paint(Shade(col, Jitter(seed, i, k)));
                    lp.Quad(rings[i, k], rings[i, k1], rings[i + 1, k1], rings[i + 1, k]);
                }
            lp.Paint(Shade(top, 0.9f));
            for (int k = 0; k < sides; k++)
            {
                int k1 = (k + 1) % sides;
                lp.Tri(c[0], rings[0, k1], rings[0, k]);
                lp.Tri(c[n - 1], rings[n - 1, k], rings[n - 1, k1]);
            }
        }

        /// Púa (pirámide) que sale de 'basePos' en la dirección 'dir'.
        static void Spike(LowPoly lp, Vector3 basePos, Vector3 dir, float radius, float length, Color col, int sides = 4)
        {
            lp.Paint(col).At(basePos, Quaternion.FromToRotation(Vector3.up, dir.normalized), Vector3.one).Prism(sides, radius, 0f, length, true);
            lp.Reset();
        }

        /// Cristal de hielo: prisma de 5 caras con punta (más largo y facetado que una púa).
        static void Crystal(LowPoly lp, Vector3 basePos, Vector3 dir, float radius, float length, Color col)
        {
            var rot = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            lp.Paint(col).At(basePos, rot, Vector3.one).Prism(5, radius, radius * 0.8f, length * 0.65f, false, 0.2f);
            lp.Paint(Color.Lerp(col, Color.white, 0.3f)).At(basePos + rot * Vector3.up * (length * 0.65f), rot, Vector3.one).Prism(5, radius * 0.8f, 0f, length * 0.35f, false, 0.2f);
            lp.Reset();
        }

        /// Cuerno curvo: tubo que se afina hasta la punta y cambia del color de la base al de la punta.
        static void Horn(LowPoly lp, Vector3[] pts, float radius, Color baseCol, Color tipCol)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                float t0 = i / (float)(pts.Length - 1), t1 = (i + 1) / (float)(pts.Length - 1);
                float r0 = Mathf.Lerp(radius, 0.012f, t0), r1 = Mathf.Lerp(radius, 0.012f, t1);
                Color col = Color.Lerp(baseCol, tipCol, (t0 + t1) * 0.5f + 0.15f);
                Loft(lp, new[] { pts[i], pts[i + 1] }, new[] { new Vector2(r0, r0), new Vector2(r1, r1) }, 5, col, col, 140 + i);
            }
        }

        static void TwoSided(LowPoly lp, Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            lp.Paint(col);
            lp.Tri(a, b, c);
            lp.Tri(a, c, b);
        }

        static Vector3 Sample(Vector3[] c, float t)
        {
            float f = t * (c.Length - 1); int i = Mathf.Min((int)f, c.Length - 2);
            return Vector3.Lerp(c[i], c[i + 1], f - i);
        }

        static Vector2 SampleR(Vector2[] r, float t)
        {
            float f = t * (r.Length - 1); int i = Mathf.Min((int)f, r.Length - 2);
            return Vector2.Lerp(r[i], r[i + 1], f - i);
        }

        /// Variación pequeña y estable de tono por cara (da vida al facetado).
        static float Jitter(int seed, int i, int k)
        {
            unchecked
            {
                int h = seed * 73856093 ^ i * 19349663 ^ k * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                return 0.93f + ((h & 0xffff) / 65535f) * 0.12f;
            }
        }

        static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        static DragonHeadSpec DefaultHead(DragonSpec s) => new DragonHeadSpec
        {
            skin = s.body, dark = s.dark, crest = s.spike, horn = s.bone, eye = new Color(1f, 0.7f, 0.2f),
            breathCore = new Color(1f, 0.9f, 0.5f), breathGlow = new Color(1f, 0.5f, 0.12f),
        };
    }
}
