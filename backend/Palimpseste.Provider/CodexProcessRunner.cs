using System.Diagnostics;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Palimpseste.Provider;

// This class only transports trusted application prompts and artifacts. It accepts no client arguments.
public sealed class CodexProcessRunner
{
    private static readonly byte[] PngMagic = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly string[] DisabledFeatures =
    [
        "shell_tool", "unified_exec", "computer_use", "browser_use",
        "browser_use_external", "browser_use_full_cdp_access", "apps",
        "plugins", "hooks", "multi_agent", "skill_mcp_dependency_install",
        "shell_snapshot", "web_search", "standalone_web_search", "web_search_cached", "web_search_request",
        "remote_plugin", "goals", "memories", "personality", "code_mode", "in_app_browser",
        "in_app_chat", "in_app_dictation", "in_app_local_automation", "in_app_updates",
        "image_generation", "skill_search", "tool_suggest", "view_image", "workspace_dependencies",
        "sleep_tool", "tool_call_mcp_elicitation", "auth_elicitation", "code_mode_host", "unified_exec_tty"
    ];
    private readonly CodexSettings settings;
    public CodexProcessRunner(CodexSettings settings) => this.settings = settings;

    public Task<CodexResult> RunAsync(CodexAttempt attempt, CancellationToken cancellationToken) => ExecuteAsync(attempt, true, cancellationToken);

    // Only the operator's explicit active doctor uses this path to establish compatibility.
    public Task<CodexResult> ProbeAsync(CodexAttempt attempt, CancellationToken cancellationToken) => ExecuteAsync(attempt, false, cancellationToken);

