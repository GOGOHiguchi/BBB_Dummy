using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Course list UI. Uses the five biome buttons already placed in ManuPanel.prefab.
public class CourseSettingsTabs : MonoBehaviour
{
    static readonly string[] Biomes = { "草原", "海岸", "砂漠", "雪原", "都市" };
    readonly List<CourseEntry> courses = new();
    readonly List<Button> biomeButtons = new();
    Button battleTab, courseTab, allButton;
    GameObject battlePanel, coursePanel;
    Transform sourceList, enabledContent, excludedContent;
    Slider holes;
    TMP_Text holesLabel, biomeLabel, enabledHeader, excludedHeader;
    string selectedBiome = Biomes[0];
    bool initialized;

    class CourseEntry { public string Name; public Button Button; public TMP_Text Label; public bool Enabled = true; }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        battlePanel = Find("BattlePanel");
        coursePanel = Find("CourseSettingsView");
        sourceList = Find("CourseList")?.transform;
        holes = Find("HoleCountSlider")?.GetComponent<Slider>();
        holesLabel = Find("Label_3 ホール")?.GetComponent<TMP_Text>();
        biomeLabel = Find("Label_バイオーム")?.GetComponent<TMP_Text>();
        battleTab = Find("Button_対戦設定")?.GetComponent<Button>();
        courseTab = Find("Button_コース設定")?.GetComponent<Button>();
        allButton = Find("Button_全コース")?.GetComponent<Button>();
        if (battleTab) battleTab.onClick.AddListener(ShowBattle);
        if (courseTab) courseTab.onClick.AddListener(ShowCourses);
        if (allButton) allButton.onClick.AddListener(EnableAll);
        BindBiomes();
        ReadCourseButtons();
        SetupHoleSlider();
        BuildTwoLists();
        ShowBattle();
    }

    GameObject Find(string name)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    void BindBiomes()
    {
        biomeButtons.Clear();
        foreach (string biome in Biomes)
        {
            var button = Find("Biome_" + biome)?.GetComponent<Button>();
            if (!button) continue;
            button.onClick.AddListener(() => { selectedBiome = biome; RefreshBiomeButtons(); if (biomeLabel) biomeLabel.text = "バイオーム：" + biome; });
            biomeButtons.Add(button);
        }
        RefreshBiomeButtons();
        if (biomeLabel) biomeLabel.text = "バイオーム：" + selectedBiome;
    }

    void RefreshBiomeButtons()
    {
        for (int i=0;i<biomeButtons.Count;i++)
        {
            var image = biomeButtons[i] ? biomeButtons[i].GetComponent<Image>() : null;
            if (image) image.color = Biomes[i] == selectedBiome ? new Color(.88f,.72f,.36f) : new Color(.72f,.76f,.70f);
        }
    }

    void ReadCourseButtons()
    {
        if (!sourceList) { Debug.LogWarning("[CourseSettingsTabs] CourseList not found."); return; }
        foreach (Transform child in sourceList)
        {
            var button = child.GetComponent<Button>();
            if (!button) continue;
            courses.Add(new CourseEntry { Name = child.name.Replace("Button_", ""), Button = button, Label = child.GetComponentInChildren<TMP_Text>(true) });
        }
    }

    void SetupHoleSlider()
    {
        if (!holes) return;
        holes.wholeNumbers = true; holes.minValue = 1; holes.maxValue = Mathf.Max(1,courses.Count);
        holes.value = Mathf.Clamp(holes.value,1,holes.maxValue);
        holes.onValueChanged.AddListener(v => { if(holesLabel) holesLabel.text = Mathf.RoundToInt(v)+" ホール"; });
        if(holesLabel) holesLabel.text = Mathf.RoundToInt(holes.value)+" ホール";
    }

    void BuildTwoLists()
    {
        if (!coursePanel) return;
        var existing = coursePanel.transform.Find("RuntimeCourseLists");
        if(existing) Destroy(existing.gameObject);
        var root = new GameObject("RuntimeCourseLists",typeof(RectTransform));
        root.transform.SetParent(coursePanel.transform,false);
        var rr=root.GetComponent<RectTransform>();
        rr.anchorMin=rr.anchorMax=new Vector2(.5f,.5f); rr.pivot=new Vector2(.5f,.5f);
        rr.anchoredPosition=new Vector2(0,-125); rr.sizeDelta=new Vector2(1100,430);
        enabledContent=CreateScrollPanel(root.transform,"EnabledPanel","有効",new Vector2(-275,0),out enabledHeader);
        excludedContent=CreateScrollPanel(root.transform,"ExcludedPanel","無効（除外）",new Vector2(275,0),out excludedHeader);
        foreach(var entry in courses)
        {
            entry.Button.onClick.RemoveAllListeners();
            entry.Button.onClick.AddListener(()=>MoveCourse(entry));
            entry.Button.transform.SetParent(enabledContent,false);
            var le=entry.Button.GetComponent<LayoutElement>();
            if(!le) le=entry.Button.gameObject.AddComponent<LayoutElement>();
            le.minHeight=44; le.preferredHeight=44; le.flexibleHeight=0;
            RefreshEntry(entry);
        }
        if(sourceList) sourceList.gameObject.SetActive(false);
        UpdateHeaders();
    }

    Transform CreateScrollPanel(Transform parent,string name,string heading,Vector2 position,out TMP_Text header)
    {
        var panel=new GameObject(name,typeof(RectTransform),typeof(Image));
        panel.transform.SetParent(parent,false);
        var pr=panel.GetComponent<RectTransform>(); pr.anchorMin=pr.anchorMax=new Vector2(.5f,.5f); pr.pivot=new Vector2(.5f,.5f); pr.anchoredPosition=position; pr.sizeDelta=new Vector2(530,420);
        panel.GetComponent<Image>().color=new Color(.91f,.86f,.72f,.97f);
        var head=MakeText(panel.transform,heading,25,new Vector2(0,-8),new Vector2(490,38)); header=head.GetComponent<TMP_Text>();
        var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));
        viewport.transform.SetParent(panel.transform,false);
        var vr=viewport.GetComponent<RectTransform>(); vr.anchorMin=Vector2.zero; vr.anchorMax=Vector2.one; vr.offsetMin=new Vector2(12,12); vr.offsetMax=new Vector2(-34,-48);
        viewport.GetComponent<Image>().color=new Color(.98f,.95f,.84f);
        var content=new GameObject("Content",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform,false);
        var cr=content.GetComponent<RectTransform>(); cr.anchorMin=new Vector2(0,1); cr.anchorMax=new Vector2(1,1); cr.pivot=new Vector2(.5f,1); cr.sizeDelta=Vector2.zero;
        var vlg=content.GetComponent<VerticalLayoutGroup>(); vlg.spacing=5; vlg.padding=new RectOffset(6,6,6,6); vlg.childAlignment=TextAnchor.UpperCenter; vlg.childControlWidth=true; vlg.childControlHeight=true; vlg.childForceExpandWidth=true; vlg.childForceExpandHeight=false;
        var fit=content.GetComponent<ContentSizeFitter>(); fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;
        var scroll=panel.AddComponent<ScrollRect>(); scroll.viewport=vr; scroll.content=cr; scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=25;
        var track=new GameObject("Scrollbar",typeof(RectTransform),typeof(Image),typeof(Scrollbar)); track.transform.SetParent(panel.transform,false);
        var tr=track.GetComponent<RectTransform>(); tr.anchorMin=new Vector2(1,0); tr.anchorMax=new Vector2(1,1); tr.pivot=new Vector2(1,.5f); tr.offsetMin=new Vector2(-24,12); tr.offsetMax=new Vector2(-12,-48);
        track.GetComponent<Image>().color=new Color(.77f,.72f,.61f);
        var handle=new GameObject("Handle",typeof(RectTransform),typeof(Image)); handle.transform.SetParent(track.transform,false);
        var hr=handle.GetComponent<RectTransform>(); hr.anchorMin=Vector2.zero; hr.anchorMax=Vector2.one; hr.offsetMin=hr.offsetMax=Vector2.zero;
        handle.GetComponent<Image>().color=new Color(.48f,.57f,.40f);
        var bar=track.GetComponent<Scrollbar>(); bar.direction=Scrollbar.Direction.BottomToTop; bar.handleRect=hr; bar.targetGraphic=handle.GetComponent<Image>();
        scroll.verticalScrollbar=bar; scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        return content.transform;
    }

    GameObject MakeText(Transform parent,string value,float size,Vector2 pos,Vector2 dimensions)
    {
        var go=new GameObject("Header",typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=new Vector2(.5f,1); r.pivot=new Vector2(.5f,1); r.anchoredPosition=pos; r.sizeDelta=dimensions;
        var t=go.GetComponent<TextMeshProUGUI>(); t.text=value; t.fontSize=size; t.color=new Color(.24f,.29f,.22f); t.alignment=TextAlignmentOptions.Center; t.raycastTarget=false;
        return go;
    }

    void MoveCourse(CourseEntry e)
    {
        if(e.Enabled && ActiveCount()<=1) return;
        e.Enabled=!e.Enabled;
        e.Button.transform.SetParent(e.Enabled?enabledContent:excludedContent,false);
        RefreshEntry(e); UpdateHeaders(); UpdateHoleLimit();
    }

    void EnableAll()
    {
        foreach(var e in courses){e.Enabled=true; e.Button.transform.SetParent(enabledContent,false); RefreshEntry(e);}
        UpdateHeaders(); UpdateHoleLimit();
    }

    int ActiveCount()=>courses.FindAll(e=>e.Enabled).Count;
    void UpdateHeaders(){if(enabledHeader)enabledHeader.text="有効 ("+ActiveCount()+")"; if(excludedHeader)excludedHeader.text="無効 ("+(courses.Count-ActiveCount())+")";}
    void UpdateHoleLimit(){if(!holes)return; holes.maxValue=Mathf.Max(1,ActiveCount()); holes.value=Mathf.Clamp(holes.value,holes.minValue,holes.maxValue); if(holesLabel)holesLabel.text=Mathf.RoundToInt(holes.value)+" ホール";}
    void RefreshEntry(CourseEntry e){if(e.Label)e.Label.text=e.Name+(e.Enabled?"  ✓ 有効":"  × 除外"); var image=e.Button?e.Button.GetComponent<Image>():null; if(image)image.color=e.Enabled?new Color(.28f,.62f,.38f):new Color(.56f,.57f,.58f);}
    void ShowBattle(){if(battlePanel)battlePanel.SetActive(true);if(coursePanel)coursePanel.SetActive(false);TabColor(battleTab,new Color(.32f,.62f,.38f),true);TabColor(courseTab,new Color(.95f,.67f,.29f),false);}
    void ShowCourses(){if(battlePanel)battlePanel.SetActive(false);if(coursePanel)coursePanel.SetActive(true);TabColor(battleTab,new Color(.32f,.62f,.38f),false);TabColor(courseTab,new Color(.95f,.67f,.29f),true);foreach(var e in courses)RefreshEntry(e);}
    void TabColor(Button b,Color c,bool selected){if(!b)return;var i=b.GetComponent<Image>();if(i)i.color=selected?Color.Lerp(c,Color.white,.28f):c;}
}