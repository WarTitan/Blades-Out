// FILE: PsychoactiveEffectsManager.cs
// Manager that owns the database and triggers effect prefabs.
// Each item has its own script (derived from PsychoactiveEffectBase) on a prefab.
// Press Alpha1 to trigger item 0 (Vodka) for testing.

using UnityEngine;
using Mirror;

[AddComponentMenu("Gameplay/Psychoactive Effects Manager")]
[DefaultExecutionOrder(50000)]
public class PsychoactiveEffectsManager : NetworkBehaviour
{
    [System.Serializable]
    public class ItemEntry
    {
        public string itemName;
        [TextArea(2, 4)]
        public string description;
        public GameObject previewPrefab;                 // optional for UI
        public PsychoactiveEffectBase effectPrefab;      // prefab with the concrete effect script
        public float durationSeconds = 10f;
        [Range(0f, 1f)] public float intensity = 0.75f;
    }

    [Header("Items Database")]
    public ItemEntry[] items;

    [Header("Debug")]
    public bool verboseLogs = false;

    private Camera targetCam;
    private PsychoactiveEffectBase current;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        ResolveCamera();
    }

    private void OnEnable()
    {
        if (isLocalPlayer) ResolveCamera();
    }

    private void OnDisable()
    {
        StopCurrentEffect();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        // Test hotkey: 1 triggers first item (Vodka)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TriggerByIndex(0);
        }
    }

    // -------- Public API --------

    public void TriggerByIndex(int index)
    {
        if (!isLocalPlayer) return;
        if (items == null || index < 0 || index >= items.Length) return;

        var entry = items[index];
        if (entry == null || entry.effectPrefab == null)
        {
            if (verboseLogs) Debug.LogWarning("[PsychoactiveEffectsManager] Missing effect prefab at index " + index);
            return;
        }

        ResolveCamera();
        if (targetCam == null)
        {
            if (verboseLogs) Debug.LogWarning("[PsychoactiveEffectsManager] No camera found.");
            return;
        }

        StopCurrentEffect();

        // Instantiate the prefab under the camera so LateUpdate runs after look and is local
        var parent = targetCam.transform;
        var inst = Instantiate(entry.effectPrefab, parent, false);
        current = inst;

        if (verboseLogs) Debug.Log("[PsychoactiveEffectsManager] Begin " + (entry.itemName != null ? entry.itemName : inst.itemName));

        current.OnFinished += OnEffectFinished;
        current.Begin(targetCam, Mathf.Max(0.01f, entry.durationSeconds), Mathf.Clamp01(entry.intensity));
    }

    public void StopCurrentEffect()
    {
        if (current != null)
        {
            current.OnFinished -= OnEffectFinished;
            current.Cancel();
            current = null;
        }
    }

    // -------- Helpers --------

    private void OnEffectFinished(PsychoactiveEffectBase eff)
    {
        if (current == eff)
        {
            current.OnFinished -= OnEffectFinished;
            current = null;
        }
    }

    private void ResolveCamera()
    {
        if (targetCam != null) return;

        var lca = GetComponent<LocalCameraActivator>();
        if (lca != null && lca.playerCamera != null)
        {
            targetCam = lca.playerCamera;
        }
        else
        {
            targetCam = GetComponentInChildren<Camera>(true);
            if (targetCam == null) targetCam = Camera.main;
        }
    }
}
