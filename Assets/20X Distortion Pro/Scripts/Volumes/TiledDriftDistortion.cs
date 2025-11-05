using UnityEngine;
using UnityEngine.Rendering;
namespace DistortionsPro_20X
{
    [VolumeComponentMenu("20X Distortions Pro/Tiled Drift Distortion")]
    public class TiledDriftDistortion : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Effect amount. Volumes 'Weight' value affect this parameter")]
        public ClampedFloatParameter Amount = new ClampedFloatParameter(0f, 0f, 1f, true);
        [Space]
        public NoInterpClampedFloatParameter Size = new NoInterpClampedFloatParameter(10f, 0f, 100f);
        public NoInterpClampedFloatParameter Speed = new NoInterpClampedFloatParameter(1f, 0f, 20f);
        [Space]
        [Tooltip("Mask texture")]
        public TextureParameter mask = new TextureParameter(null);
        [Tooltip("If your mask texture is not alpha transparent, use a red channel.")]
        public DistortionsPro_20X.maskChannelModeParameter maskChannel = new DistortionsPro_20X.maskChannelModeParameter();
        [Tooltip("Value to adjust mask edge thickness.")]
        public NoInterpClampedFloatParameter maskEdgeFineTuning = new NoInterpClampedFloatParameter(.15f, 0.000001f, 1f);
        [Space]
        [Tooltip("Use Global Post Processing Settings to enable or disable Post Processing in scene view or via camera setup. THIS SETTING SHOULD BE TURNED OFF FOR EFFECTS, IN CASE OF USING THEM FOR SEPARATE LAYERS")]
        public BoolParameter GlobalPostProcessingSettings = new BoolParameter(false);


        public bool IsActive() => Amount.GetValue<float>() > 0.0f;

        public bool IsTileCompatible() => false;
    }
}