using UnityEngine;

public static class PlayerBallData
{
    public static BallType SelectedType { get; private set; }

    static PlayerBallData()
    {
        // 前回保存したボールタイプを読み込み（デフォルトは 0: Speed）
        SelectedType = (BallType)PlayerPrefs.GetInt("SavedBallType", 0);
    }

    public static void SetBallType(BallType type)
    {
        SelectedType = type;
        PlayerPrefs.SetInt("SavedBallType", (int)type);
        PlayerPrefs.Save();
    }
}