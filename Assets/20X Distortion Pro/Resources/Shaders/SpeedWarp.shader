Shader "DistortionsPro_20X/SpeedWarp"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
    }
    HLSLINCLUDE

        // ───────────────────────── Universal RP includes ─────────────────────────
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // The Blit.hlsl file provides the vertex shader (Vert),
        // the input structure (Attributes), and the output structure (Varyings)
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // ───────────────────────── Texture bindings ─────────────────────────────
        TEXTURE2D(_Mask);
        SAMPLER  (sampler_Mask);

        // ───────────────────────── Public uniforms ──────────────────────────────
        float  _FadeMultiplier;        // enables mask-driven modulation
        float  MaskThreshold;          // centre of mask smoothstep

        // Warp parameters
        float  NoiseFrequency;         // frequency of the perlin field
        float  WarpStrength;           // displacement magnitude
        float  FlowRate;               // speed multiplier for scrolling noise
        float2 WarpCenter;             // offset for vortex origin (0-1)

        // ───────────────────────── Gradient hash for Perlin ─────────────────────
        float2 GradientForCell(uint2 cell)
        {
            cell.x = (cell.x == uint(NoiseFrequency)) ? 0u : cell.x;
            int n = int(cell.x) + int(cell.y) * 11111;
            n = (n << 13) ^ n;
            n = (n * (n * n * 15731 + 789221) + 1376312589) >> 16;
            n &= 7;
            float2 grad = float2(n & 1, n >> 1) * 2.0 - 1.0;
            return (n >= 6) ? float2(0.0, grad.x) :
                   (n >= 4) ? float2(grad.x, 0.0)  : grad;
        }

        // ───────────────────────── 2-D Perlin noise (periodic in X) ─────────────
        float PerlinNoise(float2 p)
        {
            uint2 cell  = uint2(floor(p));
            float2 fract = frac(p);
            float2 fade = fract * fract * (3.0 - 2.0 * fract);

            float g00 = dot(GradientForCell(cell + uint2(0, 0)), fract - float2(0, 0));
            float g10 = dot(GradientForCell(cell + uint2(1, 0)), fract - float2(1, 0));
            float g01 = dot(GradientForCell(cell + uint2(0, 1)), fract - float2(0, 1));
            float g11 = dot(GradientForCell(cell + uint2(1, 1)), fract - float2(1, 1));

            float nx0 = lerp(g00, g10, fade.x);
            float nx1 = lerp(g01, g11, fade.x);
            return lerp(nx0, nx1, fade.y);
        }
        float2 EyeCenterUV()
        {
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
            return ndc * 0.5 + 0.5;                    // -> UV 0..1 
        }
        // ───────────────────────── Fragment shader ──────────────────────────────
        float4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

            float2 uv = IN.texcoord;
            float2 c =EyeCenterUV();
            float2 offset = uv - c - WarpCenter;
            offset.y *= _ScreenParams.y / _ScreenParams.x;
            float radius = length(offset);
            float2 dir   = offset / max(radius, 1e-5);

            float quadrantScalar = (dir.y > 0.0)
                                   ? (dir.x > 0.0 ? dir.x : dir.y)
                                   : (dir.x > 0.0 ? -dir.y : -dir.x);

            if (_FadeMultiplier > 0.0)
            {
                float maskSample;
            #if ALPHA_CHANNEL
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
            #else
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
            #endif
                float fadeVal = smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskSample);
                WarpStrength *= fadeVal;
            }

            quadrantScalar = 1.0 - sqrt(1.0 - quadrantScalar);
            float noiseSample = PerlinNoise(float2(quadrantScalar * NoiseFrequency, _Time.y * 100.0 * FlowRate));
            float displacement = noiseSample * radius * radius * WarpStrength;
            uv += dir * displacement;

            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        }

    ENDHLSL

    SubShader
    {
        Name "#SpeedWarp#"
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
