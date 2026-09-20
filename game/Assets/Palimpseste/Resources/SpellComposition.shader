Shader "Palimpseste/SpellComposition"
{
    Properties
    {
        [HDR] _Color ("Chromatic energy", Color) = (.3,.15,1,1)
        [HDR] _Accent ("Hot accent", Color) = (.7,.85,1,1)
        _Mode ("Seal / shell / spark / ribbon / wave / curtain", Float) = 0
        _Intensity ("Radiance", Float) = 2
        _Opacity ("Envelope", Float) = 1
        _Progress ("Lifecycle", Float) = 0
        _Seed ("Controlled variation", Float) = 0
        _Motif ("Runic / orbital / vortex / fracture / storm / petal", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Accent;
                float _Mode, _Intensity, _Opacity, _Progress, _Seed, _Motif;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : COLOR;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            float hash21(float2 p) { p = frac(p * float2(123.34,456.21)); p += dot(p,p+45.32); return frac(p.x*p.y); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
            }
            float flow(float2 p) { return noise(p)*.57 + noise(p*2.07+7.3)*.28 + noise(p*4.13+13.1)*.15; }
            float ring(float r,float radius,float width) { return 1-smoothstep(width,width+max(fwidth(r),.0012),abs(r-radius)); }
            float lineSegment(float2 p,float2 a,float2 b,float width)
            {
                float2 pa=p-a, ba=b-a;
                float d=length(pa-ba*saturate(dot(pa,ba)/max(dot(ba,ba),.00001)));
                return 1-smoothstep(width,width+.0016,d);
            }
            float seal(float2 p,float t)
            {
                float r=length(p), a=atan2(p.y,p.x), tau=6.2831853;
                float outer=ring(r,.91,.005)+ring(r,.875,.0025)+ring(r,.78,.004);
                float tick=pow(saturate(cos((a+t*.045)*48)),24)*smoothstep(.814,.821,r)*(1-smoothstep(.852,.86,r));
                float sector=floor((a/tau+.5+t*.003)*24);
                float2 local=float2(frac((a/tau+.5+t*.003)*24)-.5,(r-.705)*8);
                float glyph=lineSegment(local,float2(-.18,-.2),float2(.18,.2),.035);
                glyph+=lineSegment(local,float2(-.18,.2),float2(.18,-.2),.035)*step(.32,hash21(float2(sector,_Seed)));
                glyph+=lineSegment(local,float2(0,-.28),float2(0,.28),.035)*step(.2,hash21(float2(sector+4,_Seed)));
                glyph+=lineSegment(local,float2(-.2,.15),float2(.15,.15),.03)*step(.45,hash21(float2(sector+9,_Seed)));
                glyph*=smoothstep(.65,.66,r)*(1-smoothstep(.75,.76,r));
                float inner=ring(r,.606,.0035)+ring(r,.574,.002);
                float petalR=.34+.12*cos(a*6+t*.16);
                float petal=ring(r,petalR,.004);
                float star=ring(r,.43*cos(3.14159265/6)/max(cos(fmod(a+t*.12+tau,tau/6)-3.14159265/6),.1),.004);
                star+=ring(r,.43*cos(3.14159265/6)/max(cos(fmod(a-t*.09+tau+.52,tau/6)-3.14159265/6),.1),.003);
                float ornament=(_Motif>4.5 ? petal : star);
                float gem=pow(saturate(cos(a*8-t*.2)),36)*ring(r,.54,.023);
                return saturate(outer+tick+glyph*.7+inner+ornament*.75+gem+ring(r,.11,.004));
            }
            half4 frag(Varyings input) : SV_Target
            {
                float t=_Time.y+_Seed*4.7;
                float2 uv=input.uv, p=uv*2-1;
                float shape=0, hot=0, alpha=1;
                if (_Mode<.5)
                {
                    float r=length(p);
                    shape=seal(p,t);
                    float haze=exp(-r*r*6)*.045;
                    alpha=saturate(shape+haze)*(1-smoothstep(.96,1,r));
                    hot=shape*.25;
                }
                else if (_Mode<1.5)
                {
                    float fres=pow(saturate(1-abs(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)))),2.8);
                    float n=flow(uv*float2(7,5)+float2(-t*.4,t*.28));
                    float filaments=pow(saturate(1-abs(n-.51)*13),3);
                    shape=fres*(.24+filaments*.85)+filaments*.09;
                    alpha=shape*.64;
                    hot=filaments*fres*.45;
                }
                else if (_Mode<2.5)
                {
                    float r=length(p);
                    float core=pow(saturate(1-r),4);
                    float rays=pow(saturate(1-abs(p.x)),20)*pow(saturate(1-abs(p.y)),2);
                    rays+=pow(saturate(1-abs(p.y)),20)*pow(saturate(1-abs(p.x)),2);
                    shape=core+rays*.22; alpha=shape; hot=core*.8;
                }
                else if (_Mode<3.5)
                {
                    float n=flow(float2(uv.x*9-t*.7,uv.y*3+t*.14));
                    float spine=pow(saturate(1-abs(p.y+(n-.5)*.72)),3);
                    float breakUp=smoothstep(.21,.53,n);
                    shape=spine*(.25+breakUp*.75);
                    alpha=shape*sin(saturate(uv.x)*3.14159265);
                    hot=pow(spine,5)*breakUp*.75;
                }
                else if (_Mode<4.5)
                {
                    float r=length(p), a=atan2(p.y,p.x);
                    float n=flow(p*7+float2(t*.32,-t*.18));
                    float radius=.67+(n-.5)*.055;
                    shape=exp(-abs(r-radius)*48)+exp(-abs(r-radius)*12)*.22;
                    shape*=.74+.26*sin(a*31+t*3);
                    alpha=shape*(1-smoothstep(.96,1,r)); hot=pow(saturate(shape),4)*.65;
                }
                else
                {
                    float n=flow(float2(uv.x*13,uv.y*3-t*.52));
                    float strands=pow(saturate(1-abs(n-.56)*9),2);
                    float vertical=pow(saturate(1-uv.y),1.8)*smoothstep(0,.08,uv.y);
                    shape=strands*vertical;
                    alpha=shape*.65; hot=strands*vertical*.42;
                }
                half3 chroma=lerp(_Color.rgb,_Accent.rgb,saturate(hot));
                return half4(chroma*_Intensity*input.color.rgb, saturate(alpha*_Opacity*input.color.a));
            }
            ENDHLSL
        }
    }
}
