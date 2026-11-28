using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Voz en off femenina (español de México, es-MX-DaliaNeural) de la cinemática de apertura y del tutorial con el Maestro Athan.
    /// Cada línea tiene un id y un texto. El audio se busca en Resources/Audio/Narracion/{id} (.wav, .mp3 u .ogg);
    /// si todavía no existe, la línea se muestra solo como subtítulo durante el tiempo estimado de lectura.
    /// Los audios se generan desde el menú de Unity: MageHero ▸ Narración ▸ Generar voces (es-MX).
    public class Narrator : MonoBehaviour
    {
        public const string ResourceFolder = "Audio/Narracion";

        /// Guion completo de la voz en off. El generador de voces del editor lee esta misma tabla.
        public static readonly (string id, string text)[] Lines =
        {
            // --- Cinemática corta (≈ 15 s)
            ("intro_01", "En la Arboleda de Otoño, un joven mago entrenaba en calma…"),
            ("intro_02", "…hasta que el cielo se rasgó y cayó el Gran Cero."),
            ("intro_03", "Era hora de tomar el báculo y convertirse en el Mago de los Números."),

            // --- Tutorial (habla el Maestro Athan)
            ("tut_00", "Tranquilo, aprendiz. Antes de enfrentarlo, recuerda lo que hemos practicado."),
            ("tut_mover", "Muévete con las teclas W, A, S y D, o con las flechas."),
            ("tut_correr", "Mantén Shift mientras avanzas para correr."),
            ("tut_saltar", "Pulsa la barra espaciadora para saltar."),
            ("tut_girar", "Gira la cámara con Q y E, o mantén el clic derecho y mueve el ratón."),
            ("tut_zoom", "Acerca o aleja la vista con la rueda del ratón, o con Z y X."),
            ("tut_mapa", "Pulsa M para abrir el mapa del bosque."),
            ("tut_mapa_cerrar", "Ahí ves dónde estás y hacia dónde ir. Ciérralo con M o con Escape."),
            ("tut_pausa", "Pulsa Escape para abrir el menú de pausa. Desde ahí puedes reiniciar, ver el mapa o cambiar de escenario."),
            ("tut_pausa_cerrar", "Cuando estés listo, pulsa Escape otra vez o elige Continuar."),
            ("tut_atacar", "Ahora, apunta al Gólem con el ratón y haz clic izquierdo para lanzar un hechizo. Acierta tres veces."),
            ("tut_bien", "¡Bien hecho!"),
            ("tut_final", "¡Excelente! Para vencerlo de verdad tendrás que resolver la operación de su pecho. Si te atascas, pulsa H para ver una pista. Sigue el camino iluminado."),
        };

        static Dictionary<string, string> table;
        static Narrator instance;

        /// Dónde se escriben los subtítulos. La cinemática lo apunta a su propia pantalla; si es null se usa el HUD.
        public static Action<string, float> SubtitleSink;
        /// Se dispara al empezar (true) y al terminar (false) cada línea (el orbe del maestro late mientras habla).
        public static event Action<bool> Speaking;

        AudioSource source;
        readonly Queue<string> queue = new Queue<string>();
        Coroutine current;
        public static bool IsSpeaking => instance != null && instance.current != null;

        public static string Text(string id)
        {
            if (table == null)
            {
                table = new Dictionary<string, string>();
                foreach (var l in Lines) table[l.id] = l.text;
            }
            return table.TryGetValue(id, out var t) ? t : id;
        }

        static Narrator Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("Narrador");
            instance = go.AddComponent<Narrator>();
            instance.source = go.AddComponent<AudioSource>();
            instance.source.playOnAwake = false;
            instance.source.spatialBlend = 0f;
            instance.source.volume = 1f;
            instance.source.priority = 0;
            instance.source.ignoreListenerPause = true;      // el maestro sigue hablando con el menú de pausa abierto (paso del tutorial)
            return instance;
        }

        /// Duración de la línea (audio real si existe; si no, tiempo de lectura del subtítulo).
        public static float Duration(string id)
        {
            var clip = Load(id);
            return clip != null ? clip.length + 0.25f : ReadingTime(Text(id));
        }

        static float ReadingTime(string text) => Mathf.Clamp(text.Length / 15f, 1.6f, 9f);

        static AudioClip Load(string id) => Resources.Load<AudioClip>(ResourceFolder + "/" + id);

        /// Pone la línea en cola (se dice cuando termina la anterior).
        public static void Say(string id) { var n = Get(); n.queue.Enqueue(id); if (n.current == null) n.current = n.StartCoroutine(n.Run()); }

        /// Corta lo que esté diciendo y dice esta línea ya.
        public static void SayNow(string id) { Stop(); Say(id); }

        /// Espera a que se diga la línea (para usar con yield return en el tutorial).
        public static IEnumerator SayAndWait(string id, bool interrupt = true)
        {
            if (interrupt) SayNow(id); else Say(id);
            yield return null;
            while (IsSpeaking) yield return null;
        }

        public static void Stop()
        {
            if (instance == null) return;
            instance.queue.Clear();
            if (instance.current != null) { instance.StopCoroutine(instance.current); instance.current = null; Speaking?.Invoke(false); }
            instance.source.Stop();
            SubtitleSink?.Invoke("", 0f);
        }

        IEnumerator Run()
        {
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                string text = Text(id);
                var clip = Load(id);
                float seconds = clip != null ? clip.length : ReadingTime(text);
                ShowSubtitle(text, seconds + 0.6f);
                Speaking?.Invoke(true);
                if (clip != null) { source.volume = GameSettings.Voice; source.clip = clip; source.Play(); }
                // tiempo real: la voz no se ralentiza con la cámara lenta de la cinemática
                for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) yield return null;
                Speaking?.Invoke(false);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            current = null;
        }

        static void ShowSubtitle(string text, float seconds)
        {
            if (SubtitleSink != null) { SubtitleSink(text, seconds); return; }
            var ui = UIManager.Instance;
            if (ui != null) ui.SetSubtitle(text, seconds);
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
