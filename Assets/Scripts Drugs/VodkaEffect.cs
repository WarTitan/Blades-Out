// FILE: VodkaEffect.cs
// FULL REPLACEMENT
// Drunk warp stays purely as a blit so it does NOT fight FOV.
// If you want subtle FOV wobble, set fovWobbleAmplitude > 0 and it will
// contribute via EffectFovMixer (not touch camera.fieldOfView directly).

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

    private Coroutine routine;
    private Material runtimeMat;
    private DrunkBlit drunk;
    private EffectFovMixer mixer;
    private int fovHandle = -1;

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
            // fallback: try find by name if user created shader named "Drunk"
            var sh = Shader.Find("Drunk");
            if (sh != null) runtimeMat = new Material(sh);
        }

        if (runtimeMat != null)
        {
            drunk.SetMaterial(runtimeMat);
        }
        else
        {
            drunk.SetMaterial(null);
        }

        routine = StartCoroutine(Run(duration, Mathf.Clamp01(intensity)));
    }

    protected override void OnEnd()
    {
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

        if (drunk != null) drunk.SetMaterial(null);

        if (runtimeMat != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(runtimeMat);
#else
            Object.Destroy(runtimeMat);
#endif
            runtimeMat = null;
        }
    }

    private IEnumerator Run(float duration, float strength)
    {
        float t0 = Time.time;
        float tEnd = t0 + Mathf.Max(0.1f, duration);

        float fin = Mathf.Max(0f, fadeInSeconds);
        float fout = Mathf.Max(0f, fadeOutSeconds);

        while (Time.time < tEnd && targetCam != null)
        {
            float t = Time.time - t0;
            float timeLeft = Mathf.Max(0f, tEnd - Time.time);

            float in01 = (fin > 0f) ? Mathf.Clamp01(t / fin) : 1f;
            float out01 = (fout > 0f) ? Mathf.Clamp01(timeLeft / fout) : 1f;
            float env = Mathf.Clamp01(Mathf.Min(in01, out01) * strength);

            // Drive drunk material intensity smoothly
            if (runtimeMat != null)
            {
                // Example: fade in/out by multiplying distort strength
                runtimeMat.SetFloat("_Intensity", env);
                // If your drunk shader uses different params (e.g., _Time based),
                // you can still pass env for amplitude scaling.
            }

            // Optional FOV wobble via mixer (tiny)
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
