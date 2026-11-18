using Fusion;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using Lovatto.MiniMap;

public class PlayerMovement : NetworkBehaviour
{
    public static PlayerMovement Local { get; private set; }
    private NetworkCharacterController _ncc;

    [Header("カメラターゲット")]
    [SerializeField]
    private Transform cameraTarget;

    [Header("カメラエイムオフセット設定")]
    [SerializeField] private Vector3 normalCameraOffset = new Vector3(0f, 0.9f, 0f); // 通常位置
    [SerializeField] private Vector3 aimingCameraOffset = new Vector3(1.0f, 1.5f, 0f); // ロケラン構え時の位置（数値を大きくするとより左下に寄る）
    [SerializeField] private float cameraTransitionSpeed = 10f; // カメラ移動の滑らかさ

    [Header("アニメーション設定")]
    [SerializeField] private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsStunnedHash = Animator.StringToHash("IsStunned");

    [Header("移動スピード")]
    [SerializeField] private float speed = 5f;

    [Header("慣性の調整（加速度）")]
    [SerializeField] private float acceleration = 10f; 
    [Networked] private Vector3 CurrentVelocity { get; set; }

    [Networked] public NetworkBool IsMovingInput { get; set; }

    [Header("ダッシュ設定")]
    [Networked] public TickTimer DashTimer { get; set; }
    [Networked] public float CurrentDashMultiplier { get; set; } = 1.0f;

    [Header("ジャンプ設定")]
    [SerializeField] private float jumpForce = 7f;
    [Networked] private NetworkBool CanJump { get; set; }
    [Networked] public NetworkBool PrevJumpPressed { get; set; }

    [Header("ジャンピングブーツ設定")]
    [SerializeField] private float bootForwardForce = 20f;
    [SerializeField] private float bootJumpForce = 12f;

    [Header("電磁石（シールド）設定")]
    public float shieldDuration = 10f; 
    [SerializeField] private GameObject shieldVisual;
    [Networked] 
    public TickTimer ShieldTimer { get; set; }

    [Header("アイテムエフェクト設定")]
    [SerializeField] private GameObject speedBoostVisual;     // プレイヤーの子に置くスピード用エフェクト
    [SerializeField] private GameObject jumpBootEffectPrefab; // 足元に生成するジャンプ用エフェクト
    [SerializeField] private float jumpBootEffectOffsetY = 0.5f; // ジャンプエフェクトの高さ調整

    [Header("アイテムSE設定")]
    [SerializeField] private AudioSource itemAudioSource;
    [InspectorName("ダッシュ")]
    [SerializeField] private AudioClip jumpBootSE;       // おすすめ: DM-CGS-21 / 45
    [InspectorName("スピード")]
    [SerializeField] private AudioClip speedBoostSE;     // おすすめ: DM-CGS-28 / 15
    [InspectorName("オーラ")]
    [SerializeField] private AudioClip shieldSE;         // おすすめ: DM-CGS-32 / 18
    [InspectorName("地雷")]
    [SerializeField] private AudioClip landminePlaceSE;  // おすすめ: DM-CGS-03 / 07
    [InspectorName("サテライト")]
    [SerializeField] private AudioClip satelliteReadySE; // おすすめ: DM-CGS-26 / 47
    [InspectorName("ロケラン")]
    [SerializeField] private AudioClip rocketLauncherSE; // 必要であれば割り当て

    [Header("被弾・スタン設定")]
    [SerializeField] private float carStunDuration = 2f;

    [Networked] private Vector3 KnockbackVelocity { get; set; }

    [Header("ボウリングボール設定")]
    [SerializeField] private NetworkPrefabRef ballPrefab;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float maxThrowForce = 90f;
    [SerializeField] private float chargeRate = 40.5f;
    [SerializeField] private float pickupDistance = 2.5f;

    [Header("ロケットランチャー設定")]
    [SerializeField] private RocketLauncherController rocketLauncherController;
    [SerializeField] private GameObject rocketLauncherObject;
    private bool _isAimingRocket = false;
    private int _aimingSlotIndex = -1;
    [Networked] public NetworkBool IsAimingRocket { get; set; }
    [Networked] public int AimingSlotIndex { get; set; } = -1;

    [Header("サテライトレーザー設定")]
    [SerializeField] private NetworkPrefabRef satelliteLaserPrefab;
    [Networked] public NetworkBool IsReadyingSatelliteLaser { get; set; }
    [Networked] public NetworkBool IsInvincible { get; set; }

