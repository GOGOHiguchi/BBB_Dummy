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
        if (HasStateAuthority)
        {
            lifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 弾の位置・寿命は State Authority のみが管理する。
        // クライアント側は NetworkTransform の補間結果を表示する。
        if (!HasStateAuthority || targetHit)
            return;

        transform.position += transform.forward * (speed * Runner.DeltaTime);

        if (lifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessHit(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other.gameObject);
    }

    private void ProcessHit(GameObject hitObject)
    {
        // 衝突結果の確定は State Authority に一本化する。
        if (!HasStateAuthority || targetHit)
            return;

        // 発射者自身との接触は無視する。
        PlayerMovement player = hitObject.GetComponentInParent<PlayerMovement>();
        if (player != null &&
            player.Object != null &&
            player.Object.InputAuthority == Object.InputAuthority)
        {
            return;
        }

        targetHit = true;

        // 爆発演出は全クライアントで一度だけ再生する。
        Rpc_PlayImpactEffect(transform.position);

        Runner.Despawn(Object);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayImpactEffect(Vector3 impactPosition)
    {
        if (projectileMesh != null)
            projectileMesh.enabled = false;

        if (inFlightAudioSource != null)
            inFlightAudioSource.Stop();

        if (disableOnHit != null)
            disableOnHit.Stop();

        if (rocketExplosionPrefab != null)
        {
            Instantiate(rocketExplosionPrefab, impactPosition, Quaternion.identity);
        }
    }
}
