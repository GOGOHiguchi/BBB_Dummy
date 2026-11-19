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
    private TickTimer despawnTimer;
    private Collider[] projectileColliders;

    // PrefabにNetworkTransformがないため、弾のTransformをNetworked値で同期する。
    [Networked] private Vector3 SyncedPosition { get; set; }
    [Networked] private Quaternion SyncedRotation { get; set; }

    // RPCを受信する時間を確保してから、State Authorityが弾をDespawnする。
    private const float ImpactDespawnDelay = 0.35f;

    public override void Spawned()
    {
        projectileColliders = GetComponentsInChildren<Collider>(true);

        if (HasStateAuthority)
        {
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
            lifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
        }
        else
        {
            transform.SetPositionAndRotation(SyncedPosition, SyncedRotation);
        }
    }

    public override void Render()
    {
        // State Authority以外は、ネットワークで届いた位置・回転を表示する。
        if (!HasStateAuthority)
        {
            transform.SetPositionAndRotation(SyncedPosition, SyncedRotation);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 弾の位置・寿命・着弾後の破棄は State Authority のみが管理する。
        if (!HasStateAuthority)
            return;

        // 着弾後は移動・追加衝突を行わず、RPCの到達時間を確保してから破棄する。
        if (targetHit)
        {
            if (despawnTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }

            return;
        }

        transform.position += transform.forward * (speed * Runner.DeltaTime);
        SyncedPosition = transform.position;
        SyncedRotation = transform.rotation;

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

        // 同じ弾で複数回着弾処理が走らないよう、先に着弾済みにする。
        targetHit = true;
        despawnTimer = TickTimer.CreateFromSeconds(Runner, ImpactDespawnDelay);

        // 爆発演出を全クライアントへ通知する。
        // Despawnはここでは行わず、FixedUpdateNetworkで遅延実行する。
        Rpc_PlayImpactEffect(transform.position);
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

        // 爆発中に弾のColliderが他の物体へ反応しないようにする。
        if (projectileColliders != null)
        {
            foreach (Collider projectileCollider in projectileColliders)
            {
                if (projectileCollider != null)
                    projectileCollider.enabled = false;
            }
        }

        if (rocketExplosionPrefab != null)
        {
            Instantiate(rocketExplosionPrefab, impactPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("[RocketProjectileController] rocketExplosionPrefab が未設定です。");
        }
    }
}
