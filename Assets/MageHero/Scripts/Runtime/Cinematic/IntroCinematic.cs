using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Director de la cinemática de apertura (≈ 15 s, Diseños/guion_v2.md).
    /// Se reproduce la primera vez que se entra al Nivel 1 (Arboleda de Otoño) o cuando el menú pide "Ver historia".
    /// El mago tala → cae el meteorito del Gran Cero → los animales huyen → empuña el báculo → aparece el Gólem.
    /// Una voz en off femenina (Narrator, español de México) lo cuenta con subtítulos y luego empieza el tutorial
    /// (GolemTutorial). Se puede saltar manteniendo Esc. Todo se construye por código: no hace falta tocar la escena.
    [DefaultExecutionOrder(900)]
    public class IntroCinematic : MonoBehaviour
    {
        public const string LevelScene = "Level_otono";
        public const string SeenKey = "MageHero.IntroVista";

        /// El menú lo activa para volver a ver la historia aunque ya se haya visto.
        public static bool ForceNext;
        public static bool IsPlaying { get; private set; }

        IntroSet set;
        bool play;

        // Actores
        MageRig mage;
        SpiritAthan spirit;
        StoneGolem golem;
        CinematicScreen screen;
        ParticleSystem leaves;
        GameObject smokeColumn;

        // Cámara
        Camera cam;
        IsoCameraFollow iso;
        bool rig;
        Vector3 camPos, camLook;
        float camFov = 40f, camRoll;
        Color skyColor0, fogColor0; float fogEnd0, fogStart0;

        // Estado del juego que se congela durante la cinemática
        readonly List<Behaviour> disabled = new List<Behaviour>();
        readonly List<GameObject> hidden = new List<GameObject>();
        readonly List<Behaviour> enemiesOff = new List<Behaviour>();     // spawners e IA: siguen apagados durante el tutorial
        AudioSource ambience; float ambienceVol0;
        Coroutine main;
        bool finishing;
        Quaternion? fallTreeRot0;          // rotación del árbol antes de caer (por si se salta la cinemática a mitad de la caída)

        // ------------------------------------------------------------------ arranque
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != LevelScene || GameObject.FindWithTag("Player") == null) return;
            var set = IntroSet.Build();
            var go = new GameObject("IntroCinematic");
            var ic = go.AddComponent<IntroCinematic>();
            ic.set = set;
            ic.play = (ForceNext || PlayerPrefs.GetInt(SeenKey, 0) == 0) && !SaveGame.Restoring;   // al continuar partida no se repite
            ForceNext = false;
        }

        IEnumerator Start()
        {
            if (!play)
            {
                // Ya vista: el nivel empieza directamente (el Gólem espera como jefe al final del camino iluminado).
                yield break;
            }
            IsPlaying = true;
            screen = CinematicScreen.Create();          // negro desde el primer fotograma
            Narrator.SubtitleSink = screen.ShowCaption;
            yield return null;                          // deja que el HUD, el combate y la progresión se construyan
            yield return null;
            Freeze();
            main = StartCoroutine(Run());
        }

        void Update()
        {
            if (IsPlaying && !finishing && screen != null && screen.SkipRequested) StartCoroutine(SkipRoutine());
        }

        // ------------------------------------------------------------------ guion
        IEnumerator Run()
        {
            yield return ShortCinematic();
            yield return Handoff(false);
        }

        // ================================================================== CINEMÁTICA CORTA (≈ 15 s)
        // 0–3 s    El mago tala y parte el tronco.
        // 3–6 s    Mira al cielo del sur: cae el meteorito del Gran Cero (destello, temblor, columna de humo).
        // 6–8.5 s  Las aves y los animales escapan del bosque.
        // 8.5–11.5 Suelta el hacha y empuña el báculo, que se enciende.
        // 11.5–15  Camina hacia el sendero; un árbol cae y aparece el Gólem de Piedra (título encima).
        float cineStart;
        IEnumerator ShortCinematic()
        {
            cineStart = Time.time;
            Vector3 S = set.stumpTop;
            Vector3 M = set.mageMark;
            screen.SetBars(1f);
            screen.SetGrade(18f, new Color(1f, 0.93f, 0.82f), 16f, 0.08f);

            // --- 1 · Talando (plano medio desde un lado, se acerca un poco)
            mage.HoldAxe(set.axe);
            // de frente y en diagonal: se ven el mago entero, el hacha y el tronco sobre el tocón
            Vector3 mid = (M + S) * 0.5f; mid.y = M.y + 0.95f;
            CamCut(M + new Vector3(3.4f, 1.8f, 3.6f), mid, 36f);
            StartCoroutine(CamMove(M + new Vector3(2.9f, 1.6f, 3.1f), mid, 3.4f, 34f));
            screen.SetFocus(4.4f, 3f, 55f);
            StartCoroutine(screen.Fade(Color.black, 0f, 0.7f));
            Narrator.Say("intro_01");
            yield return W(0.5f);
            yield return mage.ChopRaise(0.5f);
            yield return mage.ChopSwing(0.1f);
            SplitLog();
            CameraShake3D.Shake(0.05f, 0.15f);
            yield return W(2.2f);

            // --- 2 · El meteorito (contrapicado detrás del mago, mirando al cielo del sur)
            mage.ArmsRest();
            mage.Face(M + Vector3.back * 10f);
            screen.SetDepthOfField(false);
            // contrapicado bajo y a un lado: el mago queda en el borde del encuadre y el cielo del sur, libre
            Vector3 eye = M + new Vector3(1.7f, 0.75f, 1.3f);
            CamCut(eye, M + new Vector3(-4f, 10f, -32f), 52f);
            StartCoroutine(FogTo(360f, 1.5f));
            SkyToCrimson(1.2f);
            StartCoroutine(Rumble(2.6f, 0.05f));
            Narrator.Say("intro_02");
            var meteor = Meteor();
            Vector3 m0 = new Vector3(60f, 190f, -330f), m1 = new Vector3(28f, 4f, -175f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.7f)
            {
                meteor.position = Vector3.Lerp(m0, m1, t * t);
                meteor.Rotate(140f * Time.deltaTime, 90f * Time.deltaTime, 0);
                yield return null;
            }
            Destroy(meteor.gameObject, 0.1f);
            screen.Flash(Color.white, 0.9f);
            CameraShake3D.Shake(0.6f, 0.9f);
            BuildSmokeColumn(0f);
            StartCoroutine(GrowSmoke(4f));
            yield return W(1.1f);
            screen.SetDepthOfField(true);

            // --- 3 · Los animales escapan (plano bajo junto al mago)
            CamCut(M + new Vector3(-2.6f, 0.6f, 2.4f), M + new Vector3(0.6f, 1.6f, -1.2f), 42f);
            screen.SetFocus(3.4f, 3.5f, 50f);
            CineCritters.Flock(this, M + new Vector3(-1.5f, 6f, -10f), new Vector3(0.2f, 0f, 1f), 24, 9f);
            StartCoroutine(CineCritters.Run(CineCritters.Fox(), M + new Vector3(-6f, 0, -3.5f), M + new Vector3(9f, 0, 3.5f), 8.5f, 6f));
            StartCoroutine(CineCritters.Run(CineCritters.Deer(), M + new Vector3(-7f, 0, -5f), M + new Vector3(9f, 0, 2.5f), 9.5f, 6f));
            spirit.SetUnease(0.8f);
            yield return W(1.8f);

            // --- 4 · Suelta el hacha y empuña el báculo (corte a la puerta de la cabaña, órbita corta)
            StartCoroutine(mage.DropAxe(set.axe));
            yield return W(0.3f);
            Vector3 grab = set.staffProp.position + set.cabin.forward * 0.7f; grab.y = Ground(grab);   // delante de la puerta (antes quedaba dentro de la cabaña)
            mage.Teleport(grab, set.staffProp.position - grab);
            Vector3 c0 = mage.Root.position;
            // órbita de perfil por el lado sur (fuera de la cabaña): se ven el mago, la puerta y el báculo encendiéndose
            StartCoroutine(CamOrbit(c0 + Vector3.up * 1.1f, 3.4f, 1.0f, 195f, 240f, 2.8f, 42f));
            screen.SetFocus(2.6f, 2.4f, 55f);
            Narrator.Say("intro_03");
            yield return W(0.3f);
            mage.TakeStaff(set.staffProp);
            StaffAwakening();
            yield return W(2.2f);

            // --- 5 · Hacia el sendero: cae un árbol y aparece el Gólem
            Vector3 fight = set.mageMark + new Vector3(0.8f, 0f, -1.2f); fight.y = Ground(fight);
            StartCoroutine(mage.WalkTo(fight, 2.2f));
            Vector3 toG = set.golemStop - fight; toG.y = 0; toG.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, toG);
            Vector3 sp = fight + right * 1.3f - toG * 0.2f; sp.y = Ground(sp);
            spirit.SetUnease(0.3f);
            StartCoroutine(spirit.GlideTo(sp, 2.6f, set.golemStop));
            StartCoroutine(CamFollow(() => mage.Root.position - toG * 3.8f + right * -1.3f + Vector3.up * 2.7f,
                                     () => set.golemStop + Vector3.up * 2.0f, 4f, 44f));
            screen.SetFocus(7f, 5f, 45f);
            // El Gólem sale de detrás del árbol caído, ya en el borde del bosque (cerca del prado)
            Vector3 from = Vector3.Lerp(set.golemFrom, set.golemStop, 0.22f); from.y = Ground(from);   // justo detrás del árbol que cae
            golem = StoneGolem.Create(from, Quaternion.LookRotation(toG * -1f));
            golem.SetEyes(0f, true); golem.SetCracks(0.6f); golem.ShowOperation(true);
            StartCoroutine(TreeFall());
            yield return W(0.9f);
            StartCoroutine(golem.WalkTo(set.golemStop, 2.4f));
            golem.SetEyes(1f); golem.SetCracks(1.4f);
            StartCoroutine(screen.ShowTitle("MAGO DE LOS NÚMEROS", "", 0.5f, 1.4f, 0.6f));
            yield return W(1.4f);
            golem.LookAt(mage.Head);
            mage.Face(set.golemStop);
            spirit.Look(golem.Head.position);
            yield return W(0.6f);
        }

        // ================================================================== PASO AL JUEGO
        IEnumerator Handoff(bool skipped)
        {
            // La cámara se aleja hasta el encuadre de juego (detrás del mago, mirando al Gólem).
            finishing = true;
            screen.AllowSkip = false;
            Vector3 toG = set.golemStop - mage.Root.position; toG.y = 0;
            if (iso != null)
            {
                iso.yaw = Quaternion.LookRotation(toG.normalized).eulerAngles.y;
                iso.GoalPose(out Vector3 gp, out Quaternion gr);
                Vector3 gl = gp + gr * Vector3.forward * 10f;
                StartCoroutine(CamMove(gp, gl, skipped ? 0.01f : 1.6f, iso.fieldOfView, Ease.InOut));
            }
            StartCoroutine(SkyRestore(3f));
            StartCoroutine(FogTo(fogEnd0, 3f));
            StartCoroutine(screen.Release(skipped ? 0.6f : 1.6f));
            spirit.Reach(null);
            // Espera a que se retiren barras, fundido y post-procesado antes de destruir la pantalla de cine.
            // (Antes, al saltar, se destruía a los 0.1 s con Fade/Release aún en marcha → MissingReferenceException.)
            float minWait = skipped ? 0.85f : 1.65f;
            for (float t = 0; t < minWait || (t < 4f && !screen.Released); t += Time.unscaledDeltaTime) yield return null;
            Finish();
        }

        IEnumerator SkipRoutine()
        {
            finishing = true;
            screen.AllowSkip = false;
            if (main != null) StopCoroutine(main);
            Time.timeScale = 1f;
            Narrator.Stop();
            yield return screen.Fade(Color.black, 1f, 0.35f);
            StopAllCoroutines();
            StartCoroutine(SkipFinish());
        }

        IEnumerator SkipFinish()
        {
            // Estado final del mundo, como si la cinemática hubiera terminado.
            set.logWhole.gameObject.SetActive(false);
            set.logHalfA.gameObject.SetActive(true); set.logHalfB.gameObject.SetActive(true);
            if (set.fallTree != null && set.fallTree.gameObject.activeSelf && !ForestTrees.IsToppled(set.fallTree))
            {
                set.fallTree.rotation = Quaternion.AngleAxis(-88f, Vector3.forward) * (fallTreeRot0 ?? set.fallTree.rotation);
                ForestTrees.MarkToppled(set.fallTree);
            }
            foreach (var t in set.pathTrees) ForestTrees.ToppleInstant(t, ForestTrees.AwayFromPath(t, set.golemFrom, set.golemStop));
            if (golem == null) golem = StoneGolem.Create(set.golemStop, Quaternion.identity);
            golem.StopAllCoroutines();
            golem.transform.position = set.golemStop;
            golem.SetWalk(0f); golem.SetEyes(1f, true); golem.SetCracks(1f); golem.ShowOperation(true);
            Time.timeScale = 1f;
            if (smokeColumn == null) { BuildSmokeColumn(1f); }
            SkyToCrimson(0.01f);
            var lm = leaves.main; lm.simulationSpeed = 1f;
            Vector3 fight = set.mageMark + new Vector3(0.8f, 0f, -1.2f); fight.y = Ground(fight);
            mage.Teleport(fight, set.golemStop - fight);
            mage.DropAxeInstant(set.axe);
            mage.TakeStaff(set.staffProp);
            Vector3 toG = set.golemStop - fight; toG.y = 0; toG.Normalize();
            Vector3 sp = fight + Vector3.Cross(Vector3.up, toG) * 1.3f; sp.y = Ground(sp);
            spirit.StopAllCoroutines();
            spirit.transform.position = sp; spirit.SetUnease(0.2f);
            golem.transform.rotation = Quaternion.LookRotation(-toG);
            golem.LookAt(mage.Head);
            SetAmbience(ambienceVol0, 0.5f);
            yield return null;
            StartCoroutine(screen.Fade(Color.black, 0f, 0.8f));
            yield return Handoff(true);
        }

        /// Devuelve el control al jugador y deja el mundo listo para el primer combate.
        void Finish()
        {
            if (cineStart > 0f) Debug.Log($"[MageHero] Cinemática de apertura: {Time.time - cineStart:0.0} s hasta el tutorial.");
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            rig = false;
            Transform playerT = mage.Root;
            mage.Release();
            mage = null;
            Narrator.Stop();
            Narrator.SubtitleSink = null;
            foreach (var b in disabled) if (b != null) b.enabled = true;
            disabled.Clear();
            // Los enemigos siguen apagados/ocultos hasta que termine el tutorial.
            var enemies = new List<Behaviour>(enemiesOff); var hiddenEnemies = new List<GameObject>(hidden);
            enemiesOff.Clear(); hidden.Clear();
            if (iso != null) { iso.Snap(); iso.enabled = true; }
            if (leaves != null) { var lm = leaves.main; lm.simulationSpeed = 1f; }
            SetAmbience(ambienceVol0, 1f);
            var ui = UIManager.Instance;
            if (ui != null) ui.EndCinematic();
            GolemTutorial.Begin(golem, spirit, playerT, set.golemFrom, () =>
            {
                foreach (var b in enemies) if (b != null) b.enabled = true;
                foreach (var g in hiddenEnemies) if (g != null) g.SetActive(true);
            });
            IsPlaying = false;
            if (screen != null) Destroy(screen.gameObject, 0.1f);
            Destroy(this, 0.2f);
        }

        // ------------------------------------------------------------------ preparación
        void Freeze()
        {
            cam = Camera.main;
            iso = cam != null ? cam.GetComponent<IsoCameraFollow>() : null;
            skyColor0 = cam.backgroundColor; fogColor0 = RenderSettings.fogColor; fogEnd0 = RenderSettings.fogEndDistance; fogStart0 = RenderSettings.fogStartDistance;
            Off(iso);
            foreach (var s in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) OffEnemy(s);
            foreach (var ai in FindObjectsByType<EnemyAI3D>(FindObjectsSortMode.None))
            {
                OffEnemy(ai);
                if (Vector3.Distance(ai.transform.position, set.stumpTop) < 26f && ai.gameObject.activeSelf) { ai.gameObject.SetActive(false); hidden.Add(ai.gameObject); }
            }
            // HUD: se ocultan todos los lienzos de pantalla (menos el de la cinemática).
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && c.gameObject != screen.gameObject) Off(c);
            var ui = UIManager.Instance;
            if (ui != null) ui.BeginCinematic();

            var li = FindAnyObjectByType<LevelInfo>();
            ambience = li != null ? li.GetComponent<AudioSource>() : null;
            ambienceVol0 = ambience != null ? ambience.volume : 0.35f;

            var player = GameObject.FindWithTag("Player");
            mage = new MageRig(player.transform, this, disabled);
            mage.Teleport(set.mageMark, set.mageFacing);
            Vector3 toM = set.mageMark - set.spiritSeat; toM.y = 0;
            spirit = SpiritAthan.Create(set.spiritSeat - Vector3.up * 0.28f, Quaternion.LookRotation(toM.normalized));
            spirit.Look(set.mageMark + Vector3.up * 1.4f);
            leaves = CineCritters.Leaves(set.stumpTop, 16f);
            rig = true;
        }

        void Off(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; disabled.Add(b); } }
        void OffEnemy(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; enemiesOff.Add(b); } }

        // ------------------------------------------------------------------ cámara
        enum Ease { Linear, InOut, In, Out }
        static float E(Ease e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Ease.InOut: return t * t * (3f - 2f * t);
                case Ease.In: return t * t;
                case Ease.Out: return 1f - (1f - t) * (1f - t);
                default: return t;
            }
        }

        int camToken;
        void CamCut(Vector3 pos, Vector3 look, float fov) { camToken++; camPos = pos; camLook = look; camFov = fov; }

        IEnumerator CamMove(Vector3 pos, Vector3 look, float seconds, float fov, Ease e = Ease.InOut)
        {
            int token = ++camToken;
            Vector3 p0 = camPos, l0 = camLook; float f0 = camFov;
            for (float t = 0; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                if (token != camToken) yield break;
                float k = E(e, t);
                camPos = Vector3.Lerp(p0, pos, k); camLook = Vector3.Lerp(l0, look, k); camFov = Mathf.Lerp(f0, fov, k);
                yield return null;
            }
            if (token == camToken) { camPos = pos; camLook = look; camFov = fov; }
        }

        IEnumerator CamFollow(Func<Vector3> pos, Func<Vector3> look, float seconds, float fov)
        {
            int token = ++camToken;
            Vector3 p0 = camPos, l0 = camLook; float f0 = camFov;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (token != camToken) yield break;
                float k = E(Ease.InOut, Mathf.Clamp01(t / 0.8f));
                camPos = Vector3.Lerp(p0, pos(), k); camLook = Vector3.Lerp(l0, look(), k); camFov = Mathf.Lerp(f0, fov, k);
                yield return null;
            }
        }

        /// Órbita alrededor de un punto (contrapicado que gira alrededor del mago).
        IEnumerator CamOrbit(Vector3 center, float radius, float height, float fromYaw, float toYaw, float seconds, float fov)
        {
            int token = ++camToken;
            camFov = fov;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                if (token != camToken) yield break;
                float yaw = Mathf.Lerp(fromYaw, toYaw, E(Ease.InOut, t)) * Mathf.Deg2Rad;
                camPos = center + new Vector3(Mathf.Sin(yaw) * radius, height - 0.9f, Mathf.Cos(yaw) * radius);
                camLook = center + Vector3.up * 0.25f;
                yield return null;
            }
        }

        IEnumerator FocusTo(float distance, float aperture, float seconds)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                screen.SetFocus(Mathf.Lerp(3f, distance, t), aperture);
                yield return null;
            }
        }

        void LateUpdate()
        {
            if (mage != null) mage.Apply(Time.deltaTime);
            if (!rig || cam == null) return;
            cam.transform.position = camPos;
            Vector3 d = camLook - camPos;
            if (d.sqrMagnitude > 1e-6f) cam.transform.rotation = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(0, 0, camRoll);
            cam.fieldOfView = camFov;
        }

        // ------------------------------------------------------------------ efectos de la escena
        WaitForSeconds W(float s) => new WaitForSeconds(s);

        IEnumerator SlowMo(float scale, float realSeconds)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = 1f;
        }

        void SplitLog()
        {
            set.logWhole.gameObject.SetActive(false);
            set.logHalfA.gameObject.SetActive(true); set.logHalfB.gameObject.SetActive(true);
            set.logHalfA.position = set.stumpTop + new Vector3(-0.09f, 0, 0); set.logHalfA.rotation = Quaternion.identity;
            set.logHalfB.position = set.stumpTop + new Vector3(0.09f, 0, 0); set.logHalfB.rotation = Quaternion.Euler(0, 180, 0);
            StartCoroutine(Topple(set.logHalfA, Vector3.left, 1f));
            StartCoroutine(Topple(set.logHalfB, Vector3.right, -1f));
            Chips(set.stumpTop + Vector3.up * 0.3f, 14);
        }

        /// Media leña que cae hacia un lado desde el tocón y rueda un poco.
        IEnumerator Topple(Transform t, Vector3 dir, float sign)
        {
            Vector3 p0 = t.position; Quaternion r0 = t.rotation;
            float ground = Ground(p0 + dir * 0.6f);
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.55f)
            {
                float e = E(Ease.In, k);
                t.position = new Vector3(p0.x + dir.x * 0.55f * e, Mathf.Lerp(p0.y, ground + 0.12f, e), p0.z + dir.z * 0.55f * e);
                t.rotation = Quaternion.AngleAxis(sign * 90f * e, Vector3.forward) * r0;
                yield return null;
            }
        }

        /// Astillas de madera que saltan (pequeños trozos con trayectoria balística).
        void Chips(Vector3 at, int count)
        {
            var rng = new System.Random(Time.frameCount);
            for (int i = 0; i < count; i++)
            {
                var lp = new LowPoly().Paint(MenuStyle.Hex(0xd9b07a));
                lp.Box(new Vector3(0.09f, 0.025f, 0.035f));
                var go = lp.Spawn("Astilla", null, IntroSet.VertexToon(), default, false);
                go.transform.position = at;
                Vector3 v = new Vector3(((float)rng.NextDouble() - .5f) * 3f, 1.5f + (float)rng.NextDouble() * 2.5f, ((float)rng.NextDouble() - .5f) * 3f);
                StartCoroutine(Ballistic(go.transform, v, (float)rng.NextDouble() * 720f));
            }
        }

        IEnumerator Ballistic(Transform t, Vector3 v, float spin)
        {
            float ground = Ground(t.position);
            while (t != null && t.position.y > ground + 0.02f)
            {
                v += Physics.gravity * Time.deltaTime;
                t.position += v * Time.deltaTime;
                t.Rotate(spin * Time.deltaTime, spin * 0.7f * Time.deltaTime, 0);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject, 6f);
        }

        IEnumerator TreeFall()
        {
            if (set.fallTree == null) { yield return W(1f); yield break; }
            Transform tree = set.fallTree;
            // Primero tiembla (algo lo empuja desde dentro del bosque), luego cae hacia el este.
            Vector3 p0 = tree.position; Quaternion r0 = tree.rotation;
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                tree.position = p0 + new Vector3(Mathf.Sin(t * 60f) * 0.06f, 0, Mathf.Cos(t * 47f) * 0.05f);
                yield return null;
            }
            tree.position = p0;
            CameraShake3D.Shake(0.1f, 0.3f);
            fallTreeRot0 = r0;
            // El Gólem se abre paso: los demás árboles de su ruta caen hacia los lados (ya no los atraviesa).
            for (int i = 0; i < set.pathTrees.Count; i++)
            {
                var pt = set.pathTrees[i];
                ForestTrees.StartTopple(pt, ForestTrees.AwayFromPath(pt, set.golemFrom, set.golemStop), 1.0f, 0.15f + i * 0.22f);
            }
            Vector3 axis = Vector3.forward;              // gira sobre Z: la copa cae hacia +X (este), cruzando el sendero del Gólem
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.1f)
            {
                float e = t * t * t;                     // cada vez más rápido
                tree.rotation = Quaternion.AngleAxis(-88f * e, axis) * r0;
                yield return null;
            }
            tree.rotation = Quaternion.AngleAxis(-88f, axis) * r0;
            ForestTrees.MarkToppled(tree);
            CameraShake3D.Shake(0.35f, 0.5f);
            Dust(p0 + new Vector3(3.5f, 0.4f, 0), 60, 2.2f);
            Dust(p0 + new Vector3(1.5f, 0.4f, 0), 30, 1.6f);
            // rebote
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
            {
                tree.rotation = Quaternion.AngleAxis(-88f + Mathf.Sin(t * Mathf.PI) * 4f, axis) * r0;
                yield return null;
            }
        }

        void Dust(Vector3 at, int count, float size)
        {
            var ps = CineGlow.Motes("Polvo", null, at, new Color(0.42f, 0.36f, 0.28f), 0f, size, 2.4f, 2.4f, 0.8f, -0.01f);
            ps.Emit(count);
            Destroy(ps.gameObject, 3f);
        }

        void PulseStaffProp()
        {
            var cr = set.staffProp.Find("Crystal");
            Vector3 at = cr != null ? cr.GetComponent<Renderer>().bounds.center : set.staffProp.position + Vector3.up * 1.9f;
            CineGlow.Burst(at, new Color(0.6f, 1.6f, 2.6f), 16, 0.5f, 0.1f, 1.2f, -0.1f);
        }

        /// Números dorados que orbitan la punta del báculo y un estallido de luz blanca.
        void StaffAwakening()
        {
            Vector3 tip = mage.CrystalPos;
            string[] syms = { "1", "2", "3", "+", "−", "×", "÷" };
            for (int i = 0; i < syms.Length; i++)
            {
                var gn = CineGlow.Number(syms[i], null, tip, new Color(2.8f, 2.0f, 0.7f), 0.28f);
                StartCoroutine(OrbitNumber(gn, tip, i / (float)syms.Length * Mathf.PI * 2f, 0.15f * i));
            }
            StartCoroutine(TipFlash(1.0f));
        }

        IEnumerator OrbitNumber(GlowNumber gn, Vector3 center, float angle, float delay)
        {
            yield return W(delay);
            gn.FadeTo(1f, 4f);
            for (float t = 0; t < 2.6f; t += Time.deltaTime)
            {
                center = mage.CrystalPos;
                float a = angle + t * 2.4f, r = 0.35f + t * 0.25f;
                gn.transform.position = center + new Vector3(Mathf.Cos(a) * r, 0.2f + t * 0.35f, Mathf.Sin(a) * r);
                if (t > 1.8f) gn.FadeTo(0f, 2f);
                yield return null;
            }
            Destroy(gn.gameObject);
        }

        IEnumerator TipFlash(float delay)
        {
            yield return W(delay);
            Vector3 tip = mage.CrystalPos;
            screen.Flash(new Color(1f, 1f, 1f, 0.75f), 0.9f);
            CineGlow.Burst(tip, new Color(2.5f, 2.8f, 3.2f), 70, 2.6f, 0.1f, 1.4f, 0f);
            screen.SetBloom(2.2f);
            var lg = new GameObject("LuzBaculo").AddComponent<Light>();
            lg.type = LightType.Point; lg.color = new Color(0.85f, 0.95f, 1f); lg.range = 7f; lg.intensity = 6f;
            lg.transform.position = tip;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.4f)
            {
                lg.transform.position = mage.CrystalPos;
                lg.intensity = Mathf.Lerp(6f, 1.2f, t);
                screen.SetBloom(Mathf.Lerp(2.2f, 1.1f, t));
                yield return null;
            }
            lg.transform.SetParent(mage.CrystalTransform, true);
            Destroy(lg.gameObject, 6f);
        }

        // ---- cielo, humo y onda
        void SkyToCrimson(float seconds) => StartCoroutine(SkyLerp(new Color(0.93f, 0.62f, 0.48f), new Color(0.9f, 0.6f, 0.5f), seconds));
        IEnumerator SkyRestore(float seconds) { yield return SkyLerp(skyColor0, fogColor0, seconds); }

        IEnumerator SkyLerp(Color sky, Color fog, float seconds)
        {
            Color s0 = cam.backgroundColor, f0 = RenderSettings.fogColor;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                cam.backgroundColor = Color.Lerp(s0, sky, t);
                RenderSettings.fogColor = Color.Lerp(f0, fog, t);
                yield return null;
            }
            cam.backgroundColor = sky; RenderSettings.fogColor = fog;
        }

        IEnumerator FogTo(float end, float seconds)
        {
            float e0 = RenderSettings.fogEndDistance;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / seconds) { RenderSettings.fogEndDistance = Mathf.Lerp(e0, end, t); yield return null; }
            RenderSettings.fogEndDistance = end;
        }

        IEnumerator Rumble(float seconds, float amp)
        {
            for (float t = 0; t < seconds; t += 0.1f) { CameraShake3D.Shake(amp, 0.15f); yield return W(0.1f); }
        }

        Transform Meteor()
        {
            var t = new GameObject("Meteorito").transform;
            var core = new LowPoly().Paint(MenuStyle.Hex(0x2a2220));
            core.Rock(5.5f, 66, 0.25f);
            core.Spawn("Roca", t, RuntimeMaterials.Toon(MenuStyle.Hex(0x2a2220)), default, false);
            var fire = new LowPoly().Paint(Color.white);
            fire.Rock(7f, 67, 0.3f);
            fire.Spawn("Fuego", t, CineGlow.Make(new Color(4f, 1.4f, 0.3f), 1.5f, 0.2f, 0.4f), default, false);
            var trail = CineGlow.Motes("Estela", t, Vector3.zero, new Color(4f, 1.3f, 0.25f), 220f, 6f, 1.5f, 1.6f, 4f, 0f);
            var tm = trail.main; tm.startSize = new ParticleSystem.MinMaxCurve(4f, 9f);
            var smoke = CineGlow.Motes("Humo", t, Vector3.zero, new Color(0.25f, 0.18f, 0.15f), 120f, 10f, 0.8f, 3f, 4f, 0f);
            var sm = smoke.main; sm.startSize = new ParticleSystem.MinMaxCurve(8f, 16f);
            return t;
        }

        void BuildSmokeColumn(float grown)
        {
            if (smokeColumn != null) return;
            Vector3 at = new Vector3(28f, 0f, -175f);
            smokeColumn = new GameObject("ColumnaDeHumo_GranCero");
            smokeColumn.transform.position = at;
            var sh = Shader.Find("MageHero/Smoke");
            var lp = new LowPoly().Paint(Color.white);
            lp.Prism(14, 10f, 26f, 150f, false, 0.6f);
            Material mat;
            if (sh != null)
            {
                mat = new Material(sh);
                mat.SetColor("_BaseColor", new Color(0.12f, 0.08f, 0.08f, 0.9f));
                mat.SetColor("_EdgeColor", new Color(1f, 0.3f, 0.08f, 1f));
                mat.SetFloat("_EdgeStrength", 1.4f);
                mat.SetFloat("_NoiseScale", 0.05f);
                mat.SetFloat("_NoiseSpeed", 0.06f);
                mat.SetFloat("_Dissolve", 0.25f);
                mat.SetFloat("_Wobble", 2.5f);
                mat.SetFloat("_Soft", 2f);
            }
            else mat = RuntimeMaterials.Toon(new Color(0.15f, 0.1f, 0.1f));
            var go = lp.Spawn("Humo", smokeColumn.transform, mat, default, false);
            var glow = new LowPoly().Paint(Color.white);
            glow.At(Vector3.zero, Quaternion.identity, new Vector3(1f, 0.35f, 1f)).Rock(30f, 8, 0.2f);
            glow.Spawn("ResplandorLava", smokeColumn.transform, CineGlow.Make(new Color(2.2f, 0.5f, 0.1f), 1.2f, 0.25f, 0.3f), default, false);
            smokeColumn.transform.localScale = new Vector3(1f, Mathf.Max(0.02f, grown), 1f);
        }

        IEnumerator GrowSmoke(float seconds)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                if (smokeColumn != null) smokeColumn.transform.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, t), Mathf.Lerp(0.02f, 1f, E(Ease.Out, t)), Mathf.Lerp(0.6f, 1f, t));
                yield return null;
            }
        }

        void SetAmbience(float vol, float seconds) { if (ambience != null) StartCoroutine(AmbienceTo(vol, seconds)); }
        IEnumerator AmbienceTo(float vol, float seconds)
        {
            float v0 = ambience.volume;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds)) { ambience.volume = Mathf.Lerp(v0, vol, t); yield return null; }
            ambience.volume = vol;
        }

        public static float Ground(Vector3 p)
        {
            return RuntimeMaterials.Ground(p, out Vector3 hit) ? hit.y : p.y;
        }

        void OnDestroy()
        {
            IsPlaying = false;
            if (screen != null && Narrator.SubtitleSink == (Action<string, float>)screen.ShowCaption) Narrator.SubtitleSink = null;
            if (Time.timeScale < 1f) Time.timeScale = 1f;
        }
    }
}
