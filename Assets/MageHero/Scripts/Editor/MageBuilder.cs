#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MageHero.Editor
{
    /// Construye el prefab del mago (low-poly, misterioso), sus animaciones y su Animator.
    public static class MageBuilder
    {
        public const string PrefabPath = AssetLib.Root + "/Prefabs/Player_Prefab.prefab";
        static readonly string Meshes = AssetLib.Gen + "/Mage";

        static Color32 C(int h) { return MeshBuilder.Col(h); }
        static Matrix4x4 M(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float sx = 1, float sy = 1, float sz = 1)
        { return MeshBuilder.TRS(x, y, z, rx, ry, rz, sx, sy, sz); }

        static GameObject Part(string name, Transform parent, Vector3 pos, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }
        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = pos; return g;
        }

        const int Robe = 0x1D3557, Deep = 0x16294a, Trim = 0x3A86FF, Gold = 0xFFB703, Skin = 0xF4C7A1, Hair = 0xc9d3ea, Dark = 0x1b1b2a, Wood = 0x5b3a24, Boot = 0x2a1d18, Red = 0x8a2f45;

        public static GameObject BuildPrefab()
        {
            AssetLib.EnsureFolder(AssetLib.Root + "/Prefabs");
            var vt = AssetLib.VertexToon();
            var glow = AssetLib.Glow(0x7ff4ff);
            var goldGlow = AssetLib.Glow(0xffd24a);

            var root = new GameObject("Player_Prefab");
            root.tag = "Player";
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.4f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.7f, 0);
            cc.slopeLimit = 45f; cc.stepOffset = 0.3f; cc.skinWidth = 0.03f;
            var pc = root.AddComponent<PlayerController>();
            GameLayers.Set(root, GameLayers.Player);
            root.AddComponent<PlayerSafety>();
            root.AddComponent<WaterSensor>();

            var vis = new GameObject("Visuals"); vis.transform.SetParent(root.transform, false);
            var anim = vis.AddComponent<Animator>(); anim.applyRootMotion = false;
            var pv = vis.AddComponent<PlayerVisuals>();
            var fx = vis.AddComponent<MageFX>();
            var model = new GameObject("Model").transform; model.SetParent(vis.transform, false);

            // ---- Cuerpo (túnica, cabeza, sombrero, libro) ----
            Mesh body = AssetLib.SaveMesh(Meshes, "Body", () =>
            {
                var b = new MeshBuilder();
                b.Cyl(M(0, .245f, 0), 8, .26f, .46f, .95f, C(Robe));
                b.Cyl(M(0, .225f, 0), 8, .47f, .48f, .09f, C(Trim));
                b.Cyl(M(0, .35f, 0), 8, .455f, .465f, .04f, C(Gold));
                b.Cyl(M(0, .80f, 0), 8, .31f, .31f, .08f, C(Gold));
                b.Cyl(M(0, 1.11f, 0), 8, .33f, .33f, .18f, C(Trim));
                b.Box(M(.16f, 1.0f, -.27f, 0, 0, 7f), .14f, .46f, .05f, C(Trim));
                b.Icosa(M(0, 1.42f, 0, 0, 0, 0, 1, .95f, 1), .36f, C(Skin), 1);
                b.Icosa(M(0, 1.47f, -.09f), .40f, C(Hair), 1);
                b.Icosa(M(-.2f, 1.53f, .27f, 0, 0, 0, 1, .8f, .7f), .13f, C(Hair));
                b.Icosa(M(0, 1.50f, .27f, 0, 0, 0, 1, .8f, .7f), .13f, C(Hair));
                b.Icosa(M(.2f, 1.53f, .27f, 0, 0, 0, 1, .8f, .7f), .13f, C(Hair));
                b.Cyl(M(0, 1.60f, 0, 4.6f, 0, 0), 12, .72f, .72f, .04f, C(Deep));
                b.Cyl(M(0, 1.64f, 0), 12, .44f, .47f, .10f, C(Gold));
                b.Box(M(-.38f, .72f, .12f, 0, 20f, 0), .22f, .28f, .07f, C(Red));
                b.Box(M(-.31f, .72f, .16f), .05f, .10f, .08f, C(Gold));
                b.Icosa(M(.37f, .70f, .10f), .10f, C(Wood));
                return b.ToMesh("Body");
            });
            Part("Body", model, Vector3.zero, body, vt);

            // ---- Brillos: ojos, hebillas, runas del sombrero ----
            Mesh eyes = AssetLib.SaveMesh(Meshes, "Eyes", () =>
            {
                var b = new MeshBuilder();
                b.Icosa(M(-.13f, 1.40f, .325f, 0, 0, 0, 1, 1.35f, 1), .05f, C(0x7ff4ff));
                b.Icosa(M(.13f, 1.40f, .325f, 0, 0, 0, 1, 1.35f, 1), .05f, C(0x7ff4ff));
                return b.ToMesh("Eyes");
            });
            Part("Eyes", model, Vector3.zero, eyes, glow);
            Mesh buckles = AssetLib.SaveMesh(Meshes, "Buckles", () =>
            {
                var b = new MeshBuilder();
                b.Box(M(0, .84f, .31f), .11f, .11f, .03f, C(Gold));
                b.Box(M(0, 1.69f, .46f), .14f, .12f, .03f, C(Gold));
                return b.ToMesh("Buckles");
            });
            Part("Buckles", model, Vector3.zero, buckles, goldGlow);

            // ---- Punta del sombrero (2 tramos, movimiento secundario) ----
            var tip1 = Pivot("Hat_Bone_01", model, new Vector3(0, 1.64f, 0));
            Mesh tipA = AssetLib.SaveMesh(Meshes, "HatTip1", () =>
            {
                var b = new MeshBuilder();
                b.Cone(Matrix4x4.identity, 12, .46f, 1.1f, C(Deep));
                return b.ToMesh("HatTip1");
            });
            Part("HatCone1", tip1, Vector3.zero, tipA, vt);
            Mesh moon = AssetLib.SaveMesh(Meshes, "HatMoon", () =>
            {
                var b = new MeshBuilder();
                for (int k = 0; k < 6; k++)   // luna creciente: arco de 6 segmentos
                {
                    float a = -0.5f + k * 0.8f;
                    b.Box(M(Mathf.Cos(a) * .11f, .42f + Mathf.Sin(a) * .11f, .30f, 0, 0, a * Mathf.Rad2Deg), .06f, .045f, .04f, C(0xffd24a));
                }
                b.Octa(M(.03f, .74f, .17f), .05f, C(0xffd24a));
                b.Octa(M(-.13f, .22f, .36f), .045f, C(0xffd24a));
                return b.ToMesh("HatMoon");
            });
            Part("MoonStars", tip1, Vector3.zero, moon, goldGlow);
            var tip2 = Pivot("Hat_Bone_02", tip1, new Vector3(0, 1.0f, 0));
            tip2.localRotation = Quaternion.Euler(0.3f * Mathf.Rad2Deg, 0, 0);
            Mesh tipB = AssetLib.SaveMesh(Meshes, "HatTip2", () =>
            {
                var b = new MeshBuilder();
                b.Cone(Matrix4x4.identity, 10, .17f, .55f, C(Deep));
                return b.ToMesh("HatTip2");
            });
            Part("HatCone2", tip2, Vector3.zero, tipB, vt);
            Mesh orb = AssetLib.SaveMesh(Meshes, "HatOrb", () => { var b = new MeshBuilder(); b.Icosa(M(0, .56f, 0), .06f, C(0xffd24a)); return b.ToMesh("HatOrb"); });
            Part("HatOrb", tip2, Vector3.zero, orb, goldGlow);

            // ---- Capa con borde en zigzag ----
            var cape = Pivot("Cape", model, new Vector3(0, 1.12f, -.3f));
            Mesh capeM = AssetLib.SaveMesh(Meshes, "Cape", () =>
            {
                var b = new MeshBuilder();
                b.Box(M(0, -.2f, 0), .76f, .5f, .05f, C(Deep));
                foreach (float x in new[] { -.285f, -.095f, .095f, .285f }) b.Cone(M(x, -.37f, 0, 0, 0, 180f), 4, .1f, .5f, C(Deep));
                b.Box(M(0, -.43f, .005f), .78f, .04f, .06f, C(Trim));
                return b.ToMesh("Cape");
            });
            Part("CapeMesh", cape, Vector3.zero, capeM, vt);

            // ---- Brazos (mangas acampanadas) y piernas (botas con correa) ----
            Transform armL = null, armR = null;
            foreach (int s in new[] { -1, 1 })
            {
                string side = s < 0 ? "L" : "R";
                var arm = Pivot("Arm_" + side, model, new Vector3(.38f * s, 1.05f, 0));
                Mesh am = AssetLib.SaveMesh(Meshes, "Arm", () =>
                {
                    var b = new MeshBuilder();
                    b.Cyl(M(0, -.45f, 0), 8, .072f, .135f, .45f, C(Robe));
                    b.Cyl(M(0, -.445f, 0), 8, .14f, .14f, .05f, C(Gold));
                    b.Icosa(M(0, -.5f, 0), .08f, C(Skin));
                    return b.ToMesh("Arm");
                });
                Part("ArmMesh", arm, Vector3.zero, am, vt);
                if (s < 0) armL = arm; else armR = arm;

                var leg = Pivot("Leg_" + side, model, new Vector3(.16f * s, .5f, 0));
                Mesh lm = AssetLib.SaveMesh(Meshes, "Leg", () =>
                {
                    var b = new MeshBuilder();
                    b.Cyl(M(0, -.45f, 0), 8, .10f, .09f, .45f, C(Dark));
                    b.Box(M(0, -.45f, .05f), .22f, .14f, .34f, C(Boot));
                    b.Box(M(0, -.40f, .02f), .24f, .03f, .10f, C(Gold));
                    return b.ToMesh("Leg");
                });
                Part("LegMesh", leg, Vector3.zero, lm, vt);
            }

            // ---- Báculo con cristal y runas orbitando ----
            Mesh staff = AssetLib.SaveMesh(Meshes, "Staff", () =>
            {
                var b = new MeshBuilder();
                b.Cyl(M(0, -.95f, .14f), 6, .035f, .045f, 1.8f, C(Wood));
                b.Cyl(M(0, .84f, .14f), 8, .12f, .12f, .04f, C(Gold));
                return b.ToMesh("Staff");
            });
            Part("Staff", armR, Vector3.zero, staff, vt);
            Mesh crystalM = AssetLib.SaveMesh(Meshes, "Crystal", () => { var b = new MeshBuilder(); b.Octa(Matrix4x4.identity, .13f, C(0x7ff4ff)); return b.ToMesh("Crystal"); });
            var crystal = Part("Crystal", armR, new Vector3(0, 1.03f, .14f), crystalM, glow).transform;
            crystal.localScale = new Vector3(1f, 1.5f, 1f);
            var orbit = Pivot("RuneOrbit", armR, new Vector3(0, 1.03f, .14f));

            // ---- Referencias y animación ----
            fx.controller = pc; fx.cape = cape; fx.hatTip1 = tip1; fx.hatTip2 = tip2; fx.crystal = crystal; fx.orbit = orbit;
            var ctrl = BuildAnimator();
            anim.runtimeAnimatorController = ctrl;

            var sens = new GameObject("Sensors"); sens.transform.SetParent(root.transform, false);
            var gs = sens.AddComponent<GroundSensor>();
            Set(pc, "sensor", gs);
            Set(pv, "controller", pc); Set(pv, "sensor", gs); Set(pv, "animator", anim);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Animaciones procedurales: Idle / Walk / Run ----------
        static AnimationCurve Wave(float len, float amp, int cycles, float phase)
        {
            var c = new AnimationCurve();
            const int n = 8;
            for (int i = 0; i <= n; i++) c.AddKey(len * i / n, amp * Mathf.Sin(2f * Mathf.PI * (cycles * i / (float)n + phase)));
            for (int i = 0; i <= n; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        static AnimationClip Clip(string name, float len, float swingDeg, float bob, float leanDeg)
        {
            var clip = new AnimationClip { name = name };
            const string P = "Model/";
            clip.SetCurve(P + "Arm_L", typeof(Transform), "localEulerAnglesRaw.x", Wave(len, swingDeg, 1, 0f));
            clip.SetCurve(P + "Arm_R", typeof(Transform), "localEulerAnglesRaw.x", Wave(len, swingDeg, 1, 0.5f));
            clip.SetCurve(P + "Leg_L", typeof(Transform), "localEulerAnglesRaw.x", Wave(len, swingDeg, 1, 0.5f));
            clip.SetCurve(P + "Leg_R", typeof(Transform), "localEulerAnglesRaw.x", Wave(len, swingDeg, 1, 0f));
            clip.SetCurve("Model", typeof(Transform), "m_LocalPosition.y", Wave(len, bob, 2, 0.25f));
            clip.SetCurve("Model", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(new Keyframe(0, leanDeg), new Keyframe(len, leanDeg)));
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = true; s.loopBlendPositionY = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            AssetLib.EnsureFolder(AssetLib.Gen + "/Animation");
            string path = AssetLib.Gen + "/Animation/" + name + ".anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static AnimatorController BuildAnimator()
        {
            string path = AssetLib.Gen + "/Animation/Mage.controller";
            AssetDatabase.DeleteAsset(path);
            var idle = Clip("Mage_Idle", 2.4f, 2f, 0.03f, 0f);
            var walk = Clip("Mage_Walk", 1.0f, 22f, 0.04f, 3f);
            var run = Clip("Mage_Run", 0.6f, 30f, 0.06f, 6f);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Slope", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            BlendTree tree;
            ctrl.CreateBlendTreeInController("Locomotion", out tree);
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f); tree.AddChild(walk, 0.57f); tree.AddChild(run, 1f);
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }
    }
}
#endif
