// FILE: ItemDeck.cs
// FULL FILE (ASCII only)
//
// Scene singleton holding all item definitions.
// Each entry defines a name, a visual prefab (tray model),
// an effect prefab (VFX + scripts), a lifetime, and an intensity.
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
        [Header("Info")]
        public string itemName;

        [Header("Visual on tray")]
        public GameObject visualPrefab;

        [Header("Effect")]
        [Tooltip("Prefab spawned when this item is consumed. Can contain PsychoactiveEffectBase or IItemEffect.")]
        public GameObject effectPrefab;

        [Tooltip("How long the effect should last (seconds).")]
        public float effectLifetime = 5f;

        [Tooltip("How strong the effect is. For Flow Mosh this maps to Blend (0..1).")]
        [Range(0f, 1f)]
        public float effectIntensity = 1f;
    }

    [Header("All items in the game")]
    public List<ItemEntry> items = new List<ItemEntry>();

    public int Count => (items != null ? items.Count : 0);

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
