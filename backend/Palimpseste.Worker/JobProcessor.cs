using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Palimpseste.Contracts;
using Palimpseste.Core;
using Palimpseste.Provider;
using Palimpseste.Storage;

namespace Palimpseste.Worker;

public sealed class JobProcessor
{
    private readonly JobRepository jobs;
    private readonly IArtifactStore files;
    private readonly string specRoot;
    private readonly LunaCodexProvider provider;
    private readonly CodexSettings settings;
    private readonly string workerId = Environment.MachineName + ":" + Guid.NewGuid().ToString("N");

    public JobProcessor(NpgsqlDataSource source, string artifactRoot, string specRoot,
        LunaCodexProvider provider, CodexSettings settings)
    {
        jobs = new JobRepository(source);
        files = new FileArtifactStore(artifactRoot);
        this.specRoot = specRoot;
        this.provider = provider;
        this.settings = settings;
    }

    public async Task RunAsync(CancellationToken shutdown)
    {
        while (!shutdown.IsCancellationRequested)
        {
            ClaimedJob? job = null;
            try { job = await jobs.ClaimAsync(workerId, shutdown); }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                Console.Error.WriteLine($"worker claim failed: {e.GetType().Name}");
                await Task.Delay(TimeSpan.FromSeconds(5), shutdown);
                continue;
            }
            if (job is null) { await Task.Delay(TimeSpan.FromSeconds(2), shutdown); continue; }
            using var leaseLost = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
            var heartbeat = HeartbeatAsync(job, leaseLost);
            try { await ProcessAsync(job, leaseLost.Token); }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
            catch (Exception e)
            {
                Console.Error.WriteLine($"job {job.Id:N} incident: {e.GetType().Name}");
                try { await jobs.SetStateAsync(job, "needs_operator", "worker_exception", "Incident technique à examiner", false, CancellationToken.None); }
                catch (Exception) { /* A lost lease may already belong to another worker. */ }
            }
            finally
            {
                leaseLost.Cancel();
                try { await heartbeat; } catch (OperationCanceledException) { }
            }
        }
    }

    private async Task HeartbeatAsync(ClaimedJob job, CancellationTokenSource cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellation.Token);
            try
            {
                if (!await jobs.RenewAsync(job, workerId, cancellation.Token))
                {
                    cancellation.Cancel();
                    return;
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception)
            {
                cancellation.Cancel();
                return;
            }
        }
    }

    private async Task ProcessAsync(ClaimedJob job, CancellationToken ct)
    {
        if (await jobs.HasUncertainAttemptAsync(job, ct))
        {
            await jobs.SetStateAsync(job, "needs_operator", "uncertain_provider_attempt",
                "Tentative fournisseur à rapprocher avant toute reprise", false, ct);
            return;
        }
        if (job.Kind == "authoring")
        {
            await ProcessAuthoringAsync(job, ct);
            return;
        }
        if (job.Kind != "production" || job.ParchmentId is null || job.CaptureId is null)
        {
            await jobs.SetStateAsync(job, "needs_operator", "authoring_not_supported",
                "Tâche de diagnostic auteur non intégrée au worker", false, ct);
            return;
        }
        var capture = await jobs.GetCaptureAsync(job, ct);
        using var captureManifest = JsonDocument.Parse(capture.ManifestJson);
        var requirePalette = captureManifest.RootElement.GetProperty("layout_version").GetString() == "free_canvas_v2";
        var drawing = await ReadCheckedAsync(capture.DrawingKey, capture.DrawingSha, ct);
        var ink = await ReadCheckedAsync(capture.InkKey, capture.InkSha, ct);
        var reference = await ReadCheckedAsync(capture.ReferenceKey, capture.ReferenceSha, ct);
        // The reference image remains a visual guide only. Its former radial
        // role hints must not dictate what the player's drawing means.
        const string layout = "{\"canvas_width\":1024,\"canvas_height\":1024,\"reference_purpose\":\"identify_printed_guides_only\",\"semantic_regions\":false}";
        var capabilities = await File.ReadAllTextAsync(Path.Combine(specRoot, "contracts", "capability-catalog.json"), ct);
        var inputHash = Sha256(Encoding.UTF8.GetBytes(capture.ManifestSha + capture.DrawingSha + capture.ReferenceSha + layout + capabilities + provider.PromptASha256 + provider.EffectRecipesPromptSha256));

        var descriptionRecord = await jobs.GetDescriptionAsync(job, ct);
        byte[] description;
        if (descriptionRecord is null)
        {
            await jobs.SetStateAsync(job, "interpreting", null, "Interprétation du dessin", false, ct);
            var attempt = await jobs.BeginAttemptAsync(job, "A", settings.InterpreterModel, settings.InterpreterEffort, inputHash, ct);
            var result = await provider.InterpretAsync(job.Id.ToString("N"), attempt.ToString("N"),
                files.PathForKey(capture.ReferenceKey), files.PathForKey(capture.DrawingKey), layout, capabilities, ct);
            IReadOnlyList<ValidationIssue> issues = ValidateNewInterpretation(result.Utf8, requirePalette);
            for (var repairNumber = await jobs.CountAttemptsAsync(job, "repair_A", ct) + 1;
                 repairNumber <= 2 && CanRepair(result, issues); repairNumber++)
            {
                await jobs.CompleteAttemptAsync(job, attempt, "invalid", result.Sha256, result.Transport.CliVersion,
                    result.Transport.SessionId, result.Transport.UsageJson, "description_invalid", ct);
                attempt = await jobs.BeginAttemptAsync(job, "repair_A", settings.InterpreterModel, settings.InterpreterEffort,
                    inputHash, ct);
                result = await provider.RepairAsync(new RepairAttempt(
                    job.Id.ToString("N"), attempt.ToString("N"), "A", layout,
                    result.Transport.FinalJson!, RepairErrors(result, issues), repairNumber,
                    files.PathForKey(capture.ReferenceKey), files.PathForKey(capture.DrawingKey), layout,
                    null, capabilities), ct);
                issues = ValidateNewInterpretation(result.Utf8, requirePalette);
            }
            var status = result.Transport.Outcome == ProviderOutcome.Success && issues.Count == 0 ? "success" :
                result.Transport.Outcome == ProviderOutcome.TransportUncertain ? "transport_uncertain" : "invalid";
            if (SafeToRetryTransport(result) && await jobs.CountAttemptsAsync(job, "A", ct) < 3)
            {
                await jobs.CompleteAttemptAsync(job, attempt, "invalid", result.Sha256, result.Transport.CliVersion,
                    result.Transport.SessionId, result.Transport.UsageJson, result.Transport.ErrorCode, ct);
                await jobs.ScheduleRetryAsync(job, "A", result.Transport.ErrorCode!, ct);
                return;
            }
            if (status != "success" || result.Utf8 is null)
            {
                var errorCode = issues.Count != 0 ? "description_invalid" : "provider_a_" + result.Transport.Outcome.ToString().ToLowerInvariant();
                await jobs.CompleteAttemptAsync(job, attempt, status, result.Sha256, result.Transport.CliVersion,
                    result.Transport.SessionId, result.Transport.UsageJson,
                    errorCode, ct);
                await jobs.SetStateAsync(job, "needs_operator", errorCode,
                    "Interprétation indisponible ; support conservé", false, ct);
                return;
            }
            var artifact = await files.PutAsync(result.Utf8, "json", "application/json", ct);
            await jobs.SaveDescriptionAsync(job, attempt, artifact, Encoding.UTF8.GetString(result.Utf8), inputHash,
                LunaCodexProvider.PromptAVersion, result.Transport, ct);
            description = result.Utf8;
        }
        else description = await ReadCheckedAsync(descriptionRecord.StorageKey, descriptionRecord.Sha256, ct);

        // D13 freezes a real generated image before the multimodal planner.
        // Earlier admitted jobs retain their original versioned pipeline.
        Palimpseste.Contracts.SpellVisualReference? visualMetadata = null;
        Palimpseste.Provider.SpellVisualReference? visualInput = null;
        if (job.VisualPipelineVersion >= 1)
        {
            var visual = await jobs.GetVisualReferenceAsync(job, ct);
            if (visual is null)
            {
                await jobs.SetStateAsync(job, "generating_visual_reference", null, "Création de l’image du sort", false, ct);
                var visualInputHash = Sha256(Encoding.UTF8.GetBytes(Sha256(description) + provider.PromptGSha256));
                var attempt = await jobs.BeginAttemptAsync(job, "G", settings.Model, settings.Effort, visualInputHash, ct);
                var generated = await provider.GenerateVisualReferenceAsync(job.Id.ToString("N"), attempt.ToString("N"), description, ct);
                if (generated.Transport.Outcome != ProviderOutcome.Success || generated.PngBytes is null)
                {
                    var error = "provider_g_" + generated.Transport.Outcome.ToString().ToLowerInvariant();
                    await jobs.CompleteAttemptAsync(job, attempt,
                        generated.Transport.Outcome == ProviderOutcome.TransportUncertain ? "transport_uncertain" : "invalid",
                        generated.Sha256, generated.Transport.CliVersion, generated.Transport.SessionId,
                        generated.Transport.UsageJson, error, ct);
                    if (!generated.Transport.ProcessStarted && generated.Transport.Outcome == ProviderOutcome.ProcessFailure &&
                        await jobs.CountAttemptsAsync(job, "G", ct) < 3)
                    {
                        await jobs.ScheduleRetryAsync(job, "G", error, ct);
                        return;
                    }
                    await jobs.SetStateAsync(job, "needs_operator", error,
                        "Image du sort indisponible ; dessin et description conservés", false, ct);
                    return;
                }
                var artifact = await files.PutAsync(generated.PngBytes, "png", "image/png", ct);
                await jobs.SaveVisualReferenceAsync(job, attempt, artifact, Sha256(description), visualInputHash,
                    LunaCodexProvider.PromptGVersion, generated.Width!.Value, generated.Height!.Value, generated.Transport, ct);
                visual = await jobs.GetVisualReferenceAsync(job, ct) ?? throw new InvalidDataException("visual_reference_not_persisted");
            }
            var bytes = await ReadCheckedAsync(visual.StorageKey, visual.Sha256, ct);
            if (visual.DescriptionSha256 != Sha256(description) || bytes.Length != visual.SizeBytes)
                throw new InvalidDataException("visual_reference_provenance_mismatch");
            visualInput = new(files.PathForKey(visual.StorageKey), visual.Sha256);
            visualMetadata = new()
            {
                artifact_id = "a" + visual.ArtifactId.ToString("N"), sha256 = visual.Sha256,
                size_bytes = visual.SizeBytes, width_px = visual.Width, height_px = visual.Height,
                description_sha256 = visual.DescriptionSha256, prompt_version = visual.PromptVersion
            };
        }

        var geometryRecords = await jobs.GetGeometryAsync(job, ct);
        if (geometryRecords.Geometry.Count == 0)
        {
            await jobs.SetStateAsync(job, "resolving_geometry", null, "Préparation de la forme du sort", false, ct);
            var decoded = ContractJson.DeserializeStrict<SpellDescription>(description, "spell-description");
            var resolved = GeometryResolver.Resolve(ink, decoded);
            if (!resolved.Success)
            {
                await jobs.SetStateAsync(job, "needs_operator", "geometry_unavailable",
                    "Géométrie du dessin non exploitable par le profil actuel", false, ct);
                return;
            }
            var geometryArtifacts = new Dictionary<string, StoredArtifact>(StringComparer.Ordinal);
            var maskArtifacts = new Dictionary<string, StoredArtifact>(StringComparer.Ordinal);
            foreach (var item in resolved.GeometryJson)
                geometryArtifacts[item.Key] = await files.PutAsync(item.Value, "json", "application/json", ct);
            foreach (var item in resolved.MaskPng)
                maskArtifacts[item.Key] = await files.PutAsync(item.Value, "png", "image/png", ct);
            await jobs.SaveGeometryAsync(job, geometryArtifacts, maskArtifacts, ct);
            geometryRecords = await jobs.GetGeometryAsync(job, ct);
        }
        var geometryJson = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var maskPng = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var item in geometryRecords.Geometry) geometryJson[item.GeometryId] = await ReadCheckedAsync(item.StorageKey, item.Sha256, ct);
        foreach (var item in geometryRecords.Masks) maskPng[item.FileName] = await ReadCheckedAsync(item.StorageKey, item.Sha256, ct);
        var geometryContext = "[" + string.Join(",", geometryJson.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => Encoding.UTF8.GetString(x.Value))) + "]";
        var planInputHash = Sha256(Encoding.UTF8.GetBytes(Sha256(description) + geometryContext + capabilities + provider.PromptBSha256 + provider.EffectRecipesSha256 + visualMetadata?.sha256));

        var planRecord = await jobs.GetPlanAsync(job, ct);
        byte[] plan;
        if (planRecord is null)
        {
            await jobs.SetStateAsync(job, "planning", null, "Traduction des règles", false, ct);
            var attempt = await jobs.BeginAttemptAsync(job, "B", settings.Model, settings.Effort,
                planInputHash, ct);
            var result = await provider.PlanAsync(job.Id.ToString("N"), attempt.ToString("N"),
                description, geometryContext, capabilities, visualInput, ct);
            IReadOnlyList<ValidationIssue> issues = result.Utf8 is null ? [] : SpellCompiler.ValidatePlanJson(description, result.Utf8, geometryJson, maskPng, visualMetadata);
            for (var repairNumber = await jobs.CountAttemptsAsync(job, "repair_B", ct) + 1;
                 repairNumber <= 2 && CanRepair(result, issues); repairNumber++)
            {
                await jobs.CompleteAttemptAsync(job, attempt, "invalid", result.Sha256, result.Transport.CliVersion,
                    result.Transport.SessionId, result.Transport.UsageJson, "plan_invalid", ct);
                attempt = await jobs.BeginAttemptAsync(job, "repair_B", settings.Model, settings.Effort,
                    planInputHash, ct);
                result = await provider.RepairAsync(new RepairAttempt(
                    job.Id.ToString("N"), attempt.ToString("N"), "B", Encoding.UTF8.GetString(description),
                    result.Transport.FinalJson!, RepairErrors(result, issues), repairNumber,
                    null, null, null, geometryContext, capabilities, visualInput), ct);
                issues = result.Utf8 is null ? [] : SpellCompiler.ValidatePlanJson(description, result.Utf8, geometryJson, maskPng, visualMetadata);
            }
            var status = result.Transport.Outcome == ProviderOutcome.Success && issues.Count == 0 ? "success" :
                result.Transport.Outcome == ProviderOutcome.TransportUncertain ? "transport_uncertain" : "invalid";
            if (SafeToRetryTransport(result) && await jobs.CountAttemptsAsync(job, "B", ct) < 3)
            {
                await jobs.CompleteAttemptAsync(job, attempt, "invalid", result.Sha256, result.Transport.CliVersion,
                    result.Transport.SessionId, result.Transport.UsageJson, result.Transport.ErrorCode, ct);
                await jobs.ScheduleRetryAsync(job, "B", result.Transport.ErrorCode!, ct);
                return;
            }
            if (status != "success" || result.Utf8 is null)
            {
                var errorCode = issues.Count != 0 ? "plan_invalid" : "provider_b_" + result.Transport.Outcome.ToString().ToLowerInvariant();
                await jobs.CompleteAttemptAsync(job, attempt, status, result.Sha256, result.Transport.CliVersion,
                    result.Transport.SessionId, result.Transport.UsageJson,
                    errorCode, ct);
                await jobs.SetStateAsync(job, "needs_operator", errorCode,
                    "Traduction indisponible ; description conservée", false, ct);
                return;
            }
            var artifact = await files.PutAsync(result.Utf8, "json", "application/json", ct);
            await jobs.SavePlanAsync(job, attempt, artifact, Encoding.UTF8.GetString(result.Utf8),
                LunaCodexProvider.PromptBVersion, result.Transport, ct);
            plan = result.Utf8;
        }
        else plan = await ReadCheckedAsync(planRecord.StorageKey, planRecord.Sha256, ct);

        await jobs.SetStateAsync(job, "validating", null, "Validation du sort", false, ct);
        var spellId = Guid.NewGuid();
        var actualARequestedModel = await jobs.GetSuccessfulRequestedModelAsync(job, "A", ct);
        var actualBRequestedModel = await jobs.GetSuccessfulRequestedModelAsync(job, "B", ct);
        var compilation = SpellCompiler.Compile(new CompilationInput
        {
            DescriptionJson = description, PlanJson = plan, GeometryJson = geometryJson, MaskPng = maskPng,
            VisualReference = visualMetadata,
            GeometryArtifactIds = geometryRecords.Geometry.ToDictionary(x => x.GeometryId, x => "a" + x.ArtifactId.ToString("N"), StringComparer.Ordinal),
            MaskArtifactIds = geometryRecords.Masks.ToDictionary(x => x.FileName, x => "a" + x.ArtifactId.ToString("N"), StringComparer.Ordinal),
            SpellId = spellId.ToString("N"), ParchmentId = job.ParchmentId.Value.ToString("N"),
            SignatureSeedHex = capture.SignatureSeedHex, CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            Provenance = new SpellProvenance
            {
                mode = "drawing", capture_sha256 = capture.ManifestSha, reference_sha256 = capture.ReferenceSha,
                // Read the persisted request, including when a legacy job
                // resumes after the interpreter model changes.
                model_a = actualARequestedModel, model_b = actualBRequestedModel,
                prompt_a_version = descriptionRecord?.PromptVersion ?? LunaCodexProvider.PromptAVersion,
                prompt_b_version = planRecord?.PromptVersion ?? LunaCodexProvider.PromptBVersion,
                // Codex JSONL exposes a thread ID, not a provider response ID.
                response_a_id = null, response_b_id = null
            }
        });
        if (!compilation.Success)
        {
            await jobs.SetStateAsync(job, "needs_operator", "compile_rejected",
                "Plan rejeté par les règles du jeu", false, ct);
            return;
        }
        var compiledArtifact = await files.PutAsync(compilation.PayloadUtf8, "json", "application/json", ct);
        await jobs.PublishAsync(job, spellId, compiledArtifact, ct);
    }

    private async Task ProcessAuthoringAsync(ClaimedJob job, CancellationToken ct)
    {
        var input = await jobs.GetAuthoringInputAsync(job, ct);
        var description = await ReadCheckedAsync(input.DescriptionKey, input.DescriptionSha, ct);
        if (SpellCompiler.ValidateDescriptionJson(description).Count != 0)
        {
            await jobs.SetStateAsync(job, "needs_operator", "authored_description_invalid",
                "Description auteur non conforme", false, ct);
            return;
        }
        var geometryRecords = await jobs.GetGeometryAsync(job, ct);
        if (geometryRecords.Geometry.Count == 0)
        {
            await jobs.LinkAuthoringGeometryAsync(job, input.GeometryArtifactIds, ct);
            geometryRecords = await jobs.GetGeometryAsync(job, ct);
        }
        var geometryJson = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var maskPng = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var item in geometryRecords.Geometry) geometryJson[item.GeometryId] = await ReadCheckedAsync(item.StorageKey, item.Sha256, ct);
        foreach (var item in geometryRecords.Masks) maskPng[item.FileName] = await ReadCheckedAsync(item.StorageKey, item.Sha256, ct);
        var geometryContext = "[" + string.Join(",", geometryJson.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => Encoding.UTF8.GetString(x.Value))) + "]";
        var capabilities = await File.ReadAllTextAsync(Path.Combine(specRoot, "contracts", "capability-catalog.json"), ct);
        var planInputHash = Sha256(Encoding.UTF8.GetBytes(Sha256(description) + geometryContext + capabilities + provider.PromptBSha256 + provider.EffectRecipesSha256));
        var planRecord = await jobs.GetPlanAsync(job, ct);
        if (planRecord is null)
        {
            await jobs.SetStateAsync(job, "planning", null, "Diagnostic de traduction", false, ct);
            var attempt = await jobs.BeginAttemptAsync(job, "B", settings.Model, settings.Effort,
                planInputHash, ct);
            var result = await provider.PlanAsync(job.Id.ToString("N"), attempt.ToString("N"),
                description, geometryContext, capabilities, ct);
            IReadOnlyList<ValidationIssue> issues = result.Utf8 is null ? [] : SpellCompiler.ValidatePlanJson(description, result.Utf8, geometryJson, maskPng);
            for (var repairNumber = await jobs.CountAttemptsAsync(job, "repair_B", ct) + 1;
                 repairNumber <= 2 && CanRepair(result, issues); repairNumber++)
            {
                await jobs.CompleteAttemptAsync(job, attempt, "invalid", result.Sha256, result.Transport.CliVersion,
                    result.Transport.SessionId, result.Transport.UsageJson, "plan_invalid", ct);
                attempt = await jobs.BeginAttemptAsync(job, "repair_B", settings.Model, settings.Effort,
                    planInputHash, ct);
                result = await provider.RepairAsync(new RepairAttempt(
                    job.Id.ToString("N"), attempt.ToString("N"), "B", Encoding.UTF8.GetString(description),
                    result.Transport.FinalJson!, RepairErrors(result, issues), repairNumber,
                    null, null, null, geometryContext, capabilities), ct);
                issues = result.Utf8 is null ? [] : SpellCompiler.ValidatePlanJson(description, result.Utf8, geometryJson, maskPng);
            }
            var status = result.Transport.Outcome == ProviderOutcome.Success && issues.Count == 0 ? "success" :
                result.Transport.Outcome == ProviderOutcome.TransportUncertain ? "transport_uncertain" : "invalid";
            if (SafeToRetryTransport(result) && await jobs.CountAttemptsAsync(job, "B", ct) < 3)
            {
                await jobs.CompleteAttemptAsync(job, attempt, "invalid", result.Sha256, result.Transport.CliVersion,
                    result.Transport.SessionId, result.Transport.UsageJson, result.Transport.ErrorCode, ct);
                await jobs.ScheduleRetryAsync(job, "B", result.Transport.ErrorCode!, ct);
                return;
            }
            if (status != "success" || result.Utf8 is null)
            {
                var errorCode = issues.Count != 0 ? "plan_invalid" : "provider_b_" + result.Transport.Outcome.ToString().ToLowerInvariant();
                await jobs.CompleteAttemptAsync(job, attempt, status, result.Sha256, result.Transport.CliVersion,
                    result.Transport.SessionId, result.Transport.UsageJson,
                    errorCode, ct);
                await jobs.SetStateAsync(job, "needs_operator", errorCode,
                    "Diagnostic de traduction indisponible", false, ct);
                return;
            }
            var artifact = await files.PutAsync(result.Utf8, "json", "application/json", ct);
            await jobs.SavePlanAsync(job, attempt, artifact, Encoding.UTF8.GetString(result.Utf8),
                LunaCodexProvider.PromptBVersion, result.Transport, ct);
        }
        await jobs.SetStateAsync(job, "ready", null, "Plan de diagnostic disponible", false, ct);
    }

    private async Task<byte[]> ReadCheckedAsync(string key, string expectedHash, CancellationToken ct)
    {
        var bytes = await files.ReadAsync(key, ct);
        if (Sha256(bytes) != expectedHash) throw new InvalidDataException("artifact_hash_mismatch");
        return bytes;
    }

    private static bool CanRepair(ProviderDocument result, IReadOnlyList<ValidationIssue> issues) =>
        (result.Transport.Outcome is ProviderOutcome.Success or ProviderOutcome.InvalidSchema or ProviderOutcome.BusinessViolation) &&
        !string.IsNullOrWhiteSpace(result.Transport.FinalJson) &&
        result.Transport.FinalJson.Length <= 100_000 &&
        (issues.Count != 0 || result.Transport.Outcome != ProviderOutcome.Success);

    // Once the CLI process has started, a failed call may already have consumed
    // usage. Only proven pre-launch failures are retried without reconciliation.
    private static bool SafeToRetryTransport(ProviderDocument result) =>
        result.Transport.Outcome == ProviderOutcome.ProcessFailure &&
        (result.Transport.ErrorCode is "process_not_started" or "process_start_failed");

    private static IReadOnlyList<string> RepairErrors(ProviderDocument result, IReadOnlyList<ValidationIssue> issues) =>
        issues.Count == 0
            ? [result.Transport.ErrorCode ?? "invalid_schema"]
            : issues.Take(32).Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}")
                .Select(message => message[..Math.Min(4000, message.Length)]).ToArray();

    private static IReadOnlyList<ValidationIssue> ValidateNewInterpretation(byte[]? utf8, bool requirePalette)
    {
        if (utf8 is null) return [];
        return SpellCompiler.ValidateWholeImageDescriptionJson(utf8, requirePalette, requireVisualForm: true);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
