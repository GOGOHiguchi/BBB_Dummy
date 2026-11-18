using Fusion;
using UnityEngine;

public class BowlingArmController : NetworkBehaviour
{
    [Header("参照設定")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Animator animator;

    [Header("ボーン参照（IK/角度制御用）")]
    [Tooltip("左肩または左腕のボーンTransformをセットします")]
    [SerializeField] private Transform leftArmBone;

    [Header("投球モーション設定")]
    [SerializeField] private Vector3 maxChargeArmRotation = new Vector3(0f, -30f, -85f); // 溜め時の腕の後ろ引き角度
    [SerializeField] private float smoothSpeed = 10f;

    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int ThrowTriggerHash = Animator.StringToHash("Throw");

    private Quaternion _originalArmRotation;
    private bool _prevChargingState;
    private Quaternion _targetArmRotation;
    private bool _isChargingCurrent;

    private void Awake()
    {
        if (playerMovement == null) playerMovement = GetComponentInParent<PlayerMovement>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (leftArmBone != null)
        {
            _originalArmRotation = leftArmBone.localRotation;
            _targetArmRotation = _originalArmRotation;
        }
    }

    public override void Render()
    {
        if (playerMovement == null || animator == null) return;

        bool isHolding = playerMovement.IsHoldingBall;
        float currentCharge = playerMovement.CurrentCharge;
        _isChargingCurrent = isHolding && currentCharge > 0f;

        if (_prevChargingState && !_isChargingCurrent && !isHolding)
        {
            animator.SetTrigger(ThrowTriggerHash);
        }

        _prevChargingState = _isChargingCurrent;
        animator.SetBool(IsChargingHash, _isChargingCurrent);

        if (_isChargingCurrent)
        {
            float chargeRatio = Mathf.Clamp01(currentCharge / 90f);
            Quaternion offsetRot = Quaternion.Euler(maxChargeArmRotation * chargeRatio);
            _targetArmRotation = Quaternion.Slerp(_targetArmRotation, _originalArmRotation * offsetRot, Time.deltaTime * smoothSpeed);
        }
        else
        {
            _targetArmRotation = Quaternion.Slerp(_targetArmRotation, _originalArmRotation, Time.deltaTime * smoothSpeed);
        }
    }

    // ★ Animator がポーズを決定した直後にボーンの角度を強制上書きする
    private void LateUpdate()
    {
        if (leftArmBone != null && _isChargingCurrent)
        {
            leftArmBone.localRotation = _targetArmRotation;
        }
    }
}