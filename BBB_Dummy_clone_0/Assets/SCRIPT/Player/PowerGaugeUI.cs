using UnityEngine;
using UnityEngine.UI;
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
        if (PlayerMovement.Local == null || powerSlider == null) return;

        float currentCharge = PlayerMovement.Local.CurrentCharge;
        bool isCharging = PlayerMovement.Local.IsHoldingBall && currentCharge > 0f;

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