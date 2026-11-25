using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Local, camera-only map introduction for a map scene.
/// This is intentionally independent of Fusion/game-state logic for Phase 1.
/// Assign the gameplay camera, intro camera, and ordered camera points in the Inspector.
/// </summary>
public class MapIntroController : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera introCamera;
    [SerializeField] private Camera gameplayCamera;

    [Header("Ordered Camera Route (Start -> ... -> Goal)")]
    [SerializeField] private List<Transform> cameraPoints = new List<Transform>();

    [Header("Motion")]
    [Min(0.1f)]
    [SerializeField] private float secondsPerSegment = 3f;
    [Tooltip("Adds a gentle sideways arc between points. Set to 0 for straight interpolation.")]
    [SerializeField] private float sidewaysArc = 2f;
    [Tooltip("Adds a gentle height arc between points.")]
    [SerializeField] private float heightArc = 1f;
    [SerializeField] private bool usePointRotation = true;

    [Header("Intro UI (optional)")]
    [SerializeField] private TextMeshProUGUI stageNameText;
    [SerializeField] private TextMeshProUGUI holeText;
    [Tooltip("Optional background objects paired with the text. Keep the text references unchanged.")]
    [SerializeField] private GameObject stageNameBackground;
    [SerializeField] private GameObject holeBackground;
    [SerializeField] private string stageName = "CITY RUSH";
    [SerializeField] private int holeNumber = 1;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool returnToGameplayCameraOnFinish = true;
    [Min(0.1f)]
    [SerializeField] private float cameraReturnDuration = 1.2f;

    private Coroutine playback;
    private bool finished;

    private void Awake()
    {
        if (introCamera != null)
            introCamera.enabled = false;

        if (stageNameText != null)
            stageNameText.text = stageName;

        if (holeText != null)
            holeText.text = $"HOLE {Mathf.Max(1, holeNumber):00}";
    }

    private void Start()
    {
        if (playOnStart)
            PlayIntro();
    }

    /// <summary>Starts or restarts the intro camera sequence.</summary>
    public void PlayIntro()
    {
        if (playback != null)
            StopCoroutine(playback);

        finished = false;
        playback = StartCoroutine(PlaySequence());
    }

    /// <summary>Can be connected to a UI Skip button in a later phase.</summary>
    public void SkipIntro()
    {
        if (playback != null)
        {
            StopCoroutine(playback);
            playback = null;
        }

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

        // Validate the full route before changing camera states. Catmull-Rom uses
        // neighboring points, so a null entry anywhere in the list can break playback.
        for (int i = 0; i < cameraPoints.Count; i++)
        {
            if (cameraPoints[i] == null)
            {
                Debug.LogWarning($"[MapIntroController] Camera point at index {i} is missing. Ending intro.");
                FinishIntro();
                yield break;
            }
        }

        // Ensure only the intro camera renders while the sequence is playing.
        if (gameplayCamera != null)
            gameplayCamera.enabled = false;

        introCamera.enabled = true;
        if (stageNameText != null) stageNameText.gameObject.SetActive(true);
        if (holeText != null) holeText.gameObject.SetActive(true);
        if (stageNameBackground != null) stageNameBackground.SetActive(true);
        if (holeBackground != null) holeBackground.SetActive(true);
        introCamera.transform.SetPositionAndRotation(cameraPoints[0].position, cameraPoints[0].rotation);

        for (int segment = 0; segment < cameraPoints.Count - 1; segment++)
        {
            Transform from = cameraPoints[segment];
            Transform to = cameraPoints[segment + 1];
            if (from == null || to == null)
                continue;

            Transform previous = segment > 0 ? cameraPoints[segment - 1] : from;
            Transform next = segment + 2 < cameraPoints.Count ? cameraPoints[segment + 2] : to;

            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, secondsPerSegment);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smooth ease-in/ease-out so the camera does not abruptly start or stop.
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

            introCamera.transform.SetPositionAndRotation(to.position, to.rotation);
        }

        yield return ReturnToGameplayCamera();
        playback = null;
        FinishIntro();
    }


    private IEnumerator ReturnToGameplayCamera()
    {
        if (!returnToGameplayCameraOnFinish || gameplayCamera == null || introCamera == null)
            yield break;

        Vector3 startPosition = introCamera.transform.position;
        Quaternion startRotation = introCamera.transform.rotation;

        // Cinemachine may not have updated the disabled gameplay Camera during the intro.
        // Enable it briefly so its Brain can refresh the player-facing camera pose.
        gameplayCamera.enabled = true;
        yield return null;

        Vector3 targetPosition = gameplayCamera.transform.position;
        Quaternion targetRotation = gameplayCamera.transform.rotation;

        // Keep only the intro camera rendering during the blend.
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

        if (side.sqrMagnitude < 0.001f)
            side = Vector3.right;
        else
            side.Normalize();

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
        if (finished)
            return;

        finished = true;

        if (introCamera != null)
            introCamera.enabled = false;

        if (returnToGameplayCameraOnFinish && gameplayCamera != null)
            gameplayCamera.enabled = true;

        if (stageNameText != null)
            stageNameText.gameObject.SetActive(false);

        if (holeText != null)
            holeText.gameObject.SetActive(false);
        if (stageNameBackground != null)
            stageNameBackground.SetActive(false);
        if (holeBackground != null)
            holeBackground.SetActive(false);

        playback = null;
        Debug.Log("[MapIntroController] Intro camera sequence finished.");
    }
}