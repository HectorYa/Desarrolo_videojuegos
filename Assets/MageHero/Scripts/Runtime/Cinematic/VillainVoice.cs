using UnityEngine;

namespace MageHero
{
    /// Voz del Gran Cero: grave, maléfica y grotesca. Los audios (Resources/Audio/Voces/<id>.wav) los genera
    /// Tools > Mage Hero > Cinemática > Generar voz del Gran Cero, con el efecto ya grabado en el archivo
    /// (en WebGL Unity no aplica filtros de audio en tiempo real). Si falta un audio, solo queda el subtítulo.
    public static class VillainVoice
    {
        public static readonly (string id, string text)[] Lines =
        {
            ("grancero_01", "¡Ja, ja, ja! Ningún mago cruzará mi río. ¡Este puente ahora vale cero!"),
            ("grancero_02", "Si quieres pasar, resuelve mi ecuación, ¡si es que puedes!"),
        };

        static AudioSource source;

        /// Dice la línea y devuelve cuánto dura (0 si no hay audio).
        public static float Say(string id)
        {
            var clip = Resources.Load<AudioClip>("Audio/Voces/" + id);
            if (clip == null) return 0f;
            if (source == null)
            {
                var go = new GameObject("VozGranCero");
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;           // las cinemáticas pausan el resto del audio
            }
            source.Stop();
            source.clip = clip;
            source.volume = GameSettings.Voice;
            source.Play();
            return clip.length;
        }

        public static void Stop() { if (source != null) source.Stop(); }
    }
}
