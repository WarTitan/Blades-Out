// FILE: PsychoactiveEffectsManager.cs
// FULL REPLACEMENT (ASCII only)
// No hotkeys. Tracks active effects, raises start/end events,
// and provides a snapshot for the HUD.

using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Gameplay/Effects/Psychoactive Effects Manager")]
public class PsychoactiveEffectsManager : MonoBehaviour
{
    public struct ActiveEffectInfo
    {
        public string name;
        public PsychoactiveEffectBase effect;
        public float endTime;
        public float duration;
    }

    public event Action<string, PsychoactiveEffectBase, float, float> OnEffectStarted;
    public event Action<PsychoactiveEffectBase> OnEffectEnded;

    private readonly Dictionary<PsychoactiveEffectBase, ActiveEffectInfo> active =
        new Dictionary<PsychoactiveEffectBase, ActiveEffectInfo>(16);

    // Called by effects
    public void NotifyStart(string displayName, PsychoactiveEffectBase eff, float endTime, float duration)
    {
        if (eff == null) return;
        ActiveEffectInfo info = new ActiveEffectInfo
        {
            name = string.IsNullOrEmpty(displayName) ? eff.GetType().Name : displayName,
            effect = eff,
            endTime = endTime,
            duration = duration
        };
        active[eff] = info;
        if (OnEffectStarted != null) OnEffectStarted(info.name, eff, info.endTime, info.duration);
    }

    // Called by effects
    public void NotifyEnd(PsychoactiveEffectBase eff)
    {
        if (eff == null) return;
        if (active.ContainsKey(eff)) active.Remove(eff);
        if (OnEffectEnded != null) OnEffectEnded(eff);
    }

    public List<ActiveEffectInfo> GetActiveEffectsSnapshot()
    {
        var list = new List<ActiveEffectInfo>(active.Count);
        foreach (var kv in active) list.Add(kv.Value);
        return list;
    }

    private void OnDestroy()
    {
        if (active.Count == 0) return;
        var keys = new List<PsychoactiveEffectBase>(active.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            if (OnEffectEnded != null) OnEffectEnded(keys[i]);
        }
        active.Clear();
    }
}
