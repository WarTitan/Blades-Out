Shader "DistortionsPro_20X/TiledDriftDistortion"
{
    Properties
    {
        _BlitTexture("Texture", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
    }
    HLSLINCLUDE

        // includes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // The Blit.hlsl file provides the vertex shader (Vert), the input structure (Attributes), and the output structure (Varyings)
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // textures
        TEXTURE2D(_Mask);
        SAMPLER(sampler_Mask);

        // uniforms
        float _FadeMultiplier;
        float MaskThreshold;

        float TileAmount = 12.0;
        float TileSize = 120.0;
        float DriftSpeed;
        float2 EyeCenterUV()
{
    // Optical center of the current eye (projects forward (0,0,-1) into UV)
    float4 clip = mul(UNITY_MATRIX_P, float4(0,0,-1,1));
    float2 ndc  = clip.xy / max(clip.w, 1e-6);  // -1..1
    return ndc * 0.5 + 0.5;                      // 0..1
}
float4 Frag(Varyings IN) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    // Per-eye base UV (keep this for sampling/mask)
    float2 baseUV = IN.texcoord;

    // Eye-space coordinates centered at current eye
    float2 c    = EyeCenterUV();
    float2 eUV  = baseUV - c;                    // [-0.5..0.5] around eye center
    float2 ePx  = eUV * _ScreenParams.xy;        // eye-space pixels

    // Strength, optionally modulated by mask
    float amount = TileAmount;
    if (_FadeMultiplier > 0.0)
    {
        float maskVal;
    #if ALPHA_CHANNEL
        maskVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).a;
    #else
        maskVal = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, baseUV).r;
    #endif
        amount *= smoothstep(MaskThreshold - 0.05, MaskThreshold + 0.05, maskVal);
    }
    amount *= 30.0;

    // Tile-local coords in EYE PIXELS (phase-locked per eye)
    float  ts        = max(TileSize, 1e-3);
    float2 tileLocal = frac(ePx / ts);           // 0..1 within a tile
    float2 tileRotDir = step(0.5, tileLocal);

    // Preserve original rotation logic (as in your shader)
    float2 centreOff = tileLocal - (0.5 - 1.0) * 2.0; // == tileLocal + 1.0 (kept intentionally)
    float  radius    = length(centreOff);
    float  rotDir    = step(0.3, radius) - 2.0 * (1.0 - step(0.4, radius));
    tileRotDir *= rotDir;

    // Time-varying drift (unitless), apply in EYE PIXELS
    float2 drift = float2(1.1 * cos(2.0 * _Time.y * DriftSpeed),
                          sin(2.0 * _Time.y * DriftSpeed));

    // Displace in eye-pixel space, then convert back to UV
    float2 ePxDisplaced = ePx + amount * tileRotDir * drift;
    float2 displacedUV  = c + ePxDisplaced / _ScreenParams.xy;
    displacedUV = clamp(displacedUV, 0.0, 1.0);

    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, displacedUV);
}

    ENDHLSL

    SubShader
    {
        Name "#TiledDriftDistortion#"
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
