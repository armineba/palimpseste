using System.Text;
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
await File.WriteAllTextAsync(divergentModelSchema, "{\"type\":\"object\",\"x-test\":\"divergent-model\"}");
await File.WriteAllTextAsync(divergentEffortSchema, "{\"type\":\"object\",\"x-test\":\"divergent-effort\"}");
await File.WriteAllTextAsync(missingModelSchema, "{\"type\":\"object\",\"x-test\":\"missing-model\"}");
await File.WriteAllTextAsync(missingEffortSchema, "{\"type\":\"object\",\"x-test\":\"missing-effort\"}");
await File.WriteAllTextAsync(invalidJsonSchema, "{\"type\":\"object\",\"x-test\":\"invalid-json\"}");
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
Environment.SetEnvironmentVariable("PALIMPSESTE_ALLOW_MODEL_FALLBACK", "false");
Environment.SetEnvironmentVariable("PALIMPSESTE_ALLOW_REASONING_DOWNGRADE", "false");
Environment.SetEnvironmentVariable("PALIMPSESTE_RUNTIME_EXECUTION_TOOLS", "false");
Environment.SetEnvironmentVariable("OPENAI_API_KEY", "personal-secret-sentinel");
Environment.SetEnvironmentVariable("CODEX_HOME", "E:\\PersonalCodexSentinel");

try
{
    var settings = new CodexSettings(
        fakeCommand, home, attempts, Environment.UserName, "gpt-5.6-luna", "max", true,
        TimeSpan.FromSeconds(20), 2_000_000, spec, input, development);
    var productionIssues = settings.Check(true);
    Assert(productionIssues.Contains("spec_tree_service_is_owner") || productionIssues.Contains("spec_tree_service_write_access"),
        "an insecure fixture must be refused by the production ACL gate");
    Assert(settings.Check(false).Count == 0, "transport fixture must pass non-production isolation check: " + string.Join(',', settings.Check(false)));
    var overlap = settings with { TrustedInputRoot = attempts };
    Assert(overlap.Check(false).Contains("attempt_root_and_input_root_overlap"),
        "attempt state and trusted input must not share a root");
    var profile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetEnvironmentVariable("HOME");
    if (!string.IsNullOrWhiteSpace(profile) && Directory.Exists(profile))
        Assert((settings with { CodexHome = profile }).Check(false).Contains("codex_home_inside_user_profile"),
            "a personal profile must not become CODEX_HOME");

    var runner = new CodexProcessRunner(settings);
    // ProbeAsync intentionally exercises transport only; production RunAsync
    // remains blocked by the deliberately insecure fixture ACL above.
    var valid = await runner.ProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "prompt contains & whoami and > pwned.txt", schema,
        [reference, drawing], "job-security"), CancellationToken.None);
    Assert(valid.Outcome == ProviderOutcome.Success, "valid fake Codex invocation should succeed: " + valid.ErrorCode + " dir=" + valid.AttemptDirectory);
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
    Assert((await File.ReadAllTextAsync(Path.Combine(valid.AttemptDirectory!, "runtime-codex-home.txt"))).Equals(home, StringComparison.OrdinalIgnoreCase),
        "personal CODEX_HOME must be replaced by the dedicated runtime home");

    var divergentModel = await runner.ProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", divergentModelSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(divergentModel.Outcome == ProviderOutcome.ModelUnavailable && divergentModel.ErrorCode == "reported_model_mismatch",
        "a reported model different from the requested model must never be accepted");

    var divergentEffort = await runner.ProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", divergentEffortSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(divergentEffort.Outcome == ProviderOutcome.EffortUnsupported && divergentEffort.ErrorCode == "reported_effort_mismatch",
        "a reported reasoning effort different from the requested effort must never be accepted");

    var missingModel = await runner.ProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", missingModelSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(missingModel.Outcome == ProviderOutcome.ModelUnavailable && missingModel.ErrorCode == "reported_model_missing",
        "a successful turn without reported model metadata must fail closed");

    var missingEffort = await runner.ProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", missingEffortSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(missingEffort.Outcome == ProviderOutcome.EffortUnsupported && missingEffort.ErrorCode == "reported_effort_missing",
        "a successful turn without reported reasoning effort must fail closed");

    var invalidJson = await runner.ProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", invalidJsonSchema, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(invalidJson.Outcome == ProviderOutcome.InvalidSchema && invalidJson.ErrorCode == "final_not_json" &&
        invalidJson.FinalJson == "{not-json",
        "a malformed final must be retained as bounded repair input without being accepted");

    var outsideSchema = await runner.ProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", outside, [reference, drawing], "job-security"), CancellationToken.None);
    Assert(outsideSchema.Outcome == ProviderOutcome.IsolationViolation && outsideSchema.ErrorCode == "invalid_trusted_input",
        "schema outside the specification root must be rejected before launch");

    var outsideImage = await runner.ProbeAsync(new(
        Guid.NewGuid().ToString("N"), "A", "safe", schema, [outside, drawing], "job-security"), CancellationToken.None);
    Assert(outsideImage.Outcome == ProviderOutcome.IsolationViolation && outsideImage.ErrorCode == "untrusted_image_path",
        "image outside the declared input root must be rejected before launch");

    var wrongStageImages = await runner.ProbeAsync(new(
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
        var version = schema?.Contains("model-b", StringComparison.OrdinalIgnoreCase) == true ? "sp.plan/1.0" : "sp.description/1.0";
        await File.WriteAllTextAsync(output, schemaText.Contains("invalid-json", StringComparison.OrdinalIgnoreCase)
            ? "{not-json"
            : $"{{\"schema_version\":\"{version}\"}}", new UTF8Encoding(false));
        var model = schemaText.Contains("divergent-model", StringComparison.OrdinalIgnoreCase) ? "other-model" : "gpt-5.6-luna";
        var effort = schemaText.Contains("divergent-effort", StringComparison.OrdinalIgnoreCase) ? "high" : "max";
        var reportedModel = schemaText.Contains("missing-model", StringComparison.OrdinalIgnoreCase) ? null : model;
        var reportedEffort = schemaText.Contains("missing-effort", StringComparison.OrdinalIgnoreCase) ? null : effort;
        Console.WriteLine(reportedModel is null
            ? "{\"type\":\"thread.started\",\"thread_id\":\"fake-thread\"}"
            : $"{{\"type\":\"thread.started\",\"thread_id\":\"fake-thread\",\"model\":\"{reportedModel}\"}}");
        Console.WriteLine(reportedEffort is null
            ? "{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}"
            : $"{{\"type\":\"turn.completed\",\"usage\":{{\"input_tokens\":1,\"output_tokens\":1}},\"reasoning_effort\":\"{reportedEffort}\"}}");
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
