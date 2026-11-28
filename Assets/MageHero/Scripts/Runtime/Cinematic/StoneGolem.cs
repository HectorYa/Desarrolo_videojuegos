using System;
using System.Collections;
using UnityEngine;

namespace MageHero
{
    /// Gólem de Piedra (sirviente del Gran Cero): modelo y animación compartidos por la cinemática de apertura
    /// y por el jefe del Nivel 1 (GolemBoss).
    /// Diseño (Diseños/Enemigo/Golem.png): low poly robusto y asimétrico —un brazo derecho gigante hecho de
    /// segmentos de roca con un puño enorme y un brazo izquierdo normal—, postura encorvada con la cabeza hundida
    /// en el torso, ojos y boca encendidos, piedra gris facetada y fisuras de lava naranja entre las placas.
    /// En el pecho arde la operación que hay que resolver.
    /// Animación procedural "con peso": piernas con rodilla (se dobla al dar el paso y el pie no patina),
    /// la cadera baja en cada pisada y se desplaza sobre la pierna de apoyo, los brazos siguen con inercia
    /// (resortes) y en reposo respira y cambia el peso de pierna.
    /// Poses de combate: agacharse a arrancar una roca, sostenerla sobre la cabeza, lanzarla, golpear el suelo,
    /// rugir, tambalearse, curarse y enrollarse en forma de rueda.
    public class StoneGolem : MonoBehaviour
    {
        public string operation = "8 + 5";
        public int answer = 13;

        // Articulaciones
        Transform body, hips, torso, head, legL, legR, kneeL, kneeR, shoulderL, shoulderR, elbowL, elbowR, fistL, fistR;
        Transform wheel;                                   // forma de rueda (oculta salvo al rodar)
        Material eyeMat, crackMat, chestMat, rockMat;
        // Detalle "realista": destello y retroceso al recibir golpes, daño progresivo (venas de lava que se abren
        // y trozos de roca que se desprenden), pisadas con polvo y guijarros que se le caen al andar.
        float flash, hitKick, damage, damageTarget, prevStepCos, nextPebble;
        int brokenChunks;
        static readonly (float at, string name)[] BreakOrder = { (0.3f, "Hombrera_R2"), (0.55f, "Joroba2"), (0.8f, "Pomulo_L") };
        Light coreLight;
        GlowNumber chestText;
        ParticleSystem embers;

        // Estado de animación
        float walk, walkTarget, phase, eyes, eyesTarget, cracks = 1f, cracksTarget = 1f;
        float armsUp, armsUpTarget, stompLeg, stompTarget, hunch, hunchTarget, roar, roarTarget;
        float crouch, crouchTarget, hold, holdTarget, stagger, heal, healTarget, curl, curlTarget;
        Vector3 lookTarget; bool hasLook;
        float headYaw, headPitch, idleT;
        // inercia de los brazos (resorte: ángulo y velocidad)
        float armLagL, armLagLv, armLagR, armLagRv;
        Vector3 lastPos; float planarSpeed;

        public float Height => 4.4f;
        public bool Busy { get; private set; }
        public Transform Head => head;
        public Transform Chest => torso;
        public Transform FistL => fistL;
        public Transform FistR => fistR;
        public Vector3 HandsCenter => (fistL.position + fistR.position) * 0.5f;
        public bool IsWheel => curl > 0.5f;

        static readonly Color StoneA = MenuStyle.Hex(0x8e8881), StoneB = MenuStyle.Hex(0x6c6760), StoneC = MenuStyle.Hex(0xb4ada3);
        static readonly Color Belly = MenuStyle.Hex(0x4c4743);          // partes en sombra (vientre, piernas)
        const float BaseHunch = 14f;                                     // postura encorvada en reposo (grados)
        static readonly Color Lava = new Color(3.2f, 1.05f, 0.22f), Eye = new Color(4.5f, 1.6f, 0.3f);
        static readonly int FlashId = Shader.PropertyToID("_Flash"), DamageId = Shader.PropertyToID("_Damage"),
                            VeinGlowId = Shader.PropertyToID("_VeinGlow"), VeinColorId = Shader.PropertyToID("_VeinColor");

        public static StoneGolem Create(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Golem_DePiedra");
            go.transform.SetPositionAndRotation(position, rotation);
            var g = go.AddComponent<StoneGolem>();
            g.BuildModel();
            return g;
        }

        /// Construye el modelo sobre un objeto existente (lo usa el jefe GolemBoss, que ya tiene vida y collider).
        public void BuildOn(GameObject root) { if (body == null) BuildModel(); }

