Shader "DistortionsPro_20X/TwistShrinkDistortion"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
    }
    HLSLINCLUDE

        // Universal RP includes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Texture bindings
        TEXTURE2D(_Mask);
        SAMPLER  (sampler_Mask);

        // Uniforms
        float _FadeMultiplier;
        float MaskThreshold;
        float BlendFactor;
        float TwistStrength;
        float LayerPeriod;
        float AnimSpeed;

        // Helpers
        #define PI 3.1415927
        #define Cos01(t) (0.5 - 0.5 * cos(t))
        float2 EyeCenterUV()
        {
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
            return ndc * 0.5 + 0.5;                    // -> UV 0..1 
        }
        // Render a twisting, shrinking layer
        float4 RenderLayer(float layerTime, float2 uv)
        {
            float ramp     = frac(layerTime / LayerPeriod);
            float envelope = Cos01(2.0 * PI * ramp);
            float2 c = EyeCenterUV();
            float angle    = TwistStrength * ramp * length(uv-c);
            float2x2 R     = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
            float shrink   = lerp(1.0, 0.5, ramp);
            float2 warped  = mul(R, uv) * shrink;
            return envelope * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warped);
        }

        // Fragment shader
        float4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
            float2 uv = IN.texcoord;
            float t   = _Time.y * AnimSpeed;

            float4 color = RenderLayer(t, uv);
            color += RenderLayer(t + LayerPeriod / 3.0, uv);
            color += RenderLayer(t + 2.0 * LayerPeriod / 3.0, uv);

            float blend = BlendFactor;
            if (_FadeMultiplier > 0.0)
            {
                float mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
            #if ALPHA_CHANNEL
                mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
            #endif
                blend *= smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, mask);
            }

            float4 baseCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            return lerp(baseCol, color, blend);
        }

    ENDHLSL

    SubShader
    {
        Name "#TwistShrinkDistortion#"
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
