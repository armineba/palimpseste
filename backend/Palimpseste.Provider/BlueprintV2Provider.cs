using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Palimpseste.Provider;

public sealed partial class LunaCodexProvider
{
    public const string BlueprintV2PromptVersion = "sp.prompt.blueprint/2.0";
    public const string CriticV2PromptVersion = "sp.prompt.v2-critic/2.0";
    public const string InterpreterV2PromptVersion = "sp.prompt.a-v2/1.0";
    public string InterpreterV2PromptSha256 => Digest(Encoding.UTF8.GetBytes(promptA + File.ReadAllText(
        Path.Combine(visualReviewSpecRoot, "prompts", "08_V2_INTERPRETATION.md"), Encoding.UTF8)));

    public async Task<ProviderDocument> InterpretBlueprintV2Async(string jobId, string attemptId,
        string referencePng, string drawingPng, string layout, string capabilities, CancellationToken ct)
    {
        var supplement = await File.ReadAllTextAsync(Path.Combine(visualReviewSpecRoot, "prompts", "08_V2_INTERPRETATION.md"), ct);
        var prompt = promptA + "\n" + supplement + "\nLAYOUT_CONTEXT\n" + layout +
            "\nCAPABILITIES_CONTEXT\n" + capabilities + "\nEFFECT_RECIPES_CONTEXT\n" + effectRecipesAContext;
        return Parse(await runner.RunAsync(new(attemptId, "A", prompt, schemaA,
            [referencePng, drawingPng], jobId), ct), "sp.description/1.0");
    }

    public async Task<ProviderDocument> PlanBlueprintV2Async(string jobId, string attemptId,
        byte[] description, string capabilities, SpellReferenceResearch research,
        string? previousPlan, string? feedback, string returnStage, CancellationToken ct)
    {
        var prompt = await File.ReadAllTextAsync(Path.Combine(visualReviewSpecRoot, "prompts", "06_BLUEPRINT_V2.md"), ct);
        if (!prompt.Split('\n', 2)[0].TrimEnd('\r').EndsWith("Version " + BlueprintV2PromptVersion, StringComparison.Ordinal))
            throw new InvalidDataException("v2_prompt_version");
        prompt += "\n" + await File.ReadAllTextAsync(Path.Combine(visualReviewSpecRoot, "prompts", "09_V2_NUMERIC_RULES.md"), ct);
        prompt += "\nDESCRIPTION_SHA256\n" + Digest(description) + "\nSPELL_DESCRIPTION\n" + Encoding.UTF8.GetString(description) +
            "\nCAPABILITIES_CONTEXT\n" + CapabilityContext(capabilities, description) +
            "\nEFFECT_RECIPES_CONTEXT\n" + SelectedRecipeContext(description) +
            "\nREFERENCE_RESEARCH_SHA256\n" + research.Sha256 + "\nREFERENCE_RESEARCH_DATA\n" + research.Json +
            "\nPREVIOUS_PLAN\n" + (previousPlan ?? "null") + "\nRETURN_STAGE\n" + returnStage +
            "\nVALIDATION_FEEDBACK_DATA\n" + (feedback ?? "null");
        var result = await runner.RunAsync(new(attemptId, "B", prompt,
            Path.Combine(visualReviewSpecRoot, "contracts", "codex", "model-b-v2.output-schema.json"), [], jobId), ct);
        return EnsureResearch(EnsureDescriptionHash(Parse(result, "sp.plan/1.0"), Digest(description)), research);
    }

    // A blind call receives image bytes and hash identifiers only, never subject/description/plan text.
    public async Task<ProviderDocument> JudgeBlueprintV2Async(string jobId, string attemptId, string mode,
        byte[] description, byte[] plan, IReadOnlyList<RenderedSpellFrame> frames, string? blind,
        string? telemetry, CancellationToken ct)
    {
        if (mode is not ("blind" or "structure" or "full") || frames.Count is < 1 or > 5)
            throw new ArgumentException("v2_critic_inputs");
        // The native J contract accepts 2..5 hash-bound images. A one-frame blind review repeats
        // exactly that core image, never introducing the drawing which would reveal the expected subject.
        var selected = frames.Count == 1 ? new[] { frames[0], frames[0] } : frames.ToArray();
        var reference = new SpellVisualReference(selected[0].PngPath, selected[0].Sha256);
        var prompt = await File.ReadAllTextAsync(Path.Combine(visualReviewSpecRoot, "prompts", "07_V2_CRITIC.md"), ct);
        if (!prompt.Split('\n', 2)[0].TrimEnd('\r').EndsWith("Version " + CriticV2PromptVersion, StringComparison.Ordinal))
            throw new InvalidDataException("v2_critic_prompt_version");
        prompt += "\nMODE\n" + mode + "\nDESCRIPTION_SHA256\n" + Digest(description) +
            "\nPLAN_SHA256\n" + Digest(plan) + "\nVISUAL_REFERENCE_SHA256\n" + reference.Sha256;
        if (mode != "blind") prompt += "\nSPELL_DESCRIPTION\n" + Encoding.UTF8.GetString(description) +
            "\nBLUEPRINT_PLAN\n" + Encoding.UTF8.GetString(plan) + "\nBLIND_OBSERVATION\n" + (blind ?? "null") +
            "\nACTUAL_RENDERER_MEASUREMENTS\n" + (telemetry ?? "null") +
            "\nIMAGE_ORDER\n" + JsonSerializer.Serialize(selected.Select(f => f.Phase));
        var result = await runner.RunAsync(new(attemptId, "J", prompt,
            Path.Combine(visualReviewSpecRoot, "contracts", "codex", "model-j-v2.output-schema.json"),
            selected.Select(f => f.PngPath).ToArray(), jobId, reference, selected.Select(f => f.Sha256).ToArray(),
            new(Digest(description), Digest(plan), reference.Sha256)), ct);
        var document = Parse(result, "sp.v2-review/1.0");
        if (document.Utf8 is null) return document;
        try
        {
            using var json = JsonDocument.Parse(document.Utf8);
            var r = json.RootElement;
            if (r.GetProperty("description_sha256").GetString() != Digest(description) ||
                r.GetProperty("plan_sha256").GetString() != Digest(plan) ||
                r.GetProperty("visual_reference_sha256").GetString() != reference.Sha256 ||
                r.GetProperty("mode").GetString() != mode || r.GetProperty("score_milli").GetInt32() is < 0 or > 10000 ||
                r.GetProperty("observed_subject").GetString()?.Length is < 1 or > 1500 ||
                r.GetProperty("issues").GetArrayLength() > 24) throw new InvalidDataException();
            var gates = r.GetProperty("gates");
            foreach (var name in new[] { "A_structure", "B_continuity", "C_rendering", "D_impact", "E_game_camera", "F_motion", "semantic_blind", "performance" })
                if (gates.GetProperty(name).GetString() is not ("pass" or "fail" or "not_evaluated")) throw new InvalidDataException();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or InvalidDataException or KeyNotFoundException)
        { return new(result with { Outcome = ProviderOutcome.InvalidSchema, ErrorCode = "v2_review_invalid" }, null, null); }
        return document;
    }
}
