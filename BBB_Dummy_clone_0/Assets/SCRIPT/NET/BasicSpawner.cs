using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
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

    public static Vector3 LocalAimDirection { get; private set; }
    = Vector3.forward;

    private void Update()
    {
        CheckAndRegisterRunner();
    }

    private IEnumerator Start()
    {
        NetworkRunner runner = null;

        while (runner == null || !runner.IsRunning)
        {
            foreach (var instance in NetworkRunner.Instances)
            {
                if (instance != null && instance.IsRunning)
                {
                    runner = instance;
                    break;
                }
            }
            yield return null;
        }

        Debug.Log(
            $"BasicSpawner: Runner準備完了 " +
            $"Object={runner.gameObject.name}, " +
            $"IsRunning={runner.IsRunning}, " +
            $"IsServer={runner.IsServer}"
        );

        runner.AddCallbacks(this);

        if (runner.IsServer)
        {
            foreach (var player in runner.ActivePlayers)
            {
                Debug.Log($"BasicSpawner: 既存PlayerをSpawn = {player}");
                SpawnPlayer(runner, player);
            }
        }
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

        if (activeRunner != _currentRunner)
        {
            if (_currentRunner != null)
            {
                _currentRunner.RemoveCallbacks(this);
            }

            _currentRunner = activeRunner;

            if (_currentRunner != null)
            {
                _currentRunner.AddCallbacks(this);
                Debug.Log($"BasicSpawner: Runner ({_currentRunner.gameObject.name}) にコールバックを登録しました。 (IsServer={_currentRunner.IsServer})");
                // ホスト側で未処理のプレイヤーがいればSpawn
                // if (_currentRunner.IsServer)
                // {
                //     foreach (var player in _currentRunner.ActivePlayers)
                //     {
                //         SpawnPlayer(_currentRunner, player);
                //     }
                // }
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
        if (runner.GetPlayerObject(player) != null)
        {
            Debug.Log($"Player {player} は既にSpawn済み");
            return;
        }

        Debug.Log($"Player {player} をSpawnします");

        // --- スポーン位置の決定処理 ---
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (_spawnPoints != null && _spawnPoints.Count > 0)
        {
            // リストの上から順番に割り当てる
            int pointIndex = _spawnCount % _spawnPoints.Count;
            Transform selectedPoint = _spawnPoints[pointIndex];

            if (selectedPoint != null)
            {
                spawnPosition = selectedPoint.position;
                spawnRotation = selectedPoint.rotation;

                Debug.Log(
                    $"Player {player} は スポーンポイント[{pointIndex}] " +
                    $"({selectedPoint.name}) に配置されます。"
                );

                // スポーン位置の割り当て番号を進める
                _spawnCount++;
            }
            else
            {
                Debug.LogError($"スポーンポイント[{pointIndex}]が未設定です。");
                return;
            }
        }
        else
        {
            Debug.LogWarning("BasicSpawner: スポーンポイントが設定されていません。");
            spawnPosition = new Vector3(0, 5, 0);
        }

        NetworkObject networkPlayerObject =
            runner.Spawn(
                _playerPrefab,
                spawnPosition,
                spawnRotation,
                player
            );

        runner.SetPlayerObject(player, networkPlayerObject);

        Debug.Log($"Player {player} のSpawn完了");
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

        if (Keyboard.current != null)
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
            data.interact = Keyboard.current.eKey.isPressed;
            if (data.interact)
            {
                Debug.Log("[Input] Eキーが押されています");
            }
        }
        
        if (Mouse.current != null)
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