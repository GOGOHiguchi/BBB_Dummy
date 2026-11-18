using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : NetworkBehaviour
{
    [Header("UI参照")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text statusText;

    [Header("ゲームマネージャー設定")]
    [Tooltip("試合へ遷移する際にスポーンさせるGameManagerのプレハブ")]
    [SerializeField] private NetworkPrefabRef gameManagerPrefab;

    private PlayerMovement localPlayer;

    public override void Spawned()
    {
        if (startButton != null)
        {
            startButton.gameObject.SetActive(Runner.IsServer);
            startButton.onClick.AddListener(OnStartGameClicked);
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyClicked);
        }

        FindLocalPlayer();
    }

    private void FindLocalPlayer()
    {
        if (Object == null || !Object.IsValid || Runner == null || Runner.LocalPlayer == PlayerRef.None) return;

        NetworkObject playerObject = Runner.GetPlayerObject(Runner.LocalPlayer);

        if (playerObject != null)
        {
            localPlayer = playerObject.GetComponent<PlayerMovement>();
        }
    }

    private void OnReadyClicked()
    {
        if (localPlayer == null) FindLocalPlayer();
        if (localPlayer == null) return;

        localPlayer.RpcSetReady(!localPlayer.IsReady);
        UpdateReadyUI();
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid || Runner == null || !Runner.IsRunning) return;
        UpdateReadyUI();
    }

    private void UpdateReadyUI()
    {
        if (Runner == null || !Runner.IsRunning) return;

        if (localPlayer == null || localPlayer.Object == null || !localPlayer.Object.IsValid)
        {
            FindLocalPlayer();
            return;
        }

        if (!localPlayer.Object.IsValid || !localPlayer.IsPlayerSpawned) return;

        if (statusText != null)
        {
            statusText.text = localPlayer.IsReady ? "READY!" : "NOT READY";
        }

        if (startButton != null && Runner.IsServer)
        {
            startButton.interactable = AreAllPlayersReady();
        }
    }

    private bool AreAllPlayersReady()
    {
        if (Runner == null || !Runner.IsRunning) return false;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            NetworkObject playerObject = Runner.GetPlayerObject(player);
            if (playerObject == null || !playerObject.IsValid) return false;

            PlayerMovement playerMovement = playerObject.GetComponent<PlayerMovement>();
            if (playerMovement == null || playerMovement.Object == null || !playerMovement.Object.IsValid) return false;
            if (!playerMovement.IsPlayerSpawned || !playerMovement.IsReady) return false;
        }

        return true;
    }

    private void OnStartGameClicked()
    {
        if (Runner == null || !Runner.IsServer) return;
        if (!AreAllPlayersReady()) return;

        // ロビー用のプレイヤーオブジェクトを破棄
        foreach (var player in Runner.ActivePlayers)
        {
            var playerObj = Runner.GetPlayerObject(player);
            if (playerObj != null)
            {
                Runner.Despawn(playerObj);
                Runner.SetPlayerObject(player, null);
            }
        }

        // ★ GameManagerをスポーンして、進行を丸投げする
        if (gameManagerPrefab.IsValid)
        {
            NetworkObject gmObj = Runner.Spawn(gameManagerPrefab);
            GameManager gm = gmObj.GetComponent<GameManager>();
            if (gm != null)
            {
                gm.StartTournament();
            }
        }
        else
        {
            Debug.LogError("GameManagerプレハブが設定されていません。");
        }
    }
}