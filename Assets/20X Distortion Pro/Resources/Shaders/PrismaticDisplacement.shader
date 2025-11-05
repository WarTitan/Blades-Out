Shader "DistortionsPro_20X/PrismaticDisplacement"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
    }
    HLSLINCLUDE

        //Includes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Textures 
        TEXTURE2D(_Mask);
        SAMPLER  (sampler_Mask);

        //Mask
        float  _FadeMultiplier;       // master enable for mask fading
        float  MaskThreshold;         // fade edge

        // Constants
        #define TAU 6.28318530718     // 2 * π
        #define UnitCircle(t) float2(cos(TAU * (t)), sin(TAU * (t)))

        // Animation / effect parameters
        float  FlowSpeed;            // controls time progression
        float  SampleRadius;         // radial distance for multi‑tap sampling
        uint   SampleCount;          // number of taps in the prismatic blur
        float  BlendFactor;          // final lerp amount
         
        // Utility: map 2‑D direction to rainbow hue
        float4 HueGradient(in float2 dir)
        {
            float magnitude = length(dir);
            if (magnitude == 0.0) return float4(0.0, 0.0, 0.0, 0.0);

            // Angle in radians relative to +X axis
            float angle = acos(dot(dir, float2(1.0, 0.0)) / magnitude);

            // Convert angle to rough RGB rainbow (cyclic every 2π/3)
            float3 rgb = clamp(abs(fmod(angle * 6.0 + float3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
            return float4(magnitude * (rgb - 0.1), 0.0);
        }
        float2 EyeCenterUV()
        {
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
            return ndc * 0.5 + 0.5;                    // 0..1
        }

        // Fragment shader
        float4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

            float2 uv = IN.texcoord;
            float2 c   = EyeCenterUV();
            float2 uvc = uv - c; // eye-local UV

            float   timeParam = FlowSpeed * _Time.y + 100.0; // large offset keeps animation non‑repeating

            // Base sample
            float4 baseColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

            // Accumulate offset based on surrounding colour samples arranged in a circle
            float2 offsetVec = float2(0.0, 0.0);
            for (int n = 0; n < (int)SampleCount; ++n)
            {
                float  t      = float(n) / float(SampleCount); // 0‑1 phase around circle
                float2 tapDir = SampleRadius * UnitCircle(t);                // direction on unit circle
                float2 tapUV  = uv + tapDir;    // displaced coordinate

                float4 tapCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, tapUV) / float(SampleCount);
                float  tapLen = length(tapCol.rgb);               // brightness proxy
                offsetVec += tapLen * tapDir;              // weighted sum of directions
            }

            // Low‑frequency warp pattern modulates the displacement (feels fluid)
            float2 warp = float2(sin(0.4 * timeParam + 8.0 * uvc.x + 4.0), cos(0.7 * timeParam + 14.0 * uvc.y));
            // Final UV shift: scaled by warp and accumulated offset
            float2 uvShift = 5.0 * warp * offsetVec - 0.01 * warp;

            // Optional alpha‑mask modulation of blend factor
            if (_FadeMultiplier > 0.0)
            {
                float maskSample;
            #if ALPHA_CHANNEL
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
            #else
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
            #endif
                float fadeVal = smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskSample);
                BlendFactor  *= fadeVal;
            }

            // Prismatic colour shift + displacement
            float4 warpedColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + uvShift) + HueGradient(100.0 * offsetVec);
            return lerp(baseColor, warpedColor, BlendFactor);
        }

    ENDHLSL

    SubShader
    {
        Name "#PrismaticDisplacement#"
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