// FILE: VodkaEffect.cs
// FULL REPLACEMENT (ASCII only)
// Drunk warp via blit (Built-in RP). Optional tiny FOV wobble via EffectFovMixer.
// Uses base.startTime / base.endTime so ExtendDuration() works.
// Adds safe tear-down to avoid end-of-effect hitching.

using UnityEngine;
using System.Collections;

[AddComponentMenu("Gameplay/Effects/Vodka Effect")]
[DefaultExecutionOrder(50510)]
public class VodkaEffect : PsychoactiveEffectBase
{
    [Header("Drunk Blit (Built-in RP)")]
    public Material drunkMaterialTemplate;  // your drunk shader material (optional)

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
    private DrunkBlit drunk;
    private EffectFovMixer mixer;
    private int fovHandle = -1;
    private bool isEnding;

    protected override void OnBegin(float duration, float intensity)
    {
        if (targetCam == null || !targetCam.gameObject.activeInHierarchy)
        {
            if (verboseLogs) Debug.LogWarning("[VodkaEffect] Camera missing/inactive. Aborting.");
            End();
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!enabled) enabled = true;
        isEnding = false;

        // Ensure mixer only if we actually use FOV wobble
        if (fovWobbleAmplitude > 0f)
        {
            mixer = targetCam.GetComponent<EffectFovMixer>();
            if (mixer == null) mixer = targetCam.gameObject.AddComponent<EffectFovMixer>();
            mixer.SetBaseFov(baseFov);
            fovHandle = mixer.Register(this, 0);
        }

        // Drunk blit
        drunk = targetCam.GetComponent<DrunkBlit>();
        if (drunk == null) drunk = targetCam.gameObject.AddComponent<DrunkBlit>();

        if (drunkMaterialTemplate != null)
        {
            runtimeMat = new Material(drunkMaterialTemplate);
        }
        else
        {
            // Fallback: try find by name if user created shader named "Drunk"
            var sh = Shader.Find("Drunk");
            if (sh != null) runtimeMat = new Material(sh);
        }

        if (runtimeMat != null)
        {
            // If your shader exposes _Intensity, we will drive it for fade in/out.
            runtimeMat.SetFloat("_Intensity", 0f);
            drunk.SetMaterial(runtimeMat);
        }
        else
        {
            drunk.SetMaterial(null);
        }

        // IMPORTANT: use shared endTime from base so ExtendDuration() works.
        routine = StartCoroutine(Run(Mathf.Clamp01(intensity)));
    }

    protected override void OnEnd()
    {
        if (isEnding) return;
        isEnding = true;

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

        // Disable the blit first so OnRenderImage stops running this frame.
        if (drunk != null)
        {
            drunk.SetMaterial(null);
            drunk.enabled = false;
        }

        // Defer material destruction to the next frame to avoid a stall while the camera is rendering.
        if (runtimeMat != null)
        {
            StartCoroutine(DestroyMatNextFrame(runtimeMat));
            runtimeMat = null;
        }
    }

    private IEnumerator DestroyMatNextFrame(Material m)
    {
        // Wait until end of frame to ensure no camera is mid-blit.
        yield return null;
#if UNITY_EDITOR
        // In Play Mode, prefer Destroy; DestroyImmediate can stall the editor.
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

            // Smooth fade in/out envelope
            float in01 = (fin > 0f) ? Mathf.Clamp01(t / fin) : 1f;
            float out01 = (fout > 0f) ? Mathf.Clamp01(timeLeft / fout) : 1f;
            float env = Mathf.Clamp01(Mathf.Min(in01, out01) * Mathf.Clamp01(strength));

            // Drive drunk material intensity if supported by the shader
            if (runtimeMat != null)
            {
                // If your shader does not have _Intensity, this is harmless.
                runtimeMat.SetFloat("_Intensity", env);
            }

            // Optional tiny FOV wobble via mixer (never writes camera.fieldOfView directly)
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

[AddComponentMenu("Rendering/Drunk Blit (Built-in RP)")]
[RequireComponent(typeof(Camera))]
public class DrunkBlit : MonoBehaviour
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
