#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MageHero.Editor
{
    /// Sintetiza los sonidos del Gólem de Piedra (graves, rocosos, destructivos) y los guarda como WAV en
    /// Assets/MageHero/Resources/Audio/Golem. Son archivos normales: funcionan igual en WebGL, donde Unity no
    /// aplica filtros de audio en tiempo real.
    ///  golem_paso · golem_golpe · golem_arrancar · golem_lanzar · golem_pilares · golem_impacto · golem_quiebre
    ///  golem_derrumbe · golem_rugido · golem_rueda (bucle: tierra que rueda y se amontona) · golem_ambiente (bucle)
    public static class GolemSoundSynth
    {
        const string OutDir = "Assets/MageHero/Resources/Audio/Golem";
        const int SR = 32000;

        [MenuItem("Tools/Mage Hero/Audio/Generar sonidos del Gólem")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutDir);
            var rng = new System.Random(1234);
            Save("golem_paso", Paso(rng));
            Save("golem_golpe", Golpe(rng));
            Save("golem_arrancar", Arrancar(rng));
            Save("golem_lanzar", Lanzar(rng));
            Save("golem_pilares", Pilares(rng));
            Save("golem_impacto", Impacto(rng));
            Save("golem_quiebre", Quiebre(rng));
            Save("golem_derrumbe", Derrumbe(rng));
            Save("golem_rugido", Rugido(rng));
            Save("golem_rueda", Loop(Rueda(rng, 2.4f), 0.25f));
            Save("golem_ambiente", Loop(Ambiente(rng, 9f), 1.2f));
            AssetDatabase.Refresh();
            Debug.Log("[MageHero] Sonidos del Gólem generados en " + OutDir);
        }

        // ------------------------------------------------------------------ sonidos
        static float[] Paso(System.Random r)
        {
            var b = Buf(0.7f);
            Thump(b, 0f, 75f, 36f, 0.32f, 1f);
            Burst(b, r, 0f, 0.22f, 380f, 0.55f, true);
            Gravel(b, r, 0.02f, 0.45f, 26, 0.18f);
            return b;
        }

        static float[] Golpe(System.Random r)
        {
            var b = Buf(2.2f);
            Crack(b, r, 0f, 0.9f);
            Thump(b, 0f, 95f, 28f, 0.7f, 1.1f);
            Burst(b, r, 0f, 0.6f, 900f, 0.8f, true);
            Rumble(b, r, 0.02f, 1.9f, 110f, 0.55f);
            Gravel(b, r, 0.08f, 1.6f, 120, 0.3f);
            for (int i = 0; i < 5; i++) Thump(b, 0.25f + (float)r.NextDouble() * 1.1f, 120f, 60f, 0.12f, 0.25f);
            return b;
        }

        static float[] Arrancar(System.Random r)
        {
            var b = Buf(1.2f);
            Grind(b, r, 0f, 0.95f, 260f, 0.7f, 9f);
            Gravel(b, r, 0.05f, 1.0f, 90, 0.28f);
            Crack(b, r, 0.82f, 0.6f);
            Thump(b, 0.84f, 110f, 55f, 0.2f, 0.6f);
            return b;
        }

        static float[] Lanzar(System.Random r)
        {
            var b = Buf(1.1f);
            // silbido de una roca enorme cortando el aire (barrido de un filtro resonante)
            var bp = new Biquad();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR, k = t / 1.1f;
                float f = 220f + 900f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.3f));
                bp.Bandpass(f, 2.2f);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.15f));
                b[i] += bp.Process(Noise(r)) * env * 1.3f;
            }
            Grunt(b, 0f, 0.35f, 52f, 0.5f);
            return b;
        }

        static float[] Pilares(System.Random r)
        {
            var b = Buf(1.4f);
            Thump(b, 0f, 80f, 40f, 0.35f, 0.9f);
            Grind(b, r, 0f, 0.5f, 420f, 0.6f, 14f);
            Crack(b, r, 0.02f, 0.5f);
            Gravel(b, r, 0.05f, 1.2f, 70, 0.3f);
            Rumble(b, r, 0f, 1.2f, 90f, 0.35f);
            return b;
        }

        static float[] Impacto(System.Random r)
        {
            var b = Buf(0.6f);
            Crack(b, r, 0f, 0.8f);
            Thump(b, 0f, 170f, 85f, 0.14f, 0.7f);
            Gravel(b, r, 0.02f, 0.5f, 22, 0.22f);
            return b;
        }

        static float[] Quiebre(System.Random r)
        {
            var b = Buf(1.6f);
            Crack(b, r, 0f, 1f);
            Crack(b, r, 0.09f, 0.7f);
            Grind(b, r, 0.05f, 0.35f, 600f, 0.4f, 20f);
            Thump(b, 0.62f, 90f, 40f, 0.3f, 0.9f);
            Gravel(b, r, 0.62f, 1.5f, 50, 0.3f);
            return b;
        }

        static float[] Derrumbe(System.Random r)
        {
            var b = Buf(4f);
            Rumble(b, r, 0f, 3.8f, 140f, 0.8f);
            Grind(b, r, 0f, 2.2f, 300f, 0.45f, 6f);
            for (int i = 0; i < 16; i++)
            {
                float t = (float)r.NextDouble() * 3f;
                Thump(b, t, 70f + (float)r.NextDouble() * 70f, 35f, 0.22f + (float)r.NextDouble() * 0.2f, 0.4f + (float)r.NextDouble() * 0.5f);
            }
            Gravel(b, r, 0.1f, 3.6f, 260, 0.3f);
            Crack(b, r, 0f, 0.8f);
            return b;
        }

        static float[] Rugido(System.Random r)
        {
            var b = Buf(2.4f);
            // gruñido de garganta de piedra: dientes de sierra muy graves con vibrato, filtrados como una boca enorme
            var lp = new Biquad(); var f1 = new Biquad(); var f2 = new Biquad();
            lp.Lowpass(700f, 0.7f); f1.Bandpass(320f, 3f); f2.Bandpass(720f, 4f);
            float ph1 = 0f, ph2 = 0f, ph3 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR, k = t / 2.4f;
                float env = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((2.4f - t) / 0.9f);
                float f0 = 44f + 10f * Mathf.Sin(Mathf.PI * k) + 2.5f * Mathf.Sin(t * 37f) + 1.5f * Noise(r);
                ph1 += f0 / SR; ph2 += f0 * 1.51f / SR; ph3 += f0 * 0.5f / SR;
                float saw = (Frac(ph1) * 2f - 1f) + 0.5f * (Frac(ph2) * 2f - 1f) + 0.6f * Mathf.Sin(ph3 * Mathf.PI * 2f);
                float rough = 1f + 0.6f * Noise(r);                      // aspereza (roca rozando)
                float s = lp.Process(saw * rough);
                float formant = f1.Process(s) * 1.4f + f2.Process(s) * 0.8f;
                b[i] += (s * 0.6f + formant) * env;
            }
            Grind(b, r, 0.1f, 2.1f, 220f, 0.35f, 11f);
            Rumble(b, r, 0f, 2.4f, 70f, 0.4f);
            return b;
        }

        /// Rodar: tierra y piedras que se amontonan y crujen bajo la rueda (ritmo de giro marcado).
        static float[] Rueda(System.Random r, float len)
        {
            var b = Buf(len + 0.25f);
            var lp = new Biquad(); lp.Lowpass(240f, 0.8f);
            var brown = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                brown = brown * 0.996f + Noise(r) * 0.06f;
                float spin = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Mathf.PI * 2.5f * t));
                float s = lp.Process(brown * 3f) * spin;
                s += Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.18f * spin;
                b[i] += s;
            }
            Gravel(b, r, 0f, len + 0.25f, (int)(len * 70), 0.32f);
            for (float t = 0.1f; t < len; t += 0.4f) Thump(b, t + (float)r.NextDouble() * 0.05f, 95f, 50f, 0.12f, 0.3f);
            return b;
        }

        /// Ambiente del combate: retumbar grave continuo con derrumbes lejanos de vez en cuando.
        static float[] Ambiente(System.Random r, float len)
        {
            var b = Buf(len + 1.2f);
            Rumble(b, r, 0f, len + 1.2f, 75f, 0.7f, false);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[i] += Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.14f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / len * 2f));
            }
            foreach (float t in new[] { 1.4f, 4.6f, 7.3f })
            {
                var far = Buf(2f);
                Grind(far, r, 0f, 1.4f, 260f, 0.5f, 7f);
                Gravel(far, r, 0.1f, 1.8f, 60, 0.25f);
                Thump(far, 0.2f, 80f, 40f, 0.3f, 0.5f);
                var lp = new Biquad(); lp.Lowpass(500f, 0.7f);
                int o = (int)(t * SR);
                for (int i = 0; i < far.Length && o + i < b.Length; i++) b[o + i] += lp.Process(far[i]) * 0.45f;
            }
            return b;
        }

        // ------------------------------------------------------------------ piezas
        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * SR)];
        static float Noise(System.Random r) => (float)r.NextDouble() * 2f - 1f;
        static float Frac(float x) => x - Mathf.Floor(x);

        /// Golpe sordo: seno que baja de tono con caída exponencial.
        static void Thump(float[] b, float at, float f0, float f1, float decay, float amp)
        {
            int o = (int)(at * SR), n = (int)((decay * 5f) * SR);
            float ph = 0f;
            for (int i = 0; i < n && o + i < b.Length; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(f1, f0, Mathf.Exp(-t / (decay * 0.5f)));
                ph += f / SR;
                float env = Mathf.Exp(-t / decay) * Mathf.Clamp01(t / 0.003f);
                b[o + i] += Mathf.Sin(ph * Mathf.PI * 2f) * env * amp;
            }
        }

        /// Ráfaga de ruido filtrado (paso bajo) con caída.
        static void Burst(float[] b, System.Random r, float at, float decay, float cutoff, float amp, bool low)
        {
            var f = new Biquad(); if (low) f.Lowpass(cutoff, 0.8f); else f.Highpass(cutoff, 0.8f);
            int o = (int)(at * SR), n = (int)(decay * 5f * SR);
            for (int i = 0; i < n && o + i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[o + i] += f.Process(Noise(r)) * Mathf.Exp(-t / decay) * amp;
            }
        }

        /// Chasquido de roca que se parte (ruido agudo muy corto + un poco de cuerpo).
        static void Crack(float[] b, System.Random r, float at, float amp)
        {
            var hp = new Biquad(); hp.Highpass(1800f, 0.9f);
            var bp = new Biquad(); bp.Bandpass(700f, 1.5f);
            int o = (int)(at * SR), n = (int)(0.09f * SR);
            for (int i = 0; i < n && o + i < b.Length; i++)
            {
                float t = i / (float)SR, w = Noise(r);
                b[o + i] += (hp.Process(w) * Mathf.Exp(-t / 0.008f) * 1.2f + bp.Process(w) * Mathf.Exp(-t / 0.03f) * 0.8f) * amp;
            }
        }

        /// Gravilla: muchos granos cortos que caen y rebotan, cada vez más espaciados.
        static void Gravel(float[] b, System.Random r, float from, float to, int count, float amp)
        {
            for (int g = 0; g < count; g++)
            {
                float k = (float)r.NextDouble();
                float at = Mathf.Lerp(from, to, k * k);                          // más granos al principio
                float f = 900f + (float)r.NextDouble() * 2600f;
                var bp = new Biquad(); bp.Bandpass(f, 4f);
                int o = (int)(at * SR), n = (int)(0.025f * SR);
                float a = amp * (0.3f + (float)r.NextDouble()) * (1f - k * 0.7f);
                for (int i = 0; i < n && o + i < b.Length; i++)
                    b[o + i] += bp.Process(Noise(r)) * Mathf.Exp(-i / (0.004f * SR)) * a * 2.5f;
            }
        }

        /// Retumbar grave (ruido marrón filtrado) con entrada y salida suaves.
        static void Rumble(float[] b, System.Random r, float from, float to, float cutoff, float amp, bool fade = true)
        {
            var lp = new Biquad(); lp.Lowpass(cutoff, 0.7f);
            float brown = 0f;
            int o = (int)(from * SR), n = (int)((to - from) * SR);
            for (int i = 0; i < n && o + i < b.Length; i++)
            {
                float t = i / (float)SR, len = to - from;
                brown = brown * 0.996f + Noise(r) * 0.05f;
                float env = fade ? Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-t / (len * 0.45f)) : 1f;
                b[o + i] += lp.Process(brown * 4f) * env * amp;
            }
        }

        /// Molienda: roca contra roca (ruido de banda con modulación áspera).
        static void Grind(float[] b, System.Random r, float from, float to, float center, float amp, float rate)
        {
            var bp = new Biquad(); bp.Bandpass(center, 1.2f);
            int o = (int)(from * SR), n = (int)((to - from) * SR);
            for (int i = 0; i < n && o + i < b.Length; i++)
            {
                float t = i / (float)SR, k = t / (to - from);
                float am = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * rate * Mathf.PI * 2f + Mathf.Sin(t * 3.1f) * 2f));
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k));
                b[o + i] += bp.Process(Noise(r)) * am * env * amp * 2f;
            }
        }

        static void Grunt(float[] b, float at, float dur, float f0, float amp)
        {
            var lp = new Biquad(); lp.Lowpass(500f, 0.8f);
            int o = (int)(at * SR), n = (int)(dur * SR);
            float ph = 0f;
            for (int i = 0; i < n && o + i < b.Length; i++)
            {
                float t = i / (float)SR;
                ph += f0 / SR;
                float env = Mathf.Clamp01(t / 0.04f) * Mathf.Clamp01((dur - t) / 0.15f);
                b[o + i] += lp.Process(Frac(ph) * 2f - 1f) * env * amp;
            }
        }

        /// Bucle sin corte: el final se funde con el principio.
        static float[] Loop(float[] src, float fadeSeconds)
        {
            int fade = (int)(fadeSeconds * SR), len = src.Length - fade;
            var o = new float[len];
            for (int i = 0; i < len; i++) o[i] = src[i];
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; o[i] = src[i] * k + src[len + i] * (1f - k); }
            return o;
        }

        // ------------------------------------------------------------------ salida
        static void Save(string name, float[] data)
        {
            // fuera lo que no se oye (< 35 Hz): solo restaría volumen útil, sobre todo en altavoces pequeños
            var hp = new Biquad(); hp.Highpass(35f, 0.7f);
            for (int i = 0; i < data.Length; i++) data[i] = hp.Process(data[i]);
            float peak = 1e-5f;
            foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            float g = 0.9f / peak;
            string path = Path.Combine(OutDir, name + ".wav");
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = data.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var s in data)
                {
                    float v = (float)Math.Tanh(s * g * 1.1f) / (float)Math.Tanh(1.1f);   // saturación suave
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32000f), -32767, 32767));
                }
            }
        }

        /// Filtro bicuadrático (RBJ) de un polo doble.
        class Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            void Set(float B0, float B1, float B2, float A0, float A1, float A2) { b0 = B0 / A0; b1 = B1 / A0; b2 = B2 / A0; a1 = A1 / A0; a2 = A2 / A0; }
            public void Lowpass(float f, float q) { float w = 2f * Mathf.PI * f / SR, c = Mathf.Cos(w), al = Mathf.Sin(w) / (2f * q); Set((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + al, -2 * c, 1 - al); }
            public void Highpass(float f, float q) { float w = 2f * Mathf.PI * f / SR, c = Mathf.Cos(w), al = Mathf.Sin(w) / (2f * q); Set((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + al, -2 * c, 1 - al); }
            public void Bandpass(float f, float q) { float w = 2f * Mathf.PI * f / SR, c = Mathf.Cos(w), al = Mathf.Sin(w) / (2f * q); Set(al, 0, -al, 1 + al, -2 * c, 1 - al); }
            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }
    }
}
#endif
