// FILE: VodkaEffect.cs
// Concrete effect: "Vodka" wobble + FOV pulse (+ optional URP post-effects).
// Attach this script to a prefab, then assign that prefab into the manager's item list.

using UnityEngine;
using System.Collections;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

[AddComponentMenu("Gameplay/Effects/Vodka Effect")]
[DefaultExecutionOrder(50500)]
public class VodkaEffect : PsychoactiveEffectBase
{
    [Header("Vodka Tuning")]
    public float maxRollDegrees = 8f;
    public float maxYawJitterDegrees = 4f;
    public float maxPitchJitterDegrees = 2f;
    public float fovPulse = 8f;
    public float wobbleFreq = 0.6f;   // Hz
    public float jitterFreq = 1.3f;   // Hz
    public AnimationCurve envelope = AnimationCurve.EaseInOut(0f, 0f, 0.2f, 1f);

#if UNITY_RENDER_PIPELINE_UNIVERSAL
    private Volume runtimeVol;
    private VolumeProfile runtimeProfile;
    private ChromaticAberration ca;
    private LensDistortion ld;
    private Vignette vg;
    private MotionBlur mb;
    private Bloom bm;
#endif

    private Coroutine routine;
    private float wobbleRoll, wobbleYaw, wobblePitch;
    private bool pendingWobble;

    protected override void OnBegin(float duration, float intensity)
    {
        if (targetCam == null)
        {
            Debug.LogWarning("[VodkaEffect] No camera. Aborting.");
            End();
            return;
        }

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        SetupRuntimeVolume();
#endif
        routine = StartCoroutine(Run(duration, intensity));
    }

    protected override void OnEnd()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (targetCam != null)
            targetCam.fieldOfView = baseFov;

        pendingWobble = false;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (runtimeVol != null) runtimeVol.weight = 0f;
        CleanupVolume();
#endif
    }

    private IEnumerator Run(float duration, float strength)
    {
        float t0 = Time.time;
        float tEnd = t0 + Mathf.Max(0.5f, duration);
        float seed = Random.value * 1000f;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        float ca0 = ca != null ? ca.intensity.value : 0f;
        float ld0 = ld != null ? ld.intensity.value : 0f;
        float vg0 = vg != null ? vg.intensity.value : 0f;
        float mb0 = mb != null ? mb.intensity.value : 0f;
        float bm0 = bm != null ? bm.intensity.value : 0f;
#endif

        while (Time.time < tEnd && targetCam != null)
        {
            float t = Time.time - t0;

            float ramp = Mathf.Clamp01(envelope.Evaluate(Mathf.Clamp01(t / (duration * 0.25f)))) * strength;
            float decay = Mathf.Clamp01(1f - ((Time.time - (t0 + duration * 0.5f)) / (duration * 0.5f)));
            float env = Mathf.Min(ramp, Mathf.Max(0.2f, decay));

            pendingWobble = true;
            wobbleRoll = Mathf.Sin(t * wobbleFreq * 2f * Mathf.PI) * maxRollDegrees * env;

            float nX = Mathf.PerlinNoise(seed + t * jitterFreq, 0f) * 2f - 1f;
            float nY = Mathf.PerlinNoise(0f, seed + t * jitterFreq) * 2f - 1f;
            wobbleYaw = nX * maxYawJitterDegrees * env * 0.6f;
            wobblePitch = nY * maxPitchJitterDegrees * env * 0.6f;

            targetCam.fieldOfView = baseFov + Mathf.Sin(t * 1.2f) * fovPulse * env;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
            float pp = env;
            if (runtimeVol != null) runtimeVol.weight = Mathf.Clamp01(pp);
            if (ca != null) ca.intensity.value = Mathf.Lerp(ca0, 0.9f, pp);
            if (ld != null) ld.intensity.value = Mathf.Lerp(ld0, -0.3f, pp);
            if (vg != null) vg.intensity.value = Mathf.Lerp(vg0, 0.35f, pp);
            if (mb != null) mb.intensity.value = Mathf.Lerp(mb0, 0.6f, pp);
            if (bm != null) bm.intensity.value = Mathf.Lerp(bm0, 0.6f, pp);
#endif
            yield return null;
        }

        End();
    }

    private void LateUpdate()
    {
        if (!IsRunning || targetCam == null || camTransform == null) return;
        if (!pendingWobble) return;

        Vector3 e = camTransform.localEulerAngles;
        float bx = e.x; if (bx > 180f) bx -= 360f;
        float by = e.y; if (by > 180f) by -= 360f;
        float bz = e.z; if (bz > 180f) bz -= 360f;

        float x = bx + wobblePitch;
        float y = by + wobbleYaw;
        float z = bz + wobbleRoll;

        camTransform.localRotation = Quaternion.Euler(x, y, z);
    }

#if UNITY_RENDER_PIPELINE_UNIVERSAL
    private void SetupRuntimeVolume()
    {
        if (targetCam == null || runtimeVol != null) return;

        var go = new GameObject("VodkaPostFX");
        go.transform.SetParent(targetCam.transform, false);
        runtimeVol = go.AddComponent<Volume>();
        runtimeVol.isGlobal = true;
        runtimeVol.priority = 100f;
        runtimeVol.weight = 0f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();

        ca = runtimeProfile.Add<ChromaticAberration>(true);
        if (ca != null) { ca.active = true; ca.intensity.value = 0f; }

        ld = runtimeProfile.Add<LensDistortion>(true);
        if (ld != null)
        {
            ld.active = true;
            ld.center.value = new Vector2(0.5f, 0.5f);
            ld.intensity.value = 0f;
            ld.scale.value = 1f;
        }

        vg = runtimeProfile.Add<Vignette>(true);
        if (vg != null)
        {
            vg.active = true;
            vg.intensity.value = 0f;
            vg.smoothness.value = 0.4f;
        }

        mb = runtimeProfile.Add<MotionBlur>(true);
        if (mb != null)
        {
            mb.active = true;
            mb.intensity.value = 0f;
            mb.clamp.value = 0.3f;
        }

        bm = runtimeProfile.Add<Bloom>(true);
        if (bm != null)
        {
            bm.active = true;
            bm.intensity.value = 0f;
            bm.threshold.value = 1f;
        }

        runtimeVol.profile = runtimeProfile;
    }

    private void CleanupVolume()
    {
        if (runtimeVol != null)
        {
            if (runtimeVol.gameObject != null) Destroy(runtimeVol.gameObject);
        }
        runtimeVol = null;
        runtimeProfile = null;
        ca = null; ld = null; vg = null; mb = null; bm = null;
    }
#else
    private void CleanupVolume() { }
#endif
}
