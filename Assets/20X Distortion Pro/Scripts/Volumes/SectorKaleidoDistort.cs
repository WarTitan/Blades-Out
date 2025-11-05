using UnityEngine;
using UnityEngine.Rendering;
namespace DistortionsPro_20X
{
    [VolumeComponentMenu("20X Distortions Pro/Sector Kaleido Distort")]
    public class SectorKaleidoDistort : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Effect transparancy. Volumes 'Weight' value affect this parameter")]
        public ClampedFloatParameter Fade = new(0f, 0f, 1f, true);
        public NoInterpClampedFloatParameter Intensity = new(1f, 0f, 1f);
        [Space]
        public NoInterpClampedFloatParameter InnerRadius = new(0.1f, 0f, 1f);
        public NoInterpClampedFloatParameter OuterRadius = new(0.49f, 0f, 1f);
        public NoInterpClampedFloatParameter Fill = new(0.9f, -1f, 1f);
        [Tooltip("Fill areas out of circle with black color.")]
        public BoolParameter Black = new(true);
        [Space]
        [Tooltip("Mask texture")]
        public TextureParameter mask = new(null);
        [Tooltip("If your mask texture is not alpha transparent, use a red channel.")]
        public DistortionsPro_20X.maskChannelModeParameter maskChannel = new DistortionsPro_20X.maskChannelModeParameter();
        [Tooltip("Value to adjust mask edge thickness.")]
        public NoInterpClampedFloatParameter maskEdgeFineTuning = new(.15f, 0.000001f, 1f);

        [Space]
        [Tooltip("Use Global Post Processing Settings to enable or disable Post Processing in scene view or via camera setup. THIS SETTING SHOULD BE TURNED OFF FOR EFFECTS, IN CASE OF USING THEM FOR SEPARATE LAYERS")]
        public BoolParameter GlobalPostProcessingSettings = new(false);


        public bool IsActive() => Fade.GetValue<float>() > 0.0f;

        public bool IsTileCompatible() => false;
    }
}