    [Header("アイテム設定")]
    [Networked, Capacity(3)] 
    public NetworkArray<int> ItemRests { get; }
    [SerializeField] private NetworkPrefabRef landminePrefab;
    [SerializeField] private Transform landmineSpawnPoint;

    [Networked] 
    public NetworkBool PrevUseItem { get; set; }
    public int LocalSelectedSlot { get; private set; } = 0;
    [Networked] 
    public NetworkBool PrevFirePressed { get; set; }

    [Networked]
    public TickTimer PickupCooldown { get; set; }

    [Networked]
    public TickTimer StunTimer { get; set; }

    [Header("UI補助")]
    [SerializeField] private GameObject interactUI;
    [SerializeField] private GameObject crosshairUI;

    [Networked]
    public NetworkBool IsReady { get; set; }

    [Networked] public NetworkPlayerData PlayerData { get; set; }

    // --- ボール関連の同期変数 ---
    [Networked]
    public NetworkObject MyBall { get; set; }

    [Networked]
    public NetworkBool IsHoldingBall { get; set; }

    [Networked]
    public float CurrentCharge { get; set; }

    private bool _isSpawned;
    public bool IsPlayerSpawned => _isSpawned;
    private LineRenderer _trajectoryLine;

    [Header("アイテム効果")]
    [Networked] public TickTimer SpeedBoostTimer { get; set; }
    public float speedBoostDuration = 5.0f;
    
    [Networked, Capacity(3)]
    public NetworkArray<int> ItemTypes { get; }
    [Networked, Capacity(3)]
    public NetworkArray<int> ItemMaxUses { get; }

    private void SetCurrentVelocityY(float y)
    {
        Vector3 velocity = CurrentVelocity;
        velocity.y = y;
        CurrentVelocity = velocity;
    }

    private void SetCurrentVelocityXZ(float x, float z)
    {
        Vector3 velocity = CurrentVelocity;
        velocity.x = x;
        velocity.z = z;
        CurrentVelocity = velocity;
    }

    private void SetKnockbackVelocityY(float y)
    {
        Vector3 velocity = KnockbackVelocity;
        velocity.y = y;
        KnockbackVelocity = velocity;
    }

    private void SetKnockbackVelocityXZ(float x, float z)
    {
        Vector3 velocity = KnockbackVelocity;
        velocity.x = x;
        velocity.z = z;
        KnockbackVelocity = velocity;
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (itemAudioSource == null) itemAudioSource = GetComponent<AudioSource>();

        if (rocketLauncherController == null && rocketLauncherObject != null)
        {
            rocketLauncherController = rocketLauncherObject.GetComponent<RocketLauncherController>();
        }
        if (rocketLauncherObject == null && rocketLauncherController != null)
        {
            rocketLauncherObject = rocketLauncherController.gameObject;
        }
    }

