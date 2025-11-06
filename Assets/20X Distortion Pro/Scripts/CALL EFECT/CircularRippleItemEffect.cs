// FILE: CircularRippleItemEffect.cs
// Drives CircularRippleWarp.Fade 0 -> target -> 0 when the item is consumed.
// Uses the same VolumeProfile as your Global Volume and resets Fade to 0 on cleanup.

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using DistortionsPro_20X;   // CircularRippleWarp lives in this namespace

public class CircularRippleItemEffect : MonoBehaviour, IItemEffect
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
                    Debug.Log("[CircularRippleItemEffect] Auto-assigned VolumeProfile from global Volume '" +
                              global.name + "'.");
            }
            else if (verboseLogs)
            {
                Debug.LogWarning("[CircularRippleItemEffect] No VolumeProfile assigned and no global Volume found.");
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
                Debug.LogWarning("[CircularRippleItemEffect] No VolumeProfile assigned.");
            yield break;
        }

        // Get the CircularRippleWarp override from THIS profile
        if (!profile.TryGet<CircularRippleWarp>(out var fx) || fx == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[CircularRippleItemEffect] VolumeProfile has no CircularRippleWarp override.");
            yield break;
        }

        // Make sure Fade is overridable
        fx.Fade.overrideState = true;

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

        // Fade is 0..1
        float target = Mathf.Clamp01(intensity);

        if (verboseLogs)
            Debug.Log("[CircularRippleItemEffect] Start duration=" + duration +
                      " targetFade=" + target);

        // Start at 0
        fx.Fade.value = 0f;

        // FADE IN
        float t = 0f;
        while (t < fin)
        {
            float a = (fin > 0f) ? (t / fin) : 1f;
            float v = Mathf.Lerp(0f, target, a);
            fx.Fade.value = v;

            if (verboseLogs)
                Debug.Log("[CircularRippleItemEffect] Fade (fade in) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        // HOLD
        t = 0f;
        while (t < hold)
        {
            fx.Fade.value = target;

            if (verboseLogs)
                Debug.Log("[CircularRippleItemEffect] Fade (hold) = " + target);

            t += Time.deltaTime;
            yield return null;
        }

        // FADE OUT
        t = 0f;
        while (t < fout)
        {
            float a = (fout > 0f) ? (t / fout) : 0f;
            float v = Mathf.Lerp(target, 0f, a);
            fx.Fade.value = v;

            if (verboseLogs)
                Debug.Log("[CircularRippleItemEffect] Fade (fade out) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        fx.Fade.value = 0f;

        if (verboseLogs)
            Debug.Log("[CircularRippleItemEffect] Finished, Fade reset to 0.");

        running = null;
    }

    private void ResetEffect()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        if (profile != null && profile.TryGet<CircularRippleWarp>(out var fx) && fx != null)
        {
            fx.Fade.overrideState = true;
            fx.Fade.value = 0f;

            if (verboseLogs)
                Debug.Log("[CircularRippleItemEffect] ResetEffect: Fade forced to 0.");
        }
    }

    private void OnDisable() => ResetEffect();
    private void OnDestroy() => ResetEffect();
    private void OnApplicationQuit() => ResetEffect();
}
