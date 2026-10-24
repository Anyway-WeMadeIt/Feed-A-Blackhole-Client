// Breaker 버프 구체: 링 바깥 궤도를 도는 작은 구체들. 버프 중첩 하나가 구체 하나다(달·혜성이 한 궤도를 나눠 쓴다).
// 구체는 궤도를 _OrbCount칸으로 균등하게 나눈 자리에 놓인다. 칸 0이 가장 먼저 받은 중첩이고, 공전 방향(시계방향)의 맨 앞이다.
// 칸의 종류는 _OrbKinds[칸](0 = 달, 1 = 혜성)이다. 표시는 64칸까지다.
// 모든 길이 파라미터는 월드 단위다. 사각형 크기는 그리는 범위만 정하고 구체 크기에 영향을 주지 않는다(BreakerView가 맞춘다).
// 픽셀마다 가장 가까운 칸과 그 양옆만 본다 — 구체 수와 관계없이 계산량이 같다.
Shader "BlackHole/Breaker Orbs"
{
    Properties
    {
        _OrbitRadius ("Orbit Radius", Float) = 1.8
        _OrbRadius ("Orb Radius", Float) = 0.1
        _OrbCount ("Orb Count", Float) = 0
        _Phase ("Phase (turns)", Float) = 0
        _MoonFill ("Moon Fill", Color) = (1, 1, 1, 1)
        _MoonOutline ("Moon Outline", Color) = (0.25, 0.27, 0.32, 1)
        _OutlineWidth ("Moon Outline Width", Float) = 0.025
        [Header(Comet Rainbow)]
        _RainbowSpeed ("Flow Speed (cycles/s)", Float) = 0.5
        _RainbowSpan ("Hue Span Across Orb", Float) = 1
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

            #define MAX_ORBS 64

            CBUFFER_START(UnityPerMaterial)
                float _OrbitRadius;
                float _OrbRadius;
                float _OrbCount;
                float _Phase;
                half4 _MoonFill;
                half4 _MoonOutline;
                float _OutlineWidth;
                float _RainbowSpeed;
                float _RainbowSpan;
                half _RainbowSaturation;
                half _RainbowValue;
            CBUFFER_END

            // 배열은 머티리얼 속성이 될 수 없다. BreakerView가 MaterialPropertyBlock으로 넣는다.
            float _OrbKinds[MAX_ORBS];

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // 궤도 중심(링 중심)에서 이 점까지의 월드 좌표 차이.
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

            // 칸 slot(정수, 음수 가능) 구체의 색과 덮는 정도(알파).
            half4 Orb(float2 p, float slot, int count, float aa)
            {
                int index = (int)(slot - count * floor(slot / count));
                float angle = (_Phase + slot / count) * TWO_PI;
                float2 local = p - _OrbitRadius * float2(cos(angle), sin(angle));
                float d = length(local);
                float coverage = 1 - smoothstep(_OrbRadius - aa, _OrbRadius + aa, d);
                half3 color;

                if (_OrbKinds[index] < 0.5)
                {
                    // 달: 흰 채움 + 어두운 테두리.
                    float inner = _OrbRadius - _OutlineWidth;
                    color = lerp(_MoonFill.rgb, _MoonOutline.rgb, smoothstep(inner - aa, inner + aa, d));
                }
                else
                {
                    // 혜성: 구체를 대각선으로 가로지르는 무지개가 시간에 따라 흐른다.
                    float across = dot(local, float2(0.70710678, 0.70710678)) / (2 * _OrbRadius);
                    color = HueToRgb(across * _RainbowSpan + _Time.y * _RainbowSpeed);
                }

                return half4(color, coverage);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                int count = (int)min(_OrbCount, MAX_ORBS);

                if (count < 1)
                    return half4(0, 0, 0, 0);

                float2 p = input.offset;
                // 픽셀 하나의 월드 폭. 줌·해상도와 무관하게 가장자리를 한 픽셀로 부드럽게 한다.
                float aa = max(fwidth(p.x), 1e-5);
                float turns = atan2(p.y, p.x) * INV_TWO_PI;
                float nearest = round((turns - _Phase) * count);

                // 구체가 칸 간격보다 크면 옆 칸 구체와 겹칠 수 있다. 가장 가까운 칸과 양옆 중 가장 많이 덮는 구체를 쓴다.
                half4 result = Orb(p, nearest, count, aa);
                half4 before = Orb(p, nearest - 1, count, aa);
                half4 after = Orb(p, nearest + 1, count, aa);

                if (before.a > result.a)
                    result = before;

                if (after.a > result.a)
                    result = after;

                return result;
            }
            ENDHLSL
        }
    }
}
