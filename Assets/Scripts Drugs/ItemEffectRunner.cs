using UnityEngine;

[AddComponentMenu("Gameplay/Items/Item Effect Runner")]
public class ItemEffectRunner : MonoBehaviour
{
    [Header("References")]
    public ItemDeck itemDeck;          // leave this empty on the prefab
    public Transform effectParent;     // optional

    [Header("Debug")]
    public bool verboseLogs = false;

    private void Awake()
    {
        // If nothing is assigned in inspector, find the ItemDeck in the scene
        if (itemDeck == null)
        {
#if UNITY_2023_1_OR_NEWER
            itemDeck = Object.FindFirstObjectByType<ItemDeck>();
#else
            itemDeck = Object.FindObjectOfType<ItemDeck>();
#endif

            if (verboseLogs)
            {
                if (itemDeck != null)
                    Debug.Log("[ItemEffectRunner] Auto-found ItemDeck: " + itemDeck.name);
                else
                    Debug.LogWarning("[ItemEffectRunner] No ItemDeck found in scene.");
            }
        }

        // Optional: auto-assign effectParent to the main camera
        if (effectParent == null && Camera.main != null)
        {
            effectParent = Camera.main.transform;
        }
    }

    // ... (rest of the script stays as it is) ...
}
