using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MovingCar : MonoBehaviour
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
    [SerializeField] private float ballKnockbackMultiplier = 2.0f; // ボール吹き飛ばしの倍率

    [Header("エフェクト設定")]
    [SerializeField] private GameObject hitEffectPrefab;
    [Tooltip("エフェクトを発生させないタグ（例: Mine, Aura など）")]
    [SerializeField] private List<string> ignoreEffectTags = new List<string> { "Mine", "Aura", "Invincible" };

    private Vector3 currentVelocity;

    private void Update()
    {
        if (!MapGameManager.IsGameStarted) return;

        currentVelocity = Vector3.forward * moveSpeed;
        transform.Translate(currentVelocity * Time.deltaTime, Space.Self);

        if (transform.position.z >= endZ)
        {
            Vector3 newPosition = transform.position;
            newPosition.z = startZ;
            transform.position = newPosition;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessHit(collision.gameObject, collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other.gameObject, other.ClosestPoint(transform.position));
    }

    private void ProcessHit(GameObject hitObject, Vector3 hitPoint)
    {
        // 1. プレイヤー判定（ノックバック ＋ エフェクト生成）
        PlayerMovement pm = hitObject.GetComponentInParent<PlayerMovement>();
        if (pm == null) pm = hitObject.GetComponent<PlayerMovement>();

        if (pm != null)
        {
            Vector3 knockbackForce = (Vector3.forward * knockbackBackSpeed) + (Vector3.up * knockbackUpSpeed);
            pm.ApplyKnockback(knockbackForce, stunDuration);
            SpawnHitEffect(pm.transform.position + Vector3.up * 1.0f);
            return;
        }

        // 2. ボール（Ballタグ）判定（倍率をかけた強力なノックバック / エフェクト非表示）
        if (hitObject.CompareTag("Ball"))
        {
            Rigidbody ballRb = hitObject.GetComponentInParent<Rigidbody>();
            if (ballRb == null) ballRb = hitObject.GetComponent<Rigidbody>();

            if (ballRb != null)
            {
                // Networked Rigidbody は State Authority だけが変更する。
                NetworkObject ballObject = hitObject.GetComponentInParent<NetworkObject>();
                if (ballObject != null && ballObject.HasStateAuthority)
                {
                    Vector3 ballKnockback = ((Vector3.forward * knockbackBackSpeed) + (Vector3.up * knockbackUpSpeed)) * ballKnockbackMultiplier;
                    ballRb.AddForce(ballKnockback, ForceMode.Impulse);
                }
            }
            return; // エフェクトを出さずに終了
        }

        // 3. 除外タグ判定（地雷や無敵オーラなどエフェクト非表示）
        foreach (string ignoreTag in ignoreEffectTags)
        {
            if (!string.IsNullOrEmpty(ignoreTag) && hitObject.CompareTag(ignoreTag))
            {
                return; // エフェクトを出さずに終了
            }
        }

        // 4. その他のオブジェクト（ロケランの弾など）の場合（エフェクト生成）
        SpawnHitEffect(hitPoint);
    }

    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 2.0f);
        }
    }
}