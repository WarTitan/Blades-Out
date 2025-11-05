Shader "DistortionsPro_20X/RecursiveVortexWarp"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
    }
    HLSLINCLUDE

        // Includes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // The Blit.hlsl file provides the vertex shader (Vert),
        // the input structure (Attributes), and the output structure (Varyings)
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Texture interfaces
        TEXTURE2D(_Mask);
        SAMPLER  (sampler_Mask);

        // parameters
        float _FadeMultiplier;       // enable mask-driven blend if > 0
        float maskThreshold;         // centre of smoothstep range
        float animTime;              // drives vortex evolution
        float iterationCount;        // depth of recursive loop
        float blendFactor;           // final mix amount
        float2 EyeCenterUV()
        {
            
            float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
            float2 ndc  = clip.xy / max(clip.w, 1e-6); // -1..1
            return ndc * 0.5 + 0.5;                    // -> UV 0..1 
        }
        // Fragment shader
        float4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

            // Use texture coordinate provided by Blit pipeline
            float2 uv = IN.texcoord;
            float2 c = EyeCenterUV();
            // Normalised coordinates centred on (0,0) and stretched slightly for dramatic swirl
            float2 centredCoord = (uv - c) * 1.5;
            centredCoord = centredCoord.yx * 1.5; // rotate 90° then scale for asymmetry

            // Recursive complex-plane iteration variable
            float2 accumCoord = float2(0.0, 0.0);

            // Phase term varies with time and iteration depth, creating inward-spiralling motion
            float phaseSteps = iterationCount;
            float phase = sin(animTime / phaseSteps) * phaseSteps;
            accumCoord = centredCoord * phase;

            // Iterate a simple z = sin(z^2 + c) style fractal (Julia-like)
            [loop]
            for (int idx = 0; idx < (int)iterationCount; ++idx)
            {
                float2 zSquared = float2(accumCoord.x * accumCoord.x - accumCoord.y * accumCoord.y,
                                         accumCoord.x * accumCoord.y);
                accumCoord = sin(zSquared + centredCoord);
            }

            // Sample original image and warped coordinate image
            float4 baseCol   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv); // replaced MainTex with BlitTexture
            float2 sampleUV = c + (accumCoord.yx * 0.5);   
            sampleUV = clamp(sampleUV, 0.0, 1.0);
            float4 vortexCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
            // Optional mask-controlled blend attenuation
            if (_FadeMultiplier > 0.0)
            {
                float maskSample;
            #if ALPHA_CHANNEL
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).a;
            #else
                maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
            #endif
                float fadeVal = smoothstep(maskThreshold - 0.05, maskThreshold + 0.05, maskSample);
                blendFactor *= fadeVal;
            }

            return lerp(baseCol, vortexCol, blendFactor);
        }

    ENDHLSL

    SubShader
    {
        Name "#RecursiveVortexWarp#"
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
