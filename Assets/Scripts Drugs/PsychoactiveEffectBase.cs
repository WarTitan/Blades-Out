// FILE: PsychoactiveEffectBase.cs
// FULL REPLACEMENT (ASCII)
// Base class that all effect scripts inherit from.
// Now exposes startTime and camTransform for legacy effects.

using System.Collections;
using UnityEngine;

[AddComponentMenu("Gameplay/Effects/Effect Base")]
public class PsychoactiveEffectBase : MonoBehaviour
{
    protected Camera targetCam;
    protected Transform camTransform;     // <— added for effects that use camera transform
    protected float startTime;            // <— added for effects that track elapsed via startTime
    protected float endTime;
    protected float durationSeconds;
    protected float intensity01;
    protected string displayName;

    protected float baseFov = 60f;

    private Coroutine lifeRoutine;

    // Public entry points used by item application
    public void Begin(Camera cam, float duration, float intensity)
    {
        BeginNamed(cam, duration, intensity, GetType().Name);
    }

    public void BeginNamed(Camera cam, float duration, float intensity, string name)
    {
        if (cam == null) { Destroy(gameObject); return; }

        targetCam = cam;
        camTransform = cam.transform;            // set for child effects
        durationSeconds = Mathf.Max(0.01f, duration);
        intensity01 = Mathf.Clamp01(intensity);
        displayName = string.IsNullOrEmpty(name) ? GetType().Name : name;

        if (targetCam != null && !targetCam.orthographic)
            baseFov = targetCam.fieldOfView;

        startTime = Time.time;                   // set for child effects
        endTime = startTime + durationSeconds;

        var mgr = GetComponentInParent<PsychoactiveEffectsManager>();
        if (mgr != null) mgr.NotifyStart(displayName, this, endTime, durationSeconds);

        if (lifeRoutine != null) StopCoroutine(lifeRoutine);
        lifeRoutine = StartCoroutine(Life());

        OnBegin(durationSeconds, intensity01);
    }

    public void End()
    {
        if (lifeRoutine != null) { StopCoroutine(lifeRoutine); lifeRoutine = null; }

        OnEnd(); // let derived clean up

        var mgr = GetComponentInParent<PsychoactiveEffectsManager>();
        if (mgr != null) mgr.NotifyEnd(this);

        Destroy(gameObject);
    }

    private IEnumerator Life()
    {
        while (Time.time < endTime)
            yield return null;

        End();
    }

    // Override in derived classes
    protected virtual void OnBegin(float duration, float intensity) { }
    protected virtual void OnEnd() { }
}
