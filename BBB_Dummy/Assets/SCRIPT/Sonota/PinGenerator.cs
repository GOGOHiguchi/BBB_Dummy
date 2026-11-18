using UnityEngine;

public class PinGenerator : MonoBehaviour
{
    [Header("ボウリングピンの設定")]
    [Tooltip("配置するピンのプレハブ")]
    public GameObject pinPrefab; 
    
    [Tooltip("列の数 (14列で105本になります)")]
    public int rows = 14; 
    
    [Tooltip("ピン同士の中心間距離")]
    public float spacing = 0.3f; 

    // インスペクターの歯車マーク(または右クリック)から「Generate Pins」を押して実行
    [ContextMenu("Generate Pins")]
    public void GeneratePins()
    {
        // 誤って何度も押した時のために、すでに生成されている子オブジェクトのピンを一旦すべて削除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        if (pinPrefab == null)
        {
            Debug.LogError("ピンのプレハブがアタッチされていません！");
            return;
        }

        // 正三角形の高さの比率 (1 : 2 : √3 の計算)
        float rowHeight = spacing * Mathf.Sqrt(3f) / 2f;

        int totalPins = 0;

        // 列(奥方向)のループ
        for (int row = 0; row < rows; row++)
        {
            // 行(横方向)のループ
            for (int col = 0; col <= row; col++)
            {
                // 中央揃えにするためのX座標のオフセット計算
                float xPos = (col - (row / 2f)) * spacing;
                // 奥方向(Z座標)の計算
                float zPos = row * rowHeight;

                // 配置位置の決定 (ジェネレーター自身の位置と回転を基準にする)
                Vector3 localPos = new Vector3(xPos, 0, zPos);
                Vector3 worldPos = transform.TransformPoint(localPos);

                // ピンを生成
                GameObject pin = Instantiate(pinPrefab, worldPos, transform.rotation);
                pin.transform.SetParent(transform); // ジェネレーターの子オブジェクトにして整理
                pin.name = $"Pin_{totalPins + 1}";
                
                totalPins++;
            }
        }
        
        Debug.Log($"合計 {totalPins} 本のピンを配置しました！");
    }
}