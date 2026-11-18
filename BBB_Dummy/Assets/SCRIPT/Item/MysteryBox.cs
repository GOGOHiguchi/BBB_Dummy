using Fusion;
using UnityEngine;

public class MysteryBox : NetworkBehaviour
{
    // アイテムIDと使用回数をセットで管理するための構造体
    [System.Serializable]
    public struct DropItemData
    {
        public int itemType;
        public int maxUses;
    }

    [Header("アイテム抽選設定")]
    [Tooltip("ハテナボックスから出るアイテムの候補とその使用回数を設定します")]
    [SerializeField] private DropItemData[] dropItems;

    [Header("復活設定")]
    [SerializeField] private float respawnTime = 5f;

    [Header("オブジェクト参照")]
    [SerializeField] private GameObject visualRoot; 
    [SerializeField] private Collider boxCollider;

    [Networked] public NetworkBool IsActive { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }

    public override void Spawned()
    {
        if (HasStateAuthority) IsActive = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && !IsActive)
        {
            if (RespawnTimer.Expired(Runner))
            {
                IsActive = true;
                RespawnTimer = TickTimer.None;
            }
        }
    }

    public override void Render()
    {
        if (visualRoot != null) visualRoot.SetActive(IsActive);
        if (boxCollider != null) boxCollider.enabled = IsActive;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Runner.IsServer) return;
        if (!IsActive) return;
        if (dropItems == null || dropItems.Length == 0) return;

        if (!other.CompareTag("Player")) return;

        var player = other.GetComponentInParent<PlayerMovement>();
        if (player != null)
        {
            // 登録された配列の中からランダムに1つ選ぶ
            int randomIndex = Random.Range(0, dropItems.Length);
            var selectedItem = dropItems[randomIndex];
            
            // 選ばれたアイテム固有の使用回数を渡す
            if (player.TryPickupItem(selectedItem.itemType, selectedItem.maxUses))
            {
                IsActive = false;
                RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnTime);
            }
        }
    }
}