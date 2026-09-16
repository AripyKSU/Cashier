Shader "Cashier/WorkbenchLighting"
{
 Properties
 {
  [PerRendererData] _MainTex("Sprite",2D)="white"{}
  [Normal] _NormalMap("Workbench normals",2D)="bump"{}
  _AmbientTint("Time tint",Color)=(.72,.72,.72,1)
  _SpotTint("Soft center light",Color)=(1,.92,.76,1)
  _SpotIntensity("Light intensity",Float)=.55
  _LightCenter("Light center UV",Vector)=(.5,.5,0,0)
  _LightRadius("Light radius",Float)=1
  _NormalStrength("Normal strength",Float)=.55
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off
  Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   Tags {"LightMode"="Universal2D"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
   float4 _AmbientTint, _SpotTint, _LightCenter;
   float _SpotIntensity, _LightRadius, _NormalStrength;
   struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct V { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   V vert(A i) { V o; o.vertex=TransformObjectToHClip(i.vertex.xyz); o.uv=i.uv; o.color=i.color; return o; }
   half4 frag(V i):SV_Target
   {
    half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
    float3 n=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,i.uv));
    n=normalize(float3(n.xy*_NormalStrength,max(.1,n.z)));
    float2 delta=(_LightCenter.xy-i.uv)*float2(1.777778,1);
    float diffuse=saturate(dot(n,normalize(float3(delta,.55))));
    float spot=1-smoothstep(0,max(.01,_LightRadius),length(delta));
    c.rgb*= _AmbientTint.rgb + _SpotTint.rgb * (_SpotIntensity*spot*(.3+.7*diffuse));
    return c;
   }
   ENDHLSL
  }
 }
}
