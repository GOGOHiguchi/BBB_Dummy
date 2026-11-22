using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public sealed class CharacterSelectInteractable : MonoBehaviour
{
    [SerializeField] private CharacterSelectUI selectionUI;

    private PlayerMovement localPlayer;
    // プレイヤーに複数Colliderがある場合、1つのExitで範囲内状態を解除しない。
    private readonly HashSet<Collider> localPlayerColliders = new HashSet<Collider>();
    private Collider triggerCollider;

    public static bool IsLocalPlayerInRange { get; private set; }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        if (selectionUI == null)
            selectionUI = FindFirstObjectByType<CharacterSelectUI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.HasInputAuthority)
            return;

        if (localPlayer != null && localPlayer != player)
            return;

        localPlayer = player;
        localPlayerColliders.Add(other);
        IsLocalPlayerInRange = localPlayerColliders.Count > 0;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!localPlayerColliders.Remove(other))
            return;

        // 全Colliderが退出したときだけ範囲外にする。
        if (localPlayerColliders.Count == 0)
            ClearLocalPlayer();
    }

    private void Update()
    {
        // Collider無効化やプレイヤー破棄でExitが来なかった場合の保険。
        localPlayerColliders.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);

        if (localPlayer == null || localPlayerColliders.Count == 0)
        {
            ClearLocalPlayer();
            return;
        }

        if (selectionUI == null || selectionUI.IsOpen)
            return;

        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        // Exitイベントが取りこぼされても、現在のプレイヤーColliderがTriggerと重なっている
        // ことを再確認し、古い参照による遠距離からのUI再オープンを防ぐ。
        bool stillOverlapping = triggerCollider != null &&
            localPlayerColliders.Exists(collider => triggerCollider.bounds.Intersects(collider.bounds));

        if (!stillOverlapping)
        {
            ClearLocalPlayer();
            return;
        }

        CharacterModelSwitcher switcher = localPlayer.GetComponent<CharacterModelSwitcher>();
        if (switcher != null)
            selectionUI.Open(switcher);
        else
            Debug.LogWarning("[CharacterSelectInteractable] PlayerにCharacterModelSwitcherがありません。", localPlayer);
    }

    private void ClearLocalPlayer()
    {
        localPlayerColliders.Clear();
        localPlayer = null;
        IsLocalPlayerInRange = false;
    }

    private void OnDisable()
    {
        ClearLocalPlayer();
    }
}

// HashSetにColliderの存在条件を適用する小さな拡張。
internal static class CharacterSelectColliderSetExtensions
{
    public static bool Exists(this HashSet<Collider> set, System.Func<Collider, bool> predicate)
    {
        foreach (Collider collider in set)
        {
            if (collider != null && predicate(collider))
                return true;
        }
        return false;
    }
}
