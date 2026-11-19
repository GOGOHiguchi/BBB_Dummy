using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ロビーに配置したキャラクター選択オブジェクトへ近づき、Eキーで選択画面を開く。
/// このコンポーネントを付けたオブジェクトにはIs TriggerのColliderを設定する。
/// </summary>
public sealed class CharacterSelectInteractable : MonoBehaviour
{
    [SerializeField] private CharacterSelectUI selectionUI;

    private PlayerMovement _localPlayerInRange;

    private void Awake()
    {
        if (selectionUI == null)
            selectionUI = FindFirstObjectByType<CharacterSelectUI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null && player.HasInputAuthority)
            _localPlayerInRange = player;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null && player == _localPlayerInRange)
            _localPlayerInRange = null;
    }

    private void Update()
    {
        if (_localPlayerInRange == null || selectionUI == null || selectionUI.IsOpen)
            return;

        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        CharacterModelSwitcher switcher = _localPlayerInRange.GetComponent<CharacterModelSwitcher>();
        if (switcher == null)
        {
            Debug.LogWarning("[CharacterSelectInteractable] PlayerにCharacterModelSwitcherがありません。", _localPlayerInRange);
            return;
        }

        selectionUI.Open(switcher);
    }
}
