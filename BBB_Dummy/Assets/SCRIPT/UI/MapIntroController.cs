using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[Serializable]
public class IntroCameraRoute
{
    [Tooltip("このルートのStartからGoalまでのカメラポイント。中間ポイントも複数設定できます。")]
    public List<Transform> points = new List<Transform>();
}

/// <summary>
/// ローカルのイントロカメラ制御。
/// CameraRoutesに複数ルートを登録すると、各ルートを順番に再生します。
/// 全ルートを終えてもSpawnが完了していなければ、最初のルートStartへ滑らかに戻り、全ルートを再生し直します。
/// Spawn完了は初回巡回を最後まで再生した後、または2巡目以降の途中で検知した時点でゲームカメラへ戻ります。
/// </summary>
public class MapIntroController : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera introCamera;
    [SerializeField] private Camera gameplayCamera;

    [Header("複数イントロルート (Route 1 -> Route 2 -> ... )")]
    [Tooltip("ルートごとにStart/Goalを登録します。各ルートのpointsには中間カメラポイントも追加できます。")]
    [SerializeField] private List<IntroCameraRoute> cameraRoutes = new List<IntroCameraRoute>();

    [Header("旧ルート (CameraRoutesが空の場合のみ使用)")]
    [Tooltip("旧バージョンとの互換用。Camera Routesが1つも登録されていない場合に使用します。")]
    [SerializeField] private List<Transform> cameraPoints = new List<Transform>();

    [Header("Motion")]
    [Min(0.1f)] [SerializeField] private float secondsPerSegment = 3f;
    [Tooltip("各ポイント間に追加する横方向の弧。0で直線")]
    [SerializeField] private float sidewaysArc = 2f;
    [Tooltip("各ポイント間に追加する高さ方向の弧")]
    [SerializeField] private float heightArc = 1f;
    [SerializeField] private bool usePointRotation = true;
    [Min(0.1f)] [SerializeField] private float routeTransitionDuration = 1f;
    [Min(0.1f)] [SerializeField] private float loopReturnDuration = 1.5f;

    [Header("Intro UI (optional)")]
    [SerializeField] private TextMeshProUGUI stageNameText;
    [SerializeField] private TextMeshProUGUI holeText;
    [SerializeField] private GameObject stageNameBackground;
    [SerializeField] private GameObject holeBackground;
    [Tooltip("テキストと背景をまとめた親CanvasGroupを割り当ててフェードに使用します。")]
    [SerializeField] private CanvasGroup introInfoCanvasGroup;
    [SerializeField] private string stageName = "CITY RUSH";
    [SerializeField] private int fallbackHoleNumber = 1;
    [Min(0f)] [SerializeField] private float introInfoDisplayDuration = 3f;
    [Min(0f)] [SerializeField] private float introInfoFadeDuration = 0.5f;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool returnToGameplayCameraOnFinish = true;
    [Min(0.1f)] [SerializeField] private float cameraReturnDuration = 1.2f;

    private Coroutine playback;
    private Coroutine introInfoPlayback;
    private bool finished;
    private bool infoShown;
    private int currentHoleNumber;

    private void Awake()
    {
        if (introCamera != null) introCamera.enabled = false;
        currentHoleNumber = Mathf.Max(1, fallbackHoleNumber);
        UpdateIntroInfoText();
        SetIntroInfoVisible(false);
    }

    private void Start()
    {
        if (playOnStart) PlayIntro();
    }

    public void PlayIntro()
    {
        if (playback != null) StopCoroutine(playback);
        finished = false;
        infoShown = false;
        StopIntroInfoPlayback();
        playback = StartCoroutine(PlaySequence());
    }

    public void SkipIntro()
    {
        if (finished) return;
        if (playback != null) StopCoroutine(playback);
        StopIntroInfoPlayback();
        playback = StartCoroutine(SkipSequence());
    }

    private IEnumerator SkipSequence()
    {
        if (introCamera != null && introCamera.enabled && returnToGameplayCameraOnFinish)
            yield return ReturnToGameplayCamera();
        playback = null;
        FinishIntro();
    }

    private List<List<Transform>> BuildRoutes()
    {
        var result = new List<List<Transform>>();
        if (cameraRoutes != null)
        {
            foreach (IntroCameraRoute route in cameraRoutes)
            {
                if (route == null || route.points == null || route.points.Count < 2)
                    continue;
                result.Add(route.points);
            }
        }

        // CameraRoutes未設定時は従来の1ルートを利用し、既存シーン設定を壊さない。
        if (result.Count == 0 && cameraPoints != null && cameraPoints.Count >= 2)
            result.Add(cameraPoints);
        return result;
    }

    private IEnumerator PlaySequence()
    {
        List<List<Transform>> routes = BuildRoutes();
        if (introCamera == null)
        {
            Debug.LogWarning("[MapIntroController] Intro Cameraが未設定です。イントロを終了します。");
            FinishIntro();
            yield break;
        }
        if (routes.Count == 0)
        {
            Debug.LogWarning("[MapIntroController] 有効なイントロルートがありません。2点以上のCamera Pointを設定してください。");
            FinishIntro();
            yield break;
        }

        for (int r = 0; r < routes.Count; r++)
        {
            for (int i = 0; i < routes[r].Count; i++)
            {
                if (routes[r][i] == null)
                {
                    Debug.LogWarning($"[MapIntroController] Route {r + 1} のPoint {i} が未設定です。イントロを終了します。");
                    FinishIntro();
                    yield break;
                }
            }
        }

        RefreshHoleNumber();
        UpdateIntroInfoText();
        if (gameplayCamera != null) gameplayCamera.enabled = false;
        introCamera.enabled = true;
        Transform firstStart = routes[0][0];
        introCamera.transform.SetPositionAndRotation(firstStart.position, firstStart.rotation);

        int cycle = 0;
        while (!finished)
        {
            bool firstCycle = cycle == 0;
            if (!firstCycle && IsEveryoneSpawned())
            {
                yield return ReturnToGameplayCamera();
                break;
            }

            if (firstCycle && !infoShown)
            {
                float totalDuration = 0f;
                foreach (var route in routes)
                    totalDuration += (route.Count - 1) * Mathf.Max(0.1f, secondsPerSegment);
                totalDuration += Mathf.Max(0, routes.Count - 1) * Mathf.Max(0.1f, routeTransitionDuration);
                introInfoPlayback = StartCoroutine(ShowIntroInfoAtMidpoint(totalDuration));
            }

            bool spawnCompleteMidCycle = false;
            for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                List<Transform> points = routes[routeIndex];

                // Between distinct camera sweeps, blend the previous goal to the next route start.
                if (routeIndex > 0)
                {
                    yield return MoveCameraSmoothly(points[0].position, points[0].rotation, routeTransitionDuration);
                }

                for (int segment = 0; segment < points.Count - 1; segment++)
                {
                    Transform from = points[segment];
                    Transform to = points[segment + 1];
                    Transform previous = segment > 0 ? points[segment - 1] : from;
                    Transform next = segment + 2 < points.Count ? points[segment + 2] : to;
                    float elapsed = 0f;
                    float duration = Mathf.Max(0.1f, secondsPerSegment);

                    while (elapsed < duration)
                    {
                        // First cycle completes every route. Later cycles can stop at the
                        // current camera position as soon as all networked spawns finish.
                        if (!firstCycle && IsEveryoneSpawned())
                        {
                            spawnCompleteMidCycle = true;
                            break;
                        }
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        float eased = t * t * (3f - 2f * t);
                        Vector3 position = CatmullRom(previous.position, from.position, to.position, next.position, eased);
                        position += CalculateArc(from.position, to.position, eased);
                        introCamera.transform.position = position;
                        if (usePointRotation)
                            introCamera.transform.rotation = Quaternion.Slerp(from.rotation, to.rotation, eased);
                        else
                            introCamera.transform.rotation = Quaternion.LookRotation(to.position - from.position, Vector3.up);
                        yield return null;
                    }
                    if (spawnCompleteMidCycle) break;
                    introCamera.transform.SetPositionAndRotation(to.position, to.rotation);
                }

                if (spawnCompleteMidCycle) break;
            }

            // The first full sweep always finishes. If spawns are still pending,
            // smoothly rewind to the first route's start and repeat all route groups.
            if (firstCycle || spawnCompleteMidCycle || IsEveryoneSpawned())
            {
                yield return ReturnToGameplayCamera();
                break;
            }
            yield return MoveCameraSmoothly(firstStart.position, firstStart.rotation, loopReturnDuration);
            cycle++;
        }

        playback = null;
        FinishIntro();
    }

    private IEnumerator MoveCameraSmoothly(Vector3 targetPosition, Quaternion targetRotation, float moveDuration)
    {
        if (introCamera == null) yield break;
        Vector3 startPosition = introCamera.transform.position;
        Quaternion startRotation = introCamera.transform.rotation;
        float duration = Mathf.Max(0.1f, moveDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            introCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, smooth);
            introCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smooth);
            yield return null;
        }
        introCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private bool IsEveryoneSpawned()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.Runner == null || !gm.Runner.IsRunning) return false;
        try { return gm.AllPlayersSpawned; }
        catch (InvalidOperationException) { return false; }
    }

    private void RefreshHoleNumber()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.Runner == null || !gm.Runner.IsRunning) return;
        try { currentHoleNumber = Mathf.Max(1, gm.CurrentHole); }
        catch (InvalidOperationException) { currentHoleNumber = Mathf.Max(1, fallbackHoleNumber); }
    }

    private void UpdateIntroInfoText()
    {
        if (stageNameText != null) stageNameText.text = stageName;
        if (holeText != null) holeText.text = $"HOLE {currentHoleNumber:00}";
        if (introInfoCanvasGroup != null) introInfoCanvasGroup.alpha = 0f;
    }

    private IEnumerator ShowIntroInfoAtMidpoint(float routeDuration)
    {
        float display = Mathf.Max(0f, introInfoDisplayDuration);
        float fade = Mathf.Max(0f, introInfoFadeDuration);
        float delay = Mathf.Max(0f, (routeDuration - display - fade * 2f) * 0.5f);
        if (delay > 0f) yield return new WaitForSeconds(delay);
        infoShown = true;
        SetIntroInfoVisible(true);
        yield return FadeIntroInfo(0f, 1f, fade);
        if (display > 0f) yield return new WaitForSeconds(display);
        yield return FadeIntroInfo(1f, 0f, fade);
        SetIntroInfoVisible(false);
        introInfoPlayback = null;
    }

    private IEnumerator FadeIntroInfo(float from, float to, float duration)
    {
        if (introInfoCanvasGroup == null || duration <= 0f)
        {
            if (introInfoCanvasGroup != null) introInfoCanvasGroup.alpha = to;
            yield break;
        }
        float elapsed = 0f;
        introInfoCanvasGroup.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            introInfoCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        introInfoCanvasGroup.alpha = to;
    }

    private void StopIntroInfoPlayback()
    {
        if (introInfoPlayback != null)
        {
            StopCoroutine(introInfoPlayback);
            introInfoPlayback = null;
        }
        SetIntroInfoVisible(false);
    }

    private void SetIntroInfoVisible(bool visible)
    {
        if (stageNameText != null) stageNameText.gameObject.SetActive(visible);
        if (holeText != null) holeText.gameObject.SetActive(visible);
        if (stageNameBackground != null) stageNameBackground.SetActive(visible);
        if (holeBackground != null) holeBackground.SetActive(visible);
        if (introInfoCanvasGroup != null)
        {
            introInfoCanvasGroup.alpha = visible ? 1f : 0f;
            introInfoCanvasGroup.interactable = false;
            introInfoCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator ReturnToGameplayCamera()
    {
        if (!returnToGameplayCameraOnFinish || gameplayCamera == null || introCamera == null) yield break;
        Vector3 startPosition = introCamera.transform.position;
        Quaternion startRotation = introCamera.transform.rotation;
        gameplayCamera.enabled = true;
        yield return null;
        Vector3 targetPosition = gameplayCamera.transform.position;
        Quaternion targetRotation = gameplayCamera.transform.rotation;
        gameplayCamera.enabled = false;
        yield return MoveCameraSmoothly(targetPosition, targetRotation, cameraReturnDuration);
    }

    private Vector3 CalculateArc(Vector3 start, Vector3 end, float t)
    {
        float arcFactor = Mathf.Sin(t * Mathf.PI);
        Vector3 direction = end - start;
        Vector3 side = Vector3.Cross(direction.normalized, Vector3.up);
        if (side.sqrMagnitude < 0.001f) side = Vector3.right;
        else side.Normalize();
        return side * (sidewaysArc * arcFactor) + Vector3.up * (heightArc * arcFactor);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void FinishIntro()
    {
        if (finished) return;
        finished = true;
        StopIntroInfoPlayback();
        if (introCamera != null) introCamera.enabled = false;
        if (returnToGameplayCameraOnFinish && gameplayCamera != null) gameplayCamera.enabled = true;
        SetIntroInfoVisible(false);
        playback = null;
        if (GameManager.Instance != null) GameManager.Instance.ReportIntroFinished();
        Debug.Log("[MapIntroController] Intro camera sequence finished.");
    }
}
