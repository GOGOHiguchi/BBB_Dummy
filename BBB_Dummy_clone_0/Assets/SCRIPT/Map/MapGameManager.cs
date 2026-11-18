using UnityEngine;
using System.Collections;
using TMPro;

public class MapGameManager : MonoBehaviour
{
    [Header("UI設定")]
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("タイミング調整")]
    [Tooltip("「GO!」が表示されてから消える＆ゲーム開始するまでの時間（秒）")]
    [SerializeField] private float goDisplayDelay = 1.5f; // 1.0秒から1.5秒に増やして余韻を持たせます

    [Header("カウントダウン音（任意）")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip countSound;
    [SerializeField] private AudioClip goSound;

    public static bool IsGameStarted { get; private set; } = false;

    private void Start()
    {
        IsGameStarted = false;
        StartCoroutine(StartCountdown());
    }

    private IEnumerator StartCountdown()
    {
        SetText("3");
        PlaySound(countSound);
        yield return new WaitForSeconds(1f);

        SetText("2");
        PlaySound(countSound);
        yield return new WaitForSeconds(1f);

        SetText("1");
        PlaySound(countSound);
        yield return new WaitForSeconds(1f);

        // 「GO!」表示
        SetText("GO!");
        PlaySound(goSound);

        // ★ 「GO!」の表示時間＆消えるまでの遅延（1.5秒待機）
        yield return new WaitForSeconds(goDisplayDelay);

        // ★ 「GO!」が消えるタイミングでゲーム開始！
        IsGameStarted = true;

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    private void SetText(string text)
    {
        if (countdownText != null)
        {
            countdownText.text = text;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}