using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PinManager : MonoBehaviour
{
    public static PinManager Instance { get; private set; }

    private List<BowlingPin> allPins = new List<BowlingPin>();

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

    private void Start()
    {
        // シーン内の全ピンを取得してリスト化
        allPins.AddRange(FindObjectsByType<BowlingPin>());
        Debug.Log($"[PinManager] ピンを {allPins.Count} 本認識しました。");
    }

    /// <summary>
    /// ★追加: 現在倒れていて、かつ未集計のピンの数をカウントし、集計済みにする
    /// </summary>
    public int CountAndMarkKnockedPins()
    {
        int count = 0;
        foreach (var pin in allPins)
        {
            // ピンが存在し、倒れていて、まだスコア化されていない場合
            if (pin != null && pin.IsKnockedDown() && !pin.IsScored)
            {
                count++;
                pin.IsScored = true; // 集計済みにマークする
            }
        }
        return count;
    }
}