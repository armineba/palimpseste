using System.Diagnostics;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;

namespace Palimpseste.Provider;

// This class only transports trusted application prompts and artifacts. It accepts no client arguments.
public sealed class CodexProcessRunner
{
    private const int DiagnosticCharacterLimit = 16_384;
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

    public Task<CodexResult> RunAsync(CodexAttempt attempt, CancellationToken cancellationToken) => ExecuteAsync(attempt, production: true, compatibilityProbe: false, cancellationToken: cancellationToken);

    // Only the operator's explicit active doctor uses this path to establish
    // effort compatibility. It retains every production isolation check and
    // skips only the evidence that this call is about to create.
    public Task<CodexResult> ProbeAsync(CodexAttempt attempt, CancellationToken cancellationToken) => ExecuteAsync(attempt, production: true, compatibilityProbe: true, cancellationToken: cancellationToken);

    // Transport-only fixture hook. ProviderDoctor never calls this method;
    // security tests use it with a fake executable to test argument and
    // environment handling without pretending to prove runtime isolation.
    public Task<CodexResult> TransportProbeAsync(CodexAttempt attempt, CancellationToken cancellationToken) => ExecuteAsync(attempt, production: false, compatibilityProbe: false, cancellationToken: cancellationToken);

    private async Task<CodexResult> ExecuteAsync(CodexAttempt attempt, bool production, bool compatibilityProbe, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var requestedModel = attempt?.Stage == "A" ? settings.InterpreterModel : settings.Model;
        var requestedEffort = attempt?.Stage == "A" ? settings.InterpreterEffort : settings.Effort;
        CodexResult Failure(ProviderOutcome outcome, string code, DateTimeOffset began, int? exitCode = null,
            string? session = null, string? usage = null, string? directory = null,
            string? reportedModel = null, string? reportedEffort = null, string? finalJson = null, bool processStarted = false,
            string? diagnosticStderr = null, string? diagnosticStdoutSha256 = null,
            int? diagnosticStdoutLength = null, bool diagnosticStdoutTruncated = false,
            string? diagnosticEventErrorSha256 = null, int? diagnosticEventErrorLength = null,
            bool diagnosticEventErrorTruncated = false, string? diagnosticCategory = null) =>
            CreateFailure(requestedModel, requestedEffort, outcome, code, began, exitCode,
                session, usage, directory, reportedModel, reportedEffort, finalJson, processStarted,
                diagnosticStderr, diagnosticStdoutSha256, diagnosticStdoutLength, diagnosticStdoutTruncated,
                diagnosticEventErrorSha256, diagnosticEventErrorLength, diagnosticEventErrorTruncated, diagnosticCategory);
        var issues = settings.Check(production, compatibilityProbe, attempt?.Stage ?? "B");
        if (issues.Count != 0) return Failure(ProviderOutcome.IsolationViolation, string.Join(',', issues), started);
        if (attempt is null || attempt.Stage is not ("A" or "B" or "G" or "J") || attempt.Images is null ||
            (attempt.ImageSha256 is null
                ? attempt.Stage == "J" || attempt.Images.Count != (attempt.Stage == "A" ? 2 : attempt.Stage == "B" && attempt.VisualReference is not null ? 1 : 0)
                : attempt.Stage is not ("B" or "J") || attempt.Images.Count is < 2 or > 5 ||
                  attempt.ImageSha256.Count != attempt.Images.Count || attempt.ImageSha256.Any(h => h.Length != 64 || h.Any(c => !char.IsAsciiHexDigitLower(c)))) ||
            attempt.Stage is not ("B" or "J") && attempt.VisualReference is not null ||
            attempt.Stage == "J" && attempt.VisualReference is null)
            return Failure(ProviderOutcome.IsolationViolation, "stage_image_count", started);
        if (attempt.VisualReference is { } visualReference &&
            (!string.Equals(attempt.Images[0], visualReference.PngPath, StringComparison.OrdinalIgnoreCase) ||
             visualReference.Sha256.Length != 64 || visualReference.Sha256.Any(c => c is not (>= 'a' and <= 'f') and not (>= '0' and <= '9'))))
            return Failure(ProviderOutcome.IsolationViolation, "visual_reference_binding_invalid", started);
        if (attempt.Stage == "J" && attempt.JudgementBinding is null ||
            attempt.JudgementBinding is { } binding &&
            (attempt.Stage != "J" || !IsLowerSha256(binding.DescriptionSha) ||
             !IsLowerSha256(binding.PlanSha) || !IsLowerSha256(binding.ReferenceSha) ||
             binding.ReferenceSha != attempt.VisualReference?.Sha256 ||
             binding.ReferenceSha != attempt.ImageSha256?[0]))
            return Failure(ProviderOutcome.IsolationViolation, "visual_judgement_binding_invalid", started);
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
            // Whitespace and escaped French text inflated each isolated call.
            // Compact the same schema without removing any constraint.
            var schema = JsonNode.Parse(await File.ReadAllTextAsync(attempt.SchemaPath, cancellationToken))
                ?? throw new InvalidDataException("trusted_schema_missing");
            if (attempt.JudgementBinding is { } expected)
            {
                // Only the private per-attempt copy is constrained. The trusted
                // source, its patterns and every other constraint stay intact.
                PinJudgementHash(schema, "description_sha256", expected.DescriptionSha);
                PinJudgementHash(schema, "plan_sha256", expected.PlanSha);
                PinJudgementHash(schema, "visual_reference_sha256", expected.ReferenceSha);
            }
            var schemaBytes = Encoding.UTF8.GetBytes(schema.ToJsonString(
                new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            await File.WriteAllBytesAsync(schemaPath, schemaBytes, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "prompt.txt"), attempt.Prompt, Encoding.UTF8, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "attempt.json"), JsonSerializer.Serialize(new
            {
                attempt.AttemptId, attempt.JobId, attempt.Stage, started,
                requested_model = requestedModel, requested_effort = requestedEffort,
                reported_model = (string?)null, reported_effort = (string?)null,
                prompt_utf8_bytes = Encoding.UTF8.GetByteCount(attempt.Prompt),
                schema_utf8_bytes = schemaBytes.Length,
                schema_sha256 = Convert.ToHexStringLower(SHA256.HashData(schemaBytes)),
                expected_bindings = attempt.JudgementBinding is { } provenance ? new
                {
                    description_sha256 = provenance.DescriptionSha,
                    plan_sha256 = provenance.PlanSha,
                    visual_reference_sha256 = provenance.ReferenceSha
                } : null,
                visual_reference_sha256 = attempt.VisualReference?.Sha256,
                image_sha256 = attempt.ImageSha256,
                prompt_sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(attempt.Prompt)))
            }), Encoding.UTF8, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException)
        {
            return Failure(ProviderOutcome.IsolationViolation, "attempt_input_materialization_failed", started, directory: directory);
        }
        var imagePaths = new List<string>();
        for (var i = 0; i < attempt.Images.Count; i++)
        {
            byte[] bytes;
            try
            {
                using var input = new FileStream(attempt.Images[i], FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length is < 24 or > VisualReferencePng.MaxBytes) throw new InvalidDataException("image_size");
                bytes = new byte[checked((int)input.Length)];
                await input.ReadExactlyAsync(bytes, cancellationToken);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return Failure(ProviderOutcome.IsolationViolation, "image_read_failed", started, directory: directory);
            }
            if (attempt.Stage is "B" or "J")
            {
                try
                {
                    // Only the hash-bound rendering inputs after a D16 target may use
                    // the fixed 2048x320 format. The generated target retains its stricter bounds.
                    if (i > 0 && attempt.ImageSha256 is not null && attempt.VisualReference?.AnimationSheetJson is not null)
                        VisualReferencePng.ValidateAnimationStrip(bytes);
                    else VisualReferencePng.Validate(bytes);
                    var expectedHash = attempt.ImageSha256 is null ? attempt.VisualReference!.Sha256 : attempt.ImageSha256[i];
                    if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expectedHash ||
                        i == 0 && expectedHash != attempt.VisualReference!.Sha256)
                        return Failure(ProviderOutcome.IsolationViolation, "visual_reference_input_hash_mismatch", started);
                }
                catch (IOException) { return Failure(ProviderOutcome.IsolationViolation, "invalid_visual_reference_png", started); }
            }
            else if (bytes.Length < 24 || bytes.Length > 8 * 1024 * 1024 || !bytes.AsSpan(0, 8).SequenceEqual(PngMagic) ||
                     BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) != 1024 ||
                     BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) != 1024)
                return Failure(ProviderOutcome.IsolationViolation, "invalid_png_input", started);
            var local = Path.Combine(directory, attempt.Stage is "B" or "J" ? i == 0 ? "visual-reference.png" : "render-" + i + ".png" : i == 0 ? "reference.png" : "drawing.png");
            try { await File.WriteAllBytesAsync(local, bytes, cancellationToken); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return Failure(ProviderOutcome.IsolationViolation, "image_materialization_failed", started, directory: directory);
            }
            imagePaths.Add(local);
        }

        var executable = settings.ExecutableForStage(attempt.Stage);
        var psi = new ProcessStartInfo(executable)
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
        psi.Environment["PATH"] = Path.GetDirectoryName(executable)! + Path.PathSeparator + Path.Combine(psi.Environment["SystemRoot"]!, "System32");
        // The pinned CLI removes every runtime tool except the native image
        // generator. That feature is disabled for A/B, making their registry empty.
        psi.Environment["PALIMPSESTE_IMAGEGEN_TEXT_ONLY"] = "1";
        foreach (var arg in new[] { "exec", "--model", requestedModel, "--config", $"model_reasoning_effort=\"{requestedEffort}\"", "--config", "approval_policy=\"never\"", "--config", "forced_login_method=\"chatgpt\"", "--sandbox", "read-only", "--json", "--ephemeral", "--ignore-user-config", "--ignore-rules", "--skip-git-repo-check", "--cd", directory, "--output-schema", schemaPath, "--output-last-message", outputPath })
            psi.ArgumentList.Add(arg);
        foreach (var feature in DisabledFeatures)
        {
            if (attempt.Stage == "G" && feature == "image_generation") continue;
            psi.ArgumentList.Add("--disable");
            psi.ArgumentList.Add(feature);
        }
        if (attempt.Stage == "G") { psi.ArgumentList.Add("--enable"); psi.ArgumentList.Add("image_generation"); }
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

        const bool processStarted = true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.AttemptTimeout);
        var stdoutCapture = new StreamDiagnosticCapture();
        var stderrCapture = new StreamDiagnosticCapture();
        var stdout = ReadBoundedAsync(process.StandardOutput, settings.MaxOutputBytes, stdoutCapture, timeout.Token);
        var stderr = ReadBoundedAsync(process.StandardError, 128_000, stderrCapture, timeout.Token);
        Task? inputTask = null;
        Task? exited = null;

        async Task<CodexResult> FailAfterLaunchAsync(ProviderOutcome outcome, string code, string category)
        {
            // Cancellation stops the remaining pipe operations; process termination
            // is independent of the caller's already-cancelled token. Never await a
            // live child indefinitely when a read has stopped draining its pipe.
            timeout.Cancel();
            var stopped = await StopProcessAsync(process);
            await ObserveStoppedTasksAsync(stdout, stderr, inputTask, exited);
            var stdoutDiagnostic = stdoutCapture.Snapshot();
            var stderrDiagnostic = stderrCapture.Snapshot();
            var partialEvents = ReadDiagnosticEvents(stdoutDiagnostic);
            var eventDiagnostic = SnapshotDiagnostic(partialEvents.Error);
            // A timeout or pipe failure does not erase an already observed
            // protected failure. Missing completion in an ordinary prefix is
            // not an attestation violation; ParseEvents only declares one when
            // an invalid/duplicate/order event or a completed turn proves it.
            if (partialEvents.AttestationError is { } attestationError)
            {
                outcome = ProviderOutcome.IsolationViolation;
                code = attestationError;
                category += "_protected_attestation";
            }
            else if (partialEvents.Error is { } nativeError && Classify(nativeError) == ProviderOutcome.Refusal)
            {
                outcome = ProviderOutcome.Refusal;
                code = "provider_refusal";
                category += "_protected_refusal";
            }
            var failure = Failure(stopped.Exited ? outcome : ProviderOutcome.TransportUncertain,
                code, started, stopped.ExitCode, partialEvents.SessionId, partialEvents.UsageJson,
                directory, partialEvents.ReportedModel, partialEvents.ReportedEffort,
                processStarted: processStarted, diagnosticStderr: stderrDiagnostic?.Text,
                diagnosticStdoutSha256: stdoutDiagnostic?.Sha256,
                diagnosticStdoutLength: stdoutDiagnostic?.Length,
                diagnosticStdoutTruncated: stdoutDiagnostic?.Truncated ?? false,
                diagnosticEventErrorSha256: eventDiagnostic?.Sha256,
                diagnosticEventErrorLength: eventDiagnostic?.Length,
                diagnosticEventErrorTruncated: eventDiagnostic?.Truncated ?? false,
                diagnosticCategory: stopped.Exited ? category : category + "_termination_unconfirmed");
            return await PersistFailureAsync(failure, stdoutDiagnostic?.Text, eventDiagnostic?.Text);
        }

        try
        {
            exited = process.WaitForExitAsync(timeout.Token);
            inputTask = WritePromptAsync(process.StandardInput, attempt.Prompt, timeout.Token);
            // A reader may reach its limit or fail while the child is still alive.
            // Observe every completion as it happens instead of waiting for exit
            // first, which can deadlock against a full, no-longer-drained pipe.
            await ObserveProcessTasksAsync(inputTask, stdout, stderr, exited);
            var outText = await stdout;
            var errText = await stderr;
            var diagnosticStderr = BoundDiagnostic(errText);
            var events = ParseEvents(outText);
            if (process.ExitCode != 0)
            {
                // A failing codex exec can put the useful diagnostic in its
                // JSONL stdout even when stderr only says that stdin is being
                // read. Keep the bounded raw JSONL in the private attempt
                // directory; only its hashes, lengths and category leave the
                // provider boundary.
                var stdoutDiagnostic = SnapshotDiagnostic(outText);
                var eventDiagnostic = SnapshotDiagnostic(events.Error);
                var diagnosticCategory = ChooseDiagnosticCategory(errText, events.Error, outText);
                var outcome = Classify(errText + "\n" + events.Error + "\n" + outText);
                // Preserve a provider request-schema error as an operator diagnostic.
                // It is not a malformed spell or a visual rejection by the model.
                var exitCode = outcome == ProviderOutcome.ProcessFailure &&
                    events.Error?.Contains("invalid_json_schema", StringComparison.Ordinal) == true
                    ? "codex_output_schema_rejected" : "codex_exit_nonzero";
                var exitResult = Failure(outcome, exitCode, started,
                    process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort,
                    processStarted: processStarted, diagnosticStderr: diagnosticStderr,
                    diagnosticStdoutSha256: stdoutDiagnostic?.Sha256,
                    diagnosticStdoutLength: stdoutDiagnostic?.Length,
                    diagnosticStdoutTruncated: stdoutDiagnostic?.Truncated ?? false,
                    diagnosticEventErrorSha256: eventDiagnostic?.Sha256,
                    diagnosticEventErrorLength: eventDiagnostic?.Length,
                    diagnosticEventErrorTruncated: eventDiagnostic?.Truncated ?? false,
                    diagnosticCategory: diagnosticCategory);
                return await PersistFailureAsync(exitResult, stdoutDiagnostic?.Text, eventDiagnostic?.Text);
            }
            if (!events.Completed || events.Failed || !File.Exists(outputPath))
                return await PersistFailureAsync(Failure(ProviderOutcome.Incomplete, "missing_completed_turn_or_final", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, processStarted: processStarted, diagnosticStderr: diagnosticStderr));
            if (events.AttestationError is not null)
                return await PersistFailureAsync(Failure(ProviderOutcome.IsolationViolation, events.AttestationError, started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, processStarted: processStarted, diagnosticStderr: diagnosticStderr));
            if (events.ReportedModel is null)
                return await PersistFailureAsync(Failure(ProviderOutcome.ModelUnavailable, "reported_model_missing", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, processStarted: processStarted, diagnosticStderr: diagnosticStderr));
            if (!string.Equals(events.ReportedModel, requestedModel, StringComparison.Ordinal))
                return await PersistFailureAsync(Failure(ProviderOutcome.ModelUnavailable, "reported_model_mismatch", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, processStarted: processStarted, diagnosticStderr: diagnosticStderr));
            if (events.ReportedEffort is null)
                return await PersistFailureAsync(Failure(ProviderOutcome.EffortUnsupported, "reported_effort_missing", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, processStarted: processStarted, diagnosticStderr: diagnosticStderr));
            if (!string.Equals(events.ReportedEffort, requestedEffort, StringComparison.OrdinalIgnoreCase))
                return await PersistFailureAsync(Failure(ProviderOutcome.EffortUnsupported, "reported_effort_mismatch", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, processStarted: processStarted, diagnosticStderr: diagnosticStderr));
            var info = new FileInfo(outputPath);
            if (info.Length == 0 || info.Length > settings.MaxOutputBytes)
                return await PersistFailureAsync(Failure(ProviderOutcome.Incomplete, "empty_or_oversize_final", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, processStarted: processStarted, diagnosticStderr: diagnosticStderr));
            if (CodexSettings.HasReparsePoint(outputPath))
                return await PersistFailureAsync(Failure(ProviderOutcome.IsolationViolation, "final_output_reparse_point", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, processStarted: processStarted, diagnosticStderr: diagnosticStderr));
            var json = await File.ReadAllTextAsync(outputPath, Encoding.UTF8, timeout.Token);
            try { using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 }); }
            catch (JsonException) { return await PersistFailureAsync(Failure(ProviderOutcome.InvalidSchema, "final_not_json", started, process.ExitCode, events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort, finalJson: json, processStarted: processStarted, diagnosticStderr: diagnosticStderr)); }
            GeneratedImageArtifact? generatedImage = null;
            if (attempt.Stage == "G")
            {
                try { generatedImage = ReadGeneratedImage(outText, events.SessionId, started); }
                catch (NativeImageFailureException e)
                {
                    return await PersistFailureAsync(Failure(e.Outcome, e.Message, started, process.ExitCode,
                        events.SessionId, events.UsageJson, directory, events.ReportedModel, events.ReportedEffort,
                        finalJson: json, processStarted: true, diagnosticStderr: diagnosticStderr),
                        SnapshotDiagnostic(outText)?.Text);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    return await PersistFailureAsync(Failure(ProviderOutcome.IsolationViolation,
                        "generated_image_event_or_artifact_invalid", started, process.ExitCode, events.SessionId, events.UsageJson,
                        directory, events.ReportedModel, events.ReportedEffort, processStarted: true, diagnosticStderr: diagnosticStderr),
                        SnapshotDiagnostic(outText)?.Text);
                }
            }
            var result = new CodexResult(ProviderOutcome.Success, json, process.ExitCode, events.SessionId, null, started,
                DateTimeOffset.UtcNow, GetCliVersion(executable), requestedModel, requestedEffort, events.ReportedModel, events.ReportedEffort,
                events.UsageJson, directory, processStarted, diagnosticStderr, GeneratedImage: generatedImage);
            await TryWriteAttemptOutcomeAsync(directory, result, attempt.Stage == "G" ? SnapshotDiagnostic(outText)?.Text : null, null, CancellationToken.None);
            return result;
        }
        catch (OperationCanceledException)
        {
            return await FailAfterLaunchAsync(cancellationToken.IsCancellationRequested ? ProviderOutcome.TransportUncertain : ProviderOutcome.Timeout,
                cancellationToken.IsCancellationRequested ? "cancelled_after_launch" : "attempt_timeout",
                cancellationToken.IsCancellationRequested ? "cancelled" : "timeout");
        }
        catch (InvalidDataException)
        {
            return await FailAfterLaunchAsync(ProviderOutcome.Incomplete, "event_stream_too_large",
                stdoutCapture.LimitExceeded ? "stdout_limit" : stderrCapture.LimitExceeded ? "stderr_limit" : "stream_limit");
        }
        catch (IOException)
        {
            return await FailAfterLaunchAsync(ProviderOutcome.ProcessFailure, "provider_io_failed", "io");
        }
        catch (UnauthorizedAccessException)
        {
            return await FailAfterLaunchAsync(ProviderOutcome.IsolationViolation, "provider_access_denied", "permission");
        }
    }

    private static bool IsLowerSha256(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void PinJudgementHash(JsonNode schema, string propertyName, string expected)
    {
        if (schema is not JsonObject root || root["properties"] is not JsonObject properties ||
            properties[propertyName] is not JsonObject property ||
            property["type"]?.GetValue<string>() != "string" ||
            property["pattern"]?.GetValue<string>() != "^[0-9a-f]{64}$")
            throw new InvalidDataException("trusted_judgement_schema_invalid");
        if (property["enum"] is { } existing &&
            (existing is not JsonArray allowed || !allowed.Any(value => value?.GetValue<string>() == expected)))
            throw new InvalidDataException("trusted_judgement_schema_conflict");
        property["enum"] = new JsonArray(JsonValue.Create(expected));
    }

    private CodexResult CreateFailure(string requestedModel, string requestedEffort, ProviderOutcome outcome, string code, DateTimeOffset started, int? exitCode = null,
        string? session = null, string? usage = null, string? directory = null,
        string? reportedModel = null, string? reportedEffort = null, string? finalJson = null, bool processStarted = false,
        string? diagnosticStderr = null, string? diagnosticStdoutSha256 = null,
        int? diagnosticStdoutLength = null, bool diagnosticStdoutTruncated = false,
        string? diagnosticEventErrorSha256 = null, int? diagnosticEventErrorLength = null,
        bool diagnosticEventErrorTruncated = false, string? diagnosticCategory = null) =>
        new(outcome, finalJson, exitCode, session, code, started, DateTimeOffset.UtcNow, GetCliVersion(),
            requestedModel, requestedEffort, reportedModel, reportedEffort, usage, directory, processStarted, diagnosticStderr,
            diagnosticStdoutSha256, diagnosticStdoutLength, diagnosticStdoutTruncated,
            diagnosticEventErrorSha256, diagnosticEventErrorLength, diagnosticEventErrorTruncated, diagnosticCategory);

    private async Task<CodexResult> PersistFailureAsync(CodexResult result, string? diagnosticStdout = null, string? diagnosticEventError = null)
    {
        if (result.AttemptDirectory is not null)
            await TryWriteAttemptOutcomeAsync(result.AttemptDirectory, result, diagnosticStdout, diagnosticEventError, CancellationToken.None);
        return result;
    }

    private sealed record DiagnosticSnapshot(string Text, string Sha256, int Length, bool Truncated);

    private static DiagnosticSnapshot? SnapshotDiagnostic(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        const int limit = DiagnosticCharacterLimit;
        var truncated = raw.Length > limit;
        var text = truncated ? raw[..limit] + "\n[diagnostic truncated]" : raw;
        return new(text, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))), text.Length, truncated);
    }

    private static string? BoundDiagnostic(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        const int limit = DiagnosticCharacterLimit;
        return raw.Length <= limit ? raw : raw[..limit] + "\n[stderr truncated]";
    }

    private static string ChooseDiagnosticCategory(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var lower = value.ToLowerInvariant();
            if (lower.Contains("unauthorized") || lower.Contains("authentication") || lower.Contains("login")) return "authentication";
            if (IsQuotaOrCreditExhaustion(lower)) return "quota";
            if (lower.Contains("reasoning") && (lower.Contains("unsupported") || lower.Contains("invalid"))) return "effort";
            if (lower.Contains("model") && (lower.Contains("not found") || lower.Contains("unsupported") || lower.Contains("unavailable"))) return "model";
            if (lower.Contains("json") || lower.Contains("jsonl")) return "jsonl";
            if (lower.Contains("permission") || lower.Contains("access denied")) return "permission";
        }
        return values.Any(value => !string.IsNullOrWhiteSpace(value)) ? "process" : "none";
    }

    private static async Task TryWriteAttemptOutcomeAsync(string directory, CodexResult result,
        string? diagnosticStdout, string? diagnosticEventError, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(directory, "attempt.json");
            if (!CodexSettings.IsPathInside(path, directory, allowEqual: false) ||
                CodexSettings.HasReparsePoint(directory) || !File.Exists(path) || CodexSettings.HasReparsePoint(path)) return;
            bool? stdoutPersisted = null;
            if (diagnosticStdout is not null)
            {
                stdoutPersisted = false;
                var stdoutPath = Path.Combine(directory, "stdout.jsonl");
                try
                {
                    if (CodexSettings.IsPathInside(stdoutPath, directory, allowEqual: false) &&
                        !CodexSettings.HasReparsePoint(stdoutPath))
                    {
                        await File.WriteAllTextAsync(stdoutPath, diagnosticStdout, new UTF8Encoding(false), cancellationToken);
                        stdoutPersisted = true;
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // A diagnostic-file failure must not prevent writing the
                    // primary attempt outcome when attempt.json remains writable.
                }
            }
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
            values["generated_image"] = result.GeneratedImage;
            // This is written only inside the private attempt directory. It is
            // bounded before reaching this method and is never exposed in API
            // DTOs or public doctor evidence. stdout JSONL is kept in the
            // sibling private stdout.jsonl file to avoid duplicating it here.
            values["stderr"] = result.DiagnosticStderr;
            values["stdout_jsonl_sha256"] = result.DiagnosticStdoutSha256;
            values["stdout_jsonl_length"] = result.DiagnosticStdoutLength;
            values["stdout_jsonl_truncated"] = result.DiagnosticStdoutTruncated;
            values["stdout_jsonl_persisted"] = stdoutPersisted;
            values["event_error"] = diagnosticEventError;
            values["event_error_sha256"] = result.DiagnosticEventErrorSha256;
            values["event_error_length"] = result.DiagnosticEventErrorLength;
            values["event_error_truncated"] = result.DiagnosticEventErrorTruncated;
            values["diagnostic_category"] = result.DiagnosticCategory;
            values["ended_at"] = result.EndedAt;
            values["elapsed_ms"] = Math.Max(0, (long)(result.EndedAt - result.StartedAt).TotalMilliseconds);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(values), Encoding.UTF8, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or OperationCanceledException)
        {
            // The provider result remains authoritative. Evidence enrichment
            // must never turn a completed call into a fabricated failure.
        }
    }

    private static async Task WritePromptAsync(StreamWriter writer, string prompt, CancellationToken ct)
    {
        await writer.WriteAsync(prompt.AsMemory(), ct);
        await writer.FlushAsync(ct);
        writer.Close();
    }

    private static async Task ObserveProcessTasksAsync(params Task[] tasks)
    {
        var pending = tasks.ToList();
        while (pending.Count != 0)
        {
            var completed = await Task.WhenAny(pending);
            // Propagate a pipe fault immediately even when the process is alive.
            await completed;
            pending.Remove(completed);
        }
    }

    private static async Task<(bool Exited, int? ExitCode)> StopProcessAsync(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or
            UnauthorizedAccessException or NotSupportedException) { }
        // Killing can fail (for example if permissions changed). Do not turn a
        // known pipe failure into another indefinite wait, or claim completion.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await process.WaitForExitAsync(cleanup.Token);
            return (true, process.ExitCode);
        }
        catch (Exception e) when (e is OperationCanceledException or InvalidOperationException or
            System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        { return (false, null); }
    }

    private static async Task ObserveStoppedTasksAsync(params Task?[] tasks)
    {
        var completion = Task.WhenAll(tasks.OfType<Task>());
        try { await completion.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception e) when (e is OperationCanceledException or InvalidDataException or IOException or UnauthorizedAccessException or
            InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            // All faults are observed even if an OS pipe does not promptly honor
            // cancellation. These tasks cannot authorize another provider call.
            if (!completion.IsCompleted)
                _ = completion.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private sealed class StreamDiagnosticCapture
    {
        private readonly object gate = new();
        private readonly StringBuilder prefix = new();
        private bool limitExceeded;
        public bool LimitExceeded { get { lock (gate) return limitExceeded; } }

        public void Append(char[] buffer, int count, bool exceedsLimit)
        {
            lock (gate)
            {
                limitExceeded |= exceedsLimit;
                // One extra character lets SnapshotDiagnostic preserve the
                // truncation flag without retaining the rest of a large stream.
                var keep = Math.Min(count, DiagnosticCharacterLimit + 1 - prefix.Length);
                if (keep > 0) prefix.Append(buffer, 0, keep);
            }
        }

        public DiagnosticSnapshot? Snapshot()
        {
            lock (gate) return SnapshotDiagnostic(prefix.ToString());
        }
    }

    private static (string? Error, string? SessionId, string? UsageJson, string? ReportedModel, string? ReportedEffort,
        string? AttestationError)
        ReadDiagnosticEvents(DiagnosticSnapshot? snapshot)
    {
        if (snapshot is null) return default;
        var jsonl = snapshot.Truncated ? snapshot.Text[..DiagnosticCharacterLimit] : snapshot.Text;
        // Retain complete events from a prefix; a cut JSONL line is not a native
        // provider error and must not replace an earlier error event.
        var lastNewline = jsonl.LastIndexOf('\n');
        if (lastNewline >= 0) jsonl = jsonl[..(lastNewline + 1)];
        else if (snapshot.Truncated) return default;
        try
        {
            var events = ParseEvents(jsonl);
            return (events.Error, events.SessionId, events.UsageJson, events.ReportedModel, events.ReportedEffort,
                events.AttestationError);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        { return default; }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit,
        StreamDiagnosticCapture diagnostic, CancellationToken ct)
    {
        var buffer = new char[4096];
        var result = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), ct);
            if (read == 0) return result.ToString();
            var exceedsLimit = result.Length + read > limit;
            diagnostic.Append(buffer, read, exceedsLimit);
            if (exceedsLimit) throw new InvalidDataException("stream limit");
            result.Append(buffer, 0, read);
        }
    }

    private static (bool Completed, bool Failed, string? Error, string? SessionId, string? UsageJson, string? ReportedModel, string? ReportedEffort, string? AttestationError) ParseEvents(string jsonl)
    {
        var completed = false; var failed = false; string? error = null; string? session = null; string? usage = null;
        string? reportedModel = null; string? reportedEffort = null;
        string? attestationError = null; var attestationSeen = false; string? previousType = null;
        foreach (var line in jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeElement)) continue;
                var type = typeElement.GetString();
                if (type == "thread.started" && root.TryGetProperty("thread_id", out var id)) session = id.GetString();
                if (type == "provider.attested")
                {
                    if (attestationSeen)
                        attestationError ??= "provider_attestation_duplicate";
                    attestationSeen = true;
                    if (completed)
                        attestationError = "provider_attestation_order";

                    var source = root.TryGetProperty("source", out var sourceElement) &&
                        sourceElement.ValueKind == JsonValueKind.String
                        ? sourceElement.GetString()
                        : null;
                    var model = root.TryGetProperty("model", out var modelElement) &&
                        modelElement.ValueKind == JsonValueKind.String
                        ? modelElement.GetString()
                        : null;
                    var effort = root.TryGetProperty("reasoning_effort", out var effortElement) &&
                        effortElement.ValueKind == JsonValueKind.String
                        ? effortElement.GetString()
                        : null;
                    var responseCountValid = root.TryGetProperty("response_count", out var countElement) &&
                        countElement.ValueKind == JsonValueKind.Number &&
                        countElement.TryGetInt32(out var responseCount) && responseCount >= 1;
                    if (!string.Equals(source, "server_response", StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(effort) || !responseCountValid)
                        attestationError ??= "provider_attestation_invalid";
                    reportedModel = model;
                    reportedEffort = effort;
                }
                if (type == "turn.completed")
                {
                    completed = true;
                    if (root.TryGetProperty("usage", out var value)) usage = value.GetRawText();
                    if (!attestationSeen)
                        attestationError ??= "provider_attestation_missing";
                    else if (!string.Equals(previousType, "provider.attested", StringComparison.Ordinal))
                        attestationError ??= "provider_attestation_order";
                }
                else if (attestationSeen && string.Equals(previousType, "provider.attested", StringComparison.Ordinal))
                {
                    attestationError ??= "provider_attestation_order";
                }
                if (type is "turn.failed" or "error")
                {
                    failed = true;
                    if (root.TryGetProperty("error", out var value)) error = value.ToString();
                    else if (root.TryGetProperty("message", out value)) error = value.ToString();
                }
                previousType = type;
            }
            catch (JsonException) { failed = true; error = "invalid_jsonl_event"; }
        }
        if (error is null && attestationError is not null) error = attestationError;
        return (completed, failed, error, session, usage, reportedModel, reportedEffort, attestationError);
    }

    private static ProviderOutcome Classify(string message)
    {
        var lower = message.ToLowerInvariant();
        if (IsQuotaOrCreditExhaustion(lower)) return ProviderOutcome.Quota;
        if (lower.Contains("unauthorized") || lower.Contains("authentication") || lower.Contains("login")) return ProviderOutcome.Authentication;
        if (lower.Contains("model") && (lower.Contains("not found") || lower.Contains("unsupported"))) return ProviderOutcome.ModelUnavailable;
        if (lower.Contains("reasoning") && (lower.Contains("unsupported") || lower.Contains("invalid"))) return ProviderOutcome.EffortUnsupported;
        if (lower.Contains("refus")) return ProviderOutcome.Refusal;
        return ProviderOutcome.ProcessFailure;
    }

    private static bool IsQuotaOrCreditExhaustion(string lower) =>
        lower.Contains("quota") || lower.Contains("rate limit") ||
        lower.Contains("usage limit") || lower.Contains("usage_limit") ||
        lower.Contains("insufficient credits") || lower.Contains("insufficient_credits") ||
        lower.Contains("credits exhausted") || lower.Contains("credit balance");

    private GeneratedImageArtifact ReadGeneratedImage(string jsonl, string? sessionId, DateTimeOffset started)
    {
        string? path = null; string? callId = null; var count = 0;
        foreach (var line in jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            if (!root.TryGetProperty("type", out var eventType) || eventType.GetString() != "item.completed" ||
                !root.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("type", out var type) || type.GetString() != "image_generation") continue;
            count++;
            if (count != 1 ||
                item.GetProperty("source").GetString() != "native_image_generation" ||
                item.GetProperty("protocol").GetString() != "palimpseste.codex-image/1.0" ||
                item.GetProperty("text_only").ValueKind != JsonValueKind.True ||
                item.GetProperty("one_shot").ValueKind != JsonValueKind.True)
                throw new InvalidDataException("native_image_generation_event_invalid");
            if (item.GetProperty("status").GetString() == "failed")
            {
                var quota = item.TryGetProperty("failure", out var nativeFailure) && nativeFailure.ValueKind == JsonValueKind.Object &&
                    nativeFailure.TryGetProperty("type", out var failureType) && failureType.ValueKind == JsonValueKind.String &&
                    failureType.GetString() == "usageLimitExceeded";
                throw new NativeImageFailureException(quota ? ProviderOutcome.Quota : ProviderOutcome.ProcessFailure,
                    quota ? "native_image_generation_quota" : "native_image_generation_failed");
            }
            if (item.GetProperty("status").GetString() != "completed" ||
                item.TryGetProperty("failure", out var failure) && failure.ValueKind != JsonValueKind.Null)
                throw new InvalidDataException("native_image_generation_event_invalid");
            path = item.GetProperty("saved_path").GetString();
            callId = item.GetProperty("call_id").GetString();
        }
        if (count != 1 || string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException("native_image_generation_event_missing");
        static string Segment(string value) => new(value.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        var expected = Path.Combine(settings.CodexHome, "generated_images", Segment(sessionId), Segment(callId) + ".png");
        if (!CodexSettings.IsPathInside(path, Path.Combine(settings.CodexHome, "generated_images"), allowEqual: false) ||
            !string.Equals(Path.GetFullPath(path), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase) ||
            CodexSettings.HasReparsePoint(path) || !File.Exists(path))
            throw new InvalidDataException("native_image_generation_path_untrusted");
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is < 50 or > VisualReferencePng.MaxBytes || File.GetLastWriteTimeUtc(path) < started.UtcDateTime.AddSeconds(-2))
            throw new InvalidDataException("native_image_generation_size_or_age_invalid");
        var bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        var dimensions = VisualReferencePng.Validate(bytes);
        return new(path, Convert.ToHexStringLower(SHA256.HashData(bytes)), dimensions.Width, dimensions.Height, callId);
    }

    private sealed class NativeImageFailureException(ProviderOutcome outcome, string code) : Exception(code)
    {
        public ProviderOutcome Outcome { get; } = outcome;
    }

    private string GetCliVersion(string? executable = null)
    {
        try { return FileVersionInfo.GetVersionInfo(executable ?? settings.Executable).ProductVersion ?? "non exposé"; }
        catch { return "non exposé"; }
    }
}
