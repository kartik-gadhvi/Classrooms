using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Fires onStep immediately on pointer down, then repeats while held.
/// Works with any pointer source that goes through the EventSystem,
/// including the Meta ISDK ray via PointableCanvasModule.
/// Wire actions to onStep, not to the Button's onClick.
/// </summary>
[RequireComponent(typeof(Button))]
public class UiRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("How long the button must be held before it starts repeating. A normal click never repeats.")]
    public float repeatDelay = 0.75f;
    public float repeatInterval = 0.15f;
    [Tooltip("Ignore a second press arriving within this time (ray + poke modules can both report the same press).")]
    public float debounceSeconds = 0.2f;
    public UnityEvent onStep = new UnityEvent();

    private Button button;
    private bool held;
    private float holdStartTime;
    private float lastStepTime;
    private float lastPressTime = -10f;

    public Button Button
    {
        get
        {
            if (button == null)
                button = GetComponent<Button>();
            return button;
        }
    }

    private void OnDisable()
    {
        held = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Button.interactable)
            return;

        if (Time.unscaledTime - lastPressTime < debounceSeconds)
            return;

        lastPressTime = Time.unscaledTime;
        held = true;
        holdStartTime = Time.unscaledTime;
        lastStepTime = Time.unscaledTime;
        onStep.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        held = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        held = false;
    }

    private void Update()
    {
        if (!held)
            return;

        if (!Button.interactable)
        {
            held = false;
            return;
        }

        if (Time.unscaledTime - holdStartTime < repeatDelay)
            return;

        if (Time.unscaledTime - lastStepTime >= repeatInterval)
        {
            lastStepTime = Time.unscaledTime;
            onStep.Invoke();
        }
    }
}
