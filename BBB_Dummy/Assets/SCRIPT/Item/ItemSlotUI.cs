using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro用

public class ItemSlotUI : MonoBehaviour
{
    [System.Serializable]
    public class ItemData
    {
        public string itemName;
        public Sprite itemIcon;
    }

    [System.Serializable]
    public class SlotUI
    {
        public GameObject slotRoot;
        public Image[] restIcons = new Image[6];
        
        [Header("アイテム可視化用")]
        public Image itemIconImage; // アイテム画像を表示するImage
        public TextMeshProUGUI itemNameText; // アイテム名を表示するText
    }

    [SerializeField] private SlotUI[] slots = new SlotUI[3];
    
    [Header("アイテムデータベース (Index 1から登録)")]
    // 要素0は空、要素1はスピードポーションといった具合に設定
    [SerializeField] private ItemData[] itemDatabase; 

    [Header("選択中のスロットの見た目設定")]
    [SerializeField] private float selectedScale = 1.1f;
    [SerializeField] private float normalScale = 1.0f;

    private bool debugLogged = false;

    private bool localFoundLogged = false;

    private void Update()
    {
        if (PlayerMovement.Local == null)
        {
            return;
        }

        if (!localFoundLogged)
        {
            Debug.Log("[ItemSlotUI] PlayerMovement.Localを取得しました。");
            localFoundLogged = true;
        }

        if (!debugLogged)
        {
            var player = PlayerMovement.Local;

            for (int i = 0; i < 3; i++)
            {
                Debug.Log(
                    $"[ItemSlotUI] Slot={i}, " +
                    $"Type={player.ItemTypes[i]}, " +
                    $"Rest={player.ItemRests[i]}");
            }

            debugLogged = true;
        }

        if (PlayerMovement.Local == null) return;

        int activeSlot = PlayerMovement.Local.LocalSelectedSlot;

        for (int i = 0; i < 3; i++)
        {
            if (slots[i].slotRoot != null)
            {
                float targetScale = (i == activeSlot) ? selectedScale : normalScale;
                slots[i].slotRoot.transform.localScale = Vector3.Lerp(
                    slots[i].slotRoot.transform.localScale, 
                    Vector3.one * targetScale, 
                    Time.deltaTime * 15f
                );
            }

            int restCount = PlayerMovement.Local.ItemRests[i];
            int itemType = PlayerMovement.Local.ItemTypes[i]; // アイテムの種類を取得
            int maxUses = PlayerMovement.Local.ItemMaxUses[i];

            // アイテムの画像と名前のUI更新
            if (restCount > 0 && itemType > 0 && itemType < itemDatabase.Length)
            {
                var data = itemDatabase[itemType];
                if (slots[i].itemIconImage != null)
                {
                    slots[i].itemIconImage.sprite = data.itemIcon;
                    slots[i].itemIconImage.enabled = true;
                }
                if (slots[i].itemNameText != null)
                {
                    slots[i].itemNameText.text = (i == activeSlot) ? data.itemName : "";
                }
            }
            else
            {
                if (slots[i].itemIconImage != null) slots[i].itemIconImage.enabled = false;
                if (slots[i].itemNameText != null) slots[i].itemNameText.text = "";
            }

            // 画像非表示・残回数表示ロジック（変更なし）
            foreach (var icon in slots[i].restIcons)
            {
                if (icon != null)
                    icon.enabled = false;
            }

            if (maxUses == 1 && restCount >= 1)
            {
                SetRestIcon(slots[i], 0);
            }
            else if (maxUses == 2)
            {
                if (restCount >= 1) SetRestIcon(slots[i], 1);
                if (restCount >= 2) SetRestIcon(slots[i], 2);
            }
            else if (maxUses >= 3)
            {
                if (restCount >= 1) SetRestIcon(slots[i], 3);
                if (restCount >= 2) SetRestIcon(slots[i], 4);
                if (restCount >= 3) SetRestIcon(slots[i], 5);
            }
        }
    }

    private void SetRestIcon(SlotUI slot, int index)
    {
        if (slot.restIcons != null &&
            index >= 0 &&
            index < slot.restIcons.Length &&
            slot.restIcons[index] != null)
        {
            slot.restIcons[index].enabled = true;
        }
    }
}