// FILE: DripMeltItemEffect.cs
// Drives the DripMeltDistortion Strength 0 -> target -> 0 when the item is consumed.
// Uses the same VolumeProfile asset as your Global Volume.
// Resets Strength back to 0 when disabled/destroyed/app quits.

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using DistortionsPro_20X;   // DripMeltDistortion lives in this namespace

public class DripMeltItemEffect : MonoBehaviour, IItemEffect
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
                    Debug.Log("[DripMeltItemEffect] Auto-assigned VolumeProfile from global Volume '" +
                              global.name + "'.");
            }
            else if (verboseLogs)
            {
                Debug.LogWarning("[DripMeltItemEffect] No VolumeProfile assigned and no global Volume found.");
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
                Debug.LogWarning("[DripMeltItemEffect] No VolumeProfile assigned.");
            yield break;
        }

        // Get the DripMeltDistortion override from THIS profile
        if (!profile.TryGet<DripMeltDistortion>(out var fx) || fx == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[DripMeltItemEffect] VolumeProfile has no DripMeltDistortion override.");
            yield break;
        }

        // Make sure Strength is overridable
        fx.Strength.overrideState = true;

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

        // Strength range is [min .. max] (0..2 by default)
        float maxStrength = fx.Strength.max;
        float target = Mathf.Clamp01(intensity) * maxStrength;

        if (verboseLogs)
            Debug.Log("[DripMeltItemEffect] Start duration=" + duration +
                      " targetStrength=" + target + " (max=" + maxStrength + ")");

        // Start at 0
        fx.Strength.value = 0f;

        // FADE IN
        float t = 0f;
        while (t < fin)
        {
            float a = (fin > 0f) ? (t / fin) : 1f;
            float v = Mathf.Lerp(0f, target, a);
            fx.Strength.value = v;

            if (verboseLogs)
                Debug.Log("[DripMeltItemEffect] Strength (fade in) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        // HOLD
        t = 0f;
        while (t < hold)
        {
            fx.Strength.value = target;

            if (verboseLogs)
                Debug.Log("[DripMeltItemEffect] Strength (hold) = " + target);

            t += Time.deltaTime;
            yield return null;
        }

        // FADE OUT
        t = 0f;
        while (t < fout)
        {
            float a = (fout > 0f) ? (t / fout) : 0f;
            float v = Mathf.Lerp(target, 0f, a);
            fx.Strength.value = v;

            if (verboseLogs)
                Debug.Log("[DripMeltItemEffect] Strength (fade out) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        fx.Strength.value = 0f;

        if (verboseLogs)
            Debug.Log("[DripMeltItemEffect] Finished, Strength reset to 0.");

        running = null;
    }

    private void ResetEffect()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        if (profile != null && profile.TryGet<DripMeltDistortion>(out var fx) && fx != null)
        {
            fx.Strength.overrideState = true;
            fx.Strength.value = 0f;

            if (verboseLogs)
                Debug.Log("[DripMeltItemEffect] ResetEffect: Strength forced to 0.");
        }
    }

    private void OnDisable() => ResetEffect();
    private void OnDestroy() => ResetEffect();
    private void OnApplicationQuit() => ResetEffect();
}
