using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ロビー用アバター選択画面。Canvasに追加するとUIを自動生成する。
/// CharacterModelDatabaseの49モデルからサムネイルをランタイム生成する。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterSelectUI : MonoBehaviour
{
    private const string DatabaseResourceName = "CharacterModelDatabase";
    private const int PreviewLayer = 31;

    [Header("表示設定")]
    [SerializeField] private CharacterModelDatabase database;
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private Color panelColor = new Color(0.025f, 0.12f, 0.25f, 0.97f);

    private readonly List<Texture2D> _thumbnailTextures = new List<Texture2D>();
    private readonly List<Sprite> _thumbnailSprites = new List<Sprite>();
    private readonly Dictionary<int, Sprite> _thumbnailById = new Dictionary<int, Sprite>();
    private readonly Dictionary<int, TilePortrait> _tilesById = new Dictionary<int, TilePortrait>();
    private readonly List<Button> _maleButtons = new List<Button>();
    private readonly List<Button> _femaleButtons = new List<Button>();

    private RectTransform _root;
    private RectTransform _gridContent;
    private ScrollRect _scrollRect;
    private RawImage _previewImage;
    private Text _maleCount;
    private Text _femaleCount;
    private Text _selectedName;
    private Text _statusText;
    private Button _maleTab;
    private Button _femaleTab;
    private Button _confirmButton;
    private Button _cancelButton;

    private Camera _previewCamera;
    private RenderTexture _previewTexture;
    private Transform _previewStage;
    private GameObject _previewModel;
    private CharacterModelSwitcher _localSwitcher;

    private int _selectedId;
    private int _originalId;
    private bool _showFemale;
    private bool _built;
    private bool _closed;

    private Font _font;

    private void Start()
    {
        if (database == null)
            database = Resources.Load<CharacterModelDatabase>(DatabaseResourceName);

        if (database == null)
        {
            Debug.LogError("[CharacterSelectUI] Resources/CharacterModelDatabase が見つかりません。", this);
            return;
        }

        _localSwitcher = FindLocalSwitcher();
        _originalId = _localSwitcher != null
            ? _localSwitcher.SavedModelId
            : PlayerPrefs.GetInt(CharacterModelSwitcher.PreferenceKey, 0);

        if (!database.IsValidId(_originalId))
            _originalId = 0;

        _selectedId = _originalId;
        _showFemale = _selectedId >= 29;

        BuildPreviewStage();
        BuildInterface();
        RefreshGrid();
        UpdatePreview(_selectedId);
        SetVisible(showOnStart);

        if (_previewModel != null)
            _previewModel.SetActive(false);
        StartCoroutine(GenerateAllThumbnails());
    }

    private void LateUpdate()
    {
        if (_previewModel != null && !_closed)
            _previewModel.transform.Rotate(0f, 12f * Time.unscaledDeltaTime, 0f, Space.World);
    }

    private CharacterModelSwitcher FindLocalSwitcher()
    {
        CharacterModelSwitcher[] switchers = FindObjectsByType<CharacterModelSwitcher>(FindObjectsSortMode.None);
        foreach (CharacterModelSwitcher candidate in switchers)
        {
            if (candidate != null && candidate.Object != null && candidate.HasInputAuthority)
                return candidate;
        }
        return null;
    }

    private void BuildPreviewStage()
    {
        GameObject stage = new GameObject("CharacterSelect_PreviewStage");
        stage.transform.position = new Vector3(0f, -1000f, 0f);
        _previewStage = stage.transform;

        GameObject cameraObject = new GameObject("CharacterSelect_PreviewCamera");
        cameraObject.transform.SetParent(_previewStage, false);
        _previewCamera = cameraObject.AddComponent<Camera>();
        _previewCamera.enabled = false;
        _previewCamera.clearFlags = CameraClearFlags.SolidColor;
        _previewCamera.backgroundColor = new Color(0.12f, 0.68f, 0.88f, 1f);
        _previewCamera.orthographic = true;
        _previewCamera.nearClipPlane = 0.05f;
        _previewCamera.farClipPlane = 30f;
        _previewCamera.cullingMask = 1 << PreviewLayer;

        _previewTexture = new RenderTexture(256, 320, 16, RenderTextureFormat.ARGB32);
        _previewTexture.name = "CharacterSelect_Preview_RT";
        _previewTexture.Create();
        _previewCamera.targetTexture = _previewTexture;
    }

    private void BuildInterface()
    {
        if (_built) return;
        _built = true;

        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null)
            _font = Font.CreateDynamicFontFromOSFont("Arial", 20);

        _root = CreateRect("CharacterSelect_Root", transform);
        Stretch(_root);
        Image rootImage = _root.gameObject.AddComponent<Image>();
        rootImage.color = new Color(0.015f, 0.045f, 0.10f, 0.96f);
        rootImage.raycastTarget = true;

        RectTransform left = CreateRect("LeftPanel", _root);
        Anchor(left, 0.015f, 0.025f, 0.335f, 0.975f);
        AddImage(left, new Color(0.025f, 0.20f, 0.36f, 0.98f), 22);

        RectTransform right = CreateRect("RightPanel", _root);
        Anchor(right, 0.35f, 0.025f, 0.985f, 0.975f);
        AddImage(right, new Color(0.015f, 0.07f, 0.16f, 0.98f), 22);

        CreateText(left, "CHARACTERS", 38, FontStyle.Bold, Color.white,
            new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.99f), TextAnchor.MiddleCenter);
        CreateText(left, "キャラクター", 27, FontStyle.Bold, Color.white,
            new Vector2(0.04f, 0.855f), new Vector2(0.96f, 0.925f), TextAnchor.MiddleCenter);

        RectTransform previewFrame = CreateRect("PreviewFrame", left);
        Anchor(previewFrame, 0.055f, 0.205f, 0.945f, 0.845f);
        AddImage(previewFrame, new Color(0.05f, 0.52f, 0.73f, 1f), 18);

        RectTransform previewRect = CreateRect("AvatarPreview", previewFrame);
        Stretch(previewRect);
        _previewImage = previewRect.gameObject.AddComponent<RawImage>();
        _previewImage.texture = _previewTexture;
        _previewImage.color = Color.white;
        _previewImage.raycastTarget = false;

        _selectedName = CreateText(left, "", 24, FontStyle.Bold, Color.white,
            new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.205f), TextAnchor.MiddleCenter);

        _cancelButton = CreateButton(left, "もどる\n(CANCEL)", new Color(0.10f, 0.43f, 0.78f),
            new Vector2(0.055f, 0.035f), new Vector2(0.475f, 0.135f), 27, CancelSelection);
        _confirmButton = CreateButton(left, "決定\n(SAVE)", new Color(0.15f, 0.72f, 0.16f),
            new Vector2(0.525f, 0.035f), new Vector2(0.945f, 0.135f), 29, ConfirmSelection);

        CreateText(right, "アバター設定", 48, FontStyle.Bold, new Color(1f, 0.84f, 0.15f),
            new Vector2(0.05f, 0.89f), new Vector2(0.95f, 0.99f), TextAnchor.MiddleCenter);
        CreateText(right, "CHOOSE YOUR CHARACTER", 24, FontStyle.Bold, Color.white,
            new Vector2(0.05f, 0.845f), new Vector2(0.95f, 0.90f), TextAnchor.MiddleCenter);

        _maleTab = CreateButton(right, "男性  (29)\nMALE / BOYS", new Color(0.06f, 0.40f, 0.88f),
            new Vector2(0.035f, 0.735f), new Vector2(0.49f, 0.835f), 27,
            () => SetGender(false));
        _femaleTab = CreateButton(right, "女性  (20)\nFEMALE / GIRLS", new Color(0.90f, 0.25f, 0.10f),
            new Vector2(0.51f, 0.735f), new Vector2(0.965f, 0.835f), 27,
            () => SetGender(true));

        _maleCount = CreateText(right, "29", 25, FontStyle.Bold, Color.white,
            new Vector2(0.43f, 0.72f), new Vector2(0.49f, 0.78f), TextAnchor.MiddleCenter);
        _femaleCount = CreateText(right, "20", 25, FontStyle.Bold, Color.white,
            new Vector2(0.905f, 0.72f), new Vector2(0.965f, 0.78f), TextAnchor.MiddleCenter);

        RectTransform scrollArea = CreateRect("CharacterScrollView", right);
        Anchor(scrollArea, 0.025f, 0.025f, 0.975f, 0.715f);
        Image scrollBackground = AddImage(scrollArea, new Color(0.005f, 0.035f, 0.09f, 1f), 16);

        _scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 35f;

        RectTransform viewport = CreateRect("Viewport", scrollArea);
        Stretch(viewport);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        _gridContent = CreateRect("GridContent", viewport);
        _gridContent.anchorMin = new Vector2(0f, 1f);
        _gridContent.anchorMax = new Vector2(1f, 1f);
        _gridContent.pivot = new Vector2(0.5f, 1f);
        _gridContent.anchoredPosition = Vector2.zero;
        _gridContent.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup grid = _gridContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.spacing = new Vector2(12f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.cellSize = new Vector2(205f, 210f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter fitter = _gridContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect.viewport = viewport;
        _scrollRect.content = _gridContent;

        _statusText = CreateText(right, "", 16, FontStyle.Normal, Color.white,
            new Vector2(0.04f, 0.0f), new Vector2(0.96f, 0.035f), TextAnchor.MiddleCenter);
    }

    private void RefreshGrid()
    {
        if (_gridContent == null) return;

        for (int i = _gridContent.childCount - 1; i >= 0; i--)
            Destroy(_gridContent.GetChild(i).gameObject);

        _maleButtons.Clear();
        _femaleButtons.Clear();

        IReadOnlyList<CharacterModelDatabase.Entry> entries = database.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            CharacterModelDatabase.Entry entry = entries[i];
            if (entry == null || entry.Prefab == null || entry.IsFemale != _showFemale)
                continue;

            CreateCharacterTile(entry);
        }

        _scrollRect.verticalNormalizedPosition = 1f;
        UpdateTabAppearance();
    }

    private void CreateCharacterTile(CharacterModelDatabase.Entry entry)
    {
        GameObject tileObject = new GameObject("Character_" + entry.Id, typeof(RectTransform), typeof(Image), typeof(Button));
        tileObject.transform.SetParent(_gridContent, false);

        Image tileImage = tileObject.GetComponent<Image>();
        tileImage.color = new Color(0.025f, 0.25f, 0.58f, 1f);

        Button button = tileObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.75f, 0.9f, 1f);
        colors.pressedColor = new Color(0.5f, 0.75f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => SelectCharacter(entry.Id));

        Outline outline = tileObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.1f, 1f);
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = entry.Id == _selectedId;

        RectTransform tileRect = tileObject.GetComponent<RectTransform>();
        tileRect.sizeDelta = new Vector2(150f, 165f);

        GameObject portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(tileObject.transform, false);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        Anchor(portraitRect, 0.06f, 0.20f, 0.94f, 0.96f);
        Image portrait = portraitObject.GetComponent<Image>();
        portrait.color = new Color(0.1f, 0.45f, 0.85f, 1f);
        portrait.preserveAspect = true;
        if (entry.Thumbnail != null)
            portrait.sprite = entry.Thumbnail;
        else if (_thumbnailById.TryGetValue(entry.Id, out Sprite cachedSprite))
            portrait.sprite = cachedSprite;

        Text placeholder = CreateText(tileObject.transform, entry.DisplayName, 14, FontStyle.Bold,
            Color.white, new Vector2(0.04f, 0.015f), new Vector2(0.96f, 0.22f), TextAnchor.MiddleCenter);
        placeholder.resizeTextForBestFit = true;
        placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;
        placeholder.verticalOverflow = VerticalWrapMode.Truncate;

        if (entry.IsFemale) _femaleButtons.Add(button);
        else _maleButtons.Add(button);

        TilePortrait portraitData = new TilePortrait { Image = portrait, Outline = outline, Id = entry.Id };
        _tilesById[entry.Id] = portraitData;
    }

    private void SetGender(bool female)
    {
        _showFemale = female;
        RefreshGrid();
    }

    private void UpdateTabAppearance()
    {
        if (_maleTab != null)
            _maleTab.interactable = _showFemale;
        if (_femaleTab != null)
            _femaleTab.interactable = !_showFemale;
    }

    private void SelectCharacter(int id)
    {
        if (!database.IsValidId(id)) return;

        _selectedId = id;
        _showFemale = id >= 29;
        RefreshGrid();
        UpdatePreview(id);

        if (_localSwitcher == null)
            _localSwitcher = FindLocalSwitcher();

        _statusText.text = "選択中: " + GetEntryName(id);
    }

    private void UpdatePreview(int id)
    {
        if (_previewStage == null || !database.TryGetEntry(id, out CharacterModelDatabase.Entry entry))
            return;

        if (_previewModel != null)
            Destroy(_previewModel);

        _previewModel = Instantiate(entry.Prefab, _previewStage);
        _previewModel.name = "Preview_" + entry.DisplayName;
        _previewModel.transform.localPosition = Vector3.zero;
        _previewModel.transform.localRotation = Quaternion.identity;
        SetLayerRecursively(_previewModel, PreviewLayer);

        Animator previewAnimator = _previewModel.GetComponentInChildren<Animator>(true);
        if (previewAnimator != null)
        {
            if (_localSwitcher == null)
                _localSwitcher = FindLocalSwitcher();

            if (_localSwitcher != null)
            {
                PlayerMovement localMovement = _localSwitcher.GetComponent<PlayerMovement>();
                Animator sourceAnimator = localMovement != null
                    ? localMovement.GetComponentInChildren<Animator>(true)
                    : null;
                if (sourceAnimator != null)
                    previewAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            }
            previewAnimator.applyRootMotion = false;
        }

        FitPreviewCamera(_previewModel);
        _previewCamera.Render();

        if (_selectedName != null)
            _selectedName.text = entry.DisplayName;
    }

    private void FitPreviewCamera(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 center = bounds.center;
        float height = Mathf.Max(bounds.size.y, 1f);
        float width = Mathf.Max(bounds.size.x, 0.6f);
        _previewCamera.orthographicSize = Mathf.Max(height * 0.62f, width * 0.85f);
        _previewCamera.transform.position = center + new Vector3(0f, height * 0.12f, -5.5f);
        _previewCamera.transform.LookAt(center + Vector3.up * (height * 0.08f));
    }

    private IEnumerator GenerateAllThumbnails()
    {
        if (_gridContent == null) yield break;

        IReadOnlyList<CharacterModelDatabase.Entry> entries = database.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            CharacterModelDatabase.Entry entry = entries[i];
            if (entry == null || entry.Prefab == null) continue;

            if (entry.Thumbnail != null || _thumbnailById.ContainsKey(entry.Id))
                continue;

            GameObject capture = Instantiate(entry.Prefab, _previewStage);
            capture.name = "ThumbnailCapture_" + entry.DisplayName;
            capture.transform.localPosition = Vector3.zero;
            capture.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(capture, PreviewLayer);
            FitPreviewCamera(capture);

            yield return new WaitForEndOfFrame();
            _previewCamera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _previewTexture;
            Texture2D texture = new Texture2D(128, 160, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(64f, 80f, 128f, 160f), 0, 0);
            texture.Apply(false, false);
            RenderTexture.active = previous;

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            _thumbnailById[entry.Id] = sprite;
            if (_tilesById.TryGetValue(entry.Id, out TilePortrait tile) && tile.Image != null)
            {
                tile.Image.sprite = sprite;
                tile.Image.color = Color.white;
                tile.Image.preserveAspect = true;
            }
            _thumbnailTextures.Add(texture);
            _thumbnailSprites.Add(sprite);

            Destroy(capture);
            yield return null;
        }

        UpdatePreview(_selectedId);
    }

    private string GetEntryName(int id)
    {
        return database.TryGetEntry(id, out CharacterModelDatabase.Entry entry)
            ? entry.DisplayName
            : "Unknown";
    }

    private void ConfirmSelection()
    {
        if (!database.IsValidId(_selectedId)) return;

        if (_localSwitcher == null)
            _localSwitcher = FindLocalSwitcher();

        bool confirmed = false;
        if (_localSwitcher != null && _localSwitcher.HasInputAuthority)
            confirmed = _localSwitcher.ConfirmSelection(_selectedId);

        if (!confirmed)
        {
            PlayerPrefs.SetInt(CharacterModelSwitcher.PreferenceKey, _selectedId);
            PlayerPrefs.Save();
        }

        _originalId = _selectedId;
        _closed = true;
        SetVisible(false);
    }

    private void CancelSelection()
    {
        if (_localSwitcher == null)
            _localSwitcher = FindLocalSwitcher();

        _selectedId = _originalId;
        _showFemale = _selectedId >= 29;
        UpdatePreview(_selectedId);
        _closed = true;
        SetVisible(false);
    }

    public void Open()
    {
        if (!_built || database == null) return;

        _closed = false;
        _selectedId = PlayerPrefs.GetInt(CharacterModelSwitcher.PreferenceKey, _originalId);
        if (!database.IsValidId(_selectedId)) _selectedId = 0;
        _showFemale = _selectedId >= 29;
        RefreshGrid();
        UpdatePreview(_selectedId);
        SetVisible(true);
    }

    public void SetVisible(bool visible)
    {
        if (_root != null)
            _root.gameObject.SetActive(visible);

        if (visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnDestroy()
    {
        if (_previewCamera != null)
            _previewCamera.targetTexture = null;
        if (_previewTexture != null)
        {
            _previewTexture.Release();
            Destroy(_previewTexture);
        }
        if (_previewModel != null)
            Destroy(_previewModel);
        if (_previewStage != null)
            Destroy(_previewStage.gameObject);

        foreach (Sprite sprite in _thumbnailSprites)
            if (sprite != null) Destroy(sprite);
        foreach (Texture2D texture in _thumbnailTextures)
            if (texture != null) Destroy(texture);
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void Anchor(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
    {
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private Image AddImage(RectTransform rect, Color color, float radius)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(Transform parent, string value, int size, FontStyle style, Color color,
        Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment)
    {
        GameObject go = new GameObject("Text_" + value, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Anchor(rect, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);

        Text text = go.GetComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(Transform parent, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax, int fontSize, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject("Button_" + label.Replace("\n", "_"),
            typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        Anchor(rect, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);

        Image image = go.GetComponent<Image>();
        image.color = color;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(3f, -3f);

        Text text = CreateText(go.transform, label, fontSize, FontStyle.Bold, Color.white,
            new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), TextAnchor.MiddleCenter);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = fontSize;
        text.lineSpacing = 0.9f;

        return button;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private sealed class TilePortrait
    {
        public int Id;
        public Image Image;
        public Outline Outline;
    }
}
