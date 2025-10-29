// FILE: ItemDeck.cs
// NEW FILE (ASCII only)
// Scene singleton: holds all item definitions (effect prefab + 3D visual).
// Server draws items by id (index in the list).

using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Items/Item Deck")]
public class ItemDeck : MonoBehaviour
{
    [System.Serializable]
    public class ItemEntry
    {
        public string itemName;
        public GameObject visualPrefab;              // 3D object to show in trays
        public PsychoactiveEffectBase effectPrefab;  // effect to apply when consumed (optional for now)
        public float durationSeconds = 10f;
        public float intensity = 1f;
    }

    public static ItemDeck Instance { get; private set; }

    public List<ItemEntry> items = new List<ItemEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public int Count { get { return items != null ? items.Count : 0; } }

    public ItemEntry Get(int id)
    {
        if (id < 0 || id >= Count) return null;
        return items[id];
    }

    // Simple server-side draw. Replace with RNG/weighted logic as needed.
    public int DrawRandomId()
    {
        if (Count <= 0) return -1;
        return Random.Range(0, Count);
    }
}
