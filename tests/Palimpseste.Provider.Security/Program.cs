using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using Palimpseste.Provider;

if (args.Length > 0 && string.Equals(args[0], "exec", StringComparison.Ordinal))
{
    await RunFakeProviderAsync(args[1..]);
    return;
}

var root = Path.Combine("E:\\PalimpsesteProviderSecurity", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var home = Path.Combine(root, "codex-home");
var attempts = Path.Combine(root, "attempts");
var spec = Path.Combine(root, "spec");
var input = Path.Combine(root, "inputs");
var development = Path.Combine(root, "development");
foreach (var directory in new[] { home, attempts, spec, input, development }) Directory.CreateDirectory(directory);

var fakeCommand = Environment.ProcessPath ?? throw new InvalidOperationException("Test executable path unavailable.");

var schema = Path.Combine(spec, "model-a.output-schema.json");
await File.WriteAllTextAsync(schema, "{\"type\":\"object\"}");
var divergentModelSchema = Path.Combine(spec, "model-a-divergent-model.output-schema.json");
var divergentEffortSchema = Path.Combine(spec, "model-a-divergent-effort.output-schema.json");
var missingModelSchema = Path.Combine(spec, "model-a-missing-model.output-schema.json");
var missingEffortSchema = Path.Combine(spec, "model-a-missing-effort.output-schema.json");
var invalidJsonSchema = Path.Combine(spec, "model-a-invalid-json.output-schema.json");
var exitStderrSchema = Path.Combine(spec, "model-a-exit-stderr.output-schema.json");
var exitJsonlSchema = Path.Combine(spec, "model-a-exit-jsonl.output-schema.json");
var creditsExhaustedSchema = Path.Combine(spec, "model-a-credits-exhausted.output-schema.json");
var missingAttestationSchema = Path.Combine(spec, "model-a-missing-attestation.output-schema.json");
var duplicateAttestationSchema = Path.Combine(spec, "model-a-duplicate-attestation.output-schema.json");
var outOfOrderAttestationSchema = Path.Combine(spec, "model-a-out-of-order-attestation.output-schema.json");
var arbitraryMetadataSchema = Path.Combine(spec, "model-a-arbitrary-metadata.output-schema.json");
var invalidAttestationSchema = Path.Combine(spec, "model-a-invalid-attestation.output-schema.json");
await File.WriteAllTextAsync(divergentModelSchema, "{\"type\":\"object\",\"x-test\":\"divergent-model\"}");
await File.WriteAllTextAsync(divergentEffortSchema, "{\"type\":\"object\",\"x-test\":\"divergent-effort\"}");
await File.WriteAllTextAsync(missingModelSchema, "{\"type\":\"object\",\"x-test\":\"missing-model\"}");
await File.WriteAllTextAsync(missingEffortSchema, "{\"type\":\"object\",\"x-test\":\"missing-effort\"}");
await File.WriteAllTextAsync(invalidJsonSchema, "{\"type\":\"object\",\"x-test\":\"invalid-json\"}");
await File.WriteAllTextAsync(exitStderrSchema, "{\"type\":\"object\",\"x-test\":\"exit-stderr\"}");
await File.WriteAllTextAsync(exitJsonlSchema, "{\"type\":\"object\",\"x-test\":\"exit-jsonl\"}");
await File.WriteAllTextAsync(creditsExhaustedSchema, "{\"type\":\"object\",\"x-test\":\"credits-exhausted\"}");
await File.WriteAllTextAsync(missingAttestationSchema, "{\"type\":\"object\",\"x-test\":\"missing-attestation\"}");
await File.WriteAllTextAsync(duplicateAttestationSchema, "{\"type\":\"object\",\"x-test\":\"duplicate-attestation\"}");
await File.WriteAllTextAsync(outOfOrderAttestationSchema, "{\"type\":\"object\",\"x-test\":\"out-of-order-attestation\"}");
await File.WriteAllTextAsync(arbitraryMetadataSchema, "{\"type\":\"object\",\"x-test\":\"arbitrary-metadata\"}");
await File.WriteAllTextAsync(invalidAttestationSchema, "{\"type\":\"object\",\"x-test\":\"invalid-attestation\"}");
var reference = Path.Combine(input, "reference.png");
var drawing = Path.Combine(input, "drawing.png");
await File.WriteAllBytesAsync(reference, SecurityFixtures.Png1024);
await File.WriteAllBytesAsync(drawing, SecurityFixtures.Png1024);
var outside = Path.Combine(root, "outside-secret.txt");
await File.WriteAllTextAsync(outside, "server secret sentinel");

var previousFallback = Environment.GetEnvironmentVariable("PALIMPSESTE_ALLOW_MODEL_FALLBACK");
var previousDowngrade = Environment.GetEnvironmentVariable("PALIMPSESTE_ALLOW_REASONING_DOWNGRADE");
var previousTools = Environment.GetEnvironmentVariable("PALIMPSESTE_RUNTIME_EXECUTION_TOOLS");
var previousCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
var previousApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var previousCodexApiKey = Environment.GetEnvironmentVariable("CODEX_API_KEY");
Environment.SetEnvironmentVariable("PALIMPSESTE_ALLOW_MODEL_FALLBACK", "false");
Environment.SetEnvironmentVariable("PALIMPSESTE_ALLOW_REASONING_DOWNGRADE", "false");
Environment.SetEnvironmentVariable("PALIMPSESTE_RUNTIME_EXECUTION_TOOLS", "false");
Environment.SetEnvironmentVariable("OPENAI_API_KEY", "personal-secret-sentinel");
Environment.SetEnvironmentVariable("CODEX_API_KEY", "personal-codex-api-key-sentinel");
Environment.SetEnvironmentVariable("CODEX_HOME", "E:\\PersonalCodexSentinel");

try
{
    var settings = new CodexSettings(
        fakeCommand, home, attempts, Environment.UserName, "gpt-5.6-luna", "max", true,
        TimeSpan.FromSeconds(20), 2_000_000, spec, input, development);
    var productionIssues = settings.Check(true);
    Assert(productionIssues.Contains("spec_tree_service_is_owner") || productionIssues.Contains("spec_tree_service_write_access"),
        "an insecure fixture must be refused by the production ACL gate");
    Assert(productionIssues.Contains("development_root_deny_incomplete_or_not_inherited"),
        "a development tree without a complete inherited deny must be refused");
    if (OperatingSystem.IsWindows())
    {
        var serviceSid = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Current SID unavailable.");
        var isolatedDevelopment = new DirectoryInfo(development);
        var access = isolatedDevelopment.GetAccessControl(AccessControlSections.Access);
        var partialRule = new FileSystemAccessRule(serviceSid, FileSystemRights.ReadData,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Deny);
        access.AddAccessRule(partialRule);
        isolatedDevelopment.SetAccessControl(access);
        try
        {
            Assert(settings.Check(true).Contains("development_root_deny_incomplete_or_not_inherited"),
                "a read-only partial deny must not prove source isolation");
        }
        finally
        {
            access.RemoveAccessRuleSpecific(partialRule);
            isolatedDevelopment.SetAccessControl(access);
        }
    }

    var featureEvidence = Path.Combine(root, "feature-evidence.json");
    var validFeatureJson = JsonSerializer.Serialize(new
    {
        kind = "provider_feature_doctor", service_identity = Environment.UserName,
        model = "gpt-5.6-luna", effort = "max", disable_flag_parser_status = "ok",
        effective_disable_observed = true,
        cli_executable_sha256 = CodexSettings.ComputeExecutableSha256(fakeCommand)
    });
    await File.WriteAllTextAsync(featureEvidence, validFeatureJson);
    var featureSettings = settings with
    {
        RuntimeFeaturesCompatibilityVerified = true,
        RuntimeFeaturesEvidencePath = featureEvidence,
        RuntimeFeaturesEvidenceSha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(featureEvidence)))
    };
    Assert(!featureSettings.Check(true).Contains("runtime_feature_evidence_content_invalid"),
        "a doctor evidence hash must bind to the configured executable");
    var otherExecutable = Path.Combine(root, "other-codex.exe");
    await File.WriteAllTextAsync(otherExecutable, "different executable bytes");
    Assert((featureSettings with { Executable = otherExecutable }).Check(true).Contains("runtime_feature_evidence_content_invalid"),
        "a replaced CLI must invalidate its previous doctor evidence");
    var effortEvidence = Path.Combine(root, "effort-evidence.json");
    var aAttemptDirectory = Path.Combine(attempts, "active-a-attempt");
    var bAttemptDirectory = Path.Combine(attempts, "plan-b-attempt");
    Directory.CreateDirectory(aAttemptDirectory);
    Directory.CreateDirectory(bAttemptDirectory);
    var aFinal = Path.Combine(aAttemptDirectory, "final.json");
    var bFinal = Path.Combine(bAttemptDirectory, "final.json");
    await File.WriteAllTextAsync(aFinal, "{\"synthetic_a\":true}");
    await File.WriteAllTextAsync(bFinal, "{\"synthetic_b\":true}");
    var aSha = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(aFinal)));
    var bSha = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(bFinal)));
    var inkSha = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(drawing)));
    var contextSha = new string('c', 64);
    var stageA = new
    {
        outcome = "Success", requested_model = "gpt-5.6-luna", requested_effort = "max",
        reported_model = "gpt-5.6-luna", reported_effort = "max",
        process_started = true, exit_code = 0, final_sha256 = aSha,
        attempt_directory = aAttemptDirectory
    };
    var stageB = new
    {
        outcome = "Success", requested_model = "gpt-5.6-luna", requested_effort = "max",
        reported_model = "gpt-5.6-luna", reported_effort = "max",
        process_started = true, exit_code = 0, final_sha256 = bSha,
        attempt_directory = bAttemptDirectory
    };
    await File.WriteAllTextAsync(effortEvidence, JsonSerializer.Serialize(new
    {
        kind = "provider_doctor", mode = "active", active_result = "success", plan_validation_status = "success",
        compilation_status = "success", compiler_version = "sp.compiler/1.0",
        compiled_probe_sha256 = contextSha,
        requested_model = "gpt-5.6-luna", requested_effort = "max",
        cli_executable_sha256 = CodexSettings.ComputeExecutableSha256(fakeCommand),
        expected_service_identity = Environment.UserName, service_identity = Environment.UserName,
        stage_a = stageA, stage_b = stageB,
        geometry = new { source = "resolver_from_ink_and_description",
            description_sha256 = aSha, ink_sha256 = inkSha, context_sha256 = contextSha }
    }));
    var effortSettings = settings with
    {
        CompatibilityEvidencePath = effortEvidence,
        CompatibilityEvidenceSha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(effortEvidence)))
    };
    Assert(!effortSettings.Check(true).Contains("effort_evidence_content_invalid"),
        "an active doctor evidence hash must bind to the configured executable");
    Assert((effortSettings with { Executable = otherExecutable }).Check(true).Contains("effort_evidence_content_invalid"),
        "a replaced CLI must invalidate its previous active effort evidence");

    var approvedEvidence = Path.Combine(root, "approved-evidence");
    Directory.CreateDirectory(approvedEvidence);
    var activeAReport = Path.Combine(approvedEvidence, "active-a.json");
    await File.WriteAllTextAsync(activeAReport, JsonSerializer.Serialize(new
    {
        kind = "provider_doctor", mode = "active", active_result = "success",
        requested_model = "gpt-5.6-luna", requested_effort = "max",
        cli_executable_sha256 = CodexSettings.ComputeExecutableSha256(fakeCommand),
        expected_service_identity = Environment.UserName, service_identity = Environment.UserName,
        stage_a = stageA,
        // This obsolete doctor's apparent B success must never count as B proof.
        stage_b = new { outcome = "Success", reported_effort = "max" }
    }));
    Assert((effortSettings with
    {
        CompatibilityEvidencePath = activeAReport,
        CompatibilityEvidenceSha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(activeAReport)))
    }).Check(true).Contains("effort_evidence_content_invalid"),
        "old active doctor success without validated resolved geometry must not open the worker");
    var planBReport = Path.Combine(approvedEvidence, "plan-b.json");
    var geometryEvidence = new { source = "resolver_from_ink_and_description",
        description_sha256 = aSha, ink_sha256 = inkSha, context_sha256 = contextSha };
    var validPlanEvidence = new
    {
        kind = "provider_doctor", mode = "plan", active_result = "success",
        requested_model = "gpt-5.6-luna", requested_effort = "max",
        cli_executable_sha256 = CodexSettings.ComputeExecutableSha256(fakeCommand),
        expected_service_identity = Environment.UserName, service_identity = Environment.UserName,
        stage_a_reuse = new { source_file = aFinal,
            sha256 = aSha, hash_verified = true, model_call_executed = false },
        stage_b_model_call_executed = true, stage_b = stageB, geometry = geometryEvidence
    };
    await File.WriteAllTextAsync(planBReport, JsonSerializer.Serialize(validPlanEvidence));
    Assert((effortSettings with
    {
        CompatibilityEvidencePath = planBReport,
        CompatibilityEvidenceSha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(planBReport)))
    }).Check(true).Contains("effort_evidence_content_invalid"),
        "B-only report cannot stand alone as proof of two real model calls");
    var offlineReport = Path.Combine(approvedEvidence, "offline-validation.json");
    var validOfflineEvidence = new
    {
        kind = "provider_offline_validation", mode = "validate", result = "success",
        validation_status = "success", compilation_status = "success",
        requested_model = "gpt-5.6-luna", requested_effort = "max",
        cli_executable_sha256 = CodexSettings.ComputeExecutableSha256(fakeCommand),
        expected_service_identity = Environment.UserName, service_identity = Environment.UserName,
        model_calls_executed = false,
        description_file = aFinal, plan_file = bFinal, ink_file = drawing,
        description_sha256 = aSha, plan_sha256 = bSha, ink_sha256 = inkSha,
        geometry_source = "resolver_from_ink_and_description",
        geometry_resolver_version = "sp.geometry.resolver/1.0",
        geometry_context_sha256 = contextSha,
        compiler_version = "sp.compiler/1.0", compiled_probe_sha256 = contextSha,
        doctor_executable_sha256 = CodexSettings.ComputeExecutableSha256(fakeCommand)
    };
    await File.WriteAllTextAsync(offlineReport, JsonSerializer.Serialize(validOfflineEvidence));
    var compositeEvidence = Path.Combine(approvedEvidence, "composite.json");
    async Task WriteCompositeAsync(string planReportSha, string offlineReportSha)
    {
        await File.WriteAllTextAsync(compositeEvidence, JsonSerializer.Serialize(new
        {
            kind = "provider_doctor_composite", format_version = 2,
            active_a_report = new { path = activeAReport,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(activeAReport))) },
            plan_b_report = new { path = planBReport,
                sha256 = planReportSha },
            offline_validation_report = new { path = offlineReport,
                sha256 = offlineReportSha }
        }));
    }
    string PlanReportSha() => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(planBReport)));
    string OfflineReportSha() => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(offlineReport)));
    await WriteCompositeAsync(PlanReportSha(), OfflineReportSha());
    CodexSettings WithComposite() => effortSettings with
    {
        CompatibilityEvidencePath = compositeEvidence,
        CompatibilityEvidenceSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(compositeEvidence)))
    };
    Assert(!WithComposite().Check(true).Contains("effort_evidence_content_invalid"),
        "hash-linked A/B reports and offline compiled validation must pass the composite gate");
    await File.WriteAllTextAsync(planBReport, JsonSerializer.Serialize(validPlanEvidence), new UTF8Encoding(true));
    var bomPlanBytes = await File.ReadAllBytesAsync(planBReport);
    Assert(bomPlanBytes.AsSpan(0, 3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }),
        "the source report fixture must have a UTF-8 BOM");
    await WriteCompositeAsync(PlanReportSha(), OfflineReportSha());
    Assert(!WithComposite().Check(true).Contains("effort_evidence_content_invalid"),
        "a source report with UTF-8 BOM must pass when its hash covers the complete file bytes");
    await WriteCompositeAsync(new string('b', 64), OfflineReportSha());
    Assert(WithComposite().Check(true).Contains("effort_evidence_content_invalid"),
        "a UTF-8 BOM source report must still fail when its recorded hash is wrong");
    await File.WriteAllTextAsync(planBReport, JsonSerializer.Serialize(validPlanEvidence), new UTF8Encoding(false));
    await WriteCompositeAsync(PlanReportSha(), OfflineReportSha());
    await File.WriteAllTextAsync(aFinal, "tampered A");
    Assert(WithComposite().Check(true).Contains("effort_evidence_content_invalid"),
        "composite proof must reject changed A final bytes");
    await File.WriteAllTextAsync(aFinal, "{\"synthetic_a\":true}");
    await File.WriteAllTextAsync(bFinal, "tampered B");
    Assert(WithComposite().Check(true).Contains("effort_evidence_content_invalid"),
        "composite proof must reject changed B final bytes");
    await File.WriteAllTextAsync(bFinal, "{\"synthetic_b\":true}");
    await WriteCompositeAsync(new string('b', 64), OfflineReportSha());
    Assert(WithComposite().Check(true).Contains("effort_evidence_content_invalid"),
        "composite proof must reject a mismatched source report hash");
    await File.WriteAllTextAsync(offlineReport, JsonSerializer.Serialize(new
    {
        kind = "provider_offline_validation", mode = "validate", result = "success",
        validation_status = "success", compilation_status = "not_run",
        requested_model = "gpt-5.6-luna", requested_effort = "max",
        cli_executable_sha256 = CodexSettings.ComputeExecutableSha256(fakeCommand),
        expected_service_identity = Environment.UserName, service_identity = Environment.UserName,
        model_calls_executed = false,
        description_file = aFinal, plan_file = bFinal, ink_file = drawing,
        description_sha256 = aSha, plan_sha256 = bSha, ink_sha256 = inkSha,
        geometry_source = "resolver_from_ink_and_description",
        geometry_resolver_version = "sp.geometry.resolver/1.0",
        geometry_context_sha256 = contextSha,
        compiler_version = "sp.compiler/1.0", compiled_probe_sha256 = contextSha,
        doctor_executable_sha256 = CodexSettings.ComputeExecutableSha256(fakeCommand)
    }));
    await WriteCompositeAsync(PlanReportSha(), OfflineReportSha());
    Assert(WithComposite().Check(true).Contains("effort_evidence_content_invalid"),
        "a report without actual controlled compilation must not open the worker");
    await File.WriteAllTextAsync(offlineReport, JsonSerializer.Serialize(validOfflineEvidence));
    await File.WriteAllTextAsync(planBReport, JsonSerializer.Serialize(new
    {
        kind = "provider_doctor", mode = "plan", active_result = "success",
        requested_model = "gpt-5.6-luna", requested_effort = "max",
        cli_executable_sha256 = CodexSettings.ComputeExecutableSha256(fakeCommand),
        expected_service_identity = Environment.UserName, service_identity = Environment.UserName,
        stage_a_reuse = new { source_file = aFinal,
            sha256 = new string('c', 64), hash_verified = true, model_call_executed = false },
        stage_b_model_call_executed = true, stage_b = stageB, geometry = geometryEvidence
    }));
    await WriteCompositeAsync(PlanReportSha(), OfflineReportSha());
    Assert(WithComposite().Check(true).Contains("effort_evidence_content_invalid"),
        "composite proof must reject a B report bound to another A output");
    Assert(settings.Check(false).Count == 0, "transport fixture must pass non-production isolation check: " + string.Join(',', settings.Check(false)));
    Assert((settings with { Effort = "xhigh" }).Check(false).Contains("effort_below_documented_max"),
        "the worker must reject a configured effort below the documented maximum");
    var overlap = settings with { TrustedInputRoot = attempts };
    Assert(overlap.Check(false).Contains("attempt_root_and_input_root_overlap"),
        "attempt state and trusted input must not share a root");
    var profile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetEnvironmentVariable("HOME");
    if (!string.IsNullOrWhiteSpace(profile) && Directory.Exists(profile))
        Assert((settings with { CodexHome = profile }).Check(false).Contains("codex_home_inside_user_profile"),
            "a personal profile must not become CODEX_HOME");

    var runner = new CodexProcessRunner(settings);
    // TransportProbeAsync intentionally exercises transport only; production
    // RunAsync and the active doctor ProbeAsync retain the isolation gate.
    var productionBlocked = await new CodexProcessRunner(settings with { EffortCompatibilityVerified = false }).RunAsync(new(
        Guid.NewGuid().ToString("N"), "A", "production must require evidence", schema,
        [reference, drawing], "job-security"), CancellationToken.None);
    Assert(productionBlocked.ErrorCode?.Contains("effort_not_verified", StringComparison.Ordinal) == true &&
        !productionBlocked.ProcessStarted && productionBlocked.AttemptDirectory is null,
        "RunAsync must retain the production effort evidence gate");

    var preflightBlocked = await new CodexProcessRunner(settings with { Effort = "xhigh" }).ProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "preflight must block before launch", schema,
        [reference, drawing], "job-security"), CancellationToken.None);
    Assert(preflightBlocked.ErrorCode?.Contains("effort_below_documented_max", StringComparison.Ordinal) == true &&
        preflightBlocked.ErrorCode?.Contains("runtime_feature_disable_not_verified", StringComparison.Ordinal) == true &&
        !preflightBlocked.ProcessStarted && preflightBlocked.AttemptDirectory is null,
        "ProbeAsync must defer only effort evidence and retain the runtime feature gate");

    var valid = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "prompt contains & whoami and > pwned.txt", schema,
        [reference, drawing], "job-security"), CancellationToken.None);
    Assert(valid.Outcome == ProviderOutcome.Success, "valid fake Codex invocation should succeed: " + valid.ErrorCode + " dir=" + valid.AttemptDirectory);
    Assert(valid.ProcessStarted, "a successful fake invocation must report that the process started");
    Assert(valid.FinalJson?.Contains("sp.description/1.0", StringComparison.Ordinal) == true, "final JSON must be read from output-last-message");
    Assert(valid.AttemptDirectory is not null && File.Exists(Path.Combine(valid.AttemptDirectory, "attempt.json")), "attempt evidence must be persisted");

    var argumentCapture = Path.Combine(valid.AttemptDirectory!, "fake-arguments.txt");
    Assert(File.Exists(argumentCapture), "fake executable must have received structured arguments");
    var arguments = await File.ReadAllTextAsync(argumentCapture);
    Assert(arguments.Contains("--model\ngpt-5.6-luna", StringComparison.Ordinal), "model must be an argument, not a shell fragment");
    Assert(arguments.Contains("model_reasoning_effort=\"max\"", StringComparison.Ordinal), "maximum requested effort must be forwarded literally");
    Assert(arguments.Contains("--sandbox\nread-only", StringComparison.Ordinal), "read-only sandbox must be requested");
    Assert(arguments.Contains("--disable\nshell_tool", StringComparison.Ordinal), "shell tool must be explicitly disabled");
    Assert(!arguments.Contains("whoami", StringComparison.Ordinal), "prompt data must never be copied into process arguments");
    Assert(!File.Exists(Path.Combine(valid.AttemptDirectory!, "pwned.txt")), "prompt metacharacters must not execute locally");
    Assert((await File.ReadAllTextAsync(Path.Combine(valid.AttemptDirectory!, "inherited-openai-key.txt"))).Length == 0,
        "personal API key must not be inherited by the runtime process");
    Assert((await File.ReadAllTextAsync(Path.Combine(valid.AttemptDirectory!, "inherited-codex-api-key.txt"))).Length == 0,
        "personal Codex API key must not be inherited by the runtime process");
    Assert((await File.ReadAllTextAsync(Path.Combine(valid.AttemptDirectory!, "runtime-codex-home.txt"))).Equals(home, StringComparison.OrdinalIgnoreCase),
        "personal CODEX_HOME must be replaced by the dedicated runtime home");

    var exitWithStderr = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", exitStderrSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(exitWithStderr.Outcome == ProviderOutcome.ProcessFailure && exitWithStderr.ErrorCode == "codex_exit_nonzero" &&
        exitWithStderr.ProcessStarted && exitWithStderr.ExitCode == 23,
        "a non-zero provider must retain its started and exit-code signals");
    Assert(exitWithStderr.DiagnosticStderr?.Contains("fake-stderr-sentinel", StringComparison.Ordinal) == true &&
        exitWithStderr.DiagnosticStderr.Contains("[stderr truncated]", StringComparison.Ordinal) &&
        exitWithStderr.DiagnosticStderr.Length <= 16_384 + 32,
        "provider stderr must be bounded before private evidence persistence");
    Assert(exitWithStderr.AttemptDirectory is not null &&
        JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(exitWithStderr.AttemptDirectory, "attempt.json"))).RootElement.GetProperty("exit_code").GetInt32() == 23 &&
        JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(exitWithStderr.AttemptDirectory, "attempt.json"))).RootElement.GetProperty("stderr").GetString()?.Contains("fake-stderr-sentinel", StringComparison.Ordinal) == true,
        "bounded stderr and exit code must be persisted only in private attempt evidence");

    var exitWithJsonl = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", exitJsonlSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(exitWithJsonl.Outcome == ProviderOutcome.ModelUnavailable && exitWithJsonl.ErrorCode == "codex_exit_nonzero" &&
        exitWithJsonl.ProcessStarted && exitWithJsonl.ExitCode == 29 && exitWithJsonl.DiagnosticCategory == "model",
        "a non-zero provider must classify a model error emitted only on JSONL stdout");
    Assert(exitWithJsonl.DiagnosticStdoutSha256 is not null && exitWithJsonl.DiagnosticStdoutLength is > 0 &&
        exitWithJsonl.DiagnosticStdoutTruncated && exitWithJsonl.DiagnosticEventErrorSha256 is not null &&
        exitWithJsonl.DiagnosticEventErrorLength is > 0 && exitWithJsonl.DiagnosticEventErrorTruncated,
        "JSONL stdout and event error summaries must be bounded and returned without raw payloads");
    Assert(exitWithJsonl.AttemptDirectory is not null &&
        File.Exists(Path.Combine(exitWithJsonl.AttemptDirectory, "stdout.jsonl")),
        "bounded failing JSONL must be written to the private stdout.jsonl attempt file");
    var jsonlAttempt = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(exitWithJsonl.AttemptDirectory!, "attempt.json"))).RootElement;
    var privateJsonlBytes = await File.ReadAllBytesAsync(Path.Combine(exitWithJsonl.AttemptDirectory!, "stdout.jsonl"));
    var privateJsonl = Encoding.UTF8.GetString(privateJsonlBytes);
    Assert(privateJsonl.Contains("fake-jsonl-error-sentinel", StringComparison.Ordinal) &&
        privateJsonl.Contains("[diagnostic truncated]", StringComparison.Ordinal) &&
        privateJsonl.Length <= 16_384 + 32 &&
        Convert.ToHexStringLower(SHA256.HashData(privateJsonlBytes)) == exitWithJsonl.DiagnosticStdoutSha256 &&
        jsonlAttempt.GetProperty("stdout_jsonl_sha256").GetString() == exitWithJsonl.DiagnosticStdoutSha256 &&
        jsonlAttempt.GetProperty("event_error").GetString()?.Contains("fake-jsonl-error-sentinel", StringComparison.Ordinal) == true &&
        jsonlAttempt.GetProperty("event_error").GetString()?.Length <= 16_384 + 32,
        "raw JSONL/event error must remain bounded inside the private attempt only");

    var creditsExhausted = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", creditsExhaustedSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(creditsExhausted.Outcome == ProviderOutcome.Quota && creditsExhausted.DiagnosticCategory == "quota" &&
        creditsExhausted.ProcessStarted && creditsExhausted.ExitCode == 30,
        "an exhausted existing credit balance must be reported as quota without a fallback");

    var divergentModel = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", divergentModelSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(divergentModel.Outcome == ProviderOutcome.ModelUnavailable && divergentModel.ErrorCode == "reported_model_mismatch",
        "a reported model different from the requested model must never be accepted");

    var divergentEffort = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", divergentEffortSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(divergentEffort.Outcome == ProviderOutcome.EffortUnsupported && divergentEffort.ErrorCode == "reported_effort_mismatch",
        "a reported reasoning effort different from the requested effort must never be accepted");

    var missingModel = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", missingModelSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(missingModel.Outcome == ProviderOutcome.IsolationViolation && missingModel.ErrorCode == "provider_attestation_invalid",
        "a provider attestation without a model must fail closed");

    var missingEffort = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", missingEffortSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(missingEffort.Outcome == ProviderOutcome.IsolationViolation && missingEffort.ErrorCode == "provider_attestation_invalid",
        "a provider attestation without reasoning effort must fail closed");

    var invalidAttestation = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", invalidAttestationSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(invalidAttestation.Outcome == ProviderOutcome.IsolationViolation && invalidAttestation.ErrorCode == "provider_attestation_invalid",
        "a provider attestation with the wrong source or response count must fail closed");

    var missingAttestation = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", missingAttestationSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(missingAttestation.Outcome == ProviderOutcome.IsolationViolation && missingAttestation.ErrorCode == "provider_attestation_missing",
        "a successful turn without the server attestation must fail closed");

    var duplicateAttestation = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", duplicateAttestationSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(duplicateAttestation.Outcome == ProviderOutcome.IsolationViolation && duplicateAttestation.ErrorCode == "provider_attestation_duplicate",
        "duplicate server attestations must fail closed");

    var outOfOrderAttestation = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", outOfOrderAttestationSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(outOfOrderAttestation.Outcome == ProviderOutcome.IsolationViolation && outOfOrderAttestation.ErrorCode == "provider_attestation_order",
        "a server attestation not adjacent to turn.completed must fail closed");

    var arbitraryMetadata = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", arbitraryMetadataSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(arbitraryMetadata.Outcome == ProviderOutcome.Success &&
        arbitraryMetadata.ReportedModel == "gpt-5.6-luna" && arbitraryMetadata.ReportedEffort == "max",
        "model and effort fields on ordinary JSONL events must not be trusted");

    var invalidJson = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", invalidJsonSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(invalidJson.Outcome == ProviderOutcome.InvalidSchema && invalidJson.ErrorCode == "final_not_json" &&
        invalidJson.FinalJson == "{not-json",
        "a malformed final must be retained as bounded repair input without being accepted");

    var outsideSchema = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", outside, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(outsideSchema.Outcome == ProviderOutcome.IsolationViolation && outsideSchema.ErrorCode == "invalid_trusted_input",
        "schema outside the specification root must be rejected before launch");

    var outsideImage = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", schema, [outside, drawing], "job-security"), CancellationToken.None);
    Assert(outsideImage.Outcome == ProviderOutcome.IsolationViolation && outsideImage.ErrorCode == "untrusted_image_path",
        "image outside the declared input root must be rejected before launch");

    var wrongStageImages = await runner.TransportProbeAsync(new(
        Guid.NewGuid().ToString("N"), "B", "safe", schema, [reference], "job-security"), CancellationToken.None);
    Assert(wrongStageImages.Outcome == ProviderOutcome.IsolationViolation && wrongStageImages.ErrorCode == "stage_image_count",
        "B must not accept image inputs");

    Console.WriteLine("Provider security tests passed: process arguments, environment clearing, path roots, prompt injection and stage input bounds.");
}
finally
{
    Environment.SetEnvironmentVariable("PALIMPSESTE_ALLOW_MODEL_FALLBACK", previousFallback);
    Environment.SetEnvironmentVariable("PALIMPSESTE_ALLOW_REASONING_DOWNGRADE", previousDowngrade);
    Environment.SetEnvironmentVariable("PALIMPSESTE_RUNTIME_EXECUTION_TOOLS", previousTools);
    Environment.SetEnvironmentVariable("OPENAI_API_KEY", previousApiKey);
    Environment.SetEnvironmentVariable("CODEX_API_KEY", previousCodexApiKey);
    Environment.SetEnvironmentVariable("CODEX_HOME", previousCodexHome);
    if (Environment.GetEnvironmentVariable("PALIMPSESTE_KEEP_SECURITY_FIXTURE") != "1")
    {
        // This test owns only a GUID child under this explicit fixture root.
        // Keep the guard next to the recursive delete so a future path change
        // cannot turn cleanup into an arbitrary filesystem operation.
        var fixtureRoot = Path.GetFullPath("E:\\PalimpsesteProviderSecurity").TrimEnd('\\', '/');
        var fixturePath = Path.GetFullPath(root).TrimEnd('\\', '/');
        var childPrefix = fixtureRoot + Path.DirectorySeparatorChar;
        if (fixturePath.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fixturePath, fixtureRoot, StringComparison.OrdinalIgnoreCase))
        {
            try { Directory.Delete(fixturePath, recursive: true); } catch { }
        }
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task RunFakeProviderAsync(string[] arguments)
{
    // This child proves only transport/process isolation. It is never a Luna
    // call and must not be presented as model compatibility evidence.
    var argsFile = Path.Combine(Environment.CurrentDirectory, "fake-arguments.txt");
    await File.WriteAllTextAsync(argsFile, string.Join('\n', arguments), new UTF8Encoding(false));
    var keyFile = Path.Combine(Environment.CurrentDirectory, "inherited-openai-key.txt");
    await File.WriteAllTextAsync(keyFile, Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "", new UTF8Encoding(false));
    var codexKeyFile = Path.Combine(Environment.CurrentDirectory, "inherited-codex-api-key.txt");
    await File.WriteAllTextAsync(codexKeyFile, Environment.GetEnvironmentVariable("CODEX_API_KEY") ?? "", new UTF8Encoding(false));
    var homeFile = Path.Combine(Environment.CurrentDirectory, "runtime-codex-home.txt");
    await File.WriteAllTextAsync(homeFile, Environment.GetEnvironmentVariable("CODEX_HOME") ?? "", new UTF8Encoding(false));
    string? output = null;
    string? schema = null;
    for (var i = 0; i < arguments.Length; i++)
    {
        if (arguments[i] == "--output-last-message" && i + 1 < arguments.Length) output = arguments[i + 1];
        if (arguments[i] == "--output-schema" && i + 1 < arguments.Length) schema = arguments[i + 1];
    }
    if (output is null) Environment.ExitCode = 11;
    else
    {
        var schemaText = schema is not null && File.Exists(schema) ? await File.ReadAllTextAsync(schema) : "";
        if (schemaText.Contains("exit-stderr", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.Write("fake-stderr-sentinel\n" + new string('x', 20_000));
            Environment.ExitCode = 23;
            return;
        }
        if (schemaText.Contains("exit-jsonl", StringComparison.OrdinalIgnoreCase))
        {
            var jsonlMessage = "fake-jsonl-error-sentinel model unsupported " + new string('z', 20_000);
            Console.WriteLine($"{{\"type\":\"error\",\"error\":{{\"code\":\"model_not_supported\",\"message\":\"{jsonlMessage}\"}}}}");
            Environment.ExitCode = 29;
            return;
        }
        if (schemaText.Contains("credits-exhausted", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("{\"type\":\"error\",\"error\":{\"code\":\"insufficient_credits\",\"message\":\"Existing credits exhausted\"}}");
            Environment.ExitCode = 30;
            return;
        }
        var version = schema?.Contains("model-b", StringComparison.OrdinalIgnoreCase) == true ? "sp.plan/1.0" : "sp.description/1.0";
        await File.WriteAllTextAsync(output, schemaText.Contains("invalid-json", StringComparison.OrdinalIgnoreCase)
            ? "{not-json"
            : $"{{\"schema_version\":\"{version}\"}}", new UTF8Encoding(false));
        var model = schemaText.Contains("divergent-model", StringComparison.OrdinalIgnoreCase) ? "other-model" : "gpt-5.6-luna";
        var effort = schemaText.Contains("divergent-effort", StringComparison.OrdinalIgnoreCase) ? "high" : "max";
        var missingModel = schemaText.Contains("missing-model", StringComparison.OrdinalIgnoreCase);
        var missingEffort = schemaText.Contains("missing-effort", StringComparison.OrdinalIgnoreCase);
        var missingAttestation = schemaText.Contains("missing-attestation", StringComparison.OrdinalIgnoreCase);
        var duplicateAttestation = schemaText.Contains("duplicate-attestation", StringComparison.OrdinalIgnoreCase);
        var outOfOrderAttestation = schemaText.Contains("out-of-order-attestation", StringComparison.OrdinalIgnoreCase);
        var arbitraryMetadata = schemaText.Contains("arbitrary-metadata", StringComparison.OrdinalIgnoreCase);
        var invalidAttestation = schemaText.Contains("invalid-attestation", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine(arbitraryMetadata
            ? "{\"type\":\"thread.started\",\"thread_id\":\"fake-thread\",\"model\":\"spoofed-model\",\"reasoning_effort\":\"low\"}"
            : "{\"type\":\"thread.started\",\"thread_id\":\"fake-thread\"}");
        var attestation = missingModel
            ? "{\"type\":\"provider.attested\",\"source\":\"server_response\",\"reasoning_effort\":\"max\",\"response_count\":1}"
            : missingEffort
                ? "{\"type\":\"provider.attested\",\"source\":\"server_response\",\"model\":\"gpt-5.6-luna\",\"response_count\":1}"
                : invalidAttestation
                    ? "{\"type\":\"provider.attested\",\"source\":\"client\",\"model\":\"gpt-5.6-luna\",\"reasoning_effort\":\"max\",\"response_count\":0}"
                : $"{{\"type\":\"provider.attested\",\"source\":\"server_response\",\"model\":\"{model}\",\"reasoning_effort\":\"{effort}\",\"response_count\":1}}";
        var completion = arbitraryMetadata
            ? "{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":1,\"output_tokens\":1},\"model\":\"spoofed-model\",\"reasoning_effort\":\"low\"}"
            : "{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}";
        if (outOfOrderAttestation)
        {
            Console.WriteLine(completion);
            Console.WriteLine(attestation);
        }
        else if (missingAttestation)
        {
            Console.WriteLine(completion);
        }
        else
        {
            Console.WriteLine(attestation);
            if (duplicateAttestation) Console.WriteLine(attestation);
            Console.WriteLine(completion);
        }
    }
}

static class SecurityFixtures
{
public static readonly byte[] Png1024 =
[
    137, 80, 78, 71, 13, 10, 26, 10,
    0, 0, 0, 13, 73, 72, 68, 82,
    0, 0, 4, 0, 0, 0, 4, 0
];
}
