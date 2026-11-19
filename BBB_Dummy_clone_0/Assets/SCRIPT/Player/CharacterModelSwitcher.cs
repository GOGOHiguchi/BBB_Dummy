using Fusion;
using UnityEngine;

/// <summary>
/// Playerのネットワークルートを維持したまま、見た目モデルだけを全クライアントで切り替える。
/// 0 = 男性、1 = 女性。選択値は各PCのPlayerPrefsからInputAuthorityがStateAuthorityへ通知する。
/// </summary>
public class CharacterModelSwitcher : NetworkBehaviour
{
    public const string PreferenceKey = "BBB.CharacterModel";

    [Header("モデルPrefab（見た目専用）")]
    [SerializeField] private GameObject maleModelPrefab;
    [SerializeField] private GameObject femaleModelPrefab;

    [Header("モデル配置先")]
    [SerializeField] private Transform characterVisualRoot;

    [Networked]
    public int CharacterModelId { get; set; }

    private RuntimeAnimatorController _sharedAnimatorController;
    private int _appliedModelId = -1;

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

        // 既存モデルに設定されているAnimator Controllerを新モデルへ引き継ぐ。
        Animator currentAnimator = GetComponentInChildren<Animator>(true);
        if (currentAnimator != null)
            _sharedAnimatorController = currentAnimator.runtimeAnimatorController;
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            int savedModelId = PlayerPrefs.GetInt(PreferenceKey, 0);
            RpcSetCharacterModel(Mathf.Clamp(savedModelId, 0, 1));
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcSetCharacterModel(int modelId)
    {
        CharacterModelId = Mathf.Clamp(modelId, 0, 1);
    }

    public override void Render()
    {
        // Networked値の更新を各クライアントで検知して、見た目だけを切り替える。
        if (_appliedModelId != CharacterModelId)
            ApplyModel(CharacterModelId);
    }

    private void ApplyModel(int modelId)
    {
        if (characterVisualRoot == null)
        {
            Debug.LogError("[CharacterModelSwitcher] CharacterVisualが見つかりません。", this);
            return;
        }

        GameObject modelPrefab = modelId == 1 ? femaleModelPrefab : maleModelPrefab;
        if (modelPrefab == null)
        {
            Debug.LogError($"[CharacterModelSwitcher] モデルPrefabが未設定です。ModelId={modelId}", this);
            return;
        }

        // CurrentModelなど、配置先直下にある旧モデルだけを入れ替える。
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

        Animator modelAnimator = modelInstance.GetComponentInChildren<Animator>(true);
        if (modelAnimator == null)
        {
            Debug.LogError($"[CharacterModelSwitcher] {modelPrefab.name} にAnimatorがありません。", modelPrefab);
            _appliedModelId = modelId;
            return;
        }

        if (_sharedAnimatorController != null)
            modelAnimator.runtimeAnimatorController = _sharedAnimatorController;

        // ルートのNetworkCharacterControllerで移動するため、モデル側のRoot Motionは無効。
        modelAnimator.applyRootMotion = false;

        Transform leftUpperArm = FindLeftUpperArm(modelInstance.transform);

        if (_playerMovement != null)
            _playerMovement.SetCharacterAnimator(modelAnimator);

        if (_emoteController != null)
            _emoteController.SetCharacterAnimator(modelAnimator);

        if (_bowlingArmController != null)
            _bowlingArmController.SetCharacterAnimator(modelAnimator, leftUpperArm);

        _appliedModelId = modelId;
        Debug.Log($"[CharacterModelSwitcher] モデルを切り替えました: {modelPrefab.name}", this);
    }

    private static Transform FindLeftUpperArm(Transform modelRoot)
    {
        foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Bip001 L UpperArm" ||
                child.name == "LeftUpperArm" ||
                child.name == "mixamorig:LeftArm")
                return child;
        }

        Debug.LogWarning("[CharacterModelSwitcher] 左腕ボーンが見つかりません。投球時の腕補正は無効になります。", modelRoot);
        return null;
    }
}
