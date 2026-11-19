using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EmoteWheelController : NetworkBehaviour
{
    [Header("参照設定")]
    [SerializeField] private GameObject emoteWheelUI;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("スロットUI（1番〜8番の順にドラッグ＆ドロップ）")]
    [SerializeField] private RectTransform[] slotTransforms = new RectTransform[8];
    [SerializeField] private Image[] slotImages = new Image[8];

    [Header("ハイライト演出設定")]
    [SerializeField] private float normalScale = 1.0f;
    [SerializeField] private float highlightScale = 1.2f;
    [SerializeField] private float scaleSmoothSpeed = 15f;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.3f, 1f);

    [Header("設定")]
    [SerializeField] private Key emoteKey = Key.R;
    [SerializeField] private int closeSlotIndex = 8; // 「閉じる（キャンセル）」にする枠番号（デフォルトはラストの8番）

    [Header("バク宙ジャンプ演出設定")]
    [SerializeField] private int backflipEmoteIndex = 1;     // バク宙のエモート番号（1〜8）
    [SerializeField] private float backflipJumpDelay = 0.45f; // 踏切までのタメ時間（秒）
    [SerializeField] private float backflipJumpPower = 7.5f;  // バク宙時のジャンプの高さ

    [Networked]
    public int CurrentEmoteIndex { get; set; }

    // エモート番号がクライアントへ届く前に消えないよう、一定時間保持する。
    [Networked]
    private TickTimer EmoteResetTimer { get; set; }

    // ピン撃破後にState Authorityが選んだエモートを全クライアントへ同期する。
    [Networked]
    public NetworkBool IsCelebrating { get; set; }

    [Networked]
    private TickTimer CelebrationEmoteTimer { get; set; }

    [Networked]
    private NetworkBool CelebrationGapPending { get; set; }

    [Header("撃破後ランダムエモート設定")]
    [SerializeField] private float celebrationEmoteMinInterval = 2.5f;
    [SerializeField] private float celebrationEmoteMaxInterval = 4.0f;

    [Header("ネットワーク同期設定")]
    [SerializeField] private float emoteNetworkDisplayDuration = 0.25f;

    private static readonly int EmoteIndexHash = Animator.StringToHash("EmoteIndex");
    private bool _isWheelOpen;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (emoteWheelUI != null) emoteWheelUI.SetActive(false);
    }

    /// <summary>キャラクターモデル差し替え時にAnimator参照を更新する。</summary>
    public void SetCharacterAnimator(Animator value)
    {
        animator = value;
    }


    private void Update()
    {
        // 自分が操作するプレイヤーでない場合は操作を受け付けない
        if (!HasInputAuthority) return;

        // 撃破後はホットリングを開けない。開いていた場合も閉じてカーソルを戻す。
        if (IsCelebrating || (playerMovement != null && playerMovement.IsPinFinished))
        {
            if (_isWheelOpen)
            {
                _isWheelOpen = false;
                if (emoteWheelUI != null) emoteWheelUI.SetActive(false);
                ResetAllSlotHighlights();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            return;
        }

        // Rキー押下中でホイール開閉
        if (Keyboard.current != null && Keyboard.current[emoteKey].isPressed)
        {
            if (!_isWheelOpen)
            {
                OpenWheel();
            }

            // ホイールを開いている間、カーソルが合っているスロットをハイライト
            int hoveredSlot = GetSelectedEmoteFromMousePosition();
            UpdateSlotHighlight(hoveredSlot);
        }
        else
        {
            if (_isWheelOpen)
            {
                CloseWheelAndExecute();
            }
        }
    }

    private void OpenWheel()
    {
        _isWheelOpen = true;
        if (emoteWheelUI != null) emoteWheelUI.SetActive(true);

        // 開いた瞬間のカーソル位置で即座にハイライトを初期化
        ResetAllSlotHighlights();
        int hoveredSlot = GetSelectedEmoteFromMousePosition();
        UpdateSlotHighlight(hoveredSlot);

        // マウスカーソルを表示して自由移動可能にする
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseWheelAndExecute()
    {
        _isWheelOpen = false;
        ResetAllSlotHighlights();
        if (emoteWheelUI != null) emoteWheelUI.SetActive(false);

        // カーソルを元のロック状態に戻す
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 決定されたエモート番号を取得
        int selectedEmote = GetSelectedEmoteFromMousePosition();

        // 「閉じる」枠（closeSlotIndex）または中央（0）の場合は何もせず終了
        if (selectedEmote <= 0 || selectedEmote == closeSlotIndex)
        {
            return;
        }

        RpcPlayEmote(selectedEmote);
    }

    // 選択中のスロットだけ拡大＋色変更し、他を通常状態に戻す
    private void UpdateSlotHighlight(int hoveredSlot)
    {
        for (int i = 0; i < 8; i++)
        {
            bool isSelected = (hoveredSlot == i + 1);

            if (slotTransforms != null && i < slotTransforms.Length && slotTransforms[i] != null)
            {
                float targetScale = isSelected ? highlightScale : normalScale;
                Vector3 targetVec = new Vector3(targetScale, targetScale, 1f);
                slotTransforms[i].localScale = Vector3.Lerp(
                    slotTransforms[i].localScale,
                    targetVec,
                    Time.deltaTime * scaleSmoothSpeed
                );
            }

            if (slotImages != null && i < slotImages.Length && slotImages[i] != null)
            {
                slotImages[i].color = isSelected ? highlightColor : normalColor;
            }
        }
    }

    private void ResetAllSlotHighlights()
    {
        for (int i = 0; i < 8; i++)
        {
            if (slotTransforms != null && i < slotTransforms.Length && slotTransforms[i] != null)
            {
                slotTransforms[i].localScale = new Vector3(normalScale, normalScale, 1f);
            }

            if (slotImages != null && i < slotImages.Length && slotImages[i] != null)
            {
                slotImages[i].color = normalColor;
            }
        }
    }

    // 画面中央からのマウス角度で8分割（1〜8）を判定
    private int GetSelectedEmoteFromMousePosition()
    {
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 dir = mousePos - screenCenter;

        // 中央付近の操作はキャンセル（0扱い）
        if (dir.magnitude < 50f) return 0;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        // 8分割の角度判定（上を起点に時計回り）
        float shiftedAngle = (angle + 22.5f) % 360f;
        int sector = Mathf.FloorToInt(shiftedAngle / 45f);

        switch (sector)
        {
            case 1: return 1; // 上
            case 0: return 2; // 右上
            case 7: return 3; // 右
            case 6: return 4; // 右下
            case 5: return 5; // 下
            case 4: return 6; // 左下
            case 3: return 7; // 左
            case 2: return 8; // 左上（ラスト）
            default: return 0;
        }
    }

    /// <summary>
    /// ピン撃破後のランダムエモートを開始する。State Authorityからのみ呼び出す。
    /// </summary>
    public void BeginCelebration()
    {
        if (!HasStateAuthority || IsCelebrating)
            return;

        if (closeSlotIndex <= 1)
        {
            Debug.LogWarning("[EmoteWheelController] 再生可能なエモート枠がありません。", this);
            return;
        }

        IsCelebrating = true;
        CelebrationGapPending = false;
        EmoteResetTimer = TickTimer.None;
        SelectNextCelebrationEmote();
        Debug.Log($"[EmoteWheelController] 撃破後ランダムエモート開始: {Object.InputAuthority}");
    }

    private void SelectNextCelebrationEmote()
    {
        int emoteCount = closeSlotIndex - 1;
        int nextIndex = Random.Range(1, closeSlotIndex);

        // 同じエモートが連続しないようにする（2種類以上ある場合）。
        if (emoteCount > 1 && nextIndex == CurrentEmoteIndex)
        {
            nextIndex = (nextIndex % emoteCount) + 1;
        }

        CurrentEmoteIndex = nextIndex;

        float minInterval = Mathf.Max(0.25f, celebrationEmoteMinInterval);
        float maxInterval = Mathf.Max(minInterval, celebrationEmoteMaxInterval);
        float nextInterval = Random.Range(minInterval, maxInterval);
        CelebrationEmoteTimer = TickTimer.CreateFromSeconds(Runner, nextInterval);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcPlayEmote(int emoteIndex)
    {
        if (IsCelebrating || (playerMovement != null && playerMovement.IsPinFinished))
            return;

        if (emoteIndex <= 0 || emoteIndex >= closeSlotIndex)
            return;

        // Animatorへ反映される時間を確保し、ネットワークTickで後からリセットする。
        CurrentEmoteIndex = emoteIndex;
        EmoteResetTimer = TickTimer.CreateFromSeconds(Runner, emoteNetworkDisplayDuration);

        Debug.Log($"EmoteWheel: Player {Object.InputAuthority} がエモート {emoteIndex} を再生します。");

        // バク宙エモートなら踏切タイミングに合わせて実際にジャンプさせる
        if (emoteIndex == backflipEmoteIndex && playerMovement != null)
        {
            StartCoroutine(DoBackflipJumpRoutine());
        }
    }

    private IEnumerator DoBackflipJumpRoutine()
    {
        yield return new WaitForSeconds(backflipJumpDelay);
        if (playerMovement != null)
        {
            playerMovement.TriggerEmoteJump(backflipJumpPower);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        // 撃破後は試合オブジェクトが破棄されるまでランダムエモートを継続する。
        if (IsCelebrating)
        {
            if (CelebrationEmoteTimer.Expired(Runner))
            {
                // 既存ホットリングと同じく、いったん0へ戻してから次のエモートを開始する。
                // Animator側の「再生中エモート→待機→次のエモート」の遷移を確実に通す。
                if (!CelebrationGapPending)
                {
                    CurrentEmoteIndex = 0;
                    CelebrationGapPending = true;
                    CelebrationEmoteTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
                }
                else
                {
                    CelebrationGapPending = false;
                    SelectNextCelebrationEmote();
                }
            }
            return;
        }

        // 通常のホットリングエモートは従来どおり短時間だけ表示する。
        if (CurrentEmoteIndex != 0 && EmoteResetTimer.Expired(Runner))
        {
            CurrentEmoteIndex = 0;
            EmoteResetTimer = TickTimer.None;
        }
    }

    public override void Render()
    {
        if (animator == null)
            return;

        // ホスト・クライアントとも、同期されたエモート番号をAnimatorへ反映する。
        animator.SetInteger(EmoteIndexHash, CurrentEmoteIndex);
    }
}