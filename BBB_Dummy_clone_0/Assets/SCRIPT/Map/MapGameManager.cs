using UnityEngine;
using TMPro;

/// <summary>
/// GameManagerのネットワーク同期済みCurrentState/StateTimerを参照して
/// カウントダウン表示だけを各クライアントで描画する。
/// ローカルCoroutineや独自の開始時計は使用しない。
/// </summary>
public class MapGameManager : MonoBehaviour
{
    [Header("UI設定")]
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("カウントダウン音（任意）")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip countSound;
    [SerializeField] private AudioClip goSound;

    public static bool IsGameStarted { get; private set; }

    private string _lastDisplayedStage = string.Empty;

    private void Awake()
    {
        IsGameStarted = false;
        _lastDisplayedStage = string.Empty;

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private void Update()
    {
        GameManager gameManager = GameManager.Instance;

        if (gameManager == null || gameManager.Runner == null)
        {
            IsGameStarted = false;
            SetCountdownVisible(false);
            return;
        }

        switch (gameManager.CurrentState)
        {
            case GameManager.GameState.Countdown:
                IsGameStarted = false;
                UpdateCountdown(gameManager);
                break;

            case GameManager.GameState.Playing:
                IsGameStarted = true;
                _lastDisplayedStage = string.Empty;
                SetCountdownVisible(false);
                break;

            default:
                IsGameStarted = false;
                _lastDisplayedStage = string.Empty;
                SetCountdownVisible(false);
                break;
        }
    }

    private void UpdateCountdown(GameManager gameManager)
    {
        float remaining = gameManager.StateTimer.RemainingTime(gameManager.Runner) ?? 0f;

        string stage;
        if (remaining > 3.5f)
            stage = "3";
        else if (remaining > 2.5f)
            stage = "2";
        else if (remaining > 1.5f)
            stage = "1";
        else if (remaining > 0f)
            stage = "GO!";
        else
            stage = "GO!";

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = stage;
        }

        if (stage != _lastDisplayedStage)
        {
            if (stage == "GO!")
                PlaySound(goSound);
            else
                PlaySound(countSound);

            _lastDisplayedStage = stage;
        }
    }

    private void SetCountdownVisible(bool visible)
    {
        if (countdownText != null && countdownText.gameObject.activeSelf != visible)
            countdownText.gameObject.SetActive(visible);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
