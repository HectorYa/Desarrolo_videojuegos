#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MageHero.Editor
{
    /// Genera la voz del Gran Cero (VillainVoice.Lines) en Assets/MageHero/Resources/Audio/Voces:
    ///  1) edge-tts con una voz masculina de México (es-MX-JorgeNeural), lenta y grave (requiere internet y
    ///     `pip install edge-tts`, lo mismo que la voz en off).
    ///  2) Efecto grotesco grabado en el WAV: más grave y lenta, una segunda voz una octava por debajo,
    ///     modulación metálica, distorsión y reverberación de caverna.
    public static class VillainVoiceMenu
    {
        const string OutDir = "Assets/MageHero/Resources/Audio/Voces";
        const string TmpDir = "Assets/MageHero/_VozTemporal";
        const string Voice = "es-MX-JorgeNeural";

        [MenuItem("Tools/Mage Hero/Cinemática/Generar voz del Gran Cero (grave y maléfica)")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutDir);
            Directory.CreateDirectory(TmpDir);
            int ok = 0;
            string txt = Path.Combine(Path.GetTempPath(), "magehero_grancero.txt");
            try
            {
                for (int i = 0; i < VillainVoice.Lines.Length; i++)
                {
                    var (id, text) = VillainVoice.Lines[i];
                    EditorUtility.DisplayProgressBar("Voz del Gran Cero", text, i / (float)VillainVoice.Lines.Length);
                    File.WriteAllText(txt, text, new UTF8Encoding(false));
                    string rawPath = TmpDir + "/" + id + "_raw.mp3";
                    int code = Run("python", $"-m edge_tts --voice {Voice} --rate=-4% --pitch=-30Hz --file \"{txt}\" --write-media \"{Path.GetFullPath(rawPath)}\"", out string err);
                    if (code != 0 || !File.Exists(rawPath)) { Debug.LogWarning($"[MageHero] edge-tts falló en '{id}': {err}"); continue; }
                    AssetDatabase.ImportAsset(rawPath, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (AudioImporter)AssetImporter.GetAtPath(rawPath);
                    var s = importer.defaultSampleSettings; s.loadType = AudioClipLoadType.DecompressOnLoad; importer.defaultSampleSettings = s;
                    importer.forceToMono = true;
                    importer.SaveAndReimport();
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(rawPath);
                    if (clip == null || !clip.LoadAudioData()) { Debug.LogWarning($"[MageHero] No se pudo leer el audio de '{id}'."); continue; }
                    var data = new float[clip.samples * clip.channels];
                    clip.GetData(data, 0);
                    var mono = Trim(ToMono(data, clip.channels));
                    var fx = Grotesque(mono, clip.frequency);
                    WriteWav(OutDir + "/" + id + ".wav", fx, clip.frequency);
                    ok++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.DeleteAsset(TmpDir);
                AssetDatabase.Refresh();
            }
            Debug.Log($"[MageHero] Voz del Gran Cero: {ok}/{VillainVoice.Lines.Length} líneas en {OutDir}.");
            if (ok == 0) EditorUtility.DisplayDialog("Voz del Gran Cero", "No se pudo generar la voz. Hace falta internet y edge-tts (pip install edge-tts).", "Entendido");
        }

        // ------------------------------------------------------------------ efecto
        static float[] Grotesque(float[] x, int sr)
        {
            // 1) más grave y lenta (remuestreo)
            var slow = Resample(x, 0.9f);
            // 2) segunda voz una octava por debajo (mismo ritmo) y 3) modulación metálica en anillo
            var octave = PitchShift(slow, 0.5f, sr);
            var fifth = PitchShift(slow, 0.75f, sr);
            int tail = (int)(1.2f * sr);
            var y = new float[slow.Length + tail];
            for (int i = 0; i < slow.Length; i++)
            {
                float t = i / (float)sr;
                float ring = slow[i] * Mathf.Sin(2f * Mathf.PI * 46f * t);
                y[i] = slow[i] * 0.75f + octave[i] * 0.7f + fifth[i] * 0.25f + ring * 0.3f;
            }
            // 4) distorsión suave (garganta rasposa)
            float peak = 1e-5f; for (int i = 0; i < y.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(y[i]));
            for (int i = 0; i < y.Length; i++) y[i] = (float)Math.Tanh(y[i] / peak * 2.4f);
            // quita el siseo agudo de la distorsión
            Lowpass(y, 4200f, sr);
            // 5) reverberación de caverna
            var wet = Reverb(y, sr);
            for (int i = 0; i < y.Length; i++) y[i] = y[i] * 0.8f + wet[i] * 0.42f;
            return y;
        }

        static float[] Resample(float[] x, float ratio)
        {
            int n = (int)(x.Length / ratio);
            var y = new float[n];
            for (int i = 0; i < n; i++)
            {
                float p = i * ratio; int a = (int)p; float f = p - a;
                float s0 = a < x.Length ? x[a] : 0f, s1 = a + 1 < x.Length ? x[a + 1] : 0f;
                y[i] = s0 + (s1 - s0) * f;
            }
            return y;
        }

        /// Cambio de tono sin cambiar la duración (dos lecturas de una línea de retardo que se cruzan).
        static float[] PitchShift(float[] x, float ratio, int sr)
        {
            int w = (int)(0.07f * sr);
            var y = new float[x.Length];
            float phase = 0f, step = (1f - ratio) / w;
            for (int n = 0; n < x.Length; n++)
            {
                phase += step; if (phase >= 1f) phase -= 1f;
                float pb = phase + 0.5f; if (pb >= 1f) pb -= 1f;
                y[n] = Tap(x, n - phase * w) * Mathf.Sin(Mathf.PI * phase) + Tap(x, n - pb * w) * Mathf.Sin(Mathf.PI * pb);
            }
            return y;
        }

        static float Tap(float[] x, float pos)
        {
            if (pos < 0f) return 0f;
            int a = (int)pos; float f = pos - a;
            float s0 = a < x.Length ? x[a] : 0f, s1 = a + 1 < x.Length ? x[a + 1] : 0f;
            return s0 + (s1 - s0) * f;
        }

        static void Lowpass(float[] y, float cutoff, int sr)
        {
            float k = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sr), z1 = 0f, z2 = 0f;
            for (int i = 0; i < y.Length; i++) { z1 += (y[i] - z1) * k; z2 += (z1 - z2) * k; y[i] = z2; }
        }

        /// Reverberación de Schroeder: 4 filtros peine en paralelo + 2 pasa-todo.
        static float[] Reverb(float[] x, int sr)
        {
            float[] combMs = { 37.1f, 41.1f, 47.3f, 53.9f };
            var outp = new float[x.Length];
            foreach (float ms in combMs)
            {
                int d = (int)(ms * 0.001f * sr);
                var buf = new float[d]; int idx = 0; float lp = 0f;
                for (int i = 0; i < x.Length; i++)
                {
                    float o = buf[idx];
                    lp = lp + (o - lp) * 0.45f;                       // las paredes se tragan los agudos
                    buf[idx] = x[i] + lp * 0.82f;
                    idx = (idx + 1) % d;
                    outp[i] += o * 0.25f;
                }
            }
            foreach (float ms in new[] { 5.0f, 1.7f })
            {
                int d = (int)(ms * 0.001f * sr);
                var buf = new float[d]; int idx = 0;
                for (int i = 0; i < outp.Length; i++)
                {
                    float b = buf[idx], v = outp[i];
                    float o = -v * 0.7f + b;
                    buf[idx] = v + b * 0.7f;
                    idx = (idx + 1) % d;
                    outp[i] = o;
                }
            }
            return outp;
        }

        /// Quita el silencio del principio y del final que deja el sintetizador.
        static float[] Trim(float[] x)
        {
            float peak = 1e-5f; foreach (var v in x) peak = Mathf.Max(peak, Mathf.Abs(v));
            float th = peak * 0.02f;
            int a = 0, b = x.Length - 1;
            while (a < b && Mathf.Abs(x[a]) < th) a++;
            while (b > a && Mathf.Abs(x[b]) < th) b--;
            a = Mathf.Max(0, a - 400); b = Mathf.Min(x.Length - 1, b + 2000);
            var y = new float[b - a + 1];
            Array.Copy(x, a, y, 0, y.Length);
            return y;
        }

        static float[] ToMono(float[] d, int ch)
        {
            if (ch <= 1) return d;
            var m = new float[d.Length / ch];
            for (int i = 0; i < m.Length; i++) { float s = 0f; for (int c = 0; c < ch; c++) s += d[i * ch + c]; m[i] = s / ch; }
            return m;
        }

        static void WriteWav(string path, float[] data, int sr)
        {
            float peak = 1e-5f; foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            float g = 0.92f / peak;
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = data.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(sr); w.Write(sr * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var s in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * g * 32767f), -32767, 32767));
            }
        }

        static int Run(string exe, string args, out string output)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true, RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                };
                using (var p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    output = p.StandardError.ReadToEnd();
                    p.WaitForExit(120000);
                    return p.ExitCode;
                }
            }
            catch (Exception e) { output = e.Message; return -1; }
        }
    }
}
#endif
