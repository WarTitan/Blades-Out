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
        public GameObject visualPrefab;              // 3D object to show in trays

        [Header("Effect")]
        [Tooltip("Prefab spawned when this item is consumed. Can contain IItemEffect scripts.")]
        public GameObject effectPrefab;              // Effect prefab to spawn on consume (client-side)

        [Tooltip("How long the effect should last (seconds).")]
        public float effectLifetime = 5f;            // If > 0, destroys effect after this many seconds

        [Tooltip("How strong the effect is. For Flow Mosh this maps to Blend (0..1).")]
        [Range(0f, 1f)]
        public float effectIntensity = 1f;           // Passed to IItemEffect.Play as intensity
    }

    [Header("Deck")]
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

