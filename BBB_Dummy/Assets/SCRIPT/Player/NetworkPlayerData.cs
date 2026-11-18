using Fusion;

// プレイヤーごとのネットワーク同期用構造体
public struct NetworkPlayerData : INetworkStruct
{
    public PlayerRef PlayerRef;
    public NetworkString<_32> PlayerName; // プレイヤー名
    public NetworkBool IsFinished;        // フィニッシュ完了か

    // --- スコア関連 ---
    public int CurrentHoleScore;    // 今回のホールの加点
    public int TotalScore;          // 現在のトータルスコア
    public int BestHoleScore;       // 1ホールの最高スコア

    // --- スタッツ関連 ---
    public float MaxHitDistance;    // 最も遠い位置からピンを倒した距離(m)
    public float TotalFinishTime;   // 総フィニッシュタイム（平均計算用）
    public int FinishedHolesCount;  // 完了したホール数
    public int ItemCount;           // 獲得したアイテム数(使用ではなく獲得数)
    public int KnockoutAttempts;    // ノックアウト試行数
    public int KnockoutSuccesses;   // ノックアウト成功数

    // --- ルームボード用 ---
    public int FirstPlaceCount;     // 1位になった回数
    public int PinHitCount;         // ピンを1ピンでも倒せた回数
    public int ShotsToGoal;         // ゴールまでの打数
    public int InterferenceCount;   // 妨害成功回数
    public int Ping;                // Ping値（ミリ秒）

    // 平均フィニッシュタイム計算プロパティ
    public readonly float AverageFinishTime => FinishedHolesCount > 0 ? TotalFinishTime / FinishedHolesCount : 0f;

    // ノックアウト率計算プロパティ
    public readonly float KnockoutRate => KnockoutAttempts > 0 ? (float)KnockoutSuccesses / KnockoutAttempts : 0f;
}