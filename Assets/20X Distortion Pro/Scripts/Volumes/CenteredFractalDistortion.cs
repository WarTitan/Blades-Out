using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace DistortionsPro_20X
{
    [VolumeComponentMenu("20X Distortions Pro/Centered Fractal Distortion")]
    public class CenteredFractalDistortion : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Effect amount. Volumes 'Weight' value affect this parameter")]
        public ClampedFloatParameter Intensity = new(0f, 0f, 1f, true);
        [Space]
        [Tooltip("Squeeze image to the center.")]
        public BoolParameter CenterFocus = new(false);
        public BoolParameter CubicNoise = new(false);
        [Tooltip("Distortion waves size.")]
        public NoInterpClampedFloatParameter Size = new(0.6f, 0f, 10f);
        [Tooltip("Effect speed. Use this value to animate effect.")]
        public NoInterpClampedFloatParameter Speed = new(1f, 0f, 10f);
        [Space]
        [Tooltip("Mask texture.")]
        public TextureParameter mask = new(null);
        [Tooltip("If your mask texture is not alpha transparent, use a red channel.")]
        public DistortionsPro_20X.maskChannelModeParameter maskChannel = new DistortionsPro_20X.maskChannelModeParameter();
        [Tooltip("Value to adjust mask edge thickness.")]
        public NoInterpClampedFloatParameter maskEdgeFineTuning = new(.15f, 0.000001f, 1f);
        [Space]
        [Tooltip("Use Global Post Processing Settings to enable or disable Post Processing in scene view or via camera setup. THIS SETTING SHOULD BE TURNED OFF FOR EFFECTS, IN CASE OF USING THEM FOR SEPARATE LAYERS")]
        public BoolParameter GlobalPostProcessingSettings = new(false);
        public bool IsActive() => Intensity.value > 0;

        public bool IsTileCompatible() => false;
    }
}