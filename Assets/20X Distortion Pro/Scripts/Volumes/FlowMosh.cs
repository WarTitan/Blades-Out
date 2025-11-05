using UnityEngine;
using UnityEngine.Rendering;
namespace DistortionsPro_20X
{
    [VolumeComponentMenu("20X Distortions Pro/Flow Mosh")]
    public class FlowMosh : VolumeComponent, IPostProcessComponent
    {
        [Header("Blend")]
        [Tooltip("Global blend between original image (0) and full datamosh (1).")]
        public ClampedFloatParameter Blend = new ClampedFloatParameter(1f, 0f, 1f);

        [Header("Main")]
        [Tooltip("Pixels whose flow magnitude is below this value remain untouched.")]
        public NoInterpClampedFloatParameter ActivationThreshold = new NoInterpClampedFloatParameter(0.9f, 0f, 0.9999f);
        public NoInterpClampedFloatParameter FlowGain = new NoInterpClampedFloatParameter(0.8f, 0f, 2);
        public NoInterpClampedFloatParameter FlowXScale = new NoInterpClampedFloatParameter(0.5f, 0f, 1);

        [Tooltip("Minimum brightness change that triggers the smear — higher = stronger effect, lower = weaker")]
        public NoInterpClampedFloatParameter BrightnessThreshold = new NoInterpClampedFloatParameter(.4f, 0f, 1f);

        [Tooltip("Boost for very small brightness differences")]
        public NoInterpClampedFloatParameter MicroContrastBoost = new NoInterpClampedFloatParameter(.4f, 0f, 1f);

        [Header("Block Grid")]
        [Tooltip("Size of snap blocks in UV space (≈ 0.002 is ~2 px at 1080p).")]
        public NoInterpClampedFloatParameter BlockSize = new NoInterpClampedFloatParameter(0.002f, 0.0001f, 0.2f);

        [Header("Noise")]
        [Tooltip("Blue-noise wobble amplitude (0 = none, 1 = strong).")]
        public NoInterpClampedFloatParameter NoiseAmplitude = new NoInterpClampedFloatParameter(0.9f, 0f, 1f);

        [Header("Chroma Shift")]
        [Tooltip("Chroma glitch: shift red channel.")]
        public NoInterpClampedFloatParameter ChromaShift = new NoInterpClampedFloatParameter(0.5f, 0f, 2f);

        [Header("Global Override")]
        [Tooltip("When enabled, all parameters are taken from Global Post-processing Settings.")]
        public BoolParameter GlobalPostProcessingSettings = new BoolParameter(false);

        public bool IsActive() => Blend.value > 0f;
        public bool IsTileCompatible() => false;
    }
}
