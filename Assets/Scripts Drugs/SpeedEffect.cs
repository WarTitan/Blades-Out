// FILE: SpeedEffect.cs
// FULL REPLACEMENT (ASCII only)
// - Uses EffectFovMixer so FOV stacks with other effects.
// - Adds color controls (tint, saturation, contrast, sharpen) for the Speed blit.
// - Color tint can also pulse subtly with the FOV pulse.

using UnityEngine;
using System.Collections;

[AddComponentMenu("Gameplay/Effects/Speed Effect")]
[DefaultExecutionOrder(50500)]
public class SpeedEffect : PsychoactiveEffectBase
{
    [Header("Optional Blit (Built-in RP)")]
    public Material speedMaterialTemplate;  // shader "Hidden/SpeedEffect" (optional)

    [Header("FOV Core")]
    public float baseFovBoost = 14f;

    [Header("Startup Spike")]
    public float crazySpike = 28f;
    public float spikeDecaySeconds = 1.2f;

    [Header("Slow Pulse After Spike")]
    public float slowPulseAmplitude = 6f;
    public float slowPulseHz = 0.65f;
    public float pulseRiseSeconds = 0.8f;
    public float pulseStartDelay = 0.2f;

    [Header("Micro Jitter (very subtle)")]
    public bool enableMicroJitter = true;
    public float yawJitterDeg = 0.25f;
    public float pitchJitterDeg = 0.2f;
    public float jitterHz = 7.0f;

    [Header("Fade Envelope")]
    public float fadeInSeconds = 0.08f;
    public float fadeOutSeconds = 0.6f;

    [Header("Color Grade")]
    public Color tintColor = new Color(1f, 0.95f, 0.85f, 1f);
    [Range(0f, 1f)] public float tintStrength = 0.25f;
    [Range(0f, 1f)] public float tintPulseAmplitude = 0.15f;  // extra tint on pulse
    public float saturationBoost = 0.25f;
    public float contrastBoost = 0.25f;
    public float sharpenAmount = 0.70f;

    [Header("Debug")]
    public bool verboseLogs = false;

    // Runtime
    private Coroutine routine;
    private Material runtimeMat;
    private SpeedBlit blit;
    private float seed;

    private EffectFovMixer mixer;
    private int fovHandle = -1;

    protected override void OnBegin(float duration, float intensity)
    {
        if (targetCam == null || !targetCam.gameObject.activeInHierarchy)
        {
            if (verboseLogs) Debug.LogWarning("[SpeedEffect] Camera missing/inactive. Aborting.");
            End();
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!enabled) enabled = true;

        // Ensure mixer
        mixer = targetCam.GetComponent<EffectFovMixer>();
        if (mixer == null) mixer = targetCam.gameObject.AddComponent<EffectFovMixer>();
        mixer.SetBaseFov(baseFov);
        fovHandle = mixer.Register(this, 0);

        // Optional blit
        blit = targetCam.GetComponent<SpeedBlit>();
        if (blit == null) blit = targetCam.gameObject.AddComponent<SpeedBlit>();

        if (speedMaterialTemplate != null)
        {
            runtimeMat = new Material(speedMaterialTemplate);
        }
        else
        {
            var sh = Shader.Find("Hidden/SpeedEffect");
            if (sh != null) runtimeMat = new Material(sh);
        }

        if (runtimeMat != null)
        {
            runtimeMat.SetFloat("_Intensity", 0f);
            runtimeMat.SetFloat("_Sharp", Mathf.Max(0f, sharpenAmount));
            runtimeMat.SetFloat("_Sat", saturationBoost);
            runtimeMat.SetFloat("_Contrast", contrastBoost);
            runtimeMat.SetFloat("_Pulse", 0f);
            runtimeMat.SetColor("_TintColor", tintColor);
            runtimeMat.SetFloat("_TintStrength", 0f);
            blit.SetMaterial(runtimeMat);
        }
        else
        {
            blit.SetMaterial(null);
        }

        seed = Random.value * 1000f;
        routine = StartCoroutine(Run(duration, Mathf.Clamp01(intensity)));
    }

    protected override void OnEnd()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (mixer != null && fovHandle != -1)
        {
            mixer.Unregister(fovHandle);
            fovHandle = -1;
        }

        if (blit != null) blit.SetMaterial(null);

        if (runtimeMat != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(runtimeMat);
#else
            Object.Destroy(runtimeMat);
#endif
            runtimeMat = null;
        }
    }

    private IEnumerator Run(float duration, float strength)
    {
        float t0 = Time.time;
        float tEnd = t0 + Mathf.Max(0.1f, duration);

        float fin = Mathf.Max(0f, fadeInSeconds);
        float fout = Mathf.Max(0f, fadeOutSeconds);

        float spikeDecay = Mathf.Max(0.0001f, spikeDecaySeconds);
        float pulseRise = Mathf.Max(0.0001f, pulseRiseSeconds);

        while (Time.time < tEnd && targetCam != null)
        {
            float t = Time.time - t0;
            float timeLeft = Mathf.Max(0f, tEnd - Time.time);

            float in01 = (fin > 0f) ? Mathf.Clamp01(t / fin) : 1f;
            float out01 = (fout > 0f) ? Mathf.Clamp01(timeLeft / fout) : 1f;
            float env = Mathf.Clamp01(Mathf.Min(in01, out01) * strength);

            float spike = crazySpike * Mathf.Exp(-t / spikeDecay);

            float tp = Mathf.Max(0f, t - pulseStartDelay);
            float pulseGate = 1f - Mathf.Exp(-tp / pulseRise);
            float pulse = Mathf.Sin(tp * slowPulseHz * 2f * Mathf.PI);
            float pulseTerm = slowPulseAmplitude * pulseGate * pulse;

            float fovDelta = env * (baseFovBoost + spike + pulseTerm);

            if (mixer != null && fovHandle != -1)
                mixer.SetDelta(fovHandle, fovDelta);

            if (enableMicroJitter && camTransform != null)
            {
                float jx = (Mathf.PerlinNoise(seed + t * jitterHz, 0f) * 2f - 1f) * pitchJitterDeg * env;
                float jy = (Mathf.PerlinNoise(0f, seed + t * jitterHz) * 2f - 1f) * yawJitterDeg * env;

                Vector3 e = camTransform.localEulerAngles;
                float ex = e.x; if (ex > 180f) ex -= 360f;
                float ey = e.y; if (ey > 180f) ey -= 360f;
                camTransform.localRotation = Quaternion.Euler(ex + jx, ey + jy, 0f);
            }

            if (runtimeMat != null)
            {
                // Drive color grade
                float tintNow = Mathf.Clamp01(tintStrength + Mathf.Abs(pulse) * tintPulseAmplitude) * env;
                runtimeMat.SetFloat("_Intensity", env);
                runtimeMat.SetFloat("_Pulse", pulse);
                runtimeMat.SetColor("_TintColor", tintColor);
                runtimeMat.SetFloat("_TintStrength", tintNow);
                runtimeMat.SetFloat("_Sharp", Mathf.Max(0f, sharpenAmount));
                runtimeMat.SetFloat("_Sat", saturationBoost);
                runtimeMat.SetFloat("_Contrast", contrastBoost);
            }

            yield return null;
        }

        End();
    }
}

[AddComponentMenu("Rendering/Speed Blit (Built-in RP)")]
[RequireComponent(typeof(Camera))]
public class SpeedBlit : MonoBehaviour
{
    [SerializeField] private Material material;

    public void SetMaterial(Material m)
    {
        material = m;
        enabled = (material != null && material.shader != null);
    }

    private void OnEnable()
    {
        if (material == null || material.shader == null)
            enabled = false;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null || material.shader == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        Graphics.Blit(source, destination, material);
    }
}
