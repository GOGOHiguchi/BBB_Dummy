using Fusion;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Lovatto.MiniMap;

public class PlayerMovement : NetworkBehaviour
{
    public static PlayerMovement Local { get; private set; }
    private NetworkCharacterController _ncc;

    [Header("カメラターゲット")]
    [SerializeField]
    private Transform cameraTarget;

    [Header("カメラエイムオフセット設定")]
    [SerializeField] private Vector3 aimingCameraOffset = new Vector3(1.0f, 1.5f, 0f); // ロケラン構え時のローカル座標
    [SerializeField] private float cameraTransitionSpeed = 10f; // カメラ移動の滑らかさ
    private Vector3 _cameraTargetBaseLocalPosition;
    private bool _cameraTargetBasePositionCached;

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
    [SerializeField] private AudioClip jumpBootSE;
    [InspectorName("ダッシュ音量")]
    [Range(0f, 10f)] [SerializeField] private float jumpBootVolume = 1.0f;

    [InspectorName("スピード")]
    [SerializeField] private AudioClip speedBoostSE;
    [InspectorName("スピード音量")]
    [Range(0f, 10f)] [SerializeField] private float speedBoostVolume = 1.0f;

    [InspectorName("オーラ")]
    [SerializeField] private AudioClip shieldSE;
    [InspectorName("オーラ音量")]
    [Range(0f, 10f)] [SerializeField] private float shieldVolume = 1.0f;

    [InspectorName("地雷")]
    [SerializeField] private AudioClip landminePlaceSE;
    [InspectorName("地雷音量")]
    [Range(0f, 10f)] [SerializeField] private float landminePlaceVolume = 1.0f;

    [InspectorName("サテライト")]
    [SerializeField] private AudioClip satelliteReadySE;
    [InspectorName("サテライト音量")]
    [Range(0f, 10f)] [SerializeField] private float satelliteReadyVolume = 1.0f;

    [InspectorName("ロケラン")]
    [SerializeField] private AudioClip rocketLauncherSE;
    [InspectorName("ロケラン音量")]
    [Range(0f, 10f)] [SerializeField] private float rocketLauncherVolume = 1.0f;

    private AudioSource _shieldLoopAudioSource; // オーラSE専用3Dスピーカー（エフェクト消滅と同時に停止するため）

    [Header("被弾・スタン設定")]
    [SerializeField] private float carStunDuration = 2f;

    [Networked] private Vector3 KnockbackVelocity { get; set; }
    private bool _wasLocalStunned = false; // 自分専用のスタンSE切り替え判定用

    [Header("ボウリングボール設定")]
    [SerializeField] private NetworkPrefabRef ballPrefab;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float maxThrowForce = 90f;
    [SerializeField] private float chargeRate = 40.5f;
    // UI側でローカル予測ゲージを描画するための読み取り専用値。
    public float ChargeRate => chargeRate;
    [Tooltip("短いチャージでもボウリングらしい初速を確保する")]
    [SerializeField] private float minThrowSpeed = 8.5f;
    [Tooltip("最大チャージ時の投球速度（Unity単位/秒）")]
    [SerializeField] private float maxThrowSpeed = 15f;
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

    // ピン撃破後、次のホール/試合シーンへ移るまで操作を禁止する。
    [Networked]
    public NetworkBool IsPinFinished { get; set; }

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

    // クライアントの取得操作を先行表示するための見た目専用予測。
    private bool _predictingPickupVisual;
    private float _pickupPredictionEndTime;
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
        // Prefabで設定したCameraTargetの位置を基準位置として保存する。
        // 通常時にスクリプトがPivot基準の固定座標へ戻してしまうことを防ぐ。
        if (cameraTarget != null)
        {
            _cameraTargetBaseLocalPosition = cameraTarget.localPosition;
            _cameraTargetBasePositionCached = true;
        }

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
        if (IsPinFinished) return;

