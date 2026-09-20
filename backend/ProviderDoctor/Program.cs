using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Palimpseste.Contracts;
using Palimpseste.Core;
using Palimpseste.Provider;

var settings = CodexSettings.FromEnvironment();
if (args.Length == 0 || args[0] is not ("local" or "interpreter" or "astra" or "active" or "plan" or "validate"))
{
    Console.Error.WriteLine("Usage: ProviderDoctor local | interpreter <spec-root> <reference.png> <drawing.png> | active <spec-root> <reference.png> <drawing.png> --ink <ink.png> | plan <spec-root> <frozen-a.json> --a-sha256 <sha256> --ink <ink.png> | validate <spec-root> <a-final.json> <b-final.json> --a-sha256 <sha256> --b-sha256 <sha256> --ink <ink.png>; all modes accept --write <evidence.json>.");
    return 2;
}

var mode = args[0];
var writePath = ReadOption(args, "--write");
string? executableHash = null;
try { executableHash = CodexSettings.ComputeExecutableSha256(settings.Executable); }
catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
var version = await RunCommandAsync(settings, "--version");
var help = await RunCommandAsync(settings, "exec", "--help");
var featureSyntax = await RunCommandAsync(settings, DoctorConstants.DisabledHelpArguments());
// `features list` is a local CLI inspection and does not contact a model. It
// is intentionally recorded as an observation only: a successful parser test
// does not prove that every flag changes the capability used by `exec`.
var featureList = await RunCommandAsync(settings, DoctorConstants.DisabledFeatureListArguments());
var auth = await RunCommandAsync(settings, "login", "status");
var optionIssues = DoctorConstants.RequiredOptions.Where(option => !help.Output.Contains(option, StringComparison.Ordinal)).ToArray();
var featureObservations = ParseFeatureList(featureList.Output);
var featureListAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var requestedFeatureObservations = DoctorConstants.DisabledFeatures
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToDictionary(
        feature => feature,
        feature => ObservedFeature(feature, featureObservations, featureListAliases),
        StringComparer.OrdinalIgnoreCase);
var gateFeatureNames = CodexSettings.RuntimeFeatureGateNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
var gateFeatureObservations = requestedFeatureObservations
    .Where(entry => gateFeatureNames.Contains(entry.Key))
    .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
var activeRequestedFeatures = gateFeatureObservations
    .Where(entry => entry.Value == true)
    .Select(entry => entry.Key)
    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
    .ToArray();
var activeInternalFeatures = requestedFeatureObservations
    .Where(entry => !gateFeatureNames.Contains(entry.Key) && entry.Value == true)
    .Select(entry => entry.Key)
    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
    .ToArray();
var featureObservationComplete = featureList.Status == "ok" &&
    gateFeatureObservations.Values.All(value => value.HasValue);
