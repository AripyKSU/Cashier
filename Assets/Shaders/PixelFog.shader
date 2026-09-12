Shader "Cashier/2D/PixelFog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Fog Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlowSpeedX ("Horizontal Flow (widths/sec)", Float) = 0.1
        _FlowSpeedY ("Vertical Flow (heights/sec)", Float) = 0.015
        _NoiseScale1 ("Noise 1 Scale", Float) = 4
        _NoiseSpeed1 ("Noise 1 Speed", Float) = 0.65
        _NoiseStrength1 ("Noise 1 Strength (pixels)", Range(0,3)) = 1.2
        _NoiseScale2 ("Noise 2 Scale", Float) = 11
        _NoiseSpeed2 ("Noise 2 Speed", Float) = 1.1
        _NoiseStrength2 ("Noise 2 Strength (pixels)", Range(0,3)) = 0.7
        _SwirlStrength ("Local Swirl Strength (pixels)", Range(0,3)) = 1.4
        _SwirlScale ("Local Swirl Scale", Float) = 8
        _SwirlSpeed ("Local Swirl Speed", Float) = 0.8
        _AlphaMin ("Minimum Alpha Multiplier", Range(0.05,1)) = 0.45
        _AlphaMax ("Maximum Alpha Multiplier", Range(0.05,1)) = 0.8
        _AlphaNoiseStrength ("Alpha Noise Mix", Range(0,1)) = 0.65
        _PixelSize ("Pixel Grid (source pixels)", Range(1,8)) = 1
        _DistortionStrength ("Overall Distortion Multiplier", Range(0,3)) = 1
        _Opacity ("Opacity", Range(0,1)) = 0.65
        _BottomFadePixels ("Bottom Edge Fade (source pixels)", Float) = 0
        _Seed ("Pattern Seed", Float) = 1
        [Toggle] _UsePresentationTime ("Use Pausable Presentation Time", Float) = 0
        _PresentationSeconds ("Presentation Seconds", Float) = 0
        [Toggle] _UseUI ("uGUI Image Mode", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
               "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull Off

        Pass
        {
            Name "PixelFog"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FogVertex
            #pragma fragment FogFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            // Core2D -> Core -> GlobalSamplers supplies sampler_PointClamp; do not redeclare it.
            // Explicit point sampling and LOD 0 prevent bilinear and mip blending.
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FlowSpeedX, _FlowSpeedY;
                float _NoiseScale1, _NoiseSpeed1, _NoiseStrength1;
                float _NoiseScale2, _NoiseSpeed2, _NoiseStrength2;
                float _SwirlStrength, _SwirlScale, _SwirlSpeed;
                float _AlphaMin, _AlphaMax, _AlphaNoiseStrength;
                float _PixelSize, _DistortionStrength, _Opacity, _Seed;
                float _UseUI;
                float _BottomFadePixels;
                float _UsePresentationTime, _PresentationSeconds;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings FogVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // uGUI has no SpriteRenderer flip/color data; use its vertex data directly.
                float3 position = _UseUI > 0.5 ? input.positionOS : UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(position);
                output.uv = input.uv;
                output.color = input.color * _Color;
                if (_UseUI < 0.5) output.color *= unity_SpriteColor;
                return output;
            }

            float Hash(float2 cell)
            {
                float3 p = frac(float3(cell.xyx) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Value noise plus its analytic gradient: no extra texture or derivative taps.
            float3 NoiseGradient(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float a = Hash(cell);
                float b = Hash(cell + float2(1,0));
                float c = Hash(cell + float2(0,1));
                float d = Hash(cell + 1);
                float2 u = f * f * (3 - 2 * f);
                float2 du = 6 * f * (1 - f);
                float k = a - b - c + d;
                return float3(a + (b-a)*u.x + (c-a)*u.y + k*u.x*u.y,
                              du.x * (b-a + k*u.y), du.y * (c-a + k*u.x));
            }

            float2 NoiseVector(float2 p)
            {
                return float2(NoiseGradient(p).x, NoiseGradient(p + float2(17.3,9.2)).x) * 2 - 1;
            }

            half4 FogFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = _UsePresentationTime > 0.5 ? _PresentationSeconds : _Time.y;
                float seed = fmod(abs(_Seed), 4096);
                float2 seedOffset = float2(seed * 0.37, seed * 0.73);
                float2 size = max(_MainTex_TexelSize.zw, 1);
                float grid = max(1, round(_PixelSize));
                float2 uv = (floor(input.uv * size / grid) * grid + grid * 0.5) / size;
                // Positive speed moves the visible pattern right/up. Flow is separate from distortion.
                float2 flowingUV = uv - float2(_FlowSpeedX, _FlowSpeedY) * time;
                float2 p = uv + seedOffset;
                float2 n1 = NoiseVector(p * max(0.01,_NoiseScale1) +
                    time * _NoiseSpeed1 * float2(0.71,0.29));
                float2 n2 = NoiseVector(p * max(0.01,_NoiseScale2) +
                    time * _NoiseSpeed2 * float2(-0.43,0.83));
                float3 swirl = NoiseGradient(p * max(0.01,_SwirlScale) +
                    time * _SwirlSpeed * float2(0.31,-0.57));
                // Perpendicular noise gradient curls around LOCAL extrema, not the sprite center.
                float2 curl = float2(swirl.z, -swirl.y);
                curl /= max(1, length(curl));
                float2 pixels = (n1 * _NoiseStrength1 + n2 * _NoiseStrength2 +
                    curl * _SwirlStrength) * max(0,_DistortionStrength);
                pixels *= min(1, 3 / max(length(pixels), 0.0001));
                // Wrap explicitly within this standalone sprite, then sample exact source texel centers.
                float2 source = frac(flowingUV + pixels / size) * size;
                source = min(floor(source / grid) * grid + floor(grid * 0.5), size - 1);
                half4 fog = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_PointClamp, (source + 0.5) / size, 0);
                float alphaLow = clamp(min(_AlphaMin,_AlphaMax),0.05,1);
                float alphaHigh = clamp(max(_AlphaMin,_AlphaMax),alphaLow,1);
                float alphaNoise = NoiseGradient(p * 3.7 + time * float2(-0.17,0.11)).x;
                float modulation = lerp(1, lerp(alphaLow,alphaHigh,alphaNoise), saturate(_AlphaNoiseStrength));
                // Zero source alpha stays zero. Noise cannot globally flash the fog off.
                fog.rgb *= input.color.rgb;
                fog.a *= input.color.a * saturate(_Opacity) * modulation;
                // Fade both the fixed mesh bottom and the sampled PNG bottom, so vertical
                // wrapping cannot expose a moving straight cut. Texture sampling stays point-filtered.
                if (_BottomFadePixels > 0)
                {
                    float bottomDistance = min(floor(input.uv.y * size.y), source.y);
                    fog.a *= smoothstep(0, max(1, _BottomFadePixels), bottomDistance);
                }
                return fog;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
