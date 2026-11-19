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
    [SerializeField] private float explosionEffectLifetime = 5f;

    // 発動状態はState Authorityが管理し、ネットワーク上で共有する
    [Networked] private NetworkBool HasTriggered { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        // 接触判定・攻撃確定・DespawnはState Authorityだけで実行する
        if (!HasStateAuthority || HasTriggered || other == null)
            return;

        // 子Colliderから接触しても、親階層のPlayerMovementを取得する
        PlayerMovement targetPlayer = other.GetComponentInParent<PlayerMovement>();

        if (targetPlayer != null)
        {
            // 設置者本人の接触では発動しない
            if (targetPlayer.Object == null ||
                targetPlayer.Object.InputAuthority == Object.InputAuthority)
            {
                return;
            }

            Vector3 knockbackDir = targetPlayer.transform.position - transform.position;
            knockbackDir.y = 0f;

            if (knockbackDir.sqrMagnitude < 0.001f)
                knockbackDir = Vector3.forward;

            Vector3 force =
                knockbackDir.normalized * explosionForce +
                Vector3.up * upwardsModifier;

            // シールド・無敵などで防がれた場合も、地雷自体は消費する
            HasTriggered = true;

            bool knockedBack = targetPlayer.ApplyKnockback(force, stunDuration);

            if (knockedBack && GameManager.Instance != null)
            {
                GameManager.Instance.RecordInterference(Object.InputAuthority);
            }

            Debug.Log($"[Landmine] プレイヤーに発動: {targetPlayer.Object.InputAuthority}");
            Runner.Despawn(Object);
            return;
        }

        // 子Colliderに付いたタグ/Rigidbodyではなく、親階層から取得する
        NetworkObject targetNetworkObject = other.GetComponentInParent<NetworkObject>();
        Rigidbody targetRigidbody = other.GetComponentInParent<Rigidbody>();

        // NetworkObjectを持つ物理オブジェクト（ボール等）のみ対象にする
        if (targetNetworkObject == null ||
            targetRigidbody == null ||
            targetNetworkObject == Object ||
            !targetNetworkObject.HasStateAuthority ||
            targetNetworkObject.InputAuthority == Object.InputAuthority)
        {
            return;
        }

        HasTriggered = true;

        Vector3 forceDir = targetRigidbody.worldCenterOfMass - transform.position;
        forceDir.y = 0f;

        if (forceDir.sqrMagnitude < 0.001f)
            forceDir = Vector3.forward;

        // 物理状態の変更も対象NetworkObjectのState Authorityでのみ実施
        if (!targetRigidbody.isKinematic)
        {
            Vector3 force =
                forceDir.normalized * explosionForce * 1.5f +
                Vector3.up * upwardsModifier;

            targetRigidbody.AddForce(force, ForceMode.Impulse);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecordInterference(Object.InputAuthority);
        }

        Debug.Log($"[Landmine] NetworkObjectに発動: {targetNetworkObject.name}");
        Runner.Despawn(Object);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Despawn通知を受け取った各クライアントでローカル演出を再生する。
        // 発動直後にDespawnされるため、ここではNetworkedフラグの最終同期に依存しない。
        if (explosionEffectPrefab == null)
            return;

        GameObject effect = Instantiate(
            explosionEffectPrefab,
            transform.position,
            Quaternion.identity
        );

        if (explosionEffectLifetime > 0f)
        {
            Destroy(effect, explosionEffectLifetime);
        }
    }
}