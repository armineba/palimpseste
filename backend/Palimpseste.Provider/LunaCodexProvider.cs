using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Palimpseste.Provider;

public interface IMultimodalInterpreter
{
    Task<ProviderDocument> InterpretAsync(string jobId, string attemptId, string referencePng, string drawingPng, string layoutJson, string capabilitiesJson, CancellationToken ct);
}

public interface IDescriptionPlanner
{
    Task<ProviderDocument> PlanAsync(string jobId, string attemptId, byte[] frozenDescriptionUtf8, string geometryJson, string capabilitiesJson, CancellationToken ct);
}

public interface ITechnicalRepairProvider
{
    Task<ProviderDocument> RepairAsync(RepairAttempt attempt, CancellationToken ct);
}

/// <summary>
/// A repair request contains only server-authorized data. The worker owns the
/// retry counter and persists the before/after artifacts; the provider enforces
/// the same hard per-stage bound as a second line of defense.
/// </summary>
public sealed record RepairAttempt(
    string JobId,
    string AttemptId,
    string Stage,
    string OriginalAuthorizedInput,
    string PreviousOutput,
    IReadOnlyList<string> ValidationErrors,
    int AttemptNumber,
    string? ReferencePng,
    string? DrawingPng,
    string? LayoutJson,
    string? GeometryJson,
    string CapabilitiesJson);

public sealed record ProviderDocument(CodexResult Transport, byte[]? Utf8, string? Sha256);

public sealed class LunaCodexProvider : IMultimodalInterpreter, IDescriptionPlanner, ITechnicalRepairProvider
{
    public const string InterpreterModel = "gpt-6-astra";
    public const string PlannerModel = "gpt-5.6-luna";
    public const string PromptAVersion = "sp.prompt.a/1.7";
    public const string PromptBVersion = "sp.prompt.b/1.3";
    private readonly CodexProcessRunner runner;
    private readonly string promptA;
    private readonly string promptB;
    private readonly string promptRepair;
    private readonly string schemaA;
    private readonly string schemaB;
    public string PromptASha256 { get; }
    public string PromptBSha256 { get; }