        // ------------------------------------------------------------------ modelo
        void BuildModel()
        {
            crackMat = CineGlow.Make(Lava, 0f, 1f, 0.35f);
            eyeMat = CineGlow.Make(Eye, 0f, 1f, 0.1f);
            rockMat = DetailMaterials.Stone(2.6f, 0.18f);                       // piedra moteada con musgo y venas de lava
            var rock = rockMat;

            body = LowPoly.Pivot("Cuerpo", transform, Vector3.zero);
            hips = LowPoly.Pivot("Cadera", body, new Vector3(0, 1.6f, 0));
            Chunk(hips, "Pelvis", Vector3.zero, new Vector3(1.3f, 0.62f, 0.9f), 1, Belly, rock);
            Crack(hips, new Vector3(0, 0.22f, 0.3f), new Vector3(0.702f, 0.035f, 0.234f), 6f);

            // Piernas cortas y oscuras (en sombra bajo el torso), con rodilla: muslo y espinilla + pie.
            legL = LowPoly.Pivot("Pierna_L", hips, new Vector3(-0.52f, -0.12f, 0));
            legR = LowPoly.Pivot("Pierna_R", hips, new Vector3(0.52f, -0.12f, 0));
            foreach (var (leg, s) in new[] { (legL, 2), (legR, 3) })
            {
                Chunk(leg, "Muslo", new Vector3(0, -0.36f, 0), new Vector3(0.82f, 0.82f, 0.8f), 10 + s, Belly, rock);
                var knee = LowPoly.Pivot("Rodilla", leg, new Vector3(0, -0.72f, 0.02f));
                Chunk(knee, "Espinilla", new Vector3(0, -0.3f, 0), new Vector3(0.72f, 0.66f, 0.72f), 15 + s, StoneB, rock);
                Chunk(knee, "Pie", new Vector3(0, -0.62f, 0.16f), new Vector3(1.0f, 0.42f, 1.25f), 20 + s, StoneB, rock);
                Crack(knee, new Vector3(0, 0.02f, 0.22f), new Vector3(0.39f, 0.035f, 0.296f), 12f);
                if (leg == legL) kneeL = knee; else kneeR = knee;
            }

            // Torso: gran masa encorvada. Pecho ancho, joroba alta en la espalda y vientre oscuro.
            torso = LowPoly.Pivot("Torso", hips, new Vector3(0, 0.25f, 0));
            Chunk(torso, "Vientre", new Vector3(0, 0.35f, 0.12f), new Vector3(1.35f, 0.85f, 1.05f), 30, Belly, rock);
            Chunk(torso, "Pecho", new Vector3(0, 1.02f, 0.12f), new Vector3(2.05f, 1.45f, 1.4f), 31, StoneA, rock);
            Chunk(torso, "Joroba", new Vector3(0.1f, 1.7f, -0.4f), new Vector3(2.25f, 1.4f, 1.45f), 32, StoneC, rock);
            Chunk(torso, "Joroba2", new Vector3(-0.55f, 1.9f, -0.25f), new Vector3(1.0f, 0.75f, 0.9f), 35, StoneA, rock);
            // Hombrera derecha enorme (sostiene el brazo gigante) y una pequeña a la izquierda
            Chunk(torso, "Hombrera_R", new Vector3(1.2f, 1.78f, 0.02f), new Vector3(1.3f, 1.0f, 1.2f), 34, StoneC, rock);
            Chunk(torso, "Hombrera_R2", new Vector3(1.45f, 1.35f, 0.25f), new Vector3(0.8f, 0.7f, 0.8f), 36, StoneA, rock);
            Chunk(torso, "Hombrera_L", new Vector3(-1.05f, 1.55f, 0.02f), new Vector3(0.75f, 0.6f, 0.75f), 33, StoneC, rock);

            // Fisuras de energía: venas de lava entre las placas de piedra (pecho, costados, espalda)
            Crack(torso, new Vector3(0, 0.66f, 0.72f), new Vector3(0.936f, 0.035f, 0.12f), 4f);
            Crack(torso, new Vector3(-0.5f, 1.35f, 0.74f), new Vector3(0.035f, 0.585f, 0.12f), 28f);
            Crack(torso, new Vector3(0.55f, 1.3f, 0.74f), new Vector3(0.035f, 0.507f, 0.12f), -32f);
            Crack(torso, new Vector3(-0.85f, 0.95f, 0.45f), new Vector3(0.035f, 0.507f, 0.234f), -14f);
            Crack(torso, new Vector3(0.9f, 1.1f, 0.4f), new Vector3(0.035f, 0.429f, 0.234f), 16f);
            Crack(torso, new Vector3(0.1f, 1.95f, 0.15f), new Vector3(0.858f, 0.035f, 0.39f), -6f);
            Crack(torso, new Vector3(0.95f, 1.6f, 0.35f), new Vector3(0.39f, 0.035f, 0.39f), 22f);
            Crack(torso, new Vector3(-0.2f, 1.45f, -0.95f), new Vector3(0.78f, 0.035f, 0.12f), 10f);

            // Grieta ancha del pecho, donde arde la operación (llave para vencerlo)
            var plate = new LowPoly().Paint(Color.white);
            plate.At(new Vector3(0, 1.08f, 0.8f), Quaternion.Euler(-4, 0, 3), Vector3.one).Box(new Vector3(0.95f, 0.36f, 0.03f));
            plate.At(new Vector3(-0.42f, 1.18f, 0.79f), Quaternion.Euler(-4, 0, 28), Vector3.one).Box(new Vector3(0.35f, 0.2f, 0.03f));
            plate.At(new Vector3(0.4f, 0.98f, 0.79f), Quaternion.Euler(-4, 0, -22), Vector3.one).Box(new Vector3(0.32f, 0.18f, 0.03f));
            chestMat = CineGlow.Make(new Color(0.9f, 0.18f, 0.04f), 0f, 1f, 0.25f);
            plate.Spawn("PlacaLava", torso, chestMat, default, false);
            chestText = CineGlow.Number(operation, torso, new Vector3(0, 1.08f, 0.86f), new Color(5f, 2.6f, 0.9f), 0.36f, false);
            chestText.transform.localRotation = Quaternion.Euler(-4, 180, 0);   // TextMesh se lee mirando a -Z: lo giramos hacia delante
            chestText.Pulse = 0.2f;
            chestText.FadeTo(1f, 10f);

            // Cabeza hundida en la parte delantera del torso (sin cuello): cráneo, ceja pesada,
            // ojos de magma y una boca dentada que brilla desde dentro.
            head = LowPoly.Pivot("Cabeza", torso, new Vector3(0, 1.62f, 0.66f));
            Chunk(head, "Craneo", new Vector3(0, 0.12f, -0.05f), new Vector3(0.9f, 0.7f, 0.75f), 41, StoneA, rock);
            Chunk(head, "Ceja", new Vector3(0, 0.34f, 0.2f), new Vector3(0.95f, 0.24f, 0.4f), 42, StoneC, rock);
            Chunk(head, "Pomulo_L", new Vector3(-0.33f, 0.05f, 0.22f), new Vector3(0.3f, 0.3f, 0.3f), 44, StoneB, rock);
            Chunk(head, "Pomulo_R", new Vector3(0.33f, 0.05f, 0.22f), new Vector3(0.3f, 0.3f, 0.3f), 45, StoneB, rock);
            Chunk(head, "Mandibula", new Vector3(0, -0.2f, 0.16f), new Vector3(0.72f, 0.26f, 0.45f), 43, StoneB, rock);
            foreach (float x in new[] { -0.19f, 0.19f })
            {
                var e = new LowPoly().Paint(Color.white);
                e.At(new Vector3(x, 0.2f, 0.36f), Quaternion.Euler(0, 0, x > 0 ? -12f : 12f), new Vector3(1.7f, 0.75f, 0.6f)).Rock(0.075f, 5, 0f, 0);
                e.Spawn("Ojo", head, eyeMat, default, false);
            }
            // Boca: hueco encendido con dientes de piedra triangulares arriba y abajo
            var mouth = new LowPoly().Paint(Color.white);
            mouth.At(new Vector3(0, -0.07f, 0.36f)).Box(new Vector3(0.46f, 0.13f, 0.04f));
            mouth.Spawn("Boca", head, eyeMat, default, false);
            var teeth = new LowPoly().Paint(StoneC);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.18f + i * 0.09f;
                teeth.At(new Vector3(x, 0.0f, 0.385f), Quaternion.Euler(180, 0, 0), Vector3.one).Prism(3, 0.035f, 0f, 0.07f);
                teeth.At(new Vector3(x + 0.045f, -0.14f, 0.385f), Quaternion.identity, Vector3.one).Prism(3, 0.032f, 0f, 0.06f);
            }
            teeth.Spawn("Dientes", head, rock, default, false);
            Crack(head, new Vector3(-0.28f, 0.33f, 0.3f), new Vector3(0.035f, 0.12f, 0.06f), 30f);

