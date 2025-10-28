// FILE: LsdEffect.cs
// Trippy LSD effect + double-buffered, edge-masked trails
// Adds CENTER-CLEAR controls (radius/feather) sent to the shader.

using UnityEngine;
using System.Collections;

[AddComponentMenu("Gameplay/Effects/LSD Effect")]
[DefaultExecutionOrder(50530)]
public class LsdEffect : PsychoactiveEffectBase
{
    [Header("Blit Material (Hidden/LsdEffect)")]
    public Material lsdMaterialTemplate;

    [Header("Look Controls")]
    [Range(0f, 1f)] public float strength = 1.0f;
    public float hueShiftHz = 0.25f;
    public float kaleidoMin = 4f;
    public float kaleidoMax = 8f;
    public float kaleidoSweepHz = 0.10f;
    public float swirlStrength = 1.2f;
    public float warpAmplitude = 0.020f;
    public float warpHz = 0.7f;
    public float chromAb = 1.0f;
    public float vignette = 0.35f;

    [Header("Center Clear")]
    [Range(0f, 0.9f)] public float centerClearRadius = 0.27f;  // 0 = no clear center, 0.3 ~ nice circle
    [Range(0.01f, 0.9f)] public float centerClearFeather = 0.18f;

    [Header("Trails (Afterimage)")]
    public bool enableTrails = true;
    [Range(0f, 1f)] public float trailMix = 0.18f;
    [Range(0f, 1f)] public float trailEdgeMask = 0.8f;
    public float trailEdgeBoost = 2.0f;
    public float trailEdgeSoftness = 1.5f;
    [Range(0f, 1f)] public float trailBorderMask = 0.4f;

    [Header("Breathing Zoom (FOV via mixer)")]
    public bool useFovMixer = true;
    public float baseFovBoost = 6f;
    public float fovPulseAmp = 6f;
    public float fovHz = 0.18f;
    public float fadeInSeconds = 0.45f;
    public float fadeOutSeconds = 0.70f;

    [Header("Performance")]
    [Tooltip("1 = full res, 2 = half, 4 = quarter for the internal work buffers.")]
    public int downscale = 1;

    [Header("Debug")]
    public bool verboseLogs = false;

    private Coroutine routine;
    private Material runtimeMat;
    private LsdBlit blit;
    private EffectFovMixer fovMixer;
    private int fovHandle = -1;

    protected override void OnBegin(float duration, float intensity)
    {
        if (targetCam == null || !targetCam.gameObject.activeInHierarchy)
        {
            if (verboseLogs) Debug.LogWarning("[LsdEffect] No active camera; abort.");
            End();
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!enabled) enabled = true;

        if (useFovMixer)
        {
            fovMixer = targetCam.GetComponent<EffectFovMixer>();
            if (fovMixer == null) fovMixer = targetCam.gameObject.AddComponent<EffectFovMixer>();
            fovMixer.SetBaseFov(baseFov);
            fovHandle = fovMixer.Register(this, 0);
        }

        blit = targetCam.GetComponent<LsdBlit>();
        if (blit == null) blit = targetCam.gameObject.AddComponent<LsdBlit>();

        if (lsdMaterialTemplate != null)
        {
            runtimeMat = new Material(lsdMaterialTemplate);
        }
        else
        {
            var sh = Shader.Find("Hidden/LsdEffect");
            if (sh != null) runtimeMat = new Material(sh);
        }

        if (runtimeMat != null)
        {
            blit.Configure(runtimeMat, enableTrails, trailMix, Mathf.Max(1, downscale));
        }
        else
        {
            blit.Configure(null, false, 0f, Mathf.Max(1, downscale));
        }

        routine = StartCoroutine(Run(Mathf.Clamp01(intensity)));
    }

    protected override void OnEnd()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (fovMixer != null && fovHandle != -1)
        {
            fovMixer.Unregister(fovHandle);
            fovHandle = -1;
        }

        if (blit != null) blit.Configure(null, false, 0f, Mathf.Max(1, downscale));

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

