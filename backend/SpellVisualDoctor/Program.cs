using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Palimpseste.Contracts;
using Palimpseste.Core;
using Palimpseste.Provider;
using Palimpseste.Worker;
using ContractReference = Palimpseste.Contracts.SpellVisualReference;
using ProviderReference = Palimpseste.Provider.SpellVisualReference;

return await VisualDoctor.RunAsync(args);

internal static class VisualDoctor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] Stages = ["A", "G", "B", "J"];
    private const string Kind = "spell_visual_doctor";

    public static async Task<int> RunAsync(string[] args)
    {
        var report = new Dictionary<string, object?> {
            ["kind"] = Kind, ["schema_version"] = "sp.visual-doctor/1.0",
            ["started_at"] = DateTimeOffset.UtcNow, ["phase"] = "arguments", ["result"] = "in_progress",
            ["new_model_calls_executed"] = 0, ["provider_call_pending"] = false,
            ["database_touched"] = false, ["player_library_touched"] = false,
            ["software_build_executed"] = false, ["repairs_executed"] = 0,
            ["human_acceptance"] = "not_requested", ["physics_test_executed"] = false
        };
        string? reportPath = null;
        var phase = "arguments";
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        var ct = cancellation.Token;
        try
        {
            var options = ParseArguments(args);
            var settings = CodexSettings.FromEnvironment();
            reportPath = CheckedReportPath(options["--report"], settings);
            report["service_identity"] = Environment.UserName;
            report["expected_service_identity"] = settings.ExpectedServiceUser;
            await CheckpointAsync();
            phase = "preflight";
            report["phase"] = phase;
            var spec = Path.GetFullPath(options["--spec"]);
            if (!SamePath(spec, settings.TrustedSpecificationRoot)) throw new ProbeFailure("spec_root_mismatch");
            var preflight = Stages.ToDictionary(stage => stage,
                stage => settings.Check(production: true, compatibilityProbe: true, stage: stage).ToArray());
            report["preflight_issues"] = preflight;
            if (preflight.Values.Any(issues => issues.Length > 0)) throw new ProbeFailure("provider_preflight_failed");
            var nativeHash = CodexSettings.ComputeExecutableSha256(settings.Executable);
            var imageNativeHash = CodexSettings.ComputeExecutableSha256(settings.ExecutableForStage("G"));
            if (nativeHash != imageNativeHash) throw new ProbeFailure("native_binaries_differ");
            report["cli_executable"] = settings.Executable;
            report["cli_executable_sha256"] = nativeHash;
            report["image_cli_executable_sha256"] = imageNativeHash;
            report["cli_image_generation_contract"] = "palimpseste.codex-image/1.0";
            report["execution_mode"] = "probe_async";
            report["auth_mode"] = "runner_forced_chatgpt";
            var rendererManifest = CheckRendererFiles(settings);
            report["renderer_manifest_sha256"] = rendererManifest;

            var paths = new Dictionary<string, string>();
            var inputs = new Dictionary<string, byte[]>();
            foreach (var name in new[] { "reference", "drawing", "ink" })
            {
                var path = Path.GetFullPath(options["--" + name]);
                if (!settings.IsTrustedInputFile(path)) throw new ProbeFailure("untrusted_" + name + "_input");
                var bytes = await ReadBoundedAsync(path, 8 * 1024 * 1024, ct);
                var image = PngCodec.DecodeRgba(bytes);
                if (image.Width != 1024 || image.Height != 1024) throw new ProbeFailure("input_dimensions_" + name);
                paths.Add(name, path); inputs.Add(name, bytes);
            }
            var inputHashes = inputs.ToDictionary(pair => pair.Key, pair => Hash(pair.Value));
            report["input_sha256"] = inputHashes;
            report["input_paths"] = paths;
            var specHashes = SpecificationHashes(spec, settings);
            report["spec_sha256"] = specHashes;
            report["prompt_versions"] = new { a = LunaCodexProvider.PromptAVersion, g = LunaCodexProvider.PromptGVersion,
                b = LunaCodexProvider.PromptBVersion, j = LunaCodexProvider.PromptJVersion };
            var runId = Guid.NewGuid();
            report["run_id"] = runId.ToString("N");
            var cache = Path.Combine(settings.TrustedInputRoot, "spell-visual-doctor", runId.ToString("N"));
            if (!CodexSettings.IsPathInside(cache, settings.TrustedInputRoot, false) || CodexSettings.HasReparsePoint(cache) || Directory.Exists(cache))
                throw new ProbeFailure("probe_cache_untrusted");
            Directory.CreateDirectory(Path.Combine(cache, "artifacts"));
            report["cache_directory"] = cache;
            JsonElement? reused = null;
            if (options.TryGetValue("--reuse-report", out var reusePath))
            {
                var oldBytes = await ReadReusableReportAsync(reusePath, options["--reuse-report-sha256"], settings, ct);
                using var previous = JsonDocument.Parse(oldBytes);
                var old = previous.RootElement;
                if (old.GetProperty("kind").GetString() != Kind || old.GetProperty("result").GetString() == "success" ||
                    old.GetProperty("provider_call_pending").GetBoolean() ||
                    !string.Equals(old.GetProperty("service_identity").GetString(), settings.ExpectedServiceUser, StringComparison.OrdinalIgnoreCase) ||
                    old.GetProperty("cli_executable_sha256").GetString() != nativeHash ||
                    old.GetProperty("image_cli_executable_sha256").GetString() != imageNativeHash ||
                    !MatchesHashes(old.GetProperty("spec_sha256"), specHashes) ||
                    !MatchesHashes(old.GetProperty("input_sha256"), inputHashes))
                    throw new ProbeFailure("reuse_provenance_mismatch_or_uncertain");
                reused = old.Clone();
                report["reuse_source"] = new { path = Path.GetFullPath(reusePath), sha256 = Hash(oldBytes),
                    source_result = old.GetProperty("result").GetString(), hash_verified = true, native_and_spec_pins_verified = true };
            }
            await CheckpointAsync();
            var provider = new LunaCodexProvider(new CodexProcessRunner(settings), spec);
            var capabilities = await File.ReadAllTextAsync(Path.Combine(spec, "contracts", "capability-catalog.json"), ct);
            const string layout = "{\"canvas_width\":1024,\"canvas_height\":1024,\"reference_purpose\":\"identify_printed_guides_only\",\"semantic_regions\":false}";
            byte[] description;
            if (CanReuse("a")) description = await ReuseDocumentAsync("a", "description.json");
            else
            {
                await BeforeCallAsync("a");
                var a = await provider.ProbeInterpretAsync(runId.ToString("N"), Guid.NewGuid().ToString("N"),
                    paths["reference"], paths["drawing"], layout, capabilities, ct);
                await RecordStageAsync("a", a.Transport, a.Utf8, a.Sha256);
                RequireStage(a.Transport, a.Utf8, settings, "A");
                description = a.Utf8!;
            }
            phase = "description_validation";
            var descriptionIssues = SpellCompiler.ValidateWholeImageDescriptionJson(description, true, true, requireLifecycle: true);
            report["description_issue_codes"] = descriptionIssues.Select(issue => issue.Code).Distinct().ToArray();
            if (descriptionIssues.Count != 0) throw new ProbeFailure("description_rejected");
            await File.WriteAllBytesAsync(Path.Combine(cache, "description.json"), description, ct);
            report["description_sha256"] = Hash(description);
            report["stage_a_completed"] = true;
            await CheckpointAsync();

            phase = "geometry";
            var typedDescription = ContractJson.DeserializeStrict<SpellDescription>(description, "spell-description");
            var geometry = GeometryResolver.Resolve(inputs["ink"], typedDescription);
            report["geometry_issue_codes"] = geometry.Issues.Select(issue => issue.Code).Distinct().ToArray();
            if (!geometry.Success) throw new ProbeFailure("geometry_rejected");
            var geometryContext = "[" + string.Join(",", geometry.GeometryJson.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => Encoding.UTF8.GetString(pair.Value))) + "]";
            report["geometry"] = new { context_sha256 = Hash(Encoding.UTF8.GetBytes(geometryContext)),
                source_description_sha256 = Hash(description), source_ink_sha256 = inputHashes["ink"],
                geometry_ids = geometry.GeometryJson.Keys.OrderBy(id => id).ToArray(),
                algorithms = geometry.Assets.Values.Select(asset => asset.algorithm).Distinct().ToArray() };
            byte[] visualBytes;
            if (CanReuse("g"))
            {
                visualBytes = await ReuseDocumentAsync("g", "visual-reference.png");
                if (reused!.Value.GetProperty("description_sha256").GetString() != Hash(description))
                    throw new ProbeFailure("reuse_image_description_hash");
            }
            else
            {
                await BeforeCallAsync("g");
                var g = await provider.ProbeGenerateVisualReferenceAsync(runId.ToString("N"), Guid.NewGuid().ToString("N"), description, ct);
                var receipt = g.Transport.FinalJson is null ? null : Encoding.UTF8.GetBytes(g.Transport.FinalJson);
                await RecordStageAsync("g", g.Transport, receipt, receipt is null ? null : Hash(receipt));
                RequireStage(g.Transport, g.PngBytes, settings, "G");
                if (g.Transport.GeneratedImage is null || g.Sha256 != Hash(g.PngBytes!)) throw new ProbeFailure("generated_image_attestation_missing");
                visualBytes = g.PngBytes!;
                report["native_generated_image"] = g.Transport.GeneratedImage;
            }
            var dimensions = VisualReferencePng.Validate(visualBytes);
            var imageHash = Hash(visualBytes);
            var visualPath = Path.Combine(cache, "visual-reference.png");
            await File.WriteAllBytesAsync(visualPath, visualBytes, ct);
            report["visual_reference_sha256"] = imageHash;
            report["visual_reference"] = new { path = visualPath, sha256 = imageHash, size_bytes = visualBytes.Length,
                width_px = dimensions.Width, height_px = dimensions.Height, description_sha256 = Hash(description) };
            report["stage_g_completed"] = true;
            await CheckpointAsync();
            var reference = new ProviderReference(visualPath, imageHash);
            var metadata = new ContractReference { artifact_id = ArtifactId("reference", imageHash), sha256 = imageHash,
                size_bytes = visualBytes.Length, width_px = dimensions.Width, height_px = dimensions.Height,
                description_sha256 = Hash(description), prompt_version = LunaCodexProvider.PromptGVersion };

            byte[] plan;
            if (CanReuse("b")) plan = await ReuseDocumentAsync("b", "plan.json");
            else
            {
                await BeforeCallAsync("b");
                var b = await provider.ProbePlanAsync(runId.ToString("N"), Guid.NewGuid().ToString("N"), description,
                    geometryContext, capabilities, reference, ct);
                await RecordStageAsync("b", b.Transport, b.Utf8, b.Sha256);
                RequireStage(b.Transport, b.Utf8, settings, "B");
                plan = b.Utf8!;
            }
            phase = "plan_validation";
            var planIssues = SpellCompiler.ValidatePlanJson(description, plan, geometry.GeometryJson, geometry.MaskPng, metadata);
            report["plan_issue_codes"] = planIssues.Select(issue => issue.Code).Distinct().ToArray();
            report["plan_validation_status"] = planIssues.Count == 0 ? "success" : "rejected";
            if (planIssues.Count != 0) throw new ProbeFailure("plan_rejected");
            await File.WriteAllBytesAsync(Path.Combine(cache, "plan.json"), plan, ct);
            report["plan_sha256"] = Hash(plan);
            report["stage_b_completed"] = true;
            await CheckpointAsync();

            phase = "compilation";
            var compileInput = new CompilationInput {
                DescriptionJson = description, PlanJson = plan, GeometryJson = geometry.GeometryJson, MaskPng = geometry.MaskPng,
                GeometryArtifactIds = geometry.GeometryJson.ToDictionary(pair => pair.Key, pair => ArtifactId("geometry:" + pair.Key, Hash(pair.Value))),
                MaskArtifactIds = geometry.MaskPng.ToDictionary(pair => pair.Key, pair => ArtifactId("mask:" + pair.Key, Hash(pair.Value))),
                SpellId = "visual-probe-" + runId.ToString("N"), ParchmentId = "visual-probe-" + runId.ToString("N"),
                CreatedAt = DateTimeOffset.UtcNow.ToString("o"), MinimumClientVersion = "1.4.0",
                SignatureSeedHex = inputHashes["drawing"][..16], VisualReference = metadata,
                Provenance = new SpellProvenance { mode = "drawing", capture_sha256 = inputHashes["drawing"], reference_sha256 = inputHashes["reference"],
                    model_a = settings.InterpreterModel, model_b = settings.Model, prompt_a_version = LunaCodexProvider.PromptAVersion,
                    prompt_b_version = LunaCodexProvider.PromptBVersion, response_a_id = null, response_b_id = null }
            };
            var compiled = SpellCompiler.Compile(compileInput);
            report["compiler_version"] = SpellCompiler.Version;
            report["compilation_status"] = compiled.Success ? "success" : "rejected";
            report["compilation_issue_codes"] = compiled.Issues.Select(issue => issue.Code).Distinct().ToArray();
            if (!compiled.Success) throw new ProbeFailure("compilation_rejected");
            var assets = new Dictionary<string, byte[]> { [metadata.artifact_id] = visualBytes };
            foreach (var pair in compileInput.GeometryArtifactIds) assets.Add(pair.Value, geometry.GeometryJson[pair.Key]);
            foreach (var pair in compileInput.MaskArtifactIds) assets.Add(pair.Value, geometry.MaskPng[pair.Key]);
            foreach (var pair in assets) await File.WriteAllBytesAsync(Path.Combine(cache, "artifacts", pair.Key), pair.Value, ct);
            await File.WriteAllBytesAsync(Path.Combine(cache, "spell.json"), compiled.PayloadUtf8, ct);
            await File.WriteAllTextAsync(Path.Combine(cache, "spell.json.sha256"), compiled.PayloadSha256, Encoding.ASCII, ct);
            report["compiled_spell_sha256"] = compiled.PayloadSha256;
            report["compiled_cache_scope"] = "private_probe_only_not_published";
            await CheckpointAsync();

            phase = "capture";
            await CheckpointAsync();
            var capture = await new TrustedVisualCapture(settings).CaptureAsync(runId, compiled.PayloadUtf8, description, assets, ct);
            await File.WriteAllBytesAsync(Path.Combine(cache, "capture.json"), capture.Manifest, ct);
            report["capture"] = new { manifest_sha256 = Hash(capture.Manifest), measured_fps = capture.MeasuredFps,
                frames = capture.Frames.Select(frame => new { phase = frame.Phase, path = frame.PngPath, sha256 = frame.Sha256 }).ToArray(),
                completed = true, presentation_only = true, fps_gate_passed = capture.MeasuredFps >= 30 };
            await CheckpointAsync();
            await BeforeCallAsync("j");
            var j = await provider.JudgeVisualAsync(runId.ToString("N"), Guid.NewGuid().ToString("N"), description,
                plan, reference, capture.Frames, null, probe: true, ct);
            await RecordStageAsync("j", j.Document.Transport, j.Document.Utf8, j.Document.Sha256);
            RequireStage(j.Document.Transport, j.Document.Utf8, settings, "J");
            await File.WriteAllBytesAsync(Path.Combine(cache, "verdict.json"), j.Document.Utf8!, ct);
            using var verdict = JsonDocument.Parse(j.Document.Utf8!);
            report["verdict"] = verdict.RootElement.Clone();
            report["score"] = j.Score;
            report["lifecycle_faithful"] = j.LifecycleFaithful;
            report["stage_j_completed"] = true;
            report["visual_target_reached"] = j.Score == 10000 && j.LifecycleFaithful && capture.MeasuredFps >= 30;
            report["result"] = "success";
            report["phase"] = phase = "completed";
            report["ended_at"] = DateTimeOffset.UtcNow;
            await CheckpointAsync();
            Console.WriteLine("SpellVisualDoctor completed; private report saved. Score " + j.Score + "/10000. No player spell published.");
            return 0;

            async Task BeforeCallAsync(string stage)
            {
                phase = "stage_" + stage;
                report["provider_call_pending"] = true;
                await CheckpointAsync();
            }
            async Task RecordStageAsync(string stage, CodexResult result, byte[]? bytes, string? sha)
            {
                if (result.ProcessStarted) calls++;
                report["new_model_calls_executed"] = calls;
                report["provider_call_pending"] = false;
                report["stage_" + stage] = StageEvidence(result, sha);
                report["stage_" + stage + "_model_call_executed"] = result.ProcessStarted;
                if (bytes is not null) await File.WriteAllBytesAsync(Path.Combine(cache, stage + "-response.json"), bytes, ct);
                await CheckpointAsync();
            }
            bool CanReuse(string stage) => reused is { } old && old.TryGetProperty("stage_" + stage + "_completed", out var done) && done.ValueKind == JsonValueKind.True;
            async Task<byte[]> ReuseDocumentAsync(string stage, string filename)
            {
                var old = reused!.Value;
                var historic = old.GetProperty("stage_" + stage);
                var requestedModel = stage == "a" ? settings.InterpreterModel : settings.Model;
                var requestedEffort = stage == "a" ? settings.InterpreterEffort : settings.Effort;
                if (historic.GetProperty("outcome").GetString() != "Success" ||
                    historic.GetProperty("exit_code").GetInt32() != 0 || !historic.GetProperty("process_started").GetBoolean() ||
                    historic.GetProperty("requested_model").GetString() != requestedModel || historic.GetProperty("reported_model").GetString() != requestedModel ||
                    historic.GetProperty("requested_effort").GetString() != requestedEffort || historic.GetProperty("reported_effort").GetString() != requestedEffort)
                    throw new ProbeFailure("reuse_stage_attestation");
                var path = Path.Combine(old.GetProperty("cache_directory").GetString()!, filename);
                if (!settings.IsTrustedInputFile(path)) throw new ProbeFailure("reuse_cache_untrusted");
                var bytes = await ReadBoundedAsync(path, 8 * 1024 * 1024, ct);
                var hashKey = stage switch { "a" => "description_sha256", "g" => "visual_reference_sha256", _ => "plan_sha256" };
                if (Hash(bytes) != old.GetProperty(hashKey).GetString()) throw new ProbeFailure("reuse_document_hash");
                var finalPath = historic.GetProperty("final_path").GetString();
                if (finalPath is null || !CodexSettings.IsTrustedFile(finalPath, settings.AttemptRoot) ||
                    Hash(await ReadBoundedAsync(finalPath, 2_000_000, ct)) != historic.GetProperty("final_sha256").GetString())
                    throw new ProbeFailure("reuse_native_final_hash");
                if (stage == "g")
                {
                    var original = old.GetProperty("native_generated_image");
                    var nativePath = original.GetProperty("SavedPath").GetString()!;
                    if (!CodexSettings.IsTrustedFile(nativePath, Path.Combine(settings.CodexHome, "generated_images")) ||
                        Hash(await ReadBoundedAsync(nativePath, 8 * 1024 * 1024, ct)) != Hash(bytes) ||
                        original.GetProperty("Sha256").GetString() != Hash(bytes)) throw new ProbeFailure("reuse_native_image_hash");
                    report["native_generated_image"] = original.Clone();
                }
                report["stage_" + stage] = historic.Clone();
                report["stage_" + stage + "_model_call_executed"] = false;
                report["stage_" + stage + "_reused"] = true;
                await CheckpointAsync();
                return bytes;
            }
        }
        catch (Exception error)
        {
            report["result"] = "failed";
            report["phase"] = phase;
            report["error_code"] = error is ProbeFailure failure ? failure.Code : phase + "_failed";
            report["error_type"] = error.GetType().Name;
            // Runtime exceptions may contain arbitrary provider text or private paths.
            // Persist stable codes and previously captured structured evidence only.
            if (error is InvalidDataException && error.Message.Length <= 100 && error.Message.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                report["runtime_error_code"] = error.Message;
            report["ended_at"] = DateTimeOffset.UtcNow;
            try { await CheckpointAsync(); }
            catch { Console.Error.WriteLine("private_report_write_failed"); }
            Console.Error.WriteLine("SpellVisualDoctor failed at " + phase + ": " + report["error_code"]);
            return 1;
        }

        async Task CheckpointAsync()
        {
            report["phase"] = phase;
            report["updated_at"] = DateTimeOffset.UtcNow;
            if (reportPath is null) return;
            var temporary = reportPath + ".tmp";
            if (CodexSettings.HasReparsePoint(reportPath) || CodexSettings.HasReparsePoint(temporary)) throw new ProbeFailure("report_reparse");
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(report, JsonOptions) + "\n", new UTF8Encoding(false));
            File.Move(temporary, reportPath, overwrite: true);
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        string[] required = ["--spec", "--reference", "--drawing", "--ink", "--report"];
        var allowed = required.Concat(["--reuse-report", "--reuse-report-sha256"]).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
            if (index + 1 >= args.Length || !allowed.Contains(args[index]) || string.IsNullOrWhiteSpace(args[index + 1]) ||
                !result.TryAdd(args[index], args[index + 1])) throw new ProbeFailure("invalid_arguments");
        if (required.Any(name => !result.ContainsKey(name)) || result.ContainsKey("--reuse-report") != result.ContainsKey("--reuse-report-sha256"))
            throw new ProbeFailure("missing_arguments");
        return result;
    }

    private static string PendingRoot(CodexSettings settings) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(settings.CodexHome).TrimEnd(Path.DirectorySeparatorChar))!, "evidence", "pending");
    private static string CheckedReportPath(string path, CodexSettings settings)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ProbeFailure("report_absolute_path_required");
        path = Path.GetFullPath(path);
        if (!CodexSettings.IsPathInside(path, PendingRoot(settings), false) || !Directory.Exists(Path.GetDirectoryName(path)) ||
            CodexSettings.HasReparsePoint(path) || File.Exists(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new ProbeFailure("report_path_untrusted_or_exists");
        return path;
    }
    private static async Task<byte[]> ReadReusableReportAsync(string path, string hash, CodexSettings settings, CancellationToken ct)
    {
        path = Path.GetFullPath(path);
        if (!CodexSettings.IsTrustedFile(path, PendingRoot(settings))) throw new ProbeFailure("reuse_report_untrusted");
        var bytes = await ReadBoundedAsync(path, 500_000, ct);
        if (Hash(bytes) != hash.ToLowerInvariant()) throw new ProbeFailure("reuse_report_hash");
        return bytes;
    }
    private static Dictionary<string, string> SpecificationHashes(string spec, CodexSettings settings)
    {
        string[] files = ["prompts/01_MODEL_A_INTERPRETE.md", "prompts/02_MODEL_B_TRADUCTEUR.md", "prompts/03_REPARATION_TECHNIQUE.md",
            "prompts/04_IMAGE_REFERENCE.md", "prompts/05_VISUAL_CRITIC.md", "contracts/capability-catalog.json", "contracts/effect-recipes.json",
            "contracts/effect-recipes-prompt.json", "contracts/codex/model-a.output-schema.json", "contracts/codex/model-b.output-schema.json",
            "contracts/codex/model-g.output-schema.json", "contracts/codex/model-j.output-schema.json"];
        return files.ToDictionary(file => file, file => {
            var path = Path.Combine(spec, file);
            if (!CodexSettings.IsTrustedFile(path, settings.TrustedSpecificationRoot)) throw new ProbeFailure("spec_file_untrusted");
            return CodexSettings.ComputeExecutableSha256(path);
        });
    }
    private static string CheckRendererFiles(CodexSettings settings)
    {
        var path = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_RENDERER_EXE") ?? "";
        var hash = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_RENDERER_MANIFEST_SHA256") ?? "";
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) || CodexSettings.HasReparsePoint(path) ||
            Path.GetFileName(path) != "Palimpseste.exe") throw new ProbeFailure("renderer_not_configured");
        var directory = Path.GetDirectoryName(path)!;
        if (CodexSettings.IsPathInside(directory, settings.TrustedInputRoot, true) ||
            CodexSettings.IsPathInside(directory, settings.DevelopmentRoot, true) ||
            CodexSettings.IsPathInside(directory, settings.AttemptRoot, true)) throw new ProbeFailure("renderer_directory_untrusted");
        var manifestPath = Path.Combine(directory, "renderer-manifest.json");
        if (!File.Exists(manifestPath) || CodexSettings.HasReparsePoint(manifestPath) ||
            new FileInfo(manifestPath).Length > 128_000 || CodexSettings.ComputeExecutableSha256(manifestPath) != hash)
            throw new ProbeFailure("renderer_manifest_mismatch");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        if (manifest.RootElement.GetProperty("version").GetString() != "1.4.0") throw new ProbeFailure("renderer_version_mismatch");
        foreach (var entry in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            var file = Path.GetFullPath(Path.Combine(directory, entry.GetProperty("file").GetString()!));
            if (!CodexSettings.IsTrustedFile(file, directory) || CodexSettings.ComputeExecutableSha256(file) != entry.GetProperty("sha256").GetString())
                throw new ProbeFailure("renderer_file_hash_mismatch");
        }
        return hash;
    }
    private static object StageEvidence(CodexResult result, string? sha) => new {
        outcome = result.Outcome.ToString(), error_code = result.ErrorCode, exit_code = result.ExitCode, cli_version = result.CliVersion,
        requested_model = result.RequestedModel, requested_effort = result.RequestedEffort,
        reported_model = result.ReportedModel, reported_effort = result.ReportedEffort, process_started = result.ProcessStarted,
        started_at = result.StartedAt, ended_at = result.EndedAt, elapsed_ms = Math.Max(0, (long)(result.EndedAt - result.StartedAt).TotalMilliseconds),
        usage = result.UsageJson, diagnostic_category = result.DiagnosticCategory,
        final_sha256 = sha, attempt_directory = result.AttemptDirectory,
        final_path = result.AttemptDirectory is null ? null : Path.Combine(result.AttemptDirectory, "final.json"),
        diagnostic_stdout_sha256 = result.DiagnosticStdoutSha256, diagnostic_stdout_length = result.DiagnosticStdoutLength
    };
    private static void RequireStage(CodexResult result, byte[]? bytes, CodexSettings settings, string stage)
    {
        if (bytes is null || result.Outcome != ProviderOutcome.Success) throw new ProbeFailure("stage_" + stage.ToLowerInvariant() + "_failed");
        var model = stage == "A" ? settings.InterpreterModel : settings.Model;
        var effort = stage == "A" ? settings.InterpreterEffort : settings.Effort;
        if (!result.ProcessStarted || result.ExitCode != 0 || result.RequestedModel != model || result.ReportedModel != model ||
            result.RequestedEffort != effort || result.ReportedEffort != effort) throw new ProbeFailure("stage_" + stage.ToLowerInvariant() + "_metadata_unverified");
    }
    private static bool MatchesHashes(JsonElement source, Dictionary<string, string> hashes) =>
        source.EnumerateObject().Count() == hashes.Count && hashes.All(pair => source.TryGetProperty(pair.Key, out var actual) && actual.GetString() == pair.Value);
    private static async Task<byte[]> ReadBoundedAsync(string path, int maximum, CancellationToken ct)
    {
        if (CodexSettings.HasReparsePoint(path)) throw new ProbeFailure("input_reparse");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length < 1 || file.Length > maximum) throw new ProbeFailure("input_size");
        var bytes = new byte[checked((int)file.Length)]; await file.ReadExactlyAsync(bytes, ct); return bytes;
    }
    private static bool SamePath(string first, string second) => string.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string ArtifactId(string role, string sha) => "a" + Hash(Encoding.UTF8.GetBytes(role + ":" + sha))[..32];
    private sealed class ProbeFailure(string code) : Exception(code) { public string Code { get; } = code; }
}
