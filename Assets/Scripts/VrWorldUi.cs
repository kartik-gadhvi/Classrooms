using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class VrWorldUi
{
    public static TMP_FontAsset ResolveFont(TMP_Text source)
    {
        if (source != null && source.font != null)
            return source.font;

        return TMP_Settings.defaultFontAsset;
    }

    public static Image CreateImage(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    public static TMP_Text CreateText(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, TMP_FontAsset font, float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    public static Button CreateButton(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, string label, TMP_FontAsset font, Color background)
    {
        var image = CreateImage(parent, name, size, anchoredPosition, background);
        UiTheme.ApplyCard(image, background, 1.8f);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        UiTheme.ApplySelectableColors(button);

        var text = CreateText(image.transform, "Label", size, Vector2.zero, font, Mathf.Max(16f, size.y * 0.42f), TextAlignmentOptions.Center);
        text.text = label;
        text.color = UiTheme.TextPrimary;
        return button;
    }

    public static Slider CreateSlider(Transform parent, string name, Vector2 size, Vector2 anchoredPosition)
    {
        var root = new GameObject(name, typeof(RectTransform));
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = size;
        rootRect.anchoredPosition = anchoredPosition;

        var background = CreateImage(rootRect, "Background", Vector2.zero, Vector2.zero, UiTheme.Track);
        Stretch(background.rectTransform);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.SetParent(rootRect, false);
        Stretch(fillAreaRect, 8f, 4f);

        var fill = CreateImage(fillAreaRect, "Fill", Vector2.zero, Vector2.zero, UiTheme.Accent);
        Stretch(fill.rectTransform);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.SetParent(rootRect, false);
        Stretch(handleAreaRect, 8f, 0f);

        var handle = CreateImage(handleAreaRect, "Handle", new Vector2(26f, 26f), Vector2.zero, Color.white);

        var slider = root.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        UiTheme.StyleSlider(slider, 26f, Mathf.Min(10f, size.y * 0.4f));
        return slider;
    }

    public static bool TryGetRayHit(RectTransform rect, Transform rayOrigin, float maxDistance, float padding, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (rect == null || rayOrigin == null)
            return false;

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        Plane plane = new Plane(rect.forward, rect.position);
        if (!plane.Raycast(ray, out float distance))
            return false;
        if (distance < 0f || distance > maxDistance)
            return false;

        localPoint = rect.InverseTransformPoint(ray.GetPoint(distance));
        Rect padded = rect.rect;
        padded.xMin -= padding;
        padded.yMin -= padding;
        padded.width += padding * 2f;
        padded.height += padding * 2f;
        return padded.Contains(localPoint);
    }

    public static float HorizontalSliderValue(RectTransform rect, Vector2 localPoint, float min, float max)
    {
        float normalized = Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, localPoint.x);
        return Mathf.Lerp(min, max, Mathf.Clamp01(normalized));
    }

    public static Transform FindLeftRayOrigin()
    {
        return FindNamed("LeftHandAnchor")
            ?? FindNamed("LeftControllerAnchor")
            ?? FindNamed("LeftHand")
            ?? (Camera.main != null ? Camera.main.transform : null);
    }

    private static Transform FindNamed(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.transform : null;
    }

    private static void Stretch(RectTransform rect, float horizontalInset = 0f, float verticalInset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontalInset, verticalInset);
        rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
