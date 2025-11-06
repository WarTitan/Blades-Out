using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Gameplay/Effects/Effects Manager")]
public class PsychoactiveEffectsManager : MonoBehaviour
{
    [Serializable]
    public struct ActiveEffectInfo
    {
        public string id;        // internal ID
        public string name;      // display name
        public float endTime;    // Time.time when it ends
        public float duration;   // total duration (seconds)
    }

    public event Action<ActiveEffectInfo> OnEffectStarted;
    public event Action<string> OnEffectEnded; // id

    private readonly Dictionary<string, ActiveEffectInfo> active =
        new Dictionary<string, ActiveEffectInfo>(16);

    public string RegisterEffect(string displayName, float duration)
    {
        if (string.IsNullOrEmpty(displayName))
            displayName = "Effect";

        duration = Mathf.Max(0f, duration);
        float end = Time.time + duration;

        var info = new ActiveEffectInfo
        {
            id = Guid.NewGuid().ToString(),
            name = displayName,
            endTime = end,
            duration = duration
        };

        active[info.id] = info;

        Debug.Log("[EffectsManager] RegisterEffect " + info.name + " duration=" + duration);
        OnEffectStarted?.Invoke(info);
        return info.id;
    }

    public void EndEffect(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!active.Remove(id)) return;

        OnEffectEnded?.Invoke(id);
    }

    public List<ActiveEffectInfo> GetActiveEffectsSnapshot()
    {
        return new List<ActiveEffectInfo>(active.Values);
    }

    private void Update()
    {
        if (active.Count == 0) return;

        float now = Time.time;
        List<string> toEnd = null;

        foreach (var kv in active)
        {
            if (kv.Value.endTime <= now)
            {
                if (toEnd == null) toEnd = new List<string>(4);
                toEnd.Add(kv.Key);
            }
        }

        if (toEnd != null)
        {
            for (int i = 0; i < toEnd.Count; i++)
                EndEffect(toEnd[i]);
        }
    }

    private void OnDestroy()
    {
        if (active.Count == 0) return;
        var ids = new List<string>(active.Keys);
        for (int i = 0; i < ids.Count; i++)
            OnEffectEnded?.Invoke(ids[i]);
        active.Clear();
    }
}
