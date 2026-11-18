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
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            explosionRadius
        );

        foreach (Collider hit in hits)
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

                // 攻撃済みなら除外
                if (hitPlayers.Contains(targetRef))
                    continue;

                Vector3 direction =
                    player.transform.position - transform.position;

                direction.y = 0f;

                if (direction.sqrMagnitude < 0.001f)
                    direction = Vector3.forward;

                Vector3 force =
                    direction.normalized * explosionForce +
                    Vector3.up * upwardsModifier;

                player.ApplyKnockback(force, stunDuration);

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
