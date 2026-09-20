Shader "Palimpseste/SpellSpectralCloth"
{
    Properties
    {
        [HDR] _Color ("Spectral fabric", Color) = (.25,.07,.5,1)
        [HDR] _Accent ("Pearlescent edges", Color) = (.75,.63,1,1)
        _Opacity ("Opacity", Float) = .78
        _Torn ("Tattered flowing veils", Float) = 1
        _Seed ("Variation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
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
                float amount=_Torn*input.uv.x;
                input.positionOS.y+=sin(input.uv.x*11-_Time.y*3.3+_Seed)*.10*amount;
                input.positionOS.x+=sin(input.uv.x*8-_Time.y*2.4+_Seed)*.08*amount;
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
                // Broad flowing folds retain the silhouette instead of carving foil-like holes.
                float2 p=uv*float2(5,2.5)+float2(-_Time.y*.28,_Seed);
                float n=noise(p)*.78+noise(p*1.9+4.3)*.22;
                float edge=abs(uv.y*2-1);
                float threshold=_Torn*(.06+uv.x*.11+pow(edge,5)*.23);
                float fabric=smoothstep(threshold-.10,threshold+.13,n);
                float fres=pow(saturate(1-abs(dot(SafeNormalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)))),2.5);
                float folds=.5+.5*sin(uv.y*12+uv.x*5+n*1.8-_Time.y*.4);
                float tail=lerp(1,1-smoothstep(.66,1,uv.x),_Torn);
                float edgeFade=lerp(1,1-smoothstep(.55,1,edge),_Torn*.78);
                float a=fabric*tail*edgeFade*_Opacity*(.43+folds*.25+fres*.22);
                half3 baseColor=_Color.rgb*(.44+n*.24+folds*.16);
                half3 radiance=_Accent.rgb*(pow(fres,2)*.47+pow(folds,6)*.14);
                return half4((baseColor+radiance)*a,a);
            }
            ENDHLSL
        }
    }
}
