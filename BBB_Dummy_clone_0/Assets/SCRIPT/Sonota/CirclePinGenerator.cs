using UnityEngine;

public class CirclePinGenerator : MonoBehaviour
{
    [Header("円形ピンの設定")]
    [Tooltip("配置するピンのプレハブ")]
    public GameObject pinPrefab; 
    
    [Tooltip("層の数 (2層で中心1本+周囲6本=7本になります)")]
    public int rows = 2; 
    
    [Tooltip("ピン同士の中心間距離(半径の基準)")]
    public float spacing = 0.3f; 

    [ContextMenu("Generate Pins")]
    public void GeneratePins()
    {
        // 既存のピンをすべて削除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        if (pinPrefab == null)
        {
            Debug.LogError("ピンのプレハブがアタッチされていません！");
            return;
        }

        int totalPins = 0;

        // 層（リング）ごとのループ
        for (int row = 0; row < rows; row++)
        {
            if (row == 0)
            {
                // 1層目：中心に1本だけ配置
                PlacePin(Vector3.zero, totalPins);
                totalPins++;
            }
            else
            {
                // 2層目以降：6の倍数で円周上にピンを配置
                int pinsInRing = 6 * row; 
                float radius = spacing * row; 
                float angleStep = 360f / pinsInRing; 

                for (int i = 0; i < pinsInRing; i++)
                {
                    // 角度を計算してラジアンに変換
                    float angle = i * angleStep;
                    float rad = angle * Mathf.Deg2Rad;
                    
                    // 三角関数でX, Z座標を計算 (円周上の位置)
                    float xPos = Mathf.Cos(rad) * radius;
                    float zPos = Mathf.Sin(rad) * radius;
                    
                    PlacePin(new Vector3(xPos, 0, zPos), totalPins);
                    totalPins++;
                }
            }
        }
        
        Debug.Log($"合計 {totalPins} 本のピンを円形に配置しました！");
    }

    // ピンを生成して配置する共通メソッド
    private void PlacePin(Vector3 localPos, int index)
    {
        Vector3 worldPos = transform.TransformPoint(localPos);
        GameObject pin = Instantiate(pinPrefab, worldPos, transform.rotation);
        pin.transform.SetParent(transform);
        pin.name = $"Pin_{index + 1}";
    }
}