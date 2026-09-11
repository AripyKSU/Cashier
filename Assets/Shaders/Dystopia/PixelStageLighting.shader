Shader "Dystopia/Imported/Cashier/PixelStageLighting"
{
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white"{} [PerRendererData] _NormalMap("RGB Tangent Normal",2D)="bump"{} }
 SubShader {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
 Cull Off ZWrite Off ZTest Always
 Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
 Pass {
 Tags {"LightMode"="Universal2D"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
 TEXTURE2D(_MainTex); TEXTURE2D(_NormalMap); TEXTURE2D(_CustomerSilhouette);
 float4 _ShadowBody;
 float _ReceiveCustomerShadow, _CustomerShadowOpacity;
 float4 _SunShadowOrigin;
 float _SunShadowOpacity;
 float4 _MainTex_TexelSize, _Tint, _Ambient, _Sun, _LampColor, _LampPosition, _SunDirection, _ClipRect, _SkyOrigin, _SkyGlow;
 float _Surface, _LampStrength, _RimStrength, _RimWidthPixels, _Steps, _NormalStrength, _RoomBounce;
 float4 _SpotOrigin, _SpotDirection;
 float _SpotPower, _SpotHaze, _SpotResponse, _KeyContrast, _SpotSoftness;
 float _DaylightDetail, _DaylightFill;
 float _BottomShade, _ContactShadow, _PropFill;
 float _HighlightResponse, _SpecularResponse, _Emission;
 float4 _ContactAnchor;
 float4 _TowerOrigins;
 float _TowerPower;
 float4 _NeutralRegion;
 float _UseNeutralRegion, _NeutralBrightness;
 float _AmbientSeconds;
 // Small opaque silhouettes live in the sky layer, behind the separately drawn skyline and shop.
 float SkyBirds(float2 world){
  float mask=0;
  [unroll] for(int bird=0;bird<5;bird++){
   float seconds=_AmbientSeconds;
   float x=frac(seconds*(.009+bird*.0005)+bird*.17)*1480-100;
   float y=-155-bird*13+sin(seconds*.7+bird)*5;
   float2 p=(world-float2(x,y))/1.25;
   float wing=abs(p.x);
   float flap=sin(seconds*(6+bird*.3)+bird*2);
   float wingY=wing*(.25+.5*flap);
   float wings=step(wing,7)*step(abs(p.y-wingY),1.3);
   float body=step(wing,1.5)*step(abs(p.y+1),2);
   mask=max(mask,max(wings,body));
  }
  return mask;
 }
 // A narrow transition keeps contrast without cutting a whole sprite at one threshold.
 float CharacterLight(float facing){
  return .08+.47*smoothstep(.32,.44,facing)+.45*smoothstep(.66,.78,facing);
 }
 // Planar silhouette projection, clipped to the authored counter top band.
 float CustomerShadow(float2 world,float2 tower){
  float depth=_ShadowBody.z-world.y;
  float v=depth/max(1,_ShadowBody.w);
  float drift=(_ShadowBody.x-tower.x)*depth/max(100,tower.y-_ShadowBody.z);
  float u=(world.x-_ShadowBody.x-drift)/max(1,_ShadowBody.y*(.55+v*.45))+.5;
  float inside=step(0,u)*step(u,1)*step(0,v)*step(v,1);
  return step(.5,SAMPLE_TEXTURE2D(_CustomerSilhouette,sampler_PointClamp,float2(u,v)).a)*inside;
 }
 // Screen-space cone shared by the haze and the receiving surfaces.
 float SpotCone(float2 world){
  float2 delta=world-_SpotOrigin.xy;
  float distanceToLight=length(delta);
  float angular=dot(delta/max(.001,distanceToLight),_SpotDirection.xy);
  float innerCone=lerp(_SpotDirection.z,.9999,saturate(_SpotSoftness));
  return smoothstep(_SpotDirection.z,innerCone,angular)
   *(1-smoothstep(_SpotDirection.w*.75,_SpotDirection.w,distanceToLight));
 }
 struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct V {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float2 world:TEXCOORD1;};
 V vert(A i){V o;float3 w=TransformObjectToWorld(i.vertex.xyz);o.vertex=TransformWorldToHClip(w);o.world=w.xy-float2(10000,10000);o.uv=i.uv;o.color=i.color*_Tint;return o;}
 half4 frag(V i):SV_Target {
  if(_Surface>7.5)return half4(_LampColor.rgb,SpotCone(i.world)*_SpotHaze);
  clip(min(min(i.world.x-_ClipRect.x,_ClipRect.z-i.world.x),min(i.world.y-_ClipRect.y,_ClipRect.w-i.world.y)));
  if(_ContactShadow>.5){
   // Both terms start at the actual footprint; only the softer cast extends away.
   float2 contact=(i.uv-_ContactAnchor.xy)*2;
   float depth=max(0,-contact.y);
   float footprint=max(.1,_ContactAnchor.z);
   float core=.98*exp(-2*pow(abs(contact.x)/footprint,8)-220*contact.y*contact.y);
   float castX=contact.x-depth*_ContactAnchor.w*2;
   float soft=.72*exp(-2*pow(abs(castX)/(footprint*1.22),6)-3.8*depth);
   soft*=1-smoothstep(0,.12,contact.y);
   float edge=smoothstep(0,.08,i.uv.x)*smoothstep(0,.08,1-i.uv.x)*smoothstep(0,.1,i.uv.y);
   float opacity=(1-(1-core)*(1-soft))*edge;
   return half4(_Tint.rgb,_Tint.a*opacity);
  }
  half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv);
  if(_UseNeutralRegion>.5){
   float inside=step(_NeutralRegion.x,i.uv.x)*step(_NeutralRegion.y,i.uv.y)*step(i.uv.x,_NeutralRegion.z)*step(i.uv.y,_NeutralRegion.w);
   float gray=dot(c.rgb,float3(.2126,.7152,.0722))*_NeutralBrightness;
   c.rgb=lerp(c.rgb,gray.xxx,inside);
  }
  c*=i.color;
  if(_Surface>6.5){
   // Illuminate bright cloud pixels, leaving dark skyline silhouettes intact.
   float sky=smoothstep(.69,.91,i.uv.y);
   float cloud=smoothstep(.23,.63,dot(c.rgb,float3(.3,.59,.11)));
   float2 d=(i.uv-_SkyOrigin.xy)*float2(3,5);
   float opening=exp(-dot(d,d))*(.8+.2*sin(i.uv.x*24+i.uv.y*12+_SkyOrigin.z));
   c.rgb+=_SkyGlow.rgb*sky*cloud*opening;
   // Keep silhouettes at 75% display gray, independent of the sky tint.
   float3 birdColor=float3(.75,.75,.75);
   #ifndef UNITY_COLORSPACE_GAMMA
   birdColor=SRGBToLinear(birdColor);
   #endif
   c.rgb=lerp(c.rgb,birdColor,SkyBirds(i.world));
   return c;
  }
  if(_Surface>3.5){float chroma=max(c.r,max(c.g,c.b))-min(c.r,min(c.g,c.b));c.a*=step(.18,chroma);return c;}
  if(_Surface<.5)return half4(c.rgb*(1+_Emission),c.a);
  // Compress baked bright flecks before lighting; retain dark seams and rust color.
  float albedoLuma=dot(c.rgb,float3(.2126,.7152,.0722));
  c.rgb*=lerp(1,_HighlightResponse,smoothstep(.08,.4,albedoLuma));
  // Broad surface normals describe torso/face volume, not noisy rust or cloth pixels.
  float2 xy=(i.uv-float2(.5,.48))*float2(1.65,.8);
  float3 n=normalize(float3(xy,sqrt(saturate(1-dot(xy,xy)))));
  if(_Surface<1.5)n=normalize(float3(0,.3,1));
  if(_Surface>2.5)n=normalize(float3(xy.x*.25,.35,1));
  if(_NormalStrength>0){
   // Uncompressed linear RGB normals, no platform-dependent packed-normal decoding.
   float3 mapped=SAMPLE_TEXTURE2D(_NormalMap,sampler_PointClamp,i.uv).rgb*2-1;
   mapped.z=max(.15,mapped.z);
   // Keep torso curvature: a flat-facing detail map must not flatten both sides.
   float3 detailed=normalize(float3(n.xy+mapped.xy*_NormalStrength,n.z*lerp(1,mapped.z,_NormalStrength*.5)));
   n=normalize(lerp(normalize(lerp(n,normalize(mapped),_NormalStrength)),detailed,_KeyContrast));
  }
  float3 l=normalize(float3(_LampPosition.xy-i.world,_LampPosition.z));
  float attenuation=pow(saturate(1-distance(_LampPosition.xy,i.world)/max(1,_LampPosition.w)),1.3);
  float diffuse=floor(max(0,dot(n,l))*max(2,_Steps)+.5)/max(2,_Steps);
  float person=step(1.5,_Surface)*(1-step(2.5,_Surface));
  // Normal-mapped props share the customer light/shade response, retaining their own normals.
  float subject=max(person,step(2.5,_Surface)*step(.001,_NormalStrength));
  // Sample the raw normal response once; quantizing twice produces horizontal bands.
  float sunFacing=saturate(dot(n,normalize(_SunDirection.xyz)));
  if(subject>.5){
   // Smooth daylight response keeps normal-map detail between the night lighting bands.
   sunFacing=lerp(CharacterLight(sunFacing),.22+.78*sunFacing,_DaylightDetail);
   diffuse=CharacterLight(saturate(dot(n,l)));
  }
  float contrast=_KeyContrast*max(subject,_SpotResponse*.65);
  float3 light=_Ambient.rgb*lerp(1,.4,contrast)+_Sun.rgb*sunFacing*lerp(.5,.8,contrast);
  light+=_LampColor.rgb*attenuation*diffuse*_LampStrength*lerp(1,.18,contrast);
  float3 spotVector=normalize(float3(_SpotOrigin.xy-i.world,_SpotOrigin.z));
  float spotDiffuse=floor(saturate(dot(n,spotVector))*max(2,_Steps)+.5)/max(2,_Steps);
  if(subject>.5)spotDiffuse=CharacterLight(saturate(dot(n,spotVector)));
  light+=_LampColor.rgb*SpotCone(i.world)*_SpotPower*_SpotResponse*spotDiffuse;
  // A multiplicative fill preserves the source face and cloth pixels in deep daytime shade.
  if(subject>.5)light=max(light,_DaylightFill.xxx);
  // A user-adjustable prop fill keeps clock housing readable outside the spotlight.
  light=max(light,_PropFill.xxx);

  float2 offset=normalize(_SunDirection.xy)*_MainTex_TexelSize.xy*_RimWidthPixels;
  float neighbor=SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv+offset).a;
  float rim=saturate(c.a-neighbor)*_RimStrength;
  float3 result=c.rgb*light+_Sun.rgb*rim*.35*(1-person);
  float broadFalloff=.35+.65*saturate(1-distance(_LampPosition.xy,i.world)/max(1,_LampPosition.w*1.8));
  result+=c.rgb*_LampColor.rgb*_RoomBounce*broadFalloff*(.35+.65*diffuse)*lerp(1,.15,contrast);
  if(_Surface>2.5){
   float spec=pow(saturate(dot(n,normalize(l+float3(0,0,1)))),24);
   float metalMask=smoothstep(.12,.5,dot(c.rgb,float3(.3,.59,.11)));
   result+=_LampColor.rgb*spec*attenuation*metalMask*_LampStrength*.3*lerp(1,.18,contrast)*_SpecularResponse;
  }
  if(_ReceiveCustomerShadow>.5){
   float shadow=max(CustomerShadow(i.world,_TowerOrigins.xy),CustomerShadow(i.world,_TowerOrigins.zw)*.7);
   // Daytime sunlight casts one shadow; keep the authored tower shadows at night.
   float sunShadow=CustomerShadow(i.world,_SunShadowOrigin.xy)*_SunShadowOpacity;
   result*=1-max(shadow*_CustomerShadowOpacity,sunShadow);
  }
  // Contact darkening stays on the lower rim of explicitly marked tabletop props.
  result*=1-_BottomShade*(1-smoothstep(.08,.32,i.uv.y));
  return half4(result,c.a);
 }
 ENDHLSL
 }
 }
}
