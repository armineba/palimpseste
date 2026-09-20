Shader "Palimpseste/SpellImageConstruction"
{
    Properties
    {
        [HDR] _Color ("Image palette", Color) = (.3,.6,1,1)
        _Material ("Glass / energy / mist / stone / metal", Float) = 0
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
            // Ashima/stegu simplex, Unity adaptation by Keijiro; MIT license
            // retained in SourcedNoise/LICENSE.txt alongside reviewed sources.
            #include "SourcedNoise/SimplexNoise3D.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Material, _Shape, _Opacity, _Emission, _Envelope, _Seed, _Armed;
                float _Reveal, _Dissolve;
                float4 _BehaviorFlow;
                float _BehaviorAge, _ResourceEnabled, _ResourceParticle, _BehaviorEnabled, _BehaviorMotion;
            CBUFFER_END
            TEXTURE2D(_ResourceTex); SAMPLER(sampler_ResourceTex);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings vert(Attributes input)
            {
                Varyings output;
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
                if (_Material<.5)
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
