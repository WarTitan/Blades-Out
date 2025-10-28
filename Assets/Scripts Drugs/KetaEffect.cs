// FILE: KetaEffect.cs
// FULL REPLACEMENT (ASCII only)
// Realistic left-right head tilt using EffectRollMixer:
// - Symmetric cycle: left dwell -> transit -> right dwell -> transit -> ...
// - Eased motion through center (smootherstep), brief pauses at extremes
// - Mild tempo irregularity and tiny jitter to avoid robotic feel
// - While active, rotates mouse axes with roll via LocalCameraController
// - Uses base.startTime / base.endTime so ExtendDuration() works

using UnityEngine;
using System.Collections;

[AddComponentMenu("Gameplay/Effects/Keta Effect")]
[DefaultExecutionOrder(50520)]
public class KetaEffect : PsychoactiveEffectBase
{
    [Header("Tilt Shape (degrees)")]
    public float swayAmplitudeDeg = 36f;      // peak tilt +/- around 0
    public float baseLeanDeg = 0f;            // constant offset (keep 0 for perfect symmetry)

    [Header("Timing (seconds)")]
    public float halfTravelSeconds = 1.2f;    // time to go from left to right (or right to left)
    public float dwellSeconds = 0.15f;        // pause at extremes
    public float fadeInSeconds = 0.25f;
    public float fadeOutSeconds = 0.70f;

    [Header("Natural Irregularity")]
    public float tempoIrregularityHz = 0.35f; // how fast tempo fluctuates
    [Range(0f, 0.6f)]
    public float tempoIrregularityStrength = 0.15f; // 0..0.6 scale of speed variation

    [Header("Micro Jitter (tiny, for realism)")]
    public float jitterDeg = 0.6f;
    public float jitterHz = 2.2f;

    [Header("Mouse Input Assist")]
    public bool forceRotateInputWithRoll = true;   // make controls follow the tilt
    [Range(0f, 1f)] public float forcedRollInputFactor = 1.0f;

    [Header("Debug")]
    public bool verboseLogs = false;

    // Runtime
    private EffectRollMixer rollMixer;
    private int rollHandle = -1;
    private Coroutine routine;

    // Input assist state
    private LocalCameraController lcc;
    private bool savedRotateFlag;
    private float savedRollFactor;
    private bool changedLcc;

    // Phase time with tempo irregularity
    private float phaseTime;          // accumulated "musical time" that advances with jittered speed
    private float tempoSeed;          // noise seed
    private float jitterSeed;         // noise seed for micro jitter

    protected override void OnBegin(float duration, float intensity)
    {
        if (targetCam == null || !targetCam.gameObject.activeInHierarchy)
        {
            if (verboseLogs) Debug.LogWarning("[KetaEffect] No active camera; abort.");
            End();
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!enabled) enabled = true;

        // Mixer
        rollMixer = targetCam.GetComponent<EffectRollMixer>();
        if (rollMixer == null) rollMixer = targetCam.gameObject.AddComponent<EffectRollMixer>();
        rollHandle = rollMixer.Register(this, 0);

        // Input assist
        lcc = targetCam.GetComponentInParent<LocalCameraController>();
        changedLcc = false;
        if (forceRotateInputWithRoll && lcc != null)
        {
            savedRotateFlag = lcc.rotateInputWithRoll;
            savedRollFactor = lcc.rollInputFactor;
            lcc.rotateInputWithRoll = true;
            lcc.rollInputFactor = Mathf.Clamp01(forcedRollInputFactor);
            changedLcc = true;
        }

        // Seeds and phase
        tempoSeed = Random.value * 1000f;
        jitterSeed = Random.value * 2000f;
        phaseTime = 0f;

        routine = StartCoroutine(Run(Mathf.Clamp01(intensity)));
    }

    protected override void OnEnd()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (rollMixer != null && rollHandle != -1)
        {
            rollMixer.Unregister(rollHandle);
            rollHandle = -1;
        }

        if (changedLcc && lcc != null)
        {
            lcc.rotateInputWithRoll = savedRotateFlag;
            lcc.rollInputFactor = savedRollFactor;
            changedLcc = false;
        }
    }

    private IEnumerator Run(float strength)
    {
        float fin = Mathf.Max(0f, fadeInSeconds);
        float fout = Mathf.Max(0f, fadeOutSeconds);

        // Precompute segment lengths
        float half = Mathf.Max(0.05f, halfTravelSeconds);
        float dwell = Mathf.Max(0f, dwellSeconds);
        float cycleLen = 2f * half + 2f * dwell; // L dwell -> L->R -> R dwell -> R->L

        while (Time.time < endTime && targetCam != null)
        {
            float t = Time.time - startTime;
            float timeLeft = Mathf.Max(0f, endTime - Time.time);

            // Global envelope (fade in/out)
            float in01 = (fin > 0f) ? Mathf.Clamp01(t / fin) : 1f;
            float out01 = (fout > 0f) ? Mathf.Clamp01(timeLeft / fout) : 1f;
            float env = Mathf.Clamp01(Mathf.Min(in01, out01) * strength);

            // Tempo irregularity (speed multiplier 1 +/- strength)
            float tempoNoise = (Mathf.PerlinNoise(tempoSeed + Time.time * tempoIrregularityHz, 0f) * 2f - 1f);
            float speedMul = 1f + tempoIrregularityStrength * tempoNoise;

            // Advance phase time with irregular speed
            phaseTime += Time.deltaTime * Mathf.Max(0.2f, speedMul);

            // Where are we in the cycle?
            float u = Mathf.Repeat(phaseTime, cycleLen);

            // Compute roll angle based on which segment we are in
            float A = Mathf.Abs(swayAmplitudeDeg);
            float roll = 0f;

            if (u < dwell)
            {
                // Left dwell at -A
                roll = -A;
            }
            else if (u < dwell + half)
            {
                // Transit L -> R over 'half' seconds with smootherstep easing
                float segT = (u - dwell) / half;               // 0..1
                float v = SmootherStep(segT);
                roll = Mathf.Lerp(-A, +A, v);
            }
            else if (u < dwell + half + dwell)
            {
                // Right dwell at +A
                roll = +A;
            }
            else
            {
                // Transit R -> L
                float segT = (u - (dwell + half + dwell)) / half; // 0..1
                float v = SmootherStep(segT);
                roll = Mathf.Lerp(+A, -A, v);
            }

            // Tiny head jitter around the baseline (adds imperfection)
            float j = (Mathf.PerlinNoise(jitterSeed + Time.time * jitterHz, 0f) * 2f - 1f) * jitterDeg;

            float finalRoll = env * (baseLeanDeg + roll + j);

            if (rollMixer != null && rollHandle != -1)
                rollMixer.SetDelta(rollHandle, finalRoll);

            yield return null;
        }

        End();
    }

    // Smoother than SmoothStep; cubic quintic smootherstep 0..1
    private static float SmootherStep(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * x * (x * (x * 6f - 15f) + 10f);
    }
}
