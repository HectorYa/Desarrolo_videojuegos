using System.Collections;
using UnityEngine;

namespace MageHero
{
    /// Jefe del nivel 1 (Otoño): el Gólem de Piedra, al final del camino iluminado.
    /// Usa el modelo y la animación de StoneGolem (el mismo de la cinemática de apertura).
    ///
    /// Habilidades (las va aprendiendo a medida que pierde vida):
    ///  · Fase 1 (100–70 %): arranca rocas del suelo y las lanza en parábola · golpe con los puños si estás cerca.
    ///  · Fase 2 (< 70 %):   + golpea el suelo y hace BROTAR LA TIERRA en línea hacia el mago.
    ///  · Fase 3 (< 40 %):   + se enrolla como una RUEDA y rueda a toda velocidad hacia el mago para aplastarlo.
    ///
    /// Momento matemático: cada vez que pierde un 15 % de vida, el tiempo se ralentiza, la escena se oscurece
    /// y aparece un problema (sumas y restas, cada vez más difíciles; también arde en su pecho). De fondo, en cámara
    /// lenta, el mago salta intentando escapar y el gólem le lanza una roca.
    ///  · Correcto → la roca estalla en el aire, el gólem se tambalea y el combate continúa.
    ///  · Incorrecto → el gólem recupera el 15 % de vida que había perdido (y la roca sigue su camino).
    [RequireComponent(typeof(HealthSystem))]
    public class GolemBoss : MonoBehaviour
    {
        public float rockDamage = 18f, slamDamage = 20f, spikeDamage = 16f, rollDamage = 24f;
        public float rockCooldown = 3.6f, slamCooldown = 3f, spikeCooldown = 7f, rollCooldown = 10f;
        public float rockMinRange = 6f, rockMaxRange = 26f, slamRange = 4.2f;
        public float walkSpeed = 1.9f;
        [Tooltip("Cada cuánto (fracción de vida) aparece un problema matemático.")]
        public float mathStep = 0.15f;

        StoneGolem g;
        FinalBoss boss; EnemyAI3D ai; HealthSystem hs;
        Transform player; CharacterController playerCC; HealthSystem playerHealth;
        bool acting, inMoment, dead;
        float nextRock, nextSlam, nextSpike, nextRoll, nextThink;
        float nextThreshold;
        int phaseShown = 1, solved;
        bool woke;
        AudioSource ambience;
        Color rockColor = new Color(0.55f, 0.52f, 0.48f);

        // ------------------------------------------------------------------ construcción
        /// Lo llama FinalBoss al crear el jefe del Nivel 1. Los colores se mantienen por compatibilidad.
        public static void Build(GameObject root, Color rock, Color crack)
        {
            var golem = root.AddComponent<StoneGolem>();
            golem.BuildOn(root);
            var b = root.AddComponent<GolemBoss>();
            b.g = golem;
            b.rockColor = rock;
        }

        void Start()
        {
            boss = GetComponent<FinalBoss>(); ai = GetComponent<EnemyAI3D>(); hs = GetComponent<HealthSystem>();
            if (g == null) g = GetComponent<StoneGolem>();
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                playerCC = p.GetComponentInParent<CharacterController>();
                player = playerCC != null ? playerCC.transform : p.transform;
                playerHealth = player.GetComponent<HealthSystem>();
            }
            if (ai != null) ai.enabled = false;               // el gólem se mueve solo (pasos sincronizados, sin patinar)
            hs.destroyOnDeath = false;                         // primero se desmorona, luego desaparece
            hs.Died += OnDied;
            // cada golpe salta en esquirlas y la roca se va resquebrajando según la vida que le queda
            hs.Damaged += (_, amount) =>
            {
                if (dead || g == null) return;
                Vector3 toPlayer = player != null ? Flat(player.position - transform.position).normalized : transform.forward;
                Vector3 at = (g.Chest != null ? g.Chest.position : transform.position) + Vector3.up * Random.Range(0.6f, 1.8f)
                           + toPlayer * 1.1f + Vector3.Cross(Vector3.up, toPlayer) * Random.Range(-0.8f, 0.8f);
                g.Hit(at, Mathf.Clamp(amount / 12f, 0.5f, 1.5f));
                g.SetDamage(1f - hs.Normalized);
            };
            hs.Changed += _ => { if (!dead && g != null) g.SetDamage(1f - hs.Normalized); };
            nextThreshold = 1f - mathStep;
            nextRock = Time.time + 2.5f; nextSpike = Time.time + 4f; nextRoll = Time.time + 6f;
            g.SetEyes(0f, true); g.SetCracks(0.5f); g.SetHunch(0.9f); g.ShowOperation(false);
        }

