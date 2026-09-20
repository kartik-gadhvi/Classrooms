using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared visual language for all experiment / chat UI.
/// Sprites are loaded from Resources/UITheme (generated assets) with a
/// procedural fallback so runtime-built UI never breaks.
/// </summary>
public static class UiTheme
{
    // ---- Palette ----
    public static readonly Color Card          = new Color(0.078f, 0.094f, 0.129f, 0.97f);
    public static readonly Color CardSoft      = new Color(0.129f, 0.153f, 0.208f, 1f);
    public static readonly Color Control       = new Color(0.180f, 0.212f, 0.282f, 1f);
    public static readonly Color Accent        = new Color(0.298f, 0.553f, 1f, 1f);
    public static readonly Color TextPrimary   = new Color(0.949f, 0.961f, 0.980f, 1f);
    public static readonly Color TextMuted     = new Color(0.608f, 0.647f, 0.733f, 1f);
    public static readonly Color Track         = new Color(1f, 1f, 1f, 0.10f);
    public static readonly Color Divider       = new Color(1f, 1f, 1f, 0.08f);
    public static readonly Color Good          = new Color(0.204f, 0.827f, 0.600f, 1f);
    public static readonly Color Warn          = new Color(0.984f, 0.749f, 0.141f, 1f);
    public static readonly Color Danger        = new Color(0.973f, 0.443f, 0.443f, 1f);

    private static Sprite roundRect;
    private static Sprite circle;

    public static Sprite RoundRect
    {
        get
        {
            if (roundRect == null)
                roundRect = Resources.Load<Sprite>("UITheme/roundrect");
            if (roundRect == null)
                roundRect = BuildRoundRect();
            return roundRect;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
                circle = Resources.Load<Sprite>("UITheme/circle");
            if (circle == null)
                circle = BuildCircle();
            return circle;
        }
    }

    /// <summary>Style an Image as a rounded card. Higher cornerScale = smaller corner radius.</summary>
    public static void ApplyCard(Image img, Color color, float cornerScale = 1f)
    {
        if (img == null)
            return;

        img.sprite = RoundRect;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.pixelsPerUnitMultiplier = Mathf.Max(0.1f, cornerScale);
    }

    /// <summary>Tint block shared by every themed selectable.</summary>
    public static void ApplySelectableColors(Selectable selectable)
    {
        if (selectable == null)
            return;

        var colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.14f, 1.16f, 1.2f, 1f);
        colors.pressedColor = new Color(0.72f, 0.76f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.65f, 0.45f);
        colors.fadeDuration = 0.08f;
        selectable.colors = colors;
    }

    /// <summary>Restyle an existing Unity Slider into the themed look (thin track, accent fill, round handle).</summary>
    public static void StyleSlider(Slider slider, float handleSize = 26f, float trackHeight = 10f)
    {
        if (slider == null)
            return;

        RectTransform rect = slider.GetComponent<RectTransform>();
        float halfGap = Mathf.Max(1f, (rect.rect.height - trackHeight) * 0.5f);

        Transform bgT = slider.transform.Find("Background");
        if (bgT != null)
        {
            var bg = bgT.GetComponent<Image>();
            ApplyCard(bg, Track, 4f);
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(1f, 1f);
            bgRect.offsetMin = new Vector2(0f, halfGap);
            bgRect.offsetMax = new Vector2(0f, -halfGap);
        }

        Transform fillArea = slider.transform.Find("Fill Area");
        if (fillArea != null)
        {
            var faRect = fillArea as RectTransform;
            faRect.anchorMin = new Vector2(0f, 0f);
            faRect.anchorMax = new Vector2(1f, 1f);
            faRect.offsetMin = new Vector2(handleSize * 0.5f, halfGap);
            faRect.offsetMax = new Vector2(-handleSize * 0.5f, -halfGap);
        }

        if (slider.fillRect != null)
        {
            var fill = slider.fillRect.GetComponent<Image>();
            ApplyCard(fill, Accent, 4f);
            slider.fillRect.offsetMin = Vector2.zero;
            slider.fillRect.offsetMax = Vector2.zero;
        }

        Transform handleArea = slider.transform.Find("Handle Slide Area");
        if (handleArea != null)
        {
            var haRect = handleArea as RectTransform;
            haRect.anchorMin = new Vector2(0f, 0f);
            haRect.anchorMax = new Vector2(1f, 1f);
            haRect.offsetMin = new Vector2(handleSize * 0.5f, 0f);
            haRect.offsetMax = new Vector2(-handleSize * 0.5f, 0f);
        }

        if (slider.handleRect != null)
        {
            var handle = slider.handleRect.GetComponent<Image>();
            handle.sprite = Circle;
            handle.type = Image.Type.Simple;
            handle.color = Color.white;
            slider.handleRect.anchorMin = new Vector2(slider.handleRect.anchorMin.x, 0.5f);
            slider.handleRect.anchorMax = new Vector2(slider.handleRect.anchorMax.x, 0.5f);
            slider.handleRect.sizeDelta = new Vector2(handleSize, handleSize);
            slider.targetGraphic = handle;
        }

        slider.transition = Selectable.Transition.ColorTint;
        ApplySelectableColors(slider);
    }

    /// <summary>Create a small square themed button that fires once on press and repeats while held.</summary>
    public static UiRepeatButton CreateStepButton(Transform parent, string name, float size, Vector2 anchoredPosition, string label, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = anchoredPosition;

        var image = go.GetComponent<Image>();
        ApplyCard(image, Control, 2f);
        image.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ApplySelectableColors(button);

        var text = VrWorldUi.CreateText(rect, "Label", new Vector2(size, size), Vector2.zero, font, size * 0.62f, TextAlignmentOptions.Center);
        text.text = label;
        text.color = TextPrimary;

        return go.AddComponent<UiRepeatButton>();
    }

    // ---- Procedural sprite fallbacks (used only if the generated assets are missing) ----

    private static Sprite BuildRoundRect()
    {
        const int texSize = 64;
        const float radius = 20f;
        var tex = NewTexture(texSize);

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (texSize - 1 - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (texSize - 1 - radius)));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
    }

    private static Sprite BuildCircle()
    {
        const int texSize = 64;
        const float radius = 30f;
        var tex = NewTexture(texSize);
        float c = (texSize - 1) * 0.5f;

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private static Texture2D NewTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        return tex;
    }
}