    private IEnumerator Run(float intensity01)
    {
        float fin = Mathf.Max(0f, fadeInSeconds);
        float fout = Mathf.Max(0f, fadeOutSeconds);

        while (Time.time < endTime && targetCam != null)
        {
            float t = Time.time - startTime;
            float timeLeft = Mathf.Max(0f, endTime - Time.time);

            float in01 = (fin > 0f) ? Mathf.Clamp01(t / fin) : 1f;
            float out01 = (fout > 0f) ? Mathf.Clamp01(timeLeft / fout) : 1f;
            float env = Mathf.Clamp01(Mathf.Min(in01, out01)) * strength * intensity01;

            if (fovMixer != null && fovHandle != -1)
            {
                float fovPulse = Mathf.Sin(t * fovHz * 2f * Mathf.PI) * fovPulseAmp;
                float fovDelta = env * (baseFovBoost + fovPulse);
                fovMixer.SetDelta(fovHandle, fovDelta);
            }

            if (runtimeMat != null)
            {
                float hue = t * hueShiftHz * 2f * Mathf.PI;
                float seg = Mathf.Lerp(kaleidoMin, kaleidoMax, 0.5f + 0.5f * Mathf.Sin(t * kaleidoSweepHz * 2f * Mathf.PI));

                runtimeMat.SetFloat("_Intensity", env);
                runtimeMat.SetFloat("_Hue", hue);
                runtimeMat.SetFloat("_Kaleido", seg);
                runtimeMat.SetFloat("_Swirl", swirlStrength * env);
                runtimeMat.SetFloat("_WarpAmp", warpAmplitude * env);
                runtimeMat.SetFloat("_WarpHz", warpHz);
                runtimeMat.SetFloat("_ChromAb", chromAb * env);
                runtimeMat.SetFloat("_Vignette", Mathf.Clamp01(vignette));

                // Center-clear params
                runtimeMat.SetFloat("_CenterClearRadius", Mathf.Clamp01(centerClearRadius));
                runtimeMat.SetFloat("_CenterClearFeather", Mathf.Clamp01(centerClearFeather));

                // Trails params (edge-masked)
                runtimeMat.SetFloat("_TrailMix", trailMix);
                runtimeMat.SetFloat("_TrailMixRuntime", enableTrails ? trailMix * env : 0f);
                runtimeMat.SetFloat("_TrailEdgeMask", Mathf.Clamp01(trailEdgeMask));
                runtimeMat.SetFloat("_TrailEdgeBoost", Mathf.Max(0.0f, trailEdgeBoost));
                runtimeMat.SetFloat("_TrailEdgeSoftness", Mathf.Max(0.0001f, trailEdgeSoftness));
                runtimeMat.SetFloat("_TrailVignetteMask", Mathf.Clamp01(trailBorderMask));
            }

            yield return null;
        }

        End();
    }
}

[AddComponentMenu("Rendering/LSD Blit (Built-in RP)")]
[RequireComponent(typeof(Camera))]
public class LsdBlit : MonoBehaviour
{
    [SerializeField] private Material material;
    [SerializeField] private bool trails;
    [SerializeField] private float trailMix; // 0..1
    [SerializeField] private int downscale = 1;

    private RenderTexture historyA;
    private RenderTexture historyB;
    private bool useAasRead = true;

    private int lastW, lastH, lastDs;

    public void Configure(Material m, bool useTrails, float mix, int ds)
    {
        material = m;
        trails = useTrails;
        trailMix = Mathf.Clamp01(mix);
        downscale = Mathf.Max(1, ds);

        enabled = (material != null);
        if (!enabled) ReleaseHistory();
    }

    private void OnDisable()
    {
        ReleaseHistory();
    }

    private void ReleaseHistory()
    {
        if (historyA != null) { historyA.Release(); Destroy(historyA); historyA = null; }
        if (historyB != null) { historyB.Release(); Destroy(historyB); historyB = null; }
        lastW = lastH = lastDs = 0;
        useAasRead = true;
    }

    private void EnsureHistory(RenderTexture source)
    {
        int w = Mathf.Max(1, source.width / downscale);
        int h = Mathf.Max(1, source.height / downscale);

        bool needRealloc = (historyA == null || historyB == null ||
                            w != lastW || h != lastH || downscale != lastDs ||
                            !historyA.IsCreated() || !historyB.IsCreated());

        if (needRealloc)
        {
            ReleaseHistory();

            historyA = new RenderTexture(w, h, 0, source.format);
            historyA.filterMode = FilterMode.Bilinear;
            historyA.wrapMode = TextureWrapMode.Clamp;
            historyA.name = "LSD_HistoryA";
            historyA.Create();

            historyB = new RenderTexture(w, h, 0, source.format);
            historyB.filterMode = FilterMode.Bilinear;
            historyB.wrapMode = TextureWrapMode.Clamp;
            historyB.name = "LSD_HistoryB";
            historyB.Create();

            var prev = RenderTexture.active;
            RenderTexture.active = historyA; GL.Clear(false, true, Color.black);
            RenderTexture.active = historyB; GL.Clear(false, true, Color.black);
            RenderTexture.active = prev;

            lastW = w; lastH = h; lastDs = downscale;
            useAasRead = true;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null || material.shader == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        EnsureHistory(source);

        RenderTexture readHist = useAasRead ? historyA : historyB;
        RenderTexture writeHist = useAasRead ? historyB : historyA;

        material.SetTexture("_PrevTex", readHist);
        material.SetVector("_MainTex_TexelSize", new Vector4(1f / source.width, 1f / source.height, source.width, source.height));

        RenderTexture workA = RenderTexture.GetTemporary(readHist.width, readHist.height, 0, source.format);
        RenderTexture workB = RenderTexture.GetTemporary(readHist.width, readHist.height, 0, source.format);
        workA.filterMode = FilterMode.Bilinear;
        workB.filterMode = FilterMode.Bilinear;

        Graphics.Blit(source, workA);
        Graphics.Blit(workA, workB, material, 0);   // PASS 0 (effect + center-clear)

        Graphics.Blit(workB, destination);          // to screen

        if (trails && trailMix > 0f)
        {
            material.SetFloat("_TrailMixRuntime", trailMix);
            Graphics.Blit(workB, writeHist, material, 1); // PASS 1 (edge-masked trails)
        }
        else
        {
            Graphics.Blit(workB, writeHist);
        }

        RenderTexture.ReleaseTemporary(workA);
        RenderTexture.ReleaseTemporary(workB);

        useAasRead = !useAasRead;
    }
}
