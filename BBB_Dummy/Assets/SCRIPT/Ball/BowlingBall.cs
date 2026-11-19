using Fusion;
using UnityEngine;

public enum BallType
{
    Speed, // スピードボール
    Power, // パワーボール
    Curve  // カーブボール
}

public class BowlingBall : NetworkBehaviour
{
    [Header("ネットワーク同期するボールタイプ")]
    [Networked, OnChangedRender(nameof(OnTypeChanged))]
    public BallType Type { get; set; } = BallType.Speed;

    [Header("物理設定パラメータ")]
    [SerializeField] private float speedMass = 1.0f;
    [SerializeField] private float powerMass = 3.0f;
    [SerializeField] private float curveMass = 1.8f;

    [Header("最高速度制限（飛びすぎ防止）")]
    [SerializeField] private float speedMaxVelocity = 18f;
    [SerializeField] private float powerMaxVelocity = 16f;
    [SerializeField] private float curveMaxVelocity = 17f;

    [Header("カーブボール専用パラメータ")]
    [SerializeField] private float curveForce = 12f;

    [Header("物理マテリアル設定")]
    [SerializeField] private PhysicsMaterial speedPhysicMaterial;
    [SerializeField] private PhysicsMaterial powerPhysicMaterial;
    [SerializeField] private PhysicsMaterial curvePhysicMaterial;

    [Header("見た目（マテリアル）設定")]
    [SerializeField] private MeshRenderer ballMeshRenderer;
    [SerializeField] private Material speedMaterial;
    [SerializeField] private Material powerMaterial;
    [SerializeField] private Material curveMaterial;

    private Rigidbody rb;
    private Collider ballCollider;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ballCollider = GetComponent<Collider>();
    }

    public override void Spawned()
    {
        // 自分がこのボールの操作権（InputAuthority）を持っている場合、自分の保存選択データをホストへ送信
        if (HasInputAuthority)
        {
            RpcChangeBallType(PlayerBallData.SelectedType);
        }

        ApplyBallProperties();
    }

    // ★ クライアントからホストへボール変更を通知・実行させるRPC通信
    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void RpcChangeBallType(BallType newType)
    {
        Type = newType;
        ApplyBallProperties();
    }

    // 外部（BallSelectionUIなど）からボールタイプ変更を呼ぶ用
    public void SetBallType(BallType newType)
    {
        if (Object != null && Object.IsValid)
        {
            RpcChangeBallType(newType);
        }
    }

    private void OnTypeChanged()
    {
        ApplyBallProperties();
    }

    private void ApplyBallProperties()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (ballCollider == null) ballCollider = GetComponent<Collider>();

        switch (Type)
        {
            case BallType.Speed:
                rb.mass = speedMass;
                rb.linearDamping = 0.3f;
                if (ballCollider != null && speedPhysicMaterial != null) ballCollider.material = speedPhysicMaterial;
                if (ballMeshRenderer != null && speedMaterial != null) ballMeshRenderer.material = speedMaterial;
                break;

            case BallType.Power:
                rb.mass = powerMass;
                rb.linearDamping = 0.5f;
                if (ballCollider != null && powerPhysicMaterial != null) ballCollider.material = powerPhysicMaterial;
                if (ballMeshRenderer != null && powerMaterial != null) ballMeshRenderer.material = powerMaterial;
                break;

            case BallType.Curve:
                rb.mass = curveMass;
                rb.linearDamping = 0.4f;
                if (ballCollider != null && curvePhysicMaterial != null) ballCollider.material = curvePhysicMaterial;
                if (ballMeshRenderer != null && curveMaterial != null) ballMeshRenderer.material = curveMaterial;
                break;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Rigidbodyの物理制御はState Authorityだけで行う。
        if (!HasStateAuthority || rb == null || rb.isKinematic) return;

        // 1. 各ボールの最高速度を制限
        float maxVel = speedMaxVelocity;
        if (Type == BallType.Power) maxVel = powerMaxVelocity;
        else if (Type == BallType.Curve) maxVel = curveMaxVelocity;

        if (rb.linearVelocity.magnitude > maxVel)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxVel;
        }

        // 2. カーブボールの軌道回転処理
        if (Type == BallType.Curve && rb.linearVelocity.sqrMagnitude > 0.5f)
        {
            Vector3 rightDir = Vector3.Cross(Vector3.up, rb.linearVelocity.normalized);
            rb.AddForce(-rightDir * curveForce, ForceMode.Force);
        }
    }
}