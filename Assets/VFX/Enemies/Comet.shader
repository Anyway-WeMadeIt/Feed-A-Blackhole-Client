// 혜성(픽업 적): 흐르는 무지개 원 본체와, 블랙홀을 중심으로 한 원 궤도를 따라 공전 방향 뒤쪽으로 남는 무지개 궤적(호).
// 사각형은 혜성(본체 중심)에 놓이고, 궤도 중심(블랙홀)의 위치는 _ToCenter(본체에서 블랙홀로 가는 월드 벡터)로 받는다.
// 궤적은 궤도 반지름 위의 호이며 길이(_TailLength, 월드 단위)만큼 뻗고, 끝으로 갈수록 가늘어지고(_TailTaper) 투명해진다.
// _Direction은 공전 방향이다(1 = 반시계, -1 = 시계). 궤적은 늘 그 반대쪽(뒤)에 남는다.
// 모든 길이 파라미터는 월드 단위다. 사각형 크기는 그리는 범위만 정한다(EnemyView가 맞춘다).
Shader "BlackHole/Comet"
{
    Properties
    {
        _HeadRadius ("Head Radius", Float) = 0.3
        _TailLength ("Tail Length", Float) = 1.5
        _ToCenter ("To Center (xy)", Vector) = (-3, 0, 0, 0)
        _Direction ("Direction (1 = CCW, -1 = CW)", Float) = 1
        [Header(Tail)]
        _TailTaper ("Tail End Width Ratio", Range(0, 1)) = 0.2
        _TailAlpha ("Tail Alpha", Range(0, 1)) = 0.8
        [Header(Rainbow)]
        _GradientAngle ("Head Gradient Angle (radians)", Float) = 0.7853982
        _RainbowSpeed ("Flow Speed (cycles/s)", Float) = 0.5
        _RainbowSpan ("Hue Span Across Head And Tail", Float) = 1
        _RainbowSaturation ("Saturation", Range(0, 1)) = 0.75
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
                float _HeadRadius;
                float _TailLength;
                float4 _ToCenter;
                float _Direction;
                half _TailTaper;
                half _TailAlpha;
                float _GradientAngle;
                float _RainbowSpeed;
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
                // 본체 중심에서 이 점까지의 월드 좌표 차이.
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
                // 픽셀 하나의 월드 폭. 줌·해상도와 무관하게 가장자리를 한 픽셀로 부드럽게 한다.
                float aa = max(fwidth(p.x), 1e-5);
                float flow = _Time.y * _RainbowSpeed;

                // 본체: 구체를 가로지르는 무지개. 그라데이션 방향(_GradientAngle)은 EnemyView가 돌린다(자전).
                float headCoverage = 1 - smoothstep(_HeadRadius - aa, _HeadRadius + aa, length(p));
                float across = dot(p, float2(cos(_GradientAngle), sin(_GradientAngle))) / (2 * _HeadRadius);
                half3 headColor = HueToRgb(across * _RainbowSpan + flow);

                // 궤적: 궤도 반지름 위에서, 본체의 각도부터 뒤쪽으로 tailAngle까지.
                float tailCoverage = 0;
                half3 tailColor = headColor;
                float2 toCenter = _ToCenter.xy;
                float orbit = length(toCenter);

                if (orbit > 1e-4)
                {
                    float2 q = p - toCenter;
                    float rho = length(q);
                    float2 fromCenterToHead = -toCenter / orbit;
                    float2 pixelDirection = q / max(rho, 1e-5);
                    // 본체에서 이 점까지의 부호 있는 각도. 공전 방향 앞쪽이 양수가 되게 방향을 곱한다.
                    float ahead = atan2(fromCenterToHead.x * pixelDirection.y - fromCenterToHead.y * pixelDirection.x,
                                        dot(fromCenterToHead, pixelDirection)) * _Direction;
                    float tailAngle = min(_TailLength / orbit, 3.0);
                    // 0 = 본체 자리, 1 = 궤적 끝.
                    float t = -ahead / tailAngle;

                    if (t > 0 && t < 1)
                    {
                        float halfWidth = _HeadRadius * lerp(1, _TailTaper, t);
                        tailCoverage = (1 - smoothstep(halfWidth - aa, halfWidth + aa, abs(rho - orbit))) * (1 - t) * _TailAlpha;
                        tailColor = HueToRgb(t * _RainbowSpan + flow);
                    }
                }

                float alpha = headCoverage + tailCoverage * (1 - headCoverage);
                half3 color = (headColor * headCoverage + tailColor * tailCoverage * (1 - headCoverage)) / max(alpha, 1e-5);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
