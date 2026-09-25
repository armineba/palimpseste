using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Palimpseste.Provider;

/// <summary>Reviewed construction knowledge, frozen with each new V2 research dossier.
/// This is data retrieval and validation, not skill execution or model filesystem access.</summary>
public static class UnityGodMethods
{
    public const string SkillVersion = "1.0";
    public const string ContextSchema = "sp.unity-god-context/1.0";
    public const string ReceiptSchema = "sp.unity-god-receipt/1.0";
    public static readonly string[] SourceIds = ["tinyplay_urp_shaders_collection", "xtaja_vfx_shader",
        "hovl_magic_effects_free", "unity_visual_effect_graph_samples", "keijiro_vfx_graph_assets"];
    private static readonly JsonSerializerOptions Options = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static JsonElement Freeze(string specificationRoot)
    {
        var root = Path.Combine(specificationRoot, "skills", "unity-god");
        var skill = ReadBounded(Path.Combine(root, "SKILL.md"), 24000);
        var instructions = ReadBounded(Path.Combine(root, "references", "runtime.md"), 16000);
        var catalog = ReadBounded(Path.Combine(root, "references", "methods.json"), 100000);
        using var parsed = JsonDocument.Parse(catalog);
        var source = parsed.RootElement;
        if (source.GetProperty("schema_version").GetString() != "sp.unity-god-methods/1.0" ||
            source.GetProperty("skill_version").GetString() != SkillVersion) throw new InvalidDataException("unity_god_catalog_version");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
            schema_version = ContextSchema, skill_version = SkillVersion, skill_sha256 = Hash(skill),
            method_catalog_sha256 = Hash(catalog), runtime_instructions_sha256 = Hash(instructions),
            runtime_instructions = Encoding.UTF8.GetString(instructions), source_reviews = source.GetProperty("source_reviews"),
            methods = source.GetProperty("methods")
        }, Options);
        using var frozen = JsonDocument.Parse(bytes);
        ValidateContext(frozen.RootElement);
        return frozen.RootElement.Clone();
    }

    public static void ValidateContext(JsonElement context)
    {
        if (context.GetRawText().Length > 120000 || context.GetProperty("schema_version").GetString() != ContextSchema ||
            context.GetProperty("skill_version").GetString() != SkillVersion) throw new InvalidDataException("unity_god_context_version");
        foreach (var key in new[] { "skill_sha256", "method_catalog_sha256", "runtime_instructions_sha256" })
            if (!Digest(context.GetProperty(key).GetString())) throw new InvalidDataException("unity_god_context_hash");
        var instructions = context.GetProperty("runtime_instructions").GetString()!;
        if (instructions.Length is < 100 or > 16000 || Hash(Encoding.UTF8.GetBytes(instructions)) != context.GetProperty("runtime_instructions_sha256").GetString())
            throw new InvalidDataException("unity_god_instructions_hash");
        var sources = context.GetProperty("source_reviews");
        if (sources.GetArrayLength() != SourceIds.Length || !SourceIds.ToHashSet(StringComparer.Ordinal).SetEquals(
                sources.EnumerateArray().Select(s => s.GetProperty("source_id").GetString()!)))
            throw new InvalidDataException("unity_god_sources");
        var methods = context.GetProperty("methods");
        if (methods.GetArrayLength() is < 8 or > 48) throw new InvalidDataException("unity_god_methods_count");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in methods.EnumerateArray())
        {
            var id = method.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 96 || !seen.Add(id) ||
                !SourceIds.Contains(method.GetProperty("source_id").GetString(), StringComparer.Ordinal))
                throw new InvalidDataException("unity_god_method_identity");
            var requirements = method.GetProperty("runtime_requirements");
            var selectable = method.GetProperty("runtime_selectable").GetBoolean();
            if (requirements.GetArrayLength() > 12 || selectable && requirements.GetArrayLength() == 0)
                throw new InvalidDataException("unity_god_method_requirements");
            foreach (var requirement in requirements.EnumerateArray())
                if (!AllowedPath(requirement.GetProperty("path").GetString()) ||
                    requirement.GetProperty("op").GetString() is not ("eq" or "in" or "gte" or "abs_gte"))
                    throw new InvalidDataException("unity_god_method_requirement");
        }
    }

    public static bool IsEnabled(SpellReferenceResearch research)
    {
        using var json = JsonDocument.Parse(research.Json);
        return json.RootElement.TryGetProperty("unity_god", out var context) && context.ValueKind == JsonValueKind.Object;
    }

    public static string BuildContext(SpellReferenceResearch research)
    {
        using var json = JsonDocument.Parse(research.Json);
        var c = json.RootElement.GetProperty("unity_god"); ValidateContext(c);
        return c.GetProperty("runtime_instructions").GetString() +
            "\nUNITY_GOD_VERSION " + c.GetProperty("skill_version").GetString() +
            "\nUNITY_GOD_SKILL_SHA256 " + c.GetProperty("skill_sha256").GetString() +
            "\nUNITY_GOD_CATALOG_SHA256 " + c.GetProperty("method_catalog_sha256").GetString() +
            "\nAll reviewed method cards are in REFERENCE_RESEARCH_DATA.unity_god.methods. Do not fetch URLs or execute their provenance paths.";
    }

    public static IReadOnlyList<string> ValidateDesign(byte[] plan, byte[] design, SpellReferenceResearch research)
    {
        var issues = new List<string>();
        try
        {
            if (design.Length is 0 or > 64000 || plan.Length is 0 or > 250000) return ["unity_god_design_size"];
            using var r = JsonDocument.Parse(research.Json); var c = r.RootElement.GetProperty("unity_god"); ValidateContext(c);
            using var p = JsonDocument.Parse(plan); using var d = JsonDocument.Parse(design); var spec = d.RootElement;
            ExactKeys(spec, ["skill_version", "skill_sha256", "method_catalog_sha256", "source_review", "nodes"]);
            foreach (var key in new[] { "skill_version", "skill_sha256", "method_catalog_sha256" })
                if (spec.GetProperty(key).GetString() != c.GetProperty(key).GetString()) issues.Add("unity_god_binding:" + key);
            var methods = c.GetProperty("methods").EnumerateArray().ToDictionary(m => m.GetProperty("id").GetString()!, StringComparer.Ordinal);
            var reviews = spec.GetProperty("source_review");
            if (reviews.GetArrayLength() != SourceIds.Length || !SourceIds.ToHashSet(StringComparer.Ordinal).SetEquals(
                    reviews.EnumerateArray().Select(s => s.GetProperty("source_id").GetString()!))) issues.Add("unity_god_review_all_five_sources");
            var dispositions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var review in reviews.EnumerateArray())
            {
                ExactKeys(review, ["source_id", "disposition", "reason"]); Text(review, "reason", 10, 900);
                var disposition = review.GetProperty("disposition").GetString()!;
                if (disposition is not ("used" or "reference_only" or "unavailable" or "not_applicable")) issues.Add("unity_god_disposition");
                if (!dispositions.TryAdd(review.GetProperty("source_id").GetString()!, disposition)) issues.Add("unity_god_duplicate_source");
            }
            var nodes = p.RootElement.GetProperty("nodes").EnumerateArray().ToDictionary(n => n.GetProperty("node_id").GetString()!, StringComparer.Ordinal);
            var designed = spec.GetProperty("nodes");
            if (designed.GetArrayLength() != nodes.Count) issues.Add("unity_god_all_nodes_required");
            var seen = new HashSet<string>(StringComparer.Ordinal); var usedSources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var nodeDesign in designed.EnumerateArray())
            {
                ExactKeys(nodeDesign, ["node_id", "choices", "innovation", "expected_visual_result"]);
                Text(nodeDesign, "innovation", 10, 1200); Text(nodeDesign, "expected_visual_result", 10, 1200);
                var nodeId = nodeDesign.GetProperty("node_id").GetString()!;
                if (!nodes.TryGetValue(nodeId, out var node) || !seen.Add(nodeId)) { issues.Add("unity_god_node_identity"); continue; }
                var choices = nodeDesign.GetProperty("choices");
                if (choices.GetArrayLength() is < 1 or > 4) issues.Add("unity_god_choose_1_to_4_methods:" + nodeId);
                var chosen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var choice in choices.EnumerateArray())
                {
                    ExactKeys(choice, ["method_id", "application", "adaptation", "bindings"]);
                    Text(choice, "adaptation", 20, 1800);
                    if (choice.GetProperty("application").GetString() is not ("reuse" or "adapt" or "combine")) issues.Add("unity_god_application");
                    var id = choice.GetProperty("method_id").GetString()!;
                    if (!methods.TryGetValue(id, out var method) || !chosen.Add(id)) { issues.Add("unity_god_unknown_or_duplicate_method:" + nodeId); continue; }
                    if (!method.GetProperty("runtime_selectable").GetBoolean()) { issues.Add("unity_god_method_requires_development:" + id); continue; }
                    if (!method.GetProperty("allowed_applications").EnumerateArray().Any(a => a.GetString() == choice.GetProperty("application").GetString()))
                        issues.Add("unity_god_unavailable_reuse:" + id);
                    usedSources.Add(method.GetProperty("source_id").GetString()!);
                    var bindings = choice.GetProperty("bindings"); var bound = new HashSet<string>(StringComparer.Ordinal);
                    if (bindings.GetArrayLength() is < 1 or > 16) issues.Add("unity_god_binding_count:" + id);
                    foreach (var binding in bindings.EnumerateArray())
                    {
                        ExactKeys(binding, ["path", "value"]);
                        var path = binding.GetProperty("path").GetString();
                        if (!AllowedPath(path) || !bound.Add(path!) || !TryResolve(node, path!, out var actual) ||
                            actual.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null ||
                            Scalar(actual) != binding.GetProperty("value").GetString()) issues.Add("unity_god_false_plan_binding:" + id);
                    }
                    foreach (var requirement in method.GetProperty("runtime_requirements").EnumerateArray())
                    {
                        var path = requirement.GetProperty("path").GetString()!;
                        if (!bound.Contains(path) || !TryResolve(node, path, out var actual) || !Matches(actual, requirement))
                            issues.Add("unity_god_method_not_implemented:" + id + ":" + path);
                    }
                }
            }
            foreach (var source in SourceIds)
                if (!dispositions.TryGetValue(source, out var disposition) || (disposition == "used") != usedSources.Contains(source))
                    issues.Add("unity_god_source_usage_inconsistent:" + source);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or ArgumentException or FormatException)
        { issues.Add("unity_god_design_malformed"); }
        return issues.Distinct(StringComparer.Ordinal).Take(32).ToArray();
    }

    public static byte[] CreateReceipt(byte[] plan, byte[] design, SpellReferenceResearch research)
    {
        var issues = ValidateDesign(plan, design, research);
        if (issues.Count != 0) throw new InvalidDataException(string.Join(";", issues));
        using var d = JsonDocument.Parse(design); using var r = JsonDocument.Parse(research.Json);
        var selectedIds = d.RootElement.GetProperty("nodes").EnumerateArray().SelectMany(n => n.GetProperty("choices").EnumerateArray())
            .Select(c => c.GetProperty("method_id").GetString()).ToHashSet(StringComparer.Ordinal);
        return JsonSerializer.SerializeToUtf8Bytes(new {
            schema_version = ReceiptSchema, plan_sha256 = Hash(plan), research_sha256 = research.Sha256,
            design = d.RootElement,
            selected_methods = r.RootElement.GetProperty("unity_god").GetProperty("methods").EnumerateArray()
                .Where(m => selectedIds.Contains(m.GetProperty("id").GetString())).ToArray()
        }, Options);
    }

    public static IReadOnlyList<string> ValidateReceipt(byte[] plan, byte[] receipt, SpellReferenceResearch research)
    {
        try
        {
            if (receipt.Length is 0 or > 200000) return ["unity_god_receipt_size"];
            using var r = JsonDocument.Parse(receipt); var root = r.RootElement;
            if (root.GetProperty("schema_version").GetString() != ReceiptSchema ||
                root.GetProperty("plan_sha256").GetString() != Hash(plan) || root.GetProperty("research_sha256").GetString() != research.Sha256)
                return ["unity_god_receipt_binding"];
            var design = Encoding.UTF8.GetBytes(root.GetProperty("design").GetRawText());
            var issues = ValidateDesign(plan, design, research); if (issues.Count != 0) return issues;
            using var expected = JsonDocument.Parse(CreateReceipt(plan, design, research));
            if (!JsonElement.DeepEquals(root.GetProperty("selected_methods"), expected.RootElement.GetProperty("selected_methods")))
                return ["unity_god_receipt_method_snapshot"];
            return [];
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        { return ["unity_god_receipt_malformed"]; }
    }

    private static bool Matches(JsonElement actual, JsonElement requirement) => requirement.GetProperty("op").GetString() switch {
        "eq" => JsonElement.DeepEquals(actual, requirement.GetProperty("value")),
        "in" => requirement.GetProperty("value").EnumerateArray().Any(v => JsonElement.DeepEquals(actual, v)),
        "gte" => actual.ValueKind == JsonValueKind.Number && actual.TryGetDecimal(out var a) && a >= requirement.GetProperty("value").GetDecimal(),
        "abs_gte" => actual.ValueKind == JsonValueKind.Number && actual.TryGetDecimal(out var a) && Math.Abs(a) >= requirement.GetProperty("value").GetDecimal(),
        _ => false
    };
    private static bool TryResolve(JsonElement node, string path, out JsonElement value)
    {
        value = node;
        foreach (var part in path.TrimStart('/').Split('/'))
        {
            if (value.ValueKind == JsonValueKind.Array && int.TryParse(part, out var index) && index >= 0 && index < value.GetArrayLength())
                value = value[index];
            else if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return false;
        }
        return true;
    }
    private static bool AllowedPath(string? path) => path is not null && path.Length is > 14 and < 160 &&
        path.StartsWith("/blueprint_v2/", StringComparison.Ordinal) && path.Skip(1).All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '/');
    private static string Scalar(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Text(JsonElement value, string key, int minimum, int maximum)
    { var text = value.GetProperty(key).GetString(); if (string.IsNullOrWhiteSpace(text) || text.Length < minimum || text.Length > maximum) throw new InvalidDataException("unity_god_text"); }
    private static void ExactKeys(JsonElement value, string[] keys)
    { if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != keys.Length || !keys.ToHashSet(StringComparer.Ordinal).SetEquals(value.EnumerateObject().Select(p => p.Name))) throw new InvalidDataException("unity_god_fields"); }
    private static byte[] ReadBounded(string path, int maximum)
    { if (new FileInfo(path).Length is <= 0 || new FileInfo(path).Length > maximum) throw new InvalidDataException("unity_god_file_size"); return File.ReadAllBytes(path); }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
