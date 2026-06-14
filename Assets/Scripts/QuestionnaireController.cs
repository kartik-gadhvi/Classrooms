using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Likert-Fragebögen, die NACH dem Post-Quiz angezeigt werden.
/// Der PrePostQuizController bleibt unverändert. Dieses Skript hängt sich nur
/// über den vorhandenen "Weiter"-Button und die public-Felder des Quiz ein.
///
/// Radiobuttons werden zur Laufzeit aus einem Prefab erzeugt, daher funktioniert
/// das Skript mit beliebig vielen Stufen (11, 7, ...) ohne manuelles Platzieren.
/// </summary>
public class QuestionnaireController : MonoBehaviour
{
    [Serializable]
    public class LikertItem
    {
        public string id = "";                 // z. B. "CLS01", "IPQ_G1" -> für Logging
        [TextArea(2, 4)] public string statement = "";
        public int points = 7;                 // Anzahl der Radiobuttons
        public int startValue = 1;             // Wert unter dem linken Button (1, -3, 0 ...)
        public bool showNumbers = true;        // Zahlen unter den Buttons anzeigen
        public string leftAnchor = "";
        public string centerAnchor = "";       // optional, "" = kein Mittel-Anker
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
    [Tooltip("Referenz auf das vorhandene Quiz, um den Modus zu prüfen und Gruppen umzuschalten.")]
    public PrePostQuizController prePostQuiz;
    [Tooltip("Der vorhandene 'Weiter'-Button nach dem Post-Quiz. Wird gehookt, um die Fragebögen zu starten.")]
    public Button postQuizContinueButton;
    public bool startAfterPostQuiz = true;

    [Header("Root-Objekte (gleiche Wall wie das Quiz)")]
    public GameObject wallRoot;            // wird beim Start der Fragebögen wieder eingeblendet
    public GameObject quizGroup;           // Multiple-Choice-Gruppe -> wird ausgeblendet
    public GameObject resultGroup;         // Ergebnis-Gruppe -> wird ausgeblendet
    public GameObject questionnaireGroup;  // NEUE Gruppe, die du für die Likert-Skalen baust

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
    [Tooltip("Container mit Horizontal Layout Group + Toggle Group.")]
    public RectTransform radioContainer;
    [Tooltip("Prefab: ein Toggle mit einem optionalen Zahlen-Label (TMP) namens 'Number'.")]
    public Toggle radioButtonPrefab;
    [Tooltip("Optional. Wird sonst automatisch am Container gesucht.")]
    public ToggleGroup toggleGroup;

    [Header("Weiter-Button (im Fragebogen)")]
    public Button continueButton;

    [Header("Flow")]
    public bool requireAnswerBeforeContinue = true;
    public bool hideWallWhenFinished = true;

    [Header("Events")]
    public bool logResultToConsole = true;

    /// <summary>Wird ausgelöst, wenn alle Fragebögen beantwortet sind. Antworten in Run-Reihenfolge.</summary>
    public event Action<IReadOnlyList<int>> OnFinished;

    private struct RunRef { public int section; public int index; }

    private readonly List<LikertSection> sections = new List<LikertSection>();
    private readonly List<RunRef> runOrder = new List<RunRef>();
    private readonly List<int> answers = new List<int>();   // -1 = noch nicht beantwortet
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
        // Listener wird in Start hinzugefügt -> läuft NACH dem Listener des Quiz,
        // der die Wall ausblendet. Wir blenden sie danach wieder ein.
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

    /// <summary>Kann auch manuell aufgerufen werden (z. B. aus einem UnityEvent).</summary>
    public void BeginQuestionnaire()
    {
        if (started)
            return;

        started = true;

        if (wallRoot != null) wallRoot.SetActive(true);
        if (quizGroup != null) quizGroup.SetActive(false);
        if (resultGroup != null) resultGroup.SetActive(false);
        if (questionnaireGroup != null) questionnaireGroup.SetActive(true);

        answers.Clear();
        for (int i = 0; i < runOrder.Count; i++)
            answers.Add(-1);

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
        if (progressText != null) progressText.text = "Frage " + (currentRun + 1) + " / " + runOrder.Count;

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

            toggle.isOn = false;   // vor dem Listener -> kein Fehl-Trigger

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

        // Falls die Frage schon einmal beantwortet wurde (Zurück-Navigation o. Ä.)
        if (answers[currentRun] != -1)
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
            continueButton.interactable = answers[currentRun] != -1;
        else
            continueButton.interactable = true;
    }

