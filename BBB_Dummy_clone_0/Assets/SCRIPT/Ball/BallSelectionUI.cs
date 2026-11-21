using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class BallSelectionUI : MonoBehaviour
{
    [Header("ボタン参照")]
    [SerializeField] private Button speedButton;
    [SerializeField] private Button powerButton;
    [SerializeField] private Button curveButton;

    [Header("選択中テキスト（任意）")]
    [SerializeField] private TMP_Text currentBallText;

    private void Start()
    {
        // ボタンイベントの登録
        if (speedButton != null) speedButton.onClick.AddListener(() => SelectBall(BallType.Speed));
        if (powerButton != null) powerButton.onClick.AddListener(() => SelectBall(BallType.Power));
        if (curveButton != null) curveButton.onClick.AddListener(() => SelectBall(BallType.Curve));

        UpdateUI();
    }

    private void Update()
    {
        // 状態の変化に合わせてUIの活性/非活性状態を更新
        UpdateUI();

        // FusionのSpawn完了前はNetworkedプロパティにアクセスしない。
        PlayerMovement player = PlayerMovement.Local;
        if (player == null || !player.IsPlayerSpawned || !player.IsHoldingBall) return;

        // 十字キーの左右入力でボール選択を順繰り切り替え
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                ChangeBallByOffset(1);
            }
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                ChangeBallByOffset(-1);
            }
        }
    }

    private void ChangeBallByOffset(int offset)
    {
        int totalTypes = System.Enum.GetValues(typeof(BallType)).Length;
        int currentIndex = (int)PlayerBallData.SelectedType;

        int newIndex = (currentIndex + offset + totalTypes) % totalTypes;
        SelectBall((BallType)newIndex);
    }

    private void SelectBall(BallType type)
    {
        // Spawn完了前・ボール非保持時の選択操作を拒否する。
        PlayerMovement player = PlayerMovement.Local;
        if (player == null || !player.IsPlayerSpawned || !player.IsHoldingBall) return;

        // 選択データをローカルに保存
        PlayerBallData.SetBallType(type);
        Debug.Log($"[BallSelection] ボールタイプを変更しました: {type}");

        // 自分の手元にあるボール本体へ即座に反映
        if (player.MyBall != null)
        {
            var bowlingBall = player.MyBall.GetComponent<BowlingBall>();
            if (bowlingBall != null)
            {
                bowlingBall.SetBallType(type);
            }
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        BallType currentType = PlayerBallData.SelectedType;

        // Spawn完了後のみNetworkedプロパティの保持状態を参照する。
        PlayerMovement player = PlayerMovement.Local;
        bool isHolding = player != null && player.IsPlayerSpawned && player.IsHoldingBall;

        // ボールを持っていない間はすべてのボタンを非活性（押せない状態）にする
        if (speedButton != null) speedButton.interactable = isHolding && (currentType != BallType.Speed);
        if (powerButton != null) powerButton.interactable = isHolding && (currentType != BallType.Power);
        if (curveButton != null) curveButton.interactable = isHolding && (currentType != BallType.Curve);

        // テキスト表示の更新
        if (currentBallText != null)
        {
            if (!isHolding)
            {
                currentBallText.text = "投球中（変更不可）";
            }
            else
            {
                switch (currentType)
                {
                    case BallType.Speed:
                        currentBallText.text = "選択中: スピードボール";
                        break;
                    case BallType.Power:
                        currentBallText.text = "選択中: パワーボール";
                        break;
                    case BallType.Curve:
                        currentBallText.text = "選択中: カーブボール";
                        break;
                }
            }
        }
    }
}