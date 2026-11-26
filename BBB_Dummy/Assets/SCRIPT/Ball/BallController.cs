using UnityEngine;

public class BallController : MonoBehaviour
{
    public enum BallType { Speed, Power, Curve }
    public BallType type;

    private Rigidbody rb;

    [Header("ステータス設定")]
    public float moveSpeed = 20f;
    public float curveForce = 5f; // カーブボール用の横力

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        SetupBallProperties();
    }

    // タイプごとの物理パラメータ初期設定
    void SetupBallProperties()
    {
        switch (type)
        {
            case BallType.Speed:
                rb.mass = 0.5f;
                rb.linearDamping = 0.04f; // 空気抵抗を減らしてスピードUP
                break;

            case BallType.Power:
                rb.mass = 3.0f;
                rb.linearDamping = 0.2f;
                break;

            case BallType.Curve:
                rb.mass = 1.0f;
                rb.linearDamping = 0.1f;
                break;
        }
    }

    void FixedUpdate()
    {
        // カーブボールの場合、前進方向に対して横向きの力を継続して加える
        if (type == BallType.Curve && rb.linearVelocity.magnitude > 0.1f)
        {
            // 進行方向の右向きベクトルを取得して力を加える
            Vector3 curveDirection = Vector3.Cross(rb.linearVelocity.normalized, Vector3.up);
            rb.AddForce(-curveDirection * curveForce, ForceMode.Acceleration);
        }
    }
}