var effectiveDisableObserved = featureObservationComplete && activeRequestedFeatures.Length == 0;
var local = new Dictionary<string, object?>
{
    ["kind"] = "provider_doctor",
    ["mode"] = mode,
    ["checked_at"] = DateTimeOffset.UtcNow,
    ["executable"] = settings.Executable,
    ["cli_executable_sha256"] = executableHash,
    ["cli_executable_hash_status"] = executableHash is null ? "unavailable" : "ok",
    ["cli_version"] = version.Output.Trim(),
    ["cli_version_status"] = version.Status,
    ["exec_help_status"] = help.Status,
    ["disable_flag_parser_status"] = featureSyntax.Status,
    ["feature_list_status"] = featureList.Status,
    ["feature_list_detail"] = featureList.Detail,
    ["feature_list_aliases"] = featureListAliases,
    ["feature_list_requested"] = DoctorConstants.DisabledFeatures,
    ["feature_list_observations"] = requestedFeatureObservations,
    ["feature_list_gate_observations"] = gateFeatureObservations,
    ["feature_list_active_requested_features"] = activeRequestedFeatures,
    ["feature_list_active_internal_features"] = activeInternalFeatures,
    ["feature_list_observation_complete"] = featureObservationComplete,
    ["feature_list_effective_disable_observed"] = effectiveDisableObserved,
    ["effective_disable_observed"] = effectiveDisableObserved,
    ["required_options"] = DoctorConstants.RequiredOptions,
    ["missing_options"] = optionIssues,
    ["dedicated_home"] = settings.CodexHome,
    ["attempt_root"] = settings.AttemptRoot,
    ["spec_root"] = settings.TrustedSpecificationRoot,
    ["input_root"] = settings.TrustedInputRoot,
    ["development_root"] = settings.DevelopmentRoot,
    ["service_identity"] = Environment.UserName,
    ["expected_service_identity"] = settings.ExpectedServiceUser,
    ["requested_model"] = settings.Model,
    ["requested_effort"] = settings.Effort,
    ["interpreter_requested_model"] = settings.InterpreterModel,
    ["interpreter_requested_effort"] = settings.InterpreterEffort,
    ["effort_compatibility_verified"] = settings.EffortCompatibilityVerified,
    ["runtime_features_compatibility_verified"] = settings.RuntimeFeaturesCompatibilityVerified,
    ["runtime_feature_evidence_path"] = settings.RuntimeFeaturesEvidencePath,
    ["runtime_feature_evidence_sha256_configured"] = !string.IsNullOrWhiteSpace(settings.RuntimeFeaturesEvidenceSha256),
    ["effort_evidence_path"] = settings.CompatibilityEvidencePath,
    ["effort_evidence_sha256_configured"] = !string.IsNullOrWhiteSpace(settings.CompatibilityEvidenceSha256),
    ["reported_model"] = null,
    ["reported_effort"] = null,
    ["dedicated_auth"] = auth.Status == "authenticated" ? "authenticated" : auth.Status,
    ["dedicated_auth_detail"] = auth.Status == "authenticated" ? "redacted" : auth.Detail,
    ["runtime_features_disabled"] = DoctorConstants.DisabledFeatures,
    ["active_provider_execution_mode"] = "probe_async",
    ["runtime_features_effectiveness"] = effectiveDisableObserved
        ? (activeInternalFeatures.Length == 0
            ? "observed_disabled_by_local_features_list_only"
            : "exposed_tools_observed_disabled_internal_pty_feature_still_enabled")
        : "not_proven_without_an_active_capability_observation",
    ["runtime_execution_tools"] = false,
    ["production_issues"] = settings.Check(true),
    ["interpreter_production_issues"] = settings.Check(true, stage: "A"),
    ["active_test_issues"] = settings.Check(false),
    ["model_calls_executed"] = false,
    ["compatibility_evidence"] = new
    {
        version_command = version.Status,
        exec_help_command = help.Status,
        disable_flag_parser_command = featureSyntax.Status,
        feature_list_command = featureList.Status,
        required_flags_present = optionIssues.Length == 0,
        effort_acceptance = settings.EffortCompatibilityVerified ? "operator_verified" : "not_verified_without_active_test",
        runtime_features = settings.RuntimeFeaturesCompatibilityVerified ? "operator_verified" : "not_verified_effectiveness_unknown",
        runtime_feature_observation = effectiveDisableObserved ? "all_requested_flags_reported_false" : "one_or_more_requested_flags_reported_true_or_unobserved"
    }
};

var localExit = executableHash is not null && settings.Check(false).Count == 0 && version.Status == "ok" && help.Status == "ok" &&
    featureSyntax.Status == "ok" && featureList.Status == "ok" && optionIssues.Length == 0 &&
    featureObservationComplete && effectiveDisableObserved ? 0 : 1;
if (mode == "local")
{
    await EmitAsync(local, writePath);
    return localExit;
}

