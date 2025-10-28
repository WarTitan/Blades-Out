// FILE: SpeedEffect.shader
// Built-in RP fullscreen pass for "Speed" (stimulant) look.
// Subtle sharpen + saturation + contrast, all driven by _Intensity and _Pulse.
//
// Create a Material from this shader and assign it to SpeedEffect.speedMaterialTemplate.

Shader "Hidden/SpeedEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 0
        _Pulse ("Pulse", Float) = 0
        _Sharp ("Sharpen Amount", Float) = 0.7
        _Sat ("Saturation Boost", Float) = 0.25
        _Contrast ("Contrast Boost", Float) = 0.25
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // x=1/width, y=1/height
            float _Intensity;
            float _Pulse;
            float _Sharp;
            float _Sat;
            float _Contrast;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 toGray(float3 c)
            {
                float g = dot(c, float3(0.299, 0.587, 0.114));
                return float3(g, g, g);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Base sample
                float3 col = tex2D(_MainTex, uv).rgb;

                // Small blur for unsharp mask
                float2 dx = float2(_MainTex_TexelSize.x, 0.0);
                float2 dy = float2(0.0, _MainTex_TexelSize.y);
                float3 b =
                    tex2D(_MainTex, uv + dx).rgb +
                    tex2D(_MainTex, uv - dx).rgb +
                    tex2D(_MainTex, uv + dy).rgb +
                    tex2D(_MainTex, uv - dy).rgb;
                b *= 0.25;

                // Sharpen amount rises a bit with pulse
                float sharpAmt = max(0.0, _Sharp + _Pulse * 0.12) * _Intensity;
                float3 sharpCol = saturate(col + (col - b) * sharpAmt);

                // Saturation boost
                float3 gray = toGray(sharpCol);
                float satAmt = 1.0 + _Sat * _Intensity;
                float3 satCol = lerp(gray, sharpCol, satAmt);

                // Contrast boost around mid
                float contAmt = 1.0 + _Contrast * _Intensity;
                float3 contCol = (satCol - 0.5) * contAmt + 0.5;

                return float4(contCol, 1.0);
            }
            ENDCG
        }
    }
}
