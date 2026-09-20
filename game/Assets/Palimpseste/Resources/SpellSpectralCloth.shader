Shader "Palimpseste/SpellSpectralCloth"
{
    Properties
    {
        [HDR] _Color ("Spectral fabric", Color) = (.25,.07,.5,1)
        [HDR] _Accent ("Pearlescent edges", Color) = (.75,.63,1,1)
        _Opacity ("Opacity", Float) = .78
        _Torn ("Tattered flowing veils", Float) = 1
        _Seed ("Variation", Float) = 0
        [Toggle] _DepthWrite ("Write depth for outer hood", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Face culling", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite [_DepthWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _Accent;
                float _Opacity, _Torn, _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; float3 normalWS:TEXCOORD2; };
            Varyings vert(Attributes input)
            {
                float amount=_Torn*pow(saturate(input.uv.x),.8);
                input.positionOS.y+=(sin(input.uv.x*11-_Time.y*2.5+_Seed)*.075+
                    sin(input.uv.y*13+input.uv.x*18-_Time.y*3.1)*.024)*amount;
                input.positionOS.x+=sin(input.uv.x*8+input.uv.y*3-_Time.y*1.9+_Seed)*.065*amount;
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
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
            }
            half4 frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                // Holes are anchored to the cloth. Only the mesh billows: the fabric does
                // not boil away or acquire moving metallic highlights.
                float2 grain=uv*float2(14,5)+float2(_Seed*4.13,_Seed*1.73);
                float broad=noise(uv*float2(4.1,3.2)+_Seed);
                float2 warped=grain+float2(broad-.5,noise(uv*float2(3,4)+7.1)-.5)*.9;
                float lace=noise(warped)*.60+noise(warped*2.05+9.3)*.27+
                    noise(warped*4.1+17.7)*.13;
                float2 detailUv=uv*float2(56,24)+float2(_Seed*7.2,_Seed*3.1);
                detailUv+=float2(noise(uv*float2(12,5)+_Seed),noise(uv*float2(9,8)+2.4))*2.7;
                float coarseFibre=noise(detailUv);
                float mediumFibre=noise(detailUv*2.07+8.3);
                float fineFibre=noise(detailUv*4.19+17.2);
                float detailFootprint=max(length(ddx(detailUv)),length(ddy(detailUv)));
                float mediumVisible=1-saturate(detailFootprint*.75);
                float fineVisible=1-saturate(detailFootprint*1.45);
                float fibreField=coarseFibre*.55+lerp(.5,mediumFibre,mediumVisible)*.30+
                    lerp(.5,fineFibre,fineVisible)*.15;
                float edge=abs(uv.y*2-1);
                float rootProtection=smoothstep(.055,.20,uv.x);
                float tornGrain=(fibreField-.5)*.20;
                float holeDistance=lerp(.6,lace+tornGrain-(.28+uv.x*.105+edge*.025),rootProtection);
                float raggedEdge=.055+.15*noise(uv*float2(22,3)+_Seed*2.3)+
                    .04*noise(uv*float2(75,9)+13.7);
                float edgeDistance=1-edge-raggedEdge*lerp(.35,1,rootProtection)+tornGrain*.55;
                float distanceToCloth=min(holeDistance,edgeDistance);
                float aa=max(fwidth(distanceToCloth),.019);
                float laceCoverage=smoothstep(-aa,aa,distanceToCloth);
                float fabric=lerp(1,laceCoverage,_Torn);

                // A narrow luminous fray defines the real transparent tears. The main
                // surface remains darker and fibrous, rather than shiny satin or foil.
                float fray=(1-smoothstep(.002,max(.037,aa*1.5),abs(distanceToCloth)))*_Torn;
                float brokenFray=smoothstep(.30,.71,coarseFibre*.55+mediumFibre*.45);
                fray*=.12+brokenFray*.88;
                // Domain-warped cloud fibres replace parallel lines and shiny membranes.
                float fibres=pow(saturate(1-abs(fibreField-.5)*5.5),3);
                float fibreShade=smoothstep(.27,.70,fibreField);
                float density=lerp(.58,1.12,fibreShade);

                float3 normal=SafeNormalize(input.normalWS);
                float fres=pow(saturate(1-abs(dot(normal,GetWorldSpaceNormalizeViewDir(input.positionWS)))),3.1);
                float foldField=noise(uv*float2(7.5,5.7)+float2(_Seed,2.1));
                float folds=smoothstep(.25,.78,foldField*.63+broad*.37);
                folds=lerp(.35+.4*smoothstep(.2,.8,broad),folds,_Torn);
                float diffuse=abs(dot(normal,normalize(float3(-.35,.8,.48))));
                float grazingLight=lerp(.7+.3*diffuse,.38+.62*diffuse,_Torn);
                float tail=lerp(1,1-smoothstep(.94,1,uv.x),_Torn);
                // Energy currents occupy only part of the envelope. The
                // sheet is an integration surface, never a solid fabric body.
                float surfaceDensity=lerp(.12,(.075+folds*.055)*density,_Torn);
                float a=saturate(fabric*tail*_Opacity*(surfaceDensity+fray*.18));
                half3 clothColour=_Color.rgb*(.07+folds*.13)+_Accent.rgb*.01;
                clothColour*=grazingLight*lerp(lerp(.91,1.06,fibreShade),lerp(.55,1.22,fibreShade),_Torn);
                float innerFibres=(fibres*.105+fibreShade*.025)*(.30+folds*.70);
                half3 radiance=_Accent.rgb*(fray*.19+fres*lerp(.10,.045,_Torn)+
                    innerFibres*lerp(.16,1,_Torn));
                half3 emission=radiance*fabric*tail*saturate(_Opacity);
                return half4(clothColour*a+emission,a);
            }
            ENDHLSL
        }
    }
}
