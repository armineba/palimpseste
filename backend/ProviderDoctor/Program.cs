using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Palimpseste.Provider;

var settings = CodexSettings.FromEnvironment();
if (args.Length == 0 || args[0] is not ("local" or "active"))
{
    Console.Error.WriteLine("Usage: ProviderDoctor local [--write <evidence.json>] | active <spec-root> <reference.png> <drawing.png> <geometry.json> [--write <evidence.json>]");
    return 2;
}

var mode = args[0];
var writePath = ReadOption(args, "--write");
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
    ["runtime_features_effectiveness"] = effectiveDisableObserved
        ? (activeInternalFeatures.Length == 0
            ? "observed_disabled_by_local_features_list_only"
            : "exposed_tools_observed_disabled_internal_pty_feature_still_enabled")
        : "not_proven_without_an_active_capability_observation",
    ["runtime_execution_tools"] = false,
    ["production_issues"] = settings.Check(true),
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

var localExit = settings.Check(false).Count == 0 && version.Status == "ok" && help.Status == "ok" &&
    featureSyntax.Status == "ok" && featureList.Status == "ok" && optionIssues.Length == 0 &&
    featureObservationComplete && effectiveDisableObserved ? 0 : 1;
if (mode == "local")
{
    await EmitAsync(local, writePath);
    return localExit;
}

if (args.Length < 5)
{
    Console.Error.WriteLine("Active doctor requires spec-root, reference.png, drawing.png and geometry.json.");
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
// The compatibility test itself is allowed to establish the effort flag; the
// worker still requires PALIMPSESTE_EFFORT_VERIFIED=true afterwards.
var root = Path.GetFullPath(args[1]);
var reference = Path.GetFullPath(args[2]);
var drawing = Path.GetFullPath(args[3]);
var geometryPath = Path.GetFullPath(args[4]);
if (!string.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(settings.TrustedSpecificationRoot).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
    (!CodexSettings.IsTrustedFile(geometryPath, settings.TrustedSpecificationRoot) &&
     !settings.IsTrustedInputFile(geometryPath)) ||
    !settings.IsTrustedInputFile(reference) || !settings.IsTrustedInputFile(drawing))
{
    local["active_blocked"] = true;
    local["active_block_reason"] = "active_inputs_outside_declared_roots";
    await EmitAsync(local, writePath);
    return 2;
}

var probeSettings = settings with { EffortCompatibilityVerified = true };
var provider = new LunaCodexProvider(new CodexProcessRunner(probeSettings), root);
var layout = await File.ReadAllTextAsync(Path.Combine(root, "reference", "layout-v1.json"));
var capabilities = await File.ReadAllTextAsync(Path.Combine(root, "contracts", "capability-catalog.json"));
var geometry = await File.ReadAllTextAsync(geometryPath);
var a = await provider.InterpretAsync("operator-doctor", Guid.NewGuid().ToString("N"), reference, drawing, layout, capabilities, CancellationToken.None);
local["model_calls_executed"] = true;
local["stage_a"] = StageEvidence(a);
if (a.Utf8 is null)
{
    local["active_result"] = "stage_a_failed";
    await EmitAsync(local, writePath);
    return 1;
}
if (!MatchesRequestedMetadata(a.Transport, settings))
{
    local["active_result"] = "stage_a_metadata_unverified";
    local["active_blocked"] = true;
    local["active_block_reason"] = "model_or_effort_not_reported_exactly";
    await EmitAsync(local, writePath);
    return 1;
}

var b = await provider.PlanAsync("operator-doctor", Guid.NewGuid().ToString("N"), a.Utf8, geometry, capabilities, CancellationToken.None);
local["stage_b"] = StageEvidence(b);
if (b.Utf8 is null)
    local["active_result"] = "stage_b_failed";
else if (!MatchesRequestedMetadata(b.Transport, settings))
{
    local["active_result"] = "stage_b_metadata_unverified";
    local["active_blocked"] = true;
    local["active_block_reason"] = "model_or_effort_not_reported_exactly";
}
else
    local["active_result"] = "success";
await EmitAsync(local, writePath);
return b.Utf8 is null || !MatchesRequestedMetadata(b.Transport, settings) ? 1 : 0;

static string? ReadOption(string[] values, string name)
{
    for (var i = 0; i < values.Length - 1; i++)
        if (values[i] == name) return Path.GetFullPath(values[i + 1]);
    return null;
}

static object StageEvidence(ProviderDocument document) => new
{
    outcome = document.Transport.Outcome.ToString(),
    error_code = document.Transport.ErrorCode,
    cli_version = document.Transport.CliVersion,
    requested_model = document.Transport.RequestedModel,
    requested_effort = document.Transport.RequestedEffort,
    reported_model = document.Transport.ReportedModel,
    reported_effort = document.Transport.ReportedEffort,
    usage = document.Transport.UsageJson,
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

static bool MatchesRequestedMetadata(CodexResult transport, CodexSettings settings) =>
    transport.Outcome == ProviderOutcome.Success &&
    string.Equals(transport.ReportedModel, settings.Model, StringComparison.Ordinal) &&
    string.Equals(transport.ReportedEffort, settings.Effort, StringComparison.OrdinalIgnoreCase);

static async Task EmitAsync(Dictionary<string, object?> document, string? writePath)
{
    var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);
    if (writePath is null) return;
    var full = Path.GetFullPath(writePath);
    var parent = Path.GetDirectoryName(full);
    if (parent is null || !Directory.Exists(parent) || CodexSettings.HasReparsePoint(parent))
        throw new InvalidOperationException("Evidence parent must be an existing non-reparse directory.");
    await File.WriteAllTextAsync(full, json + Environment.NewLine, Encoding.UTF8);
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
