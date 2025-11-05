Shader "DistortionsPro_20X/CircularRippleWarp"
{
    Properties
    {
        // URP blit source texture
        _BlitTexture("Texture", 2D) = "white" {}
    }

    HLSLINCLUDE

        // Includes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // The Blit.hlsl file provides the vertex shader (Vert),
        // the input structure (Attributes), and the output structure (Varyings)
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Texture interfaces
        TEXTURE2D(_Mask);
        SAMPLER(sampler_Mask);

        // External controls
        float _FadeMultiplier;  
        float MaskThreshold;    
        half  RippleFrequency;  //  how many ripples per unit radius
        half  RippleDivider;    //  scales ripple thickness
        half  BlendFade;        //  blend amount used in lerp
        half  WaveSpeed;        //  animation rate
        float2 EyeCenterUV()
        {
            // per-eye optical center in UV (0..1)
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
            return ndc * 0.5 + 0.5;
        }

        // Fragment – concentric ripple warp
        float4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

            float2 uv0 = IN.texcoord;
            float2 uv  = uv0;

            float t = _Time.x * 10.05 * WaveSpeed;

            // per-eye center
            float2 c = EyeCenterUV();

            // aspect-corrected radial distance from the per-eye center
            float2 p = uv0 - c;
            p.x *= (_ScreenParams.x / _ScreenParams.y);
            float r = length(p);

            // ripple phase (unchanged idea)
            r = frac((r - t) * RippleFrequency) / RippleDivider;

            // warp = lerp from center to original UV by r  (equivalent to your -1..1 form)
            uv = lerp(c, uv0, r);

            // Optional fade mask
            if (_FadeMultiplier > 0.0)
            {
            #if ALPHA_CHANNEL
                float maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv0).a;
            #else
                float maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv0).r;
            #endif
                float maskVal = smoothstep(MaskThreshold - 0.05,
                                           MaskThreshold + 0.05, maskSample);
                BlendFade *= maskVal;   // modulate blend by mask
            }

            float4 warped = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            float4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv0);
            return lerp(source, warped, BlendFade); // final mix
        }

    ENDHLSL

    SubShader
    {
        Name "#CircularRippleWarp#"
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
                #pragma vertex Vert        // provided by Blit.hlsl
                #pragma fragment Frag
            ENDHLSL
        }
    }
}
