using UnityEngine;
using TMPro;

public class HoleTimerUI : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("表示設定")]
    [SerializeField] private string timeFormat = "{0:00}:{1:00}"; // 分:秒 表記

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.Object || !GameManager.Instance.Object.IsValid)
        {
            return;
        }

        var gm = GameManager.Instance;

        // Playing 中はカウントダウンを行う
        if (gm.CurrentState == GameManager.GameState.Playing)
        {
            SetTextActive(true);

            float remainingTime = gm.StateTimer.RemainingTime(gm.Runner) ?? 0f;
            remainingTime = Mathf.Max(0f, remainingTime);

            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);

            if (timerText != null)
            {
                timerText.text = string.Format(timeFormat, minutes, seconds);
                timerText.color = (remainingTime <= 10f) ? Color.red : Color.white;
            }
        }
        // Countdown や Waiting の間も「02:00」として表示しておく場合
        else if (gm.CurrentState == GameManager.GameState.Countdown || gm.CurrentState == GameManager.GameState.Waiting)
        {
            SetTextActive(true);
            if (timerText != null)
            {
                timerText.text = "02:00"; // 初期時間を表示
                timerText.color = Color.white;
            }
        }
        else
        {
            SetTextActive(false);
        }
    }

    private void SetTextActive(bool isActive)
    {
        if (timerText != null && timerText.gameObject.activeSelf != isActive)
        {
            timerText.gameObject.SetActive(isActive);
        }
    }
}