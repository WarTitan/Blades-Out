// FILE: FracturedNoiseItemEffect.cs
// Drives FracturedNoiseDisplacement.Amount 0 -> target -> 0.

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using DistortionsPro_20X;

public class FracturedNoiseItemEffect : MonoBehaviour, IItemEffect
{
    [Header("Volume Profile")]
    [Tooltip("Assign the SAME VolumeProfile that your Global Volume uses. " +
             "If left empty, this script will try to find a global Volume at runtime.")]
    public VolumeProfile profile;

    [Header("Timing")]
    public float fadeInSeconds = 0.3f;
    public float fadeOutSeconds = 0.6f;

    [Header("Debug")]
    public bool verboseLogs = false;

    [Header("Item Intensity Cap")]
    [Tooltip("Maximum normalized intensity allowed from items (0..1). " +
             "The value from the ItemDeck will be clamped to this.")]
    [Range(0f, 1f)]
    public float maxIntensityFromItems = 1f;   // set to 0.7 if you want 0.7 max

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
                    Debug.Log("[FracturedNoiseItemEffect] Auto-assigned VolumeProfile from global Volume '" +
                              global.name + "'.");
            }
            else if (verboseLogs)
            {
                Debug.LogWarning("[FracturedNoiseItemEffect] No VolumeProfile assigned and no global Volume found.");
            }
        }
    }

    public void Play(float duration, float intensity)
    {
        // 🔒 Clamp incoming intensity from the item system
        intensity = Mathf.Clamp01(intensity);                // 0..1
        intensity = Mathf.Min(intensity, maxIntensityFromItems); // <= cap (e.g. 0.7)

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
                Debug.LogWarning("[FracturedNoiseItemEffect] No VolumeProfile assigned.");
            yield break;
        }

        if (!profile.TryGet<FracturedNoiseDisplacement>(out var fx) || fx == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[FracturedNoiseItemEffect] No FracturedNoiseDisplacement override in profile.");
            yield break;
        }

        fx.Amount.overrideState = true;

        if (duration <= 0f)
            duration = 0.01f;

        float fin = Mathf.Max(0f, fadeInSeconds);
        float fout = Mathf.Max(0f, fadeOutSeconds);

        float sum = fin + fout;
        if (sum > duration && sum > 0f)
        {
            float s = duration / sum;
            fin *= s;
            fout *= s;
        }

        float hold = Mathf.Max(0f, duration - fin - fout);

        // ✅ FINAL TARGET:
        // intensity here is already clamped to [0, maxIntensityFromItems],
        // so Amount will NEVER go above that (e.g. 0.7).
        float target = intensity;

        if (verboseLogs)
            Debug.Log("[FracturedNoiseItemEffect] Start duration=" + duration +
                      " clampedTarget=" + target);

        fx.Amount.value = 0f;

        // FADE IN
        float t = 0f;
        while (t < fin)
        {
            float a = (fin > 0f) ? (t / fin) : 1f;
            float v = Mathf.Lerp(0f, target, a);
            fx.Amount.value = v;

            if (verboseLogs)
                Debug.Log("[FracturedNoiseItemEffect] Amount (fade in) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        // HOLD
        t = 0f;
        while (t < hold)
        {
            fx.Amount.value = target;

            if (verboseLogs)
                Debug.Log("[FracturedNoiseItemEffect] Amount (hold) = " + target);

            t += Time.deltaTime;
            yield return null;
        }

        // FADE OUT
        t = 0f;
        while (t < fout)
        {
            float a = (fout > 0f) ? (t / fout) : 0f;
            float v = Mathf.Lerp(target, 0f, a);
            fx.Amount.value = v;

            if (verboseLogs)
                Debug.Log("[FracturedNoiseItemEffect] Amount (fade out) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        fx.Amount.value = 0f;

        if (verboseLogs)
            Debug.Log("[FracturedNoiseItemEffect] Finished, Amount reset to 0.");

        running = null;
    }

    private void ResetEffect()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        if (profile != null && profile.TryGet<FracturedNoiseDisplacement>(out var fx) && fx != null)
        {
            fx.Amount.overrideState = true;
            fx.Amount.value = 0f;
        }
    }

    private void OnDisable() => ResetEffect();
    private void OnDestroy() => ResetEffect();
    private void OnApplicationQuit() => ResetEffect();
}
