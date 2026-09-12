Shader "Cashier/World/Birds"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
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
                // Original SkyBirds uses a 1280x720 top-left pixel coordinate system.
                float2 world = float2(input.uv.x * 1280, (input.uv.y - 1) * 720);
                float mask = 0;
                [unroll] for (int bird = 0; bird < 5; bird++)
                {
                    float seconds = _PresentationSeconds;
                    float x = frac(seconds * (.009 + bird * .0005) + bird * .17) * 1480 - 100;
                    float y = -155 - bird * 13 + sin(seconds * .7 + bird) * 5;
                    float2 p = (world - float2(x,y)) / 1.25;
                    float wing = abs(p.x);
                    float flap = sin(seconds * (6 + bird * .3) + bird * 2);
                    float wings = step(wing,7) * step(abs(p.y - wing * (.25 + .5 * flap)),1.3);
                    float body = step(wing,1.5) * step(abs(p.y + 1),2);
                    mask = max(mask,max(wings,body));
                }
                half3 gray = half3(.75,.75,.75);
                #ifndef UNITY_COLORSPACE_GAMMA
                gray = SRGBToLinear(gray);
                #endif
                return half4(gray,mask * input.color.a);
            }
            ENDHLSL
        }
    }
}