    // エモート（バク宙など）用のジャンプ実行メソッド
    public void TriggerEmoteJump(float jumpPower)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null && cc.isGrounded)
        {
            SetCurrentVelocityY(jumpPower);
            CanJump = false;
        }
    }

    public void ApplyKnockback(Vector3 forceDirection, float stunTime)
    {
        // ホスト以外では攻撃判定を処理しない
        if (!HasStateAuthority) return;

        // サテライトレーザー照準中など、無敵状態なら吹き飛ばされない
        if (IsInvincible) return;

        if (!ShieldTimer.ExpiredOrNotRunning(Runner))
        {
            Debug.Log("電磁石シールドで攻撃を無効化しました！");
            return;
        }

        StunTimer = TickTimer.CreateFromSeconds(Runner, stunTime);
        KnockbackVelocity = forceDirection;
        CurrentVelocity = Vector3.zero;
        IsMovingInput = false;

        // 吹き飛ばされた時にボールを持っていたら落としてボールも飛ばす
        if (IsHoldingBall && MyBall != null)
        {
            IsHoldingBall = false;
            CurrentCharge = 0f;
            PickupCooldown = TickTimer.CreateFromSeconds(Runner, 1.0f);

            var ballRb = MyBall.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                ballRb.isKinematic = false;
                ballRb.AddForce(forceDirection, ForceMode.VelocityChange);
            }
        }
    }

    public void ApplyDash(float speedMultiplier, float duration)
    {
        if (!HasStateAuthority) return;

        CurrentDashMultiplier = speedMultiplier;
        DashTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    public void RequestSatelliteLaser(Vector3 targetPosition)
    {
        if (!HasInputAuthority) return;
        if (!IsReadyingSatelliteLaser) return;

        if (bl_MiniMap.ActiveMiniMap != null)
        {
            bl_MiniMap.ActiveMiniMap.SetToMiniMapSize();
        }

        RpcRequestSatelliteLaser(targetPosition);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcRequestSatelliteLaser(Vector3 targetPosition)
    {
        if (!IsReadyingSatelliteLaser) return;
        if (AimingSlotIndex < 0 || AimingSlotIndex >= 3) return;
        if (ItemRests[AimingSlotIndex] <= 0) return;

        // 攻撃地点にサテライトレーザーを生成する
        Runner.Spawn(
            satelliteLaserPrefab,
            targetPosition,
            Quaternion.identity,
            Object.InputAuthority
        );

        // 攻撃確定後、照準状態と無敵状態を解除
        IsReadyingSatelliteLaser = false;
        IsInvincible = false;

        // アイテムを消費
        ConsumeItem(AimingSlotIndex);
        AimingSlotIndex = -1;

        Debug.Log($"サテライトレーザー攻撃確定！ 座標: {targetPosition}");
    }

    public override void Spawned()
    {
        _isSpawned = true;
        _ncc = GetComponent<NetworkCharacterController>();

        if (HasStateAuthority)
        {
            ItemRests.Set(0, 0);
            ItemRests.Set(1, 0);
            ItemRests.Set(2, 0);

            ItemTypes.Set(0, 0);
            ItemTypes.Set(1, 0);
            ItemTypes.Set(2, 0);

            ItemMaxUses.Set(0, 0);
            ItemMaxUses.Set(1, 0);
            ItemMaxUses.Set(2, 0);

            MyBall = Runner.Spawn(
                ballPrefab,
                holdPoint.position,
                holdPoint.rotation,
                Object.InputAuthority
            );

            IsHoldingBall = true;
        }

        if (HasInputAuthority)
        {
            Local = this;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            bl_MiniMap map = bl_MiniMapUtils.GetMiniMap();
            bl_MiniMapTarget miniMapTarget = GetComponent<bl_MiniMapTarget>();

            if (map != null && miniMapTarget != null)
            {
                map.SetTarget(miniMapTarget);
                Debug.Log("ミニマップのターゲットを自分のプレイヤーに設定しました。");
            }
            else
            {
                Debug.LogWarning($"ミニマップ設定失敗 Map={map != null}, Target={miniMapTarget != null}");
            }

            var cinemachineCam = FindAnyObjectByType<CinemachineCamera>();
            if (cinemachineCam != null && cameraTarget != null)
            {
                cinemachineCam.Target.TrackingTarget = cameraTarget;
                cinemachineCam.Target.LookAtTarget = cameraTarget;
            }

            _trajectoryLine = gameObject.AddComponent<LineRenderer>();

            _trajectoryLine.positionCount = 2;

            // ガイドを太くする
            _trajectoryLine.startWidth = 0.4f;
            _trajectoryLine.endWidth = 0.4f;

            _trajectoryLine.alignment = LineAlignment.View;
            _trajectoryLine.useWorldSpace = true;

            // シェーダー設定
            Shader dottedShader =
                Shader.Find("Custom/TrajectoryDottedAlwaysOnTop");

            if (dottedShader != null)
            {
                Material dottedMaterial = new Material(dottedShader);

                dottedMaterial.SetColor(
                    "_BaseColor",
                    new Color(0.1f, 0.9f, 1f, 1f)
                );

                dottedMaterial.SetFloat("_DashCount", 10f);
                dottedMaterial.SetFloat("_DotWidth", 0.42f);
                dottedMaterial.SetFloat("_DotHeight", 0.85f);
                dottedMaterial.SetFloat("_EdgeSoftness", 0.04f);

                _trajectoryLine.material = dottedMaterial;
            }

            _trajectoryLine.textureMode = LineTextureMode.Stretch;
            _trajectoryLine.numCapVertices = 8;
            _trajectoryLine.numCornerVertices = 8;

            _trajectoryLine.enabled = false;

            RpcUpdatePlayerName(PlayerNameData.CurrentName);

            var data = PlayerData;
            data.PlayerRef = Object.InputAuthority;
            data.PlayerName = PlayerNameData.CurrentName;
            PlayerData = data; 
        }
    }

    private void ConsumeItem(int slot)
    {
        if (!HasStateAuthority) return;
        if (slot < 0 || slot >= 3) return;
        if (ItemRests[slot] <= 0) return;

        int usedItemType = ItemTypes[slot]; 

        int remaining = ItemRests[slot] - 1;
        ItemRests.Set(slot, remaining);

        if (remaining <= 0)
        {
            ItemRests.Set(slot, 0);
            ItemTypes.Set(slot, 0);
            ItemMaxUses.Set(slot, 0);
        }

        if ((usedItemType == 2 || usedItemType == 3) && GameManager.Instance != null)
        {
            GameManager.Instance.RecordItemUse(Object.InputAuthority);
        }

        Debug.Log($"スロット {slot + 1} のアイテムを消費。残り回数: {ItemRests[slot]}");
    }

    public override void FixedUpdateNetwork()
    {
        // State Authority と Input Authority 以外のプロキシでは
        // CharacterController を動かさない。ネットワーク補間とローカル移動の競合を防ぐ。
        if (!HasStateAuthority && !HasInputAuthority)
            return;

        bool isCountdown = GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Countdown;
        bool isStunned = !StunTimer.ExpiredOrNotRunning(Runner);

        if (isCountdown || isStunned)
        {
            IsMovingInput = false;
            if (CurrentCharge > 0) CurrentCharge = 0f;

            if (isStunned)
            {
                var cc = GetComponent<CharacterController>();
                if (cc != null)
                {
                    if (!cc.isGrounded)
                    {
                        SetKnockbackVelocityY(KnockbackVelocity.y + Physics.gravity.y * 2f * Runner.DeltaTime);
                    }

                    cc.Move(KnockbackVelocity * Runner.DeltaTime);

                    Vector3 horizontalVelocity = new Vector3(KnockbackVelocity.x, 0, KnockbackVelocity.z);
                    horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, 15f * Runner.DeltaTime);
                    
                    SetKnockbackVelocityXZ(horizontalVelocity.x, horizontalVelocity.z);
                }
            }
            return;
        }

        if (GetInput(out NetworkInputData data))
        {
            if (IsAimingRocket && data.selectedSlot != AimingSlotIndex)
            {
                IsAimingRocket = false;
                AimingSlotIndex = -1;
                Debug.Log("ロケットランチャーの構えを解除しました。");
            }
            if (!IsReadyingSatelliteLaser)
            {
                if (IsAimingRocket && ((data.isFirePressed && !PrevFirePressed) || (data.useItem && !PrevUseItem)))
                {
                    if (rocketLauncherController != null)
                    {
                        rocketLauncherController.FireWeapon(Runner, Object.InputAuthority, HasStateAuthority, data.aimDirection);
                    }

                    Rpc_PlayItemSE(2);

                    if (AimingSlotIndex >= 0 && AimingSlotIndex < 3)
                    {
                        ConsumeItem(AimingSlotIndex);
                    }

                    IsAimingRocket = false;
                    AimingSlotIndex = -1;
                    PrevUseItem = data.useItem;
                    return;
                }

                if (data.useItem && !PrevUseItem)
                {
                    if (!IsHoldingBall)
                    {
                        int slot = data.selectedSlot;
                        if (ItemRests[slot] > 0)
                        {
                            int currentItem = ItemTypes[slot];

                            switch (currentItem)
                            {
                                case 0:
                                    Debug.Log("アイテムが設定されていません。");
                                    break;

                                case 1:
                                    ConsumeItem(slot);
                                    SpeedBoostTimer = TickTimer.CreateFromSeconds(Runner, speedBoostDuration);
                                    Rpc_PlayItemSE(1);
                                    Debug.Log("スピードポーション使用！");
                                    break;

                                case 2:
                                    IsAimingRocket = true;
                                    AimingSlotIndex = slot;
                                    break;

                                case 3:
                                    var cc = GetComponent<CharacterController>();
                                    if (cc != null && !cc.isGrounded)
                                    {
                                        Debug.Log("空中では地雷を設置できません。");
                                        break; 
                                    }
                                    
                                    if (HasStateAuthority)
                                    {
                                        Vector3 spawnPosition;
                                        if (landmineSpawnPoint != null)
                                        {
                                            spawnPosition = landmineSpawnPoint.position;
                                        }
                                        else
                                        {
                                            spawnPosition = transform.position;
                                            Debug.LogWarning("LandmineSpawnPointが未設定です。プレイヤーの原点に地雷を設置します。");
                                        }

                                        Runner.Spawn(landminePrefab, spawnPosition, Quaternion.identity, Object.InputAuthority);
                                        ConsumeItem(slot);
                                        Rpc_PlayItemSE(3);
                                        Debug.Log($"地雷を設置しました！位置: {spawnPosition}");
                                    }
                                    break;

                                case 4:
                                    ConsumeItem(slot);
                                    CurrentVelocity = transform.forward * bootForwardForce + Vector3.up * bootJumpForce;
                                    CanJump = false;
                                    Rpc_PlayJumpBootEffect(transform.position);
                                    Rpc_PlayItemSE(4);
                                    Debug.Log("ジャンピングブーツ使用！");
                                    break;

                                case 5:
                                    ConsumeItem(slot);
                                    ShieldTimer = TickTimer.CreateFromSeconds(Runner, shieldDuration);
                                    Rpc_PlayItemSE(5);
                                    Debug.Log("電磁石シールド展開！");
                                    break;

                                case 6:
                                    IsReadyingSatelliteLaser = true;
                                    IsInvincible = true;
                                    AimingSlotIndex = slot;
                                    Rpc_PlayItemSE(6);
                                    if (HasInputAuthority && bl_MiniMap.ActiveMiniMap != null)
                                    {
                                        bl_MiniMap.ActiveMiniMap.SetToFullscreenSize();
                                    }
                                    break;

                                default:
                                    Debug.LogWarning($"未定義のアイテムタイプ: {currentItem}");
                                    break;
                            }
                        }
                    }
                }
            }
            PrevUseItem = data.useItem;
            PrevFirePressed = data.isFirePressed;

            if (!IsHoldingBall)
            {
                var cc = GetComponent<CharacterController>();
                Vector3 targetVelocity = Vector3.zero;
                bool hasMoveInput = data.movementInput.sqrMagnitude > 0.001f;
                IsMovingInput = hasMoveInput;
                
                if (hasMoveInput)
                {
                    float currentSpeed = speed;
                    if (!DashTimer.ExpiredOrNotRunning(Runner))
                    {
                        currentSpeed *= CurrentDashMultiplier;
                    }
                    else
                    {
                        CurrentDashMultiplier = 1.0f;
                    }

                    if (!SpeedBoostTimer.ExpiredOrNotRunning(Runner))
                    {
                        currentSpeed *= 2.0f;
                    }
                    targetVelocity = data.movementInput * currentSpeed;
                }

                targetVelocity.y = 0f;
                float currentY = CurrentVelocity.y;

                // 地上で移動キーを離した瞬間は慣性を残さずピタッと止める
                if (!hasMoveInput && cc != null && cc.isGrounded)
                {
                    SetCurrentVelocityXZ(0f, 0f);
                }
                else
                {
                    CurrentVelocity = Vector3.MoveTowards(
                        CurrentVelocity, 
                        targetVelocity, 
                        acceleration * Runner.DeltaTime
                    );
                }
                
                SetCurrentVelocityY(currentY);

                if (cc != null)
                {
                    if (cc.isGrounded && CurrentVelocity.y <= 0f)
                    {
                        CanJump = true;
                        SetCurrentVelocityY(-2f);
                    }
                    else
                    {
                        SetCurrentVelocityY(Mathf.Max(CurrentVelocity.y + Physics.gravity.y * 2f * Runner.DeltaTime, -30f));
                    }

                    if (data.isJumpPressed && !PrevJumpPressed && CanJump)
                    {
                        SetCurrentVelocityY(jumpForce);
                        CanJump = false;
                    }
                    cc.Move(CurrentVelocity * Runner.DeltaTime);
                    PrevJumpPressed = data.isJumpPressed;
                }

                if (IsAimingRocket && data.aimDirection.sqrMagnitude > 0)
                {
                    Vector3 lookDir = new Vector3(data.aimDirection.x, 0, data.aimDirection.z).normalized;
                    if (lookDir != Vector3.zero)
                    {
                        transform.forward = Vector3.Lerp(
                            transform.forward,
                            lookDir,
                            Runner.DeltaTime * 15f
                        );
                    }
                }
                else if (hasMoveInput)
                {
                    transform.forward = Vector3.Lerp(
                        transform.forward,
                        data.movementInput,
                        Runner.DeltaTime * 15f
                    );
                }
            }
            else
            {
                // ボールを持っているとき水平移動はゼロにするが重力は適用する
                IsMovingInput = false;
                SetCurrentVelocityXZ(0f, 0f);

                var cc = GetComponent<CharacterController>();
                if (cc != null)
                {
                    if (cc.isGrounded && CurrentVelocity.y <= 0f)
                    {
                        SetCurrentVelocityY(-2f);
                    }
                    else
                    {
                        SetCurrentVelocityY(Mathf.Max(CurrentVelocity.y + Physics.gravity.y * 2f * Runner.DeltaTime, -30f));
                    }
                    
                    cc.Move(CurrentVelocity * Runner.DeltaTime);
                }

                if (data.aimDirection.sqrMagnitude > 0)
                {
                    Vector3 lookDir = new Vector3(data.aimDirection.x, 0, data.aimDirection.z).normalized;
                    if (lookDir != Vector3.zero)
                    {
                        transform.forward = Vector3.Lerp(
                            transform.forward,
                            lookDir,
                            Runner.DeltaTime * 15f
                        );
                    }
                }
            }

            if ((HasStateAuthority || HasInputAuthority) && MyBall != null)
            {
                var ballRb = MyBall.GetComponent<Rigidbody>();

                if (IsHoldingBall)
                {
                    if (ballRb != null) ballRb.isKinematic = true;

                    if (holdPoint != null)
                    {
                        MyBall.transform.position = holdPoint.position;
                        MyBall.transform.rotation = holdPoint.rotation;
                    }

                    if (data.cancelThrow)
                    {
                        CurrentCharge = 0f;
                        IsHoldingBall = false;
                        if (ballRb != null) ballRb.isKinematic = false;
                        PickupCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    }
                    else if (data.isCharging)
                    {
                        float maxOvercharge = maxThrowForce * 1.15f;
                        CurrentCharge = Mathf.Clamp(
                            CurrentCharge + (chargeRate * Runner.DeltaTime),
                            0,
                            maxOvercharge
                        );
                    }
                    else if (!data.isCharging && CurrentCharge > 0)
                    {
                        IsHoldingBall = false;
                        if (ballRb != null)
                        {
                            ballRb.isKinematic = false;

                            Vector3 throwDir = data.aimDirection;

                            if (CurrentCharge > maxThrowForce)
                            {
                                float overpowerRatio = (CurrentCharge - maxThrowForce) / (maxThrowForce * 0.15f);
                                float deviationSign = (Runner.Tick % 2 == 0) ? 1f : -1f;
                                throwDir += transform.right * (deviationSign * overpowerRatio * 0.5f);
                            }

                            ballRb.AddForce(throwDir.normalized * CurrentCharge, ForceMode.Impulse);

                            Vector3 spin = transform.right * 15f;
                            float curveSpin = 30f;

                            if (data.isAPressed) spin += Vector3.up * -curveSpin;
                            if (data.isDPressed) spin += Vector3.up * curveSpin;

                            ballRb.maxAngularVelocity = 50f;
                            ballRb.angularVelocity = spin;
                        }

                        var impact = MyBall.GetComponent<BowlingBallImpact>();
                        if (impact != null)
                        {
                            impact.SetThrowPosition(transform.position);
                        }

                        CurrentCharge = 0f;
                        PickupCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);

                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.RegisterShot(Object.InputAuthority);
                        }
                    }
                }
                else
                {
                    if (data.interact && PickupCooldown.ExpiredOrNotRunning(Runner))
                    {
                        float distance = Vector3.Distance(transform.position, MyBall.transform.position);

                        if (distance <= pickupDistance)
                        {
                            if (ballRb != null)
                            {
                                ballRb.linearVelocity = Vector3.zero;
                                ballRb.angularVelocity = Vector3.zero;
                                ballRb.isKinematic = true;
                            }

                            if (holdPoint != null)
                            {
                                MyBall.transform.position = holdPoint.position;
                                MyBall.transform.rotation = holdPoint.rotation;
                            }

                            IsHoldingBall = true;
                            PickupCooldown = TickTimer.None;
                        }
                    }
                }
            }
        }
    }

    public bool TryPickupItem(int itemType, int maxUses)
    {
        bool pickedUp = false;
        
        for (int i = 0; i < 3; i++)
        {
            if (ItemRests[i] > 0 && ItemTypes[i] == itemType)
            {
                ItemMaxUses.Set(i, maxUses);
                ItemRests.Set(i, maxUses);
                pickedUp = true;
                break;
            }
        }

        if (!pickedUp)
        {
            for (int i = 0; i < 3; i++)
            {
                if (ItemRests[i] == 0)
                {
                    ItemTypes.Set(i, itemType);
                    ItemMaxUses.Set(i, maxUses);
                    ItemRests.Set(i, maxUses);
                    pickedUp = true;
                    break;
                }
            }
        }

        if (pickedUp && HasStateAuthority && GameManager.Instance != null)
        {
            Debug.Log($"[PlayerMovement] アイテム取得処理開始: 取得プレイヤー = {Object.InputAuthority}");
            GameManager.Instance.RecordItemPickup(Object.InputAuthority);
        }

        return pickedUp;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (HasStateAuthority && other.CompareTag("Car"))
        {
            ApplyKnockback((Vector3.forward * 12f) + (Vector3.up * 8f), carStunDuration);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (HasStateAuthority && collision.gameObject.CompareTag("Car"))
        {
            ApplyKnockback((Vector3.forward * 12f) + (Vector3.up * 8f), carStunDuration);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcSetReady(bool ready)
    {
        IsReady = ready;
    }

    private void Update()
    {
        if (!HasInputAuthority)
            return;

        // 状態によってアイテムスロット変更をブロックするか判定
        bool canChangeSlot = true;

        if (Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed)
        {
            canChangeSlot = false; // L CTRL（ズーム操作）中は変更無効
        }
        if (IsReadyingSatelliteLaser)
        {
            canChangeSlot = false; // サテライトレーザー構え中は変更無効
        }

        if (canChangeSlot)
        {
            int previousSlot = LocalSelectedSlot;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) LocalSelectedSlot = 0;
                if (Keyboard.current.digit2Key.wasPressedThisFrame) LocalSelectedSlot = 1;
                if (Keyboard.current.digit3Key.wasPressedThisFrame) LocalSelectedSlot = 2;
            }
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0)
                {
                    LocalSelectedSlot--;
                    if (LocalSelectedSlot < 0) LocalSelectedSlot = 2;
                }
                else if (scroll < 0)
                {
                    LocalSelectedSlot++;
                    if (LocalSelectedSlot > 2) LocalSelectedSlot = 0;
                }
            }
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        UpdateInteractVisuals();
        UpdateTrajectory();
        UpdateCameraTargetOffset();
    }

    public override void Render()
    {
        UpdateInteractVisuals();

        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            float animSpeed = 0f;

            // 自分の操作クライアントで現在移動キーが押されていない場合は即座に 0 にする
            bool isLocalMoving = true;
            if (HasInputAuthority && Keyboard.current != null)
            {
                isLocalMoving = Keyboard.current.wKey.isPressed ||
                                Keyboard.current.aKey.isPressed ||
                                Keyboard.current.sKey.isPressed ||
                                Keyboard.current.dKey.isPressed ||
                                Keyboard.current.upArrowKey.isPressed ||
                                Keyboard.current.downArrowKey.isPressed ||
                                Keyboard.current.leftArrowKey.isPressed ||
                                Keyboard.current.rightArrowKey.isPressed;
            }

            // ボールを持っていない時かつ移動入力中のみ速度を反映
            if (!IsHoldingBall && IsMovingInput && isLocalMoving)
            {
                Vector3 horizontalVel = new Vector3(CurrentVelocity.x, 0f, CurrentVelocity.z);
                animSpeed = horizontalVel.magnitude;

                if (animSpeed < 0.2f)
                {
                    animSpeed = 0f;
                }
            }

            animator.SetFloat(SpeedHash, animSpeed);

            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                animator.SetBool(IsGroundedHash, cc.isGrounded);
            }

            bool isStunned = !StunTimer.ExpiredOrNotRunning(Runner);
            animator.SetBool(IsStunnedHash, isStunned);
        }
    }

    private void UpdateTrajectory()
    {
        if (_trajectoryLine == null) return;

        if (IsHoldingBall &&
            Mouse.current != null &&
            Mouse.current.rightButton.isPressed)
        {
            _trajectoryLine.enabled = true;

            Vector3 startPos = holdPoint.position;

            // BasicSpawnerから実際の投擲方向を取得
            Vector3 throwDir = BasicSpawner.LocalAimDirection;

            if (throwDir.sqrMagnitude < 0.001f)
            {
                throwDir = transform.forward;
            }

            // 予測線用にY方向の角度を取り除く
            throwDir.y = 0f;

            // 水平方向に正規化
            if (throwDir.sqrMagnitude > 0.001f)
            {
                throwDir.Normalize();
            }
            else
            {
                throwDir = transform.forward;
            }

            // チャージ量に関係なく一定の長さ
            float predictionDistance = 12f;

            Vector3 endPos = startPos + throwDir * predictionDistance;

            _trajectoryLine.positionCount = 2;
            _trajectoryLine.SetPosition(0, startPos);
            _trajectoryLine.SetPosition(1, endPos);
        }
        else
        {
            _trajectoryLine.enabled = false;
        }
    }

    private void UpdateCameraTargetOffset()
    {
        if (cameraTarget != null)
        {
            // ロケラン構え中かどうかで目標座標を切り替え
            Vector3 targetPosition = IsAimingRocket ? aimingCameraOffset : normalCameraOffset;
            
            // Vector3.Lerpで滑らかに移動させる
            cameraTarget.localPosition = Vector3.Lerp(
                cameraTarget.localPosition, 
                targetPosition, 
                Time.deltaTime * cameraTransitionSpeed
            );
        }
    }

    private void UpdateInteractVisuals()
    {
        bool canPickup = false;

        bool isCountdown = GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Countdown;
        bool isStunned = !StunTimer.ExpiredOrNotRunning(Runner);

        if (!isCountdown && !isStunned &&
            !IsHoldingBall &&
            MyBall != null &&
            PickupCooldown.ExpiredOrNotRunning(Runner))
        {
            if (Vector3.Distance(transform.position, MyBall.transform.position) <= pickupDistance)
            {
                canPickup = true;
            }
        }

        if (interactUI != null)
        {
            if (interactUI.activeSelf != canPickup)
            {
                interactUI.SetActive(canPickup);
            }
        }

        if (MyBall != null)
        {
            var ballVisual = MyBall.GetComponent<BowlingBallVisual>();

            if (ballVisual != null)
            {
                ballVisual.SetOutline(canPickup);
            }
        }

        if (shieldVisual != null)
        {
            bool isShieldActive = !ShieldTimer.ExpiredOrNotRunning(Runner);
            if (shieldVisual.activeSelf != isShieldActive)
            {
                shieldVisual.SetActive(isShieldActive);
            }
        }

        if (speedBoostVisual != null)
        {
            bool isSpeedActive = !SpeedBoostTimer.ExpiredOrNotRunning(Runner);
            if (speedBoostVisual.activeSelf != isSpeedActive)
            {
                speedBoostVisual.SetActive(isSpeedActive);
            }
        }

        if (rocketLauncherObject != null)
        {
            if (rocketLauncherObject.activeSelf != IsAimingRocket)
            {
                rocketLauncherObject.SetActive(IsAimingRocket);
                if (IsAimingRocket)
                {
                    var renderers = rocketLauncherObject.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers) r.enabled = true;
                }
            }
        }

        if (crosshairUI != null)
        {
            bool shouldShowCrosshair = IsAimingRocket && HasInputAuthority;
            if (crosshairUI.activeSelf != shouldShowCrosshair)
            {
                crosshairUI.SetActive(shouldShowCrosshair);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcUpdatePlayerName(string newName)
    {
        var data = PlayerData;
        data.PlayerRef = Object.InputAuthority;
        data.PlayerName = newName;
        PlayerData = data;
    }

    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayJumpBootEffect(Vector3 footPosition)
    {
        if (jumpBootEffectPrefab != null)
        {
            Vector3 spawnPos = footPosition + Vector3.up * jumpBootEffectOffsetY;
            GameObject effect = Instantiate(jumpBootEffectPrefab, spawnPos, Quaternion.identity);
            Destroy(effect, 2.0f);
        }
    }

    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayItemSE(int itemType)
    {
        if (itemAudioSource == null) itemAudioSource = GetComponent<AudioSource>();
        if (itemAudioSource == null) return;

        AudioClip clipToPlay = null;
        switch (itemType)
        {
            case 1: clipToPlay = speedBoostSE; break;
            case 2: clipToPlay = rocketLauncherSE; break;
            case 3: clipToPlay = landminePlaceSE; break;
            case 4: clipToPlay = jumpBootSE; break;
            case 5: clipToPlay = shieldSE; break;
            case 6: clipToPlay = satelliteReadySE; break;
        }

        if (clipToPlay != null)
        {
            itemAudioSource.PlayOneShot(clipToPlay);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        
        if (hasState && MyBall != null)
        {
            runner.Despawn(MyBall);
        }
    }
}