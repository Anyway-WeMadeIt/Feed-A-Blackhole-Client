// URP Full Screen Pass Renderer Feature용 충격파 왜곡 셰이더
// Renderer Feature 설정: Requirements = Color, Injection Point = Before Rendering Post Processing
Shader "Hidden/BlackHoleShockwave"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Shockwave"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define MAX_WAVES 6

            // ShockwaveController.cs에서 전역으로 넣어주는 값들
            float4 _ShockWaves[MAX_WAVES]; // xy: 중심(뷰포트 0~1), z: 반지름(화면 높이 기준), w: 세기(0~1)
            float  _ShockWidths[MAX_WAVES]; // 링 두께(화면 높이 기준)
            float  _ShockAmp;               // 왜곡량(UV 단위, 0.02~0.05 추천)
            float  _ShockGlow;              // 링 빛 세기
            float  _ShockChroma;            // 색수차 정도 (0 = 끔)

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                float2 offset = 0;
                float glow = 0;

                [unroll]
                for (int i = 0; i < MAX_WAVES; i++)
                {
                    float4 w = _ShockWaves[i];

                    // 화면 비율 보정해서 원이 찌그러지지 않게
                    float2 d = uv - w.xy;
                    d.x *= aspect;
                    float dist = length(d);

                    // 링 중심에서의 정규화 거리 (-1~1 근처가 링)
                    float x = (dist - w.z) / max(_ShockWidths[i], 1e-4);

                    // 가우시안 미분: 링 바깥은 밀고 안쪽은 당겨서 렌즈처럼 휘게
                    float g = -x * exp(-x * x * 3.0);

                    float2 dir = d / max(dist, 1e-4);
                    dir.x /= aspect; // 다시 UV 공간으로

                    offset += dir * g * w.w * _ShockAmp;
                    glow   += exp(-x * x * 6.0) * w.w;
                }

                // 채널마다 왜곡량을 조금씩 달리해서 색수차
                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset * (1.0 + _ShockChroma)).r;
                half g2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset * (1.0 - _ShockChroma)).b;

                half3 col = half3(r, g2, b);
                col += half3(0.55, 0.7, 1.0) * glow * _ShockGlow;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
