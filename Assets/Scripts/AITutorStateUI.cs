using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum AITutorState
{
    Idle,
    Listening,
    Thinking,
    Speaking,
    Unavailable
}

public class AITutorStateUI : MonoBehaviour
{
    private Image badge;
    private TMP_Text label;
    private AITutorState current = (AITutorState)(-1);

    public static AITutorStateUI Create(RectTransform parent, TMP_Text fontSource)
    {
        var root = new GameObject("AITutorState", typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        // Status strip zone: top 32px of the dialogue panel, above the dialogue text.
        rect.sizeDelta = new Vector2(820f, 32f);
        rect.anchoredPosition = new Vector2(0f, -6f);

        var ui = root.AddComponent<AITutorStateUI>();

        var pill = VrWorldUi.CreateImage(rect, "Pill", new Vector2(820f, 32f), new Vector2(0f, -16f), UiTheme.CardSoft);
        UiTheme.ApplyCard(pill, UiTheme.CardSoft, 2f);
        pill.raycastTarget = false;

        ui.badge = VrWorldUi.CreateImage(rect, "Badge", new Vector2(14f, 14f), new Vector2(-388f, -16f), Color.gray);
        ui.badge.sprite = UiTheme.Circle;
        ui.badge.raycastTarget = false;
        ui.label = VrWorldUi.CreateText(rect, "Label", new Vector2(740f, 30f), new Vector2(20f, -16f), VrWorldUi.ResolveFont(fontSource), 18f, TextAlignmentOptions.MidlineLeft);
        ui.SetState(AITutorState.Idle);
        return ui;
    }

    public void SetState(AITutorState state, string overrideText = null)
    {
        if (current == state && overrideText == null)
            return;

        current = state;
        Color color;
        string text;

        switch (state)
        {
            case AITutorState.Listening:
                color = new Color(1f, 0.22f, 0.22f, 1f);
                text = ExperimentStrings.AiListening;
                break;
            case AITutorState.Thinking:
                color = new Color(1f, 0.72f, 0.18f, 1f);
                text = ExperimentStrings.AiThinking;
                break;
            case AITutorState.Speaking:
                color = new Color(0.25f, 0.85f, 0.45f, 1f);
                text = ExperimentStrings.AiSpeaking;
                break;
            case AITutorState.Unavailable:
                color = new Color(0.95f, 0.35f, 0.2f, 1f);
                text = ExperimentStrings.AiUnavailable;
                break;
            default:
                color = new Color(0.45f, 0.5f, 0.58f, 1f);
                text = ExperimentStrings.AiIdle;
                break;
        }

        if (!string.IsNullOrEmpty(overrideText))
            text = overrideText;

        if (badge != null)
            badge.color = color;
        if (label != null)
        {
            label.color = color;
            label.text = text;
        }
    }

    private void Update()
    {
        if (current != AITutorState.Listening && current != AITutorState.Thinking)
            return;

        if (badge == null)
            return;

        float pulse = 0.55f + 0.45f * (Mathf.Sin(Time.unscaledTime * 6f) * 0.5f + 0.5f);
        Color c = badge.color;
        c.a = pulse;
        badge.color = c;
    }
}
