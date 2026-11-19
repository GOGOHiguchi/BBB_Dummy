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
    /// シーン内に、倒れ判定を通過したピンが1本でもあるか確認する。
    /// スコア集計前の撃破演出開始判定に使用する。
    /// </summary>
    public bool HasAnyKnockedDown()
    {
        foreach (var pin in allPins)
        {
            if (pin != null && pin.IsKnockedDown())
            {
                return true;
            }
        }
        return false;
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