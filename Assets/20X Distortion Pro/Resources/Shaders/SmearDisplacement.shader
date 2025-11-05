Shader "DistortionsPro_20X/SmearDisplacement" 
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
    }
    HLSLINCLUDE

        // ───────────────────────── URP includes ─────────────────────────
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // ───────────────────────── Texture bindings ─────────────────────
        TEXTURE2D(_Mask);
        SAMPLER  (sampler_Mask);

        // ───────────────────────── Control uniforms ─────────────────────
        float  _FadeMultiplier;          // enables mask blending
        float  MaskThreshold;            // midpoint of mask fade
        uint   InvertEdge;               // 0/1 flag to invert edge map
        float  SmearStrength;            // displacement magnitude

        // ───────────────────────── Utility: grayscale luminance ─────────
        float Luminance1(float4 c) { return (c.r + c.g + c.b) / 3.0; }
        // Per-eye optical center in UV (projects forward (0,0,-1))
        float2 EyeCenterUV()
        {
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6);  // -1..1
            return ndc * 0.5 + 0.5;                     // 0..1
        }
        // ───────────────────────── Fragment shader ─────────────────────
        float4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

            float2 uv = IN.texcoord;
            float2 texel = 1.0 / _ScreenParams.xy; // pixel size in UV space

            // Sample 3×3 neighbourhood for Sobel edge detection
            float4 c   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            float4 cL  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, clamp(uv + float2(-texel.x,  0.0), 0.0, 1.0));
            float4 cR  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, clamp(uv + float2( texel.x,  0.0), 0.0, 1.0));
            float4 cU  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, clamp(uv + float2( 0.0,   texel.y), 0.0, 1.0));
            float4 cD  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, clamp(uv + float2( 0.0,  -texel.y), 0.0, 1.0));
            float4 cUL = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, clamp(uv + float2(-texel.x,  texel.y), 0.0, 1.0));
            float4 cUR = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, clamp(uv + float2( texel.x,  texel.y), 0.0, 1.0));
            float4 cDL = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, clamp(uv + float2(-texel.x, -texel.y), 0.0, 1.0));
            float4 cDR = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, clamp(uv + float2( texel.x, -texel.y), 0.0, 1.0));

            float gx = (-Luminance1(cUL)) + (-Luminance1(cL)) + (-Luminance1(cDL))
                       +  Luminance1(cUR) +  Luminance1(cR) +  Luminance1(cDR);
            float gy =  Luminance1(cUL) +  Luminance1(cU) +  Luminance1(cUR)
                       - Luminance1(cDL) - Luminance1(cD) - Luminance1(cDR);

            float edgeMagnitude = sqrt(gx * gx + gy * gy);
            if (InvertEdge == 1u) edgeMagnitude = 1.0 - edgeMagnitude;

            float saturation = Luminance1(c);

            float2 centre = EyeCenterUV();
            float2 diff     = uv - centre;
            float  radius   = length(diff);
            float  angle    = atan2(diff.y, diff.x);

            float strength = SmearStrength;
            if (_FadeMultiplier > 0.0)
            {
                float maskSample;
            #if ALPHA_CHANNEL
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
            #else
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
            #endif
                float fadeVal = smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskSample);
                strength *= fadeVal;
            }

            radius += strength * (1.0 - edgeMagnitude) * (saturation - 0.5);
            float2 warpedUV = centre + float2(cos(angle), sin(angle)) * radius;
            warpedUV = clamp(warpedUV, 0.0, 1.0);
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUV);
        }

    ENDHLSL

    SubShader
    {
        Name "#SmearDisplacement#"
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
