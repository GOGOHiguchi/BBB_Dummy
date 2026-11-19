using Fusion;
using UnityEngine;

/// <summary>
/// Playerのネットワークルートを維持したまま、見た目モデルだけを全クライアントで切り替える。
/// ID 0～28が男性、29～48が女性。プレビューはローカルだけに適用し、確定時に保存・同期する。
/// </summary>
public class CharacterModelSwitcher : NetworkBehaviour
{
    public const string PreferenceKey = "BBB.CharacterModel";
    private const string DatabaseResourceName = "CharacterModelDatabase";

    [Header("モデルデータ")]
    [SerializeField] private CharacterModelDatabase modelDatabase;

    // 旧Prefab設定との互換用。Databaseが見つからない場合のみ使用する。
    [Header("旧モデルPrefab（互換用）")]
    [SerializeField] private GameObject maleModelPrefab;
    [SerializeField] private GameObject femaleModelPrefab;

    [Header("モデル配置先")]
    [SerializeField] private Transform characterVisualRoot;

    [Networked]
    public int CharacterModelId { get; set; }

    public CharacterModelDatabase ModelDatabase => GetDatabase();
    public int SavedModelId => PlayerPrefs.GetInt(PreferenceKey, 0);

    private RuntimeAnimatorController _sharedAnimatorController;
    private int _appliedModelId = -1;
    private bool _isPreviewing;

    private PlayerMovement _playerMovement;
    private EmoteWheelController _emoteController;
    private BowlingArmController _bowlingArmController;

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _emoteController = GetComponent<EmoteWheelController>();
        _bowlingArmController = GetComponent<BowlingArmController>();

        if (characterVisualRoot == null)
            characterVisualRoot = transform.Find("CharacterVisual");

