// 화면 전환 덮개(블랙홀 아이리스): 화면 가운데 반지름 _Radius의 원 바깥을 _Color로 덮고, 원 둘레에 빛을 더한다.
// 전체 화면 UI Image에 쓴다. UI(Screen Space - Overlay)는 URP 경로가 아니라 UGUI가 그리므로 UI/Default와 같은 틀(CG, UnityCG)을 쓴다.
// 길이 파라미터는 캔버스 단위다(CanvasScaler가 맞춘 단위라 해상도가 달라도 모양이 같다). 원의 중심은 캔버스 가운데 + _Center.
// 미리 곱한 알파로 섞는다(Blend One OneMinusSrcAlpha): 덮개는 뒤를 가리고, 둘레 빛은 그 위에 더해진다.
Shader "BlackHole/UI Screen Iris"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Cover Color", Color) = (0, 0, 0, 1)
        _Radius ("Radius", Float) = 0
        _Center ("Center", Vector) = (0, 0, 0, 0)
        _RimColor ("Rim Color", Color) = (1, 0.62, 0.3, 1)
        _RimWidth ("Rim Width", Float) = 28
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.8

        // UGUI가 찾는 속성(마스크 아래에 두지 않지만 없으면 경고가 난다).
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Radius;
            float4 _Center;
            fixed4 _RimColor;
            float _RimWidth;
            half _RimStrength;

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                // 원 중심에서 이 점까지(캔버스 단위).
                float2 offset : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.offset = input.vertex.xy - _Center.xy;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float d = length(input.offset);
                // 픽셀 하나의 폭. 원 가장자리를 한 픽셀로 부드럽게 한다.
                float aa = max(fwidth(d), 1e-4);

                // 원 밖 1(덮음), 원 안 0(보임).
                float cover = smoothstep(_Radius - aa, _Radius + aa, d);
                // 원 둘레에서 멀어질수록 옅어지는 빛(안팎 모두).
                float rim = exp(-abs(d - _Radius) / max(_RimWidth, 1e-4)) * _RimStrength;

                fixed3 color = _Color.rgb * _Color.a * cover + _RimColor.rgb * _RimColor.a * rim;
                return fixed4(color, _Color.a * cover);
            }
            ENDCG
        }
    }
}
