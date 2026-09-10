Shader "Dystopia/Imported/Cashier/InspectorPortrait"
{
    Properties
    {
        [PerRendererData] _MainTex ("Portrait", 2D) = "white" {}
        _NormalMap ("Linear RGB normal", 2D) = "bump" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex, _NormalMap;
            float4 _ClipRect, _LightColor;
            float3 _LightDirection;
            float _LightIntensity, _Ambient, _NormalStrength, _NormalRotation;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float2 local:TEXCOORD1; };
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; o.local=v.vertex.xy; return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                float3 n=normalize(lerp(float3(0,0,1),tex2D(_NormalMap,i.uv).rgb*2-1,_NormalStrength));
                float s=sin(_NormalRotation), r=cos(_NormalRotation);
                n.xy=float2(r*n.x-s*n.y,s*n.x+r*n.y);
                float facing=saturate(dot(n,normalize(_LightDirection)));
                // Broad light separation retains the original dark fabric texture.
                float diffuse=smoothstep(.12,.85,facing);
                c.rgb*=float3(.85,.92,1)*_Ambient + _LightColor.rgb*diffuse*_LightIntensity;
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.local,_ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
