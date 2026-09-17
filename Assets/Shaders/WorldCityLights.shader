Shader "Cashier/World/CityLights"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _PresentationSeconds ("Pausable Seconds", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _PresentationSeconds;
            CBUFFER_END
            Varyings Vertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // 기존 UI CityLights의 창문 chroma mask를 유지하며 renderer alpha도 곱한다.
                half chroma = max(max(texel.r, texel.g), texel.b) - min(min(texel.r, texel.g), texel.b);
                texel.a *= step(0.18h, chroma);

                // 허공(할머니 머리 우측 위)에 잘못 찍혀 있던 붉은 사각형 및 노란 점 제거
                if (input.uv.x > 0.56h && input.uv.x < 0.58h && input.uv.y > 0.80h && input.uv.y < 0.88h)
                {
                    texel.a = 0.0h;
                }

                // 텍스처 자체에 실제로 칠해진 붉은색 항공 경고등 픽셀만 감지
                half redMask = step(texel.g * 1.35h, texel.r) * step(texel.b * 1.35h, texel.r) * step(0.35h, texel.r);

                // 12x8 구역별 엇박자 위상차 생성
                float blinkPhase = floor(input.uv.x * 12.0) * 1.37 + floor(input.uv.y * 8.0) * 0.91;

                // _PresentationSeconds가 설정되어 있으면 이를 우선 사용하고, 없으면 _Time.y를 사용
                float seconds = (_PresentationSeconds != 0.0) ? _PresentationSeconds : _Time.y;
                half warningBlink = (half)lerp(0.22, 1.0, step(0.08, sin(seconds * 3.2 + blinkPhase)));

                // 붉은 경고등에만 점멸 적용
                texel.a *= lerp(1.0h, warningBlink, redMask);

                // 좌측 남산타워 실제 첨탑 꼭대기 (X: 246, Y: 42, UV: 0.1471, 0.9554)에 붉은 항공 경고등 배치
                float2 texSize = _MainTex_TexelSize.zw > 0 ? _MainTex_TexelSize.zw : float2(1672.0, 941.0);
                float2 beaconPixel = (input.uv - float2(0.1471, 0.9554)) * texSize;
                float beaconCore = step(max(abs(beaconPixel.x), abs(beaconPixel.y)), 1.35);
                float beaconHalo = step(length(beaconPixel), 3.1) * 0.28;
                half beaconBlink = (half)lerp(0.22, 1.0, step(0.08, sin(seconds * 3.2)));
                half beaconAlpha = (half)saturate(beaconCore + beaconHalo) * beaconBlink;

                half3 beaconColor = half3(1.0, 0.045, 0.018);
                #ifndef UNITY_COLORSPACE_GAMMA
                beaconColor = SRGBToLinear(beaconColor);
                #endif

                texel.rgb = lerp(texel.rgb, beaconColor, saturate(beaconAlpha));
                texel.a = max(texel.a, beaconAlpha);

                return texel * input.color;
            }
            ENDHLSL
        }
    }
}
