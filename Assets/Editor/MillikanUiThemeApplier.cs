using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies the UiTheme look and the fixed zone layout to the scene UI
/// (experiment panel, tutor dialogue, guide, quiz wall). Re-runnable.
/// Most of these canvases live inside prefab instances, so every edit is
/// recorded with PrefabUtility so it survives save/merge.
/// </summary>
public static class MillikanUiThemeApplier
{
    private const string ExperimentCanvasPath = "millikan_optimized_v2/Experiment_Spray (1)/PowerSystem/ExperimentSystem/Canvas";
    private const string DialogueCanvasPath = "oldman/BottomTutorialUI";
    private const string GuideCanvasPath = "oldman/GuideUI";
    private const string WallCanvasPath = "Wall/Canvas";

    [MenuItem("Tools/Millikan/Apply UI Theme To Scene")]
    public static void Apply()
    {
        var report = new System.Text.StringBuilder();

        GameObject experiment = FindByPath(ExperimentCanvasPath);
        GameObject dialogue = FindByPath(DialogueCanvasPath);
        GameObject guide = FindByPath(GuideCanvasPath);
        GameObject wall = FindByPath(WallCanvasPath);

        if (experiment != null) { ApplyExperimentPanel(experiment); RecordAll(experiment); report.AppendLine("experiment panel: ok"); }
        else report.AppendLine("experiment panel: NOT FOUND");

        if (dialogue != null) { ApplyDialogue(dialogue, experiment); RecordAll(dialogue); report.AppendLine("dialogue: ok"); }
        else report.AppendLine("dialogue: NOT FOUND");

        if (guide != null) { ApplyGuide(guide); RecordAll(guide); report.AppendLine("guide: ok"); }
        else report.AppendLine("guide: NOT FOUND");

        if (wall != null) { ApplyWall(wall); RecordAll(wall); report.AppendLine("wall: ok"); }
        else report.AppendLine("wall: NOT FOUND");

        report.AppendLine(ApplyRadioButtonPrefab() ? "radio button prefab: ok" : "radio button prefab: NOT FOUND");

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        report.AppendLine("scene saved: " + scene.path);

        Debug.Log("[MillikanUiThemeApplier]\n" + report);
    }

    // ------------------------------------------------------------------
    // Experiment (legend) panel
    // ------------------------------------------------------------------

