using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ToonyTinyPeopleの男性29体・女性20体を検索し、Resources用データベースを生成する。
/// ID順は男性0～28、女性29～48で固定する。
/// </summary>
public static class BuildCharacterModelDatabase
{
    private const string MaleFolder = "Assets/asset/ToonyTinyPeople/TT_Citizens/prefabs/male";
    private const string FemaleFolder = "Assets/asset/ToonyTinyPeople/TT_Citizens/prefabs/female";
    private const string ResourcesFolder = "Assets/Resources";
    private const string DatabasePath = ResourcesFolder + "/CharacterModelDatabase.asset";

    [MenuItem("Tools/BBB/Build Character Model Database")]
    private static void Build()
    {
        List<GameObject> malePrefabs = LoadPrefabs(MaleFolder);
        List<GameObject> femalePrefabs = LoadPrefabs(FemaleFolder);

        if (malePrefabs.Count != 29 || femalePrefabs.Count != 20)
        {
            EditorUtility.DisplayDialog(
                "Character database check failed",
                $"モデル数が想定と異なります。\n男性: {malePrefabs.Count}/29\n女性: {femalePrefabs.Count}/20\n\nPrefabの配置先を確認してください。",
                "OK");
            return;
        }

        Directory.CreateDirectory(ResourcesFolder);
        CharacterModelDatabase database = AssetDatabase.LoadAssetAtPath<CharacterModelDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<CharacterModelDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        List<CharacterModelDatabase.Entry> entries = new List<CharacterModelDatabase.Entry>(49);
        int id = 0;
        AddEntries(malePrefabs, false, entries, ref id);
        AddEntries(femalePrefabs, true, entries, ref id);

        database.EditorSetEntries(entries);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Character database created",
            $"CharacterModelDatabaseを更新しました。\n男性: 0～28（{malePrefabs.Count}体）\n女性: 29～48（{femalePrefabs.Count}体）\n合計: {entries.Count}体\n\nPlayer側はResourcesから自動で読み込みます。",
            "OK");

        Debug.Log($"[BBB] CharacterModelDatabase generated: {entries.Count} models. " + DatabasePath);
    }

    private static List<GameObject> LoadPrefabs(string folder)
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(prefab => prefab != null)
            .ToList();
    }

    private static void AddEntries(
        List<GameObject> prefabs,
        bool isFemale,
        List<CharacterModelDatabase.Entry> entries,
        ref int id)
    {
        foreach (GameObject prefab in prefabs)
        {
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
                Debug.LogWarning($"[BBB] Animatorが見つからないモデルです: {AssetDatabase.GetAssetPath(prefab)}", prefab);

            entries.Add(new CharacterModelDatabase.Entry(
                id,
                prefab.name,
                isFemale,
                prefab));
            id++;
        }
    }
}
