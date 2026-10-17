using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Regenerates the tutorial narration with the tutor voice profile (ElevenLabs).
/// Clips are written over the files the tutorial already references, so every scene
/// picks up the new audio without re-wiring. Originals are copied to Narration/Backup first.
/// </summary>
public class MillikanNarrationGenerator : EditorWindow
{
    private const string NarrationDir = "Narration";
    private const string ManifestPath = NarrationDir + "/narration_manifest.json";
    private const string ScriptPath = NarrationDir + "/spoken_script.tsv";
    private const string BackupDir = NarrationDir + "/Backup";
    private const string GuideClipDir = "Assets/Material/RuleBasedGuide";

    private TutorVoiceProfile profile;
    private string rerollSteps = "";
    private bool rerollGerman = true;
    private bool rerollEnglish = true;
    private bool busy;
    private string status = "";

    [MenuItem("Tools/Millikan/Narration Generator")]
    private static void Open()
    {
        GetWindow<MillikanNarrationGenerator>("Narration");
    }

    private void OnEnable()
    {
        if (profile == null)
            profile = TutorVoiceProfile.Load();
    }

    private void OnGUI()
    {
        profile = (TutorVoiceProfile)EditorGUILayout.ObjectField("Voice profile", profile, typeof(TutorVoiceProfile), false);

        bool hasKey = !string.IsNullOrEmpty(ElevenLabsKey.Get());
        EditorGUILayout.HelpBox(hasKey
            ? "ElevenLabs key found."
            : "No ElevenLabs key. Add it under Tools > Millikan > API Keys.", hasKey ? MessageType.Info : MessageType.Error);

        BottomTutorialController tutorial = FindTutorial();
        if (tutorial == null)
            EditorGUILayout.HelpBox("Open a classroom scene that contains the BottomTutorialController.", MessageType.Warning);

        bool blocked = busy || tutorial == null || profile == null || EditorApplication.isPlayingOrWillChangePlaymode;
        using (new EditorGUI.DisabledScope(blocked))
        {
            if (GUILayout.Button("1. Export spoken script (no API calls)"))
                ExportScript(tutorial);

            using (new EditorGUI.DisabledScope(!hasKey))
            {
                if (GUILayout.Button("2. Generate new or changed lines"))
                    Run(tutorial, false, null);
                if (GUILayout.Button("Regenerate everything"))
                    Run(tutorial, true, null);

                GUILayout.Space(8);
                EditorGUILayout.LabelField("Re-roll lines that sound off (next take)", EditorStyles.boldLabel);
                rerollSteps = EditorGUILayout.TextField("Steps (e.g. 12, 30)", rerollSteps);
                rerollGerman = EditorGUILayout.Toggle("German", rerollGerman);
                rerollEnglish = EditorGUILayout.Toggle("English", rerollEnglish);
                if (GUILayout.Button("Re-roll"))
                    Run(tutorial, false, ParseSteps(rerollSteps));
            }
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Rule-based guide answers (live reply model)", EditorStyles.boldLabel);
        RuleBasedGuide guide = FindGuide();
        if (guide == null)
            EditorGUILayout.HelpBox("Open the classroom scene that contains the RuleBasedGuide component.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(busy || guide == null || profile == null || !hasKey || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Generate new or changed answers"))
                Run(tutorial, false, null, true, true);
            if (GUILayout.Button("Regenerate all answers"))
                Run(tutorial, true, null, true, true);
        }

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, MessageType.None);
    }

    // ------------------------------------------------------------------ lines

    private struct Line
    {
        public int step;
        public string guideId;
        public AppLanguage language;
        public string text;
        public string assetPath;

        public bool IsGuide => !string.IsNullOrEmpty(guideId);
    }

    private static BottomTutorialController FindTutorial()
    {
        return UnityEngine.Object.FindFirstObjectByType<BottomTutorialController>(FindObjectsInactive.Include);
    }

    private static RuleBasedGuide FindGuide()
    {
        return UnityEngine.Object.FindFirstObjectByType<RuleBasedGuide>(FindObjectsInactive.Include);
    }

    private string ModelFor(Line line)
    {
        return line.IsGuide ? profile.liveModel : profile.narrationModel;
    }

    private static string GuideClipPath(string id, AppLanguage language)
    {
        return GuideClipDir + "/" + (language == AppLanguage.German ? "de" : "en") + "/" + id + ".wav";
    }

    private static List<Line> BuildGuideLines()
    {
        var lines = new List<Line>();
        foreach (AppLanguage language in new[] { AppLanguage.German, AppLanguage.English })
        {
            foreach (RuleBasedGuideStrings.Line entry in RuleBasedGuideStrings.AllLines())
            {
                lines.Add(new Line
                {
                    step = -1,
                    guideId = entry.id,
                    language = language,
                    text = language == AppLanguage.German ? entry.de : entry.en,
                    assetPath = GuideClipPath(entry.id, language)
                });
            }
        }

        return lines;
    }

    private static List<Line> BuildLines(BottomTutorialController tutorial)
    {
        var lines = new List<Line>();
        AppLanguage previous = LanguageManager.Current;

        try
        {
            foreach (AppLanguage language in new[] { AppLanguage.German, AppLanguage.English })
            {
                AudioClip[] clips = language == AppLanguage.German ? tutorial.stepVoiceClips : tutorial.stepVoiceClipsEnglish;
                if (clips == null)
                    continue;

                LanguageManager.Set(language);
                for (int step = 0; step < clips.Length; step++)
                {
                    string raw = ExperimentStrings.Dialogue(step, 0, tutorial.RequiredFloatingDroplets, false);
                    string spoken = SpokenText.Convert(raw, language);
                    if (string.IsNullOrWhiteSpace(spoken))
                        continue;

                    string path = clips[step] != null
                        ? AssetDatabase.GetAssetPath(clips[step])
                        : string.Format("Assets/Material/{0}/millikan_{1:00}.wav", language == AppLanguage.German ? "MillikanWav" : "MillikanWav_en", step);

                    lines.Add(new Line { step = step, language = language, text = spoken, assetPath = path });
                }
            }
        }
        finally
        {
            LanguageManager.Set(previous);
        }

        return lines;
    }

    private void ExportScript(BottomTutorialController tutorial)
    {
        List<Line> lines = BuildLines(tutorial);
        Directory.CreateDirectory(NarrationDir);

        var sb = new StringBuilder("step\tlanguage\tclip\ttext\n");
        foreach (Line line in lines)
        {
            sb.Append(line.step).Append('\t')
              .Append(line.language == AppLanguage.German ? "de" : "en").Append('\t')
              .Append(line.assetPath).Append('\t')
              .Append(line.text).Append('\n');
        }
        File.WriteAllText(ScriptPath, sb.ToString(), new UTF8Encoding(false));

        int chars = lines.Sum(l => l.text.Length);
        status = string.Format("Exported {0} lines ({1} characters) to {2}.", lines.Count, chars, ScriptPath);
        Debug.Log("[Narration] " + status);
    }

    // ------------------------------------------------------------------ generation

    /// <summary>Generate new or changed lines without the confirmation dialog (scripted use).</summary>
    public static void GenerateChangedLinesWithoutPrompt()
    {
        var window = GetWindow<MillikanNarrationGenerator>("Narration");
        window.Run(FindTutorial(), false, null, false);
    }

    public static string LastStatus
    {
        get { return HasOpenInstances<MillikanNarrationGenerator>() ? GetWindow<MillikanNarrationGenerator>().status : ""; }
    }

    public static bool IsBusy
    {
        get { return HasOpenInstances<MillikanNarrationGenerator>() && GetWindow<MillikanNarrationGenerator>().busy; }
    }

    private async void Run(BottomTutorialController tutorial, bool force, HashSet<int> reroll, bool confirm = true, bool guideAnswers = false)
    {
        busy = true;
        status = "";
        try
        {
            if (profile == null)
                profile = TutorVoiceProfile.Load();
            await Generate(tutorial, force, reroll, confirm, guideAnswers);
        }
        catch (Exception e)
        {
            status = "Failed: " + e.Message;
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            busy = false;
            Repaint();
        }
    }

    private async Task Generate(BottomTutorialController tutorial, bool force, HashSet<int> reroll, bool confirm, bool guideAnswers)
    {
        string key = ElevenLabsKey.Get();
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("No ElevenLabs key. Add it under Tools > Millikan > API Keys.");

        Manifest manifest = Manifest.Load();
        var jobs = new List<KeyValuePair<Line, int>>();

        foreach (Line line in guideAnswers ? BuildGuideLines() : BuildLines(tutorial))
        {
            Manifest.Entry entry = manifest.Find(line.assetPath);
            int seed = entry != null ? entry.seed : profile.seed;

            bool isReroll = reroll != null && reroll.Contains(line.step)
                            && ((line.language == AppLanguage.German && rerollGerman) || (line.language == AppLanguage.English && rerollEnglish));
            if (reroll != null && !isReroll)
                continue;
            if (isReroll)
                seed++;

            string hash = Hash(line, seed);
            if (!force && !isReroll && entry != null && entry.hash == hash && File.Exists(line.assetPath))
                continue;

            jobs.Add(new KeyValuePair<Line, int>(line, seed));
        }

        if (jobs.Count == 0)
        {
            if (guideAnswers)
                AssignGuideClips();
            status = "Everything is up to date.";
            return;
        }

        int chars = jobs.Sum(j => j.Key.text.Length);
        string model = guideAnswers ? profile.liveModel : profile.narrationModel;
        float creditsPerChar = model.Contains("flash") || model.Contains("turbo") ? 0.5f : 1f;
        if (confirm && !EditorUtility.DisplayDialog(guideAnswers ? "Generate rule-based answers" : "Generate narration",
                string.Format("{0} lines, {1} characters (~{2:0} credits on {3}).\nOriginals are backed up to {4}.",
                    jobs.Count, chars, chars * creditsPerChar, model, BackupDir),
                "Generate", "Cancel"))
            return;

        await GenerateJobs(tutorial, manifest, jobs);

        if (guideAnswers)
            AssignGuideClips();
    }

    private async Task GenerateJobs(BottomTutorialController tutorial, Manifest manifest, List<KeyValuePair<Line, int>> jobs)
    {
        string key = ElevenLabsKey.Get();
        int done = 0;
        bool cancelled = false;
        var failed = new List<string>();
        var created = new List<Line>();

        foreach (KeyValuePair<Line, int> job in jobs)
        {
            Line line = job.Key;
            string label = string.Format("{0} ({1})", line.IsGuide ? line.guideId : "Step " + line.step, line.language == AppLanguage.German ? "de" : "en");
            if (EditorUtility.DisplayCancelableProgressBar("Generating narration", label + "  -  " + done + "/" + jobs.Count, done / (float)jobs.Count))
            {
                cancelled = true;
                break;
            }

            byte[] pcm = await Request(key, line, job.Value);
            if (pcm == null)
            {
                failed.Add(label);
                continue;
            }

            bool existed = File.Exists(line.assetPath);
            Backup(line.assetPath);

            float[] samples = ElevenLabsTts.Pcm16ToFloat(pcm);
            ElevenLabsTts.Normalize(samples, profile.targetRmsDb, profile.peakLimitDb);
            WriteWav(line.assetPath, samples, profile.sampleRate);
            AssetDatabase.ImportAsset(line.assetPath, ImportAssetOptions.ForceUpdate);
            if (!existed && !line.IsGuide)
                created.Add(line);

            manifest.Set(new Manifest.Entry
            {
                clip = line.assetPath,
                step = line.step,
                language = line.language == AppLanguage.German ? "de" : "en",
                text = line.text,
                seed = job.Value,
                hash = Hash(line, job.Value),
                model = ModelFor(line),
                voice = profile.voiceName,
                generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            });
            manifest.Save();
            done++;
        }

        if (created.Count > 0)
            AssignCreatedClips(tutorial, created);

        status = (cancelled ? "Cancelled after " : "Generated ") + done + " of " + jobs.Count + " lines.";
        if (failed.Count > 0)
            status += " Failed: " + string.Join(", ", failed) + " (see Console).";
        Debug.Log("[Narration] " + status);
    }

    private async Task<byte[]> Request(string key, Line line, int seed)
    {
        string language = ElevenLabsTts.LanguageCode(line.language);

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            using (UnityWebRequest request = ElevenLabsTts.CreateRequest(profile, ModelFor(line), line.text, language, seed, key))
            {
                request.timeout = 120;
                await Send(request);

                if (request.result == UnityWebRequest.Result.Success)
                    return request.downloadHandler.data;

                long code = request.responseCode;
                string body = request.downloadHandler != null ? request.downloadHandler.text : "";
                if (code == 401 || code == 402 || code == 403)
                    throw new InvalidOperationException("ElevenLabs refused the request (" + code + "): " + body);

                bool retry = code == 429 || code >= 500 || code == 0;
                Debug.LogWarning(string.Format("[Narration] Step {0} attempt {1} failed ({2}) {3}", line.step, attempt, code, body));
                if (!retry)
                    return null;

                await Task.Delay(1500 * attempt);
            }
        }

        return null;
    }

    private static Task Send(UnityWebRequest request)
    {
        var tcs = new TaskCompletionSource<bool>();
        request.SendWebRequest().completed += _ => tcs.TrySetResult(true);
        return tcs.Task;
    }

    private static void AssignCreatedClips(BottomTutorialController tutorial, List<Line> created)
    {
        foreach (Line line in created)
        {
            AudioClip[] clips = line.language == AppLanguage.German ? tutorial.stepVoiceClips : tutorial.stepVoiceClipsEnglish;
            if (clips != null && line.step < clips.Length && clips[line.step] == null)
                clips[line.step] = AssetDatabase.LoadAssetAtPath<AudioClip>(line.assetPath);
        }

        EditorUtility.SetDirty(tutorial);
        if (PrefabUtility.IsPartOfPrefabInstance(tutorial))
            PrefabUtility.RecordPrefabInstancePropertyModifications(tutorial);
        EditorSceneManager.MarkSceneDirty(tutorial.gameObject.scene);
    }

    /// <summary>Wire every recorded answer into the scene's RuleBasedGuide (clips are looked up by name).</summary>
    public static int AssignGuideClips()
    {
        RuleBasedGuide guide = FindGuide();
        if (guide == null)
            return 0;

        int count = 0;
        Undo.RecordObject(guide, "Assign rule-based answers");
        foreach (AppLanguage language in new[] { AppLanguage.German, AppLanguage.English })
        {
            var clips = new List<AudioClip>();
            foreach (RuleBasedGuideStrings.Line entry in RuleBasedGuideStrings.AllLines())
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(GuideClipPath(entry.id, language));
                if (clip != null)
                    clips.Add(clip);
            }

            guide.SetClips(language, clips.ToArray());
            count += clips.Count;
        }

        EditorUtility.SetDirty(guide);
        EditorSceneManager.MarkSceneDirty(guide.gameObject.scene);
        return count;
    }

    // ------------------------------------------------------------------ files

    private static void Backup(string assetPath)
    {
        if (!File.Exists(assetPath))
            return;

        string target = Path.Combine(BackupDir, assetPath);
        if (File.Exists(target))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.Copy(assetPath, target);
    }

    private static void WriteWav(string path, float[] samples, int sampleRate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            int dataBytes = samples.Length * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            foreach (float s in samples)
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), short.MinValue, short.MaxValue));
        }
    }

    private string Hash(Line line, int seed)
    {
        string settings = string.Join("|", new[]
        {
            line.text, seed.ToString(CultureInfo.InvariantCulture), profile.voiceId, ModelFor(line),
            profile.stability.ToString(CultureInfo.InvariantCulture), profile.similarityBoost.ToString(CultureInfo.InvariantCulture),
            profile.style.ToString(CultureInfo.InvariantCulture), profile.useSpeakerBoost ? "1" : "0",
            profile.speed.ToString(CultureInfo.InvariantCulture), profile.sampleRate.ToString(CultureInfo.InvariantCulture),
            profile.targetRmsDb.ToString(CultureInfo.InvariantCulture), profile.peakLimitDb.ToString(CultureInfo.InvariantCulture)
        });

        using (var sha = SHA1.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(settings))).Replace("-", "").ToLowerInvariant();
    }

    private static HashSet<int> ParseSteps(string text)
    {
        var steps = new HashSet<int>();
        foreach (string part in (text ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int step;
            if (int.TryParse(part, out step))
                steps.Add(step);
        }
        return steps;
    }

    // ------------------------------------------------------------------ manifest

    [Serializable]
    private class Manifest
    {
        [Serializable]
        public class Entry
        {
            public string clip;
            public int step;
            public string language;
            public string text;
            public int seed;
            public string hash;
            public string model;
            public string voice;
            public string generatedUtc;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string clip)
        {
            return entries.FirstOrDefault(e => e.clip == clip);
        }

        public void Set(Entry entry)
        {
            entries.RemoveAll(e => e.clip == entry.clip);
            entries.Add(entry);
        }

        public static Manifest Load()
        {
            return File.Exists(ManifestPath)
                ? JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) ?? new Manifest()
                : new Manifest();
        }

        public void Save()
        {
            Directory.CreateDirectory(NarrationDir);
            entries.Sort((a, b) => a.language != b.language ? string.CompareOrdinal(a.language, b.language) : a.step.CompareTo(b.step));
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(this, true), new UTF8Encoding(false));
        }
    }
}