    private static void ApplyExperimentPanel(GameObject canvasGo)
    {
        var canvasRect = (RectTransform)canvasGo.transform;
        canvasRect.sizeDelta = new Vector2(460f, 385f);

        var panel = (RectTransform)canvasGo.transform.Find("Panel");
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
        panel.offsetMin = new Vector2(8f, 8f); panel.offsetMax = new Vector2(-8f, -8f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        var panelImg = panel.GetComponent<Image>();
        UiTheme.ApplyCard(panelImg, UiTheme.Card, 1f);
        panelImg.raycastTarget = true;

        TMP_Text title = StyleRow(panel, "TitleText", -26f, new Vector2(404f, 36f), 24f, FontStyles.Bold, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
        StyleRow(panel, "MassText", -71f, new Vector2(404f, 30f), 21f, FontStyles.Normal, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
        StyleRow(panel, "ChargeText", -103f, new Vector2(404f, 30f), 21f, FontStyles.Normal, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
        TMP_Text radiusText = StyleRow(panel, "RadiusText", -147f, new Vector2(388f, 26f), 19f, FontStyles.Normal, UiTheme.TextMuted, TextAlignmentOptions.MidlineLeft);
        StyleRow(panel, "VoltageText", -247f, new Vector2(388f, 26f), 19f, FontStyles.Normal, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);

        TMP_FontAsset font = title != null ? title.font : TMP_Settings.defaultFontAsset;

        // Cards sit behind the prefab-owned rows (they cannot be reparented), so keep them first in draw order.
        RectTransform radiusCard = EnsureCard(panel, "RadiusCard", -126f, new Vector2(420f, 92f));
        RectTransform voltageCard = EnsureCard(panel, "VoltageCard", -226f, new Vector2(420f, 96f));
        RectTransform divider = EnsureRect(panel, "Divider", typeof(Image));
        Row(divider, -52f, new Vector2(404f, 2f));
        var divImg = divider.GetComponent<Image>();
        divImg.sprite = null; divImg.color = UiTheme.Divider; divImg.raycastTarget = false;

        radiusCard.SetSiblingIndex(0);
        voltageCard.SetSiblingIndex(1);
        divider.SetSiblingIndex(2);

        var sliderRect = (RectTransform)panel.Find("RadiusSlider");
        Row(sliderRect, -186f, new Vector2(290f, 28f));
        var slider = sliderRect.GetComponent<Slider>();
        UiTheme.StyleSlider(slider, 26f, 10f);

        UiRepeatButton minus = EnsureStepButton(radiusCard, "RadiusMinus", "-", new Vector2(-174f, -60f), font);
        UiRepeatButton plus = EnsureStepButton(radiusCard, "RadiusPlus", "+", new Vector2(174f, -60f), font);

        RectTransform slot = EnsureRect(voltageCard, "VoltageFineControlSlot");
        Row(slot, -66f, new Vector2(388f, 44f));

        RectTransform hintRect = EnsureRect(panel, "HintText", typeof(TextMeshProUGUI));
        Row(hintRect, -343f, new Vector2(404f, 26f));
        var hint = hintRect.GetComponent<TextMeshProUGUI>();
        hint.font = font; hint.fontSize = 19f; hint.fontStyle = FontStyles.Italic;
        hint.color = UiTheme.TextMuted; hint.alignment = TextAlignmentOptions.Center; hint.raycastTarget = false;

        foreach (string name in new[] { "ISDK_RayCanvasInteraction", "ISDK_PokeCanvasInteraction" })
        {
            var isdk = panel.Find(name) as RectTransform;
            if (isdk == null) continue;
            Stretch(isdk);
            var surface = isdk.Find("Surface") as RectTransform;
            if (surface != null) Stretch(surface);
        }

        var rsc = sliderRect.GetComponent<RadiusSliderController>();
        if (rsc != null)
        {
            rsc.panelRoot = panel.gameObject;
            rsc.radiusText = radiusText;
            rsc.minusButton = minus;
            rsc.plusButton = plus;
        }

        var legend = canvasGo.GetComponent<LegendUIController>();
        if (legend != null)
            legend.hintText = hint;
    }

    // ------------------------------------------------------------------
    // Tutor dialogue ("chat") panel — fixed zones shared with TutorialChoiceUI
    // ------------------------------------------------------------------

    private static void ApplyDialogue(GameObject canvasGo, GameObject isdkSourceCanvas)
    {
        var panel = canvasGo.transform.Find("DialoguePanel");
        UiTheme.ApplyCard(panel.GetComponent<Image>(), UiTheme.Card, 0.8f);

        var text = panel.Find("DialogueText").GetComponent<TextMeshProUGUI>();
        Center(text.rectTransform, new Vector2(0f, TutorialChoiceUI.DialogueY), new Vector2(820f, TutorialChoiceUI.DialogueHeight));
        text.fontSize = 28f; text.color = UiTheme.TextPrimary; text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;

        var hint = panel.Find("ButtonHintText").GetComponent<TextMeshProUGUI>();
        Center(hint.rectTransform, new Vector2(0f, TutorialChoiceUI.HintY), new Vector2(820f, TutorialChoiceUI.HintHeight));
        hint.fontSize = 19f; hint.color = UiTheme.TextMuted; hint.alignment = TextAlignmentOptions.Midline; hint.raycastTarget = false;

        if (canvasGo.transform.Find("ISDK_RayCanvasInteraction") == null && isdkSourceCanvas != null)
        {
            Transform source = isdkSourceCanvas.transform.Find("Panel/ISDK_RayCanvasInteraction");
            if (source != null)
            {
                GameObject copy = Object.Instantiate(source.gameObject, canvasGo.transform, false);
                copy.name = "ISDK_RayCanvasInteraction";
                Undo.RegisterCreatedObjectUndo(copy, "Create ISDK_RayCanvasInteraction");
                Stretch((RectTransform)copy.transform);
                var surface = copy.transform.Find("Surface") as RectTransform;
                if (surface != null) Stretch(surface);

                Component pointable = copy.GetComponent("PointableCanvas");
                if (pointable != null)
                {
                    var so = new SerializedObject(pointable);
                    SerializedProperty canvasProp = so.FindProperty("_canvas");
                    if (canvasProp != null)
                    {
                        canvasProp.objectReferenceValue = canvasGo.GetComponent<Canvas>();
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
        }
    }

    private static void ApplyGuide(GameObject canvasGo)
    {
        var panel = canvasGo.transform.Find("Panel");
        if (panel == null) return;
        UiTheme.ApplyCard(panel.GetComponent<Image>(), UiTheme.Card, 0.8f);
        var text = panel.Find("Text");
        if (text != null) text.GetComponent<TMP_Text>().color = UiTheme.TextPrimary;
    }

    private static void ApplyWall(GameObject canvasGo)
    {
        Transform wall = canvasGo.transform;

        foreach (string group in new[] { "QuizGroup", "ResultGroup" })
        {
            Transform t = wall.Find(group);
            if (t == null) continue;
            var img = t.GetComponent<Image>();
            if (img != null) UiTheme.ApplyCard(img, UiTheme.Card, 0.6f);
        }

        foreach (string path in new[] { "QuizGroup/AnswerButtonA", "QuizGroup/AnswerButtonB", "QuizGroup/AnswerButtonC", "ResultGroup/ContinueButton", "QuestionnaireGroup/QuestionnaireContinueButton", "Test_ToPostQuuiz" })
        {
            Transform t = wall.Find(path);
            if (t == null) continue;
            var img = t.GetComponent<Image>();
            UiTheme.ApplyCard(img, UiTheme.Control, 1.2f);
            var button = t.GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.ColorTint;
                button.targetGraphic = img;
                UiTheme.ApplySelectableColors(button);
            }
            var label = t.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = UiTheme.TextPrimary;
        }

        // The quiz controller repaints the answer buttons at runtime; keep its palette on-theme
        // (dark normal state so the light labels stay readable, muted green/red feedback).
        var quiz = canvasGo.GetComponent<PrePostQuizController>();
        if (quiz != null)
        {
            quiz.normalColor = UiTheme.Control;
            quiz.correctColor = new Color(0.16f, 0.52f, 0.34f, 1f);
            quiz.wrongColor = new Color(0.62f, 0.22f, 0.24f, 1f);
            EditorUtility.SetDirty(quiz);
        }

        // The questionnaire has no panel of its own, so its white text sat directly on the wall.
        Transform questionnaire = wall.Find("QuestionnaireGroup");
        if (questionnaire != null)
        {
            RectTransform card = EnsureRect(questionnaire, "Background", typeof(Image));
            card.SetAsFirstSibling();
            card.anchorMin = new Vector2(0.5f, 0.5f); card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = ((RectTransform)wall).sizeDelta;
            var cardImg = card.GetComponent<Image>();
            UiTheme.ApplyCard(cardImg, UiTheme.Card, 0.6f);
            cardImg.raycastTarget = false;

            foreach (var text in questionnaire.GetComponentsInChildren<TMP_Text>(true))
                text.color = text.name == "Progress" ? UiTheme.TextMuted : UiTheme.TextPrimary;
        }

        foreach (string path in new[] { "QuizGroup/ModeText", "QuizGroup/ScoreText", "QuizGroup/QuestionText", "ResultGroup/ResultText" })
        {
            Transform t = wall.Find(path);
            if (t == null) continue;
            var text = t.GetComponent<TMP_Text>();
            if (text != null) text.color = UiTheme.TextPrimary;
        }
    }

    /// <summary>The questionnaire's radio toggle shipped white-on-white (invisible checkmark).</summary>
    private static bool ApplyRadioButtonPrefab()
    {
        const string path = "Assets/Prefabs/RadioButton.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return false;

        try
        {
            var toggle = root.GetComponent<Toggle>();
            var background = toggle != null ? toggle.targetGraphic as Image : null;
            var checkmark = toggle != null ? toggle.graphic as Image : null;

            if (background != null)
                UiTheme.ApplyCard(background, UiTheme.Control, 3f);
            if (checkmark != null)
            {
                checkmark.sprite = UiTheme.Circle;
                checkmark.color = UiTheme.Accent;
                var cr = checkmark.rectTransform;
                cr.anchorMin = new Vector2(0.5f, 0.5f); cr.anchorMax = new Vector2(0.5f, 0.5f);
                cr.anchoredPosition = Vector2.zero;
                float d = Mathf.Min(background != null ? background.rectTransform.rect.height : 20f, 20f) * 0.6f;
                cr.sizeDelta = new Vector2(d, d);
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                text.color = UiTheme.TextPrimary;
            if (toggle != null)
                UiTheme.ApplySelectableColors(toggle);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static TMP_Text StyleRow(Transform panel, string name, float y, Vector2 size, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        Transform t = panel.Find(name);
        if (t == null) return null;
        var text = t.GetComponent<TMP_Text>();
        Row(text.rectTransform, y, size);
        text.fontSize = fontSize; text.fontStyle = style; text.color = color; text.alignment = align; text.raycastTarget = false;
        return text;
    }

    private static RectTransform EnsureCard(Transform parent, string name, float y, Vector2 size)
    {
        RectTransform card = EnsureRect(parent, name, typeof(Image));
        card.anchorMin = new Vector2(0.5f, 1f); card.anchorMax = new Vector2(0.5f, 1f);
        card.pivot = new Vector2(0.5f, 1f);
        card.anchoredPosition = new Vector2(0f, y);
        card.sizeDelta = size;
        var img = card.GetComponent<Image>();
        UiTheme.ApplyCard(img, UiTheme.CardSoft, 1.6f);
        img.raycastTarget = true;
        return card;
    }

    private static UiRepeatButton EnsureStepButton(RectTransform card, string name, string label, Vector2 position, TMP_FontAsset font)
    {
        Transform existing = card.Find(name);
        UiRepeatButton button = existing != null
            ? existing.GetComponent<UiRepeatButton>()
            : UiTheme.CreateStepButton(card, name, 40f, Vector2.zero, label, font);
        var rect = (RectTransform)button.transform;
        Row(rect, position.y, new Vector2(40f, 40f));
        rect.anchoredPosition = position;
        return button;
    }

    private static RectTransform EnsureRect(Transform parent, string name, params System.Type[] extraComponents)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;

        var types = new System.Type[extraComponents.Length + 1];
        types[0] = typeof(RectTransform);
        extraComponents.CopyTo(types, 1);
        var go = new GameObject(name, types);
        if (extraComponents.Length > 0 && go.GetComponent<CanvasRenderer>() == null)
            go.AddComponent<CanvasRenderer>();
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return (RectTransform)go.transform;
    }

    private static void Row(RectTransform rt, float y, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = size;
    }

    private static void Center(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    /// <summary>Persist script-made edits on prefab-instance objects as overrides.</summary>
    private static void RecordAll(GameObject root)
    {
        foreach (Component c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            EditorUtility.SetDirty(c);
            if (PrefabUtility.IsPartOfPrefabInstance(c))
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            EditorUtility.SetDirty(t.gameObject);
            if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        }
    }

    private static GameObject FindByPath(string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++)
                t = t.Find(parts[i]);
            if (t != null) return t.gameObject;
        }
        return null;
    }
}
