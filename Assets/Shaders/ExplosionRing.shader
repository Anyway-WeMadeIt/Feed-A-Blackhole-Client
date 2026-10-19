// 폭발 링: 폭발 자리를 중심으로 퍼지는 충격파. 스프라이트 없이 ring mask + glow + flash로 그린다(Breaker Ring과 같은 방식).
// - Ring: 반지름 _Radius, 굵기 _Thickness의 띠.
// - Glow: 띠 가장자리에서 멀어질수록 옅어지는 번짐. _GlowWidth만큼 멀어지면 약 37%, 4배면 약 2%.
// - Flash: 링 안쪽을 채우는 빛. 중심이 가장 밝고 링에 가까울수록 옅어진다.
// 모든 길이 파라미터는 월드 단위다. 그래서 반지름이 커져도 선 굵기는 그대로다.
// 사각형 메시(quad)에 그린다. 메시는 가장 큰 반지름 + 굵기 + 번짐이 들어갈 만큼 커야 한다(ExplosionRings가 맞춘다).
// 가산 혼합: 화면 색에 더한다 — 링과 섬광이 빛처럼 밝게 빛나고, 겹친 폭발은 더 밝아진다. 알파(_Fade)는 전체 세기로 쓴다.
// (한때 알파 덮기로 바꿨다가 폭발이 탁해져 되돌렸다. 배경은 어두운 우주라 가산이 맞는다.)
Shader "BlackHole/Explosion Ring"
{
    Properties
    {
        _Radius ("Radius", Float) = 1
        _Thickness ("Thickness", Float) = 0.15
        _Color ("Color", Color) = (1, 0.55, 0.2, 1)
        _GlowWidth ("Glow Width", Float) = 0.3
        _GlowStrength ("Glow Strength", Range(0, 1)) = 0.5
        _FlashColor ("Flash Color", Color) = (1, 0.95, 0.8, 1)
        _Flash ("Flash", Range(0, 1)) = 0
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

        Blend SrcAlpha One
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
                half4 _Color;
                float _GlowWidth;
                half _GlowStrength;
                half4 _FlashColor;
                half _Flash;
                half _Fade;
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
                // 링 중심선에서 떨어진 거리.
                float d = abs(r - _Radius);

                // Ring mask: Breaker Ring과 같은 식.
                float halfThickness = _Thickness * 0.5;
                float ring = 1 - smoothstep(halfThickness - aa, halfThickness + aa, d);

                // Glow: 띠 안은 그대로, 띠 밖은 거리에 따라 지수로 옅어진다.
                float glow = exp(-max(d - halfThickness, 0) / max(_GlowWidth, 1e-4)) * _GlowStrength;

                // Flash: 중심 1, 링 위 0, 링 밖도 0(음수는 saturate가 0으로 자른다).
                float flash = saturate(1 - r / max(_Radius, 1e-4)) * _Flash;

                half ringAmount = saturate(ring + glow) * _Color.a;
                half flashAmount = flash * _FlashColor.a;
                half3 color = _Color.rgb * ringAmount + _FlashColor.rgb * flashAmount;

                // Blend SrcAlpha One: 화면 += color × _Fade.
                return half4(color, _Fade);
            }
            ENDHLSL
        }
    }
}
