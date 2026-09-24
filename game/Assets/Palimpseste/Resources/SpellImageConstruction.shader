Shader "Palimpseste/SpellImageConstruction"
{
    Properties
    {
        [HDR] _Color ("Image palette", Color) = (.3,.6,1,1)
        _Material ("Glass / energy / mist / stone / metal", Float) = 0
        _SurfaceProfile ("Legacy / plasma / force field / toxic / spectral flow", Float) = 0
        _SurfaceTex ("Sourced surface pattern (RGB)", 2D) = "white" {}
        _Shape ("Volume / feather / ribbon / filament", Float) = 0
        _Opacity ("Surface opacity", Range(0,1)) = .7
        _Emission ("Radiance", Range(0,6)) = 1
        _Envelope ("Lifecycle", Range(0,1)) = 1
        _Reveal ("Authored entrance reveal", Range(0,1)) = 1
        _Dissolve ("Authored ending erosion", Range(0,1)) = 0
        _ResourceTex ("Curated VFX texture", 2D) = "white" {}
        _ResourceEnabled ("Curated texture enabled", Float) = 0
        _ResourceParticle ("Particle mask", Float) = 0
        _BehaviorFlow ("Authored surface advection", Vector) = (0,0,0,0)
        _BehaviorAge ("Authored motion age", Float) = 0
        _BehaviorEnabled ("Explicit behavior", Float) = 0
        _BehaviorMotion ("Behavior moves", Float) = 1
        _Armed ("Armed presentation", Range(0,1)) = 0
        _Seed ("Controlled variation", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("Depth write", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+18" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            // Ashima/stegu simplex, Unity adaptation by Keijiro; MIT license
            // retained in SourcedNoise/LICENSE.txt alongside reviewed sources.
            #include "SourcedNoise/SimplexNoise3D.hlsl"
            #include "SourcedSurfaces/KeijiroDivergenceFreeNoise.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Material, _Shape, _Opacity, _Emission, _Envelope, _Seed, _Armed;
                float _Reveal, _Dissolve;
                float4 _BehaviorFlow;
                float _BehaviorAge, _ResourceEnabled, _ResourceParticle, _BehaviorEnabled, _BehaviorMotion;
                float _SurfaceProfile;
            CBUFFER_END
            TEXTURE2D(_ResourceTex); SAMPLER(sampler_ResourceTex);
            TEXTURE2D(_SurfaceTex); SAMPLER(sampler_SurfaceTex);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                // TinyPlay Plasma's noise-driven normal displacement, adapted to
                // a small bounded ripple so the authored silhouette stays intact.
                if (_SurfaceProfile > .5 && _SurfaceProfile < 1.5)
                {
                    float age=lerp(_Time.y,_BehaviorAge*_BehaviorMotion,saturate(_BehaviorEnabled));
                    float ripple=SimplexNoise(float3(input.positionOS.xy*12+age*.12,_Seed*8));
                    input.positionOS.xyz+=input.normalOS*(ripple*.018*saturate(_Envelope));
                }
                output.positionHCS=TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;
                return output;
            }
            float hash21(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
            }
            float fbm(float2 p) { return noise(p)*.58+noise(p*2.07+7.3)*.29+noise(p*4.13+13.1)*.13; }
            // TinyPlay ToxicShader graph: animated Voronoi distance drives
            // emission and surface relief. The 3x3 search is the node's bounded
            // neighbourhood; coordinates and time remain application-controlled.
            float surfaceVoronoi(float2 uv, float angle)
            {
                float2 cell=floor(uv),local=frac(uv);
                float nearest=8;
                [unroll] for (int y=-1;y<=1;y++)
                [unroll] for (int x=-1;x<=1;x++)
                {
                    float2 neighbour=float2(x,y);
                    float2 random=float2(hash21(cell+neighbour),hash21(cell+neighbour+37.2));
                    float2 cellPoint=.5+.44*sin(random*6.2831853+angle);
                    float2 delta=neighbour+cellPoint-local;
                    nearest=min(nearest,dot(delta,delta));
                }
                return sqrt(nearest);
            }
            float surfaceSceneEyeDepth(float rawDepth)
            {
                float perspective=LinearEyeDepth(rawDepth,_ZBufferParams);
                #if UNITY_REVERSED_Z
                    float orthographic=lerp(_ProjectionParams.z,_ProjectionParams.y,rawDepth);
                #else
                    float orthographic=lerp(_ProjectionParams.y,_ProjectionParams.z,rawDepth);
                #endif
                return lerp(perspective,orthographic,unity_OrthoParams.w);
            }
            half4 frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                float3 normal=SafeNormalize(input.normalWS);
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fres=pow(saturate(1-abs(dot(normal,view))),2.4);
                float t=lerp(_Time.y,_BehaviorAge*_BehaviorMotion,saturate(_BehaviorEnabled));
                float2 advected=uv+_BehaviorFlow.xy*_BehaviorAge;
                float fabric=fbm(advected*float2(8,5)+_Seed*3.7);
                if (_ResourceEnabled>.5)
                    fabric=lerp(fabric,SimplexNoise(float3(advected*3.5,_BehaviorAge*_BehaviorMotion*.37+_Seed*7))*.5+.5,.45);
                float streams=fbm(advected*float2(13,4)+float2(-t*.42,_Seed*7));
                float ridges=pow(saturate(1-abs(streams-.54)*13),2);
                float edge=abs(uv.y*2-1);
                float feather=step(.5,_Shape)*(1-step(1.5,_Shape));
                float ribbon=step(1.5,_Shape)*(1-step(2.5,_Shape));
                float filament=step(2.5,_Shape);
                float outerEdge=smoothstep(.82,.995,edge)*feather;
                float vein=exp(-abs(uv.y-.5)*75)*feather;
                float barbs=pow(saturate(.5+.5*sin(uv.x*135+edge*34)),18)*feather;
                barbs*=1-saturate(fwidth(uv.x*135+edge*34)*.7);
                float taper=lerp(1,pow(saturate(sin(uv.x*3.14159265)),.35)*(1-smoothstep(.64,1,edge)),ribbon);
                float radiance=0,opacity=0;
                half3 colour=_Color.rgb;
                half3 accent=lerp(_Color.rgb,half3(1,1,1),.68);
                float emissionCoverage=_Opacity;
                [branch] if (_SurfaceProfile>.5 && _SurfaceProfile<1.5)
                {
                    // HLSL port of TinyPlay Shaders/VFX/PlasmaShader.shadergraph:
                    // opposite pattern scrolls, coloured Fresnel border, twirled
                    // refraction and the small normal displacement above.
                    float2 p=uv-.5;
                    float angle=length(p)*6;
                    float sine=sin(angle),cosine=cos(angle);
                    float2 swirl=float2(p.x*cosine-p.y*sine,p.x*sine+p.y*cosine)+.5;
                    float distortion=SimplexNoise(float3(swirl*4,t*.1+_Seed))*.065;
                    float2 drift=float2(t*.10,t*.07)+_BehaviorFlow.xy*_BehaviorAge;
                    half3 a=SAMPLE_TEXTURE2D(_SurfaceTex,sampler_SurfaceTex,swirl+drift+distortion).rgb;
                    half3 b=SAMPLE_TEXTURE2D(_SurfaceTex,sampler_SurfaceTex,swirl-drift-distortion).rgb;
                    float plasma=dot(a+b,half3(.2126,.7152,.0722));
                    float border=pow(saturate(1-abs(dot(normal,view))),2);
                    float2 screenUv=GetNormalizedScreenSpaceUV(input.positionHCS);
                    float2 bend=float2(ddx(distortion),ddy(distortion))*8;
                    half3 refraction=SampleSceneColor(saturate(screenUv+clamp(bend,-.012,.012)));
                    colour=_Color.rgb*(.16+plasma*.75)+refraction*.14;
                    accent=lerp(_Color.rgb,half3(1,1,1),.18);
                    radiance=(pow(saturate(plasma),2)*2.4+border*2.1)*_Emission*taper;
                    opacity=_Opacity*(.26+plasma*.46+border*.18)*taper;
                    fabric=saturate(plasma*.65+fabric*.35);
                    emissionCoverage=sqrt(saturate(_Opacity));
                }
                else if (_SurfaceProfile>1.5 && _SurfaceProfile<2.5)
                {
                    // TinyPlay ForceFieldShader: pattern*(Fresnel+intersection)
                    // plus a sparse fill. RGB is used: the source Noise.png has
                    // no useful alpha mask. Depth is valid for both lab cameras.
                    float2 screenUv=GetNormalizedScreenSpaceUV(input.positionHCS);
                    float sceneEye=surfaceSceneEyeDepth(SampleSceneDepth(screenUv));
                    float fragmentEye=-TransformWorldToView(input.positionWS).z;
                    float gap=max(0,sceneEye-fragmentEye);
                    float intersection=pow(saturate(1-gap/.32),2);
                    float rim=pow(saturate(1-abs(dot(normal,view))),3);
                    half3 source=SAMPLE_TEXTURE2D(_SurfaceTex,sampler_SurfaceTex,
                        uv*3+float2(t*.05,t*.07)+_BehaviorFlow.xy*_BehaviorAge).rgb;
                    float pattern=smoothstep(.18,.72,dot(source,half3(.2126,.7152,.0722)));
                    float boundary=saturate(rim+intersection);
                    float field=pattern*(boundary*.86+.10);
                    colour=_Color.rgb*(.28+field*.58);
                    accent=lerp(_Color.rgb,half3(1,1,1),.12);
                    radiance=(boundary*(.28+pattern*1.5)+pattern*.055)*_Emission*2.5;
                    opacity=_Opacity*saturate(field*.60+boundary*.32+.015)*taper;
                    fabric=pattern;
                    emissionCoverage=sqrt(saturate(_Opacity));
                }
                else if (_SurfaceProfile>2.5 && _SurfaceProfile<3.5)
                {
                    // TinyPlay Environment/ToxicShader graph topology: moving
                    // Voronoi, power-shaped emissive cells and height-derived
                    // normals. Palette and lifecycle come from the spell data.
                    float2 liquidUv=uv*5+float2(t*.06,t*.18)+_BehaviorFlow.xy*_BehaviorAge;
                    float cells=surfaceVoronoi(liquidUv,t*.65+_Seed*6.28);
                    float pools=pow(saturate(cells*1.6),4);
                    float film=.5+.5*SimplexNoise(float3(liquidUv*1.4,t*.13+_Seed*3));
                    float height=cells*.72+film*.28;
                    float3 tangentX=ddx(input.positionWS),tangentY=ddy(input.positionWS);
                    float3 slope=SafeNormalize(tangentX)*ddx(height)+SafeNormalize(tangentY)*ddy(height);
                    float3 wetNormal=SafeNormalize(normal-slope*2.2);
                    Light light=GetMainLight();
                    float lighting=.25+saturate(dot(wetNormal,light.direction))*.75;
                    float spec=pow(saturate(dot(wetNormal,SafeNormalize(light.direction+view))),38);
                    colour=_Color.rgb*(.18+pools*.42)*lighting+light.color*spec*.16;
                    accent=lerp(_Color.rgb,half3(1,1,1),.14);
                    radiance=(pools*2.7+pow(saturate(1-abs(dot(wetNormal,view))),3)*.4)*_Emission*taper;
                    opacity=_Opacity*(.30+film*.36+pools*.28)*taper;
                    fabric=saturate(cells*.8+film*.2);
                    emissionCoverage=sqrt(saturate(_Opacity));
                }
                else if (_SurfaceProfile>3.5)
                {
                    // Keijiro's actual cross-gradient operator drives organic
                    // surface advection. This is a visual field, not a substitute
                    // for the authored transform, projectile or vortex physics.
                    float3 coordinate=float3(uv*2.8,_Seed*7+t*.20);
                    float3 flow=PalimpsesteDFNoise3D(coordinate,coordinate.yzx+float3(7.1,3.7,13.4));
                    flow/=1+length(flow);
                    float2 flowUv=uv*float2(3,1.8)+flow.xy*.32+float2(-t*.24,t*.035)
                        +_BehaviorFlow.xy*_BehaviorAge;
                    half3 source=SAMPLE_TEXTURE2D(_SurfaceTex,sampler_SurfaceTex,flowUv).rgb;
                    float density=dot(source,half3(.2126,.7152,.0722));
                    // Thin emissive contours carry the silhouette. The broad
                    // noise field is only a trace of vapour, never a solid skin.
                    float lineWidth=.018+min(.018,fwidth(density)*.65);
                    float veins=pow(saturate(1-abs(density-.51)/lineWidth),2);
                    float softness=PalimpsesteTexturelessStrip(uv,.48);
                    float silhouette=lerp(.35+fres*.65,softness,saturate(feather+ribbon+filament));
                    float cloud=smoothstep(.16,.72,density)*silhouette;
                    colour=_Color.rgb*(.12+cloud*.20);
                    accent=lerp(_Color.rgb,half3(1,1,1),.20);
                    radiance=(veins*3.4+cloud*.06+filament*.32)*_Emission*silhouette;
                    opacity=_Opacity*(cloud*.07+veins*.16)*silhouette;
                    fabric=saturate(density*.7+fabric*.3);
                    emissionCoverage=sqrt(saturate(_Opacity));
                }
                else if (_Material<.5)
                {
                    // Emission is a perceptual control: even low-energy glass
                    // needs bright thin contours around its dark body. Keep
                    // the source hue instead of whitening every material.
                    float3 chroma=_Color.rgb/max(max(_Color.r,_Color.g),max(_Color.b,.08));
                    accent=lerp(chroma,half3(1,1,1),.22);
                    float cloud=fbm(uv*float2(12,7)+float2(fabric,streams)*1.65+float2(-t*.035,_Seed*11));
                    float fractureField=fbm(uv*float2(27,18)+float2(cloud*.9,fabric*1.2));
                    float fractureWidth=.018+min(.025,fwidth(fractureField)*.35);
                    float fractures=pow(saturate(1-abs(fractureField-.5)/fractureWidth),2);
                    // Grazing light must not illuminate an entire flat glass
                    // face. Keep the strong HDR line on the authored feather
                    // perimeter; the view-dependent sheen remains subdued.
                    float glassEdge=smoothstep(.965,.999,edge)*feather;
                    float contour=max(glassEdge,pow(fres,3.5)*.065);
                    float intensity=sqrt(max(0,_Emission))*4.5;
                    opacity=_Opacity*(.64+fabric*.20+fres*.04)*taper;
                    colour*=.16+cloud*.34;
                    radiance=intensity*(contour*1.6+fractures*(.020+cloud*.065)+vein*.14+barbs*.015);
                    emissionCoverage=sqrt(saturate(_Opacity));
                }
                else if (_Material<1.5)
                {
                    float3 chroma=_Color.rgb/max(max(_Color.r,_Color.g),max(_Color.b,.08));
                    accent=lerp(chroma,half3(1,1,1),.22);
                    opacity=_Opacity*(.16+streams*.30+fres*.18)*taper;
                    colour*=.38+streams*.48;
                    // A thin filament must carry HDR energy before the lab's
                    // existing bloom can give it a soft luminous footprint.
                    radiance=(ridges*.65+fres*.38+outerEdge*.35+vein*.24+filament*1.4)*_Emission*2.5*taper;
                    emissionCoverage=sqrt(saturate(_Opacity));
                }
                else if (_Material<2.5)
                {
                    float cloud=fbm(uv*float2(8,9)+float2(-t*.20,streams*2.1+_Seed*8));
                    float airy=smoothstep(.18,.74,streams*.54+cloud*.46)*taper;
                    float threads=pow(saturate(1-abs(cloud-.53)*11),2);
                    // Optical depth preserves transparent, low-opacity mist
                    // without attenuating its declared opacity three times.
                    opacity=(1-exp(-_Opacity*airy*1.65));
                    colour*=.55+cloud*.55;
                    accent=lerp(_Color.rgb,half3(1,1,1),.20);
                    radiance=airy*sqrt(max(0,_Emission))*(1.35+threads*2.8);
                    emissionCoverage=sqrt(saturate(_Opacity));
                }
                else
                {
                    Light light=GetMainLight();
                    float diffuse=.28+saturate(dot(normal,light.direction))*.72;
                    float metal=step(3.5,_Material);
                    float spec=pow(saturate(dot(normal,SafeNormalize(light.direction+view))),lerp(12,75,metal));
                    opacity=_Opacity;
                    colour*=diffuse*(.72+fabric*.28);
                    colour+=light.color*spec*lerp(.08,.65,metal);
                    radiance=(frac(uv.x*7+_Seed)*.02+outerEdge*.15)*_Emission;
                }
                float envelope=saturate(_Envelope);
                float resource=SAMPLE_TEXTURE2D(_ResourceTex,sampler_ResourceTex,frac(advected)).a;
                envelope*=lerp(1,.48+.52*resource,saturate(_ResourceEnabled));
                float reveal=saturate((_Reveal*1.08-uv.x)/.08);
                float erosion=saturate((fabric-_Dissolve*1.15+.10)/.10);
                envelope*=reveal*erosion;
                opacity=saturate(opacity*envelope);
                // Transparent stone/metal parts must stop writing invisible
                // depth after their bounded entrance/retirement animation.
                clip(opacity-.00001);
                return half4(colour*opacity+accent*min(radiance,_Material<2.5?16:6)*emissionCoverage*envelope*(1+_Armed*.15),opacity);
            }
            ENDHLSL
        }
    }
}
