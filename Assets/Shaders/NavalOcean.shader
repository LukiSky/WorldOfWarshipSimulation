Shader "Naval/Ocean"
{
    Properties
    {
        _HeightTex ("Height (r: -1 water .. 1 land)", 2D) = "black" {}
        _WorldMin ("World Min", Vector) = (-2000,-2000,0,0)
        _WorldSize ("World Size", Vector) = (4000,4000,0,0)

        _DeepColor ("Deep", Color) = (0.043, 0.115, 0.215, 1)
        _MidColor ("Mid", Color) = (0.07, 0.225, 0.35, 1)
        _ShallowColor ("Shallow", Color) = (0.125, 0.42, 0.51, 1)
        _ShoalColor ("Shoal", Color) = (0.30, 0.66, 0.64, 1)
        _SandColor ("Sand", Color) = (0.68, 0.62, 0.44, 1)
        _LandColor ("Land", Color) = (0.24, 0.31, 0.22, 1)
        _RockColor ("Rock", Color) = (0.34, 0.33, 0.31, 1)

        _WaveDir ("Wave Dir", Vector) = (0.7, 0.7, 0, 0)
        _WaveScale ("Wave Scale", Float) = 0.018
        _WaveSpeed ("Wave Speed", Float) = 0.09
        _Choppiness ("Choppiness", Range(0,3)) = 1
        _FoamAmount ("Shore Foam", Range(0,2)) = 1
        _Detail ("Detail (fades fine noise when zoomed out)", Range(0,1)) = 1
        _Tint ("Weather Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-200" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend One Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_HeightTex);   SAMPLER(sampler_HeightTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _WorldMin, _WorldSize;
                float4 _DeepColor, _MidColor, _ShallowColor, _ShoalColor;
                float4 _SandColor, _LandColor, _RockColor, _Tint;
                float4 _WaveDir;
                float _WaveScale, _WaveSpeed, _Choppiness, _FoamAmount, _Detail;
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
                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }

            float fbm(float2 p)
            {
                float s = 0.0, a = 0.5;
                for (int i = 0; i < 4; i++) { s += vnoise(p) * a; p *= 2.07; a *= 0.5; }
                return s;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = (IN.world - _WorldMin.xy) / _WorldSize.xy;
                // height is stored biased into 0..1; decode back to -1 (abyss) .. 1 (peak)
                float h = SAMPLE_TEXTURE2D(_HeightTex, sampler_HeightTex, saturate(uv)).r * 2.0 - 1.0;

                float t = _Time.y;
                float2 dir = normalize(_WaveDir.xy + 1e-5);
                float2 wp = IN.world * _WaveScale;

                // two travelling swell layers + fine chop
                float swell = sin(dot(wp, dir) * 3.1 - t * _WaveSpeed * 6.0) * 0.5 + 0.5;
                float swell2 = sin(dot(wp, normalize(dir + float2(-0.55, 0.4))) * 5.3 - t * _WaveSpeed * 4.1) * 0.5 + 0.5;
                // fine chop is faded out when zoomed out, otherwise it aliases into speckle
                float chop = lerp(0.5, fbm(wp * 6.0 + dir * t * _WaveSpeed * 5.0), _Detail);
                float waves = (swell * 0.5 + swell2 * 0.3 + chop * 0.4) * _Choppiness;

                if (h <= 0.0)
                {
                    float depth = saturate(-h);                    // 0 shore .. 1 abyss
                    half3 col = lerp(_ShoalColor.rgb, _ShallowColor.rgb, saturate(depth * 6.0));
                    col = lerp(col, _MidColor.rgb, saturate((depth - 0.16) * 3.2));
                    col = lerp(col, _DeepColor.rgb, saturate((depth - 0.45) * 2.2));

                    // wave banding: a gentle swell shading, stronger over shallow water
                    float band = (waves - 0.5) * lerp(0.085, 0.028, saturate(depth * 3.0));
                    col += band * col * 3.0 + band * 0.25;

                    // crest sparkle - rare, only on the steepest part of the swell
                    float crest = smoothstep(0.86, 1.08, waves * 0.55 + chop * 0.6);
                    col += crest * 0.06 * _Choppiness * _Detail;

                    // shoreline foam - a wide animated band hugging the coast
                    float shore = 1.0 - saturate(-h / 0.05);
                    float foamNoise = lerp(0.5, fbm(IN.world * 0.09 + float2(0.0, t * 0.35)), _Detail);
                    float foam = saturate(shore * 1.15 - 0.25 + (foamNoise - 0.5) * 0.9);
                    foam *= smoothstep(0.0, 0.25, shore) * _FoamAmount;
                    col = lerp(col, half3(0.86, 0.94, 0.96), saturate(foam));

                    return half4(col * _Tint.rgb, 1);
                }
                else
                {
                    float land = saturate(h);
                    float n = fbm(IN.world * 0.05);
                    half3 col = lerp(_SandColor.rgb, _LandColor.rgb, smoothstep(0.02, 0.16, land));
                    col = lerp(col, _RockColor.rgb, smoothstep(0.28, 0.62, land + (n - 0.5) * 0.25));
                    col *= 0.85 + n * 0.3;
                    // damp beach ring
                    col = lerp(col * 0.8, col, smoothstep(0.0, 0.035, land));
                    return half4(col * _Tint.rgb, 1);
                }
            }
            ENDHLSL
        }
    }
    FallBack Off
}
