using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    private bool _isReturningToTitle = false;
    [Header("プレイヤーのプレハブをここに設定")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;

    [Header("複数のスポーンポイント（Transform）を設定")]
    [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();

    private NetworkRunner _currentRunner;
    private int _spawnCount = 0;
    [SerializeField] private float _minimumSpawnDistance = 2.5f;

    public static Vector3 LocalAimDirection { get; private set; }
    = Vector3.forward;

    private void Update()
    {
        CheckAndRegisterRunner();
    }

    private void CheckAndRegisterRunner()
    {
        NetworkRunner activeRunner = null;

        foreach (var instance in NetworkRunner.Instances)
        {
            if (instance != null && instance.IsRunning)
            {
                activeRunner = instance;
                break;
            }
        }

        if (activeRunner == _currentRunner)
            return;

        if (_currentRunner != null)
        {
            _currentRunner.RemoveCallbacks(this);
        }

        _currentRunner = activeRunner;

        if (_currentRunner == null)
            return;

        // Runnerへの登録箇所をここに一本化し、二重登録を防ぐ。
        _currentRunner.AddCallbacks(this);

        Debug.Log(
            $"BasicSpawner: Runner ({_currentRunner.gameObject.name}) にコールバックを登録しました。 " +
            $"(IsServer={_currentRunner.IsServer})"
        );

        // BasicSpawnerの有効化がOnPlayerJoinedより後になった場合も、
        // 既に参加しているプレイヤーを取りこぼさない。
        if (_currentRunner.IsServer)
        {
            foreach (var player in _currentRunner.ActivePlayers)
            {
                SpawnPlayer(_currentRunner, player);
            }
        }
    }

    private void OnDisable()
    {
        if (_currentRunner != null)
        {
            _currentRunner.RemoveCallbacks(this);
            _currentRunner = null;
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log(
            $"BasicSpawner: OnPlayerJoined " +
            $"Player={player}, " +
            $"IsRunning={runner.IsRunning}, " +
            $"IsServer={runner.IsServer}"
        );

        if (runner.IsServer)
        {
            SpawnPlayer(runner, player);
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"BasicSpawner: OnSceneLoadDone IsRunning={runner.IsRunning}, IsServer={runner.IsServer}");

        if (runner.IsServer)
        {
            foreach (var player in runner.ActivePlayers)
            {
                Debug.Log($"BasicSpawner: Spawn対象 = {player}");
                SpawnPlayer(runner, player);
            }

            // ★ ここから追加：GameManagerが存在していればホール開始の初期化を行う
            if (GameManager.Instance != null)
            {
                GameManager.Instance.InitializeHole();
            }
        }
    }

    private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        if (runner.GetPlayerObject(player) != null)
        {
            Debug.Log($"Player {player} は既にSpawn済み");
            return;
        }

        Debug.Log($"Player {player} をSpawnします");

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (_spawnPoints != null && _spawnPoints.Count > 0)
        {
            if (!TrySelectSpawnPoint(
                    runner,
                    player,
                    out Transform selectedPoint,
                    out int pointIndex,
                    out float nearestPlayerDistance))
            {
                Debug.LogError("BasicSpawner: 有効なスポーンポイントがありません。各要素のTransform設定を確認してください。");
                return;
            }

            spawnPosition = selectedPoint.position;
            spawnRotation = selectedPoint.rotation;

            if (nearestPlayerDistance < _minimumSpawnDistance)
            {
                Debug.LogWarning(
                    $"BasicSpawner: 全スポーンポイントが使用中です。最も距離を確保できる " +
                    $"[{pointIndex}] ({selectedPoint.name}) を使用します。 " +
                    $"最寄りプレイヤーとの距離={nearestPlayerDistance:F2}"
                );
            }
            else
            {
                Debug.Log(
                    $"Player {player} はスポーンポイント[{pointIndex}] " +
                    $"({selectedPoint.name}) に配置されます。"
                );
            }
        }
        else
        {
            Debug.LogWarning("BasicSpawner: スポーンポイントが設定されていません。");
            spawnPosition = new Vector3(0, 5, 0);
            spawnRotation = Quaternion.identity;
        }

        NetworkObject networkPlayerObject = runner.Spawn(
            _playerPrefab,
            spawnPosition,
            spawnRotation,
            player
        );

        if (networkPlayerObject == null)
        {
            Debug.LogError($"BasicSpawner: Player {player} のSpawnに失敗しました。");
            return;
        }

        runner.SetPlayerObject(player, networkPlayerObject);
        _spawnCount++;

        Debug.Log($"Player {player} のSpawn完了");
    }

    /// <summary>
    /// リスト上位から順に空いているスポーンポイントを選ぶ。
    /// 既存プレイヤーの現在位置も確認し、同じ地点への重複スポーンを避ける。
    /// </summary>
    private bool TrySelectSpawnPoint(
        NetworkRunner runner,
        PlayerRef joiningPlayer,
        out Transform selectedPoint,
        out int selectedIndex,
        out float nearestPlayerDistance)
    {
        selectedPoint = null;
        selectedIndex = -1;
        nearestPlayerDistance = -1f;

        if (_spawnPoints == null || _spawnPoints.Count == 0)
            return false;

        int startIndex = _spawnCount % _spawnPoints.Count;
        float bestDistance = float.NegativeInfinity;
        Transform bestPoint = null;
        int bestIndex = -1;

        for (int offset = 0; offset < _spawnPoints.Count; offset++)
        {
            int index = (startIndex + offset) % _spawnPoints.Count;
            Transform candidate = _spawnPoints[index];

            if (candidate == null)
                continue;

            float nearestDistance = float.PositiveInfinity;

            foreach (PlayerRef existingPlayer in runner.ActivePlayers)
            {
                if (existingPlayer == joiningPlayer)
                    continue;

                NetworkObject existingObject = runner.GetPlayerObject(existingPlayer);
                if (existingObject == null)
                    continue;

                float distance = Vector3.Distance(
                    candidate.position,
                    existingObject.transform.position
                );

                if (distance < nearestDistance)
                    nearestDistance = distance;
            }

            // 十分に離れた地点が見つかったら、リスト順を優先して採用する。
            if (nearestDistance >= _minimumSpawnDistance)
            {
                selectedPoint = candidate;
                selectedIndex = index;
                nearestPlayerDistance = nearestDistance;
                return true;
            }

            // 全地点が近い場合は、最も距離を確保できる地点を記録する。
            if (nearestDistance > bestDistance)
            {
                bestDistance = nearestDistance;
                bestPoint = candidate;
                bestIndex = index;
            }
        }

        if (bestPoint == null)
            return false;

        selectedPoint = bestPoint;
        selectedIndex = bestIndex;
        nearestPlayerDistance = bestDistance;
        return true;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) 
    { 
        Debug.Log($"BasicSpawner: Player {player} が退出しました。");
        
        // ホスト（サーバー）側で退出したプレイヤーのオブジェクトを破棄する
        if (runner.IsServer)
        {
            var playerObj = runner.GetPlayerObject(player);
            if (playerObj != null)
            {
                runner.Despawn(playerObj);
                // 参照もクリアしておく
                runner.SetPlayerObject(player, null);
            }
        }
    }
    
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();
        Vector2 moveInput = Vector2.zero;
        // キャラクター選択中はゲーム操作をネットワーク入力へ送らない。
        bool blockGameplayInput = CharacterSelectUI.IsAnyOpen;

        if (!blockGameplayInput && Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;

            data.isWPressed = Keyboard.current.wKey.isPressed;
            data.isAPressed = Keyboard.current.aKey.isPressed;
            data.isDPressed = Keyboard.current.dKey.isPressed;
            
            data.isJumpPressed = Keyboard.current.spaceKey.isPressed;

            data.cancelThrow = Keyboard.current.fKey.isPressed;
            data.interact = !CharacterSelectInteractable.IsLocalPlayerInRange && Keyboard.current.eKey.isPressed;
            if (data.interact)
            {
                Debug.Log("[Input] Eキーが押されています");
            }
        }
        
        if (!blockGameplayInput && Mouse.current != null)
        {
            data.isCharging = Mouse.current.leftButton.isPressed;
            data.isAiming = Mouse.current.rightButton.isPressed;
            data.useItem = Mouse.current.leftButton.isPressed;
            data.isFirePressed = Mouse.current.leftButton.isPressed;
        }

        if (PlayerMovement.Local != null)
        {
            data.selectedSlot = PlayerMovement.Local.LocalSelectedSlot;
        }

        Vector3 rawDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (Camera.main != null)
        {
            Transform camTransform = Camera.main.transform;
            Vector3 camForward = camTransform.forward; // カメラの実際の向き（上下を含む）
            Vector3 camRight = camTransform.right;
            
            data.aimDirection = camForward;

            // 移動用にY軸を0にして水平なベクトルを作る
            Vector3 moveForward = camForward;
            Vector3 moveRight = camRight;
            
            moveForward.y = 0f;
            moveRight.y = 0f;
            moveForward.Normalize();
            moveRight.Normalize();

            if (rawDirection.sqrMagnitude > 0)
            {
                // 移動計算にはY軸が0のベクトルを使う
                data.movementInput = (moveForward * rawDirection.z + moveRight * rawDirection.x).normalized;
            }
        }
        else
        {
            data.movementInput = rawDirection;
            data.aimDirection = Vector3.forward;
        }
        
        LocalAimDirection = data.aimDirection.normalized;
        
        input.Set(data);
    }
    
    // --- 残りの INetworkRunnerCallbacks 実装 ---
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"BasicSpawner: Runner終了。Reason={shutdownReason}");

        // ホスト自身はLobbyMenuUI側でタイトルへ戻す
        // クライアントはここでタイトルへ戻す
        if (runner == null || runner.IsServer)
            return;

        if (_isReturningToTitle)
            return;

        _isReturningToTitle = true;

        Debug.Log("BasicSpawner: サーバーとの接続終了を検知。タイトルへ戻ります。");

        Destroy(runner.gameObject);
        SceneManager.LoadScene("1_Title");
    }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"BasicSpawner: サーバーとの接続が切断されました。Reason={reason}");

        if (_isReturningToTitle)
            return;

        _isReturningToTitle = true;

        ReturnToTitleAfterDisconnectAsync(runner);
    }

    private async void ReturnToTitleAfterDisconnectAsync(NetworkRunner runner)
    {
        // Fusionの切断処理が進むのを1フレーム待つ
        await Task.Yield();

        try
        {
            if (runner != null)
            {
                if (!runner.IsShutdown)
                {
                    await runner.Shutdown(destroyGameObject: true);
                }
                else
                {
                    Destroy(runner.gameObject);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"切断後のRunner終了処理でエラー: {e}");
        }

        Debug.Log("BasicSpawner: タイトル画面へ戻ります。");
        SceneManager.LoadScene("1_Title");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}