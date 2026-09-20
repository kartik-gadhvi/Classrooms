using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialChoiceUI : MonoBehaviour
{
    public const float DialogueY = 34f;
    public const float DialogueHeight = 212f;
    public const float HintY = -104f;
    public const float HintHeight = 40f;
    public const float ButtonRowY = -156f;
    public const float ButtonHeight = 48f;
    public const float RepeatX = 190f;

    private BottomTutorialController tutorial;
    private Button yesButton;
    private Button noButton;
    private Button repeatButton;
    private TMP_Text yesLabel;
    private TMP_Text noLabel;
    private TMP_Text repeatLabel;

    public static TutorialChoiceUI Ensure(BottomTutorialController tutorial)
    {
        if (tutorial == null || tutorial.dialogueRoot == null)
            return null;

        var existing = tutorial.dialogueRoot.GetComponent<TutorialChoiceUI>();
        if (existing != null)
            return existing;

        return tutorial.dialogueRoot.AddComponent<TutorialChoiceUI>();
    }

    public void Bind(BottomTutorialController owner)
    {
        tutorial = owner;
        BuildIfNeeded();
        RefreshLanguage();
    }

    public void Refresh(bool showYes, bool showNo)
    {
        BuildIfNeeded();

        if (yesButton != null)
            yesButton.gameObject.SetActive(showYes);
        if (noButton != null)
            noButton.gameObject.SetActive(showNo);
        if (repeatButton != null)
        {
            repeatButton.gameObject.SetActive(true);
            var rect = repeatButton.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = new Vector2(showYes || showNo ? RepeatX : 0f, ButtonRowY);
        }

        RefreshLanguage();
    }

    private void BuildIfNeeded()
    {
        if (repeatButton != null || tutorial == null || tutorial.dialogueRoot == null)
            return;

        var rootRect = tutorial.dialogueRoot.GetComponent<RectTransform>();
        if (rootRect != null && rootRect.sizeDelta.y < 280f)
            rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, 280f);

        Transform panel = tutorial.dialogueText != null ? tutorial.dialogueText.transform.parent : tutorial.dialogueRoot.transform;
        TMP_FontAsset font = VrWorldUi.ResolveFont(tutorial.dialogueText);

        // Fixed zones on the 880x380 dialogue panel (centre pivot):
        //   status pill  +184..+152 | dialogue text +140..-72 | hint -84..-124 | buttons -132..-180
        if (tutorial.dialogueText != null)
        {
            var dialogueRect = tutorial.dialogueText.rectTransform;
            dialogueRect.anchoredPosition = new Vector2(0f, DialogueY);
            dialogueRect.sizeDelta = new Vector2(820f, DialogueHeight);
        }

        if (tutorial.buttonHintText != null)
        {
            var hintRect = tutorial.buttonHintText.rectTransform;
            hintRect.anchoredPosition = new Vector2(0f, HintY);
            hintRect.sizeDelta = new Vector2(820f, HintHeight);
        }

        yesButton = VrWorldUi.CreateButton(panel, "YesButton", new Vector2(170f, ButtonHeight), new Vector2(-230f, ButtonRowY), "", font, new Color(0.18f, 0.52f, 0.32f, 0.95f));
        noButton = VrWorldUi.CreateButton(panel, "NoButton", new Vector2(170f, ButtonHeight), new Vector2(-40f, ButtonRowY), "", font, new Color(0.55f, 0.22f, 0.22f, 0.95f));
        repeatButton = VrWorldUi.CreateButton(panel, "RepeatButton", new Vector2(220f, ButtonHeight), new Vector2(RepeatX, ButtonRowY), "", font, new Color(0.2f, 0.35f, 0.62f, 0.95f));

        yesLabel = yesButton.GetComponentInChildren<TMP_Text>();
        noLabel = noButton.GetComponentInChildren<TMP_Text>();
        repeatLabel = repeatButton.GetComponentInChildren<TMP_Text>();

        yesButton.onClick.AddListener(() => tutorial.AnswerYes());
        noButton.onClick.AddListener(() => tutorial.AnswerNo());
        repeatButton.onClick.AddListener(() => tutorial.ReplayCurrentExplanation());
    }

    private void RefreshLanguage()
    {
        if (yesLabel != null) yesLabel.text = ExperimentStrings.ChoiceYes;
        if (noLabel != null) noLabel.text = ExperimentStrings.ChoiceNo;
        if (repeatLabel != null) repeatLabel.text = ExperimentStrings.ChoiceRepeat;
    }
}
