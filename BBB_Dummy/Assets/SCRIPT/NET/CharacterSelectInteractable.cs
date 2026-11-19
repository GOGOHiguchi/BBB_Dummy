using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public sealed class CharacterSelectInteractable : MonoBehaviour
{
    [SerializeField] private CharacterSelectUI selectionUI;
    private PlayerMovement localPlayer;

    public static bool IsLocalPlayerInRange { get; private set; }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (selectionUI == null) selectionUI = FindFirstObjectByType<CharacterSelectUI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.HasInputAuthority) return;
        localPlayer = player;
        IsLocalPlayerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || player != localPlayer) return;
        localPlayer = null;
        IsLocalPlayerInRange = false;
    }

    private void Update()
    {
        if (localPlayer == null || selectionUI == null || selectionUI.IsOpen) return;
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        var switcher = localPlayer.GetComponent<CharacterModelSwitcher>();
        if (switcher != null) selectionUI.Open(switcher);
        else Debug.LogWarning("[CharacterSelectInteractable] PlayerにCharacterModelSwitcherがありません。", localPlayer);
    }

    private void OnDisable()
    {
        if (localPlayer != null) IsLocalPlayerInRange = false;
        localPlayer = null;
    }
}
