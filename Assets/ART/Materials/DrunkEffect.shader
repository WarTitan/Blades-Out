// FILE: DrunkEffect.shader
// Shader for the fullscreen "drunk" look (Built-in RP).
// Similar to the reference: wavy UV warp + multi-tap blur + mild RGB split.

Shader "Hidden/DrunkEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Range(0,1)) = 0
        _Warp ("Warp", Range(0,0.1)) = 0.05
        _Offset ("Offset", Range(0,0.02)) = 0.01
        _Wave ("Wave", Range(0,5)) = 2.0
        _Speed ("Speed", Range(0,5)) = 1.0
        _BlurMix ("Blur Mix", Range(0,1)) = 0.6
        _Chromatic ("Chromatic", Range(0,2)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _Intensity;
            float _Warp;
            float _Offset;
            float _Wave;
            float _Speed;
            float _BlurMix;
            float _Chromatic;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y * _Speed;

                // Sinusoidal UV warp (scaled by intensity)
                float2 wuv = uv;
                wuv.x += cos(uv.y * _Wave + t) * _Warp * _Intensity;
                wuv.y += sin(uv.x * _Wave + t) * _Warp * _Intensity;

                // Small animated offset for blur / chromatic taps
                float offs = sin(t * 0.5) * _Offset * _Intensity;
                float2 offx = float2(offs, 0.0);
                float2 offy = float2(0.0, offs);

                // Base and blur (5 taps)
                float4 a = tex2D(_MainTex, wuv);
                float4 b = tex2D(_MainTex, wuv - offx);
                float4 c = tex2D(_MainTex, wuv + offx);
                float4 d = tex2D(_MainTex, wuv - offy);
                float4 e = tex2D(_MainTex, wuv + offy);
                float3 blur = ((a + b + c + d + e) * 0.2).rgb;

                // Chromatic split (R and B shifted opposite)
                float3 chroma;
                float4 rSamp = tex2D(_MainTex, wuv + offx * _Chromatic);
                float4 gSamp = tex2D(_MainTex, wuv);
                float4 bSamp = tex2D(_MainTex, wuv - offx * _Chromatic);
                chroma = float3(rSamp.r, gSamp.g, bSamp.b);

                // Blend: base -> blur -> chroma
                float3 baseRGB = a.rgb;
                float3 mixed = lerp(baseRGB, blur, saturate(_BlurMix));
                float3 outRGB = lerp(mixed, chroma, saturate(_Chromatic * 0.5)); // mild chroma by default

                return float4(outRGB, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