        var cc = GetComponent<CharacterController>();
        if (cc != null && cc.isGrounded)
        {
            SetCurrentVelocityY(jumpPower);
            CanJump = false;
        }
    }

    public bool ApplyKnockback(Vector3 forceDirection, float stunTime)
    {
        // ピン撃破後は追加の攻撃・ノックバックを受け付けない
        if (!HasStateAuthority || IsPinFinished) return false;

        // 無敵状態・シールド状態なら攻撃を無効化
        if (IsInvincible) return false;

        if (!ShieldTimer.ExpiredOrNotRunning(Runner))
        {
            Debug.Log("電磁石シールドで攻撃を無効化しました！");
            return false;
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
            if (ballRb != null && MyBall.HasStateAuthority)
            {
                ballRb.isKinematic = false;
                ballRb.AddForce(forceDirection, ForceMode.VelocityChange);
            }
        }

        return true;
    }

    /// <summary>
    /// ボールがピンに命中したプレイヤーを、試合終了まで操作不能にする。
    /// ノックバックの確定とエモート開始はState Authorityだけで行う。
    /// </summary>
    public void BeginPinFinish(Vector3 knockbackVelocity, float knockbackDuration)
    {
        if (!HasStateAuthority || IsPinFinished)
            return;

        IsPinFinished = true;
        StunTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, knockbackDuration));
        KnockbackVelocity = knockbackVelocity;
        CurrentVelocity = Vector3.zero;
        CurrentCharge = 0f;
        IsMovingInput = false;

        // 使用中だった照準・アイテム操作も解除する。
        IsAimingRocket = false;
        AimingSlotIndex = -1;
        IsReadyingSatelliteLaser = false;
        IsInvincible = false;

        // 万一ボール保持中に呼ばれても、手元に固定されたままにならないよう解放する。
        if (IsHoldingBall && MyBall != null)
        {
            IsHoldingBall = false;
            Rigidbody ballRb = MyBall.GetComponent<Rigidbody>();
            if (ballRb != null && MyBall.HasStateAuthority)
            {
                ballRb.isKinematic = false;
                ballRb.linearVelocity = Vector3.zero;
                ballRb.angularVelocity = Vector3.zero;
                ballRb.AddForce(knockbackVelocity, ForceMode.VelocityChange);
            }
            PickupCooldown = TickTimer.CreateFromSeconds(Runner, 999f);
        }

        EmoteWheelController emoteController = GetComponent<EmoteWheelController>();
        if (emoteController != null)
        {
            emoteController.BeginCelebration();
        }
        else
        {
            Debug.LogWarning("[PlayerMovement] EmoteWheelControllerが見つからないため、撃破後エモートを開始できません。", this);
        }

        Debug.Log($"[PlayerMovement] ピン撃破により操作ロック開始: {Object.InputAuthority}");
    }

    public void ApplyDash(float speedMultiplier, float duration)
    {
        if (!HasStateAuthority || IsPinFinished) return;

        CurrentDashMultiplier = speedMultiplier;
        DashTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    public void RequestSatelliteLaser(Vector3 targetPosition)
    {
        if (!HasInputAuthority || IsPinFinished) return;
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
        if (IsPinFinished || !IsReadyingSatelliteLaser) return;
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

        // ピン撃破後は、ノックバック中も終了後も入力処理へ進ませない。
        // 水平方向の吹き飛びは減衰させ、空中にいる間は重力だけを適用する。
        if (IsPinFinished)
        {
            IsMovingInput = false;
            CurrentVelocity = Vector3.zero;
            CurrentCharge = 0f;

            var finishController = GetComponent<CharacterController>();
            if (finishController != null)
            {
                bool knockbackActive = !StunTimer.ExpiredOrNotRunning(Runner);

                if (finishController.isGrounded && KnockbackVelocity.y <= 0f)
                {
                    SetKnockbackVelocityY(-2f);
                }
                else
                {
                    SetKnockbackVelocityY(
                        Mathf.Max(
                            KnockbackVelocity.y + Physics.gravity.y * 2f * Runner.DeltaTime,
                            -30f
                        )
                    );
                }

                if (knockbackActive)
                {
                    Vector3 horizontal = new Vector3(KnockbackVelocity.x, 0f, KnockbackVelocity.z);
                    horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, 15f * Runner.DeltaTime);
                    SetKnockbackVelocityXZ(horizontal.x, horizontal.z);
                }
                else
                {
                    SetKnockbackVelocityXZ(0f, 0f);
                }

                finishController.Move(KnockbackVelocity * Runner.DeltaTime);
            }

            return;
        }

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
                                    // ※オーラSEの再生・停止はUpdateInteractVisualsでエフェクトと完全連動させます
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

            if (HasStateAuthority && MyBall != null)
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

                            // チャージ量を投球速度へ変換する。ボールの質量差に左右されないよう
                            // 速度×質量をImpulseとして与え、Speed/Power/Curveの重量感は維持する。
                            float chargeRatio = Mathf.Clamp(CurrentCharge / maxThrowForce, 0f, 1.15f);
                            float launchSpeed = Mathf.LerpUnclamped(minThrowSpeed, maxThrowSpeed, chargeRatio);
                            BowlingBall ballSettings = MyBall.GetComponent<BowlingBall>();
                            if (ballSettings != null)
                            {
                                if (ballSettings.Type == BallType.Speed) launchSpeed *= 1.08f;
                                else if (ballSettings.Type == BallType.Curve) launchSpeed *= 1.02f;
                            }
                            ballRb.linearVelocity = Vector3.zero;
                            ballRb.AddForce(throwDir.normalized * (launchSpeed * ballRb.mass), ForceMode.Impulse);

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
        if (IsPinFinished) return false;

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

        // ピン撃破後はローカル入力によるスロット変更・カーソル操作なども受け付けない。
        if (IsPinFinished)
        {
            UpdateInteractVisuals();
            UpdateTrajectory();
            return;
        }

        UpdateLocalPickupPrediction();

        // キャラクター選択UI操作中は、ゲーム側のスロット・カーソル操作を止める。
        if (CharacterSelectUI.IsAnyOpen)
        {
            UpdateInteractVisuals();
            return;
        }

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

        // Escapeでカーソルを表示し、UIではないゲーム画面をクリックしたときに再びロックする。
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ShowCursor();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.visible && Cursor.lockState != CursorLockMode.Locked)
        {
            bool clickedUI = EventSystem.current != null &&
                             EventSystem.current.IsPointerOverGameObject();

            if (!clickedUI)
            {
                LockCursor();
            }
        }

        UpdateInteractVisuals();
        UpdateTrajectory();
        UpdateCameraTargetOffset();
    }

    /// <summary>
    /// Eキーを押した瞬間に、ボール取得の見た目だけを先行させる。
    /// Rigidbodyやネットワーク状態は変更せず、State Authorityの判定結果に従う。
    /// </summary>
    private void UpdateLocalPickupPrediction()
    {
        if (!HasInputAuthority || HasStateAuthority)
            return;

        if (IsHoldingBall)
        {
            _predictingPickupVisual = false;
            return;
        }

        if (_predictingPickupVisual && Time.unscaledTime >= _pickupPredictionEndTime)
        {
            _predictingPickupVisual = false;
        }

        if (CharacterSelectInteractable.IsLocalPlayerInRange || CharacterSelectUI.IsAnyOpen)
            return;

        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        bool isCountdown = GameManager.Instance != null &&
                           GameManager.Instance.CurrentState == GameManager.GameState.Countdown;
        bool isStunned = !StunTimer.ExpiredOrNotRunning(Runner);

        if (isCountdown || isStunned || MyBall == null ||
            !PickupCooldown.ExpiredOrNotRunning(Runner))
            return;

        if (Vector3.Distance(transform.position, MyBall.transform.position) <= pickupDistance)
        {
            _predictingPickupVisual = true;
            // サーバー応答が届かなかった場合は予測表示を解除する。
            _pickupPredictionEndTime = Time.unscaledTime + 1.0f;
        }
    }

    /// <summary>差し替え後のモデルAnimatorを操作・アニメーション処理へ反映する。</summary>
    public void SetCharacterAnimator(Animator value)
    {
        animator = value;
    }

    // CharacterModelSwitcherがモデル差し替え時に外付け武器を退避・再装着するための参照。
    public GameObject RocketLauncherObject => rocketLauncherObject;
    public RocketLauncherController RocketLauncherController => rocketLauncherController;


    private void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (!_isSpawned || MyBall == null)
            return;

        // State Authority以外ではローカル物理演算を行わない。
        // NetworkTransformによる位置更新とRigidbodyの物理演算が競合するのを防ぐ。
        if (!HasStateAuthority)
        {
            Rigidbody ballRb = MyBall.GetComponent<Rigidbody>();
            if (ballRb != null && !ballRb.isKinematic)
            {
                ballRb.linearVelocity = Vector3.zero;
                ballRb.angularVelocity = Vector3.zero;
                ballRb.isKinematic = true;
            }
        }

        // 操作中クライアントでは、予測移動中のプレイヤーにボールの見た目を追従させる。
        // ネットワーク上のボール位置や物理状態を書き換える処理ではない。
        if (HasInputAuthority && !HasStateAuthority &&
            (IsHoldingBall || _predictingPickupVisual) && holdPoint != null)
        {
            MyBall.transform.SetPositionAndRotation(holdPoint.position, holdPoint.rotation);
        }
    }

    public override void Render()
    {
        UpdateInteractVisuals();

        bool isStunned = !StunTimer.ExpiredOrNotRunning(Runner);

        // 自分の操作キャラ（HasInputAuthority）だけスタン開始・解除SEを鳴らす
        // ※ピン撃破後の演出（IsPinFinished）中は通常のスタンSEを鳴らさない
        if (HasInputAuthority)
        {
            bool shouldPlayStunSE = isStunned && !IsPinFinished;

            if (shouldPlayStunSE && !_wasLocalStunned)
            {
                if (AudioManager.instance != null)
                {
                    AudioManager.instance.PlaySE(SEType.Damage);       // スタンを食らった瞬間の音
                    AudioManager.instance.PlayLoopSE(SEType.StunLoop); // スタン中のループ音開始
                }
            }
            else if (!shouldPlayStunSE && _wasLocalStunned)
            {
                if (AudioManager.instance != null)
                {
                    AudioManager.instance.StopLoopSE(); // スタン解除でループ音停止
                }
            }
            _wasLocalStunned = shouldPlayStunSE;
        }

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
        if (cameraTarget == null)
            return;

        // 通常時はPrefabで設定した位置をそのまま使用する。
        // ロケラン構え中のみ、従来の照準用ローカル座標へ滑らかに移動する。
        if (!_cameraTargetBasePositionCached)
        {
            _cameraTargetBaseLocalPosition = cameraTarget.localPosition;
            _cameraTargetBasePositionCached = true;
        }

        Vector3 targetPosition = IsAimingRocket
            ? aimingCameraOffset
            : _cameraTargetBaseLocalPosition;

        cameraTarget.localPosition = Vector3.Lerp(
            cameraTarget.localPosition,
            targetPosition,
            Time.deltaTime * cameraTransitionSpeed
        );
    }

    private void UpdateInteractVisuals()
    {
        bool canPickup = false;

        bool isCountdown = GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Countdown;
        bool isStunned = !StunTimer.ExpiredOrNotRunning(Runner);

        if (!isCountdown && !isStunned && !IsPinFinished &&
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

        // オーラ（シールド）エフェクトと3D音響SEの完全同期
        bool isShieldActive = !ShieldTimer.ExpiredOrNotRunning(Runner) && !IsPinFinished;
        if (shieldVisual != null)
        {
            if (shieldVisual.activeSelf != isShieldActive)
            {
                shieldVisual.SetActive(isShieldActive);
            }
        }
        UpdateShieldLoopSE(isShieldActive);

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
            bool shouldShowCrosshair = IsAimingRocket && HasInputAuthority && !IsPinFinished;
            if (crosshairUI.activeSelf != shouldShowCrosshair)
            {
                crosshairUI.SetActive(shouldShowCrosshair);
            }
        }
    }

    // オーラ表示中だけ3D音響でSEを鳴らし、エフェクトが消えた瞬間に即停止する
    private void UpdateShieldLoopSE(bool isActive)
    {
        if (isActive && shieldSE != null)
        {
            if (_shieldLoopAudioSource == null)
            {
                _shieldLoopAudioSource = gameObject.AddComponent<AudioSource>();
                _shieldLoopAudioSource.loop = true;
                _shieldLoopAudioSource.playOnAwake = false;
                _shieldLoopAudioSource.spatialBlend = 1.0f; // 3D空間音響
                _shieldLoopAudioSource.rolloffMode = AudioRolloffMode.Linear;
                _shieldLoopAudioSource.minDistance = 12f;
                _shieldLoopAudioSource.maxDistance = 60f;

                if (AudioManager.instance != null && AudioManager.instance.seAudioSource != null)
                {
                    _shieldLoopAudioSource.outputAudioMixerGroup = AudioManager.instance.seAudioSource.outputAudioMixerGroup;
                }
            }

            _shieldLoopAudioSource.spatialBlend = 1.0f;
            _shieldLoopAudioSource.rolloffMode = AudioRolloffMode.Linear;
            // 1倍を超える音量倍率を設定した際に減衰距離を広げて3Dでも遠くまで聞こえるようにする
            float boost = Mathf.Max(1f, shieldVolume);
            _shieldLoopAudioSource.minDistance = 12f * boost;
            _shieldLoopAudioSource.maxDistance = 60f * boost;
            _shieldLoopAudioSource.volume = Mathf.Clamp01(shieldVolume);

            if (!_shieldLoopAudioSource.isPlaying)
            {
                _shieldLoopAudioSource.clip = shieldSE;
                _shieldLoopAudioSource.Play();
            }
        }
        else
        {
            if (_shieldLoopAudioSource != null && _shieldLoopAudioSource.isPlaying)
            {
                _shieldLoopAudioSource.Stop();
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
        // 地雷（itemType == 3）だけはプレイヤーの3D空間AudioSourceから鳴らす
        if (itemType == 3)
        {
            if (landminePlaceSE == null) return;

            if (itemAudioSource == null) itemAudioSource = GetComponent<AudioSource>();
            if (itemAudioSource != null && itemAudioSource.enabled)
            {
                itemAudioSource.spatialBlend = 1.0f; // 3D音響
                itemAudioSource.rolloffMode = AudioRolloffMode.Linear;
                itemAudioSource.minDistance = 10f;
                itemAudioSource.maxDistance = 50f;
                if (AudioManager.instance != null && AudioManager.instance.seAudioSource != null)
                {
                    itemAudioSource.outputAudioMixerGroup = AudioManager.instance.seAudioSource.outputAudioMixerGroup;
                }
                itemAudioSource.PlayOneShot(landminePlaceSE, landminePlaceVolume);
            }
            return;
        }

        AudioClip clipToPlay = null;
        float volumeScale = 1.0f;

        switch (itemType)
        {
            case 1:
                clipToPlay = speedBoostSE;
                volumeScale = speedBoostVolume;
                break;
            case 2:
                clipToPlay = rocketLauncherSE;
                volumeScale = rocketLauncherVolume;
                break;
            case 4:
                clipToPlay = jumpBootSE;
                volumeScale = jumpBootVolume;
                break;
            case 5:
                // オーラ（シールド）はUpdateShieldLoopSEで3Dループ再生するため単発再生しない
                return;
            case 6:
                clipToPlay = satelliteReadySE;
                volumeScale = satelliteReadyVolume;
                break;
        }

        if (clipToPlay == null) return;

        // 地雷・オーラ以外は2D音響（AudioManager）で再生
        if (AudioManager.instance != null && AudioManager.instance.seAudioSource != null)
        {
            AudioManager.instance.seAudioSource.PlayOneShot(clipToPlay, volumeScale);
            return;
        }

        // マップ単体起動時の予備
        if (itemAudioSource == null) itemAudioSource = GetComponent<AudioSource>();
        if (itemAudioSource != null && itemAudioSource.enabled)
        {
            itemAudioSource.PlayOneShot(clipToPlay, volumeScale);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // スタン中に試合終了・退出してもループ音が残らないよう停止
        if (HasInputAuthority && _wasLocalStunned && AudioManager.instance != null)
        {
            AudioManager.instance.StopLoopSE();
            _wasLocalStunned = false;
        }

        // オーラSEも確実に停止
        if (_shieldLoopAudioSource != null && _shieldLoopAudioSource.isPlaying)
        {
            _shieldLoopAudioSource.Stop();
        }

        base.Despawned(runner, hasState);
        
        if (hasState && MyBall != null)
        {
            runner.Despawn(MyBall);
        }
    }
}