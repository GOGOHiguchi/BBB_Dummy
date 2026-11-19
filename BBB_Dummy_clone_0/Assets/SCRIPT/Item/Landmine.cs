using Fusion;
using UnityEngine;

public class Landmine : NetworkBehaviour
{
    [Header("爆発設定")]
    [SerializeField] private float stunDuration = 3f; 
    [SerializeField] private float explosionForce = 15f; 
    [SerializeField] private float upwardsModifier = 8f; 
    
    [Header("エフェクト (任意)")]
    [SerializeField] private GameObject explosionEffectPrefab;

    // 複数Colliderが同じフレームに接触しても、爆発処理を一度だけ行う
    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
            return;

        Debug.Log(
        $"[Landmine] Trigger検知: {other.name}, " +
        $"Tag={other.tag}, " +
        $"HasStateAuthority={HasStateAuthority}"
        );

        if (!HasStateAuthority) 
        {
            Debug.Log("[Landmine] State Authorityがないため終了");
            return;
        }

    if (other.CompareTag("Player"))
        {
            var targetPlayer =
                other.GetComponentInParent<PlayerMovement>();

            if (targetPlayer == null)
            {
                Debug.LogWarning(
                    "[Landmine] PlayerMovementが見つかりません"
                );
                return;
            }

            Debug.Log(
                $"[Landmine] 接触プレイヤー={targetPlayer.Object.InputAuthority}, " +
                $"地雷所有者={Object.InputAuthority}"
            );

            if (targetPlayer.Object.InputAuthority != Object.InputAuthority)
            {
                hasTriggered = true;

                Vector3 knockbackDir =
                    targetPlayer.transform.position - transform.position;

                knockbackDir.y = 0f;

                if (knockbackDir.sqrMagnitude < 0.001f)
                    knockbackDir = Vector3.forward;

                Vector3 force =
                    knockbackDir.normalized * explosionForce
                    + Vector3.up * upwardsModifier;

                bool knockedBack = targetPlayer.ApplyKnockback(force, stunDuration);

                // シールド等で防がれた場合は妨害成功として記録しない
                if (knockedBack && GameManager.Instance != null)
                {
                    GameManager.Instance.RecordInterference(Object.InputAuthority);
                }
                Debug.Log("[Landmine] 地雷が発動しました！");
                Runner.Despawn(Object);
            }
            else
            {
                Debug.Log("[Landmine] 設置者本人のため発動しません");
            }
        }
        else if (other.CompareTag("Ball") || other.GetComponent<Rigidbody>() != null)
        {
            var networkObject = other.GetComponentInParent<NetworkObject>();
            
            if (networkObject != null &&
                networkObject.InputAuthority != Object.InputAuthority &&
                networkObject.HasStateAuthority)
            {
                hasTriggered = true;

                var rb = other.GetComponentInParent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 forceDir = (other.transform.position - transform.position).normalized;
                    if (forceDir.sqrMagnitude < 0.001f)
                        forceDir = Vector3.forward;

                    rb.AddForce((forceDir * explosionForce * 1.5f) + (Vector3.up * upwardsModifier), ForceMode.Impulse);
                }
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RecordInterference(Object.InputAuthority);
                }
                Runner.Despawn(Object);
            }
        }
    }

    // ★修正: 消去されたタイミングで、それぞれのクライアントが独自にエフェクトを再生する
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }
    }
}