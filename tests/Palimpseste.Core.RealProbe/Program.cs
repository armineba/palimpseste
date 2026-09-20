using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Palimpseste.Contracts;
using Palimpseste.Core;

if (args.Length is < 3 or > 4)
{
    Console.Error.WriteLine("Usage: Palimpseste.Core.RealProbe <description.json> <plan.json> <ink.png> [new-private-output-directory]");
    return 64;
}

var descriptionBytes = File.ReadAllBytes(args[0]);
var planBytes = File.ReadAllBytes(args[1]);
var inkBytes = File.ReadAllBytes(args[2]);
var descriptionIssues = SpellCompiler.ValidateDescriptionJson(descriptionBytes);
if (descriptionIssues.Count != 0) return Reject("description", descriptionIssues);

var description = ContractJson.DeserializeStrict<SpellDescription>(descriptionBytes, "spell-description");
var resolved = GeometryResolver.Resolve(inkBytes, description);
if (!resolved.Success) return Reject("geometry", resolved.Issues);

var planIssues = SpellCompiler.ValidatePlanJson(descriptionBytes, planBytes,
    resolved.GeometryJson, resolved.MaskPng);
if (planIssues.Count != 0) return Reject("plan", planIssues);

// These IDs are deliberately synthetic: this probe checks the controlled compiler,
// but it does not publish artifacts or claim that Unity can download this payload.
var geometryIds = resolved.GeometryJson.Keys.OrderBy(x => x, StringComparer.Ordinal)
    .Select((key, index) => (key, index))
    .ToDictionary(x => x.key, x => "probe-geometry-" + x.index, StringComparer.Ordinal);
var maskIds = resolved.MaskPng.Keys.OrderBy(x => x, StringComparer.Ordinal)
    .Select((key, index) => (key, index))
    .ToDictionary(x => x.key, x => "probe-mask-" + x.index, StringComparer.Ordinal);
var result = SpellCompiler.Compile(new CompilationInput
{
    DescriptionJson = descriptionBytes,
    PlanJson = planBytes,
    GeometryJson = resolved.GeometryJson,
    MaskPng = resolved.MaskPng,
    GeometryArtifactIds = geometryIds,
    MaskArtifactIds = maskIds,
    SpellId = "probe-only",
    ParchmentId = "probe-only",
    SignatureSeedHex = "0000000000000000",
    CreatedAt = "2026-09-20T00:00:00Z",
    Provenance = new SpellProvenance
    {
        mode = "fixture", capture_sha256 = null, reference_sha256 = null,
        model_a = null, model_b = null,
        prompt_a_version = "probe.compiler", prompt_b_version = "probe.compiler",
        response_a_id = null, response_b_id = null
    }
});
if (!result.Success) return Reject("compile", result.Issues);

if (args.Length == 4)
{
    var output = Path.GetFullPath(args[3]);
    if (Directory.Exists(output) || File.Exists(output))
    {
        Console.Error.WriteLine("Output path already exists; refusing to overwrite it.");
        return 73;
    }
    Directory.CreateDirectory(output);
    var assets = Path.Combine(output, "artifacts");
    Directory.CreateDirectory(assets);
    File.WriteAllBytes(Path.Combine(output, "packet.json"), result.PayloadUtf8);
    foreach (var pair in resolved.GeometryJson)
        File.WriteAllBytes(Path.Combine(assets, geometryIds[pair.Key]), pair.Value);
    foreach (var pair in resolved.MaskPng)
        File.WriteAllBytes(Path.Combine(assets, maskIds[pair.Key]), pair.Value);
    Console.WriteLine("probe_output=" + output);
}

Console.WriteLine("description_sha256=" + SpellCompiler.Sha256(descriptionBytes));
Console.WriteLine("plan_sha256=" + SpellCompiler.Sha256(planBytes));
Console.WriteLine("ink_sha256=" + SpellCompiler.Sha256(inkBytes));
Console.WriteLine("geometry_ids=" + string.Join(",", geometryIds.Keys.OrderBy(x => x, StringComparer.Ordinal)));
Console.WriteLine("mask_count=" + maskIds.Count);
Console.WriteLine("compiled_probe_sha256=" + result.PayloadSha256);
Console.WriteLine("max_end_tick=" + result.Spell.resource_bounds.max_end_tick);
Console.WriteLine("max_instances=" + result.Spell.resource_bounds.max_instances);
Console.WriteLine("max_effect_applications=" + result.Spell.resource_bounds.max_effect_applications);
Console.WriteLine("result=controlled_compilation_accepted_probe_only");
return 0;

static int Reject(string stage, IReadOnlyList<ValidationIssue> issues)
{
    Console.Error.WriteLine("result=" + stage + "_rejected");
    foreach (var issue in issues) Console.Error.WriteLine(issue);
    return 1;
}
