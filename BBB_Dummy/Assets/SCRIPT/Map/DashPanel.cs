using UnityEngine;
using Fusion;

public class DashPanel : NetworkBehaviour
{
    [Header("ダッシュ設定")]
    [SerializeField] private float dashSpeedMultiplier = 1.5f; // 速度倍率（1.5倍）
    [SerializeField] private float dashDuration = 1.5f;       // 加速持続時間（秒）

    [Header("効果音（任意）")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dashSound;

    private void OnTriggerEnter(Collider other)
    {
        // StateAuthority（ホスト/サーバー）のみが判定を行う
        if (!HasStateAuthority) return;

        if (other.CompareTag("Player"))
        {
            PlayerMovement player = other.GetComponent<PlayerMovement>();
            if (player != null)
            {
                // プレイヤーにダッシュ効果を適用（1.5倍）
                player.ApplyDash(dashSpeedMultiplier, dashDuration);

                // スピードアップの判定チェック
                if (!player.DashTimer.ExpiredOrNotRunning(Runner) && player.CurrentDashMultiplier > 1.0f)
                {
                    Debug.Log($"スピードアップ！ (倍率: {player.CurrentDashMultiplier}倍)");
                }
                else
                {
                    Debug.LogError("エラー：スピードが上がっていません！");
                }

                // 全クライアントで効果音を再生
                RPC_PlayDashSound();
            }
            else
            {
                Debug.LogError("エラー：接触したオブジェクトに PlayerMovement がアタッチされていません！");
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDashSound()
    {
        if (audioSource != null && dashSound != null)
        {
            audioSource.PlayOneShot(dashSound);
        }
    }
}