// One explicit multimodal A call consumes normal Codex account usage under the dedicated
// service identity. It never promotes itself: the operator must review and
// hash the report before setting the production evidence path.
if (mode is "interpreter" or "astra")
{
    local["kind"] = "interpreter_multimodal_probe";
    local["requested_model"] = settings.InterpreterModel;
    local["requested_effort"] = settings.InterpreterEffort;
    local["production_issues"] = settings.Check(true, stage: "A");
    local["active_test_issues"] = settings.Check(false, stage: "A");
    local["result"] = "preflight_rejected";
    local["output_schema_valid"] = false;
    if (args.Length < 4 || (args.Length > 4 && !args[4].StartsWith("--", StringComparison.Ordinal)) ||
        localExit != 0 || auth.Status != "authenticated" || executableHash is null)
    {
        await EmitAsync(local, writePath);
        return 2;
    }
    var specRoot = Path.GetFullPath(args[1]);
    var referencePath = Path.GetFullPath(args[2]);
    var drawingPath = Path.GetFullPath(args[3]);
    if (!string.Equals(specRoot.TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(settings.TrustedSpecificationRoot).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase) ||
        !settings.IsTrustedInputFile(referencePath) || !settings.IsTrustedInputFile(drawingPath))
    {
        local["result"] = "input_path_rejected";
        await EmitAsync(local, writePath);
        return 2;
    }
    try
    {
        var referenceHash = Hash(await File.ReadAllBytesAsync(referencePath));
        var drawingHash = Hash(await File.ReadAllBytesAsync(drawingPath));
        local["reference_sha256"] = referenceHash;
        local["drawing_sha256"] = drawingHash;
        if (string.Equals(referenceHash, drawingHash, StringComparison.Ordinal))
        {
            local["result"] = "images_identical";
            await EmitAsync(local, writePath);
            return 2;
        }
        var layoutPath = Path.Combine(specRoot, "reference", "layout-v2.json");
        if (!File.Exists(layoutPath)) layoutPath = Path.Combine(specRoot, "reference", "layout-v1.json");
        var layout = await File.ReadAllTextAsync(layoutPath);
        var astraCapabilities = await File.ReadAllTextAsync(Path.Combine(specRoot, "contracts", "capability-catalog.json"));
        var astraProvider = new LunaCodexProvider(new CodexProcessRunner(settings), specRoot);
        var a = await astraProvider.ProbeInterpretAsync("operator-interpreter-probe", Guid.NewGuid().ToString("N"),
            referencePath, drawingPath, layout, astraCapabilities, CancellationToken.None);
        local["model_calls_executed"] = a.Transport.ProcessStarted;
        local["process_started"] = a.Transport.ProcessStarted;
        local["exit_code"] = a.Transport.ExitCode;
        local["reported_model"] = a.Transport.ReportedModel;
        local["reported_effort"] = a.Transport.ReportedEffort;
        local["stage_a"] = StageEvidence(a);
        var finalFile = a.Transport.AttemptDirectory is null ? null :
            Path.Combine(a.Transport.AttemptDirectory, "final.json");
        local["final_file"] = finalFile;
        if (a.Utf8 is null || finalFile is null || !File.Exists(finalFile) ||
            !MatchesRequestedMetadata(a.Transport, settings, "A"))
        {
            local["result"] = "interpreter_transport_or_attestation_failed";
            await EmitAsync(local, writePath);
            return 1;
        }
        var issues = SpellCompiler.ValidateWholeImageDescriptionJson(a.Utf8, requirePalette: true, requireVisualForm: true);
        local["description_issue_codes"] = issues.Select(issue => issue.Code).Distinct().ToArray();
        if (issues.Count != 0)
        {
            local["result"] = "description_rejected";
            await EmitAsync(local, writePath);
            return 1;
        }
        var astraDescription = ContractJson.DeserializeStrict<SpellDescription>(a.Utf8, "spell-description");
        if (astraDescription.shape_requests.Count != 0 ||
            astraDescription.observations.Any(observation => observation.region != "full"))
        {
            local["result"] = "global_image_policy_rejected";
            await EmitAsync(local, writePath);
            return 1;
        }
        local["final_sha256"] = Hash(await File.ReadAllBytesAsync(finalFile));
        local["output_schema_valid"] = true;
        local["result"] = "success";
        await EmitAsync(local, writePath);
        return 0;
    }
    catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
    {
        local["result"] = "interpreter_input_or_output_unreadable";
        local["error_type"] = error.GetType().Name;
        await EmitAsync(local, writePath);
        return 1;
    }
}