            // Brazo DERECHO gigante: segmentos de roca apilados con lava entre ellos y un puño enorme.
            shoulderR = LowPoly.Pivot("Hombro_R", torso, new Vector3(1.32f, 1.62f, 0.05f));
            Chunk(shoulderR, "Brazo1", new Vector3(0.12f, -0.22f, 0), new Vector3(0.95f, 0.75f, 0.9f), 51, StoneA, rock);
            Chunk(shoulderR, "Brazo2", new Vector3(0.18f, -0.68f, 0.04f), new Vector3(0.9f, 0.7f, 0.88f), 52, StoneC, rock);
            Crack(shoulderR, new Vector3(0.15f, -0.45f, 0.05f), new Vector3(0.585f, 0.035f, 0.585f), 6f);
            elbowR = LowPoly.Pivot("Codo", shoulderR, new Vector3(0.2f, -1.02f, 0));
            Crack(elbowR, new Vector3(0, 0.02f, 0.02f), new Vector3(0.624f, 0.035f, 0.624f), -4f);
            Chunk(elbowR, "Antebrazo1", new Vector3(0, -0.28f, 0.05f), new Vector3(1.0f, 0.7f, 0.95f), 61, StoneA, rock);
            Chunk(elbowR, "Antebrazo2", new Vector3(0.04f, -0.72f, 0.08f), new Vector3(1.08f, 0.72f, 1.02f), 62, StoneC, rock);
            Crack(elbowR, new Vector3(0.02f, -0.5f, 0.07f), new Vector3(0.702f, 0.035f, 0.702f), 8f);
            fistR = LowPoly.Pivot("Puno", elbowR, new Vector3(0.05f, -1.15f, 0.14f));
            Chunk(fistR, "PunoMesh", new Vector3(0, -0.05f, 0), new Vector3(1.35f, 1.0f, 1.25f), 71, StoneC, rock);
            Chunk(fistR, "Nudillos", new Vector3(0, -0.12f, 0.42f), new Vector3(1.15f, 0.55f, 0.55f), 72, StoneA, rock);
            Chunk(fistR, "PunoLado", new Vector3(-0.5f, 0.05f, 0.1f), new Vector3(0.55f, 0.75f, 0.8f), 73, StoneB, rock);
            Crack(fistR, new Vector3(0, 0.2f, 0.2f), new Vector3(0.78f, 0.035f, 0.468f), -10f);
            Crack(fistR, new Vector3(0.2f, -0.15f, 0.56f), new Vector3(0.035f, 0.312f, 0.1f), 20f);
            Crack(fistR, new Vector3(-0.22f, -0.12f, 0.56f), new Vector3(0.035f, 0.273f, 0.1f), -15f);

