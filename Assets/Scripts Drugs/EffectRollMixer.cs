// FILE: EffectRollMixer.cs
// NEW FILE (ASCII only)
// Aggregates per-effect roll (degrees) and applies it once per frame to the camera.
// Prevents effects from fighting over camera roll.

using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Effects/Effect Roll Mixer")]
[DefaultExecutionOrder(65000)]
[RequireComponent(typeof(Camera))]
public class EffectRollMixer : MonoBehaviour
{
    private struct Entry
    {
        public float deltaDeg;
        public int priority;
    }

    private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    private int nextHandle = 1;

    private Transform camXform;
    private float lastAppliedDeg;

    void Awake()
    {
        camXform = GetComponent<Transform>();
        lastAppliedDeg = 0f;
    }

    public int Register(object owner, int priority = 0)
    {
        int h = nextHandle++;
        entries[h] = new Entry { deltaDeg = 0f, priority = priority };
        return h;
    }

    public void SetDelta(int handle, float rollDegrees)
    {
        Entry e;
        if (!entries.TryGetValue(handle, out e)) return;
        e.deltaDeg = rollDegrees;
        entries[handle] = e;
    }

    public void Clear(int handle)
    {
        Entry e;
        if (!entries.TryGetValue(handle, out e)) return;
        e.deltaDeg = 0f;
        entries[handle] = e;
    }

    public void Unregister(int handle)
    {
        if (entries.ContainsKey(handle)) entries.Remove(handle);
    }

    void LateUpdate()
    {
        if (camXform == null) return;

        // Undo previous frame's roll
        if (Mathf.Abs(lastAppliedDeg) > 0.0001f)
        {
            camXform.localRotation = camXform.localRotation * Quaternion.AngleAxis(-lastAppliedDeg, Vector3.forward);
        }

        // Sum all current deltas
        float sum = 0f;
        foreach (var kv in entries)
            sum += kv.Value.deltaDeg;

        // Apply new sum
        if (Mathf.Abs(sum) > 0.0001f)
        {
            camXform.localRotation = camXform.localRotation * Quaternion.AngleAxis(sum, Vector3.forward);
        }

        lastAppliedDeg = sum;
    }

    void OnDisable()
    {
        // Remove any leftover roll when disabling
        if (camXform != null && Mathf.Abs(lastAppliedDeg) > 0.0001f)
        {
            camXform.localRotation = camXform.localRotation * Quaternion.AngleAxis(-lastAppliedDeg, Vector3.forward);
        }
        lastAppliedDeg = 0f;
        entries.Clear();
    }
}
