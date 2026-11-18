using UnityEngine;
using Fusion;

public class RocketProjectileController : NetworkBehaviour
{
    [Header("移動設定")]
    public float speed = 25f;
    public float lifeTime = 5f;

    [Header("参照アセット")]
    [Tooltip("アセット付属の RocketExplosion プレハブをそのまま入れられます")]
    public GameObject rocketExplosionPrefab; 
    public MeshRenderer projectileMesh;
    public AudioSource inFlightAudioSource;
    public ParticleSystem disableOnHit;

    private bool targetHit;
    private TickTimer lifeTimer;

    public override void Spawned()
    {
        // 指定時間後に自動削除するためのタイマー設定
        lifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
    }

    public override void FixedUpdateNetwork()
    {
        if (targetHit) return;

        // 前方に移動
        transform.position += transform.forward * (speed * Runner.DeltaTime);

        // 寿命（lifeTime）が来たらサーバー側でDespawn
        if (lifeTimer.Expired(Runner))
        {
            if (HasStateAuthority) Runner.Despawn(Object);
        }
    }

    // 通常の物理衝突（Collider）の場合
    private void OnCollisionEnter(Collision collision)
    {
        ProcessHit(collision.gameObject);
    }

    // トリガー接触（Is Trigger がONのコライダー / 車など）の場合
    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other.gameObject);
    }

    // 着弾・爆発共通処理
    private void ProcessHit(GameObject hitObject)
    {
        if (targetHit) return;

        // 撃ったプレイヤー自身のコライダーと接触した場合は無視する（即時爆発の回避）
        var player = hitObject.GetComponentInParent<PlayerMovement>();
        if (player == null) player = hitObject.GetComponent<PlayerMovement>();

        if (player != null && player.Object != null && player.Object.InputAuthority == Object.InputAuthority)
        {
            return;
        }

        targetHit = true;
        
        // 描画・サウンド・パーティクルの停止処理
        if (projectileMesh != null) projectileMesh.enabled = false;
        if (inFlightAudioSource != null) inFlightAudioSource.Stop();
        if (disableOnHit != null) disableOnHit.Stop();

        // 爆発エフェクトの生成
        if (rocketExplosionPrefab != null)
        {
            Instantiate(rocketExplosionPrefab, transform.position, Quaternion.identity);
        }

        // ネットワークオブジェクトの破棄（サーバー権限）
        if (HasStateAuthority)
        {
            Runner.Despawn(Object);
        }
    }
}