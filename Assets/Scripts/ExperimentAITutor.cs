using UnityEngine;
using VRChat;

public enum GuideCondition
{
    AiBased,
    RuleBased
}

public class ExperimentAITutor : MonoBehaviour
{
    [SerializeField] private BottomTutorialController tutorial;
    [SerializeField] private VRChatSetup chatSetup;
    [SerializeField] private RuleBasedGuide ruleBasedGuide;
    [Tooltip("Set before each build. Both guides use the same avatar, voice, scene and tutorial; only the source of the answers differs.")]
    [SerializeField] private GuideCondition condition = GuideCondition.AiBased;
    [SerializeField] private bool createRecordingIndicator = true;

    public GuideCondition Condition => condition;

    private VRSpeechToText stt;
    private VRChatController chat;
    private VRChatTTS tts;
    private GameObject recordingIndicator;
    private RecordingIndicator recordingVisual;
    private AITutorStateUI stateUI;
    private bool quotaExceeded;
    private GuideExchange pending;
    private float releaseTime;
    private float transcriptTime;
    private bool missingClipsWarned;

    private bool IsRuleBased => condition == GuideCondition.RuleBased;

    private void Awake()
    {
        if (chatSetup == null)
            chatSetup = GetComponent<VRChatSetup>();
        if (tutorial == null)
            tutorial = FindFirstObjectByType<BottomTutorialController>();
        if (ruleBasedGuide == null)
            ruleBasedGuide = GetComponent<RuleBasedGuide>();
        if (IsRuleBased && ruleBasedGuide == null)
            ruleBasedGuide = gameObject.AddComponent<RuleBasedGuide>();

        chatSetup?.SetLlmEnabled(!IsRuleBased);
        chatSetup?.GetSTTManager()?.SetListeningEnabled(false);
    }

    private void Start()
    {
        Debug.Log("[ExperimentAITutor] Guide condition: " + condition);

        if (chatSetup == null)
            return;

        chat = chatSetup.GetChatController();
        stt = chatSetup.GetSTTManager();
        tts = chatSetup.GetTTSManager();

        if (stt != null)
        {
            if (createRecordingIndicator)
                EnsureRecordingIndicator();

            stt.OnRecordingStarted += OnRecordingStarted;
            stt.OnRecordingStopped += OnRecordingStopped;
            stt.OnTranscriptionReceived += OnTranscriptionReceived;
            stt.OnError += OnAiError;
            stt.SetListeningEnabled(tutorial != null && tutorial.IsTutorialSessionActive);
        }

        if (chat != null)
        {
            chat.OnError += OnAiError;
            chat.OnAIResponseReceived += OnAiResponse;
        }

        if (tts != null)
        {
            tts.OnError += OnAiError;
            tts.OnSpeakingStarted += OnSpeakingStarted;
        }

        if (tutorial != null)
        {
            tutorial.SessionStarted += OnTutorialStarted;
            tutorial.SessionEnded += OnTutorialEnded;
            tutorial.StepShown += OnStepShown;
            EnsureStateUI();
        }

        LanguageManager.Changed += ApplyLanguage;
        ApplyLanguage(LanguageManager.Current);
    }

    private void Update()
    {
        RefreshStateUI();
    }

    private void OnDestroy()
    {
        LanguageManager.Changed -= ApplyLanguage;
        CommitPending("session ended");

        if (stt != null)
        {
            stt.OnRecordingStarted -= OnRecordingStarted;
            stt.OnRecordingStopped -= OnRecordingStopped;
            stt.OnTranscriptionReceived -= OnTranscriptionReceived;
            stt.OnError -= OnAiError;
        }

        if (chat != null)
        {
            chat.OnError -= OnAiError;
            chat.OnAIResponseReceived -= OnAiResponse;
        }

        if (tts != null)
        {
            tts.OnError -= OnAiError;
            tts.OnSpeakingStarted -= OnSpeakingStarted;
        }

        if (tutorial != null)
        {
            tutorial.SessionStarted -= OnTutorialStarted;
            tutorial.SessionEnded -= OnTutorialEnded;
            tutorial.StepShown -= OnStepShown;
        }
    }

