// FILE: PrismaticDisplacementItemEffect.cs
// Drives PrismaticDisplacement.Fade 0 -> target -> 0.

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using DistortionsPro_20X;

public class PrismaticDisplacementItemEffect : MonoBehaviour, IItemEffect
{
    [Header("Volume Profile")]
    public VolumeProfile profile;

    [Header("Timing")]
    public float fadeInSeconds = 0.3f;
    public float fadeOutSeconds = 0.6f;

    [Header("Debug")]
    public bool verboseLogs = false;

    Coroutine running;

    void Awake()
    {
        if (!profile)
        {
#if UNITY_2023_1_OR_NEWER
            var vols = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
#else
            var vols = Object.FindObjectsOfType<Volume>();
#endif
            Volume global = null;
            if (vols != null)
                for (int i = 0; i < vols.Length; i++)
                    if (vols[i] && vols[i].isGlobal) { global = vols[i]; break; }

            if (global)
            {
                profile = global.profile;
                if (verboseLogs)
                    Debug.Log("[PrismaticDisplacementItemEffect] Auto profile from " + global.name);
            }
            else if (verboseLogs)
                Debug.LogWarning("[PrismaticDisplacementItemEffect] No VolumeProfile.");
        }
    }

    public void Play(float duration, float intensity)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(RunEffect(duration, intensity));
    }

    IEnumerator RunEffect(float duration, float intensity)
    {
        if (!profile)
        {
            if (verboseLogs) Debug.LogWarning("[PrismaticDisplacementItemEffect] No profile.");
            yield break;
        }

        if (!profile.TryGet<PrismaticDisplacement>(out var fx) || fx == null)
        {
            if (verboseLogs) Debug.LogWarning("[PrismaticDisplacementItemEffect] No PrismaticDisplacement.");
            yield break;
        }

        fx.Fade.overrideState = true;

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

        float maxVal = fx.Fade.max;
        float target = Mathf.Clamp01(intensity) * maxVal;

        if (verboseLogs)
            Debug.Log("[PrismaticDisplacementItemEffect] Start duration=" + duration + " target=" + target);

        fx.Fade.value = 0f;

        float t = 0f;
        while (t < fin)
        {
            float a = fin > 0f ? t / fin : 1f;
            fx.Fade.value = Mathf.Lerp(0f, target, a);
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < hold)
        {
            fx.Fade.value = target;
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < fout)
        {
            float a = fout > 0f ? t / fout : 0f;
            fx.Fade.value = Mathf.Lerp(target, 0f, a);
            t += Time.deltaTime;
            yield return null;
        }

        fx.Fade.value = 0f;
        if (verboseLogs) Debug.Log("[PrismaticDisplacementItemEffect] Done, reset.");

        running = null;
    }

    void ResetEffect()
    {
        if (running != null) StopCoroutine(running);
        running = null;

        if (profile && profile.TryGet<PrismaticDisplacement>(out var fx) && fx != null)
        {
            fx.Fade.overrideState = true;
            fx.Fade.value = 0f;
        }
    }

    void OnDisable() => ResetEffect();
    void OnDestroy() => ResetEffect();
    void OnApplicationQuit() => ResetEffect();
}
