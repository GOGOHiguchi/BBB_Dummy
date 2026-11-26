using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LobbyBallWarpManager : MonoBehaviour
{
    [Header("ワープ判定（X軸）")]
    [Tooltip("ピンの裏のX座標。このラインを超えたボールをワープさせる")]
    [SerializeField] private float triggerX = 20f;

    [Tooltip("Xが大きくなる方向（+X）に投げる場合はチェック、小さくなる方向（-X）なら外す")]
    [SerializeField] private bool isPositiveXDirection = true;

    [Tooltip("ラインを超えてからワープするまでの待機時間（秒）")]
    [SerializeField] private float warpDelay = 2.0f;

    [Header("ワープ先の位置設定")]
    [Tooltip("チェックを入れると、YとZはそのまま（または指定高さ）でXだけ「Return X」に書き換える。\n外すと下の「Return Point」の座標にワープする")]
    [SerializeField] private bool warpOnlyX = true;

    [Tooltip("ワープ先のX座標（Warp Only X がオンの時に使用）")]
    [SerializeField] private float returnX = -5f;

    [Tooltip("ボールリターン出口など、特定の座標に戻したい場合に入れる（Warp Only X がオフの時に使用）")]
    [SerializeField] private Transform returnPoint;

    [Header("対象ボールの絞り込み")]
    [Tooltip("特定のTagのボールだけ対象にする場合は入力（空欄ならシーン内の全BowlingBallを対象にする）")]
    [SerializeField] private string targetBallTag = "";

    [Header("物理リセット設定")]
    [Tooltip("ワープ時に転がる勢いと回転をゼロにする")]
    [SerializeField] private bool resetVelocity = true;

    [Tooltip("戻ってきた直後にリターン出口から少し転がり出させる速度（0ならその場に止まる）")]
    [SerializeField] private float ejectSpeed = 0f;

    private float _nextScanTime;
    private BowlingBall[] _cachedBalls;

    // ボールごとの「ワープ予定時刻（Time.time）」を記録する辞書
    private readonly Dictionary<BowlingBall, float> _pendingWarps = new Dictionary<BowlingBall, float>();

    private void FixedUpdate()
    {
        // 新しくスポーンしたプレイヤーのボールも拾えるよう、1秒ごとに一覧を更新
        if (Time.time >= _nextScanTime || _cachedBalls == null)
        {
            _cachedBalls = FindObjectsByType<BowlingBall>(FindObjectsSortMode.None);
            _nextScanTime = Time.time + 1.0f;
        }

        for (int i = 0; i < _cachedBalls.Length; i++)
        {
            BowlingBall ball = _cachedBalls[i];
            if (ball == null) continue;

            // タグ指定がある場合は一致するものだけ処理
            if (!string.IsNullOrEmpty(targetBallTag) && !ball.CompareTag(targetBallTag))
                continue;

            // プレイヤーが手に持っている最中のボールは対象外（待機予約があれば解除）
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null && rb.isKinematic)
            {
                _pendingWarps.Remove(ball);
                continue;
            }

            // ネットワーク同期オブジェクトの場合、権限のないクライアント側では動かさない
            NetworkObject netObj = ball.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsValid && !netObj.HasStateAuthority)
                continue;

            // X軸の判定ラインを超えたかチェック
            float currentX = ball.transform.position.x;
            bool passedLine = isPositiveXDirection
                ? currentX >= triggerX
                : currentX <= triggerX;

            if (passedLine)
            {
                // まだワープ予約されていなければ、現在時刻 + warpDelay 秒後に予約する
                if (!_pendingWarps.TryGetValue(ball, out float warpTime))
                {
                    _pendingWarps[ball] = Time.time + warpDelay;
                }
                // 予約時刻になったらワープ実行
                else if (Time.time >= warpTime)
                {
                    _pendingWarps.Remove(ball);
                    WarpBall(ball.transform, rb);
                }
            }
            else
            {
                // ラインより手前に戻っていたら予約をリセット
                _pendingWarps.Remove(ball);
            }
        }
    }

    private void WarpBall(Transform ballTransform, Rigidbody rb)
    {
        // 1. 速度・回転をリセット
        if (resetVelocity && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 2. ワープ先座標の計算
        Vector3 targetPos;
        Quaternion targetRot = ballTransform.rotation;

        if (warpOnlyX)
        {
            // YとZはそのまま、X座標だけ指定値（returnX）へワープ
            targetPos = ballTransform.position;
            targetPos.x = returnX;
        }
        else if (returnPoint != null)
        {
            // 指定したボールリターン出口（Transform）へワープ
            targetPos = returnPoint.position;
            targetRot = returnPoint.rotation;
        }
        else
        {
            targetPos = ballTransform.position;
            targetPos.x = returnX;
        }

        // 3. 座標適用＆物理同期
        ballTransform.SetPositionAndRotation(targetPos, targetRot);

        if (rb != null)
        {
            rb.position = targetPos;
            rb.rotation = targetRot;

            // ボールリターン出口から転がり出てくる演出
            if (ejectSpeed > 0f)
            {
                Vector3 ejectDir = returnPoint != null
                    ? returnPoint.forward
                    : (isPositiveXDirection ? Vector3.left : Vector3.right);
                rb.linearVelocity = ejectDir.normalized * ejectSpeed;
            }
        }
    }

    // Sceneビューでワープ判定ライン（赤）とワープ先ライン（緑）をX軸基準で視覚化
    private void OnDrawGizmos()
    {
        Vector3 center = transform.position;

        // 赤い線：このXを超えたらワープ（ピンの裏）
        Gizmos.color = Color.red;
        Vector3 triggerCenter = new Vector3(triggerX, center.y, center.z);
        Gizmos.DrawLine(triggerCenter + Vector3.back * 15f, triggerCenter + Vector3.forward * 15f);

        // 緑の線（または球）：ワープ先の位置
        Gizmos.color = Color.green;
        if (warpOnlyX)
        {
            Vector3 returnCenter = new Vector3(returnX, center.y, center.z);
            Gizmos.DrawLine(returnCenter + Vector3.back * 15f, returnCenter + Vector3.forward * 15f);
        }
        else if (returnPoint != null)
        {
            Gizmos.DrawWireSphere(returnPoint.position, 0.4f);
        }
    }
}