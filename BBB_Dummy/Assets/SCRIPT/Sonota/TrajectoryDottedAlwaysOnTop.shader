Shader "Custom/TrajectoryDottedAlwaysOnTop"
{
    Properties
    {
        _BaseColor ("Guide Color", Color) = (0.1, 0.9, 1, 1)
        _DashCount ("Dot Count", Range(2, 20)) = 10
        _DotWidth ("Dot Width", Range(0.2, 0.95)) = 0.42
        _DotHeight ("Dot Height", Range(0.2, 1.0)) = 0.85
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.2)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _DashCount;
                float _DotWidth;
                float _DotHeight;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 点線の繰り返し位置
                float cellX = frac(input.uv.x * _DashCount);

                // 点の中心からの距離
                float2 p;

                p.x = (cellX - 0.5) / _DotWidth;
                p.y = (input.uv.y - 0.5) / _DotHeight;

                // 楕円形の点を作る
                float distance = length(p);

                // 境界を滑らかにする
                float alpha = 1.0 - smoothstep(
                    1.0 - _EdgeSoftness,
                    1.0 + _EdgeSoftness,
                    distance
                );

                return half4(
                    _BaseColor.rgb,
                    _BaseColor.a * alpha
                );
            }

            ENDHLSL
        }
    }
}
