// FILE: LsdEffect.shader
// Built-in RP
// Pass 0: main LSD effect with CENTER-CLEAR blend
// Pass 1: history blend with EDGE-MASKED trails (double buffered)

Shader "Hidden/LsdEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PrevTex ("Prev", 2D) = "black" {}
        _Intensity ("Intensity", Float) = 0
        _Hue ("Hue Angle (rad)", Float) = 0
        _Kaleido ("Kaleido Segments", Float) = 6
        _Swirl ("Swirl", Float) = 1
        _WarpAmp ("Warp Amp", Float) = 0.02
        _WarpHz ("Warp Hz", Float) = 0.7
        _ChromAb ("Chromatic Aberration", Float) = 1.0
        _Vignette ("Vignette", Float) = 0.35

        // Trails
        _TrailMix ("Trail Mix", Float) = 0.18
        _TrailMixRuntime ("Trail Mix Runtime", Float) = 0
        _TrailEdgeMask ("Trail Edge Mask 0..1", Float) = 0.8
        _TrailEdgeBoost ("Trail Edge Boost", Float) = 2.0
        _TrailEdgeSoftness ("Trail Edge Softness", Float) = 1.5
        _TrailVignetteMask ("Trail Border Mask 0..1", Float) = 0.4

        // NEW: center-clear blend
        _CenterClearRadius ("Center Clear Radius", Float) = 0.27
        _CenterClearFeather ("Center Clear Feather", Float) = 0.18
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // ---------- PASS 0 : MAIN EFFECT (with center-clear) ----------
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _PrevTex;
            float4 _MainTex_TexelSize;

            float _Intensity;
            float _Hue;
            float _Kaleido;
            float _Swirl;
            float _WarpAmp;
            float _WarpHz;
            float _ChromAb;
            float _Vignette;

            float _CenterClearRadius;
            float _CenterClearFeather;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert(appdata v){ v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float3 hueRotate(float3 c, float a)
            {
                float3 yiq;
                yiq.x = dot(c, float3(0.299, 0.587, 0.114));
                yiq.y = dot(c, float3(0.595716, -0.274453, -0.321263));
                yiq.z = dot(c, float3(0.211456, -0.522591, 0.311135));
                float ca = cos(a), sa = sin(a);
                float I = yiq.y * ca - yiq.z * sa;
                float Q = yiq.y * sa + yiq.z * ca;
                float3 rgb;
                rgb.r = yiq.x + 0.9563 * I + 0.6210 * Q;
                rgb.g = yiq.x - 0.2721 * I - 0.6474 * Q;
                rgb.b = yiq.x - 1.1070 * I + 1.7046 * Q;
                return saturate(rgb);
            }

            float2 kaleidoUV(float2 uv, float segs, float swirl, float time)
            {
                float2 p = uv - 0.5;
                p.x *= _ScreenParams.x / _ScreenParams.y;

                float r = length(p) + 1e-6;
                float a = atan2(p.y, p.x);

                float s = max(1.0, floor(segs + 0.5));
                float phi = UNITY_PI * 2.0 / s;
                a = fmod(a, phi);
                a = abs(a - phi * 0.5);

                a += swirl * r;

                float2 q = float2(cos(a), sin(a)) * r;

                float w = _WarpAmp * (0.7 + 0.3 * sin(time * _WarpHz * 6.2831853));
                q += w * float2(sin(q.y * 6.0 + time * 2.3), cos(q.x * 6.0 - time * 1.7));

                q.x *= _ScreenParams.y / _ScreenParams.x;
                q += 0.5;
                return q;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y;

                // Base (unaltered) sample for center clarity
                float3 baseC = tex2D(_MainTex, i.uv).rgb;

                // Effect path
                float2 uvK = kaleidoUV(i.uv, _Kaleido, _Swirl * _Intensity, time);

                float2 dir = uvK - 0.5;
                float len = max(1e-4, length(dir));
                dir /= len;

                float px = _MainTex_TexelSize.x;
                float2 off = dir * (_ChromAb * px);

                float3 eff;
                eff.r = tex2D(_MainTex, uvK + off).r;
                eff.g = tex2D(_MainTex, uvK).g;
                eff.b = tex2D(_MainTex, uvK - off).b;

                eff = hueRotate(eff, _Hue);

                // CENTER-CLEAR radial mask (aspect-corrected)
                float2 p = i.uv - 0.5;
                p.x *= _ScreenParams.x / _ScreenParams.y;
                float r = length(p);
                float m = smoothstep(_CenterClearRadius, _CenterClearRadius + max(1e-5, _CenterClearFeather), r);
                float3 mixed = lerp(baseC, eff, m);

                // Vignette (applied after blend)
                float vig = 1.0 - saturate(dot(p, p));
                mixed *= lerp(1.0, vig, saturate(_Vignette));

                return float4(mixed, 1.0);
            }
            ENDCG
        }

        // ---------- PASS 1 : HISTORY BLEND (EDGE-MASKED) ----------
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragTrail
            #include "UnityCG.cginc"

            sampler2D _MainTex;      // current processed work (downscaled)
            sampler2D _PrevTex;      // history (downscaled)
            float4 _MainTex_TexelSize;

            float _TrailMixRuntime;
            float _TrailEdgeMask;
            float _TrailEdgeBoost;
            float _TrailEdgeSoftness;
            float _TrailVignetteMask;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert(appdata v){ v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            fixed4 fragTrail(v2f i):SV_Target
            {
                float2 uv = i.uv;
                float2 dx = float2(_MainTex_TexelSize.x, 0.0);
                float2 dy = float2(0.0, _MainTex_TexelSize.y);

                float3 c00 = tex2D(_MainTex, uv - dx - dy).rgb;
                float3 c10 = tex2D(_MainTex, uv      - dy).rgb;
                float3 c20 = tex2D(_MainTex, uv + dx - dy).rgb;
                float3 c01 = tex2D(_MainTex, uv - dx     ).rgb;
                float3 c11 = tex2D(_MainTex, uv          ).rgb;
                float3 c21 = tex2D(_MainTex, uv + dx     ).rgb;
                float3 c02 = tex2D(_MainTex, uv - dx + dy).rgb;
                float3 c12 = tex2D(_MainTex, uv      + dy).rgb;
                float3 c22 = tex2D(_MainTex, uv + dx + dy).rgb;

                float g00 = luma(c00), g10 = luma(c10), g20 = luma(c20);
                float g01 = luma(c01), g11 = luma(c11), g21 = luma(c21);
                float g02 = luma(c02), g12 = luma(c12), g22 = luma(c22);

                float gx = (-1.0*g00 + 1.0*g20) + (-2.0*g01 + 2.0*g21) + (-1.0*g02 + 1.0*g22);
                float gy = (-1.0*g00 - 2.0*g10 - 1.0*g20) + (1.0*g02 + 2.0*g12 + 1.0*g22);
                float edge = sqrt(gx*gx + gy*gy);

                float edgeMask = pow(saturate(edge * _TrailEdgeBoost), max(0.0001, _TrailEdgeSoftness));

                float2 p = uv - 0.5;
                float vig = 1.0 - saturate(dot(p, p) * 2.0);
                float border = 1.0 - vig;
                float borderMask = lerp(1.0, border, saturate(_TrailVignetteMask));

                float mixBase = saturate(_TrailMixRuntime);
                float mixEdge = lerp(1.0, edgeMask, saturate(_TrailEdgeMask));
                float mixFinal = saturate(mixBase * mixEdge * borderMask);

                float3 cur  = c11;
                float3 prev = tex2D(_PrevTex, uv).rgb;
                float3 outc = lerp(prev, cur, mixFinal);
                return float4(outc, 1.0);
            }
            ENDCG
        }
    }
}
