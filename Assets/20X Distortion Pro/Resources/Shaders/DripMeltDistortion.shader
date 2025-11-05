Shader "DistortionsPro_20X/DripMeltDistortion"
{
Properties
{
// URP blit source texture
    _BlitTexture("Texture", 2D) = "white" {}
}
HLSLINCLUDE

    // Core URP math helpers
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    // Provides Vert, Attributes, Varyings, and binds _BlitTexture + sampler_LinearClamp
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    // Texture interfaces
    TEXTURE2D(_Mask);
    SAMPLER(sampler_Mask);

    #pragma shader_feature FOCUS_CENTER

    // External controls
    float _FadeMultiplier;    // enables mask-driven fade
    float MaskThreshold;      // threshold center for mask

    // Distortion controls
    float WaveSpeed;          // animation rate
    float DistortStrength;    // overall distortion amplitude
    float WaveScale;          // scales UV frequency
    // --- add once in HLSLINCLUDE ---
    float2 EyeCenterUV()
    {
        // current eye optical center in UV (0..1)
        float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
        float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
        return ndc * 0.5 + 0.5;
    }

    // Fragment – dripping melt warp
    float4 Frag(Varyings IN) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
        float2 uvBase   = IN.texcoord;
        // Mask-driven strength fade
        if (_FadeMultiplier > 0.0)
        {
        #if ALPHA_CHANNEL
            float m = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uvBase).a;
        #else
            float m = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uvBase).r;
        #endif
            float maskVal = smoothstep(MaskThreshold - 0.05,
                                       MaskThreshold + 0.05, m);
            DistortStrength *= maskVal;
        }

        // eye-centered UV (per-eye), then to "pixel" space like before
        float2 c        = EyeCenterUV();
        float2 uve      = uvBase - c + 0.5;                  // eye-space 0..1
        float2 uvScreen = uve * _ScreenParams.xy;            // keep frequency behavior

        // time offset (unchanged)
        float2 timeOffset = float2(_Time.y * 10.0, _Time.y * 10.0) * 0.1 * WaveSpeed;

        // wave displacement (unchanged math)
        float2 displaced = uvScreen + sin(uvScreen * WaveScale - timeOffset) * (10.0 * DistortStrength);

        // back to global UV, undo eye-space shift
        float2 finalUV = displaced / _ScreenParams.xy;       // back to 0..1 eye-space
        finalUV = finalUV - 0.5 + c;                         // back to global 0..1
        finalUV = clamp(finalUV, 0.0, 1.0);

        return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, finalUV);
    }

ENDHLSL

SubShader
{
    Name "#DripMeltDistortion#"
    Cull Off ZWrite Off ZTest Always
    Pass
    {
        HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
        ENDHLSL
    }
}


}
