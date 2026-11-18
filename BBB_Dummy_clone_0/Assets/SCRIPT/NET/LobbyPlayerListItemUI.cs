using TMPro;
using UnityEngine;

public class LobbyPlayerListItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;

    /// <summary>
    /// プレイヤーリストのUIを更新します
    /// </summary>
    public void Setup(NetworkPlayerData data, bool isHost, bool isLocal)
    {
        if (data.Equals(default(NetworkPlayerData))) return;

        // 名前の決定
        string pName = string.IsNullOrEmpty(data.PlayerName.ToString()) ? $"Player {data.PlayerRef.RawEncoded}" : data.PlayerName.ToString();

        if (nameText != null)
        {
            nameText.text = $"{pName}";
        }
    }
}