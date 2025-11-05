// FILE: DistortionProVolumeItemEffect.cs
// Drives a single URP Volume override (20X Distortion Pro) via IItemEffect.
// You assign:
//   - the VolumeProfile asset that contains your effect
//   - optionally the override component itself
//   - or just the component type name + intensity field name
//
// ItemEffectRunner spawns this prefab and calls Play(duration, intensity),
// and we animate that effect's intensity 0 -> target -> 0.

using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

[AddComponentMenu("Gameplay/Items/Distortion Pro Volume Item Effect")]
public class DistortionProVolumeItemEffect : MonoBehaviour, IItemEffect
{
    [Header("Target Profile / Override")]
    [Tooltip("VolumeProfile asset that contains your Distortion Pro override.")]
    public VolumeProfile profile;

    [Tooltip("Optional: drag the specific override from the profile here.\n" +
             "If left null, we will search by componentTypeName.")]
    public VolumeComponent overrideComponent;

    [Tooltip("If overrideComponent is null, we search the profile for a component\n" +
             "whose type name matches this (e.g. 'FlowMoshDistortion').")]
    public string componentTypeName = "";

    [Tooltip("Name of the float/parameter that controls strength.\n" +
             "Usually 'intensity' for most Distortion Pro effects.")]
    public string intensityFieldName = "intensity";

    [Header("Timing")]
    public float fadeInSeconds = 0.3f;
    public float fadeOutSeconds = 0.6f;

    [Tooltip("Multiplier applied to the incoming intensity (0..1+).")]
    public float maxIntensity = 1f;

    [Header("Debug")]
    public bool verboseLogs = false;

    // Cached reflection
    private VolumeComponent _component;
    private FieldInfo _intensityField;
    private bool _cacheAttempted;
    private Coroutine _running;

    public void Play(float duration, float intensity)
    {
        if (_running != null)
        {
            StopCoroutine(_running);
        }
        _running = StartCoroutine(RunEffect(duration, intensity));
    }

    private IEnumerator RunEffect(float duration, float intensity)
    {
        if (!EnsureCached())
        {
            if (verboseLogs)
            {
                Debug.LogWarning("[DistortionProVolumeItemEffect] Could not find component/field. " +
                                 "Check profile, overrideComponent, componentTypeName, intensityFieldName.");
            }
            yield break;
        }

        if (duration <= 0f)
        {
            duration = 0.01f;
        }

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
        float target = maxIntensity * Mathf.Max(0f, intensity);

        if (verboseLogs)
        {
            Debug.Log("[DistortionProVolumeItemEffect] Start " + GetComponentName() +
                      " duration=" + duration + " target=" + target);
        }

        // FADE IN
        float t = 0f;
        while (t < fin)
        {
            float a = (fin > 0f) ? (t / fin) : 1f;
            SetIntensity(target * a);
            t += Time.deltaTime;
            yield return null;
        }

        // HOLD
        t = 0f;
        while (t < hold)
        {
            SetIntensity(target);
            t += Time.deltaTime;
            yield return null;
        }

        // FADE OUT
        t = 0f;
        while (t < fout)
        {
            float a = (fout > 0f) ? (1f - (t / fout)) : 0f;
            SetIntensity(target * a);
            t += Time.deltaTime;
            yield return null;
        }

        // End – turn off effect
        SetIntensity(0f);

        if (verboseLogs)
        {
            Debug.Log("[DistortionProVolumeItemEffect] Finished " + GetComponentName());
        }

        _running = null;
    }

    private bool EnsureCached()
    {
        if (_cacheAttempted)
            return _component != null && _intensityField != null;

        _cacheAttempted = true;

        if (overrideComponent != null)
        {
            _component = overrideComponent;
        }
        else if (profile != null)
        {
            foreach (var comp in profile.components)
            {
                if (comp == null) continue;
                System.Type t = comp.GetType();
                if (!string.IsNullOrEmpty(componentTypeName) &&
                    (t.Name == componentTypeName || t.FullName.EndsWith(componentTypeName)))
                {
                    _component = comp;
                    break;
                }
            }
        }

        if (_component == null)
            return false;

        var type = _component.GetType();
        _intensityField = type.GetField(intensityFieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return _intensityField != null;
    }

    private void SetIntensity(float value)
    {
        if (_component == null || _intensityField == null)
            return;

        object param = _intensityField.GetValue(_component);

        // Most URP volume params are VolumeParameter<float> / ClampedFloatParameter
        if (param is VolumeParameter<float> floatParam)
        {
            floatParam.value = value;
        }
        else if (param is ClampedFloatParameter clamped)
        {
            clamped.value = value;
        }
        else if (param is VolumeParameter baseParam)
        {
            var valueProp = baseParam.GetType().GetProperty("value");
            if (valueProp != null && valueProp.PropertyType == typeof(float))
            {
                valueProp.SetValue(baseParam, value, null);
            }
        }
        else if (param is float)
        {
            _intensityField.SetValue(_component, value);
        }
    }

    private string GetComponentName()
    {
        return _component != null ? _component.GetType().Name : "(null)";
    }
}
