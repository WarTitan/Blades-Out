Shader "DistortionsPro_20X/FlowMosh"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        Pass
        {
            Name "FlowMosh"
            ZTest Always      // draw after everything (post-process)
            Cull Off          // full-screen quad: show both faces
            ZWrite Off        // no depth writes (post-process)

            HLSLINCLUDE
// Includes
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
// Blit.hlsl gives Vert(), Varyings, and Attributes for full-screen passes
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

// Textures & samplers
TEXTURE2D_X(_PreviousTex);  SAMPLER(sampler_PreviousTex);   // previous frame (feedback)
SAMPLER(sampler_BlitTexture);

// User parameters (set from C# / material)
float _BrightnessThreshold;  // minimal luma delta that counts as motion
float _Threshold;            // minimal flow length to apply smear
float _Size;                 // block size for UV snap grid
float _NoiseAmp;             // strength of procedural wobble
float fade;                  // global blend (0 = off, 1 = full)
float _MicroContrastBoost;   // <1 boosts small differences; >1 reduces them
float ChromaShift;           // RGB misregistration amount (0 = off)
float FLOW_GAIN = .895;      // global flow scale (>1 stronger, <1 weaker)
float _FlowXScale = 0.50;    // axis bias: +X stronger, -Y weaker
float C_Offset;    // axis bias: +X stronger, -Y weaker

// Fast RGB→grayscale (simple weights; not color-accurate)
float4 ToGray(float4 c)
{
    float g = dot(c.rgb, float3(0.33, 0.5, 0.17)); // fast, approximate
    return float4(g, g, g, 1);
}
float4 TexGray_Main(float2 uv) { return ToGray(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv)); }
float4 TexGray_Prev(float2 uv) { return ToGray(SAMPLE_TEXTURE2D_X(_PreviousTex,  sampler_PreviousTex,  uv)); }

// 1D central difference along an offset axis
float Gradient1D(float2 uv, float2 off)
{
    // previous frame
    float pr = TexGray_Prev(uv + off).r - TexGray_Prev(uv - off).r;
    // current frame
    float cr = TexGray_Main(uv + off).r - TexGray_Main(uv - off).r;
    return pr + cr;
}

// Build a 2D flow vector from gradients + small time-varying jitter
float2 FlowVec(float fx, float fy, float tp)
{
    // 1) normalize and measure edge sharpness
    float L  = max(abs(fx) + abs(fy), 1e-3);
    float2 n = float2(fx, fy) / L;

    // 2) soft threshold + gamma shaping (controls turn-on and growth)
    // was EDGE_BIAS = 0.07 and EDGE_GAMMA = 1.5; now smoother and earlier
    float amp = pow(saturate(L - 0.0001), 0.05);

    // 3) jitter (adds “life” / avoids perfect alignment)
    float rand  = frac(sin(dot(n, float2(12.9898, 78.233))) * 43758.55) * 2 - 1;
    float angle = rand * 0.2 * (0.3 + 0.7 * sin(tp)) * amp;
    float s = sin(angle), c = cos(angle);
    float2 nd = float2(n.x * c - n.y * s, n.x * s + n.y * c);

    // 4) axis bias: boost X and reduce Y with one slider
    nd *= float2(1.0 + _FlowXScale, 1.0 - _FlowXScale);

    // 5) overall flow gain (final strength)
    float2 v = nd * (amp * FLOW_GAIN);

    // 6) optional quadrant packing (smooth blend to your original packing)
    const float PACK_BLEND = 0.0; // 0 = smooth (no packing), 1 = packed
    float2 fxp = float2(max(v.x, 0), abs(min(v.x, 0)));
    float2 fyp = float2(max(v.y, 0), abs(min(v.y, 0)));
    float  dirY = (fyp.x > fyp.y) ? -1 : 1;
    float2 packed = float2(fxp.x - fxp.y, max(fyp.x, fyp.y) * dirY);

    return lerp(v, packed, PACK_BLEND);
}

// Cheap hash: ~uniform random in [0,1]
float Hash21(float2 p)
{
    return frac(sin(dot(p, float2(27.1, 61.7))) * 43758.33);
}

// MAIN FRAGMENT
float4 Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv    = input.texcoord;
    float2 texel = 1/ _ScreenParams.xy;

    // Current vs previous frame colors
    float4 curRGB = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
    float4 prvRGB = SAMPLE_TEXTURE2D_X(_PreviousTex,  sampler_PreviousTex,  uv);

    // Unsigned luma difference
    float diff   = abs(dot(prvRGB.rgb - curRGB.rgb, float3(0.2126, 0.7152, 0.0722)));
    diff         = pow(diff, _MicroContrastBoost);   // boost/suppress small changes
    float motion = saturate(diff / _BrightnessThreshold); // 0..1 motion scalar

    // Edge gradients (short and long taps)
    float2 off1 = texel;
    float2 off4 = texel * C_Offset;

    float gx = lerp(Gradient1D(uv, off1),     Gradient1D(uv, off4),     0.4);
    float gy = lerp(Gradient1D(uv, off1.yy),  Gradient1D(uv, off4.yy),  0.4);

    // Flow vector with time jitter
    float2 flow = FlowVec(motion * gx, motion * gy, _Time.y * 3.0);

    // Procedural wobble noise
    float2 nUV = uv * 10 - _Time.y * 0.03;
    float2 n   = float2(Hash21(nUV), Hash21(nUV.yx + 4.0)) - 0.5;
    flow      -= n * _NoiseAmp;
    // flow = clamp(flow, -1, 1); // safety (optional)

    // Apply only if flow is large enough
    float stepper = step(_Threshold, length(flow));

    // Warp UV with block snapping
    float2 warpUV = uv + flow * texel;
    warpUV = (floor(warpUV / max(_Size, 1e-5)) + 0.5) * max(_Size, 1e-5);
    warpUV = clamp(warpUV, 0.0, 1.0);

    // Chroma glitch: shift red channel
    float2 cshift = float2(cos(flow.x * PI + _Time.y * 0.1),
                           sin(flow.y * PI + _Time.y * 0.1)) * 0.0025;
    float  r   = SAMPLE_TEXTURE2D_X(_PreviousTex, sampler_PreviousTex, warpUV + cshift * ChromaShift).r;
    float4 dat = SAMPLE_TEXTURE2D_X(_PreviousTex, sampler_PreviousTex, warpUV);
    dat.r = r;

    // Final blend: only where stepper==1
    float4 smear = lerp(curRGB, dat, stepper);
    return lerp(curRGB, smear, fade);
}
ENDHLSL

HLSLPROGRAM
    #pragma vertex Vert
    #pragma fragment Frag
ENDHLSL
        }
    }
}
