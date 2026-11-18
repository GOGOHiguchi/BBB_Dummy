using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingUI : MonoBehaviour
{
    public static LoadingUI Instance { get; private set; }

    [Header("パネル・テキスト・バー参照")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Image bgImage;           // ★ 背景のImageコンポーネント
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Slider progressBar;

    [Header("ランダム背景画像リスト")]
    [SerializeField] private Sprite[] loadingSprites; // ★ 切り替えたい画像（Sprite）を割り当てる

    [Header("アニメーション設定")]
    [Tooltip("ドットが切り替わる間隔（秒）")]
    [SerializeField] private float dotInterval = 0.4f;

    private Coroutine loadingAnimationCoroutine;
    private Coroutine progressCoroutine;
    private string baseText = "Now Loading";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Show(string message = "Now Loading")
    {
        baseText = message;

        // ★ 表示されるたびに背景画像をランダム選定
        SetRandomBackground();

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);

            if (loadingAnimationCoroutine != null) StopCoroutine(loadingAnimationCoroutine);
            if (progressCoroutine != null) StopCoroutine(progressCoroutine);

            loadingAnimationCoroutine = StartCoroutine(AnimateTextRoutine());
            progressCoroutine = StartCoroutine(AnimateProgressRoutine());
        }
    }

    private void SetRandomBackground()
    {
        if (bgImage != null && loadingSprites != null && loadingSprites.Length > 0)
        {
            int randomIndex = Random.Range(0, loadingSprites.Length);
            bgImage.sprite = loadingSprites[randomIndex];
        }
    }

    public void Hide()
    {
        if (loadingAnimationCoroutine != null)
        {
            StopCoroutine(loadingAnimationCoroutine);
            loadingAnimationCoroutine = null;
        }

        if (progressCoroutine != null)
        {
            StopCoroutine(progressCoroutine);
            progressCoroutine = null;
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    // ドット「.」→「..」→「...」のアニメーション
    private IEnumerator AnimateTextRoutine()
    {
        int dotCount = 1;

        while (true)
        {
            if (loadingText != null)
            {
                string dots = new string('.', dotCount);
                loadingText.text = $"{baseText}{dots}";
            }

            yield return new WaitForSeconds(dotInterval);
            dotCount = (dotCount % 3) + 1;
        }
    }

    // 緩急をつけて上がり、95%からは超低速で99%を目指す処理
    private IEnumerator AnimateProgressRoutine()
    {
        float currentProgress = 0f;

        // ★ 0% ～ 95% までの合計時間を 17.5 秒に固定
        float totalTime = 17.5f;

        // 1. 途中の目標 % をランダムに決定
        float p1 = Random.Range(20f, 35f);
        float p2 = Random.Range(40f, 58f);
        float p3 = Random.Range(65f, 78f);
        float p4 = Random.Range(82f, 88f);
        float p5 = 95f;

        // 2. 各区間にかける「時間の重み（ランダム）」を設定
        float w1 = Random.Range(0.5f, 1.5f);
        float w2 = Random.Range(1.0f, 3.0f);
        float w3 = Random.Range(0.8f, 2.0f);
        float w4 = Random.Range(1.5f, 3.5f);
        float w5 = Random.Range(4.0f, 8.0f);

        float sumWeight = w1 + w2 + w3 + w4 + w5;

        // 3. 合計時間が『ぴったり 17.5秒』になるよう分配
        (float target, float duration)[] steps = new (float, float)[]
        {
            (p1, totalTime * (w1 / sumWeight)),
            (p2, totalTime * (w2 / sumWeight)),
            (p3, totalTime * (w3 / sumWeight)),
            (p4, totalTime * (w4 / sumWeight)),
            (p5, totalTime * (w5 / sumWeight))
        };

        UpdateProgressUI(0f);

        // 95% までの通常カウントアップ
        foreach (var step in steps)
        {
            float startProgress = currentProgress;
            float elapsedTime = 0f;

            while (elapsedTime < step.duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / step.duration);
                t = Mathf.SmoothStep(0f, 1f, t);

                currentProgress = Mathf.Lerp(startProgress, step.target, t);
                UpdateProgressUI(currentProgress);

                yield return null;
            }
        }

        // ★ 95% 到達後：1.5〜2.5秒ごとに1%ずつ進む
        while (currentProgress < 99f)
        {
            yield return new WaitForSeconds(Random.Range(1.5f, 2.5f));

            currentProgress += 1f;
            if (currentProgress > 99f) currentProgress = 99f;

            UpdateProgressUI(currentProgress);
        }

        UpdateProgressUI(99f);
    }

    private void UpdateProgressUI(float progress)
    {
        if (progressText != null)
        {
            progressText.text = $"{Mathf.FloorToInt(progress)}%";
        }

        if (progressBar != null)
        {
            progressBar.value = (progressBar.maxValue == 1f) ? progress / 100f : progress;
        }
    }

    // 処理完了時に100%まで進めてから閉じる
    public void CompleteAndHide()
    {
        if (loadingAnimationCoroutine != null)
        {
            StopCoroutine(loadingAnimationCoroutine);
            loadingAnimationCoroutine = null;
        }

        if (progressCoroutine != null)
        {
            StopCoroutine(progressCoroutine);
            progressCoroutine = null;
        }

        StartCoroutine(CompleteProgressRoutine());
    }

    private IEnumerator CompleteProgressRoutine()
    {
        float start = progressBar != null
            ? (progressBar.maxValue == 1f ? progressBar.value * 100f : progressBar.value)
            : 99f;

        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float progress = Mathf.Lerp(start, 100f, t);

            UpdateProgressUI(progress);

            yield return null;
        }

        UpdateProgressUI(100f);

        yield return new WaitForSeconds(0.2f);

        Hide();
    }
}