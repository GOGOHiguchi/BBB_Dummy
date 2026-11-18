using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleMenuUI : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("--- 画面パネル設定 ---")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject createServerPanel;
    [SerializeField] private GameObject searchMenuPanel;
    [SerializeField] private GameObject publicSearchPanel;
    [SerializeField] private GameObject privateSearchPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("--- メインメニュー ボタン ---")]
    [SerializeField] private Button mainCreateButton;
    [SerializeField] private Button mainSearchButton;
    [SerializeField] private Button mainSettingsButton;
    [SerializeField] private Button mainQuitButton;

    [Header("--- 検索選択パネル ボタン ---")]
    [SerializeField] private Button selectPublicSearchButton;
    [SerializeField] private Button selectPrivateSearchButton;
    [SerializeField] private Button selectFriendSearchButton;

    [Header("--- ネットワーク設定 ---")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    [Header("【サーバーを作る】入力要素")]
    [SerializeField] private TMP_InputField createPlayerNameInput;
    [SerializeField] private TMP_InputField serverNameInput;
    [SerializeField] private TMP_InputField maxPlayersInput;
    [SerializeField] private Toggle isPrivateToggle;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private Button executeCreateRoomButton;

    [Header("【公開サーバー検索】入力要素")]
    [SerializeField] private TMP_InputField publicPlayerNameInput;
    [SerializeField] private Toggle includeInGamePublicToggle;
    [SerializeField] private Button searchPublicButton;
    [SerializeField] private Transform publicListContent;

    [Header("【プライベートサーバー検索】入力要素")]
    [SerializeField] private TMP_InputField privatePlayerNameInput;
    [SerializeField] private TMP_InputField searchJoinCodeInput;
    [SerializeField] private Toggle includeInGamePrivateToggle;
    [SerializeField] private Button searchPrivateButton;
    [SerializeField] private Transform privateListContent;

    [Header("確認ポップアップ")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private TMP_Text confirmationMessageText;
    [SerializeField] private Button confirmationYesButton;
    [SerializeField] private Button confirmationNoButton;

    [Header("エラー通知")]
    [SerializeField] private TMP_Text errorMessageText;
    [SerializeField] private float errorMessageDuration = 3f;

    [Header("プレハブ")]
    [SerializeField] private GameObject serverListItemPrefab;

    private NetworkRunner _runner;
    private string generatedJoinCode;
    private List<SessionInfo> cachedSessionList = new List<SessionInfo>();
    private Stack<GameObject> panelHistory = new Stack<GameObject>();
    private GameObject currentPanel;
    private bool _isLobbyJoined = false;
    private bool _isConnectingToLobby = false;
    private bool _isStartingGame = false;
    private enum ConfirmationAction
    {
        None,
        CreateRoom,
        JoinSession
    }

    private ConfirmationAction pendingAction = ConfirmationAction.None;
    private SessionInfo pendingSession;
    private Coroutine errorMessageCoroutine;

    private void Start()
    {
        // 初期状態の画面セットアップ
        InitializePanels();

        // ボタン・入力フィールドイベントの追加
        SetupButtonListeners();
        SetupInputFieldValidations();

        SetupPlayerNameSaving();

        // 参加コード生成＆Fusionロビー接続
        GenerateNewJoinCode();
        _ = ConnectToFusionLobbyAsync();

        if (confirmationPanel != null)
        confirmationPanel.SetActive(false);

        if (errorMessageText != null)
            errorMessageText.gameObject.SetActive(false);

        if (confirmationYesButton != null)
            confirmationYesButton.onClick.AddListener(OnConfirmationYesClicked);

        if (confirmationNoButton != null)
            confirmationNoButton.onClick.AddListener(CloseConfirmation);
    }

    private void Update()
    {
        // Escapeキーが押されたら1つ前のパネルに戻る
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            BackToPreviousPanel();
        }
    }

    #region パネル遷移システム

    private void InitializePanels()
    {
        // 全パネルを非表示にしてメインパネルのみ表示
        if (createServerPanel) createServerPanel.SetActive(false);
        if (searchMenuPanel) searchMenuPanel.SetActive(false);
        if (publicSearchPanel) publicSearchPanel.SetActive(false);
        if (privateSearchPanel) privateSearchPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);

        if (mainMenuPanel)
        {
            mainMenuPanel.SetActive(true);
            currentPanel = mainMenuPanel;
        }
    }

    // 指定したパネルを開く
    public void OpenPanel(GameObject targetPanel)
    {
        if (targetPanel == null || targetPanel == currentPanel) return;

        if (currentPanel != null)
        {
            currentPanel.SetActive(false);
            panelHistory.Push(currentPanel); // 履歴に保存
        }

        targetPanel.SetActive(true);
        currentPanel = targetPanel;
    }

    // 1つ前のパネルに戻る
    public void BackToPreviousPanel()
    {
        // 有効なパネルが見つかるまで履歴を取り出す
        while (panelHistory.Count > 0)
        {
            GameObject previousPanel = panelHistory.Pop();

            // 既に破棄されているパネルは無視
            if (previousPanel == null)
                continue;

            if (currentPanel != null)
                currentPanel.SetActive(false);

            previousPanel.SetActive(true);
            currentPanel = previousPanel;
            return;
        }

        // 履歴が空、または有効なパネルがない場合はメインメニューへ
        if (mainMenuPanel != null)
        {
            if (currentPanel != null)
                currentPanel.SetActive(false);

            mainMenuPanel.SetActive(true);
            currentPanel = mainMenuPanel;
        }
    }


    private void SetupButtonListeners()
    {
        // メインメニュー
        if (mainCreateButton) mainCreateButton.onClick.AddListener(() => OpenPanel(createServerPanel));
        if (mainSearchButton) mainSearchButton.onClick.AddListener(() => {
            OpenPanel(searchMenuPanel);
        });
        if (mainSettingsButton) mainSettingsButton.onClick.AddListener(() => OpenPanel(settingsPanel));
        if (mainQuitButton) mainQuitButton.onClick.AddListener(QuitGame);

        // 検索選択メニュー
        if (selectPublicSearchButton) selectPublicSearchButton.onClick.AddListener(() => {
            OpenPanel(publicSearchPanel);
            UpdateServerListUI();
        });
        if (selectPrivateSearchButton) selectPrivateSearchButton.onClick.AddListener(() => {
            OpenPanel(privateSearchPanel);
            UpdateServerListUI();
        });
        if (selectFriendSearchButton) selectFriendSearchButton.interactable = false;

        // ルーム作成・検索実行ボタン（検索ボタンを押した時はロビーへ再接続して更新）
        if (executeCreateRoomButton) executeCreateRoomButton.onClick.AddListener(ShowCreateRoomConfirmation);
        if (searchPublicButton) searchPublicButton.onClick.AddListener(UpdateServerListUI);
        if (searchPrivateButton) searchPrivateButton.onClick.AddListener(UpdateServerListUI);
    }

    private void SetupInputFieldValidations()
    {
        if (maxPlayersInput != null)
        {
            // 初期設定で初期値をセット（未入力なら4）
            if (string.IsNullOrEmpty(maxPlayersInput.text))
            {
                maxPlayersInput.text = "4";
            }

            // 入力中: 半角英数字以外の文字を除外（日本語入力中の変換を妨げないよう文字確定・変化時にフィルタリング）
            maxPlayersInput.onValueChanged.AddListener(OnMaxPlayersValueChanged);

            // 入力確定時: 2〜10の範囲内にクランプ（補正）
            maxPlayersInput.onEndEdit.AddListener(OnMaxPlayersEndEdit);
        }
    }

    private void OnMaxPlayersValueChanged(string input)
    {
        // 半角英数字以外を除去（ひらがな・全角記号・全角数字などを弾く）
        string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9]", "");
        if (sanitized != input)
        {
            maxPlayersInput.text = sanitized;
        }
    }

    private void OnMaxPlayersEndEdit(string input)
    {
        if (int.TryParse(input, out int value))
        {
            // 2〜10の範囲に数値を限定
            int clampedValue = Mathf.Clamp(value, 2, 10);
            maxPlayersInput.text = clampedValue.ToString();
        }
        else
        {
            // 数値以外の入力（空欄やアルファベットのみ等）の場合はデフォルトで4を設定
            maxPlayersInput.text = "4";
        }
    }

    private void QuitGame()
    {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            Application.Quit();
    #endif
    }

    #endregion

    #region Fusion マッチメイキング処理

    private void GenerateNewJoinCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] stringChars = new char[6];
        var random = new System.Random();
        for (int i = 0; i < stringChars.Length; i++)
        {
            stringChars[i] = chars[random.Next(chars.Length)];
        }
        generatedJoinCode = new string(stringChars);
        if (joinCodeText != null) joinCodeText.text = generatedJoinCode;
    }

    private async Task<NetworkRunner> ResetAndCreateRunner()
    {
        _isLobbyJoined = false;
        if (_runner != null)
            {
                // 既にロビーに入っている場合は重複処理を避ける
                if (_runner.IsCloudReady || !_runner.IsShutdown)
                {
                    await _runner.Shutdown(destroyGameObject: true);
                    
                    // 1フレーム待機して完全に破棄されるのを保証する
                    await Task.Yield();
                }
                _runner = null;
            }

        // 専用のGameObjectを新規作成してRunnerをアタッチ
        GameObject runnerGO = new GameObject("FusionNetworkRunner");
        DontDestroyOnLoad(runnerGO);

        _runner = runnerGO.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        return _runner;
    }

    private async Task ConnectToFusionLobbyAsync()
    {
        // 接続処理中なら重複実行しない
        if (_isConnectingToLobby)
            return;

        // すでにロビーに接続しているなら再接続しない
        if (_runner != null &&
            _runner.IsRunning &&
            _isLobbyJoined)
        {
            UpdateServerListUI();
            return;
        }

        _isConnectingToLobby = true;

        if (searchPublicButton != null)
            searchPublicButton.interactable = false;

        if (searchPrivateButton != null)
            searchPrivateButton.interactable = false;

        try
        {
            if (_runner == null || !_runner.IsRunning)
            {
                await ResetAndCreateRunner();
            }

            var result = await _runner.JoinSessionLobby(
                SessionLobby.Custom,
                "BBB_DefaultLobby"
            );

            if (!result.Ok)
            {
                Debug.LogError(
                    $"ロビー接続失敗: {result.ShutdownReason}"
                );

                return;
            }

            _isLobbyJoined = true;

            Debug.Log("Fusionロビー接続成功");
        }
        catch (Exception e)
        {
            Debug.LogError($"ロビー接続エラー: {e}");
        }
        finally
        {
            _isConnectingToLobby = false;

            if (searchPublicButton != null)
                searchPublicButton.interactable = true;

            if (searchPrivateButton != null)
                searchPrivateButton.interactable = true;
        }
    }

    
    private void ShowCreateRoomConfirmation()
    {
        pendingAction = ConfirmationAction.CreateRoom;

        if (confirmationMessageText != null)
        {
            confirmationMessageText.text =
                "この内容でサーバーを作成しますか？";
        }

        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    private void ShowJoinConfirmation(
        string serverName,
        SessionInfo session)
    {
        pendingAction = ConfirmationAction.JoinSession;
        pendingSession = session;

        if (confirmationMessageText != null)
        {
            confirmationMessageText.text =
                $"「{serverName}」に参加しますか？";
        }

        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    private void CloseConfirmation()
    {
        pendingAction = ConfirmationAction.None;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        
        UpdateServerListUI();
    }

    private async void OnConfirmationYesClicked()
    {
        if (_isStartingGame)
            return;

        ConfirmationAction action = pendingAction;
        SessionInfo session = pendingSession;

        // 二重実行防止
        pendingAction = ConfirmationAction.None;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        switch (action)
        {
            case ConfirmationAction.CreateRoom:
                OnCreateRoomClicked();
                break;

            case ConfirmationAction.JoinSession:
                LoadingUI.Instance?.Show("サーバー参加中");
                await JoinSelectedSession(session);
                break;
        }
    }


    private async void OnCreateRoomClicked()
    {
        if (_isStartingGame)
            return;

        _isStartingGame = true;

        if (executeCreateRoomButton != null)
            executeCreateRoomButton.interactable = false;

        bool success = false;
        LoadingUI.Instance?.Show("サーバー作成中");

        try
        {
            string playerName = string.IsNullOrEmpty(createPlayerNameInput.text)
                ? "Player"
                : createPlayerNameInput.text;

            string serverName = string.IsNullOrEmpty(serverNameInput.text)
                ? $"{playerName}'s Room"
                : serverNameInput.text;

            int maxPlayers = 4;

            if (int.TryParse(maxPlayersInput.text, out int parsedMax))
                maxPlayers = Mathf.Clamp(parsedMax, 2, 10);

            maxPlayersInput.text = maxPlayers.ToString();

            bool isPrivate = isPrivateToggle.isOn;

            var customProps = new Dictionary<string, SessionProperty>()
            {
                { MatchmakingKeys.SERVER_NAME, serverName },
                { MatchmakingKeys.IS_PRIVATE, isPrivate },
                { MatchmakingKeys.JOIN_CODE, isPrivate ? generatedJoinCode : "" },
                { MatchmakingKeys.IS_IN_GAME, false }
            };

            await ResetAndCreateRunner();

            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(
                SceneRef.FromIndex(
                    SceneUtility.GetBuildIndexByScenePath(lobbySceneName)
                )
            );

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = Guid.NewGuid().ToString(),
                CustomLobbyName = "BBB_DefaultLobby",
                PlayerCount = maxPlayers,
                SessionProperties = customProps,
                Scene = sceneInfo,
                SceneManager = _runner.gameObject
                    .AddComponent<NetworkSceneManagerDefault>()
            };

            var startResult = await _runner.StartGame(startGameArgs);

            if (!startResult.Ok)
            {
                LoadingUI.Instance?.CompleteAndHide();
                Debug.LogError($"ルーム作成失敗: {startResult.ShutdownReason}");

                ShowErrorMessage("サーバー作成に失敗しました。");

                await RecoverToLobbyAsync();
                return;
            }
            LoadingUI.Instance?.CompleteAndHide();

            Debug.Log("ルーム作成成功");

            success = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"ルーム作成中に例外が発生: {e}");
            LoadingUI.Instance?.CompleteAndHide();

            ShowErrorMessage("サーバー作成に失敗しました。");

            await RecoverToLobbyAsync();
        }
        finally
        {
            if (!success)
            {
                _isStartingGame = false;

                if (executeCreateRoomButton != null)
                    executeCreateRoomButton.interactable = true;
            }
        }
    }


    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        cachedSessionList = sessionList;
        UpdateServerListUI();
    }

    private void UpdateServerListUI()
    {
        ClearListContent(publicListContent);
        ClearListContent(privateListContent);

        foreach (var session in cachedSessionList)
        {
            if (!session.IsValid || !session.IsVisible) continue;

            session.Properties.TryGetValue(MatchmakingKeys.SERVER_NAME, out var serverNameProp);
            session.Properties.TryGetValue(MatchmakingKeys.IS_PRIVATE, out var isPrivateProp);
            session.Properties.TryGetValue(MatchmakingKeys.JOIN_CODE, out var joinCodeProp);
            session.Properties.TryGetValue(MatchmakingKeys.IS_IN_GAME, out var isInGameProp);

            string serverName = serverNameProp.PropertyValue != null ? serverNameProp.PropertyValue.ToString() : session.Name;
            bool isPrivate = isPrivateProp.PropertyValue != null && (bool)isPrivateProp.PropertyValue;
            string joinCode = joinCodeProp.PropertyValue != null ? joinCodeProp.PropertyValue.ToString() : "";
            bool isInGame = isInGameProp.PropertyValue != null && (bool)isInGameProp.PropertyValue;

            if (!isPrivate)
            {
                if (!includeInGamePublicToggle.isOn && isInGame) continue;
                CreateListItem(publicListContent, serverName, session);
            }
            else
            {
                if (!includeInGamePrivateToggle.isOn && isInGame) continue;
                string inputCode = searchJoinCodeInput.text.Trim().ToUpper();
                if (!string.IsNullOrEmpty(inputCode) && joinCode.Equals(inputCode, StringComparison.OrdinalIgnoreCase))
                {
                    CreateListItem(privateListContent, serverName, session);
                }
            }
        }
    }

    private void CreateListItem(Transform parent, string displayName, SessionInfo session)
    {
        if (serverListItemPrefab == null || parent == null) return;

        GameObject item = Instantiate(serverListItemPrefab, parent);
        var itemUI = item.GetComponent<ServerListItemUI>();
        if (itemUI != null)
        {
            itemUI.Setup(
                displayName,
                session.PlayerCount,
                session.MaxPlayers,
                () =>
                {
                    ShowJoinConfirmation(displayName, session);
                }
            );
        }
    }


    private async Task<bool> JoinSelectedSession(SessionInfo session)
    {
        if (_isStartingGame)
            return false;

        _isStartingGame = true;

        bool success = false;

        try
        {
            await ResetAndCreateRunner();

            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = session.Name,
                SceneManager = _runner.gameObject
                    .AddComponent<NetworkSceneManagerDefault>()
            });

            if (!result.Ok)
            {
                Debug.LogError($"参加失敗: {result.ShutdownReason}");

                ShowErrorMessage("サーバーの参加に失敗しました。");

                await RecoverToLobbyAsync();
                return false;
            }

            Debug.Log("サーバー参加成功");

            success = true;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"参加処理中に例外が発生: {e}");

            ShowErrorMessage("サーバーの参加に失敗しました。");

            await RecoverToLobbyAsync();
            return false;
        }
        finally
        {
            LoadingUI.Instance?.CompleteAndHide();
            if (!success)
                _isStartingGame = false;
        }
    }


    private void ClearListContent(Transform contentParent)
    {
        if (contentParent == null) return;
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
    }

    private void SetupPlayerNameSaving()
    {
        if (createPlayerNameInput != null)
        {
            createPlayerNameInput.text =
                PlayerNameData.CurrentName;

            createPlayerNameInput.onEndEdit.AddListener(
                SavePlayerName
            );
        }

        if (publicPlayerNameInput != null)
        {
            publicPlayerNameInput.text =
                PlayerNameData.CurrentName;

            publicPlayerNameInput.onEndEdit.AddListener(
                SavePlayerName
            );
        }

        if (privatePlayerNameInput != null)
        {
            privatePlayerNameInput.text =
                PlayerNameData.CurrentName;

            privatePlayerNameInput.onEndEdit.AddListener(
                SavePlayerName
            );
        }
    }

    private void SavePlayerName(string name)
    {
        PlayerNameData.SetName(name);

        Debug.Log(
            $"[PlayerName] 保存: {PlayerNameData.CurrentName}"
        );
    }

    private async Task RecoverToLobbyAsync()
    {
        Debug.Log("ネットワーク処理に失敗。ロビーへ復帰します。");

        NetworkRunner failedRunner = _runner;

        // 現在のRunnerを切り離す
        _runner = null;
        _isLobbyJoined = false;

        // 失敗したRunnerを終了
        if (failedRunner != null)
        {
            try
            {
                if (!failedRunner.IsShutdown)
                {
                    await failedRunner.Shutdown(
                        destroyGameObject: true
                    );
                }
                else
                {
                    Destroy(failedRunner.gameObject);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Runner終了エラー: {e}");
            }
        }

        // 新しいRunnerでロビーへ再接続
        await ConnectToFusionLobbyAsync();
    }
    
    private void ShowErrorMessage(string message)
    {
        if (errorMessageText == null)
            return;

        // 前の通知が残っていれば停止
        if (errorMessageCoroutine != null)
        {
            StopCoroutine(errorMessageCoroutine);
        }

        errorMessageText.text = message;
        errorMessageText.gameObject.SetActive(true);

        errorMessageCoroutine =
            StartCoroutine(HideErrorMessageAfterDelay());
    }

    private IEnumerator HideErrorMessageAfterDelay()
    {
        yield return new WaitForSeconds(errorMessageDuration);

        if (errorMessageText != null)
            errorMessageText.gameObject.SetActive(false);

        errorMessageCoroutine = null;
    }

    // --- コールバックの実装 ---
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    #endregion
}