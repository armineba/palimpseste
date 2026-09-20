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
                float2 p=uv*float2(20,8)+float2(-_Time.y*.4,_Seed);
                float n=noise(p)*.58+noise(p*2.1)*.28+noise(p*4.3)*.14;
                float edge=abs(uv.y*2-1);
                float threshold=_Torn*(.14+uv.x*.18+pow(edge,8)*.31);
                float fabric=smoothstep(threshold-.024,threshold+.028,n);
                float tears=(1-smoothstep(.013,.058,abs(n-threshold)))*_Torn;
                float fres=pow(saturate(1-abs(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)))),2.5);
                float folds=.5+.5*sin(uv.y*27+uv.x*9+n*5);
                float tail=lerp(1,1-smoothstep(.92,1,uv.x),_Torn);
                float a=fabric*tail*_Opacity*(.57+folds*.25+fres*.18);
                half3 baseColor=_Color.rgb*(.28+n*.5+folds*.26);
                half3 radiance=_Accent.rgb*(tears*1.1+pow(fres,2)*.38+pow(folds,8)*.10);
                return half4((baseColor+radiance)*a,a);
            }
            ENDHLSL
        }
    }
}
