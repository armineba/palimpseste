using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Palimpseste.Contracts;
using Palimpseste.Core;
using Palimpseste.Provider;
using Palimpseste.Worker;
using ProviderReference = Palimpseste.Provider.SpellVisualReference;

return await BlueprintDoctor.RunAsync(args);

internal static class BlueprintDoctor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Operator-only executable. Production authentication, allowlists, service identity, executable
    // pinning and evidence checks remain active. No ProbeAsync / fake executable / database access.
    public static async Task<int> RunAsync(string[] args)
    {
        var report = new Dictionary<string, object?> {
            ["schema_version"] = "sp.blueprint-v2-doctor/1.0", ["started_at"] = DateTimeOffset.UtcNow,
            ["result"] = "in_progress", ["execution_mode"] = "production_run_async", ["provider_call_pending"] = false,
            ["model_calls_executed"] = 0, ["attempts_submitted"] = 0, ["repairs_executed"] = 0,
            ["database_touched"] = false, ["player_library_touched"] = false, ["software_build_executed"] = false,
            ["image_generation_calls"] = 0, ["auth_changed"] = false, ["human_acceptance"] = "not_evaluated",
            ["full_validation_executed"] = false
        };
        var calls = new List<object>(); var artifacts = new List<object>(); var inputs = new Dictionary<string, string>();
        var inputPaths = new Dictionary<string, string>();
        string? reportPath = null; string? cache = null; var phase = "arguments"; int submitted = 0, executed = 0;
        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); }; var ct = cancel.Token;
        try {
            var options = Parse(args); var settings = CodexSettings.FromEnvironment();
            reportPath = CheckedReportPath(options["--report"], settings);
            var mode = options.GetValueOrDefault("--mode", "plan");
            var stopAfter = options.GetValueOrDefault("--stop-after", "core");
            report["mode"] = mode; report["stop_after"] = stopAfter;
            report["inputs_record"] = mode == "plan" ? "operator_frozen_fixture_description_not_a_real_A_result" : "operator_fixture_drawing_real_A_result";
            report["service_identity"] = Environment.UserName; report["expected_service_identity"] = settings.ExpectedServiceUser;
            await CheckpointAsync(); phase = "preflight";
            var spec = Path.GetFullPath(options["--spec"]);
            if (!SamePath(spec, settings.TrustedSpecificationRoot)) throw new DoctorFailure("spec_root_mismatch");
            var preflight = (mode == "drawing" ? new[] { "A", "B", "J" } : new[] { "B", "J" })
                .ToDictionary(stage => stage, stage => settings.Check(production: true, compatibilityProbe: false, stage: stage).ToArray());
            report["preflight_issues"] = preflight;
            if (preflight.Values.Any(list => list.Length > 0)) throw new DoctorFailure("production_preflight_failed");
            report["native_executable_sha256"] = CodexSettings.ComputeExecutableSha256(settings.Executable);
            report["auth_mode"] = "runner_forced_chatgpt";
            var specFiles = new[] { "prompts/06_BLUEPRINT_V2.md", "prompts/07_V2_CRITIC.md", "prompts/08_V2_INTERPRETATION.md",
                "prompts/09_V2_NUMERIC_RULES.md", "contracts/codex/model-b-v2.output-schema.json", "contracts/codex/model-j-v2.output-schema.json",
                "prompts/10_UNITY_GOD_RUNTIME.md", "contracts/codex/model-b-unity-god-v2.output-schema.json",
                "skills/unity-god/SKILL.md", "skills/unity-god/references/runtime.md", "skills/unity-god/references/methods.json",
                "contracts/capability-catalog.json", "assets/sourced-vfx/catalogue.json", "assets/sourced-vfx/references.json" };
            var specHashes = new Dictionary<string, string>();
            foreach (var name in specFiles) {
                var path = Path.Combine(spec, name);
                if (!settings.IsTrustedSpecificationFile(path)) throw new DoctorFailure("untrusted_specification");
                specHashes[name] = Hash(await ReadBoundedAsync(path, 1_000_000, ct));
            }
            report["spec_sha256"] = specHashes;
            var runId = Guid.NewGuid(); report["run_id"] = runId.ToString("N");
            cache = Path.Combine(settings.TrustedInputRoot, "blueprint-v2-doctor", runId.ToString("N"));
            if (!CodexSettings.IsPathInside(cache, settings.TrustedInputRoot, false) || CodexSettings.HasReparsePoint(cache) || Directory.Exists(cache))
                throw new DoctorFailure("cache_untrusted");
            Directory.CreateDirectory(cache); report["cache_directory"] = cache;
            foreach (var name in mode == "drawing" ? new[] { "drawing", "reference" } : new[] { "description", "drawing" }) {
                var path = Path.GetFullPath(options["--" + name]);
                if (!settings.IsTrustedInputFile(path)) throw new DoctorFailure("untrusted_input");
                var bytes = await ReadBoundedAsync(path, 8 * 1024 * 1024, ct);
                if (name != "description") {
                    var png = PngCodec.DecodeRgba(bytes);
                    if (png.Width != 1024 || png.Height != 1024) throw new DoctorFailure("fixture_png_dimensions");
                }
                inputPaths[name] = path; inputs[name] = Hash(bytes);
            }
            report["input_sha256"] = inputs;
            var provider = new LunaCodexProvider(new CodexProcessRunner(settings), spec);
            var capabilities = await File.ReadAllTextAsync(Path.Combine(spec, "contracts", "capability-catalog.json"), ct);
            byte[] description;
            if (mode == "drawing") {
                phase = "interpretation";
                var document = await CallAsync("A", () => provider.InterpretBlueprintV2Async(runId.ToString("N"), Guid.NewGuid().ToString("N"),
                    inputPaths["reference"], inputPaths["drawing"],
                    "{\"canvas_width\":1024,\"canvas_height\":1024,\"reference_purpose\":\"identify_printed_guides_only\",\"semantic_regions\":false}", capabilities, ct));
                description = RequireStage(document, "A", settings);
            }
            else description = await ReadBoundedAsync(inputPaths["description"], 8 * 1024 * 1024, ct);
            var descriptionIssues = SpellCompiler.ValidateDescriptionJson(description);
            report["description_validation_issues"] = descriptionIssues.Select(i => i.ToString()).ToArray();
            if (descriptionIssues.Count > 0) throw new DoctorFailure("description_invalid");
            await ArtifactAsync("description.json", description);
            phase = "research"; await CheckpointAsync();
            var research = await new SpellReferenceResearchResolver(spec).ResolveAsync(description,
                new ProviderReference(inputPaths["drawing"], inputs["drawing"]), ct, includeUnityGod: true);
            await ArtifactAsync("research.json", Encoding.UTF8.GetBytes(research.Json));
            phase = "blueprint";
            var planned = await CallAsync("B", () => provider.PlanBlueprintV2Async(runId.ToString("N"), Guid.NewGuid().ToString("N"),
                description, capabilities, research, null, null, "structural_core", ct));
            var plan = RequireStage(planned, "B", settings);
            await ArtifactAsync("plan.json", plan);
            if (planned.UnityGodReceipt is null || UnityGodMethods.ValidateReceipt(plan, planned.UnityGodReceipt, research).Count != 0)
                throw new DoctorFailure("unity_god_methods_invalid");
            await ArtifactAsync("methods.json", planned.UnityGodReceipt);
            var planIssues = SpellCompiler.ValidatePlanJson(description, plan, new Dictionary<string, byte[]>(), new Dictionary<string, byte[]>(), null, research.Sha256);
            report["plan_validation_issues"] = planIssues.Select(i => i.ToString()).ToArray();
            if (planIssues.Count != 0) throw new DoctorFailure("blueprint_invalid");
            phase = "compile";
            var compiled = SpellCompiler.Compile(new CompilationInput {
                DescriptionJson = description, PlanJson = plan, GeometryJson = new Dictionary<string, byte[]>(), MaskPng = new Dictionary<string, byte[]>(),
                GeometryArtifactIds = new Dictionary<string, string>(), MaskArtifactIds = new Dictionary<string, string>(),
                ReferenceResearchSha256 = research.Sha256, SpellId = "v2-doctor-" + runId.ToString("N"), ParchmentId = "isolated-operator-fixture",
                SignatureSeedHex = "e10a330a765bc981", MinimumClientVersion = "1.8.0", CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
                Provenance = new SpellProvenance { mode = "fixture", capture_sha256 = inputs["drawing"],
                    model_a = mode == "drawing" ? settings.InterpreterModel : null, model_b = settings.Model,
                    prompt_a_version = mode == "drawing" ? LunaCodexProvider.InterpreterV2PromptVersion : "fixture.frozen-description",
                    prompt_b_version = LunaCodexProvider.UnityGodV2PromptVersion }
            });
            report["compilation_issues"] = compiled.Issues.Select(i => i.ToString()).ToArray();
            if (!compiled.Success) throw new DoctorFailure("compile_rejected");
            await ArtifactAsync("spell.json", compiled.PayloadUtf8);
            var renderer = new TrustedVisualCapture(settings);
            phase = "core_capture"; await CheckpointAsync();
            var core = await renderer.CaptureV2Async(runId, compiled.PayloadUtf8, description, new Dictionary<string, byte[]>(), "core", ct);
            await ArtifactAsync("core-validation.json", core.Manifest); RecordFrames("core", core);
            phase = "blind_judgement";
            var blind = RequireStage(await CallAsync("J_blind", () => provider.JudgeBlueprintV2Async(runId.ToString("N"), Guid.NewGuid().ToString("N"),
                "blind", description, plan, core.Frames.Take(1).ToArray(), null, null, ct)), "J", settings);
            await ArtifactAsync("blind.json", blind);
            phase = "structure_judgement";
            var structure = RequireStage(await CallAsync("J_structure", () => provider.JudgeBlueprintV2Async(runId.ToString("N"), Guid.NewGuid().ToString("N"),
                "structure", description, plan, core.Frames.Take(5).ToArray(), Encoding.UTF8.GetString(blind), Encoding.UTF8.GetString(core.Manifest), ct, planned.UnityGodReceipt)), "J", settings);
            await ArtifactAsync("structure.json", structure);
            var coreAccepted = JudgePass(structure, "A_structure", "B_continuity", "semantic_blind") && MeasuredPass(core.Manifest, "B_continuity");
            report["core_accepted"] = coreAccepted;
            if (!coreAccepted) throw new DoctorFailure("core_validation_rejected");
            if (stopAfter == "full") {
                phase = "full_capture"; report["full_validation_executed"] = true; await CheckpointAsync();
                var full = await renderer.CaptureV2Async(runId, compiled.PayloadUtf8, description, new Dictionary<string, byte[]>(), "full", ct);
                await ArtifactAsync("full-validation.json", full.Manifest); RecordFrames("full", full);
                phase = "full_judgement";
                var frames = new[] { core.Frames[0] }.Concat(full.Frames.Take(4)).ToArray();
                var final = RequireStage(await CallAsync("J_full", () => provider.JudgeBlueprintV2Async(runId.ToString("N"), Guid.NewGuid().ToString("N"),
                    "full", description, plan, frames, Encoding.UTF8.GetString(blind), Encoding.UTF8.GetString(full.Manifest), ct, planned.UnityGodReceipt)), "J", settings);
                await ArtifactAsync("full-review.json", final);
                var passed = JudgePass(final, "C_rendering", "E_game_camera", "F_motion") && MeasuredPass(full.Manifest, "B_continuity") &&
                    MeasuredPass(full.Manifest, "D_impact") && MeasuredPass(full.Manifest, "performance") && full.MeasuredFps >= 30;
                report["full_accepted"] = passed;
                if (!passed) throw new DoctorFailure("full_validation_rejected");
            }
            phase = "immutable_input_verification";
            foreach (var pair in inputPaths)
                if (Hash(await ReadBoundedAsync(pair.Value, 8 * 1024 * 1024, ct)) != inputs[pair.Key]) throw new DoctorFailure("input_changed");
            foreach (var pair in specHashes)
                if (Hash(await ReadBoundedAsync(Path.Combine(spec, pair.Key), 1_000_000, ct)) != pair.Value) throw new DoctorFailure("specification_changed");
            report["result"] = stopAfter == "full" ? "full_passed" : "core_passed_full_not_run";
            report["completed_at"] = DateTimeOffset.UtcNow; phase = "completed"; await CheckpointAsync();
            Console.WriteLine("V2 doctor completed: " + report["result"] + "; real model calls: " + executed);
            return 0;

            async Task<ProviderDocument> CallAsync(string name, Func<Task<ProviderDocument>> action)
            {
                submitted++; report["attempts_submitted"] = submitted; report["provider_call_pending"] = true; await CheckpointAsync();
                var document = await action();
                if (document.Transport.ProcessStarted) executed++;
                report["model_calls_executed"] = executed; report["provider_call_pending"] = false;
                var t = document.Transport;
                calls.Add(new { stage = name, started_at = t.StartedAt, ended_at = t.EndedAt, process_started = t.ProcessStarted,
                    outcome = t.Outcome.ToString(), error_code = t.ErrorCode, t.ExitCode, t.CliVersion, requested_model = t.RequestedModel,
                    requested_effort = t.RequestedEffort, reported_model = t.ReportedModel, reported_effort = t.ReportedEffort,
                    usage = t.UsageJson, response_sha256 = document.Sha256, attempt_directory = t.AttemptDirectory,
                    diagnostic_category = t.DiagnosticCategory, stdout_sha256 = t.DiagnosticStdoutSha256 });
                await CheckpointAsync(); return document;
            }
        }
        catch (Exception e) {
            report["result"] = "failed"; report["failed_at"] = DateTimeOffset.UtcNow;
            report["failure_code"] = e is DoctorFailure failure ? failure.Code : e is VisualCaptureException visual ? visual.Reason :
                e is OperationCanceledException ? "cancelled" : "unexpected_" + e.GetType().Name;
            report["exception_type"] = e.GetType().Name;
            // A pending provider call is never labelled uncharged or safe to replay after an exception.
            try { await CheckpointAsync(); } catch { Console.Error.WriteLine("V2 doctor could not save its final private report."); }
            Console.Error.WriteLine("V2 doctor stopped: " + report["failure_code"] + "; phase: " + phase);
            return 2;
        }

        async Task ArtifactAsync(string name, byte[] bytes)
        {
            var path = Path.Combine(cache!, name);
            if (File.Exists(path) || CodexSettings.HasReparsePoint(path)) throw new DoctorFailure("artifact_already_exists");
            await File.WriteAllBytesAsync(path, bytes, ct);
            artifacts.Add(new { file = name, sha256 = Hash(bytes), size_bytes = bytes.Length });
            await CheckpointAsync();
        }
        void RecordFrames(string key, V2CaptureOutput capture) => report[key + "_capture"] = new {
            measured_fps = capture.MeasuredFps, blueprint_sha256 = capture.BlueprintsSha256,
            frames = capture.Frames.Select(f => new { phase = f.Phase, sha256 = f.Sha256, path = f.PngPath }).ToArray()
        };
        async Task CheckpointAsync()
        {
            report["phase"] = phase; report["updated_at"] = DateTimeOffset.UtcNow; report["calls"] = calls; report["artifacts"] = artifacts;
            if (reportPath is null) return;
            var temporary = reportPath + ".tmp";
            if (CodexSettings.HasReparsePoint(reportPath) || CodexSettings.HasReparsePoint(temporary)) throw new DoctorFailure("report_reparse");
            await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(report, JsonOptions));
            File.Move(temporary, reportPath, overwrite: true);
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var allowed = new HashSet<string> { "--spec", "--drawing", "--reference", "--description", "--report", "--mode", "--stop-after" };
        var result = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !allowed.Contains(args[i]) || !result.TryAdd(args[i], args[i + 1])) throw new DoctorFailure("invalid_arguments");
        if (new[] { "--spec", "--drawing", "--report" }.Any(key => !result.ContainsKey(key)) ||
            result.GetValueOrDefault("--mode", "plan") is not ("plan" or "drawing") ||
            result.GetValueOrDefault("--stop-after", "core") is not ("core" or "full")) throw new DoctorFailure("missing_arguments");
        var mode = result.GetValueOrDefault("--mode", "plan");
        if (mode == "plan" && !result.ContainsKey("--description") || mode == "drawing" && !result.ContainsKey("--reference"))
            throw new DoctorFailure("missing_mode_inputs");
        return result;
    }
    private static byte[] RequireStage(ProviderDocument document, string stage, CodexSettings settings)
    {
        var t = document.Transport; var model = stage == "A" ? settings.InterpreterModel : settings.Model;
        var effort = stage == "A" ? settings.InterpreterEffort : settings.Effort;
        if (document.Utf8 is null || t.Outcome != ProviderOutcome.Success) throw new DoctorFailure("provider_" + stage.ToLowerInvariant() + "_failed");
        if (!t.ProcessStarted || t.ExitCode != 0 || t.RequestedModel != model || t.ReportedModel != model ||
            t.RequestedEffort != effort || t.ReportedEffort != effort) throw new DoctorFailure("provider_metadata_unverified");
        if (Hash(document.Utf8) != document.Sha256) throw new DoctorFailure("provider_response_hash");
        return document.Utf8;
    }
    private static bool JudgePass(byte[] bytes, params string[] gates)
    {
        using var parsed = JsonDocument.Parse(bytes); var r = parsed.RootElement;
        return r.GetProperty("score_milli").GetInt32() >= 8000 &&
            (!gates.Contains("semantic_blind") || r.GetProperty("semantic_match").GetBoolean()) &&
            gates.All(g => r.GetProperty("gates").GetProperty(g).GetString() == "pass");
    }
    private static bool MeasuredPass(byte[] bytes, string name)
    {
        using var parsed = JsonDocument.Parse(bytes);
        return parsed.RootElement.GetProperty("gates").EnumerateArray().Any(g =>
            g.GetProperty("gate").GetString() == name && g.GetProperty("status").GetString() is "pass" or "technical_pass");
    }
    private static string CheckedReportPath(string path, CodexSettings settings)
    {
        var pending = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settings.CodexHome).TrimEnd(Path.DirectorySeparatorChar))!, "evidence", "pending");
        if (!Path.IsPathFullyQualified(path)) throw new DoctorFailure("report_path_must_be_absolute");
        path = Path.GetFullPath(path);
        if (!CodexSettings.IsPathInside(path, pending, false) || !Directory.Exists(Path.GetDirectoryName(path)) ||
            File.Exists(path) || CodexSettings.HasReparsePoint(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new DoctorFailure("report_path_untrusted_or_exists");
        return path;
    }
    private static async Task<byte[]> ReadBoundedAsync(string path, int maximum, CancellationToken ct)
    {
        if (CodexSettings.HasReparsePoint(path)) throw new DoctorFailure("input_reparse");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length < 1 || stream.Length > maximum) throw new DoctorFailure("input_size");
        var bytes = new byte[(int)stream.Length]; await stream.ReadExactlyAsync(bytes, ct); return bytes;
    }
    private static bool SamePath(string first, string second) => string.Equals(Path.GetFullPath(first).TrimEnd('\\', '/'), Path.GetFullPath(second).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class DoctorFailure(string code) : Exception(code) { public string Code { get; } = code; }
}