    private async Task<CodexResult> ExecuteAsync(CodexAttempt attempt, bool production, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var issues = settings.Check(production);
        if (issues.Count != 0) return Failure(ProviderOutcome.IsolationViolation, string.Join(',', issues), started);
        if (attempt is null || attempt.Stage is not ("A" or "B") || attempt.Images is null ||
            attempt.Images.Count != (attempt.Stage == "A" ? 2 : 0))
            return Failure(ProviderOutcome.IsolationViolation, "stage_image_count", started);
        if (!settings.IsTrustedSpecificationFile(attempt.SchemaPath) ||
            string.IsNullOrWhiteSpace(attempt.Prompt) || attempt.Prompt.Length > 200_000 ||
            string.IsNullOrWhiteSpace(attempt.JobId) || attempt.JobId.Length > 256)
            return Failure(ProviderOutcome.IsolationViolation, "invalid_trusted_input", started);
        if (attempt.Images.Any(image => !settings.IsTrustedInputFile(image)))
            return Failure(ProviderOutcome.IsolationViolation, "untrusted_image_path", started);

        // Application-generated ID is registered durably before launch, so a crash can be reconciled.
        if (!Guid.TryParseExact(attempt.AttemptId, "N", out _))
            return Failure(ProviderOutcome.IsolationViolation, "invalid_attempt_id", started);
        var directory = Path.Combine(settings.AttemptRoot, attempt.AttemptId);
        if (Directory.Exists(directory) || File.Exists(directory) || CodexSettings.HasReparsePoint(directory))
            return Failure(ProviderOutcome.IsolationViolation, "attempt_directory_exists", started);
        try { Directory.CreateDirectory(directory); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failure(ProviderOutcome.IsolationViolation, "attempt_directory_create_failed", started);
        }
        if (!CodexSettings.IsPathInside(directory, settings.AttemptRoot, allowEqual: false) || CodexSettings.HasReparsePoint(directory))
            return Failure(ProviderOutcome.IsolationViolation, "attempt_directory_isolation_failed", started, directory: directory);
        var schemaPath = Path.Combine(directory, "schema.json");
        var outputPath = Path.Combine(directory, "final.json");
        try
        {
            File.Copy(attempt.SchemaPath, schemaPath);
            await File.WriteAllTextAsync(Path.Combine(directory, "prompt.txt"), attempt.Prompt, Encoding.UTF8, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "attempt.json"), JsonSerializer.Serialize(new
            {
                attempt.AttemptId, attempt.JobId, attempt.Stage, started,
                requested_model = settings.Model, requested_effort = settings.Effort,
                reported_model = (string?)null, reported_effort = (string?)null,
                prompt_sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(attempt.Prompt)))
            }), Encoding.UTF8, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failure(ProviderOutcome.IsolationViolation, "attempt_input_materialization_failed", started, directory: directory);
        }
        var imagePaths = new List<string>();
        for (var i = 0; i < attempt.Images.Count; i++)
        {
            byte[] bytes;
            try { bytes = await File.ReadAllBytesAsync(attempt.Images[i], cancellationToken); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return Failure(ProviderOutcome.IsolationViolation, "image_read_failed", started, directory: directory);
            }
            if (bytes.Length < 24 || bytes.Length > 8 * 1024 * 1024 || !bytes.AsSpan(0, 8).SequenceEqual(PngMagic) ||
                BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) != 1024 ||
                BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) != 1024)
                return Failure(ProviderOutcome.IsolationViolation, "invalid_png_input", started);
            var local = Path.Combine(directory, i == 0 ? "reference.png" : "drawing.png");
            try { await File.WriteAllBytesAsync(local, bytes, cancellationToken); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return Failure(ProviderOutcome.IsolationViolation, "image_materialization_failed", started, directory: directory);
            }
            imagePaths.Add(local);
        }

        var psi = new ProcessStartInfo(settings.Executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = directory,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.Environment.Clear();
        psi.Environment["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        psi.Environment["WINDIR"] = psi.Environment["SystemRoot"];
        psi.Environment["TEMP"] = directory;
        psi.Environment["TMP"] = directory;
        psi.Environment["CODEX_HOME"] = settings.CodexHome;
        psi.Environment["USERPROFILE"] = settings.CodexHome;
        psi.Environment["APPDATA"] = settings.CodexHome;
        psi.Environment["LOCALAPPDATA"] = settings.CodexHome;
        psi.Environment["HOME"] = settings.CodexHome;
        psi.Environment["DOTNET_CLI_HOME"] = settings.CodexHome;
        psi.Environment["GIT_CONFIG_GLOBAL"] = "NUL";
        psi.Environment["GIT_CONFIG_SYSTEM"] = "NUL";
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["PATH"] = Path.GetDirectoryName(settings.Executable)! + Path.PathSeparator + Path.Combine(psi.Environment["SystemRoot"]!, "System32");
        foreach (var arg in new[] { "exec", "--model", settings.Model, "--config", $"model_reasoning_effort=\"{settings.Effort}\"", "--config", "approval_policy=\"never\"", "--sandbox", "read-only", "--json", "--ephemeral", "--ignore-user-config", "--ignore-rules", "--skip-git-repo-check", "--cd", directory, "--output-schema", schemaPath, "--output-last-message", outputPath })
            psi.ArgumentList.Add(arg);
        foreach (var feature in DisabledFeatures)
        {
            psi.ArgumentList.Add("--disable");
            psi.ArgumentList.Add(feature);
        }
        // The CLI's --image accepts one or more files. Repeated flags preserve the required order.
        foreach (var image in imagePaths) { psi.ArgumentList.Add("--image"); psi.ArgumentList.Add(image); }
        psi.ArgumentList.Add("-");

        using var process = new Process { StartInfo = psi };
        try
        {
            if (!process.Start()) return Failure(ProviderOutcome.ProcessFailure, "process_not_started", started);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Failure(ProviderOutcome.ProcessFailure, "process_start_failed", started);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.AttemptTimeout);
        var stdout = ReadBoundedAsync(process.StandardOutput, settings.MaxOutputBytes, timeout.Token);
        var stderr = ReadBoundedAsync(process.StandardError, 128_000, timeout.Token);
        try
        {
            await process.StandardInput.WriteAsync(attempt.Prompt.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var outText = await stdout;
            var errText = await stderr;
            var events = ParseEvents(outText);
            if (process.ExitCode != 0) return Failure(Classify(errText + "\n" + events.Error), "codex_exit_nonzero", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort);
            if (!events.Completed || events.Failed || !File.Exists(outputPath))
                return Failure(ProviderOutcome.Incomplete, "missing_completed_turn_or_final", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort);
            if (events.ReportedModel is null)
                return Failure(ProviderOutcome.ModelUnavailable, "reported_model_missing", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort);
            if (!string.Equals(events.ReportedModel, settings.Model, StringComparison.Ordinal))
                return Failure(ProviderOutcome.ModelUnavailable, "reported_model_mismatch", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort);
            if (events.ReportedEffort is null)
                return Failure(ProviderOutcome.EffortUnsupported, "reported_effort_missing", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort);
            if (!string.Equals(events.ReportedEffort, settings.Effort, StringComparison.OrdinalIgnoreCase))
                return Failure(ProviderOutcome.EffortUnsupported, "reported_effort_mismatch", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort);
            var info = new FileInfo(outputPath);
            if (info.Length == 0 || info.Length > settings.MaxOutputBytes)
                return Failure(ProviderOutcome.Incomplete, "empty_or_oversize_final", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort);
            if (CodexSettings.HasReparsePoint(outputPath))
                return Failure(ProviderOutcome.IsolationViolation, "final_output_reparse_point", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort);
            var json = await File.ReadAllTextAsync(outputPath, Encoding.UTF8, timeout.Token);
            try { using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 }); }
            catch (JsonException) { return Failure(ProviderOutcome.InvalidSchema, "final_not_json", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, json); }
            var result = new CodexResult(ProviderOutcome.Success, json, process.ExitCode, events.SessionId, null, started,
                DateTimeOffset.UtcNow, GetCliVersion(), settings.Model, settings.Effort, events.ReportedModel, events.ReportedEffort, events.UsageJson, directory);
            await TryWriteAttemptOutcomeAsync(directory, result, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { await process.WaitForExitAsync(CancellationToken.None); } catch (InvalidOperationException) { }
            return Failure(cancellationToken.IsCancellationRequested ? ProviderOutcome.TransportUncertain : ProviderOutcome.Timeout,
                cancellationToken.IsCancellationRequested ? "cancelled_after_launch" : "attempt_timeout", started, directory: directory);
        }
        catch (InvalidDataException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return Failure(ProviderOutcome.Incomplete, "event_stream_too_large", started, directory: directory);
        }
        catch (IOException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return Failure(ProviderOutcome.ProcessFailure, "provider_io_failed", started, directory: directory);
        }
        catch (UnauthorizedAccessException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return Failure(ProviderOutcome.IsolationViolation, "provider_access_denied", started, directory: directory);
        }
    }

    private CodexResult Failure(ProviderOutcome outcome, string code, DateTimeOffset started, int? exitCode = null,
        string? session = null, string? usage = null, string? directory = null,
        string? reportedModel = null, string? reportedEffort = null, string? finalJson = null) =>
        new(outcome, finalJson, exitCode, session, code, started, DateTimeOffset.UtcNow, GetCliVersion(),
            settings.Model, settings.Effort, reportedModel, reportedEffort, usage, directory);

    private static async Task TryWriteAttemptOutcomeAsync(string directory, CodexResult result, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(directory, "attempt.json");
            if (!File.Exists(path) || CodexSettings.HasReparsePoint(path)) return;
            var source = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
            using var doc = JsonDocument.Parse(source);
            var values = new Dictionary<string, object?>();
            foreach (var property in doc.RootElement.EnumerateObject()) values[property.Name] = property.Value.Clone();
            values["outcome"] = result.Outcome.ToString();
            values["error_code"] = result.ErrorCode;
            values["exit_code"] = result.ExitCode;
            values["session_id"] = result.SessionId;
            values["reported_model"] = result.ReportedModel;
            values["reported_effort"] = result.ReportedEffort;
            values["usage"] = result.UsageJson;
            values["ended_at"] = result.EndedAt;
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(values), Encoding.UTF8, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or OperationCanceledException)
        {
            // The provider result remains authoritative. Evidence enrichment
            // must never turn a completed call into a fabricated failure.
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken ct)
    {
        var buffer = new char[4096];
        var result = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), ct);
            if (read == 0) return result.ToString();
            if (result.Length + read > limit) throw new InvalidDataException("stream limit");
            result.Append(buffer, 0, read);
        }
    }

    private static (bool Completed, bool Failed, string? Error, string? SessionId, string? UsageJson, string? ReportedModel, string? ReportedEffort) ParseEvents(string jsonl)
    {
        var completed = false; var failed = false; string? error = null; string? session = null; string? usage = null;
        string? reportedModel = null; string? reportedEffort = null;
        foreach (var line in jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeElement)) continue;
                var type = typeElement.GetString();
                if (type == "thread.started" && root.TryGetProperty("thread_id", out var id)) session = id.GetString();
                if (root.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String) reportedModel = model.GetString();
                if (root.TryGetProperty("reasoning_effort", out var effort) && effort.ValueKind == JsonValueKind.String) reportedEffort = effort.GetString();
                if (type == "turn.completed")
                {
                    completed = true;
                    if (root.TryGetProperty("usage", out var value)) usage = value.GetRawText();
                }
                if (type is "turn.failed" or "error")
                {
                    failed = true;
                    if (root.TryGetProperty("error", out var value)) error = value.ToString();
                    else if (root.TryGetProperty("message", out value)) error = value.ToString();
                }
            }
            catch (JsonException) { failed = true; error = "invalid_jsonl_event"; }
        }
        return (completed, failed, error, session, usage, reportedModel, reportedEffort);
    }

    private static ProviderOutcome Classify(string message)
    {
        var lower = message.ToLowerInvariant();
        if (lower.Contains("quota") || lower.Contains("rate limit")) return ProviderOutcome.Quota;
        if (lower.Contains("unauthorized") || lower.Contains("authentication") || lower.Contains("login")) return ProviderOutcome.Authentication;
        if (lower.Contains("model") && (lower.Contains("not found") || lower.Contains("unsupported"))) return ProviderOutcome.ModelUnavailable;
        if (lower.Contains("reasoning") && (lower.Contains("unsupported") || lower.Contains("invalid"))) return ProviderOutcome.EffortUnsupported;
        if (lower.Contains("refus")) return ProviderOutcome.Refusal;
        return ProviderOutcome.ProcessFailure;
    }

    private string GetCliVersion()
    {
        try { return FileVersionInfo.GetVersionInfo(settings.Executable).ProductVersion ?? "non exposé"; }
        catch { return "non exposé"; }
    }
}