if (mode == "validate")
{
    local["kind"] = "provider_offline_validation";
    local["model_calls_executed"] = false;
    local["validation_status"] = "rejected";
    local["compilation_status"] = "not_run";
    local["doctor_executable_sha256"] = Environment.ProcessPath is { Length: > 0 } doctorExecutable
        ? CodexSettings.ComputeExecutableSha256(doctorExecutable) : null;
    var ink = ReadOption(args, "--ink");
    var expectedA = ReadStringOption(args, "--a-sha256");
    var expectedB = ReadStringOption(args, "--b-sha256");
    if (args.Length < 4 || ink is null || !IsSha256(expectedA) || !IsSha256(expectedB) ||
        localExit != 0 || auth.Status != "authenticated")
    {
        local["result"] = "preflight_rejected";
        await EmitAsync(local, writePath);
        return 2;
    }
    var specRoot = Path.GetFullPath(args[1]);
    var aPath = Path.GetFullPath(args[2]);
    var bPath = Path.GetFullPath(args[3]);
    if (!string.Equals(specRoot.TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(settings.TrustedSpecificationRoot).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase) ||
        !CodexSettings.IsTrustedFile(aPath, settings.AttemptRoot) ||
        !CodexSettings.IsTrustedFile(bPath, settings.AttemptRoot) ||
        !settings.IsTrustedInputFile(ink))
    {
        local["result"] = "input_path_rejected";
        await EmitAsync(local, writePath);
        return 2;
    }
    try
    {
        var aInfo = new FileInfo(aPath);
        var bInfo = new FileInfo(bPath);
        var inkInfo = new FileInfo(ink);
        if (aInfo.Length is <= 0 or > ContractJson.MaxDocumentBytes ||
            bInfo.Length is <= 0 or > ContractJson.MaxDocumentBytes ||
            inkInfo.Length is <= 0 or > ContractJson.MaxDocumentBytes)
            throw new InvalidDataException("input_size_invalid");
        var aBytes = await File.ReadAllBytesAsync(aPath);
        var bBytes = await File.ReadAllBytesAsync(bPath);
        var inkBytes = await File.ReadAllBytesAsync(ink);
        var aSha = Hash(aBytes);
        var bSha = Hash(bBytes);
        var inkSha = Hash(inkBytes);
        local["description_file"] = aPath;
        local["plan_file"] = bPath;
        local["ink_file"] = ink;
        local["description_sha256"] = aSha;
        local["plan_sha256"] = bSha;
        local["ink_sha256"] = inkSha;
        if (!string.Equals(aSha, expectedA, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(bSha, expectedB, StringComparison.OrdinalIgnoreCase))
        {
            local["result"] = "hash_mismatch";
            await EmitAsync(local, writePath);
            return 1;
        }
        var offlineDescriptionIssues = SpellCompiler.ValidateDescriptionJson(aBytes);
        if (offlineDescriptionIssues.Count != 0)
        {
            local["result"] = "description_rejected";
            local["issue_codes"] = offlineDescriptionIssues.Select(issue => issue.Code).Distinct().ToArray();
            await EmitAsync(local, writePath);
            return 1;
        }
        var decoded = ContractJson.DeserializeStrict<SpellDescription>(aBytes, "spell-description");
        var resolved = GeometryResolver.Resolve(inkBytes, decoded);
        if (!resolved.Success)
        {
            local["result"] = "geometry_rejected";
            local["issue_codes"] = resolved.Issues.Select(issue => issue.Code).Distinct().ToArray();
            await EmitAsync(local, writePath);
            return 1;
        }
        var context = "[" + string.Join(",", resolved.GeometryJson.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => Encoding.UTF8.GetString(item.Value))) + "]";
        local["geometry_source"] = "resolver_from_ink_and_description";
        local["geometry_resolver_version"] = GeometryResolver.Version;
        local["geometry_context_sha256"] = Hash(Encoding.UTF8.GetBytes(context));
        var offlinePlanIssues = SpellCompiler.ValidatePlanJson(aBytes, bBytes, resolved.GeometryJson, resolved.MaskPng);
        if (offlinePlanIssues.Count != 0)
        {
            local["result"] = "plan_rejected";
            local["issue_codes"] = offlinePlanIssues.Select(issue => issue.Code).Distinct().ToArray();
            await EmitAsync(local, writePath);
            return 1;
        }
        local["validation_status"] = "success";
        var compilation = CompileDoctorProbe(aBytes, bBytes, resolved.GeometryJson, resolved.MaskPng);
        local["compiler_version"] = SpellCompiler.Version;
        if (!compilation.Success)
        {
            local["result"] = "compilation_rejected";
            local["issue_codes"] = compilation.Issues.Select(issue => issue.Code).Distinct().ToArray();
            await EmitAsync(local, writePath);
            return 1;
        }
        local["compilation_status"] = "success";
        local["compiled_probe_sha256"] = compilation.PayloadSha256;
        local["result"] = "success";
        await EmitAsync(local, writePath);
        return 0;
    }
    catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
    {
        local["result"] = "input_unreadable";
        local["error_type"] = error.GetType().Name;
        await EmitAsync(local, writePath);
        return 1;
    }
}