/// <summary>On-screen tutorial text to what the narrator should say.</summary>
public static class SpokenText
{
    public static string Convert(string raw, AppLanguage language)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        bool de = language == AppLanguage.German;
        string s = raw.Replace("\r", "");

        s = Regex.Replace(s, "<[^>]+>", "");
        s = Regex.Replace(s, @"(?m)^\s*(Messung|Measurement)\s*:\s*\d+\s*/\s*\d+\s*$", "");
        s = Regex.Replace(s, @"(\d+(?:\.\d+)?)\s*[*×·]\s*10\^(-?)(\d+)", m =>
            m.Groups[1].Value + (de ? " mal 10 hoch " : " times 10 to the power of ")
            + (m.Groups[2].Value == "-" ? "minus " : "") + m.Groups[3].Value);
        // Long decimals are read digit by digit ("1.592" -> "eins Komma fünf neun zwei"); the voice garbles them otherwise.
        s = Regex.Replace(s, @"\b(\d)\.(\d{3,})\b", m =>
            DigitWords(m.Groups[1].Value, de) + (de ? " Komma " : " point ") + DigitWords(m.Groups[2].Value, de));
        s = s.Replace("µm", de ? "Mikrometer" : "micrometers").Replace("μm", de ? "Mikrometer" : "micrometers");
        s = Regex.Replace(s, @"\s*→\s*", " – ");
        s = Regex.Replace(s, @"\s=\s", de ? " gleich " : " equals ");
        if (de)
            s = Regex.Replace(s, @"(\d)\.(\d)", "$1,$2");

