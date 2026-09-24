// Keijiro VfxGraphAssets, commit 5013c195305288fab61cd71f72a4628dbe9d0ea4.
// Packages/jp.keijiro.vfxgraphassets/Subgraph/Divergence Free Noise 3D.vfxoperator
// and Shader/Textureless Strip.shadergraph. Unlicense; original notice retained
// in Resources/SourcedVfx/Keijiro-VfxGraphAssets-Unlicense.txt.
// SimplexNoiseGrad is provided by the already imported MIT NoiseShader source.
#ifndef PALIMPSESTE_SOURCED_KEIJIRO_SURFACES
#define PALIMPSESTE_SOURCED_KEIJIRO_SURFACES

// The operator's HLSL body is retained; only its namespace is prefixed.
float3 PalimpsesteDFNoise3D(float3 p1, float3 p2)
{
    float3 n1 = SimplexNoiseGrad(p1).xyz;
    float3 n2 = SimplexNoiseGrad(p2).xyz;
    return cross(n1, n2);
}

// Port of Textureless Strip's UV remapping and squared analytical coverage.
// The denominator guard handles the exact centre without generating infinity.
float PalimpsesteTexturelessStrip(float2 uv, float contrast)
{
    float x = (smoothstep(0.0, 0.75, uv.x) + pow(saturate(uv.x), 10.0)) * 0.5;
    float radius = length(float2(x, uv.y) * 2.0 - 1.0);
    float gain = exp(lerp(-2.71828, 2.71828, saturate(contrast)));
    float coverage = saturate(gain / max(radius, 0.0001) - gain);
    return coverage * coverage;
}

#endif
