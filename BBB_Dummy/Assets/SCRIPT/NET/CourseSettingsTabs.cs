using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobby settings tabs generated at runtime to preserve the existing ManuPanel prefab.
/// Course selection and hole count are local UI settings only; network/GameManager
/// integration is intentionally left for the follow-up implementation.
/// </summary>
public class CourseSettingsTabs : MonoBehaviour
{
    private TMP_FontAsset font;
    private GameObject coursePanel;
    private readonly List<CourseEntry> entries = new List<CourseEntry>();
    private TextMeshProUGUI holeCountText;
    private Slider holeSlider;
    private bool initialized;

    private class CourseEntry
    {
        public string SceneName;
        public bool Enabled = true;
        public Button Button;
        public TextMeshProUGUI Label;
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        var sourceText = GetComponentInChildren<TMP_Text>(true);
        if (sourceText != null) font = sourceText.font;
        CreateCoursePanel();
        CreateTabs();
        ShowBattleSettings();
    }

    private void CreateTabs()
    {
        var root = new GameObject("SettingsTabs", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -10f);
        rect.sizeDelta = new Vector2(700f, 72f);
        var battle = CreateButton(root.transform, "対戦設定", new Color(0.32f, 0.62f, 0.38f), new Vector2(-170f, 0f), new Vector2(330f, 70f));
        var course = CreateButton(root.transform, "コース設定", new Color(0.95f, 0.67f, 0.29f), new Vector2(170f, 0f), new Vector2(330f, 70f));
        battle.onClick.AddListener(ShowBattleSettings);
        course.onClick.AddListener(ShowCourseSettings);
    }

    private void CreateCoursePanel()
    {
        coursePanel = new GameObject("CourseSettingsView", typeof(RectTransform), typeof(Image));
        coursePanel.transform.SetParent(transform, false);
        var rect = coursePanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 12f);
        rect.offsetMax = new Vector2(-12f, -12f);
        coursePanel.GetComponent<Image>().color = new Color(0.14f, 0.55f, 0.82f, 0.97f);

        CreateText(coursePanel.transform, "コース設定", 38f, new Vector2(0f, -105f), new Vector2(720f, 72f), Color.white, true);
        CreateText(coursePanel.transform, "バイオーム", 27f, new Vector2(0f, -185f), new Vector2(300f, 52f), Color.white, true);
        CreateButton(coursePanel.transform, "全コース", new Color(0.2f, 0.72f, 0.86f), new Vector2(0f, -238f), new Vector2(260f, 56f));
        CreateText(coursePanel.transform, "使用するコース（クリックで有効 / 除外）", 24f, new Vector2(0f, -300f), new Vector2(760f, 50f), Color.white, true);

        var listRoot = new GameObject("CourseList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listRoot.transform.SetParent(coursePanel.transform, false);
        var listRect = listRoot.GetComponent<RectTransform>();
        listRect.anchorMin = listRect.anchorMax = new Vector2(0.5f, 0.5f);
        listRect.pivot = new Vector2(0.5f, 0.5f);
        listRect.anchoredPosition = new Vector2(0f, -20f);
        listRect.sizeDelta = new Vector2(690f, 310f);
        var layout = listRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        string[] sceneNames = { "3_Map_1", "3_Map_2", "3_Map_3" };
        foreach (string scene in sceneNames)
        {
            var entry = new CourseEntry { SceneName = scene };
            entry.Button = CreateButton(listRoot.transform, scene, new Color(0.23f, 0.74f, 0.42f), Vector2.zero, new Vector2(680f, 62f));
            entry.Label = entry.Button.GetComponentInChildren<TextMeshProUGUI>();
            entry.Button.onClick.AddListener(() => ToggleCourse(entry));
            entries.Add(entry);
        }

        CreateText(coursePanel.transform, "プレイするホール数", 24f, new Vector2(0f, -355f), new Vector2(720f, 50f), Color.white, true);
        var sliderObject = new GameObject("HoleCountSlider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(coursePanel.transform, false);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(0f, -405f);
        sliderRect.sizeDelta = new Vector2(520f, 32f);
        CreateSliderParts(sliderObject.transform);
        holeSlider = sliderObject.GetComponent<Slider>();
        holeSlider.minValue = 1;
        holeSlider.maxValue = entries.Count;
        holeSlider.wholeNumbers = true;
        holeSlider.value = entries.Count;
        holeSlider.onValueChanged.AddListener(UpdateHoleCount);
        holeCountText = CreateText(coursePanel.transform, "3 ホール", 24f, new Vector2(0f, -450f), new Vector2(300f, 50f), Color.white, true);
        coursePanel.SetActive(false);
    }

    private void CreateSliderParts(Transform sliderTransform)
    {
        var background = CreateRect(sliderTransform, "Background", new Color(0.1f, 0.28f, 0.45f), Vector2.zero, new Vector2(520f, 12f));
        var fillArea = CreateRect(sliderTransform, "FillArea", Color.clear, Vector2.zero, new Vector2(520f, 12f));
        var fill = CreateRect(fillArea.transform, "Fill", new Color(1f, 0.85f, 0.3f), Vector2.zero, Vector2.zero);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        var handleArea = CreateRect(sliderTransform, "HandleArea", Color.clear, Vector2.zero, new Vector2(520f, 32f));
        var handle = CreateRect(handleArea.transform, "Handle", Color.white, Vector2.zero, new Vector2(28f, 28f));
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        var slider = sliderTransform.GetComponent<Slider>();
        slider.targetGraphic = background.GetComponent<Image>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
    }

    private void ToggleCourse(CourseEntry entry)
    {
        entry.Enabled = !entry.Enabled;
        RefreshCourse(entry);
        int activeCount = entries.FindAll(e => e.Enabled).Count;
        holeSlider.maxValue = Mathf.Max(1, activeCount);
        holeSlider.value = Mathf.Clamp(holeSlider.value, 1f, holeSlider.maxValue);
        UpdateHoleCount(holeSlider.value);
    }

    private void RefreshCourse(CourseEntry entry)
    {
        if (entry.Label != null) entry.Label.text = entry.SceneName + (entry.Enabled ? "  ✓ 使用" : "  × 除外");
        var image = entry.Button.GetComponent<Image>();
        if (image != null) image.color = entry.Enabled ? new Color(0.23f, 0.74f, 0.42f) : new Color(0.45f, 0.46f, 0.49f);
    }

    private void UpdateHoleCount(float value)
    {
        if (holeCountText != null) holeCountText.text = Mathf.RoundToInt(value) + " ホール";
    }

    private void ShowBattleSettings()
    {
        if (coursePanel != null) coursePanel.SetActive(false);
    }

    private void ShowCourseSettings()
    {
        coursePanel.SetActive(true);
        foreach (var entry in entries) RefreshCourse(entry);
    }

    private Button CreateButton(Transform parent, string label, Color color, Vector2 position, Vector2 size)
    {
        var obj = CreateRect(parent, "Button_" + label, color, position, size);
        var button = obj.AddComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
        button.colors = colors;
        var text = CreateText(obj.transform, label, 26f, Vector2.zero, size, Color.black, true);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private TextMeshProUGUI CreateText(Transform parent, string value, float size, Vector2 position, Vector2 dimensions, Color color, bool bold)
    {
        var obj = new GameObject("Label_" + value, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var text = obj.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (bold) text.fontStyle = FontStyles.Bold;
        return text;
    }

    private GameObject CreateRect(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        obj.GetComponent<Image>().color = color;
        return obj;
    }
}
