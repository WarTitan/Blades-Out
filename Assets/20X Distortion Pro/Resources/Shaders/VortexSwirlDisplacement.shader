Shader "DistortionsPro_20X/VortexSwirlDisplacement"
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
        float  _FadeMultiplier;   // turn mask fade on/off
        float  MaskThreshold;     // mask level for smoothstep
        half   AnimSpeed;         // swirl speed
        half   BlendFactor;       // final mix strength
        half   IterationCount;    // number of samples

        float4 _CenterOffset;     // use x and y
        half   _RotScale;         // rotation strength
        half   _FalloffExp;       // fade by radius

        // helpers
        #define TAU 6.2831853     // 2*pi
        #define rot(a) float2x2(cos(a), -sin(a), sin(a), cos(a))

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

            float2 uv     = IN.texcoord; // read UV
            float2 baseUV = uv;          // keep a copy

            // move center by offset, keep in 0..1
            float2 c = saturate(EyeCenterUV() + _CenterOffset.xy);

            // UV relative to center
            float2 centred = uv - c;
            // time in radians
            float  globalT = fmod(_Time.y * AnimSpeed, TAU);

            // sum color from many turns
            float4 swirlCol = 0.0;
            [loop]
            for (float idx = 0.0; idx < IterationCount; idx += 1.0)
            {
                // phase for this layer
                float layerPhase = globalT + TAU / 3.0 * idx;
                // weight of this layer
                float weight = (0.5 - 0.5 * cos(layerPhase)) / 1.5;

                // radius and falloff
                float r        = max(length(centred), 1e-4);
                float signTerm = (-0.5 + frac(layerPhase / TAU));
                float falloff  = pow(r, -_FalloffExp);
                // final rotation amount
                float rotationCoeff = _RotScale * signTerm * falloff;

                // rotate and sample
                float2 rotatedUV = mul(centred, rot(rotationCoeff)) + c;
                swirlCol += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, rotatedUV) * weight;
            }

            // blend with optional mask
            float blend = BlendFactor;
            if (_FadeMultiplier > 0.0)
            {
                float maskSample;
            #if ALPHA_CHANNEL
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).a; // read alpha
            #else
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).r; // read red
            #endif
                // use mask to change blend
                blend *= smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskSample);
            }

            // original color
            float4 originalCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, baseUV);
            // mix original and swirl
            return lerp(originalCol, swirlCol, blend);
        }

    ENDHLSL

    SubShader
    {
        Name "#VortexSwirlDisplacement#"
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
