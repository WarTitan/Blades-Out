Shader "DistortionsPro_20X/FluidNoiseDistortion"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
    }
    HLSLINCLUDE
        // Include only the URP core functions and the Blit utility
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Mask texture and its sampler
        TEXTURE2D(_Mask);
        SAMPLER(sampler_Mask);

        // Public uniforms
        float  _FadeMultiplier;   // enables mask blending
        float  MaskThreshold;     // threshold for mask
        float  NoiseScale;        // scale of simplex noise
        float  DistortStrength;   // overall distortion intensity
        float  CustomTime;        // external time driver

        // Helpers for simplex noise
        float3 Mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float4 Mod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float4 Permute(float4 x) { return Mod289(((x * 34.0) + 1.0) * x); }
        float4 InvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }
        float2 EyeCenterUV()
{
    // UV of the current eye's optical center
    float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
    float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
    return ndc * 0.5 + 0.5;                    // 0..1
}

        // Computes 3D simplex noise
        float SimplexNoise(float3 v)
        {
            const float2  C = float2(1.0/6.0, 1.0/3.0);
            const float4  D = float4(0.0, 0.5, 1.0, 2.0);

            // First corner
            float3 i  = floor(v + dot(v, C.yyy));
            float3 x0 = v - i + dot(i, C.xxx);

            // Offsets for the remaining corners
            float3 g = step(x0.yzx, x0.xyz);
            float3 l = 1.0 - g;
            float3 i1 = min(g, l.zxy);
            float3 i2 = max(g, l.zxy);

            float3 x1 = x0 - i1 + C.xxx;
            float3 x2 = x0 - i2 + C.yyy;
            float3 x3 = x0 - D.yyy;

            // Permutations
            i = Mod289(i);
            float4 p = Permute(
                          Permute(
                            Permute(i.z + float4(0.0, i1.z, i2.z, 1.0))
                          + i.y + float4(0.0, i1.y, i2.y, 1.0))
                        + i.x + float4(0.0, i1.x, i2.x, 1.0));

            // Gradients: 7x7 points over a square, mapped onto an octahedron.
            float n_ = 0.142857142857; // 1/7
            float3 ns = n_ * D.wyz - D.xzx;

            float4 j  = p - 49.0 * floor(p * ns.z * ns.z);
            float4 x_ = floor(j * ns.z);
            float4 y_ = floor(j - 7.0 * x_);

            float4 x = x_ * ns.x + ns.yyyy;
            float4 y = y_ * ns.x + ns.yyyy;
            float4 h = 1.0 - abs(x) - abs(y);

            // Normalize gradients
            float4 b0 = float4(x.xy, y.xy);
            float4 b1 = float4(x.zw, y.zw);
            float4 s0 = floor(b0) * 2.0 + 1.0;
            float4 s1 = floor(b1) * 2.0 + 1.0;
            float4 sh = -step(h, float4(1.0,1.0,1.0,1.0));

            float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
            float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

            float3 p0 = float3(a0.xy, h.x);
            float3 p1 = float3(a0.zw, h.y);
            float3 p2 = float3(a1.xy, h.z);
            float3 p3 = float3(a1.zw, h.w);

            float4 norm = InvSqrt(float4(dot(p0,p0), dot(p1,p1), dot(p2,p2), dot(p3,p3)));
            p0 *= norm.x; p1 *= norm.y; p2 *= norm.z; p3 *= norm.w;

            // Mix contributions
            float4 m = max(0.6 - float4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3)), 0.0);
            m = m * m;
            return DistortStrength * 42.0 * dot(m*m, float4(dot(p0,x0), dot(p1,x1), dot(p2,x2), dot(p3,x3)));
        }

        // Fragment shader
        float4 Frag(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            // retrieve UV from Blit input
            float2 uv = i.texcoord;
            // Eye-space UV: same 0..1 frame but centered per-eye
            float2 c   = EyeCenterUV();
            float2 uve = uv - c + 0.5;

            // time factor for animation
            float time = CustomTime * 0.05;

            // apply mask blending if enabled
            if (_FadeMultiplier > 0.0)
            {
            #if ALPHA_CHANNEL
                float m = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
            #else
                float m = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
            #endif
                float maskVal = smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, m);
                DistortStrength *= maskVal;
            }

            // base offsets with noise (use eye-space UV)
            float offX = uve.x * 0.5 - time;
            float offY = uve.y + sin(uve.x) * 0.1 - sin(time * 0.25)
                       + SimplexNoise(float3(uve.x, uve.y, time) * NoiseScale);

            // additional turbulence (also eye-space)
            offX += SimplexNoise(float3(offX, offY, time) * 5.0) * 0.3 * NoiseScale;
            offY += SimplexNoise(float3(offX, offX, time * 0.3)) * 0.1 * NoiseScale;

            // fine-ripple details (eye-space)
            float nc = SimplexNoise(float3(offX, offY, time * 0.75) * 2.0) * 0.001;
            float nh = SimplexNoise(float3(offX, offY, time * 0.25) * 2.0) * 0.3;
            nh *= smoothstep(nh, 2.5, 3.0);

            // combine noise into warp vector
            float2 warp = float2(nc + nh, nc + nh);

            // sample distorted and base colors from the Blit texture
            float4 baseCol  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + warp);
            float4 hoverCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + warp);
            float blendFac  = clamp(nh * 10.0 + 1.0, 0.0, 1.0);

            // final mix
            return lerp(baseCol, hoverCol, blendFac);
        }
    ENDHLSL

    SubShader
    {
        Name "#FluidNoiseDistortion#"
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