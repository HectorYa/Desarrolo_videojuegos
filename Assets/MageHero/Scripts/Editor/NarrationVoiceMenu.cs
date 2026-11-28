#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MageHero.Editor
{
    /// Genera los audios de la voz en off (Narrator.Lines) en Assets/MageHero/Resources/Audio/Narracion.
    /// Motor preferido: edge-tts con la voz neuronal femenina "es-MX-DaliaNeural" (español de México, requiere internet y
    /// `pip install edge-tts`). Si no está instalado, usa las voces de Windows: primero una es-MX (Sabina / Raúl,
    /// se instalan en Configuración ▸ Hora e idioma ▸ Voz ▸ Agregar voces ▸ Español (México)) y, si no hay,
    /// cualquier voz en español avisando de que el acento no es mexicano.
    public static class NarrationVoiceMenu
    {
        const string OutDir = "Assets/MageHero/Resources/Audio/Narracion";
        const string EdgeVoice = "es-MX-DaliaNeural";      // voz femenina de México

        [MenuItem("Tools/Mage Hero/Cinemática/Generar voz en off (español de México)")]
        static void Generate()
        {
            Directory.CreateDirectory(OutDir);
            bool edge = Run("python", "-c \"import edge_tts\"", out _) == 0;
            int ok = edge ? GenerateEdge() : GenerateWindows();
            AssetDatabase.Refresh();
            Debug.Log($"[MageHero] Voz en off: {ok}/{Narrator.Lines.Length} líneas generadas en {OutDir} ({(edge ? "edge-tts " + EdgeVoice : "voces de Windows")}).");
        }

        [MenuItem("Tools/Mage Hero/Cinemática/Borrar voz en off (solo subtítulos)")]
        static void Clear()
        {
            if (!Directory.Exists(OutDir)) return;
            foreach (var l in Narrator.Lines) DeleteClip(l.id);
            AssetDatabase.Refresh();
        }

        static int GenerateEdge()
        {
            int ok = 0;
            string tmp = Path.Combine(Path.GetTempPath(), "magehero_narracion.txt");
            try
            {
                for (int i = 0; i < Narrator.Lines.Length; i++)
                {
                    var (id, text) = Narrator.Lines[i];
                    EditorUtility.DisplayProgressBar("Voz en off (es-MX)", text, i / (float)Narrator.Lines.Length);
                    File.WriteAllText(tmp, text, new UTF8Encoding(false));
                    DeleteClip(id);
                    string mp3 = Path.GetFullPath(Path.Combine(OutDir, id + ".mp3"));
                    int code = Run("python", $"-m edge_tts --voice {EdgeVoice} --rate=-6% --file \"{tmp}\" --write-media \"{mp3}\"", out string err);
                    if (code == 0 && File.Exists(mp3)) ok++;
                    else Debug.LogWarning($"[MageHero] edge-tts falló en '{id}': {err}");
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            return ok;
        }

        static int GenerateWindows()
        {
            // Lista id<TAB>texto y un script de PowerShell con System.Speech (voces de escritorio de Windows).
            string dir = Path.Combine(Path.GetTempPath(), "magehero_tts");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            foreach (var l in Narrator.Lines) { DeleteClip(l.id); sb.Append(l.id).Append('\t').Append(l.text.Replace('\t', ' ')).Append('\n'); }
            string list = Path.Combine(dir, "lineas.tsv");
            File.WriteAllText(list, sb.ToString(), new UTF8Encoding(false));
            string outAbs = Path.GetFullPath(OutDir);
            string ps1 = Path.Combine(dir, "tts.ps1");
            File.WriteAllText(ps1, @"
Add-Type -AssemblyName System.Speech
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voices = $s.GetInstalledVoices() | Where-Object { $_.Enabled } | ForEach-Object { $_.VoiceInfo }
$v = $voices | Where-Object { $_.Culture.Name -eq 'es-MX' } | Select-Object -First 1
if ($v -eq $null) { $v = $voices | Where-Object { $_.Culture.Name -like 'es-*' } | Select-Object -First 1; Write-Output ('SIN_ES_MX ' + $v.Name) }
if ($v -eq $null) { Write-Output 'SIN_ESPANOL'; exit 2 }
$s.SelectVoice($v.Name)
$s.Rate = -1
Write-Output ('VOZ ' + $v.Name)
foreach ($line in [System.IO.File]::ReadAllLines('" + list + @"', [System.Text.Encoding]::UTF8)) {
  if ($line.Trim() -eq '') { continue }
  $p = $line.Split([char]9, 2)
  $s.SetOutputToWaveFile([System.IO.Path]::Combine('" + outAbs + @"', $p[0] + '.wav'))
  $s.Speak($p[1])
}
$s.SetOutputToNull()
", new UTF8Encoding(true));
            EditorUtility.DisplayProgressBar("Voz en off", "Generando con las voces de Windows…", 0.5f);
            int code;
            string output;
            try { code = Run("powershell", $"-NoProfile -ExecutionPolicy Bypass -File \"{ps1}\"", out output, true); }
            finally { EditorUtility.ClearProgressBar(); }
            if (output.Contains("SIN_ESPANOL") || code != 0)
            {
                EditorUtility.DisplayDialog("Voz en off", "No hay ninguna voz en español instalada en Windows.\n\nInstala edge-tts (pip install edge-tts) para usar la voz de México es-MX-JorgeNeural, o agrega la voz \"Español (México)\" en Configuración ▸ Hora e idioma ▸ Voz.", "Entendido");
                return 0;
            }
            if (output.Contains("SIN_ES_MX"))
                EditorUtility.DisplayDialog("Voz en off", "Se generó con una voz de España porque no hay voz de México instalada.\n\nPara acento mexicano: pip install edge-tts (recomendado) o agrega \"Español (México)\" en Configuración ▸ Hora e idioma ▸ Voz, y vuelve a generar.", "Entendido");
            int ok = 0;
            foreach (var l in Narrator.Lines) if (File.Exists(Path.Combine(OutDir, l.id + ".wav"))) ok++;
            return ok;
        }

        static void DeleteClip(string id)
        {
            foreach (var ext in new[] { ".wav", ".mp3", ".ogg" })
            {
                string path = OutDir + "/" + id + ext;
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        static int Run(string exe, string args, out string output, bool stdoutToo = false)
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
                    string so = p.StandardOutput.ReadToEnd();
                    string se = p.StandardError.ReadToEnd();
                    p.WaitForExit(120000);
                    output = stdoutToo ? so + se : se;
                    return p.ExitCode;
                }
            }
            catch (System.Exception e) { output = e.Message; return -1; }
        }
    }
}
#endif
