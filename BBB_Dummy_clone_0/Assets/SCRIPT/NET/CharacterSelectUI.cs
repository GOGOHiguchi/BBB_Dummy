using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ロビーのキャラクター選択UI。
/// panel / modelListRoot / modelButtonPrefab等はInspectorから割り当てる。
/// </summary>
public sealed class characterSelectUI : MonoBehaviour
{
    [Header("画面")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform modelListRoot;
    [SerializeField] private Button modelButtonPrefab;
    [SerializeField] private TMP_Text selectedModelName;

    [Header("性別タブ")]
    [SerializeField] private Button maleTabButton;
    [SerializeField] private Button femaleTabButton;

    [Header("決定・キャンセル")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private CharacterModelSwitcher _target;
    private readonly List<Button> _createdButtons = new List<Button>();
    private int _selectedModelId;
    private bool _showFemale;

    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (maleTabButton != null)
            maleTabButton.onClick.AddListener(() => ShowGender(false));
        if (femaleTabButton != null)
            femaleTabButton.onClick.AddListener(() => ShowGender(true));
        if (confirmButton != null)
            confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(Cancel);
    }

    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Cancel();
    }

    public void Open(CharacterModelSwitcher target)
    {
        if (target == null || target.ModelDatabase == null)
        {
            Debug.LogError("[CharacterSelectUI] PlayerまたはCharacterModelDatabaseが設定されていません。", this);
            return;
        }

        _target = target;
        int savedId = target.SavedModelId;
        if (!target.ModelDatabase.IsValidId(savedId))
            savedId = 0;

        _selectedModelId = savedId;
        _showFemale = _selectedModelId >= 29;

        if (panel != null)
            panel.SetActive(true);

        target.PreviewModel(_selectedModelId);
        ShowGender(_showFemale);
        UpdateSelectedLabel();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowGender(bool female)
    {
        _showFemale = female;
        RebuildModelList();
    }

    private void RebuildModelList()
    {
        ClearModelButtons();

        if (_target == null || _target.ModelDatabase == null ||
            modelListRoot == null || modelButtonPrefab == null)
            return;

        IReadOnlyList<CharacterModelDatabase.Entry> entries = _target.ModelDatabase.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            CharacterModelDatabase.Entry entry = entries[i];
            if (entry == null || entry.Prefab == null || entry.IsFemale != _showFemale)
                continue;

            Button button = Instantiate(modelButtonPrefab, modelListRoot);
            _createdButtons.Add(button);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Prefab.name : entry.DisplayName;

            int modelId = entry.Id;
            button.onClick.AddListener(() => SelectModel(modelId));
        }
    }

    private void SelectModel(int modelId)
    {
        if (_target == null || !_target.PreviewModel(modelId))
            return;

        _selectedModelId = modelId;
        UpdateSelectedLabel();
    }

    private void UpdateSelectedLabel()
    {
        if (selectedModelName == null || _target == null || _target.ModelDatabase == null)
            return;

        if (_target.ModelDatabase.TryGetEntry(_selectedModelId, out CharacterModelDatabase.Entry entry))
            selectedModelName.text = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Prefab.name : entry.DisplayName;
    }

    private void Confirm()
    {
        if (_target == null || !_target.ConfirmSelection(_selectedModelId))
            return;

        Close();
    }

    private void Cancel()
    {
        if (_target != null)
            _target.CancelPreview();

        Close();
    }

    private void Close()
    {
        if (panel != null)
            panel.SetActive(false);

        ClearModelButtons();
        _target = null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ClearModelButtons()
    {
        for (int i = 0; i < _createdButtons.Count; i++)
        {
            if (_createdButtons[i] != null)
                Destroy(_createdButtons[i].gameObject);
        }
        _createdButtons.Clear();
    }
}
