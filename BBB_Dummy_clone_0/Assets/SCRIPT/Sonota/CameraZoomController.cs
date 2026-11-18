using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraZoomController : MonoBehaviour
{
    [Header("Zoom Settings")]

    [SerializeField] private float minRadius = 4f;
    [SerializeField] private float maxRadius = 12f;

    [SerializeField] private float zoomSensitivity = 0.01f;

    [SerializeField] private float zoomSmoothTime = 0.12f;

    private CinemachineOrbitalFollow orbitalFollow;

    private float targetRadius;
    private float zoomVelocity;

    private void Awake()
    {
        orbitalFollow = GetComponent<CinemachineOrbitalFollow>();

        if (orbitalFollow == null)
        {
            Debug.LogError("CinemachineOrbitalFollow が見つかりません。");
            enabled = false;
            return;
        }

        targetRadius = Mathf.Clamp(
            orbitalFollow.Radius,
            minRadius,
            maxRadius
        );

        orbitalFollow.Radius = targetRadius;
    }

    private void Update()
    {
        if (Mouse.current == null || Keyboard.current == null)
            return;

        // 左Ctrlを押している間だけズーム操作
        if (Keyboard.current.leftCtrlKey.isPressed)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;

            if (scroll != 0)
            {
                // 上方向：ズームイン
                // 下方向：ズームアウト
                targetRadius -= scroll * zoomSensitivity;

                targetRadius = Mathf.Clamp(
                    targetRadius,
                    minRadius,
                    maxRadius
                );
            }
        }

        // 滑らかにズーム
        orbitalFollow.Radius = Mathf.SmoothDamp(
            orbitalFollow.Radius,
            targetRadius,
            ref zoomVelocity,
            zoomSmoothTime
        );
    }
}
