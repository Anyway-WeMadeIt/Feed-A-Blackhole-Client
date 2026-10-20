// 혜성 버프 배경 원: 혜성 중첩이 있는 동안 Breaker 링을 덮는 반투명 무지개 원. 적 위, 링 아래에 그린다.
// 무지개는 원을 가로지르는 선형 그라데이션이다. 색이 시간에 따라 흐르고(_RainbowSpeed), 그라데이션 방향이 계속 돈다(_RainbowTurnSpeed).
// 모든 길이 파라미터는 월드 단위다. 사각형 크기는 그리는 범위만 정하고 원 크기에 영향을 주지 않는다(BreakerView가 맞춘다).
Shader "BlackHole/Breaker Comet Aura"
{
    Properties
    {
        _Radius ("Radius", Float) = 1.8
        _Alpha ("Alpha", Range(0, 1)) = 0.55
        [Header(Rainbow)]
        _RainbowSpeed ("Flow Speed (cycles/s)", Float) = 0.5
        _RainbowTurnSpeed ("Direction Turn Speed (turns/s)", Float) = 0.1
        _RainbowSpan ("Hue Span Across Circle", Float) = 1
        _RainbowSaturation ("Saturation", Range(0, 1)) = 0.55
        _RainbowValue ("Value", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        // LightMode 태그가 없는 패스(SRPDefaultUnlit)는 2D 렌더러와 기본 렌더러 모두 그린다.
        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Radius;
                half _Alpha;
                float _RainbowSpeed;
                float _RainbowTurnSpeed;
                float _RainbowSpan;
                half _RainbowSaturation;
                half _RainbowValue;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // 원 중심(링 중심)에서 이 점까지의 월드 좌표 차이.
                float2 offset : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.offset = positionWS.xy - centerWS.xy;
                return output;
            }

            half3 HueToRgb(float hue)
            {
                half3 rgb = saturate(abs(frac(hue + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
                return lerp(half3(1, 1, 1), rgb, _RainbowSaturation) * _RainbowValue;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.offset;
                // 경계는 선명하게 두되 계단만 없앤다(픽셀 하나 폭).
                float aa = max(fwidth(p.x), 1e-5);
                float coverage = 1 - smoothstep(_Radius - aa, _Radius + aa, length(p));

                float turn = _Time.y * _RainbowTurnSpeed * TWO_PI;
                float2 direction = float2(cos(turn), sin(turn));
                float across = dot(p, direction) / (2 * _Radius);
                half3 color = HueToRgb(across * _RainbowSpan + _Time.y * _RainbowSpeed);

                return half4(color, coverage * _Alpha);
            }
            ENDHLSL
        }
    }
}
