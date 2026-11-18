using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class BowlingBallImpact : NetworkBehaviour
{
    private bool hasCollidedWithPin = false;

    [Header("スコア判定設定")]
    [SerializeField] private float scoreCalculationDelay = 5f;

    [Header("物理演算の最適化")]
    [Tooltip("ピンの物理演算をオンにする半径")]
    [SerializeField] private float wakeUpRadius = 8f;
    [Tooltip("ピンのレイヤー（無駄な判定を防ぐためピン専用レイヤーを指定）")]
    [SerializeField] private LayerMask pinLayer;

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

    // 毎フレーム、ボールの周囲のピンを検知して物理演算をオンにする
    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            Vector3 checkPosition = transform.position;

            // ★追加: ボールが高速で動いている場合、少し先の位置を判定基準にする
            if (rb != null)
            {
                // 現在の速度 × 数フレーム先の位置を予測して範囲を広げる
                checkPosition += rb.linearVelocity * Runner.DeltaTime * 3f;
            }

            Collider[] colliders = Physics.OverlapSphere(checkPosition, wakeUpRadius, pinLayer);
            foreach (var col in colliders)
            {
                BowlingPin pin = col.GetComponent<BowlingPin>();
                if (pin != null)
                {
                        pin.WakeUp();
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (HasStateAuthority && !hasCollidedWithPin && collision.gameObject.CompareTag("Pin"))
        {
            hasCollidedWithPin = true;
            hitPosition = transform.position;

            StartCoroutine(CalculateScoreAndRegisterRoutine());
            Rpc_SwitchToSpectatorMode(Object.InputAuthority);
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
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SwitchToSpectatorMode(PlayerRef targetPlayer)
    {
        if (Runner.LocalPlayer == targetPlayer)
        {
            // ★修正: ボールから探すのではなく、ローカルプレイヤーのPlayerMovementを直接無効化する
            if (PlayerMovement.Local != null)
            {
                PlayerMovement.Local.enabled = false;
            }
            SwitchToSpectatorCamera();
        }
    }

    // ★追加・修正: 観戦モードへの切り替え処理
    private void SwitchToSpectatorCamera()
    {
        isSpectating = true;
        spectatorCam = FindAnyObjectByType<CinemachineCamera>();
        
        CycleSpectatorTarget(1); // 最初の観戦ターゲットへ切り替え
        Debug.Log("観戦モードに移行しました。スペースキーで視点変更できます。");
    }

    private void Update()
    {
        if (!isSpectating) return;

        // スペースキーが押されたら次のプレイヤーへ視点を切り替える
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CycleSpectatorTarget(1);
        }
    }

    private void CycleSpectatorTarget(int direction)
    {
        if (spectatorCam == null) return;

        // シーン内の全プレイヤーを取得し、リストを更新
        spectatorTargets.Clear();
        var allPlayers = FindObjectsByType<PlayerMovement>();
        
        foreach (var p in allPlayers)
        {
            // 自分自身は観戦対象から外す（自分も見たい場合は if文を消してください）
            if (p != PlayerMovement.Local)
            {
                spectatorTargets.Add(p);
            }
        }

        if (spectatorTargets.Count == 0) return; // 誰もいない場合は何もしない

        currentSpectateIndex += direction;

        // インデックスが範囲外に出たらループさせる
        if (currentSpectateIndex >= spectatorTargets.Count) currentSpectateIndex = 0;
        if (currentSpectateIndex < 0) currentSpectateIndex = spectatorTargets.Count - 1;

        var target = spectatorTargets[currentSpectateIndex];

        // カメラの追従先を変更
        spectatorCam.Target.TrackingTarget = target.transform;
        spectatorCam.Target.LookAtTarget = target.transform;
    }
}