using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class BowlingBallImpact : NetworkBehaviour
{
    private bool hasCollidedWithPin = false;

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

            // ピンの転倒とスコア集計が終わるまで、プレイヤー視点を維持する。
            StartCoroutine(CalculateScoreAndRegisterRoutine());
        }
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