    private void ApplyLanguage(AppLanguage language)
    {
        chatSetup?.SetSystemPrompt(ExperimentStrings.AiSystemPrompt);
        chatSetup?.SetWhisperLanguage(ExperimentStrings.WhisperLanguage);
        RefreshStateUI();
    }

    private void OnTutorialStarted()
    {
        ApplyLanguage(LanguageManager.Current);
        EnsureStateUI();
        if (!quotaExceeded)
            stt?.SetListeningEnabled(true);
        RefreshStateUI();
    }

    private void OnTutorialEnded()
    {
        stt?.SetListeningEnabled(false);
        chatSetup?.StopSpeaking();
        CommitPending();
        RefreshStateUI();
    }

    private void OnStepShown(int step)
    {
        chatSetup?.StopSpeaking();
        if (stt != null && stt.IsRecording)
            tutorial?.PauseNarration();
    }

    private void OnRecordingStarted()
    {
        CommitPending();
        tutorial?.PauseNarration();
        chatSetup?.StopSpeaking();
        recordingVisual?.SetRecording(true);
        RefreshStateUI();
    }

    private void OnRecordingStopped()
    {
        releaseTime = Time.realtimeSinceStartup;
    }

    private void OnTranscriptionReceived(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || quotaExceeded)
            return;

        CommitPending();
        transcriptTime = Time.realtimeSinceStartup;
        int step = tutorial != null && tutorial.IsTutorialSessionActive ? tutorial.CurrentStep : -1;

        pending = new GuideExchange
        {
            step = step,
            phase = step >= 0 ? ExperimentPhases.FromStep(step).ToString() : "OutsideTutorial",
            transcript = text,
            sttMs = Ms(releaseTime, transcriptTime)
        };

        if (IsRuleBased)
        {
            RespondRuleBased(text, Mathf.Max(0, step));
            return;
        }

        if (chat == null)
        {
            CommitPending("no chat controller");
            return;
        }