        Animator currentAnimator = GetComponentInChildren<Animator>(true);
        if (currentAnimator != null)
            _sharedAnimatorController = currentAnimator.runtimeAnimatorController;
    }

    private CharacterModelDatabase GetDatabase()
    {
        if (modelDatabase == null)
            modelDatabase = Resources.Load<CharacterModelDatabase>(DatabaseResourceName);
        return modelDatabase;
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            int savedModelId = SavedModelId;
            CharacterModelDatabase database = GetDatabase();
            if (database != null && !database.IsValidId(savedModelId))
                savedModelId = 0;

            RpcSetCharacterModel(savedModelId);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcSetCharacterModel(int modelId)
    {
        CharacterModelDatabase database = GetDatabase();
        if (database != null && !database.IsValidId(modelId))
        {
            Debug.LogWarning($"[CharacterModelSwitcher] 無効なモデルIDを受信しました: {modelId}", this);
            return;
        }

        CharacterModelId = Mathf.Clamp(modelId, 0, 48);
    }

    public override void Render()
    {
        if (_isPreviewing)
            return;

        if (_appliedModelId != CharacterModelId)
            ApplyModel(CharacterModelId);
    }

    /// <summary>
    /// 選択画面のプレビュー。PlayerPrefsとNetworked値は変更しない。
    /// </summary>
    public bool PreviewModel(int modelId)
    {
        CharacterModelDatabase database = GetDatabase();
        if (database == null || !database.IsValidId(modelId))
        {
            Debug.LogWarning($"[CharacterModelSwitcher] プレビュー対象のモデルIDがありません: {modelId}", this);
            return false;
        }

        _isPreviewing = true;
        return ApplyModel(modelId);
    }

    /// <summary>キャンセル時にネットワーク上の確定済みモデルへ戻す。</summary>
    public void CancelPreview()
    {
        _isPreviewing = false;
        ApplyModel(CharacterModelId);
    }

    /// <summary>選択をローカル保存し、Photon Fusion経由で全員へ反映する。</summary>
    public bool ConfirmSelection(int modelId)
    {
        if (!HasInputAuthority)
            return false;

        CharacterModelDatabase database = GetDatabase();
        if (database == null || !database.IsValidId(modelId))
        {
            Debug.LogError($"[CharacterModelSwitcher] 確定できないモデルIDです: {modelId}", this);
            return false;
        }

        PlayerPrefs.SetInt(PreferenceKey, modelId);
        PlayerPrefs.Save();

        _isPreviewing = false;
        RpcSetCharacterModel(modelId);
        return true;
    }

    private bool ApplyModel(int modelId)
    {
        if (characterVisualRoot == null)
        {
            Debug.LogError("[CharacterModelSwitcher] CharacterVisualが見つかりません。", this);
            return false;
        }

        GameObject modelPrefab = ResolveModelPrefab(modelId);
        if (modelPrefab == null)
        {
            Debug.LogError($"[CharacterModelSwitcher] モデルPrefabが未設定です。ModelId={modelId}", this);
            return false;
        }

        // NetworkObjectであるロケットランチャーをモデル破棄前に退避する。
        GameObject launcherObject = _playerMovement != null ? _playerMovement.RocketLauncherObject : null;
        if (launcherObject == null && _playerMovement != null &&
            _playerMovement.RocketLauncherController != null)
        {
            launcherObject = _playerMovement.RocketLauncherController.gameObject;
        }

        Transform launcherTransform = launcherObject != null ? launcherObject.transform : null;
        bool launcherWasInsideVisual = launcherTransform != null &&
                                        launcherTransform.IsChildOf(characterVisualRoot);

        Vector3 launcherLocalPosition = Vector3.zero;
        Quaternion launcherLocalRotation = Quaternion.identity;
        Vector3 launcherLocalScale = Vector3.one;

        if (launcherWasInsideVisual)
        {
            launcherLocalPosition = launcherTransform.localPosition;
            launcherLocalRotation = launcherTransform.localRotation;
            launcherLocalScale = launcherTransform.localScale;
            launcherTransform.SetParent(transform, true);
        }

        for (int i = characterVisualRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = characterVisualRoot.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        GameObject modelInstance = Instantiate(modelPrefab, characterVisualRoot, false);
        modelInstance.name = modelPrefab.name;
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        if (launcherWasInsideVisual && launcherTransform != null)
        {
            Transform rightHand = FindRightHandContainer(modelInstance.transform);
            if (rightHand != null)
            {
                launcherTransform.SetParent(rightHand, false);
                launcherTransform.localPosition = launcherLocalPosition;
                launcherTransform.localRotation = launcherLocalRotation;
                launcherTransform.localScale = launcherLocalScale;
            }
            else
            {
                launcherTransform.SetParent(transform, true);
                Debug.LogWarning("[CharacterModelSwitcher] 右手の装着先が見つからないため、ロケットランチャーをプレイヤールートに退避しました。", modelInstance);
            }
        }

        Animator modelAnimator = modelInstance.GetComponentInChildren<Animator>(true);
        if (modelAnimator == null)
        {
            Debug.LogError($"[CharacterModelSwitcher] {modelPrefab.name} にAnimatorがありません。", modelPrefab);
            _appliedModelId = -1;
            return false;
        }

        if (_sharedAnimatorController != null)
            modelAnimator.runtimeAnimatorController = _sharedAnimatorController;

        modelAnimator.applyRootMotion = false;

        Transform leftUpperArm = FindLeftUpperArm(modelInstance.transform);

        if (_playerMovement != null)
            _playerMovement.SetCharacterAnimator(modelAnimator);

        if (_emoteController != null)
            _emoteController.SetCharacterAnimator(modelAnimator);

        if (_bowlingArmController != null)
            _bowlingArmController.SetCharacterAnimator(modelAnimator, leftUpperArm);

        _appliedModelId = modelId;
        return true;
    }

    private GameObject ResolveModelPrefab(int modelId)
    {
        CharacterModelDatabase database = GetDatabase();
        if (database != null && database.TryGetEntry(modelId, out CharacterModelDatabase.Entry entry))
            return entry.Prefab;

        // Database導入前の既存シーンでも起動できるようにする。
        if (database == null)
            return modelId == 1 ? femaleModelPrefab : maleModelPrefab;

        return null;
    }

    private static Transform FindRightHandContainer(Transform modelRoot)
    {
        foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "R_hand_container" ||
                child.name == "RightHandContainer" ||
                child.name == "RightHand" ||
                child.name == "mixamorig:RightHand")
                return child;
        }

        return null;
    }

    private static Transform FindLeftUpperArm(Transform modelRoot)
    {
        foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Bip001 L UpperArm" ||
                child.name == "LeftUpperArm" ||
                child.name == "LeftArm" ||
                child.name == "mixamorig:LeftArm" ||
                child.name == "upperarm_l")
                return child;
        }

        return null;
    }
}
