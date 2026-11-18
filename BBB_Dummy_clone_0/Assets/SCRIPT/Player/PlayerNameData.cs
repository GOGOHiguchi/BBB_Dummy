using UnityEngine;

public static class PlayerNameData
{
    public static string CurrentName { get; private set; }

    static PlayerNameData()
    {
        CurrentName = PlayerPrefs.GetString("SavedPlayerName", "Player");
    }

    public static void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Player";
        }

        CurrentName = name;

        // 次回起動用に保存
        PlayerPrefs.SetString("SavedPlayerName", CurrentName);
        PlayerPrefs.Save();
    }
}