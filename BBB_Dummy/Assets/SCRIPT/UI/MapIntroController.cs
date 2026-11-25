using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Local camera-only map intro. The first route always reaches the goal.
/// If player spawns are incomplete, the camera rewinds smoothly and repeats.
/// Spawn completion during a repeated route transitions from the current camera pose.
/// </summary>
public class MapIntroController : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera introCamera;
    [SerializeField] private Camera gameplayCamera;
    [Header("Ordered Camera Route (Start -> ... -> Goal)")]
    [SerializeField] private List<Transform> cameraPoints = new List<Transform>();
    [Header("Motion")]
    [Min(0.1f)] [SerializeField] private float secondsPerSegment = 3f;
    [Tooltip("Adds a gentle sideways arc between points. Set to 0 for straight interpolation")]
    [SerializeField] private float sidewaysArc = 2f;
    [Tooltip("Adds a gentle height arc between points")]
    [SerializeField] private float heightArc = 1f;
    [SerializeField] private bool usePointRotation = true;
    [Min(0.1f)] [SerializeField] private float loopReturnDuration = 1.5f;
    [Header("Intro UI (optional)")]
    [SerializeField] private TextMeshProUGUI stageNameText;
    [SerializeField] private TextMeshProUGUI holeText;
    [SerializeField] private GameObject stageNameBackground;
    [SerializeField] private GameObject holeBackground;
    [Tooltip("Assign a CanvasGroup on a common parent of the text/background elements for fade animation.")]
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

    private IEnumerator PlaySequence()
    {
        if (introCamera == null)
        {
            Debug.LogWarning("[MapIntroController] Intro Camera is not assigned. Ending intro.");
            FinishIntro();
            yield break;
        }
        if (cameraPoints == null || cameraPoints.Count < 2)
        {
            Debug.LogWarning("[MapIntroController] At least two camera points are required. Ending intro.");
            FinishIntro();
            yield break;
        }
        for (int i = 0; i < cameraPoints.Count; i++)
        {
            if (cameraPoints[i] == null)
            {
                Debug.LogWarning($"[MapIntroController] Camera point at index {i} is missing. Ending intro.");
                FinishIntro();
                yield break;
            }
        }

        RefreshHoleNumber();
        UpdateIntroInfoText();
        if (gameplayCamera != null) gameplayCamera.enabled = false;
        introCamera.enabled = true;
        introCamera.transform.SetPositionAndRotation(cameraPoints[0].position, cameraPoints[0].rotation);

        int cycle = 0;
        while (!finished)
        {
            bool firstRoute = cycle == 0;
            if (!firstRoute && IsEveryoneSpawned())
            {
                yield return ReturnToGameplayCamera();
                break;
            }

            if (firstRoute && !infoShown)
            {
                float routeDuration = (cameraPoints.Count - 1) * Mathf.Max(0.1f, secondsPerSegment);
                introInfoPlayback = StartCoroutine(ShowIntroInfoAtMidpoint(routeDuration));
            }

            bool finishedDuringLoop = false;
            for (int segment = 0; segment < cameraPoints.Count - 1; segment++)
            {
                Transform from = cameraPoints[segment];
                Transform to = cameraPoints[segment + 1];
                Transform previous = segment > 0 ? cameraPoints[segment - 1] : from;
                Transform next = segment + 2 < cameraPoints.Count ? cameraPoints[segment + 2] : to;
                float elapsed = 0f;
                float duration = Mathf.Max(0.1f, secondsPerSegment);

                while (elapsed < duration)
                {
                    // The first pass is never cut short. On repeat passes, stop when
                    // the networked spawn-complete flag becomes true, at the current pose.
                    if (!firstRoute && IsEveryoneSpawned())
                    {
                        finishedDuringLoop = true;
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
                if (finishedDuringLoop) break;
                introCamera.transform.SetPositionAndRotation(to.position, to.rotation);
            }

            // The first pass always finishes its route. If spawn completion is reached,
            // it then returns to the player camera. Otherwise, rewind and repeat.
            if (firstRoute || finishedDuringLoop || IsEveryoneSpawned())
            {
                yield return ReturnToGameplayCamera();
                break;
            }
            yield return ReturnCameraToRouteStart();
            cycle++;
        }

        playback = null;
        FinishIntro();
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

    private IEnumerator ReturnCameraToRouteStart()
    {
        if (introCamera == null || cameraPoints.Count == 0) yield break;
        Vector3 startPosition = introCamera.transform.position;
        Quaternion startRotation = introCamera.transform.rotation;
        Vector3 targetPosition = cameraPoints[0].position;
        Quaternion targetRotation = cameraPoints[0].rotation;
        float duration = Mathf.Max(0.1f, loopReturnDuration);
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
        float duration = Mathf.Max(0.1f, cameraReturnDuration);
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
