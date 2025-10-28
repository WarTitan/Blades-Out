// FILE: PsychoactiveEffectBase.cs
// FULL REPLACEMENT (ASCII only)
// Base class for all psychoactive effects.
// Provides Begin(...), ExtendDuration(...), Cancel(), and End().
// Derived effects must read 'endTime' to allow runtime extension.

using UnityEngine;
using System;

public abstract class PsychoactiveEffectBase : MonoBehaviour
{
    [Header("Common")]
    public string itemName = "";

    protected Camera targetCam;
    protected Transform camTransform;

    // Store the camera FOV at begin for reference (mixer may override application)
    protected float baseFov;

    // Timing state (shared so manager can extend duration)
    protected float startTime;
    protected float endTime;          // absolute game time when effect ends
    protected float durationSeconds;  // initial requested duration
    protected float intensity01;      // 0..1

    public event Action<PsychoactiveEffectBase> OnFinished;

    public void Begin(Camera cam, float duration, float intensity)
    {
        if (cam == null)
        {
            Debug.LogWarning("[PsychoactiveEffectBase] Begin called with null camera.");
        }

        targetCam = cam;
        camTransform = (cam != null ? cam.transform : null);
        baseFov = (cam != null ? cam.fieldOfView : 60f);

        durationSeconds = Mathf.Max(0.01f, duration);
        intensity01 = Mathf.Clamp01(intensity);

        startTime = Time.time;
        endTime = startTime + durationSeconds;

        OnBegin(durationSeconds, intensity01);
    }

    // Adds seconds to the current endTime. Derived effects should read 'endTime' in their loops.
    public void ExtendDuration(float addSeconds)
    {
        float add = Mathf.Max(0f, addSeconds);
        endTime += add;
        OnExtended(add);
    }

    // Remaining time in seconds (never negative)
    public float GetRemainingTime()
    {
        return Mathf.Max(0f, endTime - Time.time);
    }

    public void Cancel()
    {
        OnEnd();
        var h = OnFinished;
        if (h != null) h(this);
        Destroy(gameObject);
    }

    // Call from derived effect when finished naturally
    protected void End()
    {
        OnEnd();
        var h = OnFinished;
        if (h != null) h(this);
        Destroy(gameObject);
    }

    // ---- Hooks for derived classes ----
    protected abstract void OnBegin(float duration, float intensity);
    protected virtual void OnEnd() { }
    protected virtual void OnExtended(float addSeconds) { }
}
