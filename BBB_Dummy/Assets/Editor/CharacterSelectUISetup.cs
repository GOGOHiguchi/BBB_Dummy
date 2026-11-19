#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class CharacterSelectUISetup
{
    [MenuItem("Tools/BBB/Build Character Select UI in 2_Lobby")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/2_Lobby.unity",OpenSceneMode.Single);
        Canvas canvas=GameObject.Find("Canvas")?.GetComponent<Canvas>();
        if(canvas==null) canvas=Object.FindFirstObjectByType<Canvas>();
        if(canvas==null){ Debug.LogError("2_LobbyにCanvasがありません。"); return; }

        var scaler=canvas.GetComponent<CanvasScaler>();
        if(scaler==null) scaler=Undo.AddComponent<CanvasScaler>(canvas.gameObject);
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);
        scaler.matchWidthOrHeight=.5f;
        if(canvas.GetComponent<GraphicRaycaster>()==null) Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);

        var old=canvas.transform.Find("CharacterSelect_Root");
        if(old!=null) Undo.DestroyObjectImmediate(old.gameObject);
        GameObject root=Panel("CharacterSelect_Root",canvas.transform,Vector2.zero,Vector2.one,new Color(.015f,.035f,.08f,.97f));
        GameObject left=Panel("LeftPanel",root.transform,new Vector2(.015f,.025f),new Vector2(.335f,.975f),new Color(.025f,.19f,.34f,.98f));
        GameObject right=Panel("RightPanel",root.transform,new Vector2(.35f,.025f),new Vector2(.985f,.975f),new Color(.015f,.065f,.15f,.98f));
        Label(left.transform,"キャラクター\nCHARACTERS",34,new Vector2(.05f,.88f),new Vector2(.95f,.99f));
        GameObject frame=Panel("PreviewFrame",left.transform,new Vector2(.055f,.205f),new Vector2(.945f,.85f),new Color(.05f,.48f,.68f,1));
        GameObject imageObj=new GameObject("AvatarPreview",typeof(RectTransform),typeof(RawImage));
        imageObj.transform.SetParent(frame.transform,false); Stretch(imageObj.GetComponent<RectTransform>());
        var preview=imageObj.GetComponent<RawImage>(); preview.raycastTarget=false;
        var name=Label(left.transform,"",24,new Vector2(.06f,.14f),new Vector2(.94f,.20f));
        var cancel=Button(left.transform,"もどる\n(CANCEL)",new Color(.08f,.38f,.76f),new Vector2(.055f,.035f),new Vector2(.475f,.13f));
        var confirm=Button(left.transform,"決定\n(SAVE)",new Color(.12f,.68f,.16f),new Vector2(.525f,.035f),new Vector2(.945f,.13f));
        Label(right.transform,"アバター設定",46,new Vector2(.05f,.89f),new Vector2(.95f,.99f),new Color(1,.82f,.12f));
        Label(right.transform,"CHOOSE YOUR CHARACTER",22,new Vector2(.05f,.84f),new Vector2(.95f,.90f));
        var male=Button(right.transform,"男性 (29)\nMALE / BOYS",new Color(.05f,.36f,.82f),new Vector2(.035f,.735f),new Vector2(.49f,.83f));
        var female=Button(right.transform,"女性 (20)\nFEMALE / GIRLS",new Color(.88f,.23f,.10f),new Vector2(.51f,.735f),new Vector2(.965f,.83f));

        GameObject scrollObj=Panel("CharacterScrollView",right.transform,new Vector2(.025f,.025f),new Vector2(.975f,.715f),new Color(.005f,.025f,.07f,1));
        var scroll=scrollObj.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35;
        GameObject viewport=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(Mask));
        viewport.transform.SetParent(scrollObj.transform,false); Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color=Color.white; viewport.GetComponent<Mask>().showMaskGraphic=false;
        GameObject content=new GameObject("GridContent",typeof(RectTransform),typeof(GridLayoutGroup),typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform,false);
        var cr=content.GetComponent<RectTransform>(); cr.anchorMin=new Vector2(0,1); cr.anchorMax=new Vector2(1,1); cr.pivot=new Vector2(.5f,1); cr.sizeDelta=Vector2.zero;
        var grid=content.GetComponent<GridLayoutGroup>(); grid.padding=new RectOffset(12,12,12,12); grid.spacing=new Vector2(12,12); grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount=5; grid.cellSize=new Vector2(150,172);
        content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport.GetComponent<RectTransform>(); scroll.content=cr;

        var ui=canvas.GetComponent<CharacterSelectUI>(); if(ui==null) ui=Undo.AddComponent<CharacterSelectUI>(canvas.gameObject);
        var so=new SerializedObject(ui);
        so.FindProperty("panelRoot").objectReferenceValue=root;
        so.FindProperty("previewImage").objectReferenceValue=preview;
        so.FindProperty("selectedNameText").objectReferenceValue=name;
        so.FindProperty("modelListRoot").objectReferenceValue=content.transform;
        so.FindProperty("maleTabButton").objectReferenceValue=male;
        so.FindProperty("femaleTabButton").objectReferenceValue=female;
        so.FindProperty("confirmButton").objectReferenceValue=confirm;
        so.FindProperty("cancelButton").objectReferenceValue=cancel;
        so.FindProperty("database").objectReferenceValue=Resources.Load<CharacterModelDatabase>("CharacterModelDatabase");
        so.ApplyModifiedPropertiesWithoutUndo();

        EventSystem es=Object.FindFirstObjectByType<EventSystem>();
        if(es==null){ var go=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)); go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
        else if(es.GetComponent<InputSystemUIInputModule>()==null){ var oldInput=es.GetComponent<StandaloneInputModule>(); if(oldInput!=null) Undo.DestroyObjectImmediate(oldInput); Undo.AddComponent<InputSystemUIInputModule>(es.gameObject).AssignDefaultActions(); }

        root.SetActive(false);
        EnsureSelectionPoint();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=canvas.gameObject;
        Debug.Log("[CharacterSelectUISetup] Canvas内に永続UIを作成し2_Lobbyを保存しました。");
    }

    private static void EnsureSelectionPoint()
    {
        GameObject point = GameObject.Find("CharacterSelectPoint");
        if (point != null) return;
        point = new GameObject("CharacterSelectPoint");
        Undo.RegisterCreatedObjectUndo(point, "Create Character Select Point");
        point.transform.position = Vector3.zero;
        var trigger = point.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 3f;
        Undo.AddComponent<CharacterSelectInteractable>(point);
        Debug.LogWarning("[CharacterSelectUISetup] CharacterSelectPointを原点に作成しました。Sceneビューでロビーのキャラクター選択場所へ移動してください。");
    }

    private static GameObject Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>(); r.anchorMin=min; r.anchorMax=max; r.offsetMin=r.offsetMax=Vector2.zero;
        go.GetComponent<Image>().color=color; return go;
    }
    private static Text Label(Transform parent,string value,int size,Vector2 min,Vector2 max,Color? color=null)
    {
        var go=new GameObject("Text_"+value.Replace("\n","_"),typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>(); r.anchorMin=min; r.anchorMax=max; r.offsetMin=r.offsetMax=Vector2.zero;
        var t=go.GetComponent<Text>(); t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.text=value; t.fontSize=size; t.fontStyle=FontStyle.Bold; t.alignment=TextAnchor.MiddleCenter; t.color=color??Color.white; t.resizeTextForBestFit=true; t.raycastTarget=false; return t;
    }
    private static Button Button(Transform parent,string label,Color color,Vector2 min,Vector2 max)
    {
        var go=Panel("Button_"+label.Replace("\n","_"),parent,min,max,color); var b=go.AddComponent<Button>(); b.targetGraphic=go.GetComponent<Image>();
        Label(go.transform,label,24,new Vector2(.02f,.02f),new Vector2(.98f,.98f));
        var outline=go.AddComponent<Outline>(); outline.effectColor=new Color(0,0,0,.9f); outline.effectDistance=new Vector2(3,-3); return b;
    }
    private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
}
#endif