    public LunaCodexProvider(CodexProcessRunner runner, string trustedSpecificationRoot)
    {
        this.runner = runner;
        promptA = File.ReadAllText(Path.Combine(trustedSpecificationRoot, "prompts", "01_MODEL_A_INTERPRETE.md"), Encoding.UTF8);
        if (!promptA.Split('\n', 2)[0].TrimEnd('\r').EndsWith("Version " + PromptAVersion, StringComparison.Ordinal))
            throw new InvalidDataException("prompt_a_version_mismatch");
        PromptASha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(promptA)));
        promptB = File.ReadAllText(Path.Combine(trustedSpecificationRoot, "prompts", "02_MODEL_B_TRADUCTEUR.md"), Encoding.UTF8);
        if (!promptB.Split('\n', 2)[0].TrimEnd('\r').EndsWith("Version " + PromptBVersion, StringComparison.Ordinal))
            throw new InvalidDataException("prompt_b_version_mismatch");
        PromptBSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(promptB)));
        promptRepair = File.ReadAllText(Path.Combine(trustedSpecificationRoot, "prompts", "03_REPARATION_TECHNIQUE.md"), Encoding.UTF8);
        schemaA = Path.Combine(trustedSpecificationRoot, "contracts", "codex", "model-a.output-schema.json");
        schemaB = Path.Combine(trustedSpecificationRoot, "contracts", "codex", "model-b.output-schema.json");
    }

    public async Task<ProviderDocument> InterpretAsync(string jobId, string attemptId, string referencePng, string drawingPng, string layoutJson, string capabilitiesJson, CancellationToken ct)
    {
        var prompt = promptA + "\n\nLAYOUT_CONTEXT\n" + layoutJson + "\nCAPABILITIES_CONTEXT\n" + capabilitiesJson +
            "\nIMAGE 1 = référence neutre. IMAGE 2 = dessin engagé. Réponds avec le seul contrat JSON.\n";
        var result = await runner.RunAsync(new(attemptId, "A", prompt, schemaA,
            [referencePng, drawingPng], jobId), ct);
        return Parse(result, "sp.description/1.0");
    }

    /// <summary>
    /// Explicit operator-only compatibility path. It uses ProbeAsync, whose
    /// preflight omits only the effort evidence gate while retaining the
    /// production isolation checks. The worker interfaces continue to call
    /// InterpretAsync and PlanAsync.
    /// </summary>
    public async Task<ProviderDocument> ProbeInterpretAsync(string jobId, string attemptId, string referencePng, string drawingPng,
        string layoutJson, string capabilitiesJson, CancellationToken ct)
    {
        var prompt = promptA + "\n\nLAYOUT_CONTEXT\n" + layoutJson + "\nCAPABILITIES_CONTEXT\n" + capabilitiesJson +
            "\nIMAGE 1 = reference. IMAGE 2 = drawing. Return only the JSON contract.\n";
        var result = await runner.ProbeAsync(new(attemptId, "A", prompt, schemaA,
            [referencePng, drawingPng], jobId), ct);
        return Parse(result, "sp.description/1.0");
    }

    public async Task<ProviderDocument> PlanAsync(string jobId, string attemptId, byte[] frozenDescriptionUtf8, string geometryJson, string capabilitiesJson, CancellationToken ct)
    {
        if (frozenDescriptionUtf8.Length == 0 || frozenDescriptionUtf8.Length > 250_000) throw new ArgumentOutOfRangeException(nameof(frozenDescriptionUtf8));
        var hash = Convert.ToHexStringLower(SHA256.HashData(frozenDescriptionUtf8));
        var prompt = promptB + "\n\nDESCRIPTION_SHA256\n" + hash + "\nSPELL_DESCRIPTION\n" +
            Encoding.UTF8.GetString(frozenDescriptionUtf8) + "\nGEOMETRY_CONTEXT\n" + geometryJson +
            "\nCAPABILITIES_CONTEXT\n" + capabilitiesJson + "\nRéponds avec le seul contrat JSON.\n";
        var result = await runner.RunAsync(new(attemptId, "B", prompt, schemaB,
            [], jobId), ct);
        var document = Parse(result, "sp.plan/1.0");
        return EnsureDescriptionHash(document, hash);
    }

    /// <summary>Operator-only counterpart to PlanAsync for active compatibility doctor runs.</summary>
    public async Task<ProviderDocument> ProbePlanAsync(string jobId, string attemptId, byte[] frozenDescriptionUtf8,
        string geometryJson, string capabilitiesJson, CancellationToken ct)
    {
        if (frozenDescriptionUtf8.Length == 0 || frozenDescriptionUtf8.Length > 250_000) throw new ArgumentOutOfRangeException(nameof(frozenDescriptionUtf8));
        var hash = Convert.ToHexStringLower(SHA256.HashData(frozenDescriptionUtf8));
        var prompt = promptB + "\n\nDESCRIPTION_SHA256\n" + hash + "\nSPELL_DESCRIPTION\n" +
            Encoding.UTF8.GetString(frozenDescriptionUtf8) + "\nGEOMETRY_CONTEXT\n" + geometryJson +
            "\nCAPABILITIES_CONTEXT\n" + capabilitiesJson + "\nReturn only the JSON contract.\n";
        var result = await runner.ProbeAsync(new(attemptId, "B", prompt, schemaB, [], jobId), ct);
        var document = Parse(result, "sp.plan/1.0");
        return EnsureDescriptionHash(document, hash);
    }

    public async Task<ProviderDocument> RepairAsync(RepairAttempt attempt, CancellationToken ct)
    {
        if (attempt.Stage is not ("A" or "B")) throw new ArgumentException("stage must be A or B", nameof(attempt));
        if (attempt.AttemptNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(attempt.AttemptNumber));
        if (string.IsNullOrWhiteSpace(attempt.JobId) || string.IsNullOrWhiteSpace(attempt.AttemptId))
            throw new ArgumentException("job and attempt IDs are required", nameof(attempt));
        if (string.IsNullOrWhiteSpace(attempt.OriginalAuthorizedInput) || attempt.OriginalAuthorizedInput.Length > 250_000)
            throw new ArgumentOutOfRangeException(nameof(attempt.OriginalAuthorizedInput));
        if (string.IsNullOrWhiteSpace(attempt.PreviousOutput) || attempt.PreviousOutput.Length > 250_000)
            throw new ArgumentOutOfRangeException(nameof(attempt.PreviousOutput));
        if (attempt.ValidationErrors.Count is < 1 or > 32 || attempt.ValidationErrors.Any(error => string.IsNullOrWhiteSpace(error) || error.Length > 4_000))
            throw new ArgumentOutOfRangeException(nameof(attempt.ValidationErrors));

        var prompt = new StringBuilder(promptRepair)
            .Append("\n\nSTAGE\n").Append(attempt.Stage)
            .Append("\nORIGINAL_AUTHORIZED_INPUT\n").Append(attempt.OriginalAuthorizedInput)
            .Append("\nPREVIOUS_OUTPUT\n").Append(attempt.PreviousOutput)
            .Append("\nVALIDATION_ERRORS\n").Append(string.Join("\n", attempt.ValidationErrors.Select(error => "- " + error)))
            .Append("\nATTEMPT_NUMBER\n").Append(attempt.AttemptNumber)
            .Append("\nCAPABILITIES_CONTEXT\n").Append(attempt.CapabilitiesJson)
            .Append('\n');

        string schema;
        IReadOnlyList<string> images;
        if (attempt.Stage == "A")
        {
            if (string.IsNullOrWhiteSpace(attempt.ReferencePng) || string.IsNullOrWhiteSpace(attempt.DrawingPng) ||
                string.IsNullOrWhiteSpace(attempt.LayoutJson))
                throw new ArgumentException("A repair requires both images and layout context", nameof(attempt));
            prompt.Append("LAYOUT_CONTEXT\n").Append(attempt.LayoutJson)
                .Append("\nIMAGE 1 = référence neutre. IMAGE 2 = dessin engagé.\n");
            schema = schemaA;
            images = [attempt.ReferencePng, attempt.DrawingPng];
        }
        else
        {
            if (string.IsNullOrWhiteSpace(attempt.GeometryJson))
                throw new ArgumentException("B repair requires geometry context", nameof(attempt));
            prompt.Append("GEOMETRY_CONTEXT\n").Append(attempt.GeometryJson)
                .Append("\nSPELL_DESCRIPTION_REMAINS_IMMUTABLE\n");
            schema = schemaB;
            images = [];
        }

        var result = await runner.RunAsync(new(attempt.AttemptId, attempt.Stage, prompt.ToString(), schema, images, attempt.JobId), ct);
        var document = Parse(result, attempt.Stage == "A" ? "sp.description/1.0" : "sp.plan/1.0");
        if (attempt.Stage == "B")
        {
            var descriptionHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(attempt.OriginalAuthorizedInput)));
            return EnsureDescriptionHash(document, descriptionHash);
        }
        return document;
    }

    private static ProviderDocument Parse(CodexResult transport, string version)
    {
        if (transport.Outcome != ProviderOutcome.Success || transport.FinalJson is null) return new(transport, null, null);
        var bytes = Encoding.UTF8.GetBytes(transport.FinalJson);
        try
        {
            using var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("schema_version", out var property) || property.GetString() != version)
                return new(transport with { Outcome = ProviderOutcome.InvalidSchema, ErrorCode = "schema_version_mismatch" }, null, null);
            CheckNoDuplicateProperties(doc.RootElement);
            return new(transport, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        catch (JsonException)
        {
            return new(transport with { Outcome = ProviderOutcome.InvalidSchema, ErrorCode = "duplicate_or_invalid_json" }, null, null);
        }
    }

    private static ProviderDocument EnsureDescriptionHash(ProviderDocument document, string expectedHash)
    {
        if (document.Utf8 is null) return document;
        try
        {
            using var json = JsonDocument.Parse(document.Utf8);
            if (!json.RootElement.TryGetProperty("description_sha256", out var digest) || digest.GetString() != expectedHash)
                return new(document.Transport with { Outcome = ProviderOutcome.BusinessViolation, ErrorCode = "description_hash_mismatch" }, null, null);
            return document;
        }
        catch (JsonException)
        {
            return new(document.Transport with { Outcome = ProviderOutcome.InvalidSchema, ErrorCode = "description_hash_check_failed" }, null, null);
        }
    }

    private static void CheckNoDuplicateProperties(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw new JsonException("duplicate key");
                CheckNoDuplicateProperties(property.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray()) CheckNoDuplicateProperties(item);
    }
}
