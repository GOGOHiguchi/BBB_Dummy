using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

public class HoleResultUI : MonoBehaviour
{
    [Header("パネル全体")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text nextHoleTimerText; // 「次のホールまで : XX」
    [SerializeField] private TMP_Text roomNameText;

    [Header("左側：自分のスコアボードUI")]
    [SerializeField] private TMP_Text myTotalScoreText;        // 現在のスコア
    [SerializeField] private TMP_Text bestHoleScoreText;       // 1ホールの最高スコア
    [SerializeField] private TMP_Text maxHitDistanceText;      // 最長距離スロー (m)
    [SerializeField] private TMP_Text avgFinishTimeText;       // 平均フィニッシュタイム (s)
    [SerializeField] private TMP_Text itemCountText;           // 獲得アイテム数
    [SerializeField] private TMP_Text knockoutRateText;        // ノックアウト率

    [Header("右側：ルームボードUI (リスト生成)")]
    [SerializeField] private Transform boardListContent;       // リストの親Transform (Content)
    [SerializeField] private GameObject playerBoardItemPrefab; // 作成したPrefab

    private bool isVisible = false;

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.Runner == null)
        {
            if (resultPanel != null) resultPanel.SetActive(false);
            return;
        }

        var state = GameManager.Instance.CurrentState;
        bool shouldShow = state == GameManager.GameState.HoleResult || state == GameManager.GameState.GameResult;

        if (shouldShow)
        {
            if (!isVisible)
            {
                ShowResultUI();
            }

            // タイマーのカウントダウン表示更新
            if (nextHoleTimerText != null && GameManager.Instance.StateTimer.IsRunning)
            {
                float remaining = GameManager.Instance.StateTimer.RemainingTime(GameManager.Instance.Runner) ?? 0f;
                string label = state == GameManager.GameState.GameResult ? "ロビー帰還まで : " : "次のホールまで : ";
                nextHoleTimerText.text = $"{label}{Mathf.CeilToInt(remaining)}";
            }
        }
        else
        {
            if (isVisible)
            {
                HideResultUI();
            }
        }
    }

    private void ShowResultUI()
    {
        isVisible = true;
        if (resultPanel != null) resultPanel.SetActive(true);
        UpdateRoomHeader();
        UpdateMyScoreboard();
        UpdateRoomBoardList();
    }

    private void HideResultUI()
    {
        isVisible = false;
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    private void UpdateRoomHeader()
    {
        if (roomNameText == null) return;

        var runner = GameManager.Instance.Runner;
        if (runner != null && runner.SessionInfo.IsValid)
        {
            // SessionPropertiesから MatchmakingKeys.SERVER_NAME を取得
            if (runner.SessionInfo.Properties.TryGetValue(MatchmakingKeys.SERVER_NAME, out var serverNameProp) && serverNameProp.PropertyValue != null)
            {
                roomNameText.text = $"{serverNameProp.PropertyValue.ToString()}のロビー";
            }
            else
            {
                roomNameText.text = runner.SessionInfo.Name; // フォールバック
            }
        }
    }

    // 左側：自分のスコアボードの更新
    private void UpdateMyScoreboard()
    {
        var runner = GameManager.Instance.Runner;
        if (runner == null) return;

        PlayerRef localPlayer = runner.LocalPlayer;

        if (GameManager.Instance.PlayerDatas.TryGet(localPlayer, out NetworkPlayerData myData))
        {
            if (myTotalScoreText != null) myTotalScoreText.text = myData.TotalScore.ToString();
            if (bestHoleScoreText != null) bestHoleScoreText.text = myData.BestHoleScore.ToString();
            if (maxHitDistanceText != null) maxHitDistanceText.text = $"{myData.MaxHitDistance:F1}m";
            if (avgFinishTimeText != null) avgFinishTimeText.text = $"{myData.AverageFinishTime:F1}s";
            if (itemCountText != null) itemCountText.text = myData.ItemCount.ToString();
            if (knockoutRateText != null) knockoutRateText.text = $"{myData.KnockoutRate * 100f:F1}%";
        }
    }

    // 右側：ルームボードリストの再生成
    private void UpdateRoomBoardList()
    {
        if (boardListContent == null || playerBoardItemPrefab == null) return;

        // 既存のアイテム要素を一旦クリア
        foreach (Transform child in boardListContent)
        {
            Destroy(child.gameObject);
        }

        // プレイヤーデータをトータルスコアの降順（同点の場合は今回加点の降順）でソート
        var sortedPlayers = GameManager.Instance.PlayerDatas
            .Select(kvp => kvp.Value)
            .OrderByDescending(p => p.TotalScore)
            .ThenByDescending(p => p.CurrentHoleScore)
            .ToList();

        var runner = GameManager.Instance.Runner;

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            var data = sortedPlayers[i];
            int rank = i + 1;

            GameObject itemObj = Instantiate(playerBoardItemPrefab, boardListContent);
            var itemUI = itemObj.GetComponent<PlayerBoardItemUI>();

            if (itemUI != null)
            {
                // ★ 修正：対象プレイヤーがホスト（サーバー）かどうかを正確に判定
                // (1) 自分がサーバーで対象が自分、または (2) 対象がHost(PlayerRef.FromEncoded(1))である場合
                bool isHost = (runner.IsServer && data.PlayerRef == runner.LocalPlayer) || 
                             (!runner.IsServer && data.PlayerRef == PlayerRef.FromEncoded(1));

                // クライアント側で自分のpingを更新
                if (runner != null && !isHost && data.PlayerRef == runner.LocalPlayer)
                {
                    data.Ping = Mathf.RoundToInt((float)(runner.GetPlayerRtt(runner.LocalPlayer) * 1000));
                }

                itemUI.Setup(rank, data, isHost);
            }
        }
    }
}