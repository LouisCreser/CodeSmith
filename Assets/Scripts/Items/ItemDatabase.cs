using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Factory/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public ItemData[] items;

    readonly Dictionary<string, ItemData> byId = new();

    bool isBuilt;

    void OnEnable()
    {
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        byId.Clear();
        isBuilt = true;

        if (items == null)
            return;

        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
                continue;

            byId[item.id] = item;
        }
    }

    public bool TryGetItem(string itemId, out ItemData item)
    {
        if (!isBuilt)
            RebuildLookup();

        if (string.IsNullOrEmpty(itemId))
        {
            item = null;
            return false;
        }

        return byId.TryGetValue(itemId, out item);
    }

    public Sprite GetSpriteOrFallback(string itemId, Sprite fallback)
    {
        return TryGetItem(itemId, out ItemData item) && item != null && item.sprite != null ? item.sprite : fallback;
    }
}