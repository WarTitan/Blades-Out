using UnityEngine;
using UnityEngine.Rendering;
namespace DistortionsPro_20X
{
    [VolumeComponentMenu("20X Distortions Pro/SpiralSliceKaleidoscope")]
    public class SpiralSliceKaleidoscope : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Effect transparancy. Volumes 'Weight' value affect this parameter")]
        public ClampedFloatParameter Fade = new(0f, 0f, 1f, true);
        [Space]
        [Tooltip("Amount of images")]
        public NoInterpClampedIntParameter Images = new(5, 1, 20);
        public NoInterpClampedFloatParameter Scale = new(1f, 0f, 2f);
        public NoInterpClampedFloatParameter Rotation = new(45f, 0f, 180f);
        public NoInterpClampedFloatParameter Offset = new(1f, 0f, 2f);
        public NoInterpClampedFloatParameter NoiseWarpStrength = new(0.5f, 0f, 1f);
        public BoolParameter Black = new(false);
        public NoInterpClampedFloatParameter Speed = new(0.5f, 0f, 2f);
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