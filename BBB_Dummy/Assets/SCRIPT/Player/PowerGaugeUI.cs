using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class PowerGaugeUI : MonoBehaviour
{
    [Header("スライダーと参照")]
    [SerializeField] private Slider powerSlider;       // Direction: Bottom To Top に設定したスライダー
    [SerializeField] private TMP_Text percentageText;

    [Header("ゲージ画像")]
    [SerializeField] private Image normalFillImage;    // 0~100%用の緑ゲージ (Image Type: Filled / Vertical)
    [SerializeField] private Image overPowerFillImage; // 100~115%用の赤ゲージ (100%の上部に配置)

    [Header("設定")]
    [SerializeField] private float maxForce = 90f;     // PlayerMovement の maxThrowForce に合わせる

    // クライアント側のゲージ開始遅延をなくすための表示専用チャージ予測。
    private float _predictedCharge;
    private bool _wasLocalCharging;
    private float _predictionReleaseTime;

    private void Start()
    {
        if (powerSlider != null)
        {
            powerSlider.minValue = 0f;
            // スライダー全体の長さを 115% 相当に設定
            powerSlider.maxValue = maxForce * 1.15f;
            powerSlider.value = 0f;
        }

        if (overPowerFillImage != null)
        {
            overPowerFillImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        PlayerMovement player = PlayerMovement.Local;
        if (player == null || powerSlider == null) return;

        bool isHoldingBall = player.IsHoldingBall;
        bool localCharging = isHoldingBall && Mouse.current != null &&
                             Mouse.current.leftButton.isPressed;

        if (!isHoldingBall)
        {
            _predictedCharge = 0f;
            _wasLocalCharging = false;
            _predictionReleaseTime = 0f;
        }
        else if (localCharging)
        {
            if (!_wasLocalCharging)
            {
                _predictedCharge = Mathf.Max(_predictedCharge, player.CurrentCharge);
            }

            _predictedCharge = Mathf.Min(
                _predictedCharge + player.ChargeRate * Time.deltaTime,
                maxForce * 1.15f
            );
        }
        else
        {
            if (_wasLocalCharging)
            {
                // リリース後はサーバーの投球確定を少しだけ待つ。
                _predictionReleaseTime = Time.unscaledTime + 0.6f;
            }

            _predictedCharge = Mathf.Max(_predictedCharge, player.CurrentCharge);

            // サーバー側でチャージが成立しなかった場合の表示残留を防止。
            if (player.CurrentCharge <= 0f &&
                _predictionReleaseTime > 0f &&
                Time.unscaledTime >= _predictionReleaseTime)
            {
                _predictedCharge = 0f;
                _predictionReleaseTime = 0f;
            }
        }

        _wasLocalCharging = localCharging;

        // UIのみローカル予測を使用。投球速度やネットワーク上のCurrentChargeは変更しない。
        float currentCharge = Mathf.Max(player.CurrentCharge, _predictedCharge);
        bool isCharging = isHoldingBall && currentCharge > 0f;

        // チャージ中のみ表示切り替え
        if (powerSlider.gameObject.activeSelf != isCharging)
        {
            powerSlider.gameObject.SetActive(isCharging);
        }

        if (!isCharging) return;

        // 100%を超えているか判定
        bool isOverPower = currentCharge > maxForce;

        // --- Handle（つまみ）の位置制御 ---
        float visualSliderValue = currentCharge;

        if (isOverPower)
        {
            // 100%を超えた瞬間に最高点（115%地点）へHandleを一気に跳ね上げる
            visualSliderValue = maxForce * 1.15f;
        }

        powerSlider.value = visualSliderValue;

        // --- ゲージ描画の制御 ---
        // 1. 通常ゲージ (0 ~ 100%)
        if (normalFillImage != null)
        {
            // 0 ~ maxForce の範囲を 0.0 ~ 1.0 に正規化して伸ばす
            float normalRatio = Mathf.Clamp01(currentCharge / maxForce);
            normalFillImage.fillAmount = normalRatio;
        }

        // 2. 追加の限界突破赤ゲージ (101 ~ 115%)
        if (overPowerFillImage != null)
        {
            if (isOverPower)
            {
                if (!overPowerFillImage.gameObject.activeSelf)
                {
                    overPowerFillImage.gameObject.SetActive(true);
                }
                // 100%を超えた瞬間に赤ゲージを全表示にする
                overPowerFillImage.fillAmount = 1.0f;
            }
            else
            {
                if (overPowerFillImage.gameObject.activeSelf)
                {
                    overPowerFillImage.gameObject.SetActive(false);
                }
            }
        }

        // --- ％テキストの更新 ---
        if (percentageText != null)
        {
            float percent = (currentCharge / maxForce) * 100f;
            percentageText.text = $"{Mathf.FloorToInt(percent)}";
        }
    }
}