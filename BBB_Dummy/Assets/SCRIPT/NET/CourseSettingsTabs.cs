using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CourseSettingsTabs : MonoBehaviour
{
    static readonly string[] Biomes = { "草原", "海岸", "砂漠", "雪原", "都市" };
    readonly List<CourseEntry> courses = new();
    readonly List<Button> biomeButtons = new();
    Button battleTab, courseTab, allButton;
    GameObject battlePanel, coursePanel;
    Transform courseList;
    Slider holes;
    TMP_Text holesLabel, biomeLabel;
    string selectedBiome = Biomes[0];
    bool initialized;

    class CourseEntry { public string Name; public Button Button; public TMP_Text Label; public bool Enabled = true; }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        battlePanel = Find("BattlePanel");
        coursePanel = Find("CourseSettingsView");
        courseList = Find("CourseList")?.transform;
        holes = Find("HoleCountSlider")?.GetComponent<Slider>();
        holesLabel = Find("Label_3 ホール")?.GetComponent<TMP_Text>();
        biomeLabel = Find("Label_バイオーム")?.GetComponent<TMP_Text>();
        battleTab = Find("Button_対戦設定")?.GetComponent<Button>();
        courseTab = Find("Button_コース設定")?.GetComponent<Button>();
        allButton = Find("Button_全コース")?.GetComponent<Button>();
        if (battleTab) battleTab.onClick.AddListener(ShowBattle);
        if (courseTab) courseTab.onClick.AddListener(ShowCourses);
        if (allButton) allButton.onClick.AddListener(ToggleAll);
        if (courseList)
            foreach (Transform child in courseList)
            {
                var b = child.GetComponent<Button>();
                if (!b) continue;
                var e = new CourseEntry { Name = child.name.Replace("Button_", ""), Button = b, Label = child.GetComponentInChildren<TMP_Text>(true) };
                b.onClick.AddListener(() => ToggleCourse(e));
                courses.Add(e);
                Refresh(e);
            }
        if (holes)
        {
            holes.wholeNumbers = true; holes.minValue = 1;
            holes.maxValue = Mathf.Max(1, ActiveCount());
            holes.value = Mathf.Clamp(holes.value, 1, holes.maxValue);
            holes.onValueChanged.AddListener(v => { if (holesLabel) holesLabel.text = Mathf.RoundToInt(v) + " ホール"; });
            if (holesLabel) holesLabel.text = Mathf.RoundToInt(holes.value) + " ホール";
        }
        CreateBiomeRow();
        ShowBattle();
    }

    GameObject Find(string n) { foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t.gameObject; return null; }

    void CreateBiomeRow()
    {
        if (!coursePanel) return;
        var prior = coursePanel.transform.Find("RuntimeBiomeButtons");
        if (prior) Destroy(prior.gameObject);
        var row = new GameObject("RuntimeBiomeButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(coursePanel.transform, false);
        var r = row.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(.5f, 1f); r.pivot = new Vector2(.5f, 1f);
        r.anchoredPosition = new Vector2(0, -115); r.sizeDelta = new Vector2(700, 58);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        foreach (string biome in Biomes)
        {
            var obj = new GameObject("Biome_" + biome, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(row.transform, false);
            var image = obj.GetComponent<Image>(); var button = obj.GetComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => SelectBiome(biome));
            var txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(obj.transform, false);
            var tr = txtObj.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(3,2); tr.offsetMax = new Vector2(-3,-2);
            var txt = txtObj.GetComponent<TextMeshProUGUI>(); txt.text = biome; txt.fontSize = 20; txt.color = Color.black; txt.alignment = TextAlignmentOptions.Center; txt.raycastTarget = false;
            biomeButtons.Add(button);
        }
        UpdateBiomeButtons();
        if (biomeLabel) biomeLabel.text = "バイオーム：" + selectedBiome + "（コース割当未設定）";
    }
    void SelectBiome(string b) { selectedBiome = b; UpdateBiomeButtons(); if (biomeLabel) biomeLabel.text = "バイオーム：" + b + "（コース割当未設定）"; }
    void UpdateBiomeButtons() { for (int i=0;i<biomeButtons.Count;i++) if (biomeButtons[i] && biomeButtons[i].GetComponent<Image>()) biomeButtons[i].GetComponent<Image>().color = Biomes[i]==selectedBiome ? new Color(.88f,.72f,.36f) : new Color(.72f,.76f,.70f); }

    void ToggleCourse(CourseEntry e) { if (e.Enabled && ActiveCount()<=1) return; e.Enabled=!e.Enabled; Refresh(e); UpdateLimit(); }
    void ToggleAll() { bool turnOn= courses.Exists(e=>!e.Enabled); foreach (var e in courses) { e.Enabled=turnOn; Refresh(e); } UpdateLimit(); }
    int ActiveCount() => courses.FindAll(e=>e.Enabled).Count;
    void UpdateLimit() { if (!holes) return; holes.maxValue=Mathf.Max(1,ActiveCount()); holes.value=Mathf.Clamp(holes.value,holes.minValue,holes.maxValue); if (holesLabel) holesLabel.text=Mathf.RoundToInt(holes.value)+" ホール"; }
    void Refresh(CourseEntry e) { if(e.Label) e.Label.text=e.Name+(e.Enabled?"  ✓ 使用":"  × 除外"); var img=e.Button?e.Button.GetComponent<Image>():null; if(img) img.color=e.Enabled?new Color(.23f,.74f,.42f):new Color(.45f,.46f,.49f); }
    void ShowBattle() { if(battlePanel) battlePanel.SetActive(true); if(coursePanel) coursePanel.SetActive(false); TabColor(battleTab,new Color(.32f,.62f,.38f),true); TabColor(courseTab,new Color(.95f,.67f,.29f),false); }
    void ShowCourses() { if(battlePanel) battlePanel.SetActive(false); if(coursePanel) coursePanel.SetActive(true); TabColor(battleTab,new Color(.32f,.62f,.38f),false); TabColor(courseTab,new Color(.95f,.67f,.29f),true); foreach(var e in courses) Refresh(e); }
    void TabColor(Button b, Color c, bool selected) { if(!b) return; var i=b.GetComponent<Image>(); if(i) i.color=selected?Color.Lerp(c,Color.white,.28f):c; }
}