    private void OnContinuePressed()
    {
        if (requireAnswerBeforeContinue && answers[currentRun] == -1)
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

    /// <summary>Ergebnis als CSV ("id;wert") in Run-Reihenfolge. Für dein Logging-System.</summary>
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

    // ---------------------------------------------------------------------
    //  Inhalte (nur deutsche Varianten, Wortlaut unverändert)
    // ---------------------------------------------------------------------
    private void BuildQuestionnaire()
    {
        sections.Clear();

        // === Abschnitt 1: Cognitive Load (11-stufig, Anker nur an den Enden) ===
        LikertSection cls = new LikertSection
        {
            heading = "Messung der kognitiven Belastung während der Anwendung",
            instruction = ""
        };
        string clsLeft = "trifft überhaupt nicht zu";
        string clsRight = "trifft vollständig zu";
        string[] clsItems =
        {
            "Das in der Simulation behandelte Thema war sehr komplex.",
            "Die Simulation umfasste Verfahren, die ich als sehr komplex empfand.",
            "Die Simulation umfasste Konzepte und Definitionen, die ich als sehr komplex empfand.",
            "Die in der Simulation verwendeten Anweisungen und/oder Erklärungen waren sehr unklar.",
            "Die in der Simulation verwendeten Anweisungen und/oder Erklärungen waren in Bezug auf das Lernen sehr ineffektiv.",
            "Die in der Simulation verwendeten Anweisungen und/oder Erklärungen waren voller unklarer Inhalte.",
            "Die in der Simulation verwendete Interaktionstechnik war sehr unklar.",
            "Die in der Simulation verwendete Interaktionstechnik war in Bezug auf das Lernen sehr ineffektiv.",
            "Die in der Simulation verwendete Interaktionstechnik erschwerte das Lernen.",
            "Die in der Simulation verwendete Interaktionstechnik war schwer zu beherrschen.",
            "Die Elemente in der virtuellen Umgebung machten das Lernen sehr unklar.",
            "Die virtuelle Umgebung war in Bezug auf das Lernen sehr ineffektiv.",
            "Die virtuelle Umgebung war voller irrelevanter Inhalte.",
            "Es war schwierig, die relevanten Lerninformationen in der virtuellen Umgebung zu finden.",
            "Die Simulation hat mein Verständnis der behandelten Themen wirklich verbessert.",
            "Die Simulation hat mein Wissen und Verständnis der Laborsicherheit wirklich verbessert.",
            "Die Simulation hat mein Verständnis der behandelten Verfahren wirklich verbessert.",
            "Die Simulation hat mein Verständnis der Konzepte und Definitionen wirklich verbessert."
        };
        for (int i = 0; i < clsItems.Length; i++)
        {
            cls.items.Add(new LikertItem
            {
                id = "CLS" + (i + 1).ToString("00"),
                statement = clsItems[i],
                points = 11,
                startValue = 0,        // 0..10; auf 1 ändern für 1..11
                showNumbers = true,
                leftAnchor = clsLeft,
                rightAnchor = clsRight
            });
        }
        sections.Add(cls);

        // === Abschnitt 2: IPQ (7-stufig, -3 bis +3, Anker pro Item) ===
        LikertSection ipq = new LikertSection
        {
            heading = "Messung des Präsenz-Gefühls in der virtuellen Umgebung",
            instruction = ""
        };
        AddIpq(ipq, "IPQ_G1", "In der computererzeugten Welt hatte ich den Eindruck, dort gewesen zu sein...", "überhaupt nicht", "", "sehr stark");
        AddIpq(ipq, "IPQ_SP1", "Ich hatte das Gefühl, daß die virtuelle Umgebung hinter mir weitergeht.", "trifft gar nicht zu", "", "trifft völlig zu");
        AddIpq(ipq, "IPQ_SP2", "Ich hatte das Gefühl, nur Bilder zu sehen.", "trifft gar nicht zu", "", "trifft völlig zu");
        AddIpq(ipq, "IPQ_SP3", "Ich hatte nicht das Gefühl, in dem virtuellen Raum zu sein.", "hatte nicht das Gefühl", "", "hatte das Gefühl");
        AddIpq(ipq, "IPQ_SP4", "Ich hatte das Gefühl, in dem virtuellen Raum zu handeln statt etwas von außen zu bedienen.", "trifft gar nicht zu", "", "trifft völlig zu");
        AddIpq(ipq, "IPQ_SP5", "Ich fühlte mich im virtuellen Raum anwesend.", "trifft gar nicht zu", "", "trifft völlig zu");
        AddIpq(ipq, "IPQ_INV1", "Wie bewußt war Ihnen die reale Welt, während Sie sich durch die virtuelle Welt bewegten (z.B. Geräusche, Raumtemperatur, andere Personen etc.)?", "extrem bewußt", "mittelmäßig bewußt", "unbewußt");
        AddIpq(ipq, "IPQ_INV2", "Meine reale Umgebung war mir nicht mehr bewußt.", "trifft gar nicht zu", "", "trifft völlig zu");
        AddIpq(ipq, "IPQ_INV3", "Ich achtete noch auf die reale Umgebung.", "trifft gar nicht zu", "", "trifft völlig zu");
        AddIpq(ipq, "IPQ_INV4", "Meine Aufmerksamkeit war von der virtuellen Welt völlig in Bann gezogen.", "trifft gar nicht zu", "", "trifft völlig zu");
        AddIpq(ipq, "IPQ_REAL1", "Wie real erschien Ihnen die virtuelle Umgebung?", "vollkommen real", "weder noch", "gar nicht real");
        AddIpq(ipq, "IPQ_REAL2", "Wie sehr glich Ihr Erleben der virtuellen Umgebung dem Erleben einer realen Umgebung?", "überhaupt nicht", "etwas", "vollständig");
        AddIpq(ipq, "IPQ_REAL3", "Wie real erschien Ihnen die virtuelle Welt?", "wie eine vorgestellte Welt", "", "nicht zu unterscheiden von der realen Welt");
        AddIpq(ipq, "IPQ_REAL4", "Die virtuelle Welt erschien mir wirklicher als die reale Welt.", "trifft gar nicht zu", "", "trifft völlig zu");
        sections.Add(ipq);

        // === Abschnitt 3: Weitere Fragen (7-stufig, 1 bis 7) ===
        LikertSection extra = new LikertSection
        {
            heading = "Weitere Fragen",
            instruction = "Bewerten Sie auf einer Skala von 1 (stimme überhaupt nicht zu) bis 7 (stimme voll zu)."
        };
        string exLeft = "stimme überhaupt nicht zu";
        string exRight = "stimme voll zu";
        string[] exItems =
        {
            "Die Gestaltung des virtuellen Klassenraums hat mich vom Lerninhalt abgelenkt.",
            "Ich habe die Umgebungselemente während des Lernens bewusst wahrgenommen.",
            "Die Ausstattung des Raumes hat meine Konzentration beeinträchtigt.",
            "Der virtuelle Klassenraum hat mir das Gefühl gegeben, in einer echten Lernsituation zu sein.",
            "Ich hätte mir mehr / weniger Ausstattung im Raum gewünscht.",
            "Ich hätte es hilfreich gefunden, wenn in der Anwendung noch weitere Personen anwesend gewesen wären (virtuell, also NPCs oder real, also Mitschüler)."
        };
        for (int i = 0; i < exItems.Length; i++)
        {
            extra.items.Add(new LikertItem
            {
                id = "EXTRA" + (i + 1),
                statement = exItems[i],
                points = 7,
                startValue = 1,
                showNumbers = true,
                leftAnchor = exLeft,
                rightAnchor = exRight
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
            startValue = -3,       // -3 .. +3
            showNumbers = true,
            leftAnchor = left,
            centerAnchor = center,
            rightAnchor = right
        });
    }
}