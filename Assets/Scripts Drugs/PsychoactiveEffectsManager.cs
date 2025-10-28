// FILE: PsychoactiveEffectsManager.cs
// FULL REPLACEMENT (ASCII only)
// - Hotkeys 1..9 (and numpad 1..9) trigger items[0..8].
// - Exposes events OnEffectStarted / OnEffectEnded used by PsychoactiveHUD.
// - Provides GetActiveEffectsSnapshot() used by PsychoactiveHUD to pre-populate.
// - Stacks with other effects (FOV mixing is handled by EffectFovMixer on the camera).
// - Blocks in lobby unless LocalCameraActivator forces gameplay.

using UnityEngine;
using Mirror;
using System.Collections.Generic;

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
        public GameObject previewPrefab;            // optional, not used by manager logic
        public PsychoactiveEffectBase effectPrefab; // e.g., VodkaEffect, SpeedEffect
        public float durationSeconds = 10f;
        [Range(0f, 1f)] public float intensity = 0.75f;
    }

    private class ActiveEffectRecord
    {
        public PsychoactiveEffectBase effect;
        public string name;
        public float endTime;
        public float duration;
    }

    public struct ActiveSnapshot
    {
        public string name;
        public float endTime;
        public float duration;
        public PsychoactiveEffectBase effect;
    }

    public delegate void EffectStartedHandler(string name, PsychoactiveEffectBase effect, float endTime, float duration);
    public event EffectStartedHandler OnEffectStarted;
    public event System.Action<PsychoactiveEffectBase> OnEffectEnded;

    [Header("Items Database")]
    public ItemEntry[] items;

    [Header("Rules")]
    public bool blockWhileLobbyActive = true;   // block in lobby
    public bool allowWhenForcedGameplay = true; // allow if LocalCameraActivator.IsGameplayForced

    [Header("Hotkeys")]
    public bool enableHotkeys = true;           // enable 1..9 and keypad 1..9
    public bool alsoUseNumpad = true;

    [Header("Debug")]
    public bool verboseLogs = false;

    private Camera targetCam;
    private readonly List<ActiveEffectRecord> active = new List<ActiveEffectRecord>();

    private static readonly KeyCode[] TopRow =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
        KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
    };

    private static readonly KeyCode[] Numpad =
    {
        KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3,
        KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6,
        KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9
    };

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
        CancelAllEffects();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        if (enableHotkeys && items != null && items.Length > 0)
        {
            int max = Mathf.Min(items.Length, 9);
            for (int i = 0; i < max; i++)
            {
                bool pressed = Input.GetKeyDown(TopRow[i]);
                if (!pressed && alsoUseNumpad) pressed = Input.GetKeyDown(Numpad[i]);
                if (pressed)
                {
                    TriggerByIndex(i);
                    break; // one trigger per frame
                }
            }
        }

        // auto-cancel when duration elapses
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (Time.time >= active[i].endTime && active[i].effect != null)
            {
                active[i].effect.Cancel();
            }
        }
    }

    // -------- Public API --------

    public void TriggerByIndex(int index)
    {
        if (!isLocalPlayer) return;

        string reason;
        if (!CanTriggerNow(out reason))
        {
            if (verboseLogs) Debug.Log("[PsychoactiveEffectsManager] Blocked: " + reason);
            return;
        }

        if (items == null || index < 0 || index >= items.Length)
        {
            if (verboseLogs) Debug.LogWarning("[PsychoactiveEffectsManager] Invalid item index " + index);
            return;
        }

        var entry = items[index];
        if (entry == null || entry.effectPrefab == null)
        {
            if (verboseLogs) Debug.LogWarning("[PsychoactiveEffectsManager] Missing effect prefab at index " + index);
            return;
        }

        ResolveCamera();
        if (!IsCameraUsable(targetCam, out reason))
        {
            if (verboseLogs) Debug.LogWarning("[PsychoactiveEffectsManager] " + reason);
            return;
        }

        var inst = Instantiate(entry.effectPrefab, targetCam.transform, false);
        if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);

        inst.OnFinished += OnEffectFinished;

        float d = Mathf.Max(0.01f, entry.durationSeconds);
        float end = Time.time + d;

        string shownName = string.IsNullOrEmpty(entry.itemName) ? inst.itemName : entry.itemName;
        inst.Begin(targetCam, d, Mathf.Clamp01(entry.intensity));

        var rec = new ActiveEffectRecord
        {
            effect = inst,
            name = shownName,
            endTime = end,
            duration = d
        };
        active.Add(rec);

        var started = OnEffectStarted;
        if (started != null) started(shownName, inst, end, d);

        if (verboseLogs) Debug.Log("[PsychoactiveEffectsManager] Started effect: " + shownName + " for " + d + "s (index " + index + ")");
    }

    public void CancelAllEffects()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var e = active[i].effect;
            if (e != null)
            {
                e.OnFinished -= OnEffectFinished;
                e.Cancel();
            }
        }
        active.Clear();
        var ended = OnEffectEnded;
        if (ended != null) ended(null);
    }

    public List<ActiveSnapshot> GetActiveEffectsSnapshot()
    {
        var list = new List<ActiveSnapshot>(active.Count);
        for (int i = 0; i < active.Count; i++)
        {
            var r = active[i];
            list.Add(new ActiveSnapshot
            {
                name = r.name,
                endTime = r.endTime,
                duration = r.duration,
                effect = r.effect
            });
        }
        return list;
    }

    // -------- Internals --------

    private void OnEffectFinished(PsychoactiveEffectBase eff)
    {
        if (eff != null) eff.OnFinished -= OnEffectFinished;

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].effect == eff)
            {
                active.RemoveAt(i);
                break;
            }
        }

        var ended = OnEffectEnded;
        if (ended != null) ended(eff);
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

    private bool CanTriggerNow(out string reason)
    {
        bool lobby = (LobbyStage.Instance != null && LobbyStage.Instance.lobbyActive);
        bool forced = false;

        var lca = GetComponent<LocalCameraActivator>();
        if (lca != null) forced = lca.IsGameplayForced;

        if (blockWhileLobbyActive && lobby && !(allowWhenForcedGameplay && forced))
        {
            reason = "Lobby is active and not in forced gameplay.";
            return false;
        }

        ResolveCamera();
        if (!IsCameraUsable(targetCam, out reason))
            return false;

        reason = null;
        return true;
    }

    private static bool IsCameraUsable(Camera cam, out string reason)
    {
        if (cam == null)
        {
            reason = "No camera found on local player.";
            return false;
        }
        if (!cam.gameObject.activeInHierarchy)
        {
            reason = "Camera is inactive in hierarchy.";
            return false;
        }
        reason = null;
        return true;
    }
}
