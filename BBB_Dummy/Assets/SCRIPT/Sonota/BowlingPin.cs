using UnityEngine;

public class BowlingPin : MonoBehaviour
{
    [Header("倒れ判定の設定")]
    [Tooltip("ワールドの垂直方向から何度傾いたら倒れたとみなすか")]
    [SerializeField] private float knockdownAngle = 30f;

    // ★追加: スコアとして集計済みかどうかのフラグ
    public bool IsScored { get; set; } = false;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    public void WakeUp()
    {
        if (rb != null && rb.isKinematic)
        {
            rb.isKinematic = false;
        }
    }

    /// <summary>
    /// ピンが倒れているかどうかを判定
    /// </summary>
    public bool IsKnockedDown()
    {
        // ピンの上方向ベクトルと、ワールドの上方向ベクトルの角度を測定
        float angle = Vector3.Angle(transform.up, Vector3.up);
        
        // 設定値（例: 30度）以上傾いていれば倒れたとみなす
        return angle > knockdownAngle;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 侵入してきたオブジェクトがボールまたは他のピンであれば起床する
        if (other.CompareTag("Ball") || other.CompareTag("Pin"))
        {
            WakeUp();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 自分が眠っている状態で、ピンかボールがぶつかってきたら起きる
        if (rb != null && rb.isKinematic)
        {
            if (collision.gameObject.CompareTag("Pin") || collision.gameObject.CompareTag("Ball"))
            {
                WakeUp();
            }
        }

        // 自分が起きていて、相手が眠っているピンなら相手を起こす（連鎖を確実にするため）
        if (rb != null && !rb.isKinematic)
        {
            if (collision.gameObject.CompareTag("Pin"))
            {
                var otherPin = collision.gameObject.GetComponent<BowlingPin>();
                if (otherPin != null)
                {
                    otherPin.WakeUp();
                }
            }
        }
    }
}