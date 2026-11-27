using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Controls only UI objects assigned in the Inspector. It never creates UI.
public class CourseSettingsTabs : MonoBehaviour
{
    [Serializable]
    public class CourseItem
    {
        public Button button;
        public TMP_Text label;
        public string displayName;
    }

    [Header("Tabs")]
    [SerializeField] private Button battleTabButton;
    [SerializeField] private Button courseTabButton;
    [Header("Panels")]
    [SerializeField] private GameObject battleSettingsPanel;
    [SerializeField] private GameObject courseSettingsPanel;
    [Header("Existing course-list UI")]
    [SerializeField] private Transform enabledCourseContent;
    [SerializeField] private Transform excludedCourseContent;
    [SerializeField] private Button enableAllCoursesButton;
    [SerializeField] private TMP_Text enabledCountLabel;
    [SerializeField] private TMP_Text excludedCountLabel;
    [SerializeField] private CourseItem[] courseItems = Array.Empty<CourseItem>();
    [Header("Hole count")]
    [SerializeField] private Slider holeCountSlider;
    [SerializeField] private TMP_Text holeCountLabel;
    [Header("Biome buttons: 草原, 海岸, 砂漠, 雪原, 都市")]
    [SerializeField] private Button[] biomeButtons = new Button[5];
    [SerializeField] private TMP_Text biomeLabel;

    private static readonly string[] Biomes = { "草原", "海岸", "砂漠", "雪原", "都市" };
    private static readonly Color EnabledColor = new(.28f, .62f, .38f);
    private static readonly Color ExcludedColor = new(.56f, .57f, .58f);
    private readonly HashSet<CourseItem> excluded = new();
    private int selectedBiomeIndex;
    private bool initialized;