var requiredPositionals = mode == "active" ? 4 : 3;
var inkArgument = ReadOption(args, "--ink");
var aHashArgument = ReadStringOption(args, "--a-sha256");
if (args.Length < requiredPositionals || inkArgument is null ||
    (mode == "plan" && (aHashArgument is null || !IsSha256(aHashArgument))) ||
    (mode == "active" && args.Length > 4 && !args[4].StartsWith("--", StringComparison.Ordinal)) ||
    (mode == "plan" && args.Length > 3 && !args[3].StartsWith("--", StringComparison.Ordinal)))
{
    Console.Error.WriteLine("Active doctor requires reference, drawing and --ink. Plan doctor requires frozen A, --a-sha256 and --ink.");
    await EmitAsync(local, writePath);
    return 2;
}

if (localExit != 0 || auth.Status != "authenticated")
{
    local["active_blocked"] = true;
    local["active_block_reason"] = localExit != 0 ? "local_compatibility_failed" : "dedicated_auth_not_confirmed";
    await EmitAsync(local, writePath);
    return 2;
}

// This branch is deliberately explicit and consumes normal provider usage.
// ProbeAsync is the only path allowed to establish compatibility. It skips
// only the effort evidence gate while retaining all production path, identity,
// input, output and tool restrictions. The worker still requires
// PALIMPSESTE_EFFORT_VERIFIED=true afterwards.
var root = Path.GetFullPath(args[1]);
var inkPath = inkArgument;
var reference = mode == "active" ? Path.GetFullPath(args[2]) : null;
var drawing = mode == "active" ? Path.GetFullPath(args[3]) : null;
var frozenAPath = mode == "plan" ? Path.GetFullPath(args[2]) : null;
if (!string.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(settings.TrustedSpecificationRoot).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
    !settings.IsTrustedInputFile(inkPath) ||
    (mode == "active" && (!settings.IsTrustedInputFile(reference!) || !settings.IsTrustedInputFile(drawing!))) ||
    (mode == "plan" && !settings.IsTrustedInputFile(frozenAPath!) &&
     !CodexSettings.IsTrustedFile(frozenAPath!, settings.AttemptRoot)))
{
    local["active_blocked"] = true;
    local["active_block_reason"] = "active_inputs_outside_declared_roots";
    await EmitAsync(local, writePath);
    return 2;
}

var provider = new LunaCodexProvider(new CodexProcessRunner(settings), root);
var capabilities = await File.ReadAllTextAsync(Path.Combine(root, "contracts", "capability-catalog.json"));
byte[] description;
var stageAProcessStarted = false;
if (mode == "active")
{
    const string layout = "{\"canvas_width\":1024,\"canvas_height\":1024,\"reference_purpose\":\"identify_printed_guides_only\",\"semantic_regions\":false}";
    var a = await provider.ProbeInterpretAsync("operator-doctor", Guid.NewGuid().ToString("N"), reference!, drawing!, layout, capabilities, CancellationToken.None);
    local["model_calls_executed"] = a.Transport.ProcessStarted;
    stageAProcessStarted = a.Transport.ProcessStarted;
    local["stage_a_model_call_executed"] = stageAProcessStarted;
    local["stage_a"] = StageEvidence(a);
    if (a.Utf8 is null)
    {
        local["active_result"] = "stage_a_failed";
        await EmitAsync(local, writePath);
        return 1;
    }
    if (!MatchesRequestedMetadata(a.Transport, settings, "A"))
    {
        local["active_result"] = "stage_a_metadata_unverified";
        local["active_blocked"] = true;
        local["active_block_reason"] = "model_or_effort_not_reported_exactly";
        await EmitAsync(local, writePath);
        return 1;
    }
    description = a.Utf8;
}
else
{
    var aFile = new FileInfo(frozenAPath!);
    if (aFile.Length is <= 0 or > ContractJson.MaxDocumentBytes)
    {
        local["active_result"] = "frozen_a_size_invalid";
        await EmitAsync(local, writePath);
        return 1;
    }
    description = await File.ReadAllBytesAsync(frozenAPath!);
    var actualHash = Hash(description);
    if (!string.Equals(actualHash, aHashArgument, StringComparison.OrdinalIgnoreCase))
    {
        local["active_result"] = "frozen_a_hash_mismatch";
        await EmitAsync(local, writePath);
        return 1;
    }
    local["stage_a_reuse"] = new { source_file = frozenAPath, sha256 = actualHash, hash_verified = true,
        model_call_executed = false };
}

var descriptionIssues = mode == "active"
    ? SpellCompiler.ValidateWholeImageDescriptionJson(description, requirePalette: true, requireVisualForm: true)
    : SpellCompiler.ValidateDescriptionJson(description);
