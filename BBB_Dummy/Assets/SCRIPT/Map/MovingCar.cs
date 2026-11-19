using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// シーンに配置されたネットワーク車両。
/// 移動・衝突判定はState Authorityのみが実行し、
/// TransformはNetworkTransformで全クライアントへ同期する。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class MovingCar : NetworkBehaviour
{
    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 30f;
    [SerializeField] private float startZ = -290f;
    [SerializeField] private float endZ = 158f;

    [Header("ノックバック設定（プレイヤー）")]
    [SerializeField] private float knockbackUpSpeed = 10f;
    [SerializeField] private float knockbackBackSpeed = 25f;
    [SerializeField] private float stunDuration = 2f;

    [Header("ノックバック設定（ボール専用）")]
    [SerializeField] private float ballKnockbackMultiplier = 2.0f;

    [Header("エフェクト設定")]
    [SerializeField] private GameObject hitEffectPrefab;
    [Tooltip("エフェクトを発生させないタグ（例: Mine, Aura など）")]
    [SerializeField] private List<string> ignoreEffectTags = new List<string> { "Mine", "Aura", "Invincible" };

    private Vector3 _initialPosition;

    public override void Spawned()
    {
        _initialPosition = transform.position;

        // シーン配置オブジェクトとしてロードされているか確認しやすくするログ
        Debug.Log($"[MovingCar] Spawned. StateAuthority={HasStateAuthority}, SceneObject={Object.IsSceneObject}");
    }

    public override void FixedUpdateNetwork()
    {
        // 車の位置を決定するのはState Authorityだけ。
        if (!HasStateAuthority)
            return;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        transform.position += transform.forward * moveSpeed * Runner.DeltaTime;

        if (transform.position.z >= endZ)
        {
            Vector3 position = transform.position;
            position.z = startZ;
            transform.position = position;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!HasStateAuthority)
            return;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        ProcessHit(collision.gameObject, hitPoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority)
            return;

        ProcessHit(other.gameObject, other.ClosestPoint(transform.position));
    }

    private void ProcessHit(GameObject hitObject, Vector3 hitPoint)
    {
        if (hitObject == null)
            return;

        // 1. プレイヤー
        PlayerMovement player = hitObject.GetComponentInParent<PlayerMovement>();
        if (player == null)
            player = hitObject.GetComponent<PlayerMovement>();

        if (player != null)
        {
            Vector3 knockbackForce =
                Vector3.forward * knockbackBackSpeed +
                Vector3.up * knockbackUpSpeed;

            player.ApplyKnockback(knockbackForce, stunDuration);
            RpcSpawnHitEffect(player.transform.position + Vector3.up);
            return;
        }

        // 2. ボウリングボール
        if (HasTagSafe(hitObject, "Ball"))
        {
            Rigidbody ballRb = hitObject.GetComponentInParent<Rigidbody>();
            if (ballRb == null)
                ballRb = hitObject.GetComponent<Rigidbody>();

            NetworkObject ballObject = hitObject.GetComponentInParent<NetworkObject>();
            if (ballRb != null && ballObject != null && ballObject.HasStateAuthority)
            {
                Vector3 ballKnockback =
                    (Vector3.forward * knockbackBackSpeed +
                     Vector3.up * knockbackUpSpeed) * ballKnockbackMultiplier;

                ballRb.AddForce(ballKnockback, ForceMode.Impulse);
            }

            return;
        }

        // 3. エフェクト除外タグ
        foreach (string ignoreTag in ignoreEffectTags)
        {
            if (!string.IsNullOrEmpty(ignoreTag) && HasTagSafe(hitObject, ignoreTag))
                return;
        }

        // 4. その他のオブジェクト
        RpcSpawnHitEffect(hitPoint);
    }

    private bool HasTagSafe(GameObject target, string tagName)
    {
        return target != null && target.CompareTag(tagName);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcSpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null)
            return;

        GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        Destroy(effect, 2f);
    }
}
