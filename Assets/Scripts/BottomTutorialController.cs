using TMPro;
using UnityEngine;

public class BottomTutorialController : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text dialogueText;
    public TMP_Text buttonHintText;
    public GameObject dialogueRoot;

    [Header("Guide UI")]
    public GameObject guideUIRoot;
    public TMP_Text guideUIText;

    [Header("Histogram UI")]
    public HistogramPanel histogramPanel;

    [Header("Panels")]
    public GameObject parameterPanelRoot;
    public GameObject forceArrowsRoot;

    [Header("Scene Objects")]
    public GameObject notebookObject;
    public GameObject startTeleportArrow;

    [Header("References")]
    public SpraySpawner spraySpawner;
    public RadiusSliderController radiusSliderController;
    public DropSelectionManager selectionManager;
    public StoryMeasurementRecorder measurementRecorder;
    public VoltageKnobInput voltageKnobInput;
    public PrePostQuizController quizController;

    [Header("Return Point")]
    public Transform playerRoot;
    public Transform storyReturnPoint;

    [Header("Arrows")]
    public GameObject arrowSetup;
    public GameObject arrowSprayer;
    public GameObject arrowSelectDrop;
    public GameObject arrowLight;
    public GameObject arrowCapacitor;
    public GameObject arrowVoltageKnob;

    [Header("Radius Task")]
    public float radiusTargetTolerance = 0.08f;

    [Header("Measurement Task")]
    public int requiredFloatingDroplets = 5;

    [Header("Voice")]
    public AudioSource voiceSource;
    public AudioClip[] stepVoiceClips;
    public AudioClip[] stepVoiceClipsEnglish;
    public bool stopVoiceWhenStepChanges = true;

    [Header("Sounds")]
    public AudioSource taskCompleteSfxSource;
    public AudioClip taskCompleteSfx;
    public AudioSource pageSfxSource;
    public AudioClip bookPageSfx;

    [Header("Disable These During Tutorial")]
    public Behaviour[] componentsToDisableDuringTutorial;

    public static bool TutorialInputLocked { get; private set; }

    public bool IsTutorialSessionActive => tutorialSessionActive;
    public int CurrentStep => currentStep;
    public int MeasurementsCompleted => measurementsCompleted;
    public int RequiredFloatingDroplets => requiredFloatingDroplets;

    public event System.Action SessionStarted;
    public event System.Action SessionEnded;
    public event System.Action<int> StepShown;

    private bool tutorialSessionActive;
    private int currentStep;

    private int measurementsCompleted;
    private bool firstMeasurementExplanationShown;

    private int radiusTaskIndex = -1;
    private bool radiusTaskRadiusCorrect;
    private bool radiusTaskSprayed;
    private bool radiusTaskDropEnteredField;
    private bool currentRadiusTaskDone;

    private readonly float[] radiusTargets = { 0.5f, 1.0f, 1.5f };

    private const int LastStepIndex = 66;
    private TutorialChoiceUI choiceUI;
    private bool helpDeclined;

    private void Start()
    {
        if (dialogueRoot == null)
            dialogueRoot = gameObject;

        ResetTutorialProgress();

        tutorialSessionActive = false;
        TutorialInputLocked = false;

        SetTutorialInputComponentsEnabled(true);
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);
        HideAllTemporaryUI();
        StopVoice();

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        LanguageManager.Changed += OnLanguageChanged;
        choiceUI = TutorialChoiceUI.Ensure(this);
        choiceUI?.Bind(this);
    }

    private void OnDestroy()
    {
        LanguageManager.Changed -= OnLanguageChanged;
    }

    private void OnLanguageChanged(AppLanguage language)
    {
        if (!tutorialSessionActive)
            return;

        RefreshCurrentText();
        RefreshGuideForCurrentStep();
    }

    private void Update()
    {
        if (!tutorialSessionActive)
            return;

        if (currentStep == 25 || currentStep == 26 || currentStep == 27)
            UpdateRadiusTask();

        if (OVRInput.GetDown(OVRInput.Button.One))
            AnswerYes();

        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            if (ExperimentStrings.IsQuestionStep(currentStep) && !IsSkipStep(currentStep))
                AnswerNo();
            else
                TrySkipSectionWithBButton();
        }

        if (OVRInput.GetDown(OVRInput.Button.Three) && !IsVoltageKnobGrabbed())
            ReplayCurrentExplanation();
    }

    public void BeginTutorialSession()
    {
        ResetTutorialProgress();

        tutorialSessionActive = true;
        TutorialInputLocked = true;

        if (startTeleportArrow != null)
            startTeleportArrow.SetActive(false);

        SetTutorialInputComponentsEnabled(false);
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);
        HideAllTemporaryUI();
        StopVoice();

        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);

        ShowStep();
        SessionStarted?.Invoke();
    }

    private void EndTutorialSession()
    {
        tutorialSessionActive = false;
        TutorialInputLocked = false;

        SetTutorialInputComponentsEnabled(true);
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);
        HideAllTemporaryUI();
        StopVoice();

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        ReturnToStoryStart();
        StartPostQuiz();

        SessionEnded?.Invoke();

        ResetTutorialProgress();
    }

    private void ResetTutorialProgress()
    {
        currentStep = 0;
        measurementsCompleted = 0;
        firstMeasurementExplanationShown = false;
        helpDeclined = false;

        radiusTaskIndex = -1;
        radiusTaskRadiusCorrect = false;
        radiusTaskSprayed = false;
        radiusTaskDropEnteredField = false;
        currentRadiusTaskDone = false;

        if (measurementRecorder != null)
            measurementRecorder.ClearMeasurements();
    }

    public void AnswerYes()
    {
        helpDeclined = false;
        TryAdvanceWithAButton();
    }

    public void AnswerNo()
    {
        if (!tutorialSessionActive)
            return;

        if (ExperimentStrings.IsHelpRequestStep(currentStep))
        {
            helpDeclined = true;
            RefreshCurrentText();
            PlayVoiceForCurrentStep();
            return;
        }

        if (ExperimentStrings.IsQuestionStep(currentStep))
        {
            helpDeclined = false;
            TryAdvanceWithAButton();
        }
    }

    public void ReplayCurrentExplanation()
    {
        if (!tutorialSessionActive)
            return;

        RefreshCurrentText();
        PlayVoiceForCurrentStep();
    }

    private bool IsVoltageKnobGrabbed()
    {
        return voltageKnobInput != null && voltageKnobInput.IsGrabbed;
    }

    private void TryAdvanceWithAButton()
    {
        if (IsBlockingStep(currentStep))
        {
            RefreshCurrentText();
            return;
        }

        if (currentStep == 25 || currentStep == 26 || currentStep == 27)
        {
            if (currentRadiusTaskDone)
                NextStep();
            else
                RefreshCurrentText();

            return;
        }

        if (currentStep == 40)
        {
            if (measurementsCompleted < requiredFloatingDroplets)
            {
                currentStep = 33;
                ShowStep();
            }
            else
            {
                NextStep();
            }

            return;
        }

        NextStep();
    }

    private void TrySkipSectionWithBButton()
    {
        if (!IsSkipStep(currentStep))
            return;

        // --- Logging: Nutzer hat einen Abschnitt übersprungen ---
        if (SessionLogger.Instance != null)
            SessionLogger.Instance.RegisterSkip(currentStep);

        if (currentStep == 4)
            currentStep = 17;
        else if (currentStep == 28)
            currentStep = 33;
        else if (currentStep == 47)
            currentStep = 58;
        else if (currentStep == 58)
            currentStep = 66;

        ShowStep();
    }

    private bool IsSkipStep(int step)
    {
        return step == 4 || step == 28 || step == 47 || step == 58;
    }

    private void NextStep()
    {
        helpDeclined = false;

        if (currentStep < LastStepIndex)
        {
            currentStep++;
            ShowStep();
        }
        else
        {
            EndTutorialSession();
        }
    }

    private void ShowStep()
    {
        ApplyStepSideEffects(currentStep);
        RefreshCurrentText();
        UpdateArrowForStep(currentStep);
        PlayVoiceForCurrentStep();
        StepShown?.Invoke(currentStep);
    }

    private void RefreshCurrentText()
    {
        if (dialogueText != null)
        {
            if (helpDeclined && ExperimentStrings.IsHelpRequestStep(currentStep))
                dialogueText.text = ExperimentStrings.HelpDeclined;
            else
                dialogueText.text = GetDialogueForCurrentStep();
        }

        if (buttonHintText != null)
            buttonHintText.text = GetButtonHintForCurrentStep();

        bool question = ExperimentStrings.IsQuestionStep(currentStep);
        choiceUI?.Refresh(question, question);
    }

    private void RefreshGuideForCurrentStep()
    {
        int step = currentStep;

        if (step == 17)
            ShowGuideUI(ExperimentStrings.GuideSprayer());
        else if (step == 22)
            ShowGuideUI(ExperimentStrings.GuideGravityFormula());
        else if (step == 23 || step == 24)
            ShowGuideUI(ExperimentStrings.GuideRadiusSlider());
        else if (step == 25)
            ShowGuideUI(ExperimentStrings.GuideRadiusTask("0.5", ExperimentStrings.RadiusLabel(0)));
        else if (step == 26)
            ShowGuideUI(ExperimentStrings.GuideRadiusTask("1.0", ExperimentStrings.RadiusLabel(1)));
        else if (step == 27)
            ShowGuideUI(ExperimentStrings.GuideRadiusTask("1.5", ExperimentStrings.RadiusLabel(2)));
        else if (step == 33)
            ShowGuideUI(ExperimentStrings.GuideMeasurementSpray());
        else if (step == 34)
            ShowGuideUI(ExperimentStrings.GuideMeasurementSelect());
        else if (step == 35)
            ShowGuideUI(ExperimentStrings.GuideVoltageTask());
        else if (step == 37 || step == 38)
            ShowGuideUI(ExperimentStrings.GuideElectricFormula());
        else if (step == 39)
            ShowGuideUI(ExperimentStrings.GuideParameterExplanation());
        else if (step >= 41 && step <= 46)
            ShowGuideUI(ExperimentStrings.GuideHistogram());
        else if (step >= 48 && step <= 57)
            ShowGuideUI(ExperimentStrings.GuideNotebook());
    }

    private bool IsBlockingStep(int step)
    {
        return step == 17 || step == 33 || step == 34 || step == 35;
    }

    private void ApplyStepSideEffects(int step)
    {
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);
        HideForceArrows();
        HideGuideUI();
        HideHistogramUI();
        HideAllArrows();

        if (step >= 23 && step <= 40)
            ShowParameterPanel();
        else
            HideParameterPanel();

        switch (step)
        {
            case 17:
                if (spraySpawner != null)
                    spraySpawner.ReturnToRandomModeAndClearDrops();

                ShowGuideUI(ExperimentStrings.GuideSprayer());
                break;

            case 22:
                ShowGuideUI(ExperimentStrings.GuideGravityFormula());
                break;

            case 23:
                ShowParameterPanel();

                if (radiusSliderController != null)
                    radiusSliderController.StartRadiusTask();

                if (spraySpawner != null)
                    spraySpawner.EnableTutorialRadiusMode();

                ShowGuideUI(ExperimentStrings.GuideRadiusSlider());
                break;

            case 24:
                ShowGuideUI(ExperimentStrings.GuideRadiusSlider());
                break;

            case 25:
                StartRadiusTask(0);
                ShowGuideUI(ExperimentStrings.GuideRadiusTask("0.5", ExperimentStrings.RadiusLabel(0)));
                break;

            case 26:
                StartRadiusTask(1);
                ShowGuideUI(ExperimentStrings.GuideRadiusTask("1.0", ExperimentStrings.RadiusLabel(1)));
                break;

            case 27:
                StartRadiusTask(2);
                ShowGuideUI(ExperimentStrings.GuideRadiusTask("1.5", ExperimentStrings.RadiusLabel(2)));
                break;

            case 28:
                if (radiusSliderController != null)
                    radiusSliderController.EndRadiusTask();

                if (spraySpawner != null)
                    spraySpawner.ReturnToRandomModeAndClearDrops();

                HideGuideUI();
                break;

            case 33:
                StartNewMeasurement();
                ShowGuideUI(ExperimentStrings.GuideMeasurementSpray());
                break;

            case 34:
                SetSelectionInteraction(true);
                ShowGuideUI(ExperimentStrings.GuideMeasurementSelect());
                break;

            case 35:
                SetVoltageInteraction(true);
                ShowForceArrows();
                ShowGuideUI(ExperimentStrings.GuideVoltageTask());
                break;

            case 37:
            case 38:
                ShowForceArrows();
                ShowGuideUI(ExperimentStrings.GuideElectricFormula());
                break;

            case 39:
                ShowGuideUI(ExperimentStrings.GuideParameterExplanation());
                break;

            case 41:
                SetVoltageInteraction(false);
                SetSelectionInteraction(false);

                if (spraySpawner != null)
                    spraySpawner.ResetAllDrops();

                ShowHistogramGuide();
                break;

            case 42:
            case 43:
            case 44:
            case 45:
            case 46:
                ShowHistogramGuide();
                break;

            case 47:
                if (notebookObject != null)
                    notebookObject.SetActive(true);

                PlayBookSound();
                HideGuideUI();
                break;

            case 48:
            case 49:
            case 50:
            case 51:
            case 52:
            case 53:
            case 54:
            case 55:
            case 56:
            case 57:
                ShowGuideUI(ExperimentStrings.GuideNotebook());
                break;

            case 58:
                if (notebookObject != null)
                    notebookObject.SetActive(false);

                HideAllTemporaryUI();
                break;
        }
    }

    private void StartNewMeasurement()
    {
        ShowParameterPanel();
        HideForceArrows();
        SetVoltageInteraction(false);
        SetSelectionInteraction(false);

        if (spraySpawner != null)
            spraySpawner.ReturnToRandomModeAndClearDrops();

        if (selectionManager != null)
            selectionManager.ClearSelectionAndHover();

        ResetVoltageToZero();
    }

    private void StartRadiusTask(int index)
    {
        radiusTaskIndex = index;
        radiusTaskRadiusCorrect = false;
        radiusTaskSprayed = false;
        radiusTaskDropEnteredField = false;
        currentRadiusTaskDone = false;

        ShowParameterPanel();

        if (radiusSliderController != null)
            radiusSliderController.StartRadiusTask();

        if (spraySpawner != null)
            spraySpawner.EnableTutorialRadiusMode();
    }

    private void UpdateRadiusTask()
    {
        if (currentRadiusTaskDone || radiusSliderController == null)
            return;

        float target = radiusTargets[radiusTaskIndex];
        float current = radiusSliderController.GetCurrentRadiusMicrometer();

        radiusTaskRadiusCorrect = Mathf.Abs(current - target) <= radiusTargetTolerance;

        if (radiusTaskRadiusCorrect && radiusTaskSprayed && radiusTaskDropEnteredField)
        {
            currentRadiusTaskDone = true;
            PlayTaskCompleteSound();
            RefreshCurrentText();

            // --- Logging: Radius-Aufgabe geschafft ---
            if (SessionLogger.Instance != null)
                SessionLogger.Instance.MarkRadiusTaskCompleted();


        }
    }

    public void NotifyDropletTriggered()
    {
        if (!tutorialSessionActive)
            return;

        if (currentStep == 17)
        {
            PlayTaskCompleteSound();
            NextStep();
            return;
        }

        if (currentStep == 25 || currentStep == 26 || currentStep == 27)
        {
            radiusTaskSprayed = true;
            RefreshCurrentText();
            return;
        }

        if (currentStep == 33)
        {
            PlayTaskCompleteSound();
            NextStep();
        }
    }

    public void NotifyDropEnteredField()
    {
        if (!tutorialSessionActive)
            return;

        if (currentStep == 25 || currentStep == 26 || currentStep == 27)
        {
            radiusTaskDropEnteredField = true;
            RefreshCurrentText();
        }
    }

    public void NotifyDropSelected()
    {
        if (!tutorialSessionActive)
            return;

        if (currentStep == 34)
        {
            PlayTaskCompleteSound();
            NextStep();
        }
    }

    public void NotifyVoltageSolved()
    {
        if (!tutorialSessionActive || currentStep != 35)
            return;

        measurementsCompleted++;

        // --- Logging: Tröpfchen erfolgreich zum Schweben gebracht ---
        if (SessionLogger.Instance != null)
            SessionLogger.Instance.MarkFloatTaskCompleted();

        if (measurementRecorder != null)
            measurementRecorder.RecordSelectedDrop(selectionManager);

        PlayTaskCompleteSound();
        SetVoltageInteraction(false);

        if (measurementsCompleted == 1 && !firstMeasurementExplanationShown)
        {
            firstMeasurementExplanationShown = true;
            currentStep = 36;
            ShowStep();
            return;
        }

        if (measurementsCompleted < requiredFloatingDroplets)
        {
            currentStep = 33;
            ShowStep();
            return;
        }

        currentStep = 41;
        ShowStep();
    }

    private void PlayVoiceForCurrentStep()
    {
        if (voiceSource == null)
            return;

        if (stopVoiceWhenStepChanges)
            voiceSource.Stop();

        AudioClip clip = GetVoiceClipForCurrentStep();
        if (clip == null)
            return;

        voiceSource.clip = clip;
        voiceSource.Play();
    }

    public void PauseNarration()
    {
        StopVoice();
    }

    public string GetCurrentDialogueText()
    {
        return GetDialogueForCurrentStep();
    }

    private AudioClip GetVoiceClipForCurrentStep()
    {
        AudioClip english = GetClip(stepVoiceClipsEnglish, currentStep);
        if (LanguageManager.IsEnglish && english != null)
            return english;

        return GetClip(stepVoiceClips, currentStep);
    }

    private static AudioClip GetClip(AudioClip[] clips, int index)
    {
        if (clips == null || index < 0 || index >= clips.Length)
            return null;

        return clips[index];
    }

    private void StopVoice()
    {
        if (voiceSource != null)
            voiceSource.Stop();
    }

    private string GetDialogueForCurrentStep()
    {
        return ExperimentStrings.Dialogue(currentStep, measurementsCompleted, requiredFloatingDroplets, currentRadiusTaskDone);
    }

    private string GetButtonHintForCurrentStep()
    {
        bool radiusIncomplete = (currentStep == 25 || currentStep == 26 || currentStep == 27) && !currentRadiusTaskDone;
        return ExperimentStrings.ButtonHint(currentStep, IsSkipStep(currentStep), IsBlockingStep(currentStep), radiusIncomplete);
    }

    private void ShowHistogramGuide()
    {
        ShowGuideUI(ExperimentStrings.GuideHistogram());

        if (histogramPanel != null)
            histogramPanel.Show();
    }

    private void HideAllTemporaryUI()
    {
        HideParameterPanel();
        HideGuideUI();
        HideHistogramUI();
        HideForceArrows();
        HideAllArrows();

        if (notebookObject != null)
            notebookObject.SetActive(false);
    }

    private void ShowParameterPanel()
    {
        if (parameterPanelRoot != null)
            parameterPanelRoot.SetActive(true);
    }

    private void HideParameterPanel()
    {
        if (parameterPanelRoot != null)
            parameterPanelRoot.SetActive(false);
    }

    private void ShowGuideUI(string text)
    {
        if (guideUIRoot != null)
            guideUIRoot.SetActive(true);

        if (guideUIText != null)
            guideUIText.text = text;
    }

    private void HideGuideUI()
    {
        if (guideUIRoot != null)
            guideUIRoot.SetActive(false);
    }

    private void HideHistogramUI()
    {
        if (histogramPanel != null)
            histogramPanel.Hide();
    }

    private void ShowForceArrows()
    {
        if (forceArrowsRoot != null)
            forceArrowsRoot.SetActive(true);
    }

    private void HideForceArrows()
    {
        if (forceArrowsRoot != null)
            forceArrowsRoot.SetActive(false);
    }

    private void HideAllArrows()
    {
        if (arrowSetup != null) arrowSetup.SetActive(false);
        if (arrowSprayer != null) arrowSprayer.SetActive(false);
        if (arrowSelectDrop != null) arrowSelectDrop.SetActive(false);
        if (arrowLight != null) arrowLight.SetActive(false);
        if (arrowCapacitor != null) arrowCapacitor.SetActive(false);
        if (arrowVoltageKnob != null) arrowVoltageKnob.SetActive(false);
    }

    private void UpdateArrowForStep(int step)
    {
        HideAllArrows();

        switch (step)
        {
            case 10:
                if (arrowSetup != null) arrowSetup.SetActive(true);
                break;

            case 11:
            case 17:
            case 33:
                if (arrowSprayer != null) arrowSprayer.SetActive(true);
                break;

            case 14:
                if (arrowLight != null) arrowLight.SetActive(true);
                break;

            case 15:
            case 29:
            case 30:
            case 31:
            case 32:
            case 37:
            case 38:
                if (arrowCapacitor != null) arrowCapacitor.SetActive(true);
                break;

            case 34:
                if (arrowSelectDrop != null) arrowSelectDrop.SetActive(true);
                break;

            case 35:
                if (arrowVoltageKnob != null) arrowVoltageKnob.SetActive(true);
                break;
        }
    }

    private void SetVoltageInteraction(bool enabled)
    {
        if (voltageKnobInput != null)
            voltageKnobInput.SetInteractionEnabled(enabled);
    }

    private void SetSelectionInteraction(bool enabled)
    {
        if (selectionManager != null)
            selectionManager.SetSelectionEnabled(enabled);
    }

    private void ResetVoltageToZero()
    {
        if (voltageKnobInput != null)
            voltageKnobInput.ResetVoltageToZero();
    }

    private void ReturnToStoryStart()
    {
        if (playerRoot != null && storyReturnPoint != null)
        {
            playerRoot.position = storyReturnPoint.position;
            playerRoot.rotation = storyReturnPoint.rotation;
        }
    }

    private void StartPostQuiz()
    {
        if (quizController != null)
            quizController.StartPostQuiz();
    }

    private void PlayBookSound()
    {
        if (pageSfxSource != null && bookPageSfx != null)
            pageSfxSource.PlayOneShot(bookPageSfx);
    }

    private void PlayTaskCompleteSound()
    {
        if (taskCompleteSfxSource != null && taskCompleteSfx != null)
            taskCompleteSfxSource.PlayOneShot(taskCompleteSfx);
    }

    private void SetTutorialInputComponentsEnabled(bool enabled)
    {
        if (componentsToDisableDuringTutorial == null)
            return;

        for (int i = 0; i < componentsToDisableDuringTutorial.Length; i++)
        {
            if (componentsToDisableDuringTutorial[i] != null)
                componentsToDisableDuringTutorial[i].enabled = enabled;
        }
    }
}