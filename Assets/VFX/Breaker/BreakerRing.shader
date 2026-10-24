// Breaker 링: 조준점을 중심으로 한 점선 원. 스프라이트 없이 ring mask × dash mask로 그린다.
// 모든 길이 파라미터는 월드 단위다. 그래서 반지름이 커져도 선 굵기는 그대로다.
// 사각형 메시(quad)에 그린다. 메시는 화면용 반지름 + 굵기가 들어갈 만큼 커야 한다(BreakerView가 맞춘다).
// _Rotation은 바퀴 단위다(1 = 한 바퀴). 부르는 쪽이 [0, 1)로 감아 넘긴다.
Shader "BlackHole/Breaker Ring"
{
    Properties
    {
        _Radius ("Radius", Float) = 1.5
        _Thickness ("Thickness", Float) = 0.08
        _DashCount ("Dash Count", Float) = 12
        _DashRatio ("Dash Ratio", Range(0, 1)) = 0.6
        _Rotation ("Rotation (turns)", Float) = 0
        _Color ("Color", Color) = (1, 1, 1, 1)
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _Flash ("Flash", Range(0, 1)) = 0
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
                float _Thickness;
                float _DashCount;
                float _DashRatio;
                float _Rotation;
                half4 _Color;
                half4 _FlashColor;
                half _Flash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // 링 중심에서 이 점까지의 월드 좌표 차이.
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

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.offset);
                // 픽셀 하나의 월드 폭. 줌·해상도와 무관하게 가장자리를 한 픽셀로 부드럽게 한다.
                float aa = max(fwidth(r), 1e-5);

                // Ring mask: 반지름 _Radius, 굵기 _Thickness의 띠.
                float halfThickness = _Thickness * 0.5;
                float ring = 1 - smoothstep(halfThickness - aa, halfThickness + aa, abs(r - _Radius));

                // Dash mask: 각도를 [0, 1)로 펴고 _DashCount칸으로 나눈다. u는 칸 안에서 대시 중앙까지의 거리(0 ~ 0.5)다.
                // 가장자리 폭은 픽셀 폭을 칸 단위로 바꿔 쓴다. fwidth(t)는 atan2의 이음매에서 튀므로 쓰지 않는다.
                float t = atan2(input.offset.y, input.offset.x) * INV_TWO_PI + 0.5;
                float u = abs(frac((t + _Rotation) * _DashCount) - 0.5);
                float arcAA = aa * _DashCount / (TWO_PI * max(r, 1e-4));
                float halfDash = _DashRatio * 0.5;
                float dash = _DashRatio >= 1 ? 1 : 1 - smoothstep(halfDash - arcAA, halfDash + arcAA, u);

                half4 color = lerp(_Color, _FlashColor, _Flash);
                color.a *= ring * dash;
                return color;
            }
            ENDHLSL
        }
    }
}
