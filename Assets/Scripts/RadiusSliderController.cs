using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the droplet-radius task UI. The slider is driven natively by the
/// Meta ISDK ray through the canvas event system (no manual raycasting),
/// with -/+ step buttons for fine adjustment.
/// </summary>
public class RadiusSliderController : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;
    public Slider radiusSlider;
    public TMP_Text radiusText;
    public UiRepeatButton minusButton;
    public UiRepeatButton plusButton;

    [Header("Target")]
    public SpraySpawner spraySpawner;

    [Header("Radius Settings")]
    public float minRadiusMicrometer = 0.3f;
    public float maxRadiusMicrometer = 2.0f;
    public float defaultRadiusMicrometer = 0.3f;
    public float smallStepMicrometer = 0.01f;

    [Header("Behaviour")]
    public bool applyOnlyDuringTask = true;
    public bool clearDropsWhenRadiusChanges = true;
    public bool enableTutorialRadiusModeOnStart = false;
    public bool hidePanelOnStart = false;

    [Header("Logging")]
    public bool logRadiusTaskToSessionLogger = true;

    private bool isActiveForTask;
    private float lastAppliedRadius = -999f;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (radiusSlider == null)
            radiusSlider = GetComponentInChildren<Slider>(true);

        // Re-apply the theme at runtime: the slider subtree lives inside a prefab
        // instance whose serialized style overrides are unreliable.
        if (radiusSlider != null)
            UiTheme.StyleSlider(radiusSlider, 26f, 10f);

        SetupSlider();

        if (minusButton != null)
            minusButton.onStep.AddListener(() => NudgeRadius(-smallStepMicrometer));

        if (plusButton != null)
            plusButton.onStep.AddListener(() => NudgeRadius(smallStepMicrometer));
    }

    private void OnEnable()
    {
        LanguageManager.Changed += OnLanguageChanged;
    }

    private void OnDisable()
    {
        LanguageManager.Changed -= OnLanguageChanged;
    }

    private void OnLanguageChanged(AppLanguage language)
    {
        if (radiusSlider != null)
            UpdateRadiusText(radiusSlider.value);
    }

    private void Start()
    {
        if (hidePanelOnStart && panelRoot != null)
            panelRoot.SetActive(false);

        if (enableTutorialRadiusModeOnStart)
        {
            StartRadiusTask();
        }
        else
        {
            isActiveForTask = false;
            SetControlsInteractable(false);
        }
    }

    private void SetupSlider()
    {
        if (radiusSlider == null)
            return;

        radiusSlider.minValue = minRadiusMicrometer;
        radiusSlider.maxValue = maxRadiusMicrometer;
        radiusSlider.wholeNumbers = false;
        radiusSlider.value = defaultRadiusMicrometer;

        radiusSlider.onValueChanged.RemoveAllListeners();
        radiusSlider.onValueChanged.AddListener(OnSliderValueChanged);

        UpdateRadiusText(radiusSlider.value);
    }

    public void StartRadiusTask()
    {
        isActiveForTask = true;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        SetControlsInteractable(true);
        SetRadius(defaultRadiusMicrometer, true);
    }

    public void EndRadiusTask()
    {
        isActiveForTask = false;

        SetControlsInteractable(false);

        if (spraySpawner != null)
            spraySpawner.ReturnToRandomModeAndClearDrops();

        if (panelRoot != null && hidePanelOnStart)
            panelRoot.SetActive(false);

        if (logRadiusTaskToSessionLogger && SessionLogger.Instance != null)
            SessionLogger.Instance.MarkRadiusTaskCompleted();
    }

    public float GetCurrentRadiusMicrometer()
    {
        return radiusSlider != null ? radiusSlider.value : defaultRadiusMicrometer;
    }

    public void NudgeRadius(float delta)
    {
        if (radiusSlider == null)
            return;

        if (applyOnlyDuringTask && !isActiveForTask)
            return;

        SetRadius(radiusSlider.value + delta, clearDropsWhenRadiusChanges);
    }

    public void SetRadius(float radiusMicrometer, bool clearExistingDrops)
    {
        float radius = Mathf.Clamp(radiusMicrometer, minRadiusMicrometer, maxRadiusMicrometer);

        if (radiusSlider != null)
            radiusSlider.value = radius;

        UpdateRadiusText(radius);
        ApplyRadiusToSprayer(radius, clearExistingDrops);
    }

    private void SetControlsInteractable(bool interactable)
    {
        if (radiusSlider != null)
            radiusSlider.interactable = interactable;

        if (minusButton != null)
            minusButton.Button.interactable = interactable;

        if (plusButton != null)
            plusButton.Button.interactable = interactable;
    }

    private void OnSliderValueChanged(float value)
    {
        UpdateRadiusText(value);

        if (applyOnlyDuringTask && !isActiveForTask)
            return;

        ApplyRadiusToSprayer(value, clearDropsWhenRadiusChanges);
    }

    private void ApplyRadiusToSprayer(float radius, bool clearExistingDrops)
    {
        if (Mathf.Abs(radius - lastAppliedRadius) < 0.005f)
            return;

        lastAppliedRadius = radius;

        if (spraySpawner != null)
        {
            spraySpawner.EnableTutorialRadiusMode();
            spraySpawner.SetTutorialRadiusMicrometer(radius, clearExistingDrops);
        }
    }

    private void UpdateRadiusText(float radius)
    {
        if (radiusText != null)
            radiusText.text = ExperimentStrings.RadiusSlider(radius.ToString("0.00") + " µm");
    }
}
