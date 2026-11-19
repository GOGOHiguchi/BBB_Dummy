using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class SatelliteLaserController : NetworkBehaviour
{
    [Header("レーザー設定")]
    [SerializeField] private float attackDuration = 3.44f;
    [SerializeField] private float explosionRadius = 15f;
    [SerializeField] private float explosionForce = 30f;
    [SerializeField] private float upwardsModifier = 10f;
    [SerializeField] private float stunDuration = 4f;

    [Header("判定負荷設定")]
    [SerializeField, Min(16)] private int overlapBufferSize = 128;

    [Header("エフェクト")]
    [SerializeField] private GameObject laserWarningEffectPrefab;
    [SerializeField] private GameObject laserAttackEffectPrefab;
    [SerializeField] private GameObject explosionEffectPrefab;

    [Networked] private TickTimer AttackTimer { get; set; }

    private GameObject spawnedWarningEffect;
    private GameObject spawnedAttackEffect;

    // 一度攻撃したプレイヤーを記録
    private HashSet<PlayerRef> hitPlayers = new HashSet<PlayerRef>();

    // 一度攻撃したボールを記録
    private HashSet<NetworkId> hitBalls = new HashSet<NetworkId>();

    private bool isAttackFinished;

    // OverlapSphereNonAlloc用。ネットワークTickごとの配列生成を防ぐ。
    private Collider[] overlapResults;
    private readonly HashSet<PlayerRef> checkedPlayersThisTick = new HashSet<PlayerRef>();
    private bool hasWarnedOverlapBufferFull;

    private void Awake()
    {
        overlapResults = new Collider[Mathf.Max(16, overlapBufferSize)];
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            AttackTimer =
                TickTimer.CreateFromSeconds(Runner, attackDuration);
        }

        // 警告範囲を生成
        if (laserWarningEffectPrefab != null)
        {
            spawnedWarningEffect = Instantiate(
                laserWarningEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        // 攻撃用レーザーを同時に生成
        if (laserAttackEffectPrefab != null)
        {
            spawnedAttackEffect = Instantiate(
                laserAttackEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        Debug.Log("サテライトレーザー照射開始！");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || isAttackFinished)
            return;

        // 攻撃中は毎ネットワークTickで判定
        CheckAttackTargets();

        // 3.44秒経過したら終了
        if (AttackTimer.Expired(Runner))
        {
            EndAttack();
        }
    }

    private void CheckAttackTargets()
    {
        if (overlapResults == null || overlapResults.Length != Mathf.Max(16, overlapBufferSize))
        {
            overlapResults = new Collider[Mathf.Max(16, overlapBufferSize)];
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            explosionRadius,
            overlapResults
        );

        // バッファ上限に達した場合は対象を取りこぼす可能性があるため、一度だけ警告する。
        if (hitCount == overlapResults.Length && !hasWarnedOverlapBufferFull)
        {
            hasWarnedOverlapBufferFull = true;
            Debug.LogWarning(
                $"SatelliteLaserController: Overlap buffer is full ({overlapResults.Length}). " +
                "Increase Overlap Buffer Size if targets may be missed.",
                this
            );
        }

        checkedPlayersThisTick.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];

            if (hit == null)
                continue;
        {
            // =========================
            // プレイヤーへの攻撃
            // =========================

            PlayerMovement player =
                hit.GetComponentInParent<PlayerMovement>();

            if (player != null)
            {
                if (player.Object == null)
                    continue;

                PlayerRef targetRef =
                    player.Object.InputAuthority;

                // 使用者本人を除外
                if (targetRef == Object.InputAuthority)
                    continue;

                // 攻撃済み、または同じネットワークTick内で確認済みなら除外。
                // シールド中の相手への再判定は次のTick以降に行う。
                if (hitPlayers.Contains(targetRef) || !checkedPlayersThisTick.Add(targetRef))
                    continue;

                Vector3 direction =
                    player.transform.position - transform.position;

                direction.y = 0f;

                if (direction.sqrMagnitude < 0.001f)
                    direction = Vector3.forward;

                Vector3 force =
                    direction.normalized * explosionForce +
                    Vector3.up * upwardsModifier;

                // 無敵・シールドで防がれた場合は命中扱いにしない
                if (!player.ApplyKnockback(force, stunDuration))
                    continue;

                hitPlayers.Add(targetRef);

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RecordInterference(
                        Object.InputAuthority
                    );
                }

                continue;
            }

            // =========================
            // ボールへの攻撃
            // =========================

            Rigidbody rb = hit.attachedRigidbody;

            if (rb == null)
                continue;

            NetworkObject ballObject =
                rb.GetComponent<NetworkObject>();

            if (ballObject == null)
                ballObject = rb.GetComponentInParent<NetworkObject>();

            if (ballObject == null)
                continue;

            // 自分のボールを除外
            if (ballObject.InputAuthority == Object.InputAuthority)
                continue;

            // ボールの物理状態は、そのボールのState Authorityだけが変更する
            if (!ballObject.HasStateAuthority)
                continue;

            // 攻撃済みボールを除外
            if (hitBalls.Contains(ballObject.Id))
                continue;

            if (rb.isKinematic)
                continue;

            Vector3 forceDir =
                rb.position - transform.position;

            if (forceDir.sqrMagnitude < 0.001f)
                forceDir = Vector3.forward;

            forceDir.Normalize();

            rb.AddForce(
                forceDir * explosionForce +
                Vector3.up * upwardsModifier,
                ForceMode.Impulse
            );

            hitBalls.Add(ballObject.Id);
        }
    }

    private void EndAttack()
    {
        isAttackFinished = true;

        Debug.Log("サテライトレーザー照射終了！");

        Runner.Despawn(Object);
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState
    )
    {
        if (spawnedWarningEffect != null)
        {
            Destroy(spawnedWarningEffect);
        }

        if (spawnedAttackEffect != null)
        {
            Destroy(spawnedAttackEffect);
        }

        // 未完成なのでNoneでも問題なし
        if (explosionEffectPrefab != null)
        {
            Instantiate(
                explosionEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }
    }
}
