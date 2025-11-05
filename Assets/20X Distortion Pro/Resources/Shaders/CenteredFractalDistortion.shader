Shader "DistortionsPro_20X/CenteredFractalDistortion"
{
    Properties
    {
        // Source texture provided automatically by URP Blit pass
        _BlitTexture ("Texture", 2D) = "white" {}
    }

    HLSLINCLUDE

// Core URP math-helper library
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
// Blit.hlsl provides Vert, Attributes, Varyings, and _BlitTexture + sampler_LinearClamp
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

// Extra mask texture (optional)
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

#pragma shader_feature ALPHA_CHANNEL       // pick RG or A in mask
#pragma shader_feature FOCUS_CENTER        // radial vs planar offset

// uniforms
half _FadeMultiplier;     // enables/disables mask fading
half MaskThreshold;       // mask threshold centre value
half NoiseScale;          // scales UVs fed into noise
half TimeFactor;          // external time driver
half DistortStrength;     // overall distortion amplitude
// Per-eye optical center in UV (0..1)
float2 EyeCenterUV()
{
    float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
    float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
    return ndc * 0.5 + 0.5;                    // 0..1
}

// Pseudo-random helper ------------------------------------------------
float RandomValue(float x)                    { return frac(sin(x) * 10000.0); }
float NoiseValue(uint2 c)                     { return RandomValue(c.x + c.y * 10000); }

// Fast integer rounding ------------------------------------------------
half FastFloor(half x)                       { const half o = ~(0x80000000UL >> 1); return (half)((half)(x + o) - o); }
half FastCeil (half x)                       { const half o = ~(0x80000000UL >> 1); return (half)((half)(x - o) + o); }

// Cell corners --------------------------------------------------------
float2 FloorSW(float2 p){return float2(floor(p.x), floor(p.y));}
float2 FloorSE(float2 p){return float2(ceil(p.x),  floor(p.y));}
float2 FloorNW(float2 p){return float2(floor(p.x), ceil(p.y)); }
float2 FloorNE(float2 p){return float2(ceil(p.x),  ceil(p.y)); }

// Bilinear smooth noise ----------------------------------------------
float SmoothNoise(half2 pos)
{
    half fx = FastFloor(pos.x);  half fy = FastFloor(pos.y);
    half cx = FastCeil (pos.x);  half cy = FastCeil (pos.y);
    half2 sw = half2(fx, fy); half2 se = half2(cx, fy);
    half2 nw = half2(fx, cy); half2 ne = half2(cx, cy);

    half2 k  = smoothstep(0.0, 1.0, frac(pos));
    half s   = lerp(NoiseValue(sw), NoiseValue(se), k.x);
    half n   = lerp(NoiseValue(nw), NoiseValue(ne), k.x);
    return lerp(s, n, k.y);
}
float SmoothNoiseCubic(float2 p)
{
    float2 k = smoothstep(0.0, 1.0, frac(p));
    float s  = lerp(NoiseValue(FloorSW(p)), NoiseValue(FloorSE(p)), k.x);
    float n  = lerp(NoiseValue(FloorNW(p)), NoiseValue(FloorNE(p)), k.x);
    return lerp(s, n, k.y);
}

// Five-octave fBM -----------------------------------------------------
float FractionalNoise(float2 p){ float v=0; v+=SmoothNoise(p); v+=SmoothNoise(p*2)/2; v+=SmoothNoise(p*4)/4; v+=SmoothNoise(p*8)/8; v+=SmoothNoise(p*16)/16; return v/(1+0.5+0.25+0.125+0.0625); }
float FractionalNoiseCubic(float2 p){ float v=0; v+=SmoothNoiseCubic(p); v+=SmoothNoiseCubic(p*2)/2; v+=SmoothNoiseCubic(p*4)/4; v+=SmoothNoiseCubic(p*8)/8; v+=SmoothNoiseCubic(p*16)/16; return v/(1+0.5+0.25+0.125+0.0625); }

// Moving and nested noise --------------------------------------------
float MovingNoise(float2 p){ float x=FractionalNoise(p+_Time.y); float y=FractionalNoise(p-_Time.y); return FractionalNoise(p+float2(x,y)); }
float MovingNoiseCubic(float2 p){ float x=FractionalNoiseCubic(p+_Time.y); float y=FractionalNoiseCubic(p-_Time.y); return FractionalNoiseCubic(p+float2(x,y)); }
float NestedNoise(float2 p){ float x=MovingNoise(p); float y=MovingNoise(p+100); return MovingNoise(p+float2(x,y)); }
float NestedNoiseCubic(float2 p){ float x=MovingNoiseCubic(p); float y=MovingNoiseCubic(p+100); return MovingNoiseCubic(p+float2(x,y)); }

// Fragment: quadratic noise ------------------------------------------
float4 Frag(Varyings i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float2 uv = i.texcoord;                      // fetched from Blit.hlsl

    float n    = NestedNoise(uv * 10.0 * NoiseScale);
    float lerpT= (sin(TimeFactor * 0.5) + 1.0) * 0.5;
    float offs = lerp(0.0, 2.0, lerpT);

    float2 c = EyeCenterUV();

    #if FOCUS_CENTER
    float2 offsVec = normalize(c - uv) * (n * offs);
    #else
    float2 offsVec = (c - uv) * (n * offs);
    #endif


    // Optional mask fading
    if (_FadeMultiplier > 0)
    {
        float alphaVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
        #ifdef ALPHA_CHANNEL
            alphaVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
        #endif
        float aMask = smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, alphaVal);
        DistortStrength *= aMask;
    }

    offsVec *= DistortStrength;
    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offsVec);
}

// Fragment: cubic noise ----------------------------------------------
float4 Frag1(Varyings i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float2 uv = i.texcoord;
    float n    = NestedNoiseCubic(uv * 10.0 * NoiseScale);
    float lerpT= (sin(TimeFactor * 0.5) + 1.0) * 0.5;
    float offs = lerp(0.0, 2.0, lerpT);

    #if FOCUS_CENTER
        float2 offsVec = normalize(float2(0.5,0.5) - uv) * (n * offs);
    #else
        float2 offsVec = (float2(0.5,0.5) - uv) * (n * offs);
    #endif

    if (_FadeMultiplier > 0)
    {
        float alphaVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
        #ifdef ALPHA_CHANNEL
            alphaVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
        #endif
        float aMask = smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, alphaVal);
        DistortStrength *= aMask;
    }

    offsVec *= DistortStrength;
    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offsVec);
}

ENDHLSL

SubShader
{
    Name "#CenteredFractalDistortion#"
    Cull Off ZWrite Off ZTest Always

    Pass
    {
        HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
        ENDHLSL
    }
    Pass
    {
        HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag1
        ENDHLSL
    }
}
}