    private void Awake() { Initialize(); }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (battleTabButton) battleTabButton.onClick.AddListener(ShowBattle);
        if (courseTabButton) courseTabButton.onClick.AddListener(ShowCourses);
        if (enableAllCoursesButton) enableAllCoursesButton.onClick.AddListener(EnableAll);
        if (holeCountSlider) holeCountSlider.onValueChanged.AddListener(UpdateHoleLabel);
        BindBiomeButtons();
        SetTabButtonsVisible(false);
        SetupCourses();
        SetupHoleSlider();
        ShowBattle();
        ValidateReferences();
    }

    private void BindBiomeButtons()
    {
        for (int i = 0; i < biomeButtons.Length && i < Biomes.Length; i++)
        {
            int index = i;
            if (biomeButtons[i]) biomeButtons[i].onClick.AddListener(() => SelectBiome(index));
        }
        if (biomeLabel) biomeLabel.text = "バイオーム：" + Biomes[selectedBiomeIndex];
    }

    private void SelectBiome(int index)
    {
        if (index < 0 || index >= Biomes.Length) return;
        selectedBiomeIndex = index;
        if (biomeLabel) biomeLabel.text = "バイオーム：" + Biomes[index];
    }


    private void SetupCourses()
    {
        excluded.Clear();
        foreach (var item in courseItems)
        {
            if (item == null || item.button == null) continue;
            if (string.IsNullOrWhiteSpace(item.displayName))
                item.displayName = item.button.name.Replace("Button_", "");
            item.button.onClick.AddListener(() => ToggleCourse(item));
            if (enabledCourseContent) item.button.transform.SetParent(enabledCourseContent, false);
            RefreshCourse(item, true);
        }
        UpdateCounts();
    }

    private void SetupHoleSlider()
    {
        if (!holeCountSlider) return;
        holeCountSlider.wholeNumbers = true;
        holeCountSlider.minValue = 1;
        holeCountSlider.maxValue = Mathf.Max(1, ActiveCount());
        holeCountSlider.value = Mathf.Clamp(holeCountSlider.value, 1, holeCountSlider.maxValue);
        UpdateHoleLabel(holeCountSlider.value);
    }

    private void ToggleCourse(CourseItem item)
    {
        if (item == null || item.button == null) return;
        bool isEnabled = !excluded.Contains(item);
        if (isEnabled && ActiveCount() <= 1) return;
        if (isEnabled)
        {
            excluded.Add(item);
            if (excludedCourseContent) item.button.transform.SetParent(excludedCourseContent, false);
        }
        else
        {
            excluded.Remove(item);
            if (enabledCourseContent) item.button.transform.SetParent(enabledCourseContent, false);
        }
        RefreshCourse(item, !excluded.Contains(item));
        UpdateCounts();
        UpdateHoleLimit();
    }

    private void EnableAll()
    {
        excluded.Clear();
        foreach (var item in courseItems)
        {
            if (item == null || item.button == null) continue;
            if (enabledCourseContent) item.button.transform.SetParent(enabledCourseContent, false);
            RefreshCourse(item, true);
        }
        UpdateCounts();
        UpdateHoleLimit();
    }

    private int ActiveCount()
    {
        int total = 0;
        foreach (var item in courseItems) if (item != null && item.button != null && !excluded.Contains(item)) total++;
        return total;
    }

    private void RefreshCourse(CourseItem item, bool isEnabled)
    {
        if (item.label) item.label.text = item.displayName + (isEnabled ? "  ✓ 有効" : "  × 除外");
        var image = item.button ? item.button.GetComponent<Image>() : null;
        if (image) image.color = isEnabled ? EnabledColor : ExcludedColor;
    }

    private void UpdateCounts()
    {
        int enabled = ActiveCount();
        int total = courseItems.Length;
        if (enabledCountLabel) enabledCountLabel.text = "有効 (" + enabled + ")";
        if (excludedCountLabel) excludedCountLabel.text = "無効 (" + (total - enabled) + ")";
    }

    private void UpdateHoleLimit()
    {
        if (!holeCountSlider) return;
        holeCountSlider.maxValue = Mathf.Max(1, ActiveCount());
        holeCountSlider.value = Mathf.Clamp(holeCountSlider.value, 1, holeCountSlider.maxValue);
        UpdateHoleLabel(holeCountSlider.value);
    }

    private void UpdateHoleLabel(float value)
    {
        if (holeCountLabel) holeCountLabel.text = Mathf.RoundToInt(value) + "/" + Mathf.Max(1, ActiveCount());
    }

    private void ShowBattle()
    {
        if (battleSettingsPanel) battleSettingsPanel.SetActive(true);
        if (courseSettingsPanel) courseSettingsPanel.SetActive(false);
    }

    private void ShowCourses()
    {
        if (battleSettingsPanel) battleSettingsPanel.SetActive(false);
        if (courseSettingsPanel) courseSettingsPanel.SetActive(true);
    }


    public void SetTabButtonsVisible(bool visible)
    {
        if (battleTabButton) battleTabButton.gameObject.SetActive(visible);
        if (courseTabButton) courseTabButton.gameObject.SetActive(visible);
    }

    private void ValidateReferences()
    {
        if (!battleTabButton) Debug.LogWarning("[CourseSettingsTabs] Battle tab is unassigned.", this);
        if (!courseTabButton) Debug.LogWarning("[CourseSettingsTabs] Course tab is unassigned.", this);
        if (!battleSettingsPanel) Debug.LogWarning("[CourseSettingsTabs] Battle panel is unassigned.", this);
        if (!courseSettingsPanel) Debug.LogWarning("[CourseSettingsTabs] Course panel is unassigned.", this);
        if (!enabledCourseContent) Debug.LogWarning("[CourseSettingsTabs] Enabled content is unassigned.", this);
        if (!excludedCourseContent) Debug.LogWarning("[CourseSettingsTabs] Excluded content is unassigned.", this);
        if (biomeButtons.Length != 5) Debug.LogWarning("[CourseSettingsTabs] Assign five biome buttons in order: 草原/海岸/砂漠/雪原/都市.", this);
        if (courseItems.Length == 0) Debug.LogWarning("[CourseSettingsTabs] Course button list is empty.", this);
    }
}
