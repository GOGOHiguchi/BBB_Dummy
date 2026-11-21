using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

[DisallowMultipleComponent]
public sealed class CharacterSelectUI : MonoBehaviour
{
    [Header("Canvasに保存するUI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private TMP_Text selectedNameText; // ★Text から TMP_Text に変更
    [SerializeField] private Transform modelListRoot;
    [SerializeField] private Button maleTabButton;
    [SerializeField] private Button femaleTabButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private CharacterModelDatabase database;

    private const int PreviewLayer = 31;
    private readonly Dictionary<int, RawImage> portraits = new Dictionary<int, RawImage>();
    private readonly List<RenderTexture> thumbnailTextures = new List<RenderTexture>();
    private Camera previewCamera, thumbnailCamera;
    private RenderTexture previewTexture;
    private Transform stage;
    private GameObject previewModel;
    private CharacterModelSwitcher target;
    private int selectedId, originalId;
    private bool showFemale, opened;

    public static bool IsAnyOpen { get; private set; }
    public bool IsOpen => opened;

    private void Awake()
    {
        if (database == null) database = Resources.Load<CharacterModelDatabase>("CharacterModelDatabase");
        if (panelRoot == null || previewImage == null || modelListRoot == null ||
            maleTabButton == null || femaleTabButton == null || confirmButton == null || cancelButton == null || database == null)
        {
            Debug.LogError("[CharacterSelectUI] UI参照またはCharacterModelDatabaseが不足しています。Tools/BBB/Build Character Select UI in 2_Lobbyを実行してください。", this);
            enabled = false; return;
        }

        maleTabButton.onClick.AddListener(() => SetGender(false));
        femaleTabButton.onClick.AddListener(() => SetGender(true));
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
        panelRoot.SetActive(false);
        BuildCameras();
        BuildTiles();
        StartCoroutine(BuildThumbnails());
    }

    private void Update()
    {
        if (opened && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
    }

    private void LateUpdate()
    {
        if (opened && previewModel != null)
        {
            previewModel.transform.Rotate(Vector3.up, 14f * Time.unscaledDeltaTime, Space.World);
            previewCamera.Render();
        }
    }

    private void OnDestroy()
    {
        IsAnyOpen = false;
        if (previewTexture != null) { previewTexture.Release(); Destroy(previewTexture); }
        foreach (var rt in thumbnailTextures) if (rt != null) { rt.Release(); Destroy(rt); }
        if (stage != null) Destroy(stage.gameObject);
    }

    public void Open(CharacterModelSwitcher player)
    {
        if (player == null || !enabled) return;
        target = player;
        originalId = player.SavedModelId;
        if (!database.IsValidId(originalId)) originalId = 0;
        selectedId = originalId;
        showFemale = selectedId >= 29;
        opened = IsAnyOpen = true;
        panelRoot.SetActive(true);
        SetGender(showFemale);
        ShowModel(selectedId);
        UpdateName();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetGender(bool female)
    {
        showFemale = female;
        maleTabButton.interactable = female;
        femaleTabButton.interactable = !female;

        int firstId = -1;
        foreach (var entry in database.Entries)
        {
            if (entry == null || entry.Prefab == null || entry.IsFemale != showFemale) continue;
            if (firstId < 0) firstId = entry.Id;
        }

        foreach (var pair in portraits)
        {
            if (database.TryGetEntry(pair.Key, out var entry))
                pair.Value.transform.parent.gameObject.SetActive(entry.IsFemale == showFemale);
        }

        if (firstId >= 0 && database.TryGetEntry(selectedId, out var selected) && selected.IsFemale != showFemale)
        {
            selectedId = firstId;
            ShowModel(selectedId);
            UpdateName();
        }
        RefreshSelectionOutline();
    }

    private void BuildTiles()
    {
        foreach (Transform child in modelListRoot) Destroy(child.gameObject);
        portraits.Clear();

        foreach (var entry in database.Entries)
        {
            if (entry == null || entry.Prefab == null) continue;
            GameObject tile = new GameObject("Character_" + entry.Id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            tile.transform.SetParent(modelListRoot, false);
            tile.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 172);
            tile.GetComponent<Image>().color = new Color(.035f, .22f, .52f, 1);
            tile.GetComponent<Button>().onClick.AddListener(() => Select(entry.Id));

            GameObject portrait = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
            portrait.transform.SetParent(tile.transform, false);
            var pr = portrait.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(.05f, .22f); pr.anchorMax = new Vector2(.95f, .96f);
            pr.offsetMin = pr.offsetMax = Vector2.zero;
            var raw = portrait.GetComponent<RawImage>();
            raw.raycastTarget = false;
            if (entry.Thumbnail != null) raw.texture = entry.Thumbnail.texture;
            portraits[entry.Id] = raw;

            GameObject labelObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(tile.transform, false);
            var lr = labelObj.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(.02f, .01f); lr.anchorMax = new Vector2(.98f, .23f);
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var label = labelObj.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 13; label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
            label.resizeTextForBestFit = true; label.raycastTarget = false;
            label.text = string.IsNullOrEmpty(entry.DisplayName) ? entry.Prefab.name : entry.DisplayName;
            var outline = tile.GetComponent<Outline>();
            outline.effectColor = new Color(1, .84f, .08f, 1); outline.effectDistance = new Vector2(4, -4);
            outline.enabled = false;
            tile.SetActive(false);
        }
    }

    private IEnumerator BuildThumbnails()
    {
        foreach (var entry in database.Entries)
        {
            if (entry == null || entry.Prefab == null || entry.Thumbnail != null) continue;
            GameObject model = Instantiate(entry.Prefab, stage);
            SetLayer(model, PreviewLayer);
            RenderTexture rt = new RenderTexture(144, 160, 16, RenderTextureFormat.ARGB32);
            rt.Create(); thumbnailTextures.Add(rt);
            bool previewWasActive = previewModel != null && previewModel.activeSelf;
            if (previewWasActive) previewModel.SetActive(false);
            Frame(thumbnailCamera, model, 2.5f);
            thumbnailCamera.targetTexture = rt;
            thumbnailCamera.Render();
            if (portraits.TryGetValue(entry.Id, out var image)) image.texture = rt;
            Destroy(model);
            if (previewWasActive) previewModel.SetActive(true);
            yield return null;
        }
    }

    private void BuildCameras()
    {
        GameObject stageObject = new GameObject("CharacterSelect_PreviewStage");
        stageObject.transform.position = new Vector3(0, -1000, 0);
        stage = stageObject.transform;
        previewTexture = new RenderTexture(512, 640, 16, RenderTextureFormat.ARGB32);
        previewTexture.Create(); previewImage.texture = previewTexture;
        previewCamera = MakeCamera("PreviewCamera", previewTexture);
        thumbnailCamera = MakeCamera("ThumbnailCamera", null);
    }

    private Camera MakeCamera(string name, RenderTexture rt)
    {
        GameObject go = new GameObject(name); go.transform.SetParent(stage, false);
        Camera cam = go.AddComponent<Camera>();
        cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.10f, .62f, .84f);
        cam.orthographic = true; cam.nearClipPlane = .05f; cam.farClipPlane = 40;
        cam.cullingMask = 1 << PreviewLayer; cam.targetTexture = rt;
        return cam;
    }

    private void ShowModel(int id)
    {
        if (!database.TryGetEntry(id, out var entry)) return;
        if (previewModel != null)
        {
            // Destroyはフレーム終端まで残るため、先に非表示にして旧モデルの二重描画を防ぐ。
            previewModel.SetActive(false);
            Destroy(previewModel);
        }
        previewModel = Instantiate(entry.Prefab, stage);
        SetLayer(previewModel, PreviewLayer);

        // 実プレイヤーと同じAnimator Controllerを使い、プレビューの姿勢を揃える。
        if (target != null)
        {
            Animator sourceAnimator = target.GetComponentInChildren<Animator>(true);
            Animator previewAnimator = previewModel.GetComponentInChildren<Animator>(true);
            if (sourceAnimator != null && previewAnimator != null)
                previewAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
        }

        Frame(previewCamera, previewModel, 2.15f);
        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();
    }

    private void Frame(Camera cam, GameObject model, float scale)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(model.transform.position, Vector3.one);
        bool found = false;
        foreach (var r in renderers) { if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds); }
        Vector3 center = stage.position;
        model.transform.position += center - bounds.center;
        float h = Mathf.Max(bounds.size.y, 1.2f), w = Mathf.Max(bounds.size.x, .7f);
        cam.transform.position = center + Vector3.forward * Mathf.Max(h, w) * scale + Vector3.up * h * .04f;
        cam.transform.LookAt(center + Vector3.up * h * .48f);
        cam.orthographicSize = Mathf.Max(h * .58f, w * .9f);
    }

    private void Select(int id)
    {
        if (!database.IsValidId(id)) return;
        selectedId = id;
        ShowModel(id);
        UpdateName();
        RefreshSelectionOutline();
    }

    private void RefreshSelectionOutline()
    {
        foreach (var entry in database.Entries)
        {
            if (entry == null) continue;
            var tile = modelListRoot.Find("Character_" + entry.Id);
            if (tile == null) continue;
            var outline = tile.GetComponent<Outline>();
            if (outline != null) outline.enabled = entry.Id == selectedId;
        }
    }

    private void UpdateName()
    {
        if (database.TryGetEntry(selectedId, out var entry) && selectedNameText != null)
            selectedNameText.text = string.IsNullOrEmpty(entry.DisplayName) ? entry.Prefab.name : entry.DisplayName;
    }

    private void Confirm()
    {
        if (target == null || !target.ConfirmSelection(selectedId)) return;
        Close();
    }

    private void Cancel()
    {
        // プレビューは専用ステージ内だけで行っているため、プレイヤー本体は変更しない。
        selectedId = originalId;
        Close();
    }

    private void Close()
    {
        opened = IsAnyOpen = false;
        panelRoot.SetActive(false);
        target = null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void SetLayer(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayer(child.gameObject, layer);
    }
}