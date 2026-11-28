using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Jefes dragón construidos con DragonKit (mallas low poly facetadas con escamas):
    ///  - Nivel 2 Desierto · AZHAR: wyvern de una cabeza. Vuela bajo y lanza abanicos de fuego; a mitad de vida
    ///    aterriza levantando polvo, se acerca por tierra y gira para barrer con la maza de la cola.
    ///  - Nivel 3 Costa Chatarra · HIDRAKHAN: dragón de varias cabezas (hielo y fuego). Cada cabeza apunta y ataca
    ///    por su cuenta (bolas de fuego / esquirlas de hielo); con poca vida atacan todas a la vez.
    /// Animación procedural: aleteo con bajada rápida, respiración, cuellos que se balancean, mandíbulas, cola
    /// ondulante y pasos. La piel destella al recibir un golpe.
    [RequireComponent(typeof(HealthSystem))]
    public class DragonBoss : MonoBehaviour
    {
        public float fireballDamage = 14f, iceDamage = 9f, cooldown = 3.2f, fireballSpeed = 14f, iceSpeed = 19f;
        public int fireballsPerVolley = 3, shardsPerVolley = 5;
        public float spreadDegrees = 10f;
        public float minRange = 5f, maxRange = 26f, keepAway = 6f;
        public bool landsInPhase2;        // Azhar
        public bool headsTogetherLowHp;   // Hidrakhan
        public float tailDamage = 20f;
        public float roarPitch = 1f;
        public const float HoverHeight = 2.4f;

        class HeadState
        {
            public DragonHeadRig rig;
            public float chargeT = -1f, nextShot;
            public int shotsLeft;
            public GameObject glow;
            public bool Busy => chargeT >= 0f || shotsLeft > 0;
        }

        DragonRig rig;
        readonly List<HeadState> heads = new List<HeadState>();
        FinalBoss boss; EnemyAI3D ai; HealthSystem hs; Transform player; CapsuleCollider col;
        float size = 1f, groundHeight;
        float nextFire, nextSwipe, flash, flapPhase, stepPhase, swipeYaw;
        int nextHead;
        bool flying = true, busy, roared, phase2;
        Vector3 lastPos;
        int groundMask, obstacleMask;

        // ------------------------------------------------------------------ construcción
        public static DragonBoss Build(GameObject root, DragonSpec spec)
        {
            var d = root.AddComponent<DragonBoss>();
            d.rig = DragonKit.Build(root.transform, spec, HoverHeight);
            d.size = spec.size;
            d.groundHeight = d.rig.legLength * spec.size;
            foreach (var h in d.rig.heads) d.heads.Add(new HeadState { rig = h });
            return d;
        }

        // ------------------------------------------------------------------ comportamiento
        void Start()
        {
            boss = GetComponent<FinalBoss>(); ai = GetComponent<EnemyAI3D>(); hs = GetComponent<HealthSystem>(); col = GetComponent<CapsuleCollider>();
            var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform;
            if (ai != null) { ai.meleeEnabled = false; ai.attackRange = 11f; }
            groundMask = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : Physics.DefaultRaycastLayers;
            obstacleMask = GameLayers.Exists(GameLayers.Obstacles) ? GameLayers.Mask(GameLayers.Obstacles) : 0;
            nextFire = Time.time + 2.5f;
            lastPos = transform.position;
            flapPhase = Random.value * 6f;
            hs.Damaged += (_, __) => flash = 1f;
            hs.Died += _ => OnDied();
        }

        void Update()
        {
            if (hs.IsDead || rig == null) return;
            UpdateFlash();
            Animate();
            if (boss == null || !boss.IsAwake || player == null) return;
            if (!roared) { roared = true; MageAudio.PlayBossRoar(transform.position, roarPitch); }

            foreach (var h in heads) UpdateHead(h);
            if (busy) return;

            Vector3 to = player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;

            if (landsInPhase2 && !phase2 && hs.Normalized < 0.5f && !AnyHeadBusy()) { StartCoroutine(Land()); return; }
            if (!flying && dist < 4.6f * size && Time.time >= nextSwipe && !AnyHeadBusy()) { StartCoroutine(TailSwipe()); return; }
            if (flying && keepAway > 0f && dist < keepAway) Retreat(-to.normalized);

            if (Time.time >= nextFire && !AnyHeadBusy() && dist >= minRange * (flying ? 1f : 0.4f) && dist <= maxRange)
            {
                bool together = headsTogetherLowHp && heads.Count > 1 && hs.Normalized < 0.35f;
                if (together) { foreach (var h in heads) TryCharge(h); }
                else TryCharge(heads[nextHead++ % heads.Count]);
                nextFire = Time.time + cooldown * Random.Range(0.85f, 1.15f) * (together ? 1.2f : 1f);
            }
        }

        bool AnyHeadBusy() { foreach (var h in heads) if (h.Busy) return true; return false; }

        void TryCharge(HeadState h)
        {
            if (!BossKit.LineOfSight(h.rig.mouth.position, player.position + Vector3.up)) return;
            h.chargeT = 0f;
            var spec = h.rig.spec;
            h.glow = RuntimeMaterials.Prim(PrimitiveType.Sphere, spec.ice ? "HieloEnBoca" : "FuegoEnBoca", null, h.rig.mouth.position, Vector3.one * 0.1f, RuntimeMaterials.Glow(spec.breathCore), false);
            var l = h.glow.AddComponent<Light>(); l.type = LightType.Point; l.color = spec.breathGlow; l.range = 4.5f; l.intensity = 3f; l.shadows = LightShadows.None;
        }

        void UpdateHead(HeadState h)
        {
            var spec = h.rig.spec;
            if (h.chargeT >= 0f)
            {
                h.chargeT += Time.deltaTime;
                FacePlayer(5f);
                float k = Mathf.Clamp01(h.chargeT / 0.7f);
                if (h.glow != null) { h.glow.transform.position = h.rig.mouth.position; h.glow.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.65f, k); }
                if (h.chargeT >= 0.7f) { h.chargeT = -1f; h.shotsLeft = spec.ice ? shardsPerVolley : fireballsPerVolley; h.nextShot = Time.time; }
                return;
            }
            if (h.shotsLeft <= 0 || Time.time < h.nextShot) return;

            int total = spec.ice ? shardsPerVolley : fireballsPerVolley;
            int i = total - h.shotsLeft;
            float spread = spec.ice ? spreadDegrees * 1.8f : spreadDegrees;
            float angle = total == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (float)(total - 1));
            Vector3 from = h.rig.mouth.position;
            float speed = spec.ice ? iceSpeed : fireballSpeed;
            float t = Vector3.Distance(from, player.position) / speed;
            Vector3 target = BossKit.Predict(player, t * 0.6f) + Vector3.up * 0.9f;
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * (target - from).normalized;
            BossKit.Fireball(from, dir, speed, spec.ice ? iceDamage : fireballDamage, spec.breathCore, spec.breathGlow, spec.ice ? 0.42f : 0.7f);
            HitFX.Spark(from, spec.breathGlow, 0.6f);
            h.shotsLeft--;
            h.nextShot = Time.time + (spec.ice ? 0.08f : 0.14f);
            if (h.shotsLeft <= 0 && h.glow != null) Destroy(h.glow);
        }

        IEnumerator Land()
        {
            busy = true; phase2 = true;
            MageAudio.PlayBossRoar(transform.position, roarPitch * 0.9f);
            UIManager.Instance?.ShowMessage($"¡{(boss != null ? boss.bossName : "El dragón")} aterriza!  Cuidado con su cola", 2.5f);
            flying = false;
            yield return new WaitForSeconds(1.2f);
            CameraShake3D.Shake(0.35f, 0.6f);
            Dust(transform.position, 46, 3.2f);
            if (ai != null) ai.attackRange = 3.4f * size;
            keepAway = 0f; cooldown *= 1.3f;
            nextSwipe = Time.time + 1.5f;
            busy = false;
        }

        IEnumerator TailSwipe()
        {
            busy = true;
            nextSwipe = Time.time + 4.5f;
            // Amago: se agacha y gira un poco hacia el lado contrario
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) { swipeYaw = Mathf.Lerp(0f, -25f, t / 0.35f); yield return null; }
            bool hit = false;
            const float dur = 0.75f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                swipeYaw = Mathf.Lerp(-25f, 360f, k * k * (3f - 2f * k));
                if (!hit && k > 0.4f && player != null)
                {
                    hit = true;
                    Vector3 d = player.position - transform.position; d.y = 0f;
                    if (d.magnitude < 5.2f * size)
                    {
                        var phs = player.GetComponent<HealthSystem>();
                        if (phs != null && phs.TakeDamage(tailDamage)) CameraShake3D.Shake(0.25f, 0.35f);
                    }
                    Dust(transform.position, 20, 2.2f);
                }
                yield return null;
            }
            swipeYaw = 0f;
            busy = false;
        }

        void OnDied()
        {
            foreach (var h in heads) if (h.glow != null) Destroy(h.glow);
            var c = heads.Count > 0 ? heads[0].rig.spec.breathGlow : new Color(1f, 0.5f, 0.1f);
            if (rig != null && rig.model != null) HitFX.Explosion(rig.model.position, 2.4f * size, c);
        }

        // ------------------------------------------------------------------ animación
        void Animate()
        {
            float t = Time.time, dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 v = (transform.position - lastPos) / dt; lastPos = transform.position;
            v.y = 0f;
            float speed = v.magnitude;
            float fwd = Vector3.Dot(v, transform.forward);

            // Altura: vuelo con aleteo o de pie sobre las patas
            float flapSpeed = flying ? 4.2f : 0f;
            flapPhase += dt * flapSpeed;
            float stroke = Mathf.Sin(flapPhase + 0.35f * Mathf.Sin(flapPhase));     // bajada más rápida que la subida
            float targetY = flying ? HoverHeight + Mathf.Sin(t * 1.4f) * 0.2f - stroke * 0.12f : groundHeight + Mathf.Sin(t * 1.8f) * 0.02f;
            var mp = rig.model.localPosition;
            mp.y = Mathf.Lerp(mp.y, targetY, dt * (flying ? 4f : 2.2f));
            rig.model.localPosition = mp;
            if (col != null) col.center = new Vector3(0f, mp.y, 0f);

            float lean = Mathf.Clamp(fwd * 4f, -12f, 12f);
            rig.model.localRotation = Quaternion.Slerp(rig.model.localRotation, Quaternion.Euler(flying ? lean : lean * 0.4f, swipeYaw, 0f), swipeYaw != 0f ? 1f : dt * 4f);

            // Respiración
            float breath = Mathf.Sin(t * 1.6f);
            rig.torso.localScale = new Vector3(1f + breath * 0.015f, 1f + breath * 0.025f, 1f);

            // Alas
            Quaternion wl, wr;
            if (flying)
            {
                float raise = 12f + 40f * stroke;
                float sweep = -6f * stroke;
                wl = Quaternion.Euler(0f, -sweep, -raise);
                wr = Quaternion.Euler(0f, sweep, raise);
            }
            else
            {
                float idle = Mathf.Sin(t * 1.2f) * 3f;
                wl = Quaternion.Euler(0f, -38f, 52f + idle);    // plegadas hacia abajo, la muñeca toca el suelo
                wr = Quaternion.Euler(0f, 38f, -52f - idle);
            }
            rig.wingL.localRotation = Quaternion.Slerp(rig.wingL.localRotation, wl, dt * (flying ? 14f : 4f));
            rig.wingR.localRotation = Quaternion.Slerp(rig.wingR.localRotation, wr, dt * (flying ? 14f : 4f));

            // Cuellos y cabezas
            bool awake = boss != null && boss.IsAwake && player != null;
            for (int j = 0; j < heads.Count; j++)
            {
                var h = heads[j];
                for (int i = 0; i < h.rig.neck.Count; i++)
                {
                    float yaw = Mathf.Sin(t * 1.1f + j * 1.7f + i * 0.5f) * 5f;
                    float pitch = Mathf.Sin(t * 1.3f + j + i * 0.4f) * 3f;
                    h.rig.neck[i].localRotation = Quaternion.Slerp(h.rig.neck[i].localRotation, Quaternion.Euler(pitch, yaw, 0f), dt * 3f);
                }
                if (awake)
                {
                    Vector3 look = player.position + Vector3.up - h.rig.head.position;
                    if (look.sqrMagnitude > 0.01f) h.rig.head.rotation = Quaternion.Slerp(h.rig.head.rotation, Quaternion.LookRotation(look), dt * (h.Busy ? 8f : 4f));
                }
                else h.rig.head.localRotation = Quaternion.Slerp(h.rig.head.localRotation, Quaternion.Euler(18f + Mathf.Sin(t * 0.7f + j) * 6f, Mathf.Sin(t * 0.5f + j * 2f) * 15f, 0f), dt * 2f);

                float open = h.chargeT >= 0f ? Mathf.Clamp01(h.chargeT / 0.7f) * 38f : h.shotsLeft > 0 ? 38f : 3f + Mathf.Sin(t * 1.6f + j) * 3f;
                h.rig.jaw.localRotation = Quaternion.Slerp(h.rig.jaw.localRotation, Quaternion.Euler(open, 0f, 0f), dt * 10f);
            }

            // Cola
            for (int i = 0; i < rig.tail.Count; i++)
            {
                float amp = flying ? 9f : 13f;
                rig.tail[i].localRotation = Quaternion.Euler(3f + Mathf.Sin(t * 1.7f - i * 0.5f) * 3f, Mathf.Sin(t * 1.9f - i * 0.6f) * amp, 0f);
            }

            // Patas: colgando en vuelo, pasos en tierra
            stepPhase += dt * speed * 2.4f / Mathf.Max(0.5f, size);
            float walk = Mathf.Clamp01(speed / 1.5f);
            for (int k = 0; k < rig.legs.Count; k++)
            {
                float x = flying ? 28f + Mathf.Sin(t * 1.4f + k) * 6f : Mathf.Sin(stepPhase + k * Mathf.PI + (k >= 2 ? Mathf.PI * 0.5f : 0f)) * 24f * walk;
                rig.legs[k].localRotation = Quaternion.Slerp(rig.legs[k].localRotation, Quaternion.Euler(x, 0f, 0f), dt * 6f);
            }
        }

        void UpdateFlash()
        {
            if (flash <= 0f) return;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 5f);
            foreach (var m in rig.materials) if (m != null) m.SetFloat("_Flash", flash * 0.6f);
        }

        void Dust(Vector3 at, int count, float speed)
        {
            CineGlow.Burst(at + Vector3.up * 0.3f, new Color(0.75f, 0.6f, 0.42f) * 0.7f, count, speed, 0.35f * size, 1.2f, 0.25f);
        }

        void Retreat(Vector3 dir)
        {
            Vector3 next = transform.position + dir * 2.4f * Time.deltaTime;
            if (obstacleMask != 0 && Physics.SphereCast(transform.position + Vector3.up * 1.5f, 0.8f, dir, out _, 1.5f, obstacleMask, QueryTriggerInteraction.Ignore)) return;
            if (!Physics.Raycast(next + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, groundMask, QueryTriggerInteraction.Ignore)) return;
            next.y = hit.point.y;
            transform.position = next;
        }

        void FacePlayer(float speed)
        {
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * speed);
        }

        void OnDestroy()
        {
            foreach (var h in heads) if (h.glow != null) Destroy(h.glow);
            if (rig != null) foreach (var m in rig.materials) if (m != null) Destroy(m);
        }
    }
}
