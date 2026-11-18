using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ServerListItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text serverNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button joinButton;

    public void Setup(
        string serverName,
        int currentPlayers,
        int maxPlayers,
        Action onJoinClicked)
    {
        if (serverNameText != null)
            serverNameText.text = serverName;

        if (playerCountText != null)
            playerCountText.text = $"{currentPlayers} / {maxPlayers}";

        if (joinButton == null) return;

        joinButton.onClick.RemoveAllListeners();

        joinButton.interactable = true;

        joinButton.onClick.AddListener(() =>
        {
            if (joinButton != null)
                joinButton.interactable = false;

            onJoinClicked?.Invoke();
        });
    }
}