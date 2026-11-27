using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LobbyMenuUI : NetworkBehaviour
{
    [Header("--- メニューパネル設定 ---")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject battleSettingsPanel;

    [Header("--- メニューボタン ---")]
    [SerializeField] private Button resumeButton;         // 再開
    [SerializeField] private Button battleSettingsButton; // 対戦設定
    [SerializeField] private Button optionsButton;        // オプション（未実装枠）
    [SerializeField] private Button quitButton;           // ゲーム終了

    [Header("--- メニュー画面 プレイヤーリストUI ---")]
    [SerializeField] private GameObject pListPrefab;      // PListプレハブ (LobbyPlayerListItemUIをアタッチ)
    [SerializeField] private Transform pListContent;      // PListを並べる親オブジェクト

    [Header("--- 対戦設定パネル UI ---")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Slider maxPlayersSlider; 
    [SerializeField] private TMP_Text maxPlayersText;
    [SerializeField] private TMP_Dropdown privacyDropdown;// 0: Private, 1: Public
    [SerializeField] private TMP_Text roomPasswordText;
    [SerializeField] private Button copyPasswordButton;

    [Header("--- 対戦設定画面 プレイヤー/観客リストUI ---")]
    [SerializeField] private GameObject sPListPrefab;          // S_Plistプレハブ (LobbyPlayerListItemUIをアタッチ)
    [SerializeField] private Transform sPListPlayerContent;    // プレイヤーを並べる親
    [SerializeField] private Transform sPListSpectatorContent; // 観客を並べる親

    private CourseSettingsTabs courseSettingsTabs;
    private bool isMenuOpen = false;
    private string currentJoinCode = "";

    public override void Spawned()
    {
        menuPanel.SetActive(false);
        battleSettingsPanel.SetActive(false);
        courseSettingsTabs = FindFirstObjectByType<CourseSettingsTabs>();
        if (courseSettingsTabs != null) courseSettingsTabs.SetTabButtonsVisible(false);

        SetupButtons();
        UpdatePlayerLists();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleEscapeKey();
        }

        if (isMenuOpen && Runner != null && Runner.IsRunning)
        {
            UpdatePlayerLists();
        }
    }

    private void HandleEscapeKey()
    {
        if (battleSettingsPanel.activeSelf)
        {
            // 対戦設定画面が開いている場合 -> 変更を適用して閉じる（メニュー画面は開いたまま）
            ApplySessionSettings();
            battleSettingsPanel.SetActive(false);
        }
        else if (menuPanel.activeSelf)
        {
            // メニュー画面が開いている場合 -> メニューを閉じる
            isMenuOpen = false;
            menuPanel.SetActive(false);
            if (courseSettingsTabs != null) courseSettingsTabs.SetTabButtonsVisible(false);
        }
        else
        {
            // どちらも開いていない場合 -> メニュー画面を開く
            isMenuOpen = true;
            menuPanel.SetActive(true);
            LoadCurrentSessionSettings();
        }
    }

    private string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] stringChars = new char[6];
        var random = new System.Random();
        for (int i = 0; i < stringChars.Length; i++)
        {
            stringChars[i] = chars[random.Next(chars.Length)];
        }
        return new string(stringChars);
    }

    private void SetupButtons()
    {
        resumeButton.onClick.AddListener(() =>
        {
            isMenuOpen = false;
            menuPanel.SetActive(false);
            battleSettingsPanel.SetActive(false); // 念のためこちらも閉じる
            if (courseSettingsTabs != null) courseSettingsTabs.SetTabButtonsVisible(false);
        });
        
        battleSettingsButton.onClick.AddListener(() =>
        {
            // 【修正ポイント】menuPanel は親なので閉じない。上に重ねるだけ。
            battleSettingsPanel.SetActive(true);
            if (courseSettingsTabs != null) courseSettingsTabs.SetTabButtonsVisible(true);
            LoadCurrentSessionSettings();
        });

        quitButton.onClick.AddListener(QuitGame);

        copyPasswordButton.onClick.AddListener(() =>
        {
            if (!string.IsNullOrEmpty(currentJoinCode))
            {
                GUIUtility.systemCopyBuffer = currentJoinCode;
                Debug.Log("パスワードをクリップボードにコピーしました: " + currentJoinCode);
            }
        });

        if (maxPlayersSlider != null)
        {
            maxPlayersSlider.onValueChanged.AddListener(val => 
            {
                if (maxPlayersText != null) maxPlayersText.text = val.ToString();
            });
        }

        if (privacyDropdown != null)
        {
            privacyDropdown.onValueChanged.AddListener(val => 
            {
                if (val == 0) // Private
                {
                    if (string.IsNullOrEmpty(currentJoinCode))
                    {
                        currentJoinCode = GenerateJoinCode();
                    }
                    if (roomPasswordText != null) roomPasswordText.text = currentJoinCode;
                }
                else // Public
                {
                    currentJoinCode = "";
                    if (roomPasswordText != null) roomPasswordText.text = "なし";
                }
            });
        }
    }

    private void LoadCurrentSessionSettings()
    {
        if (Runner == null || !Runner.IsRunning || Runner.SessionInfo == null) return;

        var props = Runner.SessionInfo.Properties;

        if (props.TryGetValue(MatchmakingKeys.SERVER_NAME, out var serverNameProp))
            roomNameInput.text = serverNameProp.PropertyValue != null ? serverNameProp.PropertyValue.ToString() : Runner.SessionInfo.Name;

        if (maxPlayersSlider != null)
        {
            maxPlayersSlider.value = Runner.SessionInfo.MaxPlayers;
            if (maxPlayersText != null) maxPlayersText.text = Runner.SessionInfo.MaxPlayers.ToString();
        }

        if (props.TryGetValue(MatchmakingKeys.IS_PRIVATE, out var isPrivateProp))
        {
            bool isPrivate = isPrivateProp.PropertyValue != null && (bool)isPrivateProp.PropertyValue;
            privacyDropdown.value = isPrivate ? 0 : 1;
        }

        if (props.TryGetValue(MatchmakingKeys.JOIN_CODE, out var joinCodeProp))
        {
            currentJoinCode = joinCodeProp.PropertyValue != null ? joinCodeProp.PropertyValue.ToString() : "";
            roomPasswordText.text = string.IsNullOrEmpty(currentJoinCode) ? "なし" : currentJoinCode;
        }

        bool isHost = Runner.IsServer;
        roomNameInput.interactable = isHost;
        if (maxPlayersSlider != null) maxPlayersSlider.interactable = isHost;
        privacyDropdown.interactable = isHost;
    }

    private void ApplySessionSettings()
    {
        if (Runner == null || !Runner.IsServer || Runner.SessionInfo == null) return;

        bool isPrivate = (privacyDropdown.value == 0);

        var newProps = new Dictionary<string, SessionProperty>
        {
            { MatchmakingKeys.SERVER_NAME, roomNameInput.text },
            { MatchmakingKeys.IS_PRIVATE, isPrivate },
            { MatchmakingKeys.JOIN_CODE, isPrivate ? currentJoinCode : "" },
            { MatchmakingKeys.IS_IN_GAME, false }
        };

        Runner.SessionInfo.UpdateCustomProperties(newProps);
    }

    private void UpdatePlayerLists()
    {
        if (Runner == null || !Runner.IsRunning) return;

        ClearChildren(pListContent);
        ClearChildren(sPListPlayerContent);
        ClearChildren(sPListSpectatorContent);

        // アクティブなプレイヤーを取得し、PlayerIdが若い順（ホストが先頭にくる想定）にソート
        List<PlayerRef> sortedPlayers = Runner.ActivePlayers.ToList();
        sortedPlayers.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));

        PlayerRef hostRef = sortedPlayers.Count > 0 ? sortedPlayers[0] : PlayerRef.None;

        foreach (PlayerRef player in sortedPlayers)
        {
            // --- 【修正解決】BasicSpawnerで登録されたプレイヤーオブジェクトを取得 ---
            NetworkObject playerObject = Runner.GetPlayerObject(player);
            if (playerObject == null) continue; // まだオブジェクトが生成されていない場合はスキップ

            // プレイヤーの移動・状態管理コンポーネントを取得
            PlayerMovement playerMovement = playerObject.GetComponent<PlayerMovement>();
            if (playerMovement == null) continue;

            // プレイヤーコンポーネントから、ステップ1で追加した NetworkPlayerData 構造体を取得
            NetworkPlayerData data = playerMovement.PlayerData;

            // 構造体がデフォルト（未初期化）状態であっても、PlayerRefさえ割り振られていればリストに表示する
            if (data.Equals(default(NetworkPlayerData)))
            {
                // 初期化されていない場合は最低限の情報を仮生成してUIに渡す
                data.PlayerRef = player;
            }
            // ------------------------------------------------------------------

            bool isHost = (player == hostRef); 
            bool isLocal = (player == Runner.LocalPlayer);

            // メニュー画面のリスト生成
            if (pListPrefab != null && pListContent != null)
            {
                GameObject pListItem = Instantiate(pListPrefab, pListContent);
                var uiScript = pListItem.GetComponent<LobbyPlayerListItemUI>();
                if (uiScript != null) uiScript.Setup(data, isHost, isLocal);
            }

            // 対戦設定画面のリスト生成
            if (sPListPrefab != null && sPListPlayerContent != null)
            {
                GameObject sPListItem = Instantiate(sPListPrefab, sPListPlayerContent);
                var uiScript = sPListItem.GetComponent<LobbyPlayerListItemUI>();
                if (uiScript != null) uiScript.Setup(data, isHost, isLocal);
            }
        }
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }
    }

    private async void QuitGame()
    {
        NetworkRunner runner = Runner;

        // 終了前にホストかどうかを記録しておく
        bool isHost = runner != null && runner.IsServer;

        if (runner != null && !runner.IsShutdown)
        {
            await runner.Shutdown(destroyGameObject: true);
        }

        // ホスト自身はここでタイトルへ戻る
        // クライアントはBasicSpawnerの切断コールバックで戻る
        if (isHost || runner == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("1_Title");
        }
    }
}