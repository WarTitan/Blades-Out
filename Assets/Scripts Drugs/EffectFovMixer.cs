// FILE: EffectFovMixer.cs
// NEW FILE
// Attach automatically by effects (or add to the player camera).
// Multiple effects call Register/SetDelta/Unregister; mixer sums deltas and applies once.

using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Effects/Effect FOV Mixer")]
[DefaultExecutionOrder(60000)]
[RequireComponent(typeof(Camera))]
public class EffectFovMixer : MonoBehaviour
{
    public bool clampFov = true;
    public float minFov = 40f;
    public float maxFov = 130f;

    private Camera cam;
    private float baseFov;
    private int nextHandle = 1;

    private struct Entry
    {
        public float delta;
        public int priority;
    }

    private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) enabled = false;
        baseFov = cam != null ? cam.fieldOfView : 60f;
    }

    public void SetBaseFov(float newBase)
    {
        baseFov = newBase;
    }

    public float GetBaseFov()
    {
        return baseFov;
    }

    public int Register(object owner, int priority = 0)
    {
        int h = nextHandle++;
        entries[h] = new Entry { delta = 0f, priority = priority };
        return h;
    }

    public void SetDelta(int handle, float delta)
    {
        Entry e;
        if (!entries.TryGetValue(handle, out e)) return;
        e.delta = delta;
        entries[handle] = e;
    }

    public void Clear(int handle)
    {
        Entry e;
        if (!entries.TryGetValue(handle, out e)) return;
        e.delta = 0f;
        entries[handle] = e;
    }

    public void Unregister(int handle)
    {
        if (entries.ContainsKey(handle)) entries.Remove(handle);
    }

    void LateUpdate()
    {
        if (cam == null) return;

        float sum = 0f;
        foreach (var kv in entries)
            sum += kv.Value.delta;

        float target = baseFov + sum;
        if (clampFov)
            target = Mathf.Clamp(target, minFov, maxFov);

        cam.fieldOfView = target;
    }
}
