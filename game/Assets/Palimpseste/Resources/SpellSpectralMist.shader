Shader "Palimpseste/SpellSpectralMist"
{
    Properties
    {
        [HDR] _Color ("Spectral vapour", Color) = (.28,.12,.62,1)
        [HDR] _Accent ("Pearlescent energy", Color) = (.82,.72,1,1)
        _Opacity ("Energy envelope", Range(0,1)) = .85
        _Seed ("Flow variation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            // Volume emission is already integrated and premultiplied along the ray.
            // No opaque surface, lighting normal or depth write is used.
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _Accent;
                float _Opacity, _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS=TransformWorldToHClip(output.positionWS);
                return output;
            }
            float hash31(float3 p)
            {
                p=frac(p*.1031);
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float noise3(float3 p)
            {
                float3 i=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                float lower=lerp(lerp(hash31(i),hash31(i+float3(1,0,0)),f.x),
                    lerp(hash31(i+float3(0,1,0)),hash31(i+float3(1,1,0)),f.x),f.y);
                float upper=lerp(lerp(hash31(i+float3(0,0,1)),hash31(i+float3(1,0,1)),f.x),
                    lerp(hash31(i+float3(0,1,1)),hash31(i+1),f.x),f.y);
                return lerp(lower,upper,f.z);
            }
            float cloudNoise(float3 p)
            {
                // Three fixed octaves. Coherent advection avoids rapidly boiling noise.
                return noise3(p)*.58+noise3(p*2.03+float3(9.2,3.7,6.1))*.28+
                    noise3(p*4.09+float3(17.2,11.8,4.3))*.14;
            }
            float2 rayBox(float3 origin,float3 direction)
            {
                float3 safeDirection=sign(direction+.000001)*max(abs(direction),.0001);
                float3 first=(-.5-origin)/safeDirection;
                float3 second=(.5-origin)/safeDirection;
                float3 nearPlanes=min(first,second), farPlanes=max(first,second);
                return float2(max(max(nearPlanes.x,nearPlanes.y),nearPlanes.z),
                    min(min(farPlanes.x,farPlanes.y),farPlanes.z));
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 cameraWS=GetCameraPositionWS();
                float3 origin=TransformWorldToObject(cameraWS);
                float3 direction=SafeNormalize(TransformWorldToObjectDir(input.positionWS-cameraWS,false));
                if (unity_OrthoParams.w>.5)
                {
                    float3 forwardWS=-GetWorldSpaceNormalizeViewDir(input.positionWS);
                    origin=TransformWorldToObject(input.positionWS-forwardWS*50);
                    direction=SafeNormalize(TransformWorldToObjectDir(forwardWS,false));
                }
                float2 bounds=rayBox(origin,direction);
                bounds.x=max(0,bounds.x);

                // Clip against the laboratory's opaque depth, including a target entering
                // the vapour. The container itself contributes no visible cube or sphere.
                float2 screenUv=input.positionHCS.xy/_ScaledScreenParams.xy;
                float rawDepth=SampleSceneDepth(screenUv);
                float opaqueEyeDepth=LinearEyeDepth(rawDepth,_ZBufferParams);
                if (unity_OrthoParams.w>.5)
                {
                    #if UNITY_REVERSED_Z
                        opaqueEyeDepth=lerp(_ProjectionParams.z,_ProjectionParams.y,rawDepth);
                    #else
                        opaqueEyeDepth=lerp(_ProjectionParams.y,_ProjectionParams.z,rawDepth);
                    #endif
                }
                float originEye=-TransformWorldToView(TransformObjectToWorld(origin)).z;
                float eyePerUnit=-mul((float3x3)GetWorldToViewMatrix(),TransformObjectToWorldDir(direction,false)).z;
                if (eyePerUnit>.0001)
                    bounds.y=min(bounds.y,(opaqueEyeDepth-originEye)/eyePerUnit);
                if (bounds.y<=bounds.x) return 0;

                const int STEP_COUNT=24;
                float stepLength=(bounds.y-bounds.x)/STEP_COUNT;
                float jitter=hash31(float3(floor(input.positionHCS.xy),_Seed*19.7));
                float travel=bounds.x+stepLength*(.12+jitter*.76);
                float transmittance=1;
                float3 radiance=0;
                float t=_Time.y*.24;
                float3 flow=float3(_Seed*7.1,-t,_Seed*3.2+t*.32);
                float opacity=saturate(_Opacity)*saturate(_Color.a);
                float3 vapourColour=clamp(_Color.rgb,0,4);
                float3 hotColour=clamp(_Accent.rgb,0,4);
                [loop]
                for (int stepIndex=0;stepIndex<STEP_COUNT;stepIndex++)
                {
                    float3 p=origin+direction*travel;
                    // Strictly contained within r<.49: either a .5 sphere or a unit cube
                    // can be used as the volume renderer without cropping luminous wisps.
                    float sphereEnvelope=1-smoothstep(.40,.49,length(p));
                    if (sphereEnvelope>.0001)
                    {
                        float n=cloudNoise(p*8+flow);
                        float3 distorted=p+float3(sin(p.y*9+t*1.3)*.026,
                            (n-.5)*.055,cos(p.y*7-t)*.024);
                        float3 q=distorted/float3(.43,.47,.42);
                        float hoodRadius=length(float2(q.x,(q.y-.08)*.84));
                        float archRadius=.63+sin(q.z*4.1+t*.6)*.035+(n-.5)*.13;
                        float archDistance=(hoodRadius-archRadius)*5.6;
                        float arch=exp(-archDistance*archDistance);
                        arch*=smoothstep(-.83,-.22,q.y);
                        float2 openingPoint=float2(p.x/.20,(p.y+.02)/.26);
                        float opening=exp(-dot(openingPoint,openingPoint));
                        opening*=smoothstep(-.12,.23,p.z);
                        // Broken arches of vapour leave genuine empty space around the
                        // inner light; a uniform volume fill would read as a purple orb.
                        float cloud=smoothstep(.38,.70,n)*(.025+arch*1.2);
                        cloud*=sphereEnvelope*(1-opening*.86);

                        // The inner light is an irregular volume with exponential falloff,
                        // never an opaque face, hard ellipse, sphere or textured billboard.
                        float3 corePoint=distorted-float3(0,-.015,.105);
                        corePoint.x+=sin(p.y*16-t*1.6)*.023;
                        float3 coreQ=corePoint/float3(.065,.235,.077);
                        float core=exp(-dot(coreQ,coreQ)*2.1);
                        core*=lerp(.18,1,smoothstep(.27,.72,n))*sphereEnvelope;
                        float thread=pow(saturate(1-abs(n-.55)*6),5)*arch;
                        float cloudDensity=cloud*3.2*opacity;
                        float coreDensity=core*5.2*opacity;
                        float density=cloudDensity+coreDensity;
                        float sampleAlpha=1-exp(-density*stepLength);
                        float3 scattered=(vapourColour*2.05+hotColour*thread*.38)*cloudDensity;
                        scattered+=hotColour*coreDensity*2.7;
                        float3 sampleColour=scattered/max(density,.0001);
                        radiance+=transmittance*sampleAlpha*sampleColour;
                        transmittance*=1-sampleAlpha;
                    }
                    travel+=stepLength;
                }
                return half4(min(radiance,8),1-transmittance);
            }
            ENDHLSL
        }
    }
}
