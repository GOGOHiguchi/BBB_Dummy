using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Waiting, Countdown, Playing, HoleResult, GameResult }

    // --- ネットワーク同期変数 ---
    [Networked] public GameState CurrentState { get; set; }
    [Networked] public int CurrentHole { get; set; } = 1;
    [Networked] public TickTimer StateTimer { get; set; }
    [Networked] public NetworkBool IsFirstHitClaimed { get; set; }

    [Header("設定")]
    [SerializeField] private int maxHoles = 3;
    
    // ★ UIアニメーションの「3, 2, 1, GO! (消滅)」の全演出時間(4.2秒)に合わせて設定
    [SerializeField] private float countdownTime = 4.5f; 
    
    [SerializeField] private float holeTimeLimit = 120f; // 1ホールの制限時間
    [SerializeField] private float resultTime = 10f; // リザルト画面の表示時間

    [Header("シーン遷移設定")]
    [Tooltip("3ホールのシーン名を順番に設定してください")]
    [SerializeField] private List<string> holeSceneNames = new List<string>();
    [SerializeField] private string lobbySceneName = "LobbyScene";

    // FusionのNetworkDictionaryを使用して全クライアントにスコアを同期
    [Networked, Capacity(10)]
    public NetworkDictionary<PlayerRef, NetworkPlayerData> PlayerDatas { get; }
    private float holeStartTime;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // シーン遷移しても破棄させない
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void Spawned()
    {
        Debug.Log(
            $"[GameManager Spawned] " +
            $"IsServer={Runner.IsServer}, " +
            $"LocalPlayer={Runner.LocalPlayer}, " +
            $"PlayerName={PlayerNameData.CurrentName}"
        );

        if (HasStateAuthority)
        {
            CurrentState = GameState.Waiting;
        }

        RpcSetPlayerName(
            Runner.LocalPlayer,
            PlayerNameData.CurrentName
        );
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcSetPlayerName(PlayerRef player, string name)
    {
        Debug.Log($"=== Name RPC START ===");
        Debug.Log($"Target Player = {player}");
        Debug.Log($"New Name      = {name}");

        foreach (var kvp in PlayerDatas)
        {
            Debug.Log(
                $"BEFORE  PlayerRef={kvp.Key}, Name={kvp.Value.PlayerName}"
            );
        }

        if (PlayerDatas.TryGet(player, out var data))
        {
            data.PlayerName = name;
            PlayerDatas.Set(player, data);
        }
        else
        {
            PlayerDatas.Add(player, new NetworkPlayerData
            {
                PlayerRef = player,
                PlayerName = name,
                TotalScore = 0
            });
        }

        foreach (var kvp in PlayerDatas)
        {
            Debug.Log(
                $"AFTER   PlayerRef={kvp.Key}, Name={kvp.Value.PlayerName}"
            );
        }

        Debug.Log($"=== Name RPC END ===");
    }

    // ロビーから呼ばれる：ゲーム全体（3ホール）の開始
    public void StartTournament()
    {
        if (!HasStateAuthority) return;

        CurrentHole = 1;

        // ロビーにいる全員をデータに登録
        foreach (var player in Runner.ActivePlayers)
        {
            RegisterPlayer(player);
        }

        if (holeSceneNames.Count > 0)
        {
            LoadScene(holeSceneNames[0]);
            CurrentState = GameState.Waiting;
        }
        else
        {
            Debug.LogError("ホールシーンが設定されていません。");
        }
    }

    // 各シーンロード完了時にBasicSpawnerから呼ばれる。
    // イントロ終了報告が全プレイヤーから届くまでWaitingを維持する。
    [Networked, Capacity(10)]
    private NetworkDictionary<PlayerRef, NetworkBool> IntroReadyPlayers { get; }

    public void InitializeHole()
    {
        if (!HasStateAuthority) return;

        CurrentState = GameState.Waiting;
        StateTimer = TickTimer.None;
        IsFirstHitClaimed = false;
        IntroReadyPlayers.Clear();

        foreach (PlayerRef player in Runner.ActivePlayers)
            IntroReadyPlayers.Add(player, false);

        Debug.Log($"第 {CurrentHole} ホール準備完了。イントロ終了待ち ({IntroReadyPlayers.Count}人)");
    }

    // MapIntroControllerから各プレイヤーがローカルカメラ復帰完了時に呼び出す。
    public void ReportIntroFinished()
    {
        if (Runner == null || !Runner.IsRunning)
            return;

        RpcReportIntroFinished(Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcReportIntroFinished(PlayerRef player)
    {
        if (!HasStateAuthority || CurrentState != GameState.Waiting)
            return;

        if (!Runner.ActivePlayers.Contains(player))
            return;

        if (!IntroReadyPlayers.ContainsKey(player))
            IntroReadyPlayers.Add(player, false);

        IntroReadyPlayers.Set(player, true);
        Debug.Log($"[GameManager] Intro ready: {player}");

        foreach (PlayerRef activePlayer in Runner.ActivePlayers)
        {
            if (!IntroReadyPlayers.TryGet(activePlayer, out NetworkBool ready) || !ready)
                return;
        }

        StartCountdown();
    }

    private void StartCountdown()
    {
        if (!HasStateAuthority || CurrentState != GameState.Waiting)
            return;

        CurrentState = GameState.Countdown;
        StateTimer = TickTimer.CreateFromSeconds(Runner, countdownTime);
        IsFirstHitClaimed = false;

        foreach (var kvp in PlayerDatas)
        {
            var data = kvp.Value;
            data.IsFinished = false;
            data.CurrentHoleScore = 0;
            PlayerDatas.Set(kvp.Key, data);
        }

        Debug.Log($"第 {CurrentHole} ホール開始カウントダウン！全員のカメラ復帰完了");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        switch (CurrentState)
        {
            case GameState.Countdown:
                // 4.2秒経過（「GO!」が消えた瞬間）で即座に Playing に移行して制限時間（120秒）カウント開始
                if (StateTimer.Expired(Runner))
                {
                    CurrentState = GameState.Playing;
                    StateTimer = TickTimer.CreateFromSeconds(Runner, holeTimeLimit);
                    holeStartTime = Time.time;
                    Debug.Log($"第 {CurrentHole} ホールスタート！ 制限時間スタート");
                }
                break;

            case GameState.Playing:
                if (StateTimer.Expired(Runner) || CheckAllPlayersFinished())
                {
                    EndHole();
                }
                break;

            case GameState.HoleResult:
                if (StateTimer.Expired(Runner))
                {
                    ProceedToNextHoleOrFinish();
                }
                break;

            case GameState.GameResult: // 最終結果表示後、ロビーへ
                if (StateTimer.Expired(Runner))
                {
                    ReturnToLobby();
                }
                break;
        }
    }

    public void RegisterPinHit(PlayerRef player, int rawPinCount)
    {
        if (!HasStateAuthority || !PlayerDatas.TryGet(player, out var data)) return;
        if (data.IsFinished) return; 

        float multiplier = 1.0f;
        if (!IsFirstHitClaimed)
        {
            IsFirstHitClaimed = true;
            multiplier = 1.5f;
            data.FirstPlaceCount++; // ★ 最初にフィニッシュした人 = 1位としてカウント
            Debug.Log($"Player {player} が最初のピン撃破！(1.5倍ボーナス・1位獲得)");

            if (CurrentState == GameState.Playing)
            {
                // StateTimer の現在の残り時間を取得
                float? remainingTimeOrNull = StateTimer.RemainingTime(Runner);
                
                // 残り時間が取得でき、かつ40秒より多い場合のみ、40秒に上書きする
                if (remainingTimeOrNull.HasValue && remainingTimeOrNull.Value > 40f)
                {
                    StateTimer = TickTimer.CreateFromSeconds(Runner, 40f);
                    Debug.Log("最初のプレイヤーがゴールしたため、残り時間を40秒に短縮しました！");
                }
            }
        }

        int finalScore = Mathf.RoundToInt(rawPinCount * multiplier);
        data.CurrentHoleScore = finalScore;
        data.TotalScore += finalScore;

        // BestHoleScore
        if (finalScore > data.BestHoleScore)
        {
            data.BestHoleScore = finalScore;
        }

        // PinHitCount
        if (rawPinCount > 0) data.PinHitCount++;

        // FinishTime
        float finishTime = Time.time - holeStartTime;

        data.TotalFinishTime += finishTime;
        data.FinishedHolesCount++;

        data.IsFinished = true;

        PlayerDatas.Set(player, data);

        Debug.Log(
        $"Player {player} Finish! " +
        $"Score={finalScore}, " +
        $"FinishTime={finishTime:F2}s, " +
        $"Average={data.AverageFinishTime:F2}s"
        );

        if (CheckAllPlayersFinished())
        {
            EndHole();
        }
    }

    private bool CheckAllPlayersFinished()
    {
        if (PlayerDatas.Count == 0) return false;

        foreach (var kvp in PlayerDatas)
        {
            if (!kvp.Value.IsFinished) return false;
        }
        return true;
    }

    private void EndHole()
    {
        StateTimer = TickTimer.CreateFromSeconds(Runner, resultTime);

        // まだ次のホールがある場合
        if (CurrentHole < maxHoles && CurrentHole < holeSceneNames.Count)
        {
            CurrentState = GameState.HoleResult;
            Debug.Log($"第 {CurrentHole} ホール終了！「次のホールまで」表示中");
        }
        else // 全ホール終了（最終ホール）
        {
            CurrentState = GameState.GameResult;
            Debug.Log("全ホール終了！「ロビー帰還まで」表示中");
        }
    }

    private void ProceedToNextHoleOrFinish()
    {
        CurrentState = GameState.Waiting;
        // まだ次のホールがある場合
        if (CurrentHole < maxHoles && CurrentHole < holeSceneNames.Count)
        {
            string nextScene = holeSceneNames[CurrentHole];
            CurrentHole++;
            LoadScene(nextScene);
        }
        else // 全ホール終了
        {
            CurrentState = GameState.GameResult;
            StateTimer = TickTimer.CreateFromSeconds(Runner, resultTime);
            Debug.Log("全ホール終了！最終リザルト表示");
        }
    }

    private void ReturnToLobby()
    {
        CurrentState = GameState.Waiting;
        if (HasStateAuthority)
        {
            foreach (var player in Runner.ActivePlayers)
            {
                var playerObj = Runner.GetPlayerObject(player);
                if (playerObj != null)
                {
                    Runner.Despawn(playerObj);
                    Runner.SetPlayerObject(player, null);
                }
            }
        }
        LoadScene(lobbySceneName);
        Runner.Despawn(Object); // GameManager自身の破棄
    }

    private void LoadScene(string scenePathOrName)
    {
        int index = SceneUtility.GetBuildIndexByScenePath(scenePathOrName);
        if (index >= 0)
        {
            Runner.LoadScene(SceneRef.FromIndex(index));
        }
        else
        {
            Debug.LogError($"シーン '{scenePathOrName}' がBuild Settingsに登録されていません。");
        }
    }

    public void RegisterPlayer(PlayerRef player)
    {
        if (!PlayerDatas.ContainsKey(player))
        {
            PlayerDatas.Add(player, new NetworkPlayerData { PlayerRef = player, TotalScore = 0 });
        }
    }

    public void RegisterShot(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        if (!PlayerDatas.TryGet(player, out var data))
            return;

        data.ShotsToGoal++;

        PlayerDatas.Set(player, data);
    }

    public void RegisterHitDistance(PlayerRef player, float distance)
    {
        if (!HasStateAuthority)
            return;
        if (!PlayerDatas.TryGet(player, out var data))
            return;
        if (distance > data.MaxHitDistance)
        {
            data.MaxHitDistance = distance;
        }
        PlayerDatas.Set(player, data);
    }

    // ============================================
    // ★ スタッツ記録用メソッド (ログ追加版)
    // ============================================
    public void RecordItemPickup(PlayerRef player)
    {
        if (!HasStateAuthority) return;
        
        if (PlayerDatas.TryGet(player, out var data))
        {
            data.ItemCount++;
            PlayerDatas.Set(player, data);
            Debug.Log($"[Stats] Player {player} がアイテムを獲得！ 総獲得数: {data.ItemCount}");
        }
        else
        {
            Debug.LogWarning($"[Stats] Player {player} のデータが見つかりません(ItemPickup)。");
        }
    }

    public void RecordItemUse(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (PlayerDatas.TryGet(player, out var data))
        {
            data.KnockoutAttempts++; 
            PlayerDatas.Set(player, data);
            Debug.Log($"[Stats] Player {player} が妨害アイテムを使用！ 試行回数: {data.KnockoutAttempts}");
        }
    }

    public void RecordInterference(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (PlayerDatas.TryGet(player, out var data))
        {
            data.InterferenceCount++; 
            data.KnockoutSuccesses++; 
            PlayerDatas.Set(player, data);
            Debug.Log($"[Stats] Player {player} の妨害が成功！ ノックアウト成功数: {data.KnockoutSuccesses} / 率: {data.KnockoutRate:F2}");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}