// FILE: CenteredFractalItemEffect.cs
// Drives the CenteredFractalDistortion volume Intensity 0 -> target -> 0
// when the item is consumed. Guarantees Intensity is reset to 0 when this
// object is disabled/destroyed or the app quits.

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using DistortionsPro_20X;   // CenteredFractalDistortion lives in this namespace

public class CenteredFractalItemEffect : MonoBehaviour, IItemEffect
{
    [Header("Volume Profile")]
    [Tooltip("Ideally assign the SAME VolumeProfile that your Global Volume uses. " +
             "If left empty, this script will try to find a global Volume at runtime.")]
    public VolumeProfile profile;

    [Header("Timing")]
    public float fadeInSeconds = 0.3f;
    public float fadeOutSeconds = 0.6f;

    [Header("Debug")]
    public bool verboseLogs = false;

    private Coroutine running;

    private void Awake()
    {
        // Auto-find a global VolumeProfile if none assigned
        if (profile == null)
        {
#if UNITY_2023_1_OR_NEWER
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
#else
            var volumes = Object.FindObjectsOfType<Volume>();
#endif
            Volume global = null;
            if (volumes != null)
            {
                for (int i = 0; i < volumes.Length; i++)
                {
                    if (volumes[i] != null && volumes[i].isGlobal)
                    {
                        global = volumes[i];
                        break;
                    }
                }
            }

            if (global != null)
            {
                profile = global.profile;
                if (verboseLogs)
                    Debug.Log("[CenteredFractalItemEffect] Auto-assigned VolumeProfile from global Volume '" +
                              global.name + "'.");
            }
            else if (verboseLogs)
            {
                Debug.LogWarning("[CenteredFractalItemEffect] No VolumeProfile assigned and no global Volume found.");
            }
        }
    }

    public void Play(float duration, float intensity)
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        running = StartCoroutine(RunEffect(duration, intensity));
    }

    private IEnumerator RunEffect(float duration, float intensity)
    {
        if (profile == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[CenteredFractalItemEffect] No VolumeProfile assigned.");
            yield break;
        }

        // Get the CenteredFractalDistortion override from THIS profile
        if (!profile.TryGet<CenteredFractalDistortion>(out var fx) || fx == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[CenteredFractalItemEffect] VolumeProfile has no CenteredFractalDistortion override.");
            yield break;
        }

        // Make sure Intensity is overridable
        fx.Intensity.overrideState = true;

        if (duration <= 0f)
            duration = 0.01f;

        float fin = Mathf.Max(0f, fadeInSeconds);
        float fout = Mathf.Max(0f, fadeOutSeconds);

        float sum = fin + fout;
        if (sum > duration && sum > 0f)
        {
            float scale = duration / sum;
            fin *= scale;
            fout *= scale;
        }

        float hold = Mathf.Max(0f, duration - fin - fout);

        // itemIntensity comes from ItemDeck.effectIntensity (0..1)
        float target = Mathf.Clamp01(intensity);   // Intensity is 0..1

        if (verboseLogs)
            Debug.Log("[CenteredFractalItemEffect] Start duration=" + duration + " targetIntensity=" + target);

        // Start at 0
        fx.Intensity.value = 0f;

        // FADE IN
        float t = 0f;
        while (t < fin)
        {
            float a = (fin > 0f) ? (t / fin) : 1f;
            float v = Mathf.Lerp(0f, target, a);
            fx.Intensity.value = v;

            if (verboseLogs)
                Debug.Log("[CenteredFractalItemEffect] Intensity (fade in) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        // HOLD
        t = 0f;
        while (t < hold)
        {
            fx.Intensity.value = target;

            if (verboseLogs)
                Debug.Log("[CenteredFractalItemEffect] Intensity (hold) = " + target);

            t += Time.deltaTime;
            yield return null;
        }

        // FADE OUT
        t = 0f;
        while (t < fout)
        {
            float a = (fout > 0f) ? (t / fout) : 0f;
            float v = Mathf.Lerp(target, 0f, a);
            fx.Intensity.value = v;

            if (verboseLogs)
                Debug.Log("[CenteredFractalItemEffect] Intensity (fade out) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        fx.Intensity.value = 0f;

        if (verboseLogs)
            Debug.Log("[CenteredFractalItemEffect] Finished, Intensity reset to 0.");

        running = null;
    }

    private void ResetEffect()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        if (profile != null && profile.TryGet<CenteredFractalDistortion>(out var fx) && fx != null)
        {
            fx.Intensity.overrideState = true;
            fx.Intensity.value = 0f;

            if (verboseLogs)
                Debug.Log("[CenteredFractalItemEffect] ResetEffect: Intensity forced to 0.");
        }
    }

    private void OnDisable() => ResetEffect();
    private void OnDestroy() => ResetEffect();
    private void OnApplicationQuit() => ResetEffect();
}