            // Brazo IZQUIERDO normal (más delgado), con un puño pequeño
            shoulderL = LowPoly.Pivot("Hombro_L", torso, new Vector3(-1.12f, 1.5f, 0));
            Chunk(shoulderL, "Brazo", new Vector3(-0.08f, -0.42f, 0), new Vector3(0.55f, 0.85f, 0.55f), 49, StoneB, rock);
            elbowL = LowPoly.Pivot("Codo", shoulderL, new Vector3(-0.1f, -0.9f, 0));
            Chunk(elbowL, "Antebrazo", new Vector3(0, -0.38f, 0.05f), new Vector3(0.55f, 0.8f, 0.55f), 59, StoneA, rock);
            Crack(elbowL, new Vector3(0, 0.0f, 0.05f), new Vector3(0.312f, 0.035f, 0.312f), 0f);
            fistL = LowPoly.Pivot("Puno", elbowL, new Vector3(0, -0.92f, 0.08f));
            Chunk(fistL, "PunoMesh", Vector3.zero, new Vector3(0.72f, 0.66f, 0.72f), 69, StoneC, rock);

            BuildWheel(rock);

            // Luz de lava (ilumina el suelo y las flores) y brasas que suben
            var lg = new GameObject("LuzLava"); lg.transform.SetParent(torso, false); lg.transform.localPosition = new Vector3(0, 1.1f, 1.4f);
            coreLight = lg.AddComponent<Light>(); coreLight.type = LightType.Point; coreLight.color = new Color(1f, 0.45f, 0.12f); coreLight.range = 6f; coreLight.intensity = 2.2f;
            coreLight.shadows = LightShadows.None;
            embers = CineGlow.Motes("Brasas", torso, new Vector3(0, 1.0f, 0), new Color(3f, 0.9f, 0.2f), 9f, 0.12f, 0.6f, 2.2f, 0.9f, -0.12f);

            SetEyes(0f, true);
            lastPos = transform.position;
        }

