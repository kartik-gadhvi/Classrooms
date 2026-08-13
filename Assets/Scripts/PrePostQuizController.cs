using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PrePostQuizController : MonoBehaviour
{
    [Serializable]
    public class QuizQuestion
    {
        [TextArea(2, 5)] public string question;
        [TextArea(1, 3)] public string answerA;
        [TextArea(1, 3)] public string answerB;
        [TextArea(1, 3)] public string answerC;
        [Range(0, 2)] public int correctIndex;
    }

    public enum QuizMode
    {
        LanguageSelect,
        PreQuiz,
        PostQuiz,
        Welcome
    }

    [Header("Root")]
    public GameObject wallRoot;
    public GameObject quizGroup;
    public GameObject resultGroup;

    [Header("Texts")]
    public TMP_Text modeText;
    public TMP_Text questionText;
    public TMP_Text answerTextA;
    public TMP_Text answerTextB;
    public TMP_Text answerTextC;
    public TMP_Text scoreText;
    public TMP_Text resultText;
    public TMP_Text continueText;

    [Header("Buttons")]
    public Button answerButtonA;
    public Button answerButtonB;
    public Button answerButtonC;
    public Button continueButton;

    [Header("Visual Feedback")]
    public Color normalColor = Color.white;
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;
    public float postFeedbackSeconds = 0.8f;
    public bool keepInspectorHighlightColor = true;

    [Header("Flow")]
    public bool startPreQuizOnStart = true;

    private readonly List<QuizQuestion> questions = new List<QuizQuestion>();
    private readonly List<int> preAnswers = new List<int>();
    private readonly List<int> postAnswers = new List<int>();

    private QuizMode currentMode;
    private int currentQuestionIndex;
    private int correctPostCount;

    private bool waitingForPostFeedback;
    private Coroutine feedbackRoutine;

    private Image imageA;
    private Image imageB;
    private Image imageC;

    public IReadOnlyList<int> PreAnswers => preAnswers;
    public IReadOnlyList<int> PostAnswers => postAnswers;
    public int CorrectPostCount => correctPostCount;
    public int QuestionCount => questions.Count;
    public QuizMode CurrentMode => currentMode;

    private void Awake()
    {
        if (wallRoot == null)
            wallRoot = gameObject;

        CacheButtonImages();
        SetupQuestions();
        SetupButtons();

        if (keepInspectorHighlightColor)
            KeepHighlightedColorFromInspector();
    }

    private void Start()
    {
        if (startPreQuizOnStart)
            ShowLanguageSelect();
        else
            HideWall();
    }

    private void CacheButtonImages()
    {
        if (answerButtonA != null)
            imageA = answerButtonA.GetComponent<Image>();

        if (answerButtonB != null)
            imageB = answerButtonB.GetComponent<Image>();

        if (answerButtonC != null)
            imageC = answerButtonC.GetComponent<Image>();
    }

    private void SetupButtons()
    {
        if (answerButtonA != null)
        {
            answerButtonA.onClick.RemoveAllListeners();
            answerButtonA.onClick.AddListener(() => SelectAnswer(0));
        }

        if (answerButtonB != null)
        {
            answerButtonB.onClick.RemoveAllListeners();
            answerButtonB.onClick.AddListener(() => SelectAnswer(1));
        }

        if (answerButtonC != null)
        {
            answerButtonC.onClick.RemoveAllListeners();
            answerButtonC.onClick.AddListener(() => SelectAnswer(2));
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(ContinueButtonPressed);
        }
    }

    private void KeepHighlightedColorFromInspector()
    {
        KeepHighlight(answerButtonA);
        KeepHighlight(answerButtonB);
        KeepHighlight(answerButtonC);
        KeepHighlight(continueButton);
    }

    private void KeepHighlight(Button button)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    public void StartPreQuiz()
    {
        StopFeedbackRoutineIfNeeded();

        currentMode = QuizMode.PreQuiz;
        currentQuestionIndex = 0;
        correctPostCount = 0;
        waitingForPostFeedback = false;

        preAnswers.Clear();
        SetupQuestions();

        ShowWall();
        ShowQuizGroupOnly();
        ShowQuestion();
    }

    public void StartPostQuiz()
    {
        StopFeedbackRoutineIfNeeded();

        currentMode = QuizMode.PostQuiz;
        currentQuestionIndex = 0;
        correctPostCount = 0;
        waitingForPostFeedback = false;

        postAnswers.Clear();
        SetupQuestions();

        ShowWall();
        ShowQuizGroupOnly();
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        if (currentQuestionIndex >= questions.Count)
        {
            FinishQuiz();
            return;
        }

        ClearButtonSelection();
        ResetButtonColors();
        SetAnswerButtonsInteractable(true);
        ShowAnswerButtons(true);
        ShowContinueButton(false);

        QuizQuestion q = questions[currentQuestionIndex];

        if (modeText != null)
            modeText.text = currentMode == QuizMode.PreQuiz ? ExperimentStrings.QuizPreTitle : ExperimentStrings.QuizPostTitle;

        if (questionText != null)
            questionText.text = (currentQuestionIndex + 1) + ". " + q.question;

        if (answerTextA != null)
            answerTextA.text = "A. " + q.answerA;

        if (answerTextB != null)
            answerTextB.text = "B. " + q.answerB;

        if (answerTextC != null)
            answerTextC.text = "C. " + q.answerC;

        if (resultText != null)
            resultText.text = "";

        RefreshScoreText();
        Canvas.ForceUpdateCanvases();
    }

    private void SelectAnswer(int selectedIndex)
    {
        if (currentMode == QuizMode.LanguageSelect)
        {
            LanguageManager.Set(selectedIndex == 1 ? AppLanguage.English : AppLanguage.German);
            StartPreQuiz();
            return;
        }

        if (waitingForPostFeedback)
            return;

        if (currentQuestionIndex < 0 || currentQuestionIndex >= questions.Count)
            return;

        QuizQuestion q = questions[currentQuestionIndex];

        if (currentMode == QuizMode.PreQuiz)
        {
            preAnswers.Add(selectedIndex);
            currentQuestionIndex++;
            ShowQuestion();
            return;
        }

        if (currentMode != QuizMode.PostQuiz)
            return;

        postAnswers.Add(selectedIndex);

        if (selectedIndex == q.correctIndex)
            correctPostCount++;

        ShowPostFeedback(selectedIndex, q.correctIndex);
        RefreshScoreText();

        waitingForPostFeedback = true;
        SetAnswerButtonsInteractable(false);

        feedbackRoutine = StartCoroutine(NextPostQuestionAfterDelay());
    }

    private IEnumerator NextPostQuestionAfterDelay()
    {
        yield return new WaitForSeconds(postFeedbackSeconds);

        waitingForPostFeedback = false;
        feedbackRoutine = null;

        currentQuestionIndex++;
        ShowQuestion();
    }

    private void ShowPostFeedback(int selectedIndex, int correctIndex)
    {
        ResetButtonColors();
        SetButtonColor(correctIndex, correctColor);

        if (selectedIndex != correctIndex)
            SetButtonColor(selectedIndex, wrongColor);
    }

    private void FinishQuiz()
    {
        StopFeedbackRoutineIfNeeded();
        ClearButtonSelection();
        ResetButtonColors();
        SetAnswerButtonsInteractable(false);

        if (currentMode == QuizMode.PreQuiz)
        {
            ShowWelcomePage();
            return;
        }

        if (currentMode == QuizMode.PostQuiz)
            ShowPostResult();
    }

    private void ShowWelcomePage()
    {
        currentMode = QuizMode.Welcome;

        ShowWall();
        ShowQuizAndResultGroups();
        ShowAnswerButtons(false);
        ShowContinueButton(true);

        if (modeText != null)
            modeText.text = ExperimentStrings.QuizWelcomeTitle;

        if (questionText != null)
            questionText.text = ExperimentStrings.WelcomeText;

        if (answerTextA != null)
            answerTextA.text = "";

        if (answerTextB != null)
            answerTextB.text = "";

        if (answerTextC != null)
            answerTextC.text = "";

        if (scoreText != null)
            scoreText.text = "";

        if (resultText != null)
            resultText.text = "";

        if (continueText != null)
            continueText.text = ExperimentStrings.Continue;

        Canvas.ForceUpdateCanvases();
    }

    private void ShowPostResult()
    {
        ShowResultGroupOnly();

        if (modeText != null)
            modeText.text = ExperimentStrings.QuizPostTitle;

        if (scoreText != null)
            scoreText.text = ExperimentStrings.QuizScore(correctPostCount, questions.Count);

        if (resultText != null)
            resultText.text = ExperimentStrings.QuizResult(correctPostCount, questions.Count);

        if (continueText != null)
            continueText.text = ExperimentStrings.Continue;

        ShowContinueButton(true);
    }

    private void ContinueButtonPressed()
    {
        if (currentMode == QuizMode.Welcome)
        {
            HideWall();
            return;
        }

        if (currentMode == QuizMode.PostQuiz)
        {
            HideWall();
            return;
        }
    }

    private void RefreshScoreText()
    {
        if (scoreText == null)
            return;

        if (currentMode == QuizMode.PostQuiz)
            scoreText.text = ExperimentStrings.QuizScore(correctPostCount, questions.Count);
        else
            scoreText.text = "";
    }

    private void ShowWall()
    {
        if (wallRoot != null)
            wallRoot.SetActive(true);
    }

    private void HideWall()
    {
        if (wallRoot != null)
            wallRoot.SetActive(false);
    }

    private void ShowQuizGroupOnly()
    {
        if (quizGroup != null)
            quizGroup.SetActive(true);

        if (resultGroup != null)
            resultGroup.SetActive(false);
    }

    private void ShowResultGroupOnly()
    {
        if (quizGroup != null)
            quizGroup.SetActive(false);

        if (resultGroup != null)
            resultGroup.SetActive(true);
    }

    private void ShowQuizAndResultGroups()
    {
        if (quizGroup != null)
            quizGroup.SetActive(true);

        if (resultGroup != null)
            resultGroup.SetActive(true);
    }

    private void ShowAnswerButtons(bool show)
    {
        if (answerButtonA != null)
            answerButtonA.gameObject.SetActive(show);

        if (answerButtonB != null)
            answerButtonB.gameObject.SetActive(show);

        if (answerButtonC != null)
            answerButtonC.gameObject.SetActive(show);
    }

    private void ShowContinueButton(bool show)
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(show);
    }

    private void SetAnswerButtonsInteractable(bool interactable)
    {
        if (answerButtonA != null)
            answerButtonA.interactable = interactable;

        if (answerButtonB != null)
            answerButtonB.interactable = interactable;

        if (answerButtonC != null)
            answerButtonC.interactable = interactable;
    }

    private void ResetButtonColors()
    {
        if (imageA != null)
            imageA.color = normalColor;

        if (imageB != null)
            imageB.color = normalColor;

        if (imageC != null)
            imageC.color = normalColor;
    }

    private void SetButtonColor(int index, Color color)
    {
        if (index == 0 && imageA != null)
            imageA.color = color;
        else if (index == 1 && imageB != null)
            imageB.color = color;
        else if (index == 2 && imageC != null)
            imageC.color = color;
    }

    private void ClearButtonSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (answerButtonA != null)
            answerButtonA.OnDeselect(null);

        if (answerButtonB != null)
            answerButtonB.OnDeselect(null);

        if (answerButtonC != null)
            answerButtonC.OnDeselect(null);

        if (continueButton != null)
            continueButton.OnDeselect(null);
    }

    private void StopFeedbackRoutineIfNeeded()
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        waitingForPostFeedback = false;
    }

    private void ShowLanguageSelect()
    {
        currentMode = QuizMode.LanguageSelect;

        ShowWall();
        ShowQuizGroupOnly();
        ShowAnswerButtons(true);
        if (answerButtonC != null)
            answerButtonC.gameObject.SetActive(false);
        ShowContinueButton(false);
        SetAnswerButtonsInteractable(true);
        ClearButtonSelection();
        ResetButtonColors();

        if (modeText != null)
            modeText.text = ExperimentStrings.LanguageTitle;

        if (questionText != null)
            questionText.text = ExperimentStrings.LanguageQuestion;

        if (answerTextA != null)
            answerTextA.text = "A. " + ExperimentStrings.LanguageGerman;

        if (answerTextB != null)
            answerTextB.text = "B. " + ExperimentStrings.LanguageEnglish;

        if (answerTextC != null)
            answerTextC.text = "";

        if (scoreText != null)
            scoreText.text = "";

        if (resultText != null)
            resultText.text = "";

        Canvas.ForceUpdateCanvases();
    }

    private void SetupQuestions()
    {
        questions.Clear();

        for (int i = 0; i < ExperimentStrings.QuizCount; i++)
        {
            ExperimentStrings.QuizItem item = ExperimentStrings.Quiz(i);
            questions.Add(new QuizQuestion
            {
                question = item.question,
                answerA = item.answerA,
                answerB = item.answerB,
                answerC = item.answerC,
                correctIndex = item.correctIndex
            });
        }
    }
}