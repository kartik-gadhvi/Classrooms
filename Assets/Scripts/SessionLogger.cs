using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>One question to the guide and its answer; written as a row of the dialogue CSV.</summary>
public class GuideExchange
{
    public string time = DateTime.Now.ToString("HH:mm:ss");
    public int step = -1;
    public string phase = "";
    public string transcript = "";
    public string response = "";
    public string source = "";
    public int sttMs = -1;
    public int responseMs = -1;
    public int audioMs = -1;
    public string error = "";
}

/// <summary>
/// Zentrales Logging f�r einen Studien-Durchlauf.
/// Schreibt EINE CSV-Datei mit fester Kopfzeile und genau einer Datenzeile.
/// Dateiname = Anwendungsname + Datum + Uhrzeit (ein Durchlauf = eine Datei).
///
/// Pre-Test, Post-Test und Frageb�gen werden automatisch aus den vorhandenen
/// Controllern gelesen. Skip und die beiden Aufgaben-Erfolge werden vom
/// BottomTutorialController gemeldet:
///   SessionLogger.Instance.RegisterSkip(currentStep);
///   SessionLogger.Instance.MarkRadiusTaskCompleted();
///   SessionLogger.Instance.MarkFloatTaskCompleted();
///
/// Die Datei wird nach jedem Ereignis neu geschrieben (crash-sicher).
/// </summary>
public class SessionLogger : MonoBehaviour
{
    public static SessionLogger Instance { get; private set; }

    [Header("Referenzen")]
    public PrePostQuizController prePostQuiz;
    public QuestionnaireController questionnaire;

    [Header("Datei")]
    [Tooltip("Leer lassen = Application.productName wird verwendet.")]
    public string appNameOverride = "";
    [Tooltip("Optional: Kennung der Versuchsperson, falls vorhanden.")]
    public string participantId = "";

    [Header("Leere Zellen")]
    [Tooltip("Wird in jede wirklich leere Zelle geschrieben (z. B. fehlende Antwort, kein Zeitstempel).")]
    public string nullToken = "NULL";

    [Header("Debug")]
    public bool logPathToConsole = true;

    private const char SEP = ';';        // Spaltentrenner
    private const char LISTSEP = '|';    // Trenner innerhalb einer Zelle

    private string filePath;
    private string dialoguePath;
    private string sessionStart;
    private string appName;
    private string condition = "";
    private int guideExchangeCount;
    private int guideNoMatchCount;

    // Caches (�berleben auch, falls eine Referenz beim Szenenwechsel null wird)
    private readonly List<string> questionnaireIds = new List<string>();
    private List<int> cachedPre = new List<int>();
    private List<int> cachedPost = new List<int>();
    private int cachedPostCorrect = 0;
    private int cachedPostTotal = 0;
    private int questionCount = 0;

    // Aufgaben / Skip
    private readonly List<int> skippedSections = new List<int>();
    private readonly List<string> radiusTimestamps = new List<string>();
    private readonly List<string> floatTimestamps = new List<string>();
    private string languageCode = "";
    private int aiPromptTokens;
    private int aiCompletionTokens;
    private int aiTotalTokens;
    private int aiRequestCount;
    private int aiErrorCount;
    private bool aiQuotaExceeded;
    private string aiLastError = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        appName = string.IsNullOrEmpty(appNameOverride) ? Application.productName : appNameOverride;
        appName = Sanitize(appName);

        ExperimentAITutor guide = FindFirstObjectByType<ExperimentAITutor>();
        if (guide != null)
            condition = guide.Condition.ToString();

        sessionStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string baseName = appName + FilePart(participantId) + FilePart(condition) + "_" + stamp;
        filePath = Path.Combine(Application.persistentDataPath, baseName + ".csv");
        dialoguePath = Path.Combine(Application.persistentDataPath, baseName + "_dialogue.csv");

