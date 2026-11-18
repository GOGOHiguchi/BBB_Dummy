
using UnityEngine;

public class LaserCoreAnimation : MonoBehaviour
{
    [Header("アニメーション設定")]
    [SerializeField] private float duration = 3.44f;

    [Header("太さの変化")]
    [SerializeField] private AnimationCurve widthCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 1f),
            new Keyframe(0.7f, 1f),
            new Keyframe(1f, 0f)
        );

    private Vector3 baseScale;
    private float elapsedTime;

    private void Awake()
    {
        // 元のScaleを保存
        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (elapsedTime >= duration) return;

        elapsedTime += Time.deltaTime;

        // 0～1に正規化
        float normalizedTime = Mathf.Clamp01(elapsedTime / duration);

        // カーブから現在の太さを取得
        float width = widthCurve.Evaluate(normalizedTime);

        // X・Zだけ変更し、Yは維持する
        transform.localScale = new Vector3(
            baseScale.x * width,
            baseScale.y,
            baseScale.z * width
        );
    }
}
