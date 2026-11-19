using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class BowlingBallImpact : NetworkBehaviour
{
    private bool hasCollidedWithPin = false;

    [Header("ピン撃破時のプレイヤー処理")]
    [Tooltip("ピンに当たったプレイヤーを後方へ吹き飛ばす水平速度")]
    [SerializeField] private float finishKnockbackSpeed = 22f;
    [Tooltip("吹き飛ばし時の上方向速度")]
    [SerializeField] private float finishKnockbackUpSpeed = 8f;
    [Tooltip("吹き飛ばしの継続時間")]
    [SerializeField] private float finishKnockbackDuration = 2.5f;

    [Header("スコア判定・観戦移行設定")]
    [Tooltip("衝突後、ピンの転倒数を集計するまでの待機時間")]
    [SerializeField] private float scoreCalculationDelay = 5f;
    [Tooltip("集計後、ピンの倒れた状態を見せてから観戦モードへ移行する追加時間")]
    [SerializeField] private float spectatorDelayAfterScore = 2f;

    [Header("物理演算の最適化")]
    [Tooltip("ピンの物理演算をオンにする半径")]
    [SerializeField] private float wakeUpRadius = 2.5f;
    [Tooltip("ピンのレイヤー（Pinレイヤーを指定）")]
    [SerializeField] private LayerMask pinLayer;

    private const int PinOverlapCapacity = 256;
    private readonly Collider[] pinOverlapBuffer = new Collider[PinOverlapCapacity];
    private bool pinBufferWarningLogged;

    [Networked] public Vector3 ThrowPosition { get; set; }

    private Vector3 hitPosition;

    private bool isSpectating = false;
    private CinemachineCamera spectatorCam;
    private List<PlayerMovement> spectatorTargets = new List<PlayerMovement>();
    private int currentSpectateIndex = -1;

    public void SetThrowPosition(Vector3 pos)
    {
        ThrowPosition = pos;
    }

    // ボールの少し先にあるピンを事前に起こし、Kinematicなピンが壁のように
    // ボールを押し返してしまう状況を防ぐ。
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        Vector3 checkPosition = transform.position;

        if (rb != null)
        {
            // 高速移動時も衝突より前にピンを起こせるよう、数Tick先を確認する。
            checkPosition += rb.linearVelocity * Runner.DeltaTime * 4f;
        }

        int effectivePinMask = pinLayer.value != 0
            ? pinLayer.value
            : LayerMask.GetMask("Pin");

        if (effectivePinMask == 0)
        {
            if (!pinBufferWarningLogged)
            {
                Debug.LogWarning("[BowlingBallImpact] Pinレイヤーが見つかりません。BowlingBall.prefabのPin Layer Maskを確認してください。", this);
                pinBufferWarningLogged = true;
            }
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            checkPosition,
            wakeUpRadius,
            pinOverlapBuffer,
            effectivePinMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            BowlingPin pin = pinOverlapBuffer[i].GetComponent<BowlingPin>();
            if (pin != null)
            {
                pin.WakeUp();
            }

            pinOverlapBuffer[i] = null;
        }

        if (hitCount == PinOverlapCapacity && !pinBufferWarningLogged)
        {
            Debug.LogWarning("[BowlingBallImpact] 起床範囲内のColliderがバッファ上限に達しました。必要に応じてPinOverlapCapacityを増やしてください。", this);
            pinBufferWarningLogged = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (HasStateAuthority && !hasCollidedWithPin && collision.gameObject.CompareTag("Pin"))
        {
            hasCollidedWithPin = true;
            hitPosition = transform.position;

            // 接触しただけでは撃破扱いにせず、ピンの倒れ判定が成立するまで待つ。
            Vector3 contactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            StartCoroutine(WaitForKnockdownThenFinishRoutine(contactPoint));

            // スコア集計・観戦移行のタイミングは従来どおり維持する。
            StartCoroutine(CalculateScoreAndRegisterRoutine());
        }
    }

    private IEnumerator WaitForKnockdownThenFinishRoutine(Vector3 contactPoint)
    {
        // ピンが倒れるまで監視する。スコア集計待ち時間を上限にして、
        // ピンが倒れなかった場合は撃破演出を開始しない。
        float elapsed = 0f;
        float timeout = Mathf.Max(0f, scoreCalculationDelay);

        while (elapsed < timeout)
        {
            if (PinManager.Instance != null && PinManager.Instance.HasAnyKnockedDown())
            {
                BeginOwnerPinFinish(contactPoint);
                yield break;
            }

            yield return new WaitForSeconds(0.05f);
            elapsed += 0.05f;
        }

        // 最後の待機区間で倒れたケースも拾う。
        if (PinManager.Instance != null && PinManager.Instance.HasAnyKnockedDown())
        {
            BeginOwnerPinFinish(contactPoint);
        }
        else
        {
            Debug.LogWarning("[BowlingBallImpact] ピンの倒れ判定が成立しなかったため、撃破演出を開始しませんでした。", this);
        }
    }

    private void BeginOwnerPinFinish(Vector3 contactPoint)
    {
        if (Object.InputAuthority == PlayerRef.None)
        {
            Debug.LogWarning("[BowlingBallImpact] ボールのInputAuthorityがないため、撃破プレイヤーを特定できません。", this);
            return;
        }

        NetworkObject playerObject = Runner.GetPlayerObject(Object.InputAuthority);
        if (playerObject == null)
        {
            Debug.LogWarning($"[BowlingBallImpact] PlayerObjectが見つかりません: {Object.InputAuthority}", this);
            return;
        }

        PlayerMovement player = playerObject.GetComponent<PlayerMovement>();
        if (player == null)
        {
            Debug.LogWarning("[BowlingBallImpact] PlayerMovementが見つかりません。", playerObject);
            return;
        }

        // ピンの衝突地点からプレイヤーがいる方向へ押し戻し、ピンの近くに残らないようにする。
        Vector3 awayDirection = player.transform.position - contactPoint;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude < 0.01f)
        {
            Rigidbody ballRb = GetComponent<Rigidbody>();
            Vector3 ballVelocity = ballRb != null ? ballRb.linearVelocity : transform.forward;
            awayDirection = -ballVelocity;
            awayDirection.y = 0f;
        }

        if (awayDirection.sqrMagnitude < 0.01f)
        {
            awayDirection = -transform.forward;
            awayDirection.y = 0f;
        }

        awayDirection.Normalize();

        Vector3 knockbackVelocity =
            awayDirection * finishKnockbackSpeed +
            Vector3.up * finishKnockbackUpSpeed;

        player.BeginPinFinish(knockbackVelocity, finishKnockbackDuration);
    }

    private IEnumerator CalculateScoreAndRegisterRoutine()
    {
        yield return new WaitForSeconds(scoreCalculationDelay);

        int pinScore = 0;
        if (PinManager.Instance != null)
        {
            pinScore = PinManager.Instance.CountAndMarkKnockedPins();
        }

        if (GameManager.Instance != null && Object.InputAuthority != PlayerRef.None)
        {
            if (pinScore > 0)
            {
                float distance = Vector3.Distance(ThrowPosition, hitPosition);
                GameManager.Instance.RegisterHitDistance(Object.InputAuthority, distance);
            }
            GameManager.Instance.RegisterPinHit(Object.InputAuthority, pinScore);
        }

        // 集計後も少しだけピンを見せてから観戦モードへ移行する。
        if (spectatorDelayAfterScore > 0f)
        {
            yield return new WaitForSeconds(spectatorDelayAfterScore);
        }

        Rpc_SwitchToSpectatorMode(Object.InputAuthority);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SwitchToSpectatorMode(PlayerRef targetPlayer)
    {
        if (Runner.LocalPlayer == targetPlayer)
        {
            if (PlayerMovement.Local != null)
            {
                PlayerMovement.Local.enabled = false;
            }
            SwitchToSpectatorCamera();
        }
    }

    private void SwitchToSpectatorCamera()
    {
        isSpectating = true;
        spectatorCam = FindAnyObjectByType<CinemachineCamera>();

        CycleSpectatorTarget(1);
        Debug.Log("観戦モードに移行しました。スペースキーで視点変更できます。");
    }

    private void Update()
    {
        if (!isSpectating) return;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CycleSpectatorTarget(1);
        }
    }

    private void CycleSpectatorTarget(int direction)
    {
        if (spectatorCam == null) return;

        spectatorTargets.Clear();
        var allPlayers = FindObjectsByType<PlayerMovement>();

        foreach (var p in allPlayers)
        {
            if (p != PlayerMovement.Local)
            {
                spectatorTargets.Add(p);
            }
        }

        if (spectatorTargets.Count == 0) return;

        currentSpectateIndex += direction;

        if (currentSpectateIndex >= spectatorTargets.Count) currentSpectateIndex = 0;
        if (currentSpectateIndex < 0) currentSpectateIndex = spectatorTargets.Count - 1;

        var target = spectatorTargets[currentSpectateIndex];

        spectatorCam.Target.TrackingTarget = target.transform;
        spectatorCam.Target.LookAtTarget = target.transform;
    }
}