        if (prePostQuiz != null)
            questionCount = prePostQuiz.QuestionCount;
    }

    private void Start()
    {
        // Erst hier, damit Awake() des QuestionnaireController garantiert gelaufen ist
        // und die Item-IDs bereits existieren.
        CaptureQuestionnaireIds();

        if (questionnaire != null)
            questionnaire.OnFinished += OnQuestionnaireFinished;

        WriteToDisk();   // schreibt sofort die Kopfzeile + leere Datenzeile

        if (logPathToConsole)
            Debug.Log("[SessionLogger] Datei: " + filePath);
    }

    private void OnDestroy()
    {
        if (questionnaire != null)
            questionnaire.OnFinished -= OnQuestionnaireFinished;
    }

    // ---------------------------------------------------------------------
    //  �ffentliche Hooks (aus deinen Skripten aufrufen)
    // ---------------------------------------------------------------------

    /// <summary>Aufrufen, wenn der Nutzer einen Abschnitt �berspringt. Der Schritt wird mitgeloggt.</summary>
    public void RegisterSkip(int sectionStep)
    {
        skippedSections.Add(sectionStep);
        WriteToDisk();
    }

    /// <summary>Aufrufen, sobald eine Radius-Aufgabe erfolgreich abgeschlossen wurde.</summary>
    public void MarkRadiusTaskCompleted()
    {
        radiusTimestamps.Add(DateTime.Now.ToString("HH:mm:ss"));
        WriteToDisk();
    }

    /// <summary>Aufrufen, sobald ein Tr�pfchen erfolgreich zum Schweben gebracht wurde.</summary>
    public void MarkFloatTaskCompleted()
    {
        floatTimestamps.Add(DateTime.Now.ToString("HH:mm:ss"));
        WriteToDisk();
    }

    public void SetParticipantId(string id)
    {
        participantId = id;
        WriteToDisk();
    }

    public void SetLanguage(AppLanguage language)
    {
        languageCode = language == AppLanguage.English ? "en" : "de";
        WriteToDisk();
    }

    public void RegisterAiUsage(int promptTokens, int completionTokens, int totalTokens)
    {
        aiRequestCount++;
        aiPromptTokens += promptTokens;
        aiCompletionTokens += completionTokens;
        aiTotalTokens += totalTokens;
        WriteToDisk();
    }

    public void RegisterAiError(string error)
    {
        aiErrorCount++;
        aiLastError = error ?? "";
        WriteToDisk();
    }

    public void RegisterAiQuotaExceeded(string error)
    {
        aiQuotaExceeded = true;
        RegisterAiError(error);
    }

    public void LogGuideExchange(GuideExchange exchange)
    {
        if (exchange == null)
            return;

        guideExchangeCount++;
        if (exchange.source == "rule:" + RuleBasedGuideStrings.NoMatchId)
            guideNoMatchCount++;

        var header = new StringBuilder();
        var row = new StringBuilder();
        AddField(header, row, "Time", exchange.time);
        AddField(header, row, "ParticipantId", participantId);
        AddField(header, row, "Condition", condition);
        AddField(header, row, "Language", LanguageManager.IsEnglish ? "en" : "de");
        AddField(header, row, "Step", exchange.step >= 0 ? exchange.step.ToString() : "");
        AddField(header, row, "Phase", exchange.phase);
        AddField(header, row, "Transcript", exchange.transcript);
        AddField(header, row, "Response", exchange.response);
        AddField(header, row, "Source", exchange.source);
        AddField(header, row, "SpeechToTextMs", exchange.sttMs >= 0 ? exchange.sttMs.ToString() : "");
        AddField(header, row, "ResponseMs", exchange.responseMs >= 0 ? exchange.responseMs.ToString() : "");
        AddField(header, row, "ReleaseToAudioMs", exchange.audioMs >= 0 ? exchange.audioMs.ToString() : "");
        AddField(header, row, "Error", exchange.error);

        try
        {
            if (!File.Exists(dialoguePath))
                File.WriteAllText(dialoguePath, header + "\n", new UTF8Encoding(true));
            File.AppendAllText(dialoguePath, row + "\n", new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogError("[SessionLogger] Dialog-Log fehlgeschlagen: " + e.Message);
        }

        WriteToDisk();
    }

    /// <summary>Erzwingt ein Schreiben (z. B. am Ende der Studie).</summary>
    public void FlushNow()
    {
        WriteToDisk();
    }

    // ---------------------------------------------------------------------
    //  Automatisches Schreiben an wichtigen Punkten
    // ---------------------------------------------------------------------

    private void OnQuestionnaireFinished(IReadOnlyList<int> answers)
    {
        WriteToDisk();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) WriteToDisk();   // Quest: Headset abgesetzt / App im Hintergrund
    }

    private void OnApplicationQuit()
    {
        WriteToDisk();
    }

    // ---------------------------------------------------------------------
    //  Datenerfassung
    // ---------------------------------------------------------------------

    private void RefreshCaches()
    {
        if (prePostQuiz != null)
        {
            questionCount = prePostQuiz.QuestionCount;
            cachedPre = new List<int>(prePostQuiz.PreAnswers);
            cachedPost = new List<int>(prePostQuiz.PostAnswers);
            cachedPostCorrect = prePostQuiz.CorrectPostCount;
            cachedPostTotal = prePostQuiz.QuestionCount;
        }
    }

    private void CaptureQuestionnaireIds()
    {
        questionnaireIds.Clear();
        if (questionnaire == null)
            return;

        string raw = questionnaire.BuildResultCsv();
        string[] lines = raw.Split('\n');
        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line))
                continue;
            int idx = line.IndexOf(';');
            string id = idx >= 0 ? line.Substring(0, idx) : line;
            if (!string.IsNullOrEmpty(id))
                questionnaireIds.Add(id);
        }
    }

    private Dictionary<string, int> GetQuestionnaireValues()
    {
        var map = new Dictionary<string, int>();
        if (questionnaire == null)
            return map;

        string raw = questionnaire.BuildResultCsv();
        string[] lines = raw.Split('\n');
        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line))
                continue;
            int idx = line.IndexOf(';');
            if (idx < 0)
                continue;
            string id = line.Substring(0, idx);
            string valStr = line.Substring(idx + 1).Trim();
            if (int.TryParse(valStr, out int val))
                map[id] = val;
        }
        return map;
    }

    // ---------------------------------------------------------------------
    //  CSV-Aufbau
    // ---------------------------------------------------------------------

    private void WriteToDisk()
    {
        RefreshCaches();

        StringBuilder header = new StringBuilder();
        StringBuilder row = new StringBuilder();

        AddField(header, row, "ParticipantId", participantId);
        AddField(header, row, "Condition", condition);
        AddField(header, row, "AppName", appName);
        AddField(header, row, "SessionStart", sessionStart);
        AddField(header, row, "Language", string.IsNullOrEmpty(languageCode)
            ? (LanguageManager.IsEnglish ? "en" : "de")
            : languageCode);

        // Pre-Test (gew�hlter Index 0=A,1=B,2=C; -1 = nicht beantwortet)
        for (int i = 0; i < questionCount; i++)
        {
            string val = (i < cachedPre.Count) ? cachedPre[i].ToString() : "";
            AddField(header, row, "Pre_Q" + (i + 1).ToString("00"), val);
        }

        // Post-Test
        for (int i = 0; i < questionCount; i++)
        {
            string val = (i < cachedPost.Count) ? cachedPost[i].ToString() : "";
            AddField(header, row, "Post_Q" + (i + 1).ToString("00"), val);
        }
        AddField(header, row, "Post_CorrectCount", cachedPostCorrect.ToString());
        AddField(header, row, "Post_Total", cachedPostTotal.ToString());

        // Frageb�gen (CLS / IPQ / Weitere Fragen) � pro Item eine Spalte
        Dictionary<string, int> qVals = GetQuestionnaireValues();
        foreach (string id in questionnaireIds)
        {
            string val = qVals.TryGetValue(id, out int v) ? v.ToString() : "";
            AddField(header, row, id, val);
        }

        // Story-Skip
        AddField(header, row, "Skip_EverUsed", (skippedSections.Count > 0) ? "true" : "false");
        AddField(header, row, "Skip_Count", skippedSections.Count.ToString());
        AddField(header, row, "Skip_Sections", string.Join(LISTSEP.ToString(), skippedSections));

        // Aufgabe: Radius einstellen
        AddField(header, row, "Radius_Count", radiusTimestamps.Count.ToString());
        AddField(header, row, "Radius_FirstCompleted", radiusTimestamps.Count > 0 ? radiusTimestamps[0] : "");
        AddField(header, row, "Radius_AllTimestamps", string.Join(LISTSEP.ToString(), radiusTimestamps));

        // Aufgabe: Tr�pfchen zum Schweben bringen
        AddField(header, row, "Float_Count", floatTimestamps.Count.ToString());
        AddField(header, row, "Float_FirstCompleted", floatTimestamps.Count > 0 ? floatTimestamps[0] : "");
        AddField(header, row, "Float_AllTimestamps", string.Join(LISTSEP.ToString(), floatTimestamps));

        AddField(header, row, "AI_RequestCount", aiRequestCount.ToString());
        AddField(header, row, "AI_PromptTokens", aiPromptTokens.ToString());
        AddField(header, row, "AI_CompletionTokens", aiCompletionTokens.ToString());
        AddField(header, row, "AI_TotalTokens", aiTotalTokens.ToString());
        AddField(header, row, "AI_ErrorCount", aiErrorCount.ToString());
        AddField(header, row, "AI_QuotaExceeded", aiQuotaExceeded ? "true" : "false");
        AddField(header, row, "AI_LastError", aiLastError);
        AddField(header, row, "Guide_Exchanges", guideExchangeCount.ToString());
        AddField(header, row, "Guide_RuleNoMatch", guideNoMatchCount.ToString());

        try
        {
            string content = header.ToString() + "\n" + row.ToString() + "\n";
            File.WriteAllText(filePath, content, new UTF8Encoding(true)); // BOM f�r Excel-Umlaute
        }
        catch (Exception e)
        {
            Debug.LogError("[SessionLogger] Schreiben fehlgeschlagen: " + e.Message);
        }
    }

    private void AddField(StringBuilder header, StringBuilder row, string columnName, string value)
    {
        if (header.Length > 0) header.Append(SEP);
        if (row.Length > 0) row.Append(SEP);

        header.Append(Sanitize(columnName));

        string v = Sanitize(value);
        if (string.IsNullOrEmpty(v))
            v = nullToken;
        row.Append(v);
    }

    private static string FilePart(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";

        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '-');
        return "_" + s.Trim().Replace(' ', '-');
    }

    private string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s.Replace(SEP, ',').Replace('\n', ' ').Replace('\r', ' ').Replace('"', '\'');
    }
}