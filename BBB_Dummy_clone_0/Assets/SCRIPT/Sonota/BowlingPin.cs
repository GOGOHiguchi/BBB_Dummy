using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BowlingPin : MonoBehaviour
{
    [Header("倒れ判定の設定")]
    [Tooltip("ワールドの垂直方向から何度傾いたら倒れたとみなすか")]
    [SerializeField] private float knockdownAngle = 30f;

    [Header("吹き飛び演出")]
    [Tooltip("衝突速度に掛ける追加の吹き飛び倍率")]
    [SerializeField, Range(0f, 0.5f)] private float impactVelocityMultiplier = 0.22f;
    [Tooltip("衝突時に追加する最低速度")]
    [SerializeField] private float minimumImpactVelocity = 2.5f;
    [Tooltip("衝突時に追加する速度の上限")]
    [SerializeField] private float maximumExtraVelocity = 4.5f;
    [Tooltip("吹き飛び方向に加える上向き成分")]
    [SerializeField, Range(0f, 0.5f)] private float upwardImpactBoost = 0.08f;

    // スコアとして集計済みかどうか
    public bool IsScored { get; set; } = false;

    private Rigidbody rb;
    // 1本のピンに対する追加Impulseは、ボール直撃・連鎖を合わせて1回だけ。
    private bool hasReceivedImpactBoost;

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
            ApplyImpactBoost(collision.relativeVelocity, impactVelocityMultiplier, minimumImpactVelocity, maximumExtraVelocity, upwardImpactBoost, 0.25f);
        }
        else if (hitByPin && !rb.isKinematic)
        {
            // 動いているピンがまだ眠っている隣のピンに当たった場合、
            // 相手を起こして連鎖的に倒れやすくする。
            BowlingPin otherPin = collision.gameObject.GetComponent<BowlingPin>();
            if (otherPin != null)
            {
                otherPin.WakeUp();

                Vector3 chainDirection = otherPin.transform.position - transform.position;
                Vector3 chainVelocity = chainDirection.sqrMagnitude > 0.001f
                    ? chainDirection.normalized * collision.relativeVelocity.magnitude
                    : collision.relativeVelocity;

                otherPin.ApplyImpactBoost(chainVelocity, 0.10f, 0.8f, 2.5f, 0.05f, 0.20f);
            }
        }
    }

    private void ApplyImpactBoost(
        Vector3 incomingVelocity,
        float velocityMultiplier,
        float minimumExtraVelocity,
        float maximumExtraVelocity,
        float upwardBoost,
        float spinMultiplier)
    {
        if (rb == null || rb.isKinematic || hasReceivedImpactBoost) return;

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(incomingVelocity, Vector3.up);
        if (horizontalDirection.sqrMagnitude < 0.001f)
        {
            horizontalDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (horizontalDirection.sqrMagnitude < 0.001f) return;

        horizontalDirection.Normalize();

        float extraSpeed = Mathf.Clamp(
            incomingVelocity.magnitude * velocityMultiplier,
            minimumExtraVelocity,
            maximumExtraVelocity
        );

        hasReceivedImpactBoost = true;
        Vector3 launchDirection = (horizontalDirection + Vector3.up * upwardBoost).normalized;
        rb.AddForce(launchDirection * extraSpeed, ForceMode.VelocityChange);

        // 横方向の回転も少し加え、ピンらしい転がり・ tumbling を出す。
        Vector3 spinAxis = Vector3.Cross(Vector3.up, horizontalDirection).normalized;
        if (spinAxis.sqrMagnitude > 0.001f)
        {
            rb.AddTorque(spinAxis * (extraSpeed * spinMultiplier), ForceMode.VelocityChange);
        }
    }
}