        IEnumerable<string> blocks = Regex.Split(s.Trim(), @"\n\s*\n")
            .Select(b => Regex.Replace(b.Trim(), @"\s*\n\s*", " "))
            .Where(b => b.Length > 0)
            .Select(b => ".!?…:;".IndexOf(b[b.Length - 1]) >= 0 ? b : b + ".");

        string spoken = Regex.Replace(string.Join(" ", blocks), @"\s{2,}", " ").Trim();

        // The English voice skips one-word labels such as "Task:" or "Left:" at the start of a sentence.
        if (!de)
        {
            spoken = Regex.Replace(spoken, @"(^|[.!?]\s)Task:\s", "$1Your task: ");
            spoken = Regex.Replace(spoken, @"(^|[.!?]\s)Left:\s", "$1On the left, ");
            spoken = Regex.Replace(spoken, @"(^|[.!?]\s)Right:\s", "$1On the right, ");
        }

        return spoken;
    }

    private static readonly string[] GermanDigits = { "null", "eins", "zwei", "drei", "vier", "fünf", "sechs", "sieben", "acht", "neun" };
    private static readonly string[] EnglishDigits = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };

    private static string DigitWords(string digits, bool german)
    {
        string[] names = german ? GermanDigits : EnglishDigits;
        return string.Join(" ", digits.Select(c => names[c - '0']));
    }
}
