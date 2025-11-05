using UnityEngine;
using UnityEngine.Rendering;
namespace DistortionsPro_20X
{
    [VolumeComponentMenu("20X Distortions Pro/FluidWorleyDisplacement")]
    public class FluidWorleyDisplacement : VolumeComponent, IPostProcessComponent
    {

        [Tooltip("Effect amount. Volumes 'Weight' value affect this parameter")]
        public ClampedFloatParameter Intensity = new(0f, 0.00001f, 1f, true);
        [Space]
        public NoInterpClampedFloatParameter Offset = new(0.1f, 0.0001f, 2);
        public NoInterpClampedFloatParameter Speed = new(1f, 0f, 10);
        public NoInterpClampedFloatParameter Size = new(0.1f, 0f, 3);
        [Space]
        [Tooltip("Mask texture")]
        public TextureParameter mask = new(null);
        [Tooltip("If your mask texture is not alpha transparent, use a red channel.")]
        public DistortionsPro_20X.maskChannelModeParameter maskChannel = new();
        [Tooltip("Value to adjust mask edge thickness.")]
        public NoInterpClampedFloatParameter maskEdgeFineTuning = new(.15f, 0.000001f, 1f);
        [Space]
        [Tooltip("Use Global Post Processing Settings to enable or disable Post Processing in scene view or via camera setup. THIS SETTING SHOULD BE TURNED OFF FOR EFFECTS, IN CASE OF USING THEM FOR SEPARATE LAYERS")]
        public BoolParameter GlobalPostProcessingSettings = new(false);


        public bool IsActive() => Intensity.GetValue<float>() > 0.0f;

        public bool IsTileCompatible() => false;
    }
}