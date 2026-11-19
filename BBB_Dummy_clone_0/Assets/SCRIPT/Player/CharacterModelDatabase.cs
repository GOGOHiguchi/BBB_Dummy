using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// キャラクター選択で使用する見た目Prefabの一覧。
/// IDは保存・ネットワーク同期に使うため、生成後は並び順を変更しないこと。
/// </summary>
[CreateAssetMenu(menuName = "BBB/Character Model Database", fileName = "CharacterModelDatabase")]
public sealed class CharacterModelDatabase : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private int id;
        [SerializeField] private string displayName;
        [SerializeField] private bool isFemale;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite thumbnail;

        public int Id => id;
        public string DisplayName => displayName;
        public bool IsFemale => isFemale;
        public GameObject Prefab => prefab;
        public Sprite Thumbnail => thumbnail;

        public Entry(int id, string displayName, bool isFemale, GameObject prefab, Sprite thumbnail = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.isFemale = isFemale;
            this.prefab = prefab;
            this.thumbnail = thumbnail;
        }
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;
    public int Count => entries != null ? entries.Count : 0;

    public bool TryGetEntry(int id, out Entry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Id == id && entries[i].Prefab != null)
                {
                    entry = entries[i];
                    return true;
                }
            }
        }

        entry = null;
        return false;
    }

    public bool IsValidId(int id) => TryGetEntry(id, out _);

#if UNITY_EDITOR
    public void EditorSetEntries(List<Entry> value)
    {
        entries = value ?? new List<Entry>();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
