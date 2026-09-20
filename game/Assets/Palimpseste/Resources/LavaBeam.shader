Shader "Palimpseste/LavaBeam"
{
    Properties
    {
        _BaseColor ("Beam Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Name "LavaFlow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Flow follows the line UV from emitter to endpoint. The width
                // and geometry still come only from the validated spell packet.
                float along = input.uv.x;
                float across = abs(input.uv.y * 2.0 - 1.0);
                float time = _Time.y;
                float vein = sin(along * 94.0 - time * 13.0 + sin(along * 37.0 - time * 4.0) * 1.5);
                float fleck = sin(along * 157.0 - time * 19.0 + input.uv.y * 9.0);
                float fissure = smoothstep(0.57, 0.84, vein * 0.55 + fleck * 0.3 + 0.45);
                float center = 1.0 - smoothstep(0.12, 0.9, across);
                float heat = saturate(0.31 + center * 0.49 + fleck * 0.18 - fissure * 0.4);
                float3 cooled = float3(0.31, 0.055, 0.018);
                float3 molten = float3(1.0, 0.29, 0.035);
                float3 hot = float3(1.0, 0.8, 0.29);
                float3 rgb = lerp(cooled, molten, smoothstep(0.07, 0.62, heat));
                rgb = lerp(rgb, hot, smoothstep(0.58, 0.94, heat) * 0.78);
                float edge = 1.0 - smoothstep(0.66, 1.0, across);
                return half4(rgb * _BaseColor.rgb, edge * input.color.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
