// 번개 한 줄기: From에서 To까지 꺾이며 이어지는 선. 스프라이트 없이 "꺾인 선까지의 거리"로 그린다(Explosion Ring과 같은 방식).
// - 꺾임: 선을 따라 일정 간격(_Segments칸)마다 무작위 높이를 정하고 그 사이를 직선으로 잇는다(두 겹). 양 끝은 0이라 끝점에 붙는다.
// - Core: 꺾인 선에서 굵기 _Thickness 안의 밝은 심. Glow: 심 밖으로 옅어지는 번짐.
// - _Seed가 바뀌면 꺾임 모양이 바뀐다. 부르는 쪽(LightningBolts)이 짧은 간격으로 바꿔 깜빡이게 한다.
// 사각형 메시(quad)를 선 방향으로 돌려 놓고 그린다. 길이 파라미터는 모두 월드 단위다.
// 알파 혼합: 심을 번짐 위에 덮어(over) 화면 위에 그린다. 밝은 배경에서도 색이 그대로 보인다(Breaker 셰이더와 같은 혼합).
// 알파(_Fade)는 전체 세기로 쓴다.
Shader "BlackHole/Lightning Bolt"
{
    Properties
    {
        _Length ("Length", Float) = 1
        _Segments ("Segments", Float) = 3
        _Amplitude ("Amplitude", Float) = 0.18
        _Seed ("Seed", Float) = 0
        _Thickness ("Thickness", Float) = 0.1
        _Color ("Color", Color) = (0.55, 0.8, 1, 1)
        _CoreColor ("Core Color", Color) = (0.95, 0.98, 1, 1)
        _GlowWidth ("Glow Width", Float) = 0.18
        _GlowStrength ("Glow Strength", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
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
                float _Length;
                float _Segments;
                float _Amplitude;
                float _Seed;
                float _Thickness;
                half4 _Color;
                half4 _CoreColor;
                float _GlowWidth;
                half _GlowStrength;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // 선 중심에서 이 점까지를 선 방향(x)과 그 수직(y)으로 잰 월드 거리.
                float2 local : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float2 along = normalize(TransformObjectToWorldDir(float3(1, 0, 0)).xy);
                float2 offset = positionWS.xy - centerWS.xy;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.local = float2(dot(offset, along), dot(offset, float2(-along.y, along.x)));
                return output;
            }

            // 정수 자리 n의 무작위 높이(-1 ~ 1). _Seed가 같으면 같은 값이다.
            float Hash(float n)
            {
                return frac(sin(n * 12.9898 + _Seed * 78.233) * 43758.5453) * 2 - 1;
            }

            // 꺾인 선 노이즈: 정수 자리마다 무작위 높이, 사이는 직선(부드럽게 잇지 않아서 번개처럼 꺾인다).
            float Kinks(float x)
            {
                float i = floor(x);
                return lerp(Hash(i), Hash(i + 1), frac(x));
            }

            // 선 방향 위치(월드, 중심 0)에서 번개의 수직 변위(월드).
            float Displace(float x)
            {
                float u = saturate(x / max(_Length, 1e-4) + 0.5);
                float k = u * _Segments;
                float n = Kinks(k) * 0.7 + Kinks(k * 2.3 + 17) * 0.3;
                // 양 끝 0, 가운데 1. 그래서 번개가 두 끝점에 붙는다.
                float taper = 4 * u * (1 - u);
                return n * taper * _Amplitude;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float halfLength = _Length * 0.5;
                // 끝 너머는 끝점까지의 거리로 잰다(둥근 끝).
                float x = clamp(input.local.x, -halfLength, halfLength);
                float d = Displace(x);

                // 수직 거리는 선이 기울수록 실제 거리보다 커서 가파른 곳이 가늘어 보인다. 기울기로 나눠 보정한다.
                const float e = 0.01;
                float slope = (Displace(x + e) - Displace(x - e)) / (2 * e);
                float gap = length(float2(input.local.x - x, (input.local.y - d) / sqrt(1 + slope * slope)));

                // 픽셀 하나의 월드 폭. 줌·해상도와 무관하게 가장자리를 한 픽셀로 부드럽게 한다.
                float aa = max(fwidth(gap), 1e-5);
                float halfThickness = _Thickness * 0.5;
                float core = 1 - smoothstep(halfThickness - aa, halfThickness + aa, gap);
                float glow = exp(-max(gap - halfThickness, 0) / max(_GlowWidth, 1e-4)) * _GlowStrength;

                half glowAmount = saturate(glow) * _Color.a;
                half coreAmount = core * _CoreColor.a;
                // 심을 번짐 위에 덮는다(over). 색은 덮은 양으로 나눠 알파와 따로 둔다.
                half alpha = coreAmount + glowAmount * (1 - coreAmount);
                half3 color = (_CoreColor.rgb * coreAmount + _Color.rgb * glowAmount * (1 - coreAmount)) / max(alpha, 1e-4);

                // Blend SrcAlpha OneMinusSrcAlpha: 화면 = lerp(화면, color, alpha × _Fade).
                return half4(color, alpha * _Fade);
            }
            ENDHLSL
        }
    }
}
