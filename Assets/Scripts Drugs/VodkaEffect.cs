// FILE: VodkaEffect.cs
// Vodka drunk effect for URP. Uses DrunkBlitFeature to do a fullscreen blit.
// Optional FOV wobble via EffectFovMixer. Requires URP and DrunkBlitFeature
// added as a Renderer Feature to your active URP renderer.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

[AddComponentMenu("Gameplay/Effects/Vodka Effect (URP)")]
[DefaultExecutionOrder(50510)]
public class VodkaEffect : PsychoactiveEffectBase
{
    [Header("Drunk Material Template (URP)")]
    public Material drunkMaterialTemplate;  // Assign material that uses your DrunkEffect shader

    [Header("Optional FOV Wobble (via mixer)")]
    public float fovWobbleAmplitude = 0f;   // set > 0 for tiny breathing of FOV
    public float fovWobbleHz = 0.35f;

    [Header("Fade")]
    public float fadeInSeconds = 0.35f;
    public float fadeOutSeconds = 0.6f;

    [Header("Debug")]
    public bool verboseLogs = false;

    // Runtime
    private Coroutine routine;
    private Material runtimeMat;
    private EffectFovMixer mixer;
    private int fovHandle = -1;
    private bool isEnding;

    protected override void OnBegin(float duration, float intensity)
    {
        if (verboseLogs)
        {
            Debug.Log("[VodkaEffect] OnBegin duration=" + duration +
                      " intensity=" + intensity);
        }

        // URP check
        var rp = GraphicsSettings.currentRenderPipeline;
        if (!(rp is UniversalRenderPipelineAsset))
        {
            Debug.LogWarning("[VodkaEffect] Current render pipeline is not URP. " +
                             "VodkaEffect (URP) only works with URP. Aborting.");
            End();
            return;
        }

        if (targetCam == null || !targetCam.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[VodkaEffect] targetCam is null or inactive. Aborting.");
            End();
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!enabled) enabled = true;
        isEnding = false;

        // Optional FOV wobble
        if (fovWobbleAmplitude > 0f)
        {
            mixer = targetCam.GetComponent<EffectFovMixer>();
            if (mixer == null)
            {
                mixer = targetCam.gameObject.AddComponent<EffectFovMixer>();
                if (verboseLogs)
                {
                    Debug.Log("[VodkaEffect] Added EffectFovMixer to camera " +
                              targetCam.name);
                }
            }
            mixer.SetBaseFov(baseFov);
            fovHandle = mixer.Register(this, 0);
        }

        // Create runtime material
        CreateRuntimeMaterial();

        // Tell the URP feature to use our material
        DrunkBlitFeature.DebugLogs = verboseLogs;
        if (runtimeMat != null && runtimeMat.shader != null)
        {
            DrunkBlitFeature.SetRuntimeMaterial(runtimeMat);
            if (verboseLogs)
            {
                Debug.Log("[VodkaEffect] Assigned runtime material '" +
                          runtimeMat.name + "' to DrunkBlitFeature.");
            }
        }
        else
        {
            DrunkBlitFeature.SetRuntimeMaterial(null);
            Debug.LogWarning("[VodkaEffect] runtimeMat is null or has no shader. " +
                             "DrunkBlitFeature will not render anything.");
        }

        routine = StartCoroutine(Run(Mathf.Clamp01(intensity)));
    }

    private void CreateRuntimeMaterial()
    {
        runtimeMat = null;

        if (drunkMaterialTemplate != null)
        {
            runtimeMat = new Material(drunkMaterialTemplate);
            if (verboseLogs)
            {
                Debug.Log("[VodkaEffect] Created runtime material from template '" +
                          drunkMaterialTemplate.name + "'.");
            }
        }
        else
        {
            // Try some common shader names as fallback
            Shader sh =
                Shader.Find("Drunk") ??
                Shader.Find("Hidden/Drunk") ??
                Shader.Find("Hidden/DrunkEffect") ??
                Shader.Find("Custom/DrunkEffect");

            if (sh != null)
            {
                runtimeMat = new Material(sh);
                if (verboseLogs)
                {
                    Debug.Log("[VodkaEffect] Created runtime material from Shader.Find('" +
                              sh.name + "').");
                }
            }
            else if (verboseLogs)
            {
                Debug.LogWarning("[VodkaEffect] No drunkMaterialTemplate assigned and no " +
                                 "matching drunk shader found by name.");
            }
        }

        if (runtimeMat != null)
        {
            // Try to initialize intensity props if they exist
            if (runtimeMat.HasProperty("_Intensity"))
            {
                runtimeMat.SetFloat("_Intensity", 0f);
            }
            if (runtimeMat.HasProperty("_Strength"))
            {
                runtimeMat.SetFloat("_Strength", 0f);
            }
        }
    }

    protected override void OnEnd()
    {
        if (isEnding) return;
        isEnding = true;

        if (verboseLogs)
        {
            Debug.Log("[VodkaEffect] OnEnd called.");
        }

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

        // Stop URP render feature
        DrunkBlitFeature.SetRuntimeMaterial(null);

        // Defer material destruction to next frame
        if (runtimeMat != null)
        {
            StartCoroutine(DestroyMatNextFrame(runtimeMat));
            runtimeMat = null;
        }
    }

    private IEnumerator DestroyMatNextFrame(Material m)
    {
        yield return null;
#if UNITY_EDITOR
        if (Application.isPlaying) Object.Destroy(m);
        else Object.DestroyImmediate(m);
#else
        Object.Destroy(m);
#endif
    }

    private IEnumerator Run(float strength)
    {
        float fin = Mathf.Max(0f, fadeInSeconds);
        float fout = Mathf.Max(0f, fadeOutSeconds);

        while (Time.time < endTime && targetCam != null)
        {
            float t = Time.time - startTime;
            float timeLeft = Mathf.Max(0f, endTime - Time.time);

            float in01 = (fin > 0f) ? Mathf.Clamp01(t / fin) : 1f;
            float out01 = (fout > 0f) ? Mathf.Clamp01(timeLeft / fout) : 1f;
            float env = Mathf.Clamp01(Mathf.Min(in01, out01) * Mathf.Clamp01(strength));

            if (runtimeMat != null)
            {
                if (runtimeMat.HasProperty("_Intensity"))
                {
                    runtimeMat.SetFloat("_Intensity", env);
                }
                if (runtimeMat.HasProperty("_Strength"))
                {
                    runtimeMat.SetFloat("_Strength", env);
                }
            }

            if (mixer != null && fovHandle != -1)
            {
                float wobble = Mathf.Sin(t * fovWobbleHz * 2f * Mathf.PI) * fovWobbleAmplitude;
                mixer.SetDelta(fovHandle, wobble * env);
            }

            yield return null;
        }

        End();
    }
}
