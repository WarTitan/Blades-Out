using UnityEngine;
using UnityEngine.Rendering;
namespace DistortionsPro_20X
{
    [VolumeComponentMenu("20X Distortions Pro/Vortex Swirl Displacement")]
    public class VortexSwirlDisplacement : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Effect transparancy. Volumes 'Weight' value affect this parameter")]
        public ClampedFloatParameter Fade = new ClampedFloatParameter(0f, 0f, 1, true);
        [Space]
        public NoInterpClampedFloatParameter Iterations = new NoInterpClampedFloatParameter(2f, 0.0001f, 3f);
        public NoInterpClampedFloatParameter Speed = new NoInterpClampedFloatParameter(0.1f, 0f, 10f);
        public NoInterpVector2Parameter CenterOffset = new NoInterpVector2Parameter(Vector2.zero);
        public NoInterpClampedFloatParameter RotationScale = new NoInterpClampedFloatParameter(0.5f, -2f, 2f);
        public NoInterpClampedFloatParameter Falloff = new NoInterpClampedFloatParameter(1f, 0f, 3f);
        [Space]
        [Tooltip("Mask texture")]
        public TextureParameter mask = new TextureParameter(null);
        [Tooltip("If your mask texture is not alpha transparent, use a red channel.")]
        public DistortionsPro_20X.maskChannelModeParameter maskChannel = new();
        [Tooltip("Value to adjust mask edge thickness.")]
        public NoInterpClampedFloatParameter maskEdgeFineTuning = new NoInterpClampedFloatParameter(.15f, 0.000001f, 1f);
        [Space]
        [Tooltip("Use Global Post Processing Settings to enable or disable Post Processing in scene view or via camera setup. THIS SETTING SHOULD BE TURNED OFF FOR EFFECTS, IN CASE OF USING THEM FOR SEPARATE LAYERS")]
        public BoolParameter GlobalPostProcessingSettings = new BoolParameter(false);

        public bool IsActive() => Fade.GetValue<float>() > 0.0f;

        public bool IsTileCompatible() => false;
    }
}