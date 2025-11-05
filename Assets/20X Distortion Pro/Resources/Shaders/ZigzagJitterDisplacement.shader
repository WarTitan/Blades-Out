Shader "DistortionsPro_20X/ZigzagJitterDisplacement"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
    }
    HLSLINCLUDE

        // include files
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // Blit.hlsl gives Vert and data structs
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // textures
        TEXTURE2D(_Mask);
        SAMPLER  (sampler_Mask);

        // uniforms
        float  _FadeMultiplier;   // enable fade via mask
        float  MaskThreshold;     // mask threshold for smoothstep
        float  JitterSpeed;       // noise scroll speed
        float  NoiseScale;        // controls wavelength
        float  JitterAmount;      // displacement strength
        half   BlendFactor;       // final mix weight

        // hash helpers
        float RandHash(float n) { return frac(sin(n) * 43758.5453123); }

        // 1-D value noise with linear fade curve
        float ValueNoise(float p)
        {
            float fl = floor(p);
            float fc = frac(p);
            return lerp(RandHash(fl), RandHash(fl + 1.0), fc);
        }

        float2 EyeCenterUV()
        {
            // get eye center in UV
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1)); // project forward
            float2 ndc  = clip.xy / max(clip.w, 1e-6);           // to -1..1
            return ndc * 0.5 + 0.5;                              // to 0..1
        }

        // fragment shader
        float4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

            float2 uv     = IN.texcoord;
            float2 baseUV = uv;

            float2 c = EyeCenterUV();     // current eye center in UV
            float2 e = uv - c;            // local coords relative to center

            e.y += ValueNoise(_Time.y * JitterSpeed + e.x * 50.0 / NoiseScale) * JitterAmount;
            e.x += ValueNoise(_Time.y * JitterSpeed + e.y * 25.0 / NoiseScale) * JitterAmount;

            float2 jitterUV = c + e;      // back to global UV
            jitterUV = clamp(jitterUV, 0.0, 1.0);

            // mask-controlled blend factor
            float blend = BlendFactor;
            if (_FadeMultiplier > 0.0)
            {
                float maskSample;
            #if ALPHA_CHANNEL
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).a;
            #else
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).r;
            #endif
                blend *= smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskSample);
            }

            float4 originalCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, baseUV);
            float4 jitterCol   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, jitterUV);
            return lerp(originalCol, jitterCol, blend);
        }

    ENDHLSL

    SubShader
    {
        Name "#ZigzagJitterDisplacement#"
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
