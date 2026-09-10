Shader "Dystopia/Imported/Dystopia/WatchNeutral"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _GrayRegion ("Neutral region UV", Vector) = (0,0,1,1)
        _Brightness ("Neutral brightness", Float) = 1
        _PixelGrid ("Pixel grid", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4 _GrayRegion;
            float _Brightness;
            float4 _PixelGrid;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 grid = max(_PixelGrid.xy,float2(1,1));
                float pixelated = step(1,_PixelGrid.x) * step(1,_PixelGrid.y);
                float2 pixelUv = (floor(i.uv * grid) + .5) / grid;
                fixed4 c = tex2D(_MainTex,lerp(i.uv,pixelUv,pixelated));
                // The configured UV region is neutralized; the guard material uses the full sprite.
                float inside = step(_GrayRegion.x,i.uv.x) * step(_GrayRegion.y,i.uv.y)
                    * step(i.uv.x,_GrayRegion.z) * step(i.uv.y,_GrayRegion.w);
                float gray = dot(c.rgb,float3(.2126,.7152,.0722)) * _Brightness;
                c.rgb = lerp(c.rgb,gray.xxx,inside);
                return c * i.color;
            }
            ENDCG
        }
    }
}
