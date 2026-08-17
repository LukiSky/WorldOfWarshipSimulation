Shader "Naval/Particle"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _SrcBlend ("Src", Float) = 5      // SrcAlpha
        _DstBlend ("Dst", Float) = 10     // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                o.color = IN.color;
                return o;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                return half4(c.rgb * c.a, c.a);   // premultiplied-friendly for additive/alpha
            }
            ENDHLSL
        }
    }
    FallBack Off
}
