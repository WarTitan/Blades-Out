using UnityEngine;
using UnityEngine.Rendering;
namespace DistortionsPro_20X
{
    [VolumeComponentMenu("20X Distortions Pro/RadialSliceShifter")]
    public class RadialSliceShifter : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Minimum line width. Volumes 'Weight' value affect this parameter")]
        public ClampedFloatParameter LineWidth = new(0f, 0f, 1, true);
        public NoInterpClampedFloatParameter Speed = new(1f, 0f, 10);
        [Tooltip("Animate line width")]
        public BoolParameter AnimateLine = new(true);
        [Space]
        [Tooltip("Mask texture")]
        public TextureParameter Mask = new(null);
        [Tooltip("If your mask texture is not alpha transparent, use a red channel.")]
        public DistortionsPro_20X.maskChannelModeParameter MaskChannel = new();
        [Tooltip("Value to adjust mask edge thickness.")]
        public NoInterpClampedFloatParameter MaskEdgeFineTuning = new(.15f, 0.000001f, 1f);
        [Space]
        [Tooltip("Use Global Post Processing Settings to enable or disable Post Processing in scene view or via camera setup. THIS SETTING SHOULD BE TURNED OFF FOR EFFECTS, IN CASE OF USING THEM FOR SEPARATE LAYERS")]
        public BoolParameter GlobalPostProcessingSettings = new(false);
        public bool IsActive() => LineWidth.GetValue<float>() > 0.0f;

        public bool IsTileCompatible() => false;
    }
}