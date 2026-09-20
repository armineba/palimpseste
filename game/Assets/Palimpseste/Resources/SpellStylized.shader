Shader "Palimpseste/SpellStylized"
{
    Properties
    {
        [HDR] _Color ("Energy colour", Color) = (.28,.045,.8,1)
        [HDR] _AccentColor ("Luminous edge", Color) = (.72,.45,1,1)
        _Mode ("Seal / ribbon / veil / glint / corona / mist / lance", Float) = 0
        _Intensity ("Radiance", Range(0,8)) = 2
        _Opacity ("Envelope", Range(0,1)) = 1
        _FlowSpeed ("Flow speed", Float) = 1
        _Softness ("Softness", Range(0,1)) = .4
        _Phase ("Variation", Float) = 0
        _Age ("Lifecycle", Range(0,1)) = 0
        _Motif ("Runic / orbital / vortex / fracture / storm / petal", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+16" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            // Premultiplied emission: RGB contains coverage, including particle alpha.
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _AccentColor;
                float _Mode, _Intensity, _Opacity, _FlowSpeed, _Softness, _Phase, _Age, _Motif;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            static const float TAU = 6.283185307;
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float smoothNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(hash21(i), hash21(i+float2(1,0)), f.x),
                            lerp(hash21(i+float2(0,1)), hash21(i+1), f.x), f.y);
            }
            float softLine(float distance, float width)
            {
                float aa = max(fwidth(distance) * .8, .002);
                return 1 - smoothstep(width, width + aa, abs(distance));
            }
            float lineSegment(float2 p, float2 a, float2 b, float width)
            {
                float2 ba = b-a;
                float distance = length(p-a-ba*saturate(dot(p-a,ba)/max(dot(ba,ba),.00001)));
                return softLine(distance, width);
            }
            float sectorGate(float angle, float count, float phase, float width)
            {
                float segment = abs(frac(angle / TAU * count + phase) - .5);
                return 1-smoothstep(width, width+.045, segment);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv, p = uv*2-1;
                float t = _Time.y * _FlowSpeed + _Phase;
                float radius = length(p), angle = atan2(p.y, p.x);
                float coverage = 0, highlight = 0;
                float softness = saturate(_Softness);
                if (_Mode < .5)
                {
                    // Broad coloured footing under several legible engraved paths.
                    float outer = softLine(radius-.84,.009);
                    float inner = softLine(radius-.59,.006);
                    float orbit = softLine(radius-.73,.017) * sectorGate(angle,3,-t*.085,.31);
                    float ringGlow = exp(-abs(radius-.8)*18)*.12;
                    float haze = exp(-radius*radius*3.8)*.065;
                    float spoke = pow(saturate(cos(angle*12+t*.12)),36);
                    float ticks = spoke * smoothstep(.83,.845,radius) * (1-smoothstep(.89,.905,radius));
                    float glyphAngle = angle + t*.05;
                    float2 glyph = float2(frac(glyphAngle/TAU*12+.5)-.5,(radius-.66)*10);
                    float glyphBand = smoothstep(.60,.612,radius)*(1-smoothstep(.7,.71,radius));
                    float rune = lineSegment(glyph,float2(-.14,-.27),float2(.14,.27),.025);
                    rune += lineSegment(glyph,float2(-.14,.16),float2(.14,.16),.025);
                    rune += lineSegment(glyph,float2(.14,.27),float2(.14,-.05),.025);
                    rune *= glyphBand;
                    float rosette = softLine(radius-(.34+.075*cos(angle*6+t*.18)),.007);
                    float swirls = softLine(radius-(.37+.105*sin(angle*3+t*.35)),.011);
                    float ornament = (_Motif > 4.5 ? rosette : swirls);
                    float breathing = .87+.13*sin(t*1.8);
                    coverage = (outer*.53+inner*.38+orbit*.8+ticks*.55+rune*.5+ornament*.43+ringGlow+haze)*breathing;
                    coverage *= 1-smoothstep(.94,1,radius);
                    highlight = saturate(orbit*.55+ticks*.32);
                }
                else if (_Mode < 1.5)
                {
                    // The UV centreline stays clear. Noise only bends broad luminous strands.
                    float flow = smoothNoise(float2(uv.x*5-t*.6,_Phase+uv.y*1.6));
                    float centre = p.y + sin(uv.x*10-t*2.2)*.07 + (flow-.5)*.14;
                    float width = lerp(.39,.24,uv.x);
                    float body = exp(-pow(abs(centre)/max(width,.05),2));
                    float hotLine = exp(-abs(centre+.10*sin(uv.x*8-t))*19);
                    float fineLine = exp(-abs(centre-.22)*30)*.22;
                    float ends = smoothstep(0,.08,uv.x) * (1-smoothstep(.84,1,uv.x));
                    float pulse = .66+.34*pow(saturate(.5+.5*cos(uv.x*10-t*3)),3);
                    float feather = 1-smoothstep(.66,1,abs(p.y));
                    coverage = (body*.49+hotLine*.7+fineLine)*ends*pulse*feather;
                    highlight = saturate(hotLine*.73+body*.08);
                }
                else if (_Mode < 2.5)
                {
                    // A curtain with separated, upward flowing tapered strands and open space.
                    float lanes = uv.x*11;
                    float lane = floor(lanes);
                    float seed = hash21(float2(lane,_Phase));
                    float bend = sin(uv.y*5-t*(.5+seed*.4)+lane)*.22;
                    float x = frac(lanes)-.5+bend;
                    float strandHeight = .35+seed*.65;
                    float top = 1-smoothstep(strandHeight*.45,strandHeight,uv.y);
                    float tipWidth = lerp(.21,.035,saturate(uv.y/max(strandHeight,.1)));
                    float core = exp(-abs(x)/max(tipWidth,.025)*2.7);
                    float halo = exp(-abs(x)*6)*.23;
                    float pulse = .63+.37*sin(uv.y*11-t*3+seed*TAU);
                    float base = smoothstep(0,.075,uv.y);
                    coverage = (core+halo)*top*base*pulse;
                    coverage *= smoothstep(0,.06,uv.x)*(1-smoothstep(.94,1,uv.x));
                    highlight = core*.66;
                }
                else if (_Mode < 3.5)
                {
                    // A clean star silhouette with a coloured halo, not a rectangular sprite.
                    float diamond = abs(p.x)*.92+abs(p.y)*.74;
                    float core = exp(-diamond*12);
                    float horizontal = exp(-abs(p.y)*48)*pow(saturate(1-abs(p.x)),2.4);
                    float vertical = exp(-abs(p.x)*48)*pow(saturate(1-abs(p.y)),2.4);
                    float halo = exp(-radius*radius*15)*.24;
                    coverage = (core+horizontal*.61+vertical*.85+halo)*(1-smoothstep(.83,1,radius));
                    highlight = saturate(core+horizontal*.25+vertical*.34);
                }
                else if (_Mode < 4.5)
                {
                    // Angular rim, detached broken arcs and light teeth radiating outwards.
                    float polygonAngle = fmod(angle+TAU+t*.055,TAU/12)-TAU/24;
                    float polygonRadius = .71*cos(TAU/24)/max(cos(polygonAngle),.8);
                    float radial = radius-polygonRadius;
                    float gate = sectorGate(angle,9,-t*.11,.36);
                    float ring = softLine(radial,.016)*gate;
                    float thin = softLine(radius-.85,.006)*sectorGate(angle,6,t*.065,.29);
                    float fire = pow(saturate(cos(angle*18-t*.2)),26);
                    float teeth = fire*smoothstep(.68,.74,radius)*(1-smoothstep(.77,.96,radius));
                    float halo = exp(-abs(radial)*21)*.18*gate;
                    coverage = (ring+thin*.46+teeth*.58+halo)*(1-smoothstep(.96,1,radius));
                    highlight = saturate(ring*.72+teeth*.43);
                }
                else if (_Mode < 5.5)
                {
                    // Smooth open mist lobes; broad gradients avoid a crumpled-foil appearance.
                    float n = smoothNoise(p*2.3+float2(t*.12,-t*.2));
                    float curl = sin(angle*3+t*.45)*.10 + (n-.5)*.18;
                    float body = pow(saturate(1-radius+curl),lerp(2.4,1.5,softness));
                    float wisps = .58+.42*smoothNoise(p*3.3+float2(-t*.15,t*.25));
                    float softEdge = 1-smoothstep(.65,.98,radius);
                    coverage = body*wisps*softEdge*.6;
                    highlight = body*.16;
                }
                else
                {
                    // A bright rooted, tapered energy spear, suitable for stretched billboards.
                    float lengthFade = smoothstep(0,.12,uv.y)*(1-smoothstep(.63,1,uv.y));
                    float taper = lerp(.19,.018,pow(saturate(uv.y),.8));
                    float centre = exp(-abs(p.x)/max(taper,.012)*2.5);
                    float broad = exp(-abs(p.x)/max(taper*3,.04)*2.2)*.23;
                    float tip = exp(-length(float2(p.x*5,(uv.y-.76)*3))*12)*.25;
                    coverage = (centre+broad)*lengthFade+tip;
                    coverage *= 1-smoothstep(.65,1,abs(p.x));
                    highlight = saturate(centre*.85+tip);
                }

                // Keep chroma around the hot core and honour particle colour-over-lifetime alpha.
                float alpha = saturate(coverage * saturate(_Opacity) * input.color.a * _Color.a);
                half3 colour = lerp(_Color.rgb,_AccentColor.rgb,saturate(highlight)*.83);
                half3 emission = max(colour,0)*max(input.color.rgb,0)*clamp(_Intensity,0,8)*alpha;
                return half4(emission,alpha);
            }
            ENDHLSL
        }
    }
}
