// FILE: WorleyDisplacementItemEffect.cs
// Drives WorleyDisplacement.Amount 0 -> target -> 0.

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using DistortionsPro_20X;

public class WorleyDisplacementItemEffect : MonoBehaviour, IItemEffect
{
    [Header("Volume Profile")]
    public VolumeProfile profile;

    [Header("Timing")]
    public float fadeInSeconds = 0.3f;
    public float fadeOutSeconds = 0.6f;

    [Header("Debug")]
    public bool verboseLogs = false;

    private Coroutine running;

    private void Awake()
    {
        if (profile == null)
        {
#if UNITY_2023_1_OR_NEWER
            var vols = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
#else
            var vols = Object.FindObjectsOfType<Volume>();
#endif
            Volume global = null;
            if (vols != null)
                for (int i = 0; i < vols.Length; i++)
                    if (vols[i] != null && vols[i].isGlobal) { global = vols[i]; break; }

            if (global != null)
            {
                profile = global.profile;
                if (verboseLogs)
                    Debug.Log("[WorleyDisplacementItemEffect] Auto profile from " + global.name);
            }
            else if (verboseLogs)
                Debug.LogWarning("[WorleyDisplacementItemEffect] No VolumeProfile.");
        }
    }

    public void Play(float duration, float intensity)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(RunEffect(duration, intensity));
    }

    private IEnumerator RunEffect(float duration, float intensity)
    {
        if (profile == null)
        {
            if (verboseLogs) Debug.LogWarning("[WorleyDisplacementItemEffect] No profile.");
            yield break;
        }

        if (!profile.TryGet<WorleyDisplacement>(out var fx) || fx == null)
        {
            if (verboseLogs) Debug.LogWarning("[WorleyDisplacementItemEffect] No WorleyDisplacement.");
            yield break;
        }

        fx.Amount.overrideState = true;

        if (duration <= 0f) duration = 0.01f;
        float fin = Mathf.Max(0f, fadeInSeconds);
        float fout = Mathf.Max(0f, fadeOutSeconds);
        float sum = fin + fout;
        if (sum > duration && sum > 0f)
        {
            float s = duration / sum;
            fin *= s; fout *= s;
        }
        float hold = Mathf.Max(0f, duration - fin - fout);

        float maxVal = fx.Amount.max;
        float target = Mathf.Clamp01(intensity) * maxVal;

        if (verboseLogs)
            Debug.Log("[WorleyDisplacementItemEffect] Start duration=" + duration + " target=" + target);

        fx.Amount.value = 0f;

        float t = 0f;
        while (t < fin)
        {
            float a = fin > 0f ? t / fin : 1f;
            fx.Amount.value = Mathf.Lerp(0f, target, a);
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < hold)
        {
            fx.Amount.value = target;
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < fout)
        {
            float a = fout > 0f ? t / fout : 0f;
            fx.Amount.value = Mathf.Lerp(target, 0f, a);
            t += Time.deltaTime;
            yield return null;
        }

        fx.Amount.value = 0f;
        if (verboseLogs) Debug.Log("[WorleyDisplacementItemEffect] Done, reset.");

        running = null;
    }

    private void ResetEffect()
    {
        if (running != null) StopCoroutine(running);
        running = null;

        if (profile != null && profile.TryGet<WorleyDisplacement>(out var fx) && fx != null)
        {
            fx.Amount.overrideState = true;
            fx.Amount.value = 0f;
        }
    }

    private void OnDisable() => ResetEffect();
    private void OnDestroy() => ResetEffect();
    private void OnApplicationQuit() => ResetEffect();
}
