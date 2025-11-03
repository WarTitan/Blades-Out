// FILE: ItemDeck.cs
// FULL FILE (ASCII only)
//
// Scene singleton holding all item definitions.
// Each entry defines a name, a visual prefab (tray model), and an effect prefab (VFX + optional scripts).
//
// Put this on a scene object and fill 'items' in the inspector.

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
        public GameObject effectPrefab;              // Effect prefab to spawn on consume (client-side)
        public float effectLifetime = 0f;            // If > 0, destroys effect after this many seconds
    }

    [Header("Deck")]
    public List<ItemEntry> items = new List<ItemEntry>();

    public int Count => items != null ? items.Count : 0;

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
