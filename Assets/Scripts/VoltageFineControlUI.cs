using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built fine control for the voltage knob: [-] slider [+].
/// Driven entirely by the canvas event system (ISDK ray / poke), so the
/// visible laser and the interaction always agree. Prefers docking into a
/// RectTransform named "VoltageFineControlSlot" if the panel provides one.
/// </summary>
public class VoltageFineControlUI : MonoBehaviour
{
    public float smallStep = 1f;

    private VoltageKnobInput knob;
    private Slider slider;
    private UiRepeatButton minusButton;
    private UiRepeatButton plusButton;
    private bool suppressSlider;

    public Slider Slider => slider;

    public static VoltageFineControlUI Create(VoltageKnobInput owner, TMP_Text voltageText)
    {
        RectTransform slot = FindSlot(voltageText, owner);

        var root = new GameObject("VoltageFineControl", typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();

        if (slot != null)
        {
            rect.SetParent(slot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        else
        {
            Transform parent = voltageText != null ? voltageText.transform.parent : owner.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 44f);

            Vector2 voltagePos = voltageText != null ? voltageText.rectTransform.anchoredPosition : new Vector2(0f, -108f);
            rect.anchoredPosition = new Vector2(voltagePos.x, voltagePos.y - 46f);
        }

        var ui = root.AddComponent<VoltageFineControlUI>();
        ui.knob = owner;
        ui.Build(voltageText);
        return ui;
    }

    private static RectTransform FindSlot(TMP_Text voltageText, VoltageKnobInput owner)
    {
        Transform searchRoot = voltageText != null ? voltageText.transform.parent : null;

        if (searchRoot == null && owner != null)
            searchRoot = owner.transform;

        while (searchRoot != null)
        {
            Transform slot = FindDeep(searchRoot, "VoltageFineControlSlot");

            if (slot != null)
                return slot as RectTransform;

            searchRoot = searchRoot.parent;
        }

        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);

            if (found != null)
                return found;
        }

        return null;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);

        if (slider != null)
            slider.interactable = visible;

        if (minusButton != null)
            minusButton.Button.interactable = visible;

        if (plusButton != null)
            plusButton.Button.interactable = visible;
    }

    public void SyncFromVoltage(float voltage, float min, float max)
    {
        if (slider == null)
            return;

        suppressSlider = true;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = voltage;
        suppressSlider = false;
    }

    private void Build(TMP_Text voltageText)
    {
        TMP_FontAsset font = VrWorldUi.ResolveFont(voltageText);
        var rect = (RectTransform)transform;

        const float buttonSize = 40f;

        minusButton = UiTheme.CreateStepButton(rect, "VoltageMinus", buttonSize, Vector2.zero, "-", font);
        var minusRect = (RectTransform)minusButton.transform;
        minusRect.anchorMin = new Vector2(0f, 0.5f);
        minusRect.anchorMax = new Vector2(0f, 0.5f);
        minusRect.pivot = new Vector2(0f, 0.5f);
        minusRect.anchoredPosition = Vector2.zero;

        plusButton = UiTheme.CreateStepButton(rect, "VoltagePlus", buttonSize, Vector2.zero, "+", font);
        var plusRect = (RectTransform)plusButton.transform;
        plusRect.anchorMin = new Vector2(1f, 0.5f);
        plusRect.anchorMax = new Vector2(1f, 0.5f);
        plusRect.pivot = new Vector2(1f, 0.5f);
        plusRect.anchoredPosition = Vector2.zero;

        slider = VrWorldUi.CreateSlider(rect, "VoltageSlider", new Vector2(100f, 28f), Vector2.zero);
        var sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.offsetMin = new Vector2(buttonSize + 12f, -14f);
        sliderRect.offsetMax = new Vector2(-(buttonSize + 12f), 14f);

        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(OnSliderChanged);

        minusButton.onStep.AddListener(() => Nudge(-smallStep));
        plusButton.onStep.AddListener(() => Nudge(smallStep));
    }

    private void OnSliderChanged(float value)
    {
        if (suppressSlider || knob == null)
            return;

        knob.SetVoltageFromExternal(value);
    }

    private void Nudge(float delta)
    {
        if (knob != null)
            knob.NudgeVoltage(delta);
    }
}
