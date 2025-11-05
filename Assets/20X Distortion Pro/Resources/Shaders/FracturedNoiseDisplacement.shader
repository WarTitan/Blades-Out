Shader "DistortionsPro_20X/FracturedNoiseDisplacement"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
    }
    HLSLINCLUDE

        // Include core URP functions and Blit vertex shader
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Texture bindings
        TEXTURE2D(_Mask);
        SAMPLER(sampler_Mask);

        // Mask parameters
        float  _FadeMultiplier;       // 0 = ignore mask, >0 = blend with mask
        float  MaskThreshold;         // fade edge threshold
        // Control parameters
        float  NoiseSpeed;            // animation rate of noise
        float  DisplaceMagnitude;     // maximum distortion amount
        float  BlendFactor;           // final lerp strength

        // Noise helpers
        // Simple 2D hash returning repeatable pseudo-random value in 0-1
        float HashRand(float2 st)
        {
            return frac(sin(dot(st, float2(12.9898, 78.233))) * 43758.5453123);
        }
        float2 EyeCenterUV()
        {
            // per-eye optical center in 0..1 UV
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
            return ndc * 0.5 + 0.5;                    // 0..1
        }

        // Gradient noise (classic Perlin) with Hermite interpolation
        float PerlinNoise(float2 st)
        {
            float2 cell     = floor(st);      // integer lattice coordinates
            float2 fracPart = frac(st);       // position inside cell

            // Corner pseudo-random values
            float v00 = HashRand(cell);
            float v10 = HashRand(cell + float2(1.0, 0.0));
            float v01 = HashRand(cell + float2(0.0, 1.0));
            float v11 = HashRand(cell + float2(1.0, 1.0));

            // Hermite curve for smooth interpolation
            float2 smooth = fracPart * fracPart * (3.0 - 2.0 * fracPart);

            // Bilinear blend of the four corners
            float interpX1 = lerp(v00, v10, smooth.x);
            float interpX2 = lerp(v01, v11, smooth.x);
            return lerp(interpX1, interpX2, smooth.y);
        }

        // Fragment shader
        float4 Frag(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            float2 uv  = i.texcoord;
            float2 c   = EyeCenterUV();
            float2 uvc = uv - c;                 // eye-local UV (same phase per eye)

            // Time-varying amplitudes for each axis
            float ampX = (sin(_Time.y * NoiseSpeed) + 1.0) * 0.5 * DisplaceMagnitude;
            float ampY = (cos(_Time.y * NoiseSpeed) + 1.0) * 0.5 * DisplaceMagnitude;

            // Offset samples using independent noise fields per axis
            float2 warpedUV = uv + float2(PerlinNoise(uvc * ampX), PerlinNoise(uvc * ampY));

            float4 warpedColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUV);
            float4 baseColor   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

            // Optional mask modulation
            if (_FadeMultiplier > 0.0)
            {
                float maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
            #if ALPHA_CHANNEL
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
            #endif
                float fadeVal = smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskSample);
                BlendFactor *= fadeVal;
            }

            return lerp(baseColor, warpedColor, BlendFactor);
        }
    ENDHLSL

    SubShader
    {
        Name "#FracturedNoiseDisplacement#"
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
