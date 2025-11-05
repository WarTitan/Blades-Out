// FILE: FlowMoshItemEffect.cs
// Drives the FlowMosh volume Blend 0 -> target -> 0 when the item is consumed.
// Uses the same VolumeProfile asset as your Global Volume, so it matches
// what you see in the Inspector.

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using DistortionsPro_20X;   // FlowMosh is defined in this namespace

public class FlowMoshItemEffect : MonoBehaviour, IItemEffect
{
    [Header("Volume Profile")]
    [Tooltip("Assign the SAME VolumeProfile that your Global Volume uses.")]
    public VolumeProfile profile;

    [Header("Timing")]
    public float fadeInSeconds = 0.3f;
    public float fadeOutSeconds = 0.6f;

    [Header("Debug")]
    public bool verboseLogs = false;

    public void Play(float duration, float intensity)
    {
        StartCoroutine(RunEffect(duration, intensity));
    }

    private IEnumerator RunEffect(float duration, float intensity)
    {
        if (profile == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[FlowMoshItemEffect] No VolumeProfile assigned.");
            yield break;
        }

        // Get the FlowMosh override from THIS profile (same thing you see in Inspector)
        if (!profile.TryGet<FlowMosh>(out var flow) || flow == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[FlowMoshItemEffect] VolumeProfile has no FlowMosh override.");
            yield break;
        }

        // Make sure Blend is overridable
        flow.Blend.overrideState = true;

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
        float target = Mathf.Clamp01(intensity);   // Blend is 0..1

        if (verboseLogs)
            Debug.Log("[FlowMoshItemEffect] Start duration=" + duration + " targetBlend=" + target);

        // Start at 0
        flow.Blend.value = 0f;

        // FADE IN
        float t = 0f;
        while (t < fin)
        {
            float a = (fin > 0f) ? (t / fin) : 1f;
            float v = Mathf.Lerp(0f, target, a);
            flow.Blend.value = v;

            if (verboseLogs)
                Debug.Log("[FlowMoshItemEffect] Blend (fade in) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        // HOLD
        t = 0f;
        while (t < hold)
        {
            flow.Blend.value = target;

            if (verboseLogs)
                Debug.Log("[FlowMoshItemEffect] Blend (hold) = " + target);

            t += Time.deltaTime;
            yield return null;
        }

        // FADE OUT
        t = 0f;
        while (t < fout)
        {
            float a = (fout > 0f) ? (t / fout) : 0f;
            float v = Mathf.Lerp(target, 0f, a);
            flow.Blend.value = v;

            if (verboseLogs)
                Debug.Log("[FlowMoshItemEffect] Blend (fade out) = " + v);

            t += Time.deltaTime;
            yield return null;
        }

        flow.Blend.value = 0f;

        if (verboseLogs)
            Debug.Log("[FlowMoshItemEffect] Finished, Blend reset to 0.");
    }
}
