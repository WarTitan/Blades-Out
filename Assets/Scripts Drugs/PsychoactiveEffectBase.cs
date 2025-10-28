// FILE: PsychoactiveEffectBase.cs
// Base class for all drug/dream effects. Each effect gets its own script by inheriting this.
// Manager will instantiate the prefab that has a concrete effect component and call Begin(...).

using UnityEngine;
using System;

public abstract class PsychoactiveEffectBase : MonoBehaviour
{
    public string itemName = "Unnamed";
    [TextArea(2, 4)]
    public string description = "No description";
    public GameObject previewPrefab;
    public float defaultDurationSeconds = 10f;
    [Range(0f, 1f)] public float defaultIntensity = 0.75f;

    protected Camera targetCam;
    protected Transform camTransform;
    protected float baseFov;

    public bool IsRunning { get; private set; }

    public event Action<PsychoactiveEffectBase> OnFinished;

    // Called by manager right after instantiate
    public void Begin(Camera cam, float duration, float intensity)
    {
        targetCam = cam;
        camTransform = cam != null ? cam.transform : null;
        baseFov = cam != null ? cam.fieldOfView : 60f;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        IsRunning = true;
        OnBegin(Mathf.Max(0.01f, duration), Mathf.Clamp01(intensity));
    }

    // Effect-specific start
    protected abstract void OnBegin(float duration, float intensity);

    // Manager or effect can cancel/finish
    public void Cancel()
    {
        if (!IsRunning) return;
        OnCancel();
        IsRunning = false;
        SafeFinishCallback();
        Destroy(gameObject);
    }

    // Call this when the effect naturally ends
    protected void End()
    {
        if (!IsRunning) return;
        OnEnd();
        IsRunning = false;
        SafeFinishCallback();
        Destroy(gameObject);
    }

    protected virtual void OnCancel() { OnEnd(); }
    protected virtual void OnEnd() { }

    private void SafeFinishCallback()
    {
        var cb = OnFinished;
        if (cb != null) cb(this);
    }
}
