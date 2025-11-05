using UnityEngine;
using UnityEngine.Rendering;
using System;
namespace DistortionsPro_20X
{
    public enum Pattern_variant
    {
        displace,
        standard,
        soft
    }
    [Serializable]
    public sealed class dist_variant_Parameter : VolumeParameter<Pattern_variant> { };

    [VolumeComponentMenu("20X Distortions Pro/Voronoi Patterns")]
    public class VoronoiPatterns : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Effect transparancy. Volumes 'Weight' value affect this parameter")]
        public ClampedFloatParameter Fade = new ClampedFloatParameter(0, 0, 1, true);
        [Space]
        public dist_variant_Parameter DisplaceMode = new dist_variant_Parameter();
        [Tooltip("Effect cells size")]
        public NoInterpClampedFloatParameter Size = new NoInterpClampedFloatParameter(64f, 0f, 505f);
        [Tooltip("How thik will be cells borders. ")]
        public NoInterpClampedFloatParameter Thikness = new NoInterpClampedFloatParameter(1f, 0f, 10f);
        [Tooltip("Effect speed")]
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


        public bool IsActive() => Fade.GetValue<float>() > 0.0f;

        public bool IsTileCompatible() => false;
    }
}