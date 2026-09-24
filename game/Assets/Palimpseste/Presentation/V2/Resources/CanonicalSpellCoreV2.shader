Shader "Palimpseste/CanonicalSpellV2"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.7,0.8,1,1)
        _SurfaceTex ("TinyPlay plasma RGB (MIT)", 2D) = "white" {}
        _NoiseTex ("TinyPlay noise (MIT)", 2D) = "gray" {}
        _ResourceTex ("Curated Kenney sprite (CC0)", 2D) = "white" {}
        _Opacity ("Opacity", Float) = 0.8
        _Emission ("Emission", Float) = 1
        _Surface ("Surface", Float) = 4
        _CoreOnly ("Uniform structural preview", Float) = 0
        _Secondary ("Secondary energy", Float) = 0
        _Particle ("Atmospheric particle", Float) = 0
        _Flow ("Longitudinal flow", Float) = 1
        _SecondaryMotion ("Secondary motion weight", Float) = 0
        _LoopEnabled ("Periodic stable phase", Float) = 0
        _LoopPhase ("Continuous stable cycle count", Float) = 0
        _Age ("Canonical seconds", Float) = 0
        _Envelope ("Envelope", Float) = 1
        _Reveal ("Reveal", Float) = 1
        _Dissolve ("Dissolve", Float) = 0
        _HitFlash ("Contact flash", Float) = 0
        _Armed ("Armed", Float) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination blend", Float) = 10
        [HideInInspector] _ZWrite ("Depth write", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Palimpseste/Resources/SourcedNoise/SimplexNoise3D.hlsl"
            #include "Assets/Palimpseste/Resources/SourcedSurfaces/KeijiroDivergenceFreeNoise.hlsl"
            TEXTURE2D(_SurfaceTex); SAMPLER(sampler_SurfaceTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_ResourceTex); SAMPLER(sampler_ResourceTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Opacity, _Emission, _Surface, _CoreOnly, _Secondary, _Particle;
            float _Flow, _Age, _Envelope, _Reveal, _Dissolve, _HitFlash, _Armed, _SecondaryMotion;
            float _LoopEnabled, _LoopPhase;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                clip(_Envelope - .001);
                if (_CoreOnly > .5) return half4(.84,.87,.90,saturate(_Envelope));
                float2 uv = input.uv;
                float3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1 - abs(dot(normalize(input.normalWS),view)),2.3);
                // Both texture layers flow in canonical longitudinal/radial UVs.
                // TinyPlay PlasmaShader technique: opposing sampled RGB layers.
                float2 canonicalUV = float2(uv.x * 2.2, uv.y * 2);
                float2 flowUV = canonicalUV + float2(-_Age * .22 * _Flow, _Age * .07);
                float2 secondUV = flowUV * float2(-.7,1.2) + .23;
                float2 noiseUV = flowUV * 1.3;
                if (_LoopEnabled > .5)
                {
                    // Integer texture offsets close the requested cycle exactly;
                    // each layer keeps its own canonical coordinates. Multiplying
                    // an animated UV by .7 afterward would break that seam.
                    flowUV = canonicalUV + float2(-2,1) * _LoopPhase;
                    secondUV = canonicalUV * float2(-.7,1.2) + .23 + float2(1,-2) * _LoopPhase;
                    noiseUV = canonicalUV * 1.3 + float2(1,2) * _LoopPhase;
                }
                float3 plasmaA = SAMPLE_TEXTURE2D(_SurfaceTex,sampler_SurfaceTex,flowUV).rgb;
                float3 plasmaB = SAMPLE_TEXTURE2D(_SurfaceTex,sampler_SurfaceTex,secondUV).rgb;
                float noise = SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,noiseUV).r;
                // Keijiro's Unlicense divergence-free operator enriches the
                // surface only. It cannot displace or fragment the silhouette.
                float3 domain = float3(uv.x * 3,cos(uv.y*6.283185)*1.4,sin(uv.y*6.283185)*1.4 + _Age*.16);
                if (_LoopEnabled > .5)
                    domain = float3(uv.x*3,cos(uv.y*6.283185)*1.4,sin(uv.y*6.283185)*1.4) +
                        float3(cos(_LoopPhase*6.283185),sin(_LoopPhase*6.283185),0) * .32;
                float3 curl = PalimpsesteDFNoise3D(domain,domain+float3(7.1,3.2,11.9));
                float temporalPhase = _LoopEnabled > .5 ? _LoopPhase * 18.849555 : _Age * 4 * _Flow;
                float filament = pow(saturate(.5+.5*sin(uv.y*37.699 + uv.x*18 - temporalPhase + curl.x*.2*_SecondaryMotion)),12);
                float surface = .65 + .22*noise + .13*fresnel;
                float3 tint = _BaseColor.rgb;
                if (_Surface > .5 && _Surface < 1.5) { surface = .45 + dot(plasmaA*plasmaB,float3(.5,.3,.2))*.8; tint *= .75 + plasmaA*.55; }
                else if (_Surface > 1.5 && _Surface < 2.5) surface = .4 + .5*fresnel + .1*noise;
                else if (_Surface > 2.5 && _Surface < 3.5) surface = .6 + .25*noise + .15*filament;
                else if (_Surface > 3.5) surface = .48 + .22*noise + .3*filament;
                float alpha = _Opacity * _Envelope * surface;
                if (_Secondary > .5) alpha *= filament;
                if (_Particle > .5)
                {
                    float radial = saturate(1-length(uv*2-1));
                    alpha *= radial*radial*input.color.a;
                    alpha *= SAMPLE_TEXTURE2D(_ResourceTex,sampler_ResourceTex,uv).a;
                    tint *= input.color.rgb;
                }
                // Dissolution traverses the canonical longitudinal coordinate;
                // noise has bounded influence and never invents disconnected parts.
                alpha *= smoothstep(_Dissolve-.06,_Dissolve+.04,1-uv.x*.72);
                float luminance = .65 + _Emission*(.4+.45*filament+.2*fresnel);
                return half4(tint*luminance + _HitFlash*.85, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
