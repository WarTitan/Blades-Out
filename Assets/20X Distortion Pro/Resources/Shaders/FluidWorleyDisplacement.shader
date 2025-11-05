Shader "DistortionsPro_20X/FluidWorleyDisplacement"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
    }
    HLSLINCLUDE
        // Only include URP core and the Blit utility for fullscreen passes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Mask texture and sampler
        TEXTURE2D(_Mask);
        SAMPLER(sampler_Mask);

        // Mask controls
        float _FadeMultiplier;   // 0 = ignore mask, >0 = apply mask
        float MaskThreshold;     // edge of mask transition

        // Distortion parameters
        float DisplaceAmount;    // warp magnitude in pixels
        float FlowVelocity;      // speed of flow animation
        float NoiseScale;        // scale of Worley noise pattern
        float BlendIntensity;    // overall blend strength (modulated by mask)

        // Math helpers
        float DistanceSquared(float2 v) { return dot(v, v); }  // squared length

        // jittered cell offset
        float JitterHash(float2 cell)
        {
            return frac(sin(frac(sin(cell.x) * 43.13311) + cell.y) * 31.0011);
        }

        // single-octave Worley F1, bright ridges at cell edges
        float WorleyF1(float2 pos)
        {
            float minDist = 1e30;
            [unroll] for (int dx = -1; dx <= 1; ++dx)
                [unroll] for (int dy = -1; dy <= 1; ++dy)
                {
                    float2 lattice = floor(pos) + float2(dx, dy);
                    float2 seedPt  = lattice + JitterHash(lattice);
                    minDist = min(minDist, DistanceSquared(pos - seedPt));
                }
            return 3.0 * exp(-4.0 * abs(2.5 * minDist - 1.0));
        }

        // three-band fractal Worley with time offset
        float FractalWorley(float2 uv, float t)
        {
            float a = WorleyF1(uv * 5.0  + 0.05 * t);
            float b = WorleyF1(uv * 50.0 + 0.12 - 0.1 * t);
            float c = WorleyF1(uv * -10.0 + 0.03 * t);
            float mix = a * sqrt(b) * sqrt(sqrt(c));
            return sqrt(sqrt(sqrt(mix)));
        }
        float2 EyeCenterUV()
        {
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
            return ndc * 0.5 + 0.5;                    // 0..1
        }

        //Fragment 
        float4 Frag(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            float2 uv    = i.texcoord;                             // use Blit-provided UV
            float  t     = _Time.y * FlowVelocity;                 // animated time
            float2 screen = _ScreenParams.xy;                      // screen resolution
            float2 c = EyeCenterUV();
            float2 uvFall = uv - c + 0.5;
            // compute fractal Worley in screen space
            float cellVal = FractalWorley(uv * screen / (10000.0 * NoiseScale), t);

            // radial falloff near edges
            cellVal *= exp(-DistanceSquared(abs(0.7 * uvFall - 1.0)));

            // mask modulation
            if (_FadeMultiplier > 0.0)
            {
                float maskSample;
            #if ALPHA_CHANNEL
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
            #else
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
            #endif
                float fade = smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskSample);
                BlendIntensity *= fade;
            }

            // form warp vector
            float2 warp = float2(cellVal, cellVal) * DisplaceAmount;
            // bias to keep center static
            float2 bias = float2(DisplaceAmount, DisplaceAmount) / screen;
            uv += (warp - bias) * BlendIntensity;
            uv.y -= DisplaceAmount * 0.25;                        // downward drift

            // sample from Blit texture
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        }
    ENDHLSL

    SubShader
    {
        Name "#FluidWorleyDisplacement#"
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
                #pragma vertex Vert     // provided by Blit.hlsl
                #pragma fragment Frag
            ENDHLSL
        }
    }
}