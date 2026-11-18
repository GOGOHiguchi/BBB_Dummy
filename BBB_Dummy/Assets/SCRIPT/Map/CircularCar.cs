using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CircularCar : MonoBehaviour
{
    [Header("回転中心設定")]
    [SerializeField] private Transform centerPoint;
    [SerializeField] private Vector3 centerOffset = new Vector3(-207.8f, 23.9f, 204.54f);

    [Header("周回設定")]
    [SerializeField] private float speed = 13f;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private float radius = 25f;

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

    private Vector3 _center;
    private float _currentAngle;

    private void Start()
    {
        _center = centerPoint != null ? centerPoint.position : centerOffset;

        Vector3 offset = transform.position - _center;
        offset.y = 0;
        _currentAngle = Mathf.Atan2(offset.z, offset.x);
        
        if (radius <= 0.1f)
        {
            radius = offset.magnitude;
        }
    }

    private void Update()
    {
        if (!MapGameManager.IsGameStarted) return;

        float direction = clockwise ? -1f : 1f;
        _currentAngle += direction * (speed / radius) * Time.deltaTime;

        Vector3 nextPosition = new Vector3(
            _center.x + Mathf.Cos(_currentAngle) * radius,
            transform.position.y,
            _center.z + Mathf.Sin(_currentAngle) * radius
        );

        Vector3 moveDirection = (nextPosition - transform.position).normalized;
        if (moveDirection != Vector3.zero)
        {
            transform.forward = moveDirection;
        }

        transform.position = nextPosition;
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessHit(collision.gameObject, collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        // ClosestPoint がエラーを起こさないようフォールバックを設定
        Vector3 hitPoint = transform.position;
        try
        {
            hitPoint = other.ClosestPoint(transform.position);
        }
        catch
        {
            hitPoint = other.transform.position;
        }

        ProcessHit(other.gameObject, hitPoint);
    }

    private void ProcessHit(GameObject hitObject, Vector3 hitPoint)
    {
        // 1. 除外タグ判定（最初にチェックして不要な処理をスキップ）
        foreach (string ignoreTag in ignoreEffectTags)
        {
            if (!string.IsNullOrEmpty(ignoreTag) && hitObject.CompareTag(ignoreTag))
            {
                return;
            }
        }

        // 2. プレイヤー判定（ルートオブジェクトも含めて確実に取得）
        PlayerMovement pm = hitObject.GetComponentInParent<PlayerMovement>();
        if (pm == null) pm = hitObject.GetComponent<PlayerMovement>();
        if (pm == null) pm = hitObject.GetComponentInChildren<PlayerMovement>();

        if (pm != null)
        {
            // 車の進行方向（transform.forward）へ吹っ飛ばす
            Vector3 knockbackForce = (transform.forward * knockbackBackSpeed) + (Vector3.up * knockbackUpSpeed);
            pm.ApplyKnockback(knockbackForce, stunDuration);
            SpawnHitEffect(pm.transform.position + Vector3.up * 1.0f);
            return;
        }

        // 3. ボール（Ballタグ）判定
        if (hitObject.CompareTag("Ball") || hitObject.name.Contains("Ball"))
        {
            Rigidbody ballRb = hitObject.GetComponentInParent<Rigidbody>();
            if (ballRb == null) ballRb = hitObject.GetComponent<Rigidbody>();

            if (ballRb != null)
            {
                // 物理速度を一度リセットしてからノックバックを与える（確実に弾き飛ばすため）
                ballRb.linearVelocity = Vector3.zero;
                Vector3 ballKnockback = ((transform.forward * knockbackBackSpeed) + (Vector3.up * knockbackUpSpeed)) * ballKnockbackMultiplier;
                ballRb.AddForce(ballKnockback, ForceMode.Impulse);
            }
            return;
        }

        // 4. その他の衝突（弾など）
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

    private void OnDrawGizmosSelected()
    {
        Vector3 c = centerPoint != null ? centerPoint.position : centerOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(c, radius);
    }
}