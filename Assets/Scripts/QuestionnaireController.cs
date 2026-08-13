using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestionnaireController : MonoBehaviour
{
    [Serializable]
    public class LikertItem
    {
        public string id = "";
        [TextArea(2, 4)] public string statement = "";
        public int points = 7;
        public int startValue = 1;
        public bool showNumbers = true;
        public string leftAnchor = "";
        public string centerAnchor = "";
        public string rightAnchor = "";
    }

    [Serializable]
    public class LikertSection
    {
        public string heading = "";
        [TextArea(1, 3)] public string instruction = "";
        public List<LikertItem> items = new List<LikertItem>();
    }

    [Header("Integration mit dem Quiz (optional)")]
    public PrePostQuizController prePostQuiz;
    public Button postQuizContinueButton;
    public bool startAfterPostQuiz = true;

    [Header("Root-Objekte (gleiche Wall wie das Quiz)")]
    public GameObject wallRoot;
    public GameObject quizGroup;
    public GameObject resultGroup;
    public GameObject questionnaireGroup;

    [Header("Fragebogen-UI")]
    public TMP_Text headingText;
    public TMP_Text instructionText;
    public TMP_Text statementText;
    public TMP_Text progressText;
    public TMP_Text leftAnchorText;
    public TMP_Text centerAnchorText;
    public TMP_Text rightAnchorText;
    public TMP_Text continueText;

    [Header("Radiobuttons")]
    public RectTransform radioContainer;
    public Toggle radioButtonPrefab;
    public ToggleGroup toggleGroup;

    [Header("Weiter-Button (im Fragebogen)")]
    public Button continueButton;

    [Header("Flow")]
    public bool requireAnswerBeforeContinue = true;
    public bool hideWallWhenFinished = true;

    [Header("Events")]
    public bool logResultToConsole = true;

    public const int Unanswered = int.MinValue;

    public event Action<IReadOnlyList<int>> OnFinished;

    private struct RunRef { public int section; public int index; }

    private readonly List<LikertSection> sections = new List<LikertSection>();
    private readonly List<RunRef> runOrder = new List<RunRef>();
    private readonly List<int> answers = new List<int>();
    private readonly List<Toggle> activeToggles = new List<Toggle>();

    private int currentRun = -1;
    private bool started = false;

    public IReadOnlyList<int> Answers => answers;

    private void Awake()
    {
        BuildQuestionnaire();
        BuildRunOrder();

        if (toggleGroup == null && radioContainer != null)
            toggleGroup = radioContainer.GetComponent<ToggleGroup>();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinuePressed);
        }
    }

    private void Start()
    {
        if (startAfterPostQuiz && postQuizContinueButton != null)
            postQuizContinueButton.onClick.AddListener(OnPostQuizContinuePressed);

        if (questionnaireGroup != null)
            questionnaireGroup.SetActive(false);
    }

    private void OnPostQuizContinuePressed()
    {
        if (started)
            return;

        if (prePostQuiz != null &&
            prePostQuiz.CurrentMode != PrePostQuizController.QuizMode.PostQuiz)
            return;

        BeginQuestionnaire();
    }

    public void BeginQuestionnaire()
    {
        if (started)
            return;

        started = true;
        BuildQuestionnaire();
        BuildRunOrder();

        if (wallRoot != null) wallRoot.SetActive(true);
        if (quizGroup != null) quizGroup.SetActive(false);
        if (resultGroup != null) resultGroup.SetActive(false);
        if (questionnaireGroup != null) questionnaireGroup.SetActive(true);

        answers.Clear();
        for (int i = 0; i < runOrder.Count; i++)
            answers.Add(Unanswered);

        currentRun = 0;
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (currentRun >= runOrder.Count)
        {
            Finish();
            return;
        }

        RunRef r = runOrder[currentRun];
        LikertSection sec = sections[r.section];
        LikertItem item = sec.items[r.index];

        if (headingText != null) headingText.text = sec.heading;
        if (instructionText != null) instructionText.text = sec.instruction;
        if (statementText != null) statementText.text = item.statement;
        if (progressText != null) progressText.text = ExperimentStrings.QuestionnaireProgress(currentRun + 1, runOrder.Count);
        if (continueText != null) continueText.text = ExperimentStrings.Continue;

        if (leftAnchorText != null) leftAnchorText.text = item.leftAnchor;
        if (rightAnchorText != null) rightAnchorText.text = item.rightAnchor;
        if (centerAnchorText != null)
        {
            centerAnchorText.text = item.centerAnchor;
            centerAnchorText.gameObject.SetActive(!string.IsNullOrEmpty(item.centerAnchor));
        }

        BuildRadioButtons(item);
        UpdateContinueInteractable();
        Canvas.ForceUpdateCanvases();
    }

    private void BuildRadioButtons(LikertItem item)
    {
        for (int i = 0; i < activeToggles.Count; i++)
        {
            if (activeToggles[i] != null)
                Destroy(activeToggles[i].gameObject);
        }
        activeToggles.Clear();

        if (radioButtonPrefab == null || radioContainer == null)
            return;

        for (int i = 0; i < item.points; i++)
        {
            int value = item.startValue + i;

            Toggle toggle = Instantiate(radioButtonPrefab, radioContainer);
            toggle.gameObject.SetActive(true);

            if (toggleGroup != null)
                toggle.group = toggleGroup;

            toggle.isOn = false;

            TMP_Text numberLabel = FindNumberLabel(toggle);
            if (numberLabel != null)
            {
                numberLabel.gameObject.SetActive(item.showNumbers);
                numberLabel.text = value.ToString();
            }

            int captured = value;
            toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    OnToggleSelected(captured);
            });

            activeToggles.Add(toggle);
        }

        if (answers[currentRun] != Unanswered)
        {
            int idx = answers[currentRun] - item.startValue;
            if (idx >= 0 && idx < activeToggles.Count)
                activeToggles[idx].isOn = true;
        }
    }

    private TMP_Text FindNumberLabel(Toggle toggle)
    {
        Transform t = toggle.transform.Find("Number");
        if (t != null)
            return t.GetComponent<TMP_Text>();

        return toggle.GetComponentInChildren<TMP_Text>(true);
    }

    private void OnToggleSelected(int value)
    {
        answers[currentRun] = value;
        UpdateContinueInteractable();
    }

    private void UpdateContinueInteractable()
    {
        if (continueButton == null)
            return;

        if (requireAnswerBeforeContinue)
            continueButton.interactable = answers[currentRun] != Unanswered;
        else
            continueButton.interactable = true;
    }

    private void OnContinuePressed()
    {
        if (requireAnswerBeforeContinue && answers[currentRun] == Unanswered)
            return;

        currentRun++;
        ShowCurrent();
    }

    private void Finish()
    {
        if (questionnaireGroup != null)
            questionnaireGroup.SetActive(false);

        if (hideWallWhenFinished && wallRoot != null)
            wallRoot.SetActive(false);

        if (logResultToConsole)
            Debug.Log("[Questionnaire] Ergebnisse:\n" + BuildResultCsv());

        OnFinished?.Invoke(answers);
    }

    public string BuildResultCsv()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < runOrder.Count; i++)
        {
            RunRef r = runOrder[i];
            LikertItem item = sections[r.section].items[r.index];
            int value = (i < answers.Count) ? answers[i] : -1;
            sb.Append(item.id).Append(';').Append(value).Append('\n');
        }
        return sb.ToString();
    }

    private void BuildRunOrder()
    {
        runOrder.Clear();
        for (int s = 0; s < sections.Count; s++)
            for (int i = 0; i < sections[s].items.Count; i++)
                runOrder.Add(new RunRef { section = s, index = i });
    }

    private void BuildQuestionnaire()
    {
        sections.Clear();

        LikertSection cls = new LikertSection
        {
            heading = ExperimentStrings.ClsHeading,
            instruction = ""
        };
        string[] clsItems = ExperimentStrings.ClsItems;
        for (int i = 0; i < clsItems.Length; i++)
        {
            cls.items.Add(new LikertItem
            {
                id = "CLS" + (i + 1).ToString("00"),
                statement = clsItems[i],
                points = 11,
                startValue = 0,
                showNumbers = true,
                leftAnchor = ExperimentStrings.ClsLeft,
                rightAnchor = ExperimentStrings.ClsRight
            });
        }
        sections.Add(cls);

        LikertSection ipq = new LikertSection
        {
            heading = ExperimentStrings.IpqHeading,
            instruction = ""
        };
        ExperimentStrings.IpqItem[] ipqItems = ExperimentStrings.IpqItems();
        for (int i = 0; i < ipqItems.Length; i++)
        {
            ExperimentStrings.IpqItem item = ipqItems[i];
            AddIpq(ipq, item.id, item.statement, item.left, item.center, item.right);
        }
        sections.Add(ipq);

        LikertSection extra = new LikertSection
        {
            heading = ExperimentStrings.ExtraHeading,
            instruction = ExperimentStrings.ExtraInstruction
        };
        string[] exItems = ExperimentStrings.ExtraItems;
        for (int i = 0; i < exItems.Length; i++)
        {
            extra.items.Add(new LikertItem
            {
                id = "EXTRA" + (i + 1),
                statement = exItems[i],
                points = 7,
                startValue = 1,
                showNumbers = true,
                leftAnchor = ExperimentStrings.ExtraLeft,
                rightAnchor = ExperimentStrings.ExtraRight
            });
        }
        sections.Add(extra);
    }

    private void AddIpq(LikertSection sec, string id, string statement, string left, string center, string right)
    {
        sec.items.Add(new LikertItem
        {
            id = id,
            statement = statement,
            points = 7,
            startValue = -3,
            showNumbers = true,
            leftAnchor = left,
            centerAnchor = center,
            rightAnchor = right
        });
    }
}
