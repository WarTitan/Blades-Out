Shader "DistortionsPro_20X/VortexPixelShatter"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
    }
    HLSLINCLUDE

        // includes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // The Blit.hlsl file provides the vertex shader (Vert),
        // the input structure (Attributes), and the output structure (Varyings)
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // textures
        TEXTURE2D(_Mask);
        SAMPLER  (sampler_Mask);

        // uniforms
        float _FadeMultiplier;      // enables mask blend
        float MaskThreshold;

        float PixelateStrength;
        float2 ShatterCenter;

        float2 EyeCenterUV()
        {
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
            return ndc * 0.5 + 0.5;                    // -> UV 0..1 
        }

        float4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

            float2 uv      = IN.texcoord;
            float2 c  = EyeCenterUV();
            float2 baseUV  = uv;
            float  blend   = 1.0;

            // Distance from effect centre
            float distFromCentre = length(uv - c - ShatterCenter);

            // optional mask modulation
            if (_FadeMultiplier > 0.0)
            {
                float maskVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).r;
            #ifdef ALPHA_CHANNEL
                maskVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).a;
            #endif
                blend *= smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskVal);
            }

            // pixelation clarity factor grows toward centre
            float clarity = (1.01 - PixelateStrength) * 100.0;
            clarity /= min(distFromCentre, 1.0);

            // snap UVs to grid for shatter effect
            float2 shattered = floor(uv * clarity) / clarity;

            float4 originalCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, baseUV);
            float4 shatteredCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, shattered);
            return lerp(originalCol, shatteredCol, blend);
        }


    ENDHLSL

    SubShader
    {
        Name "#VortexPixelShatter#"
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
                #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON UNITY_SINGLE_PASS_STEREO
                #pragma vertex Vert
                #pragma fragment Frag
            ENDHLSL
        }
    }
}