        pending.source = chat.Model;
        chat.SetRequestContext(BuildExperimentContext());
        RefreshStateUI();
    }

    private void RespondRuleBased(string text, int step)
    {
        if (ruleBasedGuide == null || tts == null)
        {
            CommitPending("rule-based guide or TTS missing");
            return;
        }

        RuleBasedGuide.Reply reply = ruleBasedGuide.Respond(text, step);
        pending.response = reply.text;
        pending.source = "rule:" + reply.topic;
        pending.responseMs = Ms(transcriptTime, Time.realtimeSinceStartup);

        if (reply.clip != null)
        {
            tts.PlayPrerecorded(reply.clip);
            return;
        }

        if (!missingClipsWarned)
        {
            missingClipsWarned = true;
            Debug.LogWarning("[ExperimentAITutor] Rule-based recordings missing, falling back to live TTS. Generate them under Tools > Millikan > Narration Generator.");
        }

        pending.source += " (live TTS)";
        tts.Speak(reply.text);
    }

    private void OnAiResponse(string text)
    {
        if (pending == null || IsRuleBased)
            return;

        pending.response = text;
        pending.responseMs = Ms(transcriptTime, Time.realtimeSinceStartup);
    }

    private void OnSpeakingStarted()
    {
        if (pending == null || string.IsNullOrEmpty(pending.response))
            return;

        pending.audioMs = Ms(releaseTime, Time.realtimeSinceStartup);
        CommitPending();
    }

    private void OnAiError(string error)
    {
        if (pending != null)
            CommitPending(error);
        else
            SessionLogger.Instance?.LogGuideExchange(new GuideExchange { error = error });

        if (!GroqQuotaGuard.IsQuotaOrRateLimit(error))
            return;

        quotaExceeded = true;
        stt?.SetListeningEnabled(false);
        chatSetup?.StopSpeaking();
        RefreshStateUI();
    }

    private void CommitPending(string error = null)
    {
        if (pending == null)
            return;

        if (!string.IsNullOrEmpty(error))
            pending.error = error;
        else if (pending.audioMs < 0)
            pending.error = "interrupted before the answer was spoken";

        SessionLogger.Instance?.LogGuideExchange(pending);
        pending = null;
    }

    private static int Ms(float from, float to)
    {
        return from > 0f && to >= from ? Mathf.RoundToInt((to - from) * 1000f) : -1;
    }

    private void RefreshStateUI()
    {
        if (stateUI == null)
            return;

        if (quotaExceeded || (chat != null && chat.QuotaExceeded))
        {
            quotaExceeded = true;
            stateUI.SetState(AITutorState.Unavailable);
            return;
        }

        if (stt != null && stt.IsRecording)
            stateUI.SetState(AITutorState.Listening);
        else if ((stt != null && stt.IsProcessing) || (chat != null && chat.IsProcessing) || (tts != null && tts.IsProcessing))
            stateUI.SetState(AITutorState.Thinking);
        else if (tts != null && tts.IsSpeaking)
            stateUI.SetState(AITutorState.Speaking);
        else
            stateUI.SetState(AITutorState.Idle);
    }

    private string BuildExperimentContext()
    {
        string replyLanguage = LanguageManager.IsEnglish ? "English" : "German";

        if (tutorial == null || !tutorial.IsTutorialSessionActive)
            return "The student is not currently in the Millikan experiment tutorial. Do not answer quiz items. Reply only in " + replyLanguage + ". If the question is unrelated to this VR Millikan experiment, refuse politely and ask them to stay with the experiment.";

        int step = tutorial.CurrentStep;
        return
            "Current VR experiment state. Use this to help with the present step only.\n" +
            "Reply only in " + replyLanguage + ".\n" +
            "Step: " + step + " of 66.\n" +
            "Phase: " + ExperimentPhases.Describe(ExperimentPhases.FromStep(step)) + "\n" +
            "Measurements completed: " + tutorial.MeasurementsCompleted + "/" + tutorial.RequiredFloatingDroplets + "\n" +
            "Current tutor line: " + tutorial.GetCurrentDialogueText() + "\n" +
            "Help with this step. Do not skip the student ahead. Do not reveal later historical controversy until they are past step 47. Do not tell them to press B. Do not change the experiment controls.\n" +
            "If the student question is off-topic, do not answer it. Politely say you can only help with this experiment, then steer back to this step.";
    }

    private void EnsureStateUI()
    {
        if (stateUI != null || tutorial == null || tutorial.dialogueRoot == null)
            return;

        var panel = tutorial.dialogueText != null
            ? tutorial.dialogueText.transform.parent as RectTransform
            : tutorial.dialogueRoot.GetComponent<RectTransform>();

        if (panel == null)
            return;

        stateUI = AITutorStateUI.Create(panel, tutorial.dialogueText);
    }

    private void EnsureRecordingIndicator()
    {
        Transform parent = FindHandAnchor("LeftHandAnchor");
        if (parent == null)
            parent = FindHandAnchor("LeftControllerAnchor");
        if (parent == null && Camera.main != null)
            parent = Camera.main.transform;
        if (parent == null)
            parent = transform;

        recordingIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        recordingIndicator.name = "AIRecordingIndicator";
        recordingIndicator.transform.SetParent(parent, false);
        recordingIndicator.transform.localPosition = new Vector3(0f, 0.04f, 0.08f);
        recordingIndicator.transform.localScale = Vector3.one * 0.018f;
        recordingIndicator.SetActive(false);

        var col = recordingIndicator.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        var meshRen = recordingIndicator.GetComponent<MeshRenderer>();
        if (meshRen != null)
            meshRen.material.color = new Color(1f, 0.15f, 0.15f, 1f);

        recordingVisual = recordingIndicator.AddComponent<RecordingIndicator>();
        recordingVisual.SetRecording(false);

        var sttType = typeof(VRSpeechToText);
        var field = sttType.GetField("recordingIndicator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(stt, recordingIndicator);
    }

    private static Transform FindHandAnchor(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.transform : null;
    }
}
