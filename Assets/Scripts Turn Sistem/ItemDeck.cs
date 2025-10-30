// FILE: ItemDeck.cs
// FULL FILE (ASCII only)
//
// Scene singleton that holds your item definitions and provides random draws.
// IMPORTANT: Put exactly one ItemDeck in the gameplay scene.
// For fast testing, it can auto-fill N placeholder items if the list is empty.

using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Items/Item Deck")]
public class ItemDeck : MonoBehaviour
{
    public static ItemDeck Instance;

    [System.Serializable]
    public class ItemEntry
    {
        public string itemName;
        public GameObject visualPrefab;   // optional 3D visual for trays
        public GameObject effectPrefab;   // optional effect to run on consume
        public int weight = 1;            // reserved if you add weighted draws later
    }

    [Header("Items")]
    public List<ItemEntry> items = new List<ItemEntry>();

    [Header("Testing")]
    public bool autoFillIfEmptyInPlayMode = true;
    public int autoFillCount = 20;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (Application.isPlaying && items != null && items.Count == 0 && autoFillIfEmptyInPlayMode)
        {
            for (int i = 0; i < autoFillCount; i++)
                items.Add(new ItemEntry { itemName = "TestItem_" + i, weight = 1 });
            Debug.Log("[ItemDeck] Auto-filled " + autoFillCount + " test items (no prefabs).");
        }

        Debug.Log("[ItemDeck] Ready. Count=" + Count);
    }

    public int Count { get { return items != null ? items.Count : 0; } }

    public ItemEntry Get(int id)
    {
        if (id < 0 || id >= Count) return null;
        return items[id];
    }

    // Simple uniform draw. Replace with weighted logic if needed.
    public int DrawRandomId()
    {
        int c = Count;
        if (c <= 0) return -1;
        return Random.Range(0, c);
    }
}