        int Phase => hs.Normalized < 0.4f ? 3 : hs.Normalized < 0.7f ? 2 : 1;

        // ------------------------------------------------------------------ cerebro
        void Update()
        {
            if (dead || hs == null || hs.IsDead) return;
            if (ai != null && ai.enabled) ai.enabled = false;
            if (boss == null || !boss.IsAwake || player == null) return;

            if (!woke) { woke = true; StartCoroutine(Run(WakeUp())); return; }

            if (inMoment) { hs.SetInvulnerable(0.3f); return; }

            // ¿Toca un problema matemático?
            if (!acting && hs.Normalized <= nextThreshold && nextThreshold > 0.01f) { StartCoroutine(MathMoment()); return; }

            AnnouncePhase();
            Vector3 to = player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            g.LookAt(player.position + Vector3.up * 1.4f);

            if (acting) return;
            if (Time.time >= nextThink)
            {
                nextThink = Time.time + 0.25f;
                if (dist <= slamRange && Time.time >= nextSlam) { StartCoroutine(Run(Slam())); return; }
                if (Phase >= 3 && Time.time >= nextRoll && dist > 7f && dist < 26f && Random.value < 0.55f) { StartCoroutine(Run(Roll())); return; }
                if (Phase >= 2 && Time.time >= nextSpike && dist > 4.5f && dist < 18f && Random.value < 0.6f) { StartCoroutine(Run(Spikes())); return; }
                if (Time.time >= nextRock && dist >= rockMinRange && dist <= rockMaxRange &&
                    BossKit.LineOfSight(transform.position + Vector3.up * 3f, player.position + Vector3.up)) { StartCoroutine(Run(Throw(player))); return; }
            }
            // Persecución: se acerca con pasos pesados; a media distancia se planta a lanzar.
            if (dist > 3.4f) g.StepToward(player.position, walkSpeed);
            else { g.SetWalk(0f); Face(player.position, 90f); }
        }

        /// Despierta: se yergue, se sacude las hojas y ruge.
        IEnumerator WakeUp()
        {
            // ambiente destructivo mientras dura el combate: retumbar grave y derrumbes lejanos
            if (ambience == null) ambience = GolemAudio.Loop("golem_ambiente", null, 0.55f);
            g.SetHunch(0f); g.SetEyes(1f); g.SetCracks(1f);
            yield return new WaitForSeconds(0.6f);
            yield return g.Roar(1.3f);
            UIManager.Instance?.ShowMessage("Cada vez que le quites vida aparecerá un problema: ¡resuélvelo para que no se cure!", 4.5f);
        }

        IEnumerator Run(IEnumerator action)
        {
            acting = true;
            g.SetWalk(0f);
            yield return action;
            acting = false;
        }

        void AnnouncePhase()
        {
            int ph = Phase;
            if (ph <= phaseShown) return;
            phaseShown = ph;
            var ui = UIManager.Instance;
            if (ph == 2) ui?.ShowMessage("¡El Gólem golpea el suelo y hace brotar la tierra! Esquiva las grietas.", 4f);
            if (ph == 3) ui?.ShowMessage("¡El Gólem se enrolla como una rueda! Apártate de su camino.", 4f);
            StartCoroutine(g.Roar(1.0f));
        }

