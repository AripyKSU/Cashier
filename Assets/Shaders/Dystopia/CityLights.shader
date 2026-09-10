Shader "Dystopia/Imported/Cashier/UI/CityLights"
{
 Properties { [PerRendererData] _MainTex("Lights",2D)="white"{} }
 SubShader {
 Tags { "Queue"="Transparent" "RenderType"="Transparent" }
 Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
 Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 struct V {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 sampler2D _MainTex;
 V vert(A i){V o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
 fixed4 frag(V i):SV_Target {
 fixed4 c=tex2D(_MainTex,i.uv);
 // Keep only chromatic lamp pixels; neutral generated background is never rendered.
 float chroma=max(c.r,max(c.g,c.b))-min(c.r,min(c.g,c.b));
 c.a*=step(.18,chroma);return c*i.color;
 }
 ENDCG
 }
 }
}
