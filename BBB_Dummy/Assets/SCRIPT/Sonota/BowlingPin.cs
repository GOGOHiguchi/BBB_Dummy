using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BowlingPin : MonoBehaviour
{
    [Header("倒れ判定の設定")]
    [Tooltip("ワールドの垂直方向から何度傾いたら倒れたとみなすか")]
    [SerializeField] private float knockdownAngle = 30f;

    [Header("吹き飛び演出")]
    [Tooltip("衝突速度に掛ける追加の吹き飛び倍率")]
    [SerializeField, Range(0f, 0.5f)] private float impactVelocityMultiplier = 0.16f;
    [Tooltip("衝突時に追加する最低速度")]
    [SerializeField] private float minimumImpactVelocity = 2.0f;
    [Tooltip("衝突時に追加する速度の上限")]
    [SerializeField] private float maximumExtraVelocity = 6.0f;
    [Tooltip("吹き飛び方向に加える上向き成分")]
    [SerializeField, Range(0f, 0.5f)] private float upwardImpactBoost = 0.12f;

    // スコアとして集計済みかどうか
    public bool IsScored { get; set; } = false;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            // 初期状態では物理演算を止め、大量のピンによる負荷を抑える。
            // ボールが近づいた段階でWakeUpし、衝突前にDynamicへ切り替える。
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.maxAngularVelocity = 35f;
        }
    }

    public void WakeUp()
    {
        if (rb == null || !rb.isKinematic) return;

        rb.isKinematic = false;
        rb.WakeUp();
    }

    /// <summary>
    /// ピンが倒れているかどうかを判定
    /// </summary>
    public bool IsKnockedDown()
    {
        float angle = Vector3.Angle(transform.up, Vector3.up);
        return angle > knockdownAngle;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball") || other.CompareTag("Pin"))
        {
            WakeUp();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (rb == null) return;

        bool hitByBall = collision.gameObject.CompareTag("Ball");
        bool hitByPin = collision.gameObject.CompareTag("Pin");

        // 万一、事前起動より先に衝突した場合の保険。
        if (rb.isKinematic && (hitByBall || hitByPin))
        {
            WakeUp();
        }

        if (hitByBall)
        {
            // 事前にDynamic化できていれば通常の衝突力に追加して吹き飛びを強める。
            ApplyImpactBoost(collision.relativeVelocity);
        }
        else if (hitByPin && !rb.isKinematic)
        {
            // 動いているピンがまだ眠っている隣のピンに当たった場合、
            // 相手を起こして連鎖的に倒れやすくする。
            BowlingPin otherPin = collision.gameObject.GetComponent<BowlingPin>();
            if (otherPin != null)
            {
                otherPin.WakeUp();
                otherPin.ApplyImpactBoost(collision.relativeVelocity);
            }
        }
    }

    private void ApplyImpactBoost(Vector3 incomingVelocity)
    {
        if (rb == null || rb.isKinematic) return;

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(incomingVelocity, Vector3.up);
        if (horizontalDirection.sqrMagnitude < 0.001f)
        {
            horizontalDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (horizontalDirection.sqrMagnitude < 0.001f) return;

        horizontalDirection.Normalize();

        float extraSpeed = Mathf.Clamp(
            incomingVelocity.magnitude * impactVelocityMultiplier,
            minimumImpactVelocity,
            maximumExtraVelocity
        );

        Vector3 launchDirection = (horizontalDirection + Vector3.up * upwardImpactBoost).normalized;
        rb.AddForce(launchDirection * extraSpeed, ForceMode.VelocityChange);

        // 横方向の回転も少し加え、ピンらしい転がり・ tumbling を出す。
        Vector3 spinAxis = Vector3.Cross(Vector3.up, horizontalDirection).normalized;
        if (spinAxis.sqrMagnitude > 0.001f)
        {
            rb.AddTorque(spinAxis * (extraSpeed * 0.35f), ForceMode.VelocityChange);
        }
    }
}
