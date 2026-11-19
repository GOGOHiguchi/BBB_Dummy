using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ロビーに配置したキャラクター選択オブジェクトへ近づき、Eキーで選択画面を開く。
/// このコンポーネントを付けたオブジェクトにはIs TriggerのColliderを設定する。
/// </summary>
public sealed class CharacterSelectInteractable : MonoBehaviour
{
    [SerializeField] private CharacterSelectUI selectionUI;

    private void OnTriggerStay(Collider other)
    {
        if (selectionUI == null || selectionUI.IsOpen)
            return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.HasInputAuthority)
            return;

        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        CharacterModelSwitcher switcher = player.GetComponent<CharacterModelSwitcher>();
        if (switcher == null)
        {
            Debug.LogWarning("[CharacterSelectInteractable] PlayerにCharacterModelSwitcherがありません。", player);
            return;
        }

        selectionUI.Open(switcher);
    }
}
