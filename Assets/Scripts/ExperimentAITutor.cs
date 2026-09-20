using UnityEngine;
using VRChat;

public class ExperimentAITutor : MonoBehaviour
{
    [SerializeField] private BottomTutorialController tutorial;
    [SerializeField] private VRChatSetup chatSetup;
    [SerializeField] private bool createRecordingIndicator = true;

    private VRSpeechToText stt;
    private VRChatController chat;
    private VRChatTTS tts;
    private GameObject recordingIndicator;
    private RecordingIndicator recordingVisual;
    private AITutorStateUI stateUI;
    private bool quotaExceeded;

    private void Awake()
    {
        if (chatSetup == null)
            chatSetup = GetComponent<VRChatSetup>();
        if (tutorial == null)
            tutorial = FindFirstObjectByType<BottomTutorialController>();

        chatSetup?.GetSTTManager()?.SetListeningEnabled(false);
    }

    private void Start()
    {
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
            stt.OnTranscriptionReceived += OnTranscriptionReceived;
            stt.OnError += OnAiError;
            stt.SetListeningEnabled(tutorial != null && tutorial.IsTutorialSessionActive);
        }

        if (chat != null)
            chat.OnError += OnAiError;

        if (tts != null)
            tts.OnError += OnAiError;

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

        if (stt != null)
        {
            stt.OnRecordingStarted -= OnRecordingStarted;
            stt.OnTranscriptionReceived -= OnTranscriptionReceived;
            stt.OnError -= OnAiError;
        }

        if (chat != null)
            chat.OnError -= OnAiError;

        if (tts != null)
            tts.OnError -= OnAiError;

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
        tutorial?.PauseNarration();
        chatSetup?.StopSpeaking();
        recordingVisual?.SetRecording(true);
        RefreshStateUI();
    }

    private void OnTranscriptionReceived(string text)
    {
        if (chat == null || string.IsNullOrWhiteSpace(text) || quotaExceeded)
            return;

        chat.SetRequestContext(BuildExperimentContext());
        RefreshStateUI();
    }

    private void OnAiError(string error)
    {
        if (!GroqQuotaGuard.IsQuotaOrRateLimit(error))
            return;

        quotaExceeded = true;
        stt?.SetListeningEnabled(false);
        chatSetup?.StopSpeaking();
        RefreshStateUI();
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
            "Phase: " + DescribePhase(step) + "\n" +
            "Measurements completed: " + tutorial.MeasurementsCompleted + "/" + tutorial.RequiredFloatingDroplets + "\n" +
            "Current tutor line: " + tutorial.GetCurrentDialogueText() + "\n" +
            "Help with this step. Do not skip the student ahead. Do not reveal later historical controversy until they are past step 47. Do not tell them to press B. Do not change the experiment controls.\n" +
            "If the student question is off-topic, do not answer it. Politely say you can only help with this experiment, then steer back to this step.";
    }

    private static string DescribePhase(int step)
    {
        if (step <= 3) return "introduction";
        if (step == 4) return "walk to the apparatus";
        if (step <= 16) return "historical context and apparatus tour";
        if (step == 17) return "first spray task";
        if (step <= 22) return "gravity and radius explanation";
        if (step <= 24) return "radius slider intro";
        if (step <= 27) return "radius tasks";
        if (step == 28) return "radius section wrap-up";
        if (step <= 32) return "electric field explanation";
        if (step == 33) return "measurement spray task";
        if (step == 34) return "select a droplet";
        if (step == 35) return "adjust voltage until the droplet hovers";
        if (step <= 40) return "measurement formulas and repeat loop";
        if (step <= 46) return "histogram and charge quantization";
        if (step <= 57) return "notebook and data selection controversy";
        return "publication result and closing";
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