        /// Forma de rueda: el gólem enrollado sobre sí mismo (anillo de rocas con grietas de lava y los ojos en el centro).
        void BuildWheel(Material rock)
        {
            wheel = LowPoly.Pivot("Rueda", transform, new Vector3(0, 1.55f, 0));
            var spin = LowPoly.Pivot("Giro", wheel, Vector3.zero);
            const float R = 1.55f;
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Vector3 p = new Vector3(0, Mathf.Cos(a) * R * 0.78f, Mathf.Sin(a) * R * 0.78f);
                Chunk(spin, "Segmento", p, new Vector3(1.45f, 0.95f, 0.95f), 100 + i, i % 3 == 0 ? StoneC : (i % 2 == 0 ? StoneA : StoneB), rock);
            }
            Chunk(spin, "Nucleo", Vector3.zero, new Vector3(1.3f, 1.6f, 1.6f), 120, StoneB, rock);
            var cr = new LowPoly().Paint(Color.white);
            for (int i = 0; i < 6; i++)
                cr.At(Vector3.zero, Quaternion.Euler(i * 30f, 0, 0), Vector3.one).Box(new Vector3(1.5f, 0.06f, R * 1.9f));
            cr.Spawn("GrietasRueda", spin, crackMat, default, false);
            wheel.gameObject.SetActive(false);
        }

        void Chunk(Transform parent, string name, Vector3 pos, Vector3 scale, int seed, Color c, Material mat)
        {
            var lp = new LowPoly().Paint(c);
            lp.At(pos, Quaternion.Euler(seed * 13 % 30 - 15, seed * 47 % 360, seed * 7 % 20 - 10), scale).Rock(0.5f, seed, 0.2f);
            lp.Paint(Color.Lerp(c, Color.black, 0.15f));
            lp.At(pos + new Vector3(scale.x * 0.3f, scale.y * 0.25f, scale.z * 0.28f), Quaternion.Euler(0, seed * 31, 0), scale * 0.35f).Rock(0.5f, seed + 100, 0.25f, 0);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = lp.Build(name);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        void Crack(Transform parent, Vector3 pos, Vector3 size, float roll)
        {
            var lp = new LowPoly().Paint(Color.white);
            lp.At(pos, Quaternion.Euler(0, 0, roll), Vector3.one).Box(size);
            lp.Spawn("Grieta", parent, crackMat, default, false);
        }

        // ------------------------------------------------------------------ control
        public void SetWalk(float speed01) => walkTarget = Mathf.Clamp01(speed01);
        public void SetEyes(float k, bool instant = false) { eyesTarget = k; if (instant) eyes = k; }
        public void SetCracks(float k) => cracksTarget = k;
        public void SetHunch(float k) => hunchTarget = k;
        public void LookAt(Vector3 world) { lookTarget = world; hasLook = true; }
        public void ClearLook() => hasLook = false;
        public void ShowOperation(bool on) => chestText.FadeTo(on ? 1f : 0f, 3f);
        public void SetOperationText(string text) { chestText.Text = text; }
        /// Agacharse (0 = de pie, 1 = en cuclillas con los puños en el suelo).
        public void SetCrouch(float k) => crouchTarget = k;
        /// Sostener algo con los dos puños sobre la cabeza (0..1).
        public void SetHold(float k) => holdTarget = k;
        public void SetArmsUp(float k) => armsUpTarget = k;
        public void SetHeal(float k) => healTarget = k;

        /// Un paso de persecución por fotograma: avanza "a tirones" sincronizado con las pisadas (sin patinar).
        public void StepToward(Vector3 target, float speed, float turnDegPerSec = 75f)
        {
            Vector3 d = target - transform.position; d.y = 0;
            if (d.magnitude < 0.05f) { SetWalk(0f); return; }
            SetWalk(1f);
            Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnDegPerSec * Time.deltaTime);
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, d.normalized));           // no avanza de lado
            float stepPush = StepPush;
            Vector3 p = transform.position + transform.forward * Mathf.Min(d.magnitude, speed * stepPush * facing * walk * Time.deltaTime);
            p.y = GroundY(p, transform.position.y);
            transform.position = p;
        }

        /// 0.4 en el contacto del pie, ~1.4 a mitad del paso: el cuerpo avanza cuando la pierna empuja.
        float StepPush => 0.4f + Mathf.Abs(Mathf.Sin(phase)) * 1.6f;

        /// Camina hasta un punto pegado al suelo, girando hacia él. Pasos pesados.
        public IEnumerator WalkTo(Vector3 target, float speed = 1.3f)
        {
            Busy = true;
            SetWalk(1f);
            while (true)
            {
                Vector3 d = target - transform.position; d.y = 0;
                if (d.magnitude < 0.08f) break;
                StepToward(target, speed, 70f);
                yield return null;
            }
            SetWalk(0f);
            Busy = false;
        }

        /// Pisotón: levanta la pierna derecha y la deja caer.
        public IEnumerator Stomp(float raise = 0.55f, float down = 0.12f)
        {
            Busy = true;
            stompTarget = 1f;
            yield return new WaitForSeconds(raise);
            stompTarget = -0.15f;
            yield return new WaitForSeconds(down);
            CameraShake3D.Shake(0.35f, 0.28f);
            DustPuff(kneeR.position + transform.forward * 0.3f + Vector3.down * 0.7f, 26);
            GolemAudio.Play("golem_golpe", kneeR.position, 0.8f, 1.1f);
            stompTarget = 0f;
            Busy = false;
        }

        /// Golpe con los dos puños contra el suelo. <paramref name="onImpact"/> se llama en el instante del golpe.
        public IEnumerator Smash(Action<Vector3> onImpact = null, float windup = 0.55f)
        {
            Busy = true;
            armsUpTarget = 1f; hunchTarget = -0.2f;
            yield return new WaitForSeconds(windup);
            armsUpTarget = -0.45f; hunchTarget = 0.9f; crouchTarget = 0.35f;
            yield return new WaitForSeconds(0.16f);
            Vector3 hit = transform.position + transform.forward * 1.9f;
            hit.y = GroundY(hit, transform.position.y);
            CameraShake3D.Shake(0.4f, 0.3f);
            DustPuff(hit, 30);
            GolemAudio.Play("golem_golpe", hit, 1f);
            onImpact?.Invoke(hit);
            yield return new WaitForSeconds(0.45f);
            armsUpTarget = 0f; hunchTarget = 0f; crouchTarget = 0f;
            yield return new WaitForSeconds(0.25f);
            Busy = false;
        }

        /// Rugido: echa la cabeza atrás, abre los brazos y las grietas se avivan.
        public IEnumerator Roar(float seconds = 1.4f)
        {
            Busy = true;
            roarTarget = 1f; cracksTarget = 2.2f;
            embers.Emit(30);
            Vector3 mouth = head != null ? head.position : transform.position + Vector3.up * 3f;
            GolemAudio.Play("golem_rugido", mouth, 1f, Mathf.Lerp(1.05f, 0.9f, Mathf.InverseLerp(0.8f, 1.6f, seconds)));
            MageAudio.PlayBossRoar(mouth, 0.62f);                              // capa de rugido: más grave = más pesado
            CameraShake3D.Shake(0.12f, seconds * 0.8f);
            yield return new WaitForSeconds(seconds);
            roarTarget = 0f; cracksTarget = 1f;
            Busy = false;
        }

        /// Tambaleo al recibir un golpe fuerte (o al romperse su coraza).
        public void Stagger(float k = 1f) { stagger = Mathf.Max(stagger, k); }

        // ------------------------------------------------------------------ golpes y daño
        /// Impacto en un punto del cuerpo: destello, retroceso, esquirlas de roca que saltan y polvo.
        /// strength ≈ 0.5 (golpe leve) … 1.5 (golpe muy fuerte).
        public void Hit(Vector3 point, float strength = 1f)
        {
            if (body == null) return;
            strength = Mathf.Clamp(strength, 0.3f, 2f);
            flash = 1f;
            hitKick = Mathf.Max(hitKick, 0.6f * strength);
            Stagger(0.25f * strength);
            Vector3 outDir = point - (torso != null ? torso.position : transform.position); outDir.y = 0f;
            outDir = outDir.sqrMagnitude > 0.01f ? outDir.normalized : transform.forward;
            int n = Mathf.RoundToInt(Mathf.Lerp(3f, 9f, strength / 1.5f));
            for (int i = 0; i < n; i++)
            {
                Color c = i % 3 == 0 ? StoneC : (i % 2 == 0 ? StoneA : StoneB);
                Vector3 v = outDir * UnityEngine.Random.Range(1.5f, 4f) + Vector3.up * UnityEngine.Random.Range(2f, 4.5f) + UnityEngine.Random.insideUnitSphere * 1.5f;
                Debris.Chip(point + UnityEngine.Random.insideUnitSphere * 0.2f, v, UnityEngine.Random.Range(0.05f, 0.15f), c, UnityEngine.Random.Range(2.5f, 4f));
            }
            DustPuff(point, Mathf.RoundToInt(8 * strength));
            if (embers != null) embers.Emit(Mathf.RoundToInt(6 * strength));
            GolemAudio.Play("golem_impacto", point, Mathf.Lerp(0.6f, 1f, strength / 1.5f), 1.05f, 0.08f);
        }

        /// Impacto en un punto del pecho (de frente), para quien no sabe dónde golpeó el hechizo.
        public void Hit(float strength = 1f)
        {
            if (torso == null) return;
            Hit(torso.TransformPoint(new Vector3(UnityEngine.Random.Range(-0.7f, 0.7f), UnityEngine.Random.Range(0.6f, 1.6f), 0.8f)), strength);
        }

        /// Daño acumulado (0 = intacto, 1 = a punto de caer). Las venas de lava se abren poco a poco y, al pasar
        /// ciertos umbrales, se desprende un trozo de roca dejando una herida de lava. Si se cura, las venas se cierran
        /// (los trozos perdidos no vuelven: quedan las cicatrices).
        public void SetDamage(float k)
        {
            damageTarget = Mathf.Clamp01(k);
            while (brokenChunks < BreakOrder.Length && damageTarget >= BreakOrder[brokenChunks].at)
                BreakChunk(BreakOrder[brokenChunks++].name);
        }

        void BreakChunk(string chunkName)
        {
            var t = FindDeep(transform, chunkName);
            if (t == null) return;
            var parent = t.parent;
            Vector3 center = Recenter(t.gameObject);
            // herida: grietas de lava cruzadas donde estaba la roca
            var w = new LowPoly().Paint(Color.white);
            for (int i = 0; i < 3; i++) w.At(center, Quaternion.Euler(UnityEngine.Random.Range(0, 180f), i * 60f, UnityEngine.Random.Range(0, 180f)), Vector3.one).Box(new Vector3(0.5f, 0.05f, 0.05f));
            w.Spawn("Herida", parent, crackMat, default, false);
            // la roca se suelta, sale despedida y queda en el suelo
            Vector3 outDir = t.position - transform.position; outDir.y = 0f; outDir = outDir.sqrMagnitude > 0.01f ? outDir.normalized : -transform.forward;
            t.SetParent(null, true);
            var r = t.GetComponent<MeshRenderer>(); if (r != null) r.sharedMaterial = DetailMaterials.SharedStone();
            Debris.Spawn(t.gameObject, outDir * 2.5f + Vector3.up * 3.5f, UnityEngine.Random.insideUnitSphere * 200f, 7f, 0.35f);
            DustPuff(t.position, 22);
            if (embers != null) embers.Emit(20);
            GolemAudio.Play("golem_quiebre", t.position, 1f);
            CineGlow.Burst(t.position, new Color(3f, 1.1f, 0.25f), 26, 2.4f, 0.1f, 0.8f, 0.5f);
            CameraShake3D.Shake(0.15f, 0.25f);
            Stagger(0.8f);
        }

        /// Mueve el pivote de una pieza al centro de su malla (las rocas se construyen desplazadas desde la articulación).
        static Vector3 Recenter(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return go.transform.localPosition;
            var mesh = mf.sharedMesh;
            Vector3 c = mesh.bounds.center;
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] -= c;
            mesh.vertices = v; mesh.RecalculateBounds();
            go.transform.localPosition += go.transform.localRotation * Vector3.Scale(c, go.transform.localScale);
            return go.transform.localPosition;
        }

        static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = FindDeep(c, n); if (r != null) return r; }
            return null;
        }

        /// Pisada: polvo bajo el pie, algún guijarro y un leve temblor si la cámara está cerca.
        void FootDown(Transform knee)
        {
            Vector3 foot = knee.TransformPoint(new Vector3(0, -0.8f, 0.16f));
            if (RuntimeMaterials.Ground(foot + Vector3.up, out Vector3 g)) foot.y = Mathf.Max(g.y, foot.y - 0.5f);
            DustPuff(foot, 6);
            GolemAudio.Play("golem_paso", foot, 0.85f);
            if (UnityEngine.Random.value < 0.5f)
                Debris.Chip(foot + Vector3.up * 0.1f, UnityEngine.Random.insideUnitSphere * 1.2f + Vector3.up * 1.8f, UnityEngine.Random.Range(0.04f, 0.08f), StoneB, 2f);
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - foot).sqrMagnitude < 14f * 14f) CameraShake3D.Shake(0.035f, 0.1f);
        }

        /// Se enrolla en forma de rueda (true) o vuelve a ponerse de pie (false). Una nube de polvo tapa el cambio.
        public IEnumerator Curl(bool toWheel)
        {
            Busy = true;
            if (toWheel)
            {
                crouchTarget = 1f; hunchTarget = 1f; armsUpTarget = -0.2f;
                GolemAudio.Play("golem_arrancar", transform.position + Vector3.up, 1f, 0.85f);   // rocas que se pliegan
                yield return new WaitForSeconds(0.45f);
                DustPuff(transform.position + Vector3.up * 1f, 40);
                body.gameObject.SetActive(false);
                wheel.gameObject.SetActive(true);
                curl = curlTarget = 1f;
            }
            else
            {
                DustPuff(transform.position + Vector3.up * 1f, 40);
                GolemAudio.Play("golem_arrancar", transform.position + Vector3.up, 0.9f, 1f);
                wheel.gameObject.SetActive(false);
                body.gameObject.SetActive(true);
                curl = curlTarget = 0f;
                crouch = 1f; crouchTarget = 0f; hunchTarget = 0f; armsUpTarget = 0f;
                yield return new WaitForSeconds(0.5f);
            }
            Busy = false;
        }

        /// Gira la rueda según la distancia recorrida (llamar mientras rueda).
        public void SpinWheel(float distance)
        {
            if (wheel == null) return;
            var spin = wheel.GetChild(0);
            spin.localRotation = Quaternion.Euler(distance / 1.55f * Mathf.Rad2Deg, 0, 0) * Quaternion.identity;
            wheel.localPosition = new Vector3(0, 1.55f + Mathf.Abs(Mathf.Sin(distance * 1.7f)) * 0.08f, 0);
        }

        public void DustPuff(Vector3 at, int count)
        {
            var ps = CineGlow.Motes("Polvo", null, at, new Color(0.35f, 0.3f, 0.25f), 0f, 0.7f, 2.2f, 1.3f, 0.5f, -0.02f);
            CineGlow.SetIntensity(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial, 0.6f);
            ps.Emit(count);
            Destroy(ps.gameObject, 2f);
        }

        /// El gólem se desmorona: las rocas se sueltan, caen y se hunden en el suelo; las grietas se apagan.
        public IEnumerator Crumble()
        {
            SetEyes(0f); SetCracks(0f); SetWalk(0f);
            enabled = false;
            CameraShake3D.Shake(0.3f, 0.6f);
            DustPuff(transform.position + Vector3.up * 0.5f, 50);
            GolemAudio.Play("golem_derrumbe", transform.position + Vector3.up * 1.5f, 1f);
            if (embers != null) embers.Stop();
            CineGlow.SetIntensity(eyeMat, 0f); CineGlow.SetIntensity(crackMat, 0.1f); CineGlow.SetIntensity(chestMat, 0f);
            if (chestText != null) chestText.gameObject.SetActive(false);
            if (coreLight != null) coreLight.enabled = false;
            // Cada roca se suelta con su propio peso: cae, rebota, queda un rato en el suelo y se hunde.
            // Las piezas de lava (ojos, grietas, placa) ya apagadas desaparecen entre el polvo.
            var stone = DetailMaterials.SharedStone();
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r.sharedMaterial != rockMat) { r.enabled = false; continue; }
                var go = r.gameObject;
                Recenter(go);
                go.transform.SetParent(null, true);
                r.sharedMaterial = stone;
                Vector3 v = (go.transform.position - transform.position - Vector3.up * 1.5f).normalized * UnityEngine.Random.Range(1f, 3.2f)
                          + Vector3.up * UnityEngine.Random.Range(1f, 3.5f);
                Debris.Spawn(go, v, UnityEngine.Random.insideUnitSphere * 160f, UnityEngine.Random.Range(3.5f, 5.5f), 0.35f);
            }
            for (float t = 0; t < 0.6f; t += Time.deltaTime) yield return null;
            DustPuff(transform.position + Vector3.up * 0.3f, 40);
            for (float t = 0; t < 1.8f; t += Time.deltaTime) yield return null;
        }

        static float GroundY(Vector3 p, float fallback) => RuntimeMaterials.Ground(p, out Vector3 hit) ? hit.y : fallback;

        // ------------------------------------------------------------------ animación procedural
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 dp = transform.position - lastPos; dp.y = 0; lastPos = transform.position;
            planarSpeed = Mathf.Lerp(planarSpeed, dp.magnitude / dt, 1f - Mathf.Exp(-8f * dt));

            walk = Mathf.MoveTowards(walk, walkTarget, dt * 2.5f);
            phase += dt * 3.2f * Mathf.Max(walk, 0.0001f);
            idleT += dt;
            eyes = Mathf.MoveTowards(eyes, eyesTarget, dt * 3f);
            cracks = Mathf.MoveTowards(cracks, cracksTarget, dt * 2f);
            armsUp = Damp(armsUp, armsUpTarget, armsUpTarget < armsUp ? 22f : 5f, dt);
            stompLeg = Damp(stompLeg, stompTarget, stompTarget < stompLeg ? 25f : 6f, dt);
            hunch = Damp(hunch, hunchTarget, 4f, dt);
            roar = Damp(roar, roarTarget, 4f, dt);
            crouch = Damp(crouch, crouchTarget, 5f, dt);
            hold = Damp(hold, holdTarget, 6f, dt);
            heal = Damp(heal, healTarget, 3f, dt);
            stagger = Mathf.MoveTowards(stagger, 0f, dt * 1.6f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 7f);
            hitKick = Damp(hitKick, 0f, 6f, dt);
            damage = Mathf.MoveTowards(damage, damageTarget, dt * 0.8f);

            if (curl > 0.5f) { UpdateGlow(); return; }      // en forma de rueda la anima SpinWheel

            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float w = walk;

            // ---- Pisadas: el pie izquierdo apoya cuando cos(fase) pasa de + a −, el derecho al revés.
            if (w > 0.35f && prevStepCos != 0f && Mathf.Sign(c) != Mathf.Sign(prevStepCos)) FootDown(c < 0f ? kneeL : kneeR);
            prevStepCos = c;
            // de vez en cuando se le desprende un guijarro del cuerpo al andar
            if (w > 0.5f && idleT > nextPebble)
            {
                nextPebble = idleT + UnityEngine.Random.Range(1.2f, 2.8f);
                Vector3 from = torso.TransformPoint(new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.4f, 1.9f), UnityEngine.Random.Range(-0.6f, 0.6f)));
                Debris.Chip(from, UnityEngine.Random.insideUnitSphere * 0.6f, UnityEngine.Random.Range(0.04f, 0.08f), StoneA, 2.5f);
            }
            // Idle: cambio de peso lento de una pierna a otra (solo parado).
            float idleShift = Mathf.Sin(idleT * 0.45f) * (1f - w);

            // ---- Piernas: muslo adelante/atrás; la rodilla se dobla en la fase de balanceo (pie en el aire).
            // fwd = cuánto adelanta cada pierna (piernas en oposición); en el balanceo (la pierna avanza) se dobla la rodilla.
            float fwdL = s * 26f * w, fwdR = -s * 26f * w;
            float swingL = Mathf.Max(0f, c), swingR = Mathf.Max(0f, -c);
            legL.localRotation = Quaternion.Euler(-fwdL - crouch * 55f, 0, -idleShift * 2f);
            legR.localRotation = Quaternion.Euler(-fwdR - stompLeg * 55f - crouch * 55f, 0, -idleShift * 2f);
            kneeL.localRotation = Quaternion.Euler(swingL * 38f * w + crouch * 95f, 0, 0);
            kneeR.localRotation = Quaternion.Euler(swingR * 38f * w + stompLeg * 70f + crouch * 95f, 0, 0);

            // ---- Cadera: baja en cada pisada (dos veces por ciclo) y se desplaza sobre la pierna de apoyo.
            float bob = (1f - Mathf.Abs(c)) * 0.13f * w;                          // más alta a mitad de paso
            float sway = s * 0.12f * w + idleShift * 0.06f;
            hips.localPosition = new Vector3(sway, 1.6f - 0.13f * w + bob - crouch * 0.75f + stompLeg * 0.12f, 0);
            hips.localRotation = Quaternion.Euler(0, s * 7f * w, -s * 4f * w - idleShift * 2f + Mathf.Sin(stagger * 20f) * 6f * stagger);

            // ---- Torso: inclinado al andar, respira, gira contra la cadera (contrapeso).
            float breathe = Mathf.Sin(idleT * 1.3f) * 1.2f;
            float torsoPitch = BaseHunch * (1f - crouch) + 10f * hunch + 7f * w - 18f * roar + breathe - armsUp * 8f + crouch * 30f - hold * 6f - heal * 12f - stagger * 10f - hitKick * 12f;
            torso.localRotation = Quaternion.Euler(torsoPitch, -s * 9f * w, c * 3f * w + idleShift * 1.5f);
            torso.localScale = new Vector3(1f, 1f + Mathf.Sin(idleT * 1.3f) * 0.012f, 1f);

            // ---- Brazos: cuelgan pesados con inercia (resorte) y balanceo opuesto a las piernas.
            float swingArm = s * 20f * w;                                             // brazo izquierdo atrás cuando avanza la pierna izquierda
            Spring(ref armLagL, ref armLagLv, swingArm, dt);
            Spring(ref armLagR, ref armLagRv, -swingArm, dt);
            float up = armsUp;
            float holdUp = hold * 175f;                                               // sostener sobre la cabeza
            float crouchArms = crouch * 55f;                                          // manos al suelo
            shoulderL.localRotation = Quaternion.Euler(armLagL - up * 160f - roar * 40f - holdUp - crouchArms - heal * 60f, 0, -6f - roar * 35f + hold * 22f - heal * 30f);
            shoulderR.localRotation = Quaternion.Euler(armLagR - up * 160f - roar * 40f - holdUp - crouchArms - heal * 60f, 0, 6f + roar * 35f - hold * 22f + heal * 30f);
            float elbowBend = -14f - up * 30f - hold * 70f - Mathf.Abs(armLagL) * 0.3f;
            elbowL.localRotation = Quaternion.Euler(elbowBend, 0, hold * 25f);
            elbowR.localRotation = Quaternion.Euler(elbowBend, 0, -hold * 25f);

            // ---- Cabeza: mira al objetivo con un giro lento y mecánico; de vez en cuando mira alrededor en reposo.
            float wantYaw = Mathf.Sin(idleT * 0.3f) * 18f * (1f - w), wantPitch = 0f;
            if (hasLook)
            {
                Vector3 local = torso.InverseTransformPoint(lookTarget) - head.localPosition;
                wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -25f, 30f);
            }
            headYaw = Mathf.MoveTowardsAngle(headYaw, wantYaw, dt * 55f);
            headPitch = Mathf.MoveTowards(headPitch, wantPitch - roar * 25f - heal * 15f - BaseHunch * 0.8f * (1f - crouch), dt * 60f);   // mira al frente pese a ir encorvado
            head.localRotation = Quaternion.Euler(headPitch, headYaw, Mathf.Sin(stagger * 17f) * 10f * stagger);

            UpdateGlow();
        }

        void UpdateGlow()
        {
            float flick = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3f, 0.3f);
            float k = cracks + heal * 1.5f;
            CineGlow.SetIntensity(eyeMat, eyes * (0.9f + 0.1f * Mathf.Sin(Time.time * 9f)));
            CineGlow.SetIntensity(crackMat, k * flick);
            CineGlow.SetIntensity(chestMat, k * 0.9f * flick);
            if (rockMat != null)
            {
                rockMat.SetFloat(FlashId, flash);
                rockMat.SetFloat(DamageId, damage);                              // las venas de lava se abren con el daño
                rockMat.SetFloat(VeinGlowId, Mathf.Max(0.15f, k) * flick);
                rockMat.SetColor(VeinColorId, Color.Lerp(Lava, new Color(0.8f, 3f, 0.6f), Mathf.Clamp01(heal)));
            }
            if (coreLight != null)
            {
                coreLight.intensity = 1.6f * k * flick + eyes * 0.4f;
                coreLight.color = Color.Lerp(new Color(1f, 0.45f, 0.12f), new Color(0.55f, 1f, 0.35f), Mathf.Clamp01(heal));
            }
        }

        static void Spring(ref float x, ref float v, float target, float dt)
        {
            // resorte amortiguado: sigue al objetivo con un pequeño retraso y rebote (inercia de los brazos)
            const float k = 40f, damping = 9f;
            v += (k * (target - x) - damping * v) * dt;
            x += v * dt;
        }

        static float Damp(float a, float b, float k, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-k * dt));

        void OnDestroy()
        {
            if (eyeMat != null) Destroy(eyeMat);
            if (crackMat != null) Destroy(crackMat);
            if (chestMat != null) Destroy(chestMat);
            if (rockMat != null) Destroy(rockMat);
        }
    }
}
