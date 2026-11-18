using UnityEngine;

public class GradientScroll : MonoBehaviour
{
    [SerializeField] private float scrollSpeedY = 0.5f; // 流れるスピード
    [SerializeField] private int materialIndex = 0;     // 箱マテリアルのインデックス

    private Material boxMaterial;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // インスタンス化されたマテリアルを取得
            boxMaterial = rend.materials[materialIndex];
        }
    }

    void Update()
    {
        if (boxMaterial != null)
        {
            // 時間経過でOffset（Y方向）を動かす
            float offsetY = Time.time * scrollSpeedY;
            boxMaterial.mainTextureOffset = new Vector2(0, offsetY);
        }
    }
}