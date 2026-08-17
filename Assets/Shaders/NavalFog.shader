Shader "Naval/FogOfWar"
{
    Properties
    {
        _VisionTex ("Vision (r: current, g: explored)", 2D) = "black" {}
        _WorldMin ("World Min", Vector) = (-2000,-2000,0,0)
        _WorldSize ("World Size", Vector) = (4000,4000,0,0)
        _FogColor ("Fog Color", Color) = (0.04, 0.07, 0.12, 1)
        _UnseenAlpha ("Unseen Alpha", Range(0,1)) = 0.66
        _ExploredAlpha ("Explored Alpha", Range(0,1)) = 0.34
        _WeatherAlpha ("Weather Alpha", Range(0,1)) = 0
        _WeatherColor ("Weather Color", Color) = (0.55,0.6,0.66,1)
        _Enabled ("Enabled", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_VisionTex); SAMPLER(sampler_VisionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _WorldMin, _WorldSize, _FogColor, _WeatherColor;
                float _UnseenAlpha, _ExploredAlpha, _WeatherAlpha, _Enabled;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(IN.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(wp);
                o.world = wp.xy;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i+float2(1,0)), f.x),
                            lerp(hash21(i+float2(0,1)), hash21(i+float2(1,1)), f.x), f.y);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = (IN.world - _WorldMin.xy) / _WorldSize.xy;
                float2 v = SAMPLE_TEXTURE2D(_VisionTex, sampler_VisionTex, saturate(uv)).rg;

                float visible = saturate(v.r);
                float explored = saturate(max(v.g, v.r));

                float a = lerp(_UnseenAlpha, _ExploredAlpha, explored);
                a = lerp(a, 0.0, visible);
                a *= _Enabled;

                // drifting weather veil on top of the tactical fog
                float drift = vnoise(IN.world * 0.004 + float2(_Time.y * 0.02, _Time.y * 0.013));
                float w = _WeatherAlpha * (0.75 + drift * 0.5);

                half3 col = lerp(_FogColor.rgb, _WeatherColor.rgb, saturate(w / max(a + w, 0.0001)));
                // never go fully opaque: the sea should always read through the haze
                float outA = min(a + w, 0.88);
                return half4(col, outA);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
