using TMPro;
using UnityEngine;

public class PlayerBoardItemUI : MonoBehaviour
{
    [Header("UIテキスト参照")]
    [SerializeField] private TMP_Text rankText;             // 現在順位
    [SerializeField] private TMP_Text nameText;             // 名前
    [SerializeField] private TMP_Text addedScoreText;       // 今回の加点 (+140 など)
    [SerializeField] private TMP_Text totalScoreText;       // 現在のトータルスコア
    [SerializeField] private TMP_Text firstPlaceCountText;  // 1位になった回数
    [SerializeField] private TMP_Text pinHitCountText;      // ピンを倒した回数
    [SerializeField] private TMP_Text shotsToGoalText;      // ゴールまでの打数
    [SerializeField] private TMP_Text interferenceCountText;// 妨害成功回数
    [SerializeField] private TMP_Text pingText;             // 回線状況 (ping または "ホスト")

    /// <summary>
    /// 1行分のUIデータを反映します
    /// </summary>
    public void Setup(int rank, NetworkPlayerData data, bool isHost)
    {
        if (rankText != null) rankText.text = rank.ToString();
        if (nameText != null) nameText.text = string.IsNullOrEmpty(data.PlayerName.ToString()) ? $"Player {data.PlayerRef.RawEncoded}" : data.PlayerName.ToString();
        
        if (addedScoreText != null)
        {
            addedScoreText.text = data.CurrentHoleScore >= 0 ? $"+{data.CurrentHoleScore}" : data.CurrentHoleScore.ToString();
        }

        if (totalScoreText != null) totalScoreText.text = data.TotalScore.ToString();
        if (firstPlaceCountText != null) firstPlaceCountText.text = data.FirstPlaceCount.ToString();
        if (pinHitCountText != null) pinHitCountText.text = data.PinHitCount.ToString();
        if (shotsToGoalText != null) shotsToGoalText.text = data.ShotsToGoal.ToString();
        if (interferenceCountText != null) interferenceCountText.text = data.InterferenceCount.ToString();

        // Ping表示（ホストの場合は"ホスト"表記）
        if (pingText != null)
        {
            if (isHost)
            {
                pingText.text = "ホスト";
            }
            else
            {
                pingText.text = $"{data.Ping} ms";
            }
        }
    }
}