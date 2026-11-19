using System;
using UnityEditor;
using UnityEngine;

public static class SetupCharacterModelSwitching
{
    private const string PlayerPrefabPath = "Assets/Prefab/player_characterRoot.prefab";
    private const string MalePrefabPath = "Assets/asset/character/prefabs/TT_demo_male_A.prefab";
    private const string FemalePrefabPath = "Assets/asset/character/prefabs/TT_demo_female.prefab";

    [MenuItem("Tools/BBB/Setup Character Model Switching")]
    private static void Setup()
    {
        GameObject malePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MalePrefabPath);
        GameObject femalePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FemalePrefabPath);

        if (malePrefab == null || femalePrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Character setup failed",
                "男性または女性モデルPrefabが見つかりません。\n" +
                MalePrefabPath + "\n" + FemalePrefabPath,
                "OK");
            return;
        }

        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root == null)
                throw new InvalidOperationException("Player prefab could not be loaded: " + PlayerPrefabPath);

            Transform visualRoot = root.transform.Find("CharacterVisual");
            if (visualRoot == null)
                throw new InvalidOperationException("CharacterVisual was not found in player_characterRoot.prefab.");

            CharacterModelSwitcher switcher = root.GetComponent<CharacterModelSwitcher>();
            if (switcher == null)
                switcher = root.AddComponent<CharacterModelSwitcher>();

            SerializedObject serialized = new SerializedObject(switcher);
            serialized.FindProperty("maleModelPrefab").objectReferenceValue = malePrefab;
            serialized.FindProperty("femaleModelPrefab").objectReferenceValue = femalePrefab;
            serialized.FindProperty("characterVisualRoot").objectReferenceValue = visualRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            if (saved == null)
                throw new InvalidOperationException("Unity failed to save the configured player prefab.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Character setup complete",
                "player_characterRoot.prefab にCharacterModelSwitcherを設定しました。\n" +
                "男性: TT_demo_male_A\n女性: TT_demo_female\n\n" +
                "BasicSpawnerのPlayer Prefabがplayer_characterRoot.prefabを参照していることを確認してください。",
                "OK");

            Debug.Log("[BBB] Character model switching configured on " + PlayerPrefabPath);
        }
        catch (Exception ex)
        {
            Debug.LogError("[BBB] Character model setup failed. " + ex);
            EditorUtility.DisplayDialog("Character setup failed", ex.Message, "OK");
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