if (descriptionIssues.Count != 0)
{
    local["active_result"] = "description_invalid";
    local["description_issue_codes"] = descriptionIssues.Select(issue => issue.Code).Distinct().ToArray();
    await EmitAsync(local, writePath);
    return 1;
}
string geometry;
Dictionary<string, byte[]> geometryJson;
Dictionary<string, byte[]> maskPng;
try
{
    var inkFile = new FileInfo(inkPath);
    if (inkFile.Length is <= 0 or > ContractJson.MaxDocumentBytes)
        throw new InvalidDataException("ink_size_invalid");
    var ink = await File.ReadAllBytesAsync(inkPath);
    var decoded = ContractJson.DeserializeStrict<SpellDescription>(description, "spell-description");
    var resolved = GeometryResolver.Resolve(ink, decoded);
    if (!resolved.Success)
    {
        local["active_result"] = "geometry_unavailable";
        local["geometry_issue_codes"] = resolved.Issues.Select(issue => issue.Code).Distinct().ToArray();
        await EmitAsync(local, writePath);
        return 1;
    }
    geometryJson = resolved.GeometryJson;
    maskPng = resolved.MaskPng;
    geometry = "[" + string.Join(",", resolved.GeometryJson.OrderBy(item => item.Key, StringComparer.Ordinal)
        .Select(item => Encoding.UTF8.GetString(item.Value))) + "]";
    local["geometry"] = new { source = GeometryResolver.UsesSemanticForms(decoded)
            ? "controlled_geometry_from_interpretation" : "resolver_from_ink_and_description",
        resolver_versions = resolved.Assets.Values.Select(asset => asset.algorithm).Distinct().OrderBy(value => value).ToArray(),
        description_sha256 = Hash(description), ink_sha256 = Hash(ink), context_sha256 = Hash(Encoding.UTF8.GetBytes(geometry)),
        geometry_ids = resolved.GeometryJson.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
        mask_ids = resolved.MaskPng.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray() };
}
catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
{
    local["active_result"] = "geometry_input_invalid";
    local["geometry_error_type"] = error.GetType().Name;
    await EmitAsync(local, writePath);
    return 1;
}

var b = await provider.ProbePlanAsync("operator-doctor", Guid.NewGuid().ToString("N"), description, geometry, capabilities, CancellationToken.None);
local["model_calls_executed"] = stageAProcessStarted || b.Transport.ProcessStarted;
local["stage_b_model_call_executed"] = b.Transport.ProcessStarted;
local["stage_b"] = StageEvidence(b);
if (b.Utf8 is null)
    local["active_result"] = "stage_b_failed";
else if (!MatchesRequestedMetadata(b.Transport, settings, "B"))
{
    local["active_result"] = "stage_b_metadata_unverified";
    local["active_blocked"] = true;
    local["active_block_reason"] = "model_or_effort_not_reported_exactly";
}
else
{
    var planIssues = SpellCompiler.ValidatePlanJson(description, b.Utf8, geometryJson, maskPng);
    local["plan_validation_status"] = planIssues.Count == 0 ? "success" : "rejected";
    if (planIssues.Count != 0)
    {
        local["active_result"] = "plan_rejected";
        local["plan_issue_codes"] = planIssues.Select(issue => issue.Code).Distinct().ToArray();
    }
    else
    {
        var compilation = CompileDoctorProbe(description, b.Utf8, geometryJson, maskPng);
        local["compiler_version"] = SpellCompiler.Version;
        local["compilation_status"] = compilation.Success ? "success" : "rejected";
        local["active_result"] = compilation.Success ? "success" : "compilation_rejected";
        if (compilation.Success) local["compiled_probe_sha256"] = compilation.PayloadSha256;
        else local["compilation_issue_codes"] = compilation.Issues.Select(issue => issue.Code).Distinct().ToArray();
    }
}
await EmitAsync(local, writePath);
return string.Equals(local["active_result"] as string, "success", StringComparison.Ordinal) ? 0 : 1;

static string? ReadOption(string[] values, string name)
{
    for (var i = 0; i < values.Length - 1; i++)
        if (values[i] == name) return Path.GetFullPath(values[i + 1]);
    return null;
}

static string? ReadStringOption(string[] values, string name)
{
    for (var i = 0; i < values.Length - 1; i++)
        if (values[i] == name) return values[i + 1];
    return null;
}

static bool IsSha256(string? value) => value?.Length == 64 && value.All(Uri.IsHexDigit);
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