        void Face(Vector3 p, float degPerSec)
        {
            Vector3 d = p - transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degPerSec * Time.deltaTime);
        }

        IEnumerator FaceFor(float seconds, float degPerSec = 160f)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) { Face(player.position, degPerSec); yield return null; }
        }

        // ------------------------------------------------------------------ habilidad 1: lanzar rocas
        /// Se agacha, arranca una roca del suelo, la levanta sobre la cabeza y la lanza en parábola.
        IEnumerator Throw(Transform target, bool slowMoRock = false, System.Action<GameObject> launched = null)
        {
            float k = slowMoRock ? 0.35f : 1f;          // en el momento matemático el gesto es más corto (ya va en cámara lenta)
            g.SetCrouch(1f);
            yield return FaceFor(0.45f * k);
            Vector3 ground = transform.position + transform.forward * 1.3f;
            if (RuntimeMaterials.Ground(ground, out var gh)) ground = gh;
            g.DustPuff(ground, 18);
            GolemAudio.Play("golem_arrancar", ground, 1f);                     // arranca la roca del suelo
            var rock = MakeRock(0.62f);
            float t = 0;
            // sube la roca entre los puños
            g.SetCrouch(0f); g.SetHold(1f);
            while (t < 0.75f * k)
            {
                t += Time.deltaTime;
                rock.transform.position = g.HandsCenter + Vector3.up * 0.45f;
                rock.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, t / (0.5f * k));
                Face(target.position, 120f);
                yield return null;
            }
            // lanzamiento: los brazos bajan de golpe hacia delante
            g.SetHold(0f); g.SetArmsUp(-0.35f);
            yield return new WaitForSeconds(0.08f);
            Vector3 from = rock.transform.position;
            float dist = Vector3.Distance(from, target.position);
            float flight = Mathf.Clamp(dist / 13f, 0.9f, 1.8f);
            Vector3 aim = BossKit.Predict(target, flight * 0.7f);
            if (RuntimeMaterials.Ground(aim, out var ag)) aim = ag;
            if (slowMoRock) launched?.Invoke(rock);
            else ArcProjectile.Launch(rock, from, aim, flight, 3f + dist * 0.15f, rockDamage, 2.1f, new Color(0.75f, 0.5f, 0.25f));
            GolemAudio.Play("golem_lanzar", from, 1f, slowMoRock ? 0.7f : 1f);   // silbido de la roca cortando el aire
            CameraShake3D.Shake(0.08f, 0.2f);
            yield return new WaitForSeconds(0.35f);
            g.SetArmsUp(0f);
            yield return new WaitForSeconds(0.3f);
            nextRock = Time.time + rockCooldown * (Phase >= 3 ? 0.7f : 1f);
        }

        GameObject MakeRock(float radius)
        {
            var lp = new LowPoly().Paint(rockColor);
            lp.Rock(radius, Random.Range(0, 9999), 0.25f);
            lp.Paint(new Color(0.42f, 0.55f, 0.25f));                   // un poco de musgo y tierra pegados
            lp.At(new Vector3(0, radius * 0.7f, 0), Quaternion.identity, new Vector3(1f, 0.4f, 1f)).Rock(radius * 0.6f, Random.Range(0, 9999), 0.3f, 0);
            var go = lp.Spawn("Roca", null, DetailMaterials.SharedStone());
            var crack = new LowPoly().Paint(Color.white);
            crack.At(Vector3.zero, Random.rotation, Vector3.one).Box(new Vector3(radius * 2.05f, 0.05f, 0.05f));
            crack.At(Vector3.zero, Random.rotation, Vector3.one).Box(new Vector3(0.05f, radius * 2.05f, 0.05f));
            crack.Spawn("GrietaLava", go.transform, CineGlow.Make(new Color(3f, 1f, 0.2f), 0f, 1f, 0.3f), default, false);
            return go;
        }

        // ------------------------------------------------------------------ golpe cercano
        IEnumerator Slam()
        {
            yield return FaceFor(0.15f);
            yield return g.Smash(hit =>
            {
                HitFX.Explosion(hit + Vector3.up * 0.3f, 1.8f, new Color(0.8f, 0.55f, 0.3f));
                float d = Flat(player.position - hit).magnitude;
                if (d <= slamRange && playerHealth != null) playerHealth.TakeDamage(slamDamage);
                CameraShake3D.Shake(d < 10f ? 0.45f : 0.2f, 0.4f);
            });
            nextSlam = Time.time + slamCooldown;
        }

        // ------------------------------------------------------------------ habilidad 2: brota la tierra
        /// Golpea el suelo y una línea de pilares de tierra y roca brota del suelo hacia el mago (con aviso previo).
        IEnumerator Spikes()
        {
            yield return FaceFor(0.25f);
            Vector3 targetAtCast = player.position;
            yield return g.Smash(hit =>
            {
                HitFX.Explosion(hit + Vector3.up * 0.3f, 1.4f, new Color(0.7f, 0.5f, 0.3f));
                StartCoroutine(EarthLine(hit, targetAtCast));
                if (Phase >= 3) StartCoroutine(EarthRing(transform.position, 5.5f));
            }, 0.6f);
            nextSpike = Time.time + spikeCooldown;
        }

        IEnumerator EarthLine(Vector3 from, Vector3 to)
        {
            Vector3 dir = Flat(to - from);
            float len = Mathf.Min(22f, dir.magnitude + 3f);
            dir.Normalize();
            for (float d = 1.2f; d <= len; d += 1.3f)
            {
                Vector3 p = from + dir * d;
                p += Vector3.Cross(Vector3.up, dir) * Mathf.Sin(d * 1.3f) * 0.35f;
                if (RuntimeMaterials.Ground(p, out var gp)) p = gp;
                StartCoroutine(EarthSpike(p, 1f + Random.value * 0.5f));
                yield return new WaitForSeconds(0.09f);
            }
        }

        IEnumerator EarthRing(Vector3 center, float radius)
        {
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                if (RuntimeMaterials.Ground(p, out var gp)) p = gp;
                StartCoroutine(EarthSpike(p, 0.9f));
            }
        }

        /// Un pilar de tierra: grieta brillante de aviso → brota de golpe → daña si el mago está encima → se hunde.
        IEnumerator EarthSpike(Vector3 at, float scale)
        {
            var warnMat = CineGlow.Make(new Color(2.2f, 0.7f, 0.15f), 0f, 1f, 0.4f);
            var warn = new LowPoly().Paint(Color.white);
            for (int i = 0; i < 3; i++) warn.At(Vector3.up * 0.04f, Quaternion.Euler(0, i * 60f + Random.value * 20f, 0), Vector3.one).Box(new Vector3(1.6f * scale, 0.02f, 0.08f));
            var w = warn.Spawn("AvisoGrieta", null, warnMat, at, false);
            for (float t = 0; t < 0.45f; t += Time.deltaTime) { CineGlow.SetIntensity(warnMat, 0.5f + Mathf.Sin(t * 30f) * 0.5f + t * 2f); yield return null; }

            var lp = new LowPoly();
            var rng = new System.Random(Random.Range(0, 99999));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.7f, r = i == 0 ? 0f : 0.35f;
                lp.Paint(i % 2 == 0 ? new Color(0.48f, 0.36f, 0.24f) : new Color(0.6f, 0.56f, 0.5f));
                lp.At(new Vector3(Mathf.Cos(a) * r, -0.1f, Mathf.Sin(a) * r), Quaternion.Euler((float)rng.NextDouble() * 20f - 10f, a * 57f, (float)rng.NextDouble() * 20f - 10f), Vector3.one)
                  .Prism(5, (i == 0 ? 0.42f : 0.26f) * scale, 0.02f, (i == 0 ? 1.9f : 1.1f) * scale);
            }
            lp.Paint(new Color(0.4f, 0.3f, 0.2f));
            lp.At(Vector3.zero, Quaternion.identity, new Vector3(1.4f, 0.35f, 1.4f) * scale).Rock(0.55f, rng.Next(999), 0.3f, 0);
            var spike = lp.Spawn("PilarDeTierra", null, DetailMaterials.SharedStone(), at);
            Destroy(w);
            Destroy(warnMat);
            // brota
            spike.transform.localScale = new Vector3(1f, 0.05f, 1f);
            GolemAudio.Play("golem_pilares", at, 0.75f, Random.Range(0.9f, 1.1f), 0.11f);
            HitFX.Explosion(at + Vector3.up * 0.2f, 0.8f * scale, new Color(0.55f, 0.42f, 0.28f));
            bool hit = false;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.12f)
            {
                spike.transform.localScale = new Vector3(1f, Mathf.Lerp(0.05f, 1.1f, t), 1f);
                if (!hit && player != null && Flat(player.position - at).magnitude < 1.25f * scale)
                {
                    hit = true;
                    if (playerHealth != null) playerHealth.TakeDamage(spikeDamage);
                    CameraShake3D.Shake(0.25f, 0.3f);
                }
                yield return null;
            }
            spike.transform.localScale = Vector3.one;
            yield return new WaitForSeconds(1.0f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.5f) { spike.transform.position = at + Vector3.down * 2.2f * scale * t; yield return null; }
            Destroy(spike);
        }

        // ------------------------------------------------------------------ habilidad 3: rueda
        /// Ruge, se enrolla como una rueda, marca su camino en el suelo y rueda hacia el mago (persiguiéndolo un poco).
        /// Al terminar queda aturdido un momento: es la ocasión para atacarle.
        IEnumerator Roll()
        {
            yield return FaceFor(0.3f);
            yield return g.Roar(0.6f);
            yield return g.Curl(true);

            Vector3 dir = Flat(player.position - transform.position).normalized;
            float len = Mathf.Min(28f, Flat(player.position - transform.position).magnitude + 7f);
            var lane = RollLane(transform.position, dir, len);
            yield return new WaitForSeconds(0.7f);
            Destroy(lane);

            float travelled = 0f, speed = 0f, t = 0f;
            bool hitPlayer = false;
            int obstacles = GameLayers.Exists(GameLayers.Obstacles) ? GameLayers.Mask(GameLayers.Obstacles) : 0;
            var dust = CineGlow.Motes("PolvoRueda", transform, Vector3.up * 0.2f, new Color(0.4f, 0.34f, 0.26f), 50f, 0.9f, 1.2f, 1.1f, 0.6f, -0.02f);
            var rollSound = GolemAudio.Loop("golem_rueda", transform, 1f);      // tierra que rueda y se amontona
            while (travelled < len && t < 3f)
            {
                t += Time.deltaTime;
                speed = Mathf.MoveTowards(speed, 13f, 18f * Time.deltaTime);
                if (rollSound != null) rollSound.pitch = 0.75f + speed / 13f * 0.45f;
                // persigue un poco (giro limitado): se puede esquivar apartándose a un lado
                Vector3 want = Flat(player.position - transform.position).normalized;
                if (want.sqrMagnitude > 0.01f) dir = Vector3.RotateTowards(dir, want, 22f * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
                if (obstacles != 0 && Physics.SphereCast(transform.position + Vector3.up * 1.5f, 1.2f, dir, out _, 1.2f, obstacles, QueryTriggerInteraction.Ignore))
                {
                    CameraShake3D.Shake(0.4f, 0.4f);
                    HitFX.Explosion(transform.position + dir * 1.5f + Vector3.up, 1.4f, new Color(0.7f, 0.5f, 0.3f));
                    GolemAudio.Play("golem_golpe", transform.position + dir * 1.5f, 1f, 0.9f);
                    break;                                                    // choca contra un árbol o una roca
                }
                Vector3 p = transform.position + dir * speed * Time.deltaTime;
                if (RuntimeMaterials.Ground(p, out var gp)) p.y = gp.y;
                transform.position = p;
                transform.rotation = Quaternion.LookRotation(dir);
                travelled += speed * Time.deltaTime;
                g.SpinWheel(travelled);
                if (!hitPlayer && Flat(player.position - transform.position).magnitude < 2.1f)
                {
                    hitPlayer = true;
                    if (playerHealth != null) playerHealth.TakeDamage(rollDamage);
                    CameraShake3D.Shake(0.5f, 0.45f);
                }
                if (Mathf.Repeat(travelled, 3f) < speed * Time.deltaTime) CameraShake3D.Shake(0.06f, 0.15f);
                yield return null;
            }
            dust.Stop(); Destroy(dust.gameObject, 2f);
            GolemAudio.StopLoop(rollSound, 0.5f);
            yield return g.Curl(false);
            // aturdido: se tambalea con los ojos parpadeando
            g.Stagger(1f); g.SetEyes(0.35f);
            yield return new WaitForSeconds(1.4f);
            g.SetEyes(1f);
            nextRoll = Time.time + rollCooldown;
        }

        GameObject RollLane(Vector3 from, Vector3 dir, float len)
        {
            var mat = CineGlow.Make(new Color(1.8f, 0.35f, 0.12f), 0f, 1f, 0.5f);
            var lp = new LowPoly().Paint(Color.white);
            for (float d = 0f; d < len; d += 1.0f)
            {
                Vector3 p = from + dir * d;
                if (RuntimeMaterials.Ground(p, out var gp)) p = gp;
                lp.At(p + Vector3.up * 0.06f, Quaternion.LookRotation(dir), Vector3.one).Box(new Vector3(2.6f, 0.02f, 0.5f));
            }
            return lp.Spawn("CaminoDeLaRueda", null, mat, default, false);
        }

        // ------------------------------------------------------------------ momento matemático
        IEnumerator MathMoment()
        {
            inMoment = true;
            var ui = UIManager.Instance;
            // espera a terminar la acción en curso y a que no haya otra ventana abierta (reto, pista…)
            while (acting || (ui != null && ui.IsModalOpen)) { hs.SetInvulnerable(0.3f); yield return null; }
            if (ui == null) { nextThreshold -= mathStep; inMoment = false; yield break; }
            if (hs.Normalized > nextThreshold || hs.IsDead) { inMoment = false; yield break; }   // se curó o murió mientras esperaba
            if (g.IsWheel) yield return g.Curl(false);

            var problem = SumProblem.ForStage(solved);
            g.SetOperationText(problem.text.Replace(" = ?", ""));
            g.ShowOperation(true);
            g.SetCracks(2f);

            // Cámara lenta + escena oscurecida (el problema queda en primer plano)
            var focus = BossFocus.Begin();
            float prevScale = Time.timeScale;
            StartCoroutine(RampTime(1f, 0.12f, 0.8f));              // entra en cámara lenta poco a poco

            // De fondo: el mago salta intentando escapar y el gólem le lanza una roca (en cámara lenta).
            GameObject slowRock = null;
            StartCoroutine(Throw(player, true, r => slowRock = r));
            var hop = StartCoroutine(MageEscapeHops());
            Vector3 rockFrom = Vector3.zero; float rockT = 0f;

            int answer = -1;
            ui.OpenQuiz(new UIManager.QuizData
            {
                title = "¡Resuélvelo antes de que llegue la roca!",
                subtitle = "",
                question = problem.text,
                options = problem.labels,
                onTimeout = () => { if (answer < 0) answer = -3; }      // se acabó el tiempo = fallo
            }, i => { if (answer < 0) answer = i; }, () => { if (answer < 0) answer = -2; });

            // mientras se responde, la roca avanza lentísimo hacia el mago (se queda casi suspendida)
            Vector3 rockTarget = player.position;
            while (answer == -1)
            {
                hs.SetInvulnerable(0.3f);
                if (slowRock != null)
                {
                    if (rockT == 0f) { rockFrom = slowRock.transform.position; rockTarget = player.position + Vector3.up * 1.2f; }
                    rockT = Mathf.Min(0.82f, rockT + Time.unscaledDeltaTime * 0.12f);
                    slowRock.transform.position = Vector3.Lerp(rockFrom, rockTarget, rockT) + Vector3.up * Mathf.Sin(rockT * Mathf.PI) * 2.5f;
                    slowRock.transform.Rotate(30f * Time.unscaledDeltaTime, 45f * Time.unscaledDeltaTime, 0);
                }
                yield return null;
            }

            bool correct = answer == problem.correctIndex;
            if (answer >= 0)
            {
                if (correct) ui.LockQuiz(answer, $"¡Correcto! {problem.text.Replace("?", problem.answer.ToString())}");
                else ui.LockQuiz(problem.correctIndex, $"La respuesta era {problem.answer}. ¡El Gólem recupera vida!");
                yield return new WaitForSecondsRealtime(correct ? 1.0f : 1.6f);
                ui.CloseQuiz(false);
            }
            else if (answer == -3)
            {
                yield return new WaitForSecondsRealtime(1.0f);
                ui.CloseQuiz(false);
            }

            StopCoroutine(hop);
            inMoment = false;                                        // detiene la rampa de cámara lenta
            Time.timeScale = prevScale > 0.5f ? prevScale : 1f;
            focus.End();
            g.ShowOperation(false);
            g.SetCracks(1f);

            if (correct)
            {
                solved++;
                nextThreshold -= mathStep;
                if (slowRock != null)
                {
                    CineGlow.Burst(slowRock.transform.position, new Color(2.5f, 2.2f, 1f), 50, 3f, 0.12f, 1.2f, 0.4f);
                    HitFX.Explosion(slowRock.transform.position, 1f, new Color(1f, 0.9f, 0.6f));
                    Destroy(slowRock);
                }
                g.Stagger(1f);
                CameraShake3D.Shake(0.2f, 0.3f);
                ui.ShowMessage("¡Bien! El Gólem se tambalea. ¡Sigue atacando!", 3f);
            }
            else
            {
                // la roca sigue su trayectoria y el gólem se cura el tramo perdido
                if (slowRock != null)
                {
                    Vector3 land = player.position; if (RuntimeMaterials.Ground(land, out var lg)) land = lg;
                    ArcProjectile.Launch(slowRock, slowRock.transform.position, land, 0.6f, 0.8f, rockDamage * 0.75f, 2f, new Color(0.75f, 0.5f, 0.25f));
                }
                StartCoroutine(HealBack());
                ui.ShowMessage(answer == -2 ? "Huiste del problema… ¡el Gólem recupera vida!" : answer == -3 ? "¡Se acabó el tiempo! El Gólem recupera la vida perdida." : "¡Incorrecto! El Gólem recupera la vida perdida.", 3f);
            }
            inMoment = false;
        }

        IEnumerator RampTime(float from, float to, float realSeconds)
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / realSeconds)
            {
                if (!inMoment) yield break;
                Time.timeScale = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }
            if (inMoment) Time.timeScale = to;
        }

        /// Recupera el 15 % de vida perdida con un resplandor verde y los brazos abiertos.
        IEnumerator HealBack()
        {
            acting = true;
            g.SetHeal(1f);
            float target = Mathf.Min(1f, nextThreshold + mathStep) * hs.maxHealth;
            float start = hs.Current;
            var motes = CineGlow.Motes("Curacion", transform, Vector3.up * 1.2f, new Color(0.6f, 2.2f, 0.5f), 60f, 0.14f, 1.2f, 1.4f, 1.6f, -0.2f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.4f)
            {
                float want = Mathf.Lerp(start, target, t);
                if (want > hs.Current) hs.Heal(want - hs.Current);
                hs.SetInvulnerable(0.2f);
                yield return null;
            }
            g.SetHeal(0f);
            motes.Stop(); Destroy(motes.gameObject, 2f);
            acting = false;
        }

        /// En cámara lenta el mago da saltos laterales alejándose del gólem (intenta escapar).
        IEnumerator MageEscapeHops()
        {
            if (playerCC == null) yield break;
            var pc = player.GetComponent<PlayerController>();
            bool pcWas = pc != null && pc.enabled;
            if (pc != null) pc.enabled = false;
            try
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                while (true)
                {
                    Vector3 away = Flat(player.position - transform.position).normalized;
                    Vector3 lateral = Vector3.Cross(Vector3.up, away) * side;
                    Vector3 v = (away * 0.6f + lateral).normalized * 3.2f + Vector3.up * 5.5f;
                    player.rotation = Quaternion.LookRotation(Flat(v).normalized);
                    // un salto
                    do
                    {
                        v += Physics.gravity * Time.deltaTime;
                        playerCC.Move(v * Time.deltaTime);
                        yield return null;
                    } while (!playerCC.isGrounded || v.y > 0f);
                    side = -side;
                    yield return new WaitForSeconds(0.15f);
                }
            }
            finally
            {
                if (pc != null && pcWas) { pc.enabled = true; pc.ResetMotion(); }
            }
        }

        // ------------------------------------------------------------------ muerte
        void OnDied(HealthSystem _)
        {
            if (dead) return;
            dead = true;
            StopAllCoroutines();
            GolemAudio.StopLoop(ambience, 3f);
            ambience = null;
            if (Time.timeScale < 1f && !PauseMenu.IsOpen) Time.timeScale = 1f;
            BossFocus.EndAll();
            var col = GetComponent<Collider>(); if (col != null) col.enabled = false;
            var bar = GetComponent<WorldHealthBar3D>(); if (bar != null) bar.enabled = false;
            StartCoroutine(DieRoutine());
        }

        IEnumerator DieRoutine()
        {
            if (g.IsWheel) yield return g.Curl(false);
            yield return g.Crumble();
            Destroy(gameObject, 0.2f);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }

    /// Problemas de suma y resta del Nivel 1, cada vez más difíciles (etapa = problemas ya resueltos).
    public class SumProblem
    {
        public string text; public int answer; public string[] labels; public int correctIndex;

        public static SumProblem ForStage(int stage)
        {
            int a, b, c = 0; string t; int ans;
            switch (Mathf.Clamp(stage, 0, 5))
            {
                case 0: a = Random.Range(4, 13); b = Random.Range(3, 10); ans = a + b; t = $"{a} + {b} = ?"; break;
                case 1: a = Random.Range(12, 25); b = Random.Range(3, 11); ans = a - b; t = $"{a} − {b} = ?"; break;
                case 2: a = Random.Range(15, 45); b = Random.Range(12, 40); ans = a + b; t = $"{a} + {b} = ?"; break;
                case 3: a = Random.Range(40, 90); b = Random.Range(12, 38); ans = a - b; t = $"{a} − {b} = ?"; break;
                case 4: a = Random.Range(10, 30); b = Random.Range(10, 30); c = Random.Range(5, 15); ans = a + b - c; t = $"{a} + {b} − {c} = ?"; break;
                default: a = Random.Range(60, 99); b = Random.Range(20, 50); c = Random.Range(10, 30); ans = a - b + c; t = $"{a} − {b} + {c} = ?"; break;
            }
            var opts = new System.Collections.Generic.List<int> { ans };
            int[] deltas = { 1, -1, 2, -2, 10, -10, 3 };
            foreach (int d in deltas) { if (opts.Count >= 4) break; int o = ans + d; if (o >= 0 && !opts.Contains(o) && Random.value < 0.7f) opts.Add(o); }
            for (int d = 4; opts.Count < 4; d++) if (!opts.Contains(ans + d)) opts.Add(ans + d);
            for (int i = opts.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (opts[i], opts[j]) = (opts[j], opts[i]); }
            var p = new SumProblem { text = t, answer = ans, labels = new string[4] };
            for (int i = 0; i < 4; i++) { p.labels[i] = opts[i].ToString(); if (opts[i] == ans) p.correctIndex = i; }
            return p;
        }
    }
}
