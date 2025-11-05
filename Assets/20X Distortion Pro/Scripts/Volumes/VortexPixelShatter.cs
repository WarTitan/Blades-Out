using UnityEngine;
using UnityEngine.Rendering;
namespace DistortionsPro_20X
{
    [VolumeComponentMenu("20X Distortions Pro/VortexPixelShatter")]
    public class VortexPixelShatter : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Effect amount. Volumes 'Weight' value affect this parameter")]
        public ClampedFloatParameter Amount = new(0f, 0f, 1f, true);
        public Vector2Parameter Center = new(Vector2.zero);
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
        public bool IsActive() => Amount.GetValue<float>() > 0.0f;

        public bool IsTileCompatible() => false;
    }
}