static CompilationResult CompileDoctorProbe(byte[] description, byte[] plan,
    IReadOnlyDictionary<string, byte[]> geometry, IReadOnlyDictionary<string, byte[]> masks)
{
    var geometryIds = geometry.Keys.OrderBy(key => key, StringComparer.Ordinal)
        .Select((key, index) => (key, index))
        .ToDictionary(item => item.key, item => "doctor-geometry-" + item.index, StringComparer.Ordinal);
    var maskIds = masks.Keys.OrderBy(key => key, StringComparer.Ordinal)
        .Select((key, index) => (key, index))
        .ToDictionary(item => item.key, item => "doctor-mask-" + item.index, StringComparer.Ordinal);
    return SpellCompiler.Compile(new CompilationInput
    {
        DescriptionJson = description, PlanJson = plan,
        GeometryJson = geometry, MaskPng = masks,
        GeometryArtifactIds = geometryIds, MaskArtifactIds = maskIds,
        SpellId = "doctor-only", ParchmentId = "doctor-only",
        SignatureSeedHex = "0000000000000000", CreatedAt = "2026-09-20T00:00:00Z",
        Provenance = new SpellProvenance
        {
            mode = "fixture", capture_sha256 = null, reference_sha256 = null,
            model_a = null, model_b = null,
            prompt_a_version = "doctor.offline", prompt_b_version = "doctor.offline",
            response_a_id = null, response_b_id = null
        }
    });
}

static object StageEvidence(ProviderDocument document) => new
{
    outcome = document.Transport.Outcome.ToString(),
    error_code = document.Transport.ErrorCode,
    exit_code = document.Transport.ExitCode,
    cli_version = document.Transport.CliVersion,
    requested_model = document.Transport.RequestedModel,
    requested_effort = document.Transport.RequestedEffort,
    reported_model = document.Transport.ReportedModel,
    reported_effort = document.Transport.ReportedEffort,
    process_started = document.Transport.ProcessStarted,
    started_at = document.Transport.StartedAt,
    ended_at = document.Transport.EndedAt,
    elapsed_ms = Math.Max(0, (long)(document.Transport.EndedAt - document.Transport.StartedAt).TotalMilliseconds),
    usage = document.Transport.UsageJson,
    diagnostic_category = document.Transport.DiagnosticCategory,
    diagnostic_stdout_sha256 = document.Transport.DiagnosticStdoutSha256,
    diagnostic_stdout_length = document.Transport.DiagnosticStdoutLength,
    diagnostic_stdout_truncated = document.Transport.DiagnosticStdoutTruncated,
    diagnostic_event_error_sha256 = document.Transport.DiagnosticEventErrorSha256,
    diagnostic_event_error_length = document.Transport.DiagnosticEventErrorLength,
    diagnostic_event_error_truncated = document.Transport.DiagnosticEventErrorTruncated,
    final_sha256 = document.Sha256,
    attempt_directory = document.Transport.AttemptDirectory
};

static Dictionary<string, bool> ParseFeatureList(string output)
{
    var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (columns.Length < 3 || !bool.TryParse(columns[^1], out var enabled)) continue;
        // The CLI uses a stable whitespace table: feature, lifecycle, bool.
        // Keep only a conservative identifier so arbitrary CLI text cannot
        // become a capability assertion in the evidence file.
        if (columns[0].All(character => char.IsLetterOrDigit(character) || character is '_' or '-'))
            result[columns[0]] = enabled;
    }
    return result;
}

static bool? ObservedFeature(string feature, IReadOnlyDictionary<string, bool> observations,
    IDictionary<string, string> aliases)
{
    if (observations.TryGetValue(feature, out var observed)) return observed;
    // Current CLI versions expose the network capability as
    // `standalone_web_search`; `web_search` remains a legacy accepted flag
    // but has no row in `features list`. Bind that alias only when the current
    // row is explicitly false, and preserve the mapping in the evidence.
    if (feature.Equals("web_search", StringComparison.OrdinalIgnoreCase) &&
        observations.TryGetValue("standalone_web_search", out var standalone))
    {
        aliases[feature] = "standalone_web_search";
        return standalone;
    }
    return null;
}

static bool MatchesRequestedMetadata(CodexResult transport, CodexSettings settings, string stage) =>
    transport.Outcome == ProviderOutcome.Success &&
    string.Equals(transport.ReportedModel, stage == "A" ? settings.InterpreterModel : settings.Model, StringComparison.Ordinal) &&
    string.Equals(transport.ReportedEffort, stage == "A" ? settings.InterpreterEffort : settings.Effort, StringComparison.OrdinalIgnoreCase);

