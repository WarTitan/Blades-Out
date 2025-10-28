// FILE: PsychoactiveEffectsManager.cs
// FULL REPLACEMENT (ASCII only)
// - One active effect per TYPE. Retrigger extends remaining time instead of spawning a duplicate.
// - Hotkeys: 1..9 and Keypad 1..9 map to items[0..8].
// - Events used by HUD: OnEffectStarted (also fired on extend), OnEffectEnded.
// - GetActiveEffectsSnapshot() for HUD initial population.

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
        public GameObject previewPrefab;            // optional
        public PsychoactiveEffectBase effectPrefab; // concrete effect component on a prefab
        public float durationSeconds = 10f;
        [Range(0f, 1f)] public float intensity = 0.75f;
    }

    private class ActiveEffectRecord
    {
        public PsychoactiveEffectBase effect;
        public string name;
        public float endTime;
        public float totalPlannedSeconds; // grows when extended
        public System.Type effectType;
    }

    public struct ActiveSnapshot
    {
        public string name;
        public float endTime;
        public float duration; // total planned seconds
        public PsychoactiveEffectBase effect;
    }

    public delegate void EffectStartedHandler(string name, PsychoactiveEffectBase effect, float endTime, float duration);
    public event EffectStartedHandler OnEffectStarted;
    public event System.Action<PsychoactiveEffectBase> OnEffectEnded;

    [Header("Items Database")]
    public ItemEntry[] items;

    [Header("Rules")]
    public bool blockWhileLobbyActive = true;
    public bool allowWhenForcedGameplay = true;

    [Header("Hotkeys")]
    public bool enableHotkeys = true;
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
                    break;
                }
            }
        }

        // cleanup finished
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var rec = active[i];
            if (rec.effect == null) { active.RemoveAt(i); continue; }
            if (Time.time >= rec.endTime)
            {
                // effect should self-end and call OnEffectFinished; this is just a safety net:
                rec.effect.Cancel();
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

        System.Type t = entry.effectPrefab.GetType();
        float addSeconds = Mathf.Max(0.01f, entry.durationSeconds);

        // Check if an effect of the same TYPE is already active. If so, extend it.
        for (int i = 0; i < active.Count; i++)
        {
            var rec = active[i];
            if (rec.effect != null && rec.effectType == t)
            {
                // Extend this running effect
                rec.effect.ExtendDuration(addSeconds);
                rec.endTime = Time.time + rec.effect.GetRemainingTime(); // recompute from effect
                rec.totalPlannedSeconds += addSeconds;
                active[i] = rec;

                // Reuse OnEffectStarted to update HUD's remaining time
                var started = OnEffectStarted;
                if (started != null) started(rec.name, rec.effect, rec.endTime, rec.totalPlannedSeconds);

                if (verboseLogs) Debug.Log("[PsychoactiveEffectsManager] Extended effect " + rec.name + " by +" + addSeconds + "s. New remaining ~ " + rec.effect.GetRemainingTime().ToString("0.00") + "s");
                return;
            }
        }

        // Not running yet: spawn new instance
        var inst = Instantiate(entry.effectPrefab, targetCam.transform, false);
        if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);

        inst.OnFinished += OnEffectFinished;

        string shownName = string.IsNullOrEmpty(entry.itemName) ? inst.itemName : entry.itemName;
        inst.Begin(targetCam, addSeconds, Mathf.Clamp01(entry.intensity));

        var newRec = new ActiveEffectRecord
        {
            effect = inst,
            name = shownName,
            endTime = Time.time + inst.GetRemainingTime(),
            totalPlannedSeconds = addSeconds,
            effectType = t
        };
        active.Add(newRec);

        var startedNew = OnEffectStarted;
        if (startedNew != null) startedNew(shownName, inst, newRec.endTime, newRec.totalPlannedSeconds);

        if (verboseLogs) Debug.Log("[PsychoactiveEffectsManager] Started effect: " + shownName + " for " + addSeconds + "s (type " + t.Name + ")");
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
                duration = r.totalPlannedSeconds,
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
