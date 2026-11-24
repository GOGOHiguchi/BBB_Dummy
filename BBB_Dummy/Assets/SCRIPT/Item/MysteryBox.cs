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

    [Header("取得SE設定")]
    [SerializeField] private AudioClip breakSE;
    [Range(0f, 3f)] [SerializeField] private float breakSEVolume = 1.0f;
    [Tooltip("音声ファイルの冒頭の無音を飛ばす秒数")]
    [Range(0f, 3f)] [SerializeField] private float startOffset = 0.05f;

    [Networked] public NetworkBool IsActive { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }

    private bool _localBroken = false;

    public override void Spawned()
    {
        if (HasStateAuthority) IsActive = true;
        _localBroken = false;
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
        if (IsActive) _localBroken = false;
        if (visualRoot != null) visualRoot.SetActive(IsActive);
        if (boxCollider != null) boxCollider.enabled = IsActive;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsActive || _localBroken) return;
        if (dropItems == null || dropItems.Length == 0) return;
        if (!other.CompareTag("Player")) return;

        var player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        // 1. 自分の操作キャラが触れた瞬間、通信を待たずに即座にSEを再生する
        if (player.HasInputAuthority)
        {
            // アイテム欄に空きがあるか、同じアイテムがスタック可能か事前チェック
            bool canPickup = false;
            for (int i = 0; i < 3; i++)
            {
                if (player.ItemRests[i] == 0)
                {
                    canPickup = true;
                    break;
                }
                for (int j = 0; j < dropItems.Length; j++)
                {
                    if (player.ItemTypes[i] == dropItems[j].itemType)
                    {
                        canPickup = true;
                        break;
                    }
                }
            }

            if (canPickup)
            {
                _localBroken = true;
                PlayBreakSEImmediate();
            }
        }

        // 2. 実際のアイテム付与とボックス消去はホスト側で確定処理する
        if (!Runner.IsServer) return;

        int randomIndex = Random.Range(0, dropItems.Length);
        var selectedItem = dropItems[randomIndex];

        if (player.TryPickupItem(selectedItem.itemType, selectedItem.maxUses))
        {
            IsActive = false;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnTime);
        }
    }

    private void PlayBreakSEImmediate()
    {
        if (breakSE == null) return;

        // 冒頭無音スキップ（startOffset > 0）を使う場合は専用AudioSourceで頭出し再生
        if (startOffset > 0f && startOffset < breakSE.length)
        {
            GameObject tempAudioObj = new GameObject("TempBreakSE");
            AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
            tempSource.clip = breakSE;
            tempSource.time = startOffset;

            if (AudioManager.instance != null && AudioManager.instance.seAudioSource != null)
            {
                tempSource.outputAudioMixerGroup = AudioManager.instance.seAudioSource.outputAudioMixerGroup;
                tempSource.volume = AudioManager.instance.seAudioSource.volume * breakSEVolume;
            }
            else
            {
                tempSource.volume = breakSEVolume;
            }

            tempSource.Play();
            Destroy(tempAudioObj, (breakSE.length - startOffset) + 0.1f);
            return;
        }

        // startOffset == 0 の場合は従来通り PlayOneShot で再生
        if (AudioManager.instance != null && AudioManager.instance.seAudioSource != null)
        {
            AudioManager.instance.seAudioSource.PlayOneShot(breakSE, breakSEVolume);
        }
    }
}