static async Task EmitAsync(Dictionary<string, object?> document, string? writePath)
{
    var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);
    if (writePath is null) return;
    var full = Path.GetFullPath(writePath);
    var parent = Path.GetDirectoryName(full);
    if (parent is null || !Directory.Exists(parent) || CodexSettings.HasReparsePoint(parent))
        throw new InvalidOperationException("Evidence parent must be an existing non-reparse directory.");
    await File.WriteAllTextAsync(full, json + Environment.NewLine, new UTF8Encoding(false));
}

static async Task<CommandResult> RunCommandAsync(CodexSettings settings, params string[] command)
{
    if (!File.Exists(settings.Executable)) return new("unavailable", "missing executable", "");
    if (command.Length > 0 && command[0] == "login" && !Directory.Exists(settings.CodexHome))
        return new("unavailable", "missing dedicated home", "");
    var workingDirectory = Directory.Exists(settings.CodexHome)
        ? settings.CodexHome
        : Path.GetDirectoryName(settings.Executable)!;
    var psi = new ProcessStartInfo(settings.Executable)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        WorkingDirectory = workingDirectory,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };
    psi.Environment.Clear();
    psi.Environment["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
    psi.Environment["WINDIR"] = psi.Environment["SystemRoot"];
    if (Directory.Exists(settings.CodexHome))
    {
        psi.Environment["CODEX_HOME"] = settings.CodexHome;
        psi.Environment["USERPROFILE"] = settings.CodexHome;
        psi.Environment["APPDATA"] = settings.CodexHome;
        psi.Environment["LOCALAPPDATA"] = settings.CodexHome;
        psi.Environment["HOME"] = settings.CodexHome;
    }
    psi.Environment["PATH"] = Path.GetDirectoryName(settings.Executable)! + Path.PathSeparator + Path.Combine(psi.Environment["SystemRoot"]!, "System32");
    foreach (var arg in command) psi.ArgumentList.Add(arg);
    try
    {
        using var process = Process.Start(psi);
        if (process is null) return new("error", "process_not_started", "");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        var error = await errorTask;
        var detail = (output + "\n" + error).Trim();
        if (command.Length > 0 && command[0] == "login")
            return new(process.ExitCode == 0 && detail.Contains("Logged in", StringComparison.OrdinalIgnoreCase) ? "authenticated" : "not_authenticated", "redacted", "");
        return new(process.ExitCode == 0 ? "ok" : "failed", detail.Length > 400 ? detail[..400] : detail, output + error);
    }
    catch (Exception e) when (e is System.ComponentModel.Win32Exception or OperationCanceledException or IOException)
    {
        return new("error", e.GetType().Name, "");
    }
}

sealed record CommandResult(string Status, string Detail, string Output);

static class DoctorConstants
{
    public static readonly string[] RequiredOptions =
    [
        "--model", "--config", "--sandbox", "--json", "--image", "--output-schema",
        "--output-last-message", "--cd", "--skip-git-repo-check", "--ephemeral",
        "--ignore-user-config", "--ignore-rules", "--disable"
    ];

    public static readonly string[] DisabledFeatures =
    [
        "shell_tool", "unified_exec", "computer_use", "browser_use",
        "browser_use_external", "browser_use_full_cdp_access", "apps", "plugins", "hooks",
        "multi_agent", "skill_mcp_dependency_install", "shell_snapshot", "web_search", "standalone_web_search",
        "web_search_cached", "web_search_request", "remote_plugin", "goals", "memories",
        "personality", "code_mode", "in_app_browser", "in_app_chat", "in_app_dictation",
        "in_app_local_automation", "in_app_updates", "image_generation", "skill_search", "tool_suggest",
        "view_image", "workspace_dependencies", "sleep_tool", "tool_call_mcp_elicitation", "auth_elicitation",
        "code_mode_host", "unified_exec_tty"
    ];

    public static string[] DisabledHelpArguments()
    {
        var arguments = new List<string> { "exec", "--help" };
        foreach (var feature in DisabledFeatures) { arguments.Add("--disable"); arguments.Add(feature); }
        return arguments.ToArray();
    }

    public static string[] DisabledFeatureListArguments()
    {
        var arguments = new List<string>();
        foreach (var feature in DisabledFeatures) { arguments.Add("--disable"); arguments.Add(feature); }
        arguments.Add("features");
        arguments.Add("list");
        return arguments.ToArray();
    }
}
