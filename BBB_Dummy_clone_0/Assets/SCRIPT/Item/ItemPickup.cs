using Fusion;
using UnityEngine;

public class ItemPickup : NetworkBehaviour
{
    [Header("アイテム設定")]
    [SerializeField] private int itemType = 1; // アイテムごとの固有ID
    [SerializeField] private int maxUses = 1;  // このアイテムの最大使用回数

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var player = other.GetComponentInParent<PlayerMovement>();
        if (player != null && (player.HasInputAuthority || Runner.IsServer))
        {
            Rpc_RequestPickup(player, itemType, maxUses);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestPickup(PlayerMovement player, int type, int uses)
    {
        if (player != null && player.TryPickupItem(type, uses))
        {
            Runner.Despawn(Object);
        }
    }
}