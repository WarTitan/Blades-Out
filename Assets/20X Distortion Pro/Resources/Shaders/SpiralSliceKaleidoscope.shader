Shader "DistortionsPro_20X/SpiralSliceKaleidoscope"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
    }
    HLSLINCLUDE

        // URP includes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Texture bindings
        TEXTURE2D(_Mask);
        SAMPLER  (sampler_Mask);

        // Uniforms
        float  _FadeMultiplier;        // enables mask‑based fading
        float  MaskThreshold;

        uint   IterationCount;         // number of spiral slices drawn
        float  SpiralScale;            // global scale of spirals
        float  SpiralRotation;         // per‑slice rotation
        float  SpiralOffset;           // centre shift amplitude
        float  NoiseWarpStrength;      // horizontal noise warp amount
        float  BlendFactor;            // lerp between original & kaleido
        float  AnimationSpeed;         // time multiplier
        half   BaseDarkening;          // factor to darken background before compositing
        float2 EyeCenterUV()
        {
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
            return ndc * 0.5 + 0.5;                    // -> UV 0..1 
        }
        // per-eye screen size (без unity_StereoScaleOffset)
        float2 EyeScreenWH()
        {
            #if defined(UNITY_SINGLE_PASS_STEREO) && !defined(STEREO_INSTANCING_ON) && !defined(STEREO_MULTIVIEW_ON)
                return float2(_ScreenParams.x * 0.5, _ScreenParams.y); // double-wide: ширина/2
            #else
                return _ScreenParams.xy; // instancing/multiview/mono
            #endif
        }

        // Noise helpers
        float Hash1(float p) { return frac(sin(p) * 10000.0); }
        float GridNoise(float2 p) { return Hash1(p.x + p.y * 10000.0); }

        float2 SW(float2 p) { return floor(p); }
        float2 SE(float2 p) { return float2(ceil(p.x), floor(p.y)); }
        float2 NW(float2 p) { return float2(floor(p.x), ceil(p.y)); }
        float2 NE(float2 p) { return ceil(p); }

        float SmoothNoise(float2 p)
        {
            float2 interp = smoothstep(0.0, 1.0, frac(p));
            float south = lerp(GridNoise(SW(p)), GridNoise(SE(p)), interp.x);
            float north = lerp(GridNoise(NW(p)), GridNoise(NE(p)), interp.x);
            return lerp(south, north, interp.y);
        }

        float FBMNoise(float2 p)
        {
            float t = _Time.y * AnimationSpeed;
            float sum = 0.0;
            sum += SmoothNoise(p - t);
            sum += SmoothNoise(p * 2.0 + t)     / 2.0;
            sum += SmoothNoise(p * 4.0 - t)     / 4.0;
            sum += SmoothNoise(p * 8.0 + t)     / 8.0;
            sum += SmoothNoise(p * 16.0 - t)    / 16.0;
            return sum * (1.0 / 1.9375);
        }

float4 Frag(Varyings IN) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    float2 center = EyeCenterUV();           
    float2 uv     = IN.texcoord;
    float2 baseUV = uv;

    // time-varying parameters
    float dynamicScale    = SpiralScale * 0.9 + cos(_Time.y * 2.0 * AnimationSpeed) * 0.1;
    float dynamicRotation = sin(_Time.y * 0.5 * AnimationSpeed) * SpiralRotation;
    float2 dynamicOffset  = 0.25 * SpiralOffset
                          + 0.25 * float2(cos(_Time.y * AnimationSpeed), sin(_Time.y * AnimationSpeed));

    float4 baseColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, baseUV);
    float4 composite = baseColor * BaseDarkening;

    // horizontal noise warp
    uv.x += FBMNoise(uv) * NoiseWarpStrength;

    [loop]
    for (uint idx = 0u; idx < IterationCount; ++idx)
    {
        float2 pos = uv - dynamicOffset;
        pos /= pow(abs(dynamicScale), float(idx));
        pos += dynamicOffset;

        float theta = radians(-dynamicRotation) * float(idx);
        float si = sin(theta), co = cos(theta);   

        pos -= center;
        pos *= float2(_ScreenParams.x * 0.5, _ScreenParams.y);
        pos = float2(pos.x * co - pos.y * si, pos.x * si + pos.y * co);
        pos /= float2(_ScreenParams.x * 0.5, _ScreenParams.y);
        pos += center + float2(-NoiseWarpStrength * 0.5, 0);

        if (all(pos >= 0.0) && all(pos <= 1.0))
        {
            float4 sliceCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, pos);
            composite = sliceCol + (1.0 - sliceCol.a) * composite;
        }
    }

    float blend = BlendFactor;
    if (_FadeMultiplier > 0.0)
    {
        float maskSample =
        #if ALPHA_CHANNEL
            SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).a;
        #else
            SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).r;
        #endif
        float fadeVal = smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskSample);
        blend *= fadeVal;
    }

    return lerp(baseColor, composite, blend);
}


    ENDHLSL

    SubShader
    {
        Name "#SpiralSliceKaleidoscope#"
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
