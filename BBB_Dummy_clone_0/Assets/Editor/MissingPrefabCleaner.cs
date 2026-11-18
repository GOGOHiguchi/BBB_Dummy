using UnityEngine;
using UnityEditor;

public class MissingPrefabCleaner
{
    [MenuItem("Tools/Missing Prefabを一括削除")]
    public static void Clean()
    {
        // シーン内のルートオブジェクトを取得
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int count = 0;

        foreach (GameObject root in rootObjects)
        {
            count += CleanRecursive(root);
        }

        Debug.Log($"<color=green>完了：合計 {count} 個の Missing Prefab を削除しました！</color>");
    }

    private static int CleanRecursive(GameObject go)
    {
        int removedCount = 0;

        // 子要素から逆順に処理して削除
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = go.transform.GetChild(i);
            removedCount += CleanRecursive(child.gameObject);
        }

        // リンクが切れている Missing 状態かチェック (Unity 6 / 2022以降対応)
        if (PrefabUtility.IsPrefabAssetMissing(go) || PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.MissingAsset)
        {
            Undo.DestroyObjectImmediate(go);
            removedCount++;
        }

        return removedCount;
    }
}