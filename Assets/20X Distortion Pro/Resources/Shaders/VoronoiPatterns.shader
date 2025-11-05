Shader "DistortionsPro_20X/VoronoiPatterns"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
    }
    HLSLINCLUDE

        // includes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // textures
        TEXTURE2D(_Mask);
        SAMPLER  (sampler_Mask);

        // uniforms
        float _FadeMultiplier;
        float MaskThreshold;
        float CellSize;
        float FlowRate;
        uint  DisplayMode;
        float BlendFactor;
        float Thickness;

        // scratch uniforms used by Voronoi calculation
        uniform half2 g_cell;
        uniform half2 g_offset;
        uniform half  edgeOne;
        uniform half  edgeTwo;

        // pseudo-random hash that animates over time
        float2 hashPoint2(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yzx + 19.19);
            float2 h = frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
            return 0.5 + 0.3 * sin(_Time.y * FlowRate + h * 6.2831853);
        }

        float2 EyeCenterUV()
        {
            // get eye center in UV
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1)); // project forward
            float2 ndc  = clip.xy / max(clip.w, 1e-6);           // to -1..1
            return ndc * 0.5 + 0.5;                              // to 0..1
        }

        // 2-D Voronoi – returns (edge distance, nearest feature vector)
        float3 computeVoronoi(float2 pos)
        {
            float2 baseCell = floor(pos);
            float2 fracPos  = frac(pos);

            float2 nearestVec = 0.0;
            float  minDist    = 80.0;

            [unroll] for (int oy = -2; oy <= 2; ++oy)
            {
                [unroll] for (int ox = -2; ox <= 2; ++ox)
                {
                    float2 cellOffset = float2(ox, oy);
                    float2 offset    = hashPoint2(baseCell + cellOffset);
                    float2 vec       = cellOffset + offset - fracPos;
                    float  dist      = dot(vec, vec);
                    if (dist < minDist)
                    {
                        minDist   = dist;
                        nearestVec = vec;
                        g_cell    = cellOffset;
                        g_offset  = offset;
                    }
                }
            }

            edgeOne = 10000.0;
            edgeTwo = 10000.0;
            [unroll] for (int j = -2; j <= 2; ++j)
            {
                [unroll] for (int i = -2; i <= 2; ++i)
                {
                    float2 cellOffset = g_cell + float2(i, j);
                    float2 offset    = hashPoint2(baseCell + cellOffset);
                    float2 vec       = cellOffset + offset - fracPos;
                    if (dot(nearestVec - vec, nearestVec - vec) > 0.100001)
                    {
                        float d = dot(0.5 * (nearestVec + vec), normalize(vec - nearestVec));
                        edgeTwo = min(edgeTwo, max(edgeOne, d));
                        edgeOne = min(edgeOne, d);
                    }
                }
            }
            return float3(edgeOne, nearestVec);
        }

        // Convert screen UV into displaced UV via Voronoi field
        float2 distortUV(float2 uvScreen)
        {
            // reset helper variables
            g_cell   = float2(-1, -1);
            g_offset = float2(-1, -1);
            edgeOne  = 10000.0;
            edgeTwo  = 10000.0;

            // eye space: local from the current eye center
            float2 c   = EyeCenterUV();
            float2 e   = uvScreen - c;                // around eye center
            float2 ePx = e * _ScreenParams.xy;        // eye space in pixels

            // Voronoi in eye-space pixels (use ePx instead of posPixels)
            float3 vData = computeVoronoi(ePx / CellSize);

            // jitter also in eye space
            float2 cellCoords = floor(ePx / CellSize) + g_cell;
            float2 jitter     = (hashPoint2(-cellCoords) - 0.5) * CellSize * (0.5 + 0.5 * Thickness);

            // choose edge mode, unchanged
            float edgeDist;
            if (DisplayMode == 0u)
            {
                edgeDist = clamp(clamp(vData.x * 64.0 - 0.0625, 0.0, 1.0) * clamp(vData.x * 32.0 - 0.0625, 0.0, 1.0), 0.0, 1.0);
                edgeDist *= smoothstep(-0.02, 0.03125, vData.x) * smoothstep(-0.08, 0.0625, vData.x) * smoothstep(-0.12, 0.125, vData.x);
            }
            else if (DisplayMode == 1u)
            {
                float df = clamp(2.0 - 4.0 * sqrt(sqrt(edgeTwo - edgeOne)), 0.0, 1.0);
                edgeDist = df * df * df * df;
            }
            else
            {
                float d   = length(vData.yz);
                float d10 = clamp(1.125 - 1.5 * d, 0.0, 1.0);
                float de7 = clamp(clamp(vData.x * 64.0 - 0.0625, 0.0, 1.0) * clamp(vData.x * 32.0 - 0.0625, 0.0, 1.0), 0.0, 1.0);
                edgeDist  = d10 * de7;
            }

            // displacement in eye pixels -> back to UV
            float2 ePxDisplaced = lerp(ePx, ePx + jitter, edgeDist);
            float2 uvOut        = c + ePxDisplaced / _ScreenParams.xy;

            return clamp(uvOut, 0.0, 1.0);
        }

        // fragment shader
        float4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

            float2 uv       = IN.texcoord;
            float4 warped   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortUV(uv));
            float4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

            float blend = BlendFactor;
            if (_FadeMultiplier > 0.0)
            {
                float maskVal;
            #if ALPHA_CHANNEL
                maskVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
            #else
                maskVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
            #endif
                blend *= smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskVal);
            }

            return lerp(original, warped, blend);
        }

    ENDHLSL

    SubShader
    {
        Name "#VoronoiPatterns#"
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
