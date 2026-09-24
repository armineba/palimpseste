using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Palimpseste.Provider;

namespace Palimpseste.Worker;

public sealed record VisualCaptureOutput(byte[] Manifest, IReadOnlyList<RenderedSpellFrame> Frames, double MeasuredFps);

public sealed class VisualCaptureException(string reason, Exception inner) : Exception(reason, inner)
{
    public string Reason { get; } = reason;
}

/// <summary>A fixed, prebuilt renderer. This is application code, never a model tool or a software build.</summary>
public sealed class TrustedVisualCapture(CodexSettings settings)
{
    // Concurrent jobs share this worker's GPU. Capturing one at a time keeps
    // another renderer from distorting the measured FPS and visual review.
    private static readonly SemaphoreSlim RendererGate = new(1, 1);

    public async Task<VisualCaptureOutput> CaptureAsync(Guid jobId, byte[] packet, byte[] description,
        IReadOnlyDictionary<string, byte[]> artifacts, CancellationToken ct)
    {
        var acquired = false;
        try
        {
            // Queue time is cancellable and excluded from the renderer's own timeout.
            await RendererGate.WaitAsync(ct);
            acquired = true;
            return await CaptureCoreAsync(jobId, packet, description, artifacts, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or
            KeyNotFoundException or JsonException or FormatException or OverflowException or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            // Only application-defined codes reach logs; arbitrary exception text can contain private paths.
            var reason = System.Text.RegularExpressions.Regex.IsMatch(e.Message,
                "^visual_(capture|renderer)_[a-z0-9_]{1,100}$") ? e.Message :
                e is OperationCanceledException ? "visual_renderer_timeout" : "visual_capture_failed";
            throw new VisualCaptureException(reason, e);
        }
        finally { if (acquired) RendererGate.Release(); }
    }

    private async Task<VisualCaptureOutput> CaptureCoreAsync(Guid jobId, byte[] packet, byte[] description,
        IReadOnlyDictionary<string, byte[]> artifacts, CancellationToken ct)
    {
        var executable = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_RENDERER_EXE") ?? "";
        var manifestHash = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_RENDERER_MANIFEST_SHA256") ?? "";
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Unity capture requires the configured Windows renderer");
        using var compiledPacket = JsonDocument.Parse(packet);
        var minimumClient = compiledPacket.RootElement.GetProperty("versions").GetProperty("min_client").GetString();
        ValidateRenderer(executable, manifestHash, minimumClient);
        var captureRoot = Path.Combine(settings.TrustedInputRoot, "visual-captures");
        var directory = Path.Combine(captureRoot, jobId.ToString("N") + "-" + Guid.NewGuid().ToString("N"));
        if (!CodexSettings.IsPathInside(directory, settings.TrustedInputRoot, false) || CodexSettings.HasReparsePoint(directory))
            throw new InvalidDataException("visual_capture_directory");
        Directory.CreateDirectory(Path.Combine(directory, "artifacts"));
        Directory.CreateDirectory(Path.Combine(directory, "output"));
        Directory.CreateDirectory(Path.Combine(directory, "temp"));
        var packetHash = Hash(packet);
        await File.WriteAllBytesAsync(Path.Combine(directory, "spell.json"), packet, ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "spell.json.sha256"), packetHash, Encoding.ASCII, ct);
        await File.WriteAllBytesAsync(Path.Combine(directory, "description.json"), description, ct);
        foreach (var (id, data) in artifacts)
        {
            if (id.Length is < 1 or > 64 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '.' && c != '-') || id is "." or "..")
                throw new InvalidDataException("visual_capture_artifact_id");
            await File.WriteAllBytesAsync(Path.Combine(directory, "artifacts", id), data, ct);
        }
        var start = new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(executable)!, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.Environment.Clear();
        foreach (var key in new[] { "SystemRoot", "WINDIR", "USERPROFILE", "APPDATA", "LOCALAPPDATA" })
            if (Environment.GetEnvironmentVariable(key) is { } value) start.Environment[key] = value;
        start.Environment["TEMP"] = Path.Combine(directory, "temp"); start.Environment["TMP"] = start.Environment["TEMP"];
        start.Environment["PATH"] = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows", "System32");
        foreach (var arg in new[] { "-batchmode", "-force-d3d11", "--palimpseste-visual-capture", directory,
                     "-logFile", Path.Combine(directory, "output", "renderer.log") }) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("visual_renderer_not_started");
        // Drain both streams; stdout is not used as proof of rendering.
        var stdout = DrainAsync(process.StandardOutput, ct); var stderr = DrainAsync(process.StandardError, ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            // Kill requests termination; wait for it before releasing the GPU gate.
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        await Task.WhenAll(stdout, stderr);
        if (process.ExitCode != 0) throw new IOException("visual_renderer_failed_" + process.ExitCode);
        var manifest = await ReadBoundedAsync(Path.Combine(directory, "output", "capture.json"), 64_000, ct);
        using var parsed = JsonDocument.Parse(manifest);
        var root = parsed.RootElement;
        if (root.GetProperty("schema_version").GetString() != "sp.visual-capture/1.0" ||
            root.GetProperty("spell_sha256").GetString() != packetHash || !root.GetProperty("completed").GetBoolean() ||
            !root.GetProperty("presentation_only").GetBoolean()) throw new InvalidDataException("visual_capture_manifest");
        var fps = root.GetProperty("measured_fps").GetDouble();
        if (!double.IsFinite(fps) || fps <= 0 || fps > 10000) throw new InvalidDataException("visual_capture_fps");
        var reference = compiledPacket.RootElement.GetProperty("visual_reference");
        var sheetRequired = reference.TryGetProperty("animation_sheet", out var sheet) && sheet.ValueKind == JsonValueKind.Object;
        var phaseTimings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (sheetRequired)
        {
            if (root.GetProperty("visual_reference_sha256").GetString() != reference.GetProperty("sha256").GetString() ||
                !root.TryGetProperty("animation_sheet", out var capturedSheet) || capturedSheet.ValueKind != JsonValueKind.Object ||
                capturedSheet.GetProperty("layout_version").GetString() != sheet.GetProperty("layout_version").GetString() ||
                capturedSheet.GetProperty("rows").GetInt32() != 3 || capturedSheet.GetProperty("columns").GetInt32() != 7 ||
                capturedSheet.GetProperty("ending_basis").GetString() != sheet.GetProperty("ending_basis").GetString() ||
                root.GetProperty("gameplay_executed").GetBoolean() || root.GetProperty("provider_calls").GetInt32() != 0)
                throw new InvalidDataException("visual_capture_sheet_provenance");
            phaseTimings = await ValidatePhaseSamplesAsync(root, compiledPacket.RootElement, directory, ct);
        }
        var behaviorRequired = compiledPacket.RootElement.GetProperty("plan").GetProperty("nodes").EnumerateArray()
            .Any(n => n.TryGetProperty("behavior", out var b) && b.ValueKind == JsonValueKind.Object);
        if (behaviorRequired && !sheetRequired)
        {
            if (!root.TryGetProperty("active_samples", out var samples) || samples.ValueKind != JsonValueKind.Array ||
                samples.GetArrayLength() != 4) throw new InvalidDataException("visual_capture_temporal_samples");
            double previousTime = -1;
            double[] requested = [0, .073, .191, .347];
            var index = 0;
            foreach (var sample in samples.EnumerateArray())
            {
                var name = "active_" + index + ".png";
                var observedTime = sample.GetProperty("time_seconds").GetDouble();
                var requestTime = sample.GetProperty("requested_time_seconds").GetDouble();
                if (sample.GetProperty("file").GetString() != name || !double.IsFinite(observedTime) ||
                    observedTime <= previousTime || observedTime > 120 || !double.IsFinite(requestTime) ||
                    Math.Abs(requestTime - requested[index]) > .00001)
                    throw new InvalidDataException("visual_capture_temporal_order");
                var data = await ReadBoundedAsync(Path.Combine(directory, "output", name), 8 * 1024 * 1024, ct);
                var size = VisualReferencePng.Validate(data);
                if (size.Width != 1024 || size.Height != 1024 || Hash(data) != sample.GetProperty("sha256").GetString())
                    throw new InvalidDataException("visual_capture_temporal_hash");
                previousTime = observedTime; index++;
            }
        }
        var frames = new List<RenderedSpellFrame>();
        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            var phase = frame.GetProperty("phase").GetString()!;
            var file = frame.GetProperty("file").GetString();
            if (phase is not ("appearance" or "active" or "contact" or "expiration") || file != phase + ".png" ||
                frames.Any(f => f.Phase == phase)) throw new InvalidDataException("visual_capture_frame");
            var path = Path.Combine(directory, "output", file);
            var data = await ReadBoundedAsync(path, 8 * 1024 * 1024, ct);
            var dimensions = sheetRequired ? VisualReferencePng.ValidateAnimationStrip(data) : VisualReferencePng.Validate(data);
            var hash = Hash(data);
            if (dimensions.Width != (sheetRequired ? 2048 : 1024) || dimensions.Height != (sheetRequired ? 320 : 1024) ||
                frame.GetProperty("width_px").GetInt32() != dimensions.Width || frame.GetProperty("height_px").GetInt32() != dimensions.Height ||
                hash != frame.GetProperty("sha256").GetString())
                throw new InvalidDataException("visual_capture_frame_hash");
            frames.Add(new(phase, path, hash, sheetRequired ? phaseTimings[phase] :
                phase == "active" && root.TryGetProperty("active_samples", out var activeSamples)
                    ? activeSamples.GetRawText() : null, IsAnimationStrip: sheetRequired));
        }
        if (frames.Count != 4) throw new InvalidDataException("visual_capture_phases_incomplete");
        // Re-read the immutable packet after rendering; the helper is not an editor.
        if (Hash(await File.ReadAllBytesAsync(Path.Combine(directory, "spell.json"), ct)) != packetHash)
            throw new InvalidDataException("visual_capture_packet_modified");
        return new(manifest, frames, fps);
    }

    private static async Task<Dictionary<string, string>> ValidatePhaseSamplesAsync(JsonElement manifest,
        JsonElement packet, string directory, CancellationToken ct)
    {
        const double requestTolerance = .00001;
        // Unity shortens captureDeltaTime substeps to each requested instant. Its float
        // clock stops within 5 microseconds; allow 1 ms for accumulated float precision,
        // still much shorter than the minimum 13 ms spacing of a 100 ms phase.
        const double observedTolerance = .001;
        if (!manifest.TryGetProperty("phase_samples", out var phases) || phases.ValueKind != JsonValueKind.Array ||
            phases.GetArrayLength() != 4 || manifest.GetProperty("sample_clock").GetString() !=
            "Unity presentation simulation clock; Time.captureDeltaTime substeps at most 1/120s, shortened to requested instants; GPU/PNG cost excluded from animation time; not physical elapsed time")
            throw new InvalidDataException("visual_capture_phase_samples");
        var nodes = packet.GetProperty("plan").GetProperty("nodes").EnumerateArray().ToArray();
        var initial = nodes.Where(n => n.GetProperty("activation").GetProperty("parent_id").ValueKind == JsonValueKind.Null ||
            n.GetProperty("activation").GetProperty("event").GetString() is "spawn" or "tick").ToArray();
        if (initial.Length == 0) throw new InvalidDataException("visual_capture_initial_nodes");
        var expectedIntro = initial.Max(n => LifecycleNumber(n, "intro", "duration_ms")) / 1000d;
        var expectedActive = initial.Max(n => LifecycleNumber(n, "active", "period_ms")) / 1000d;
        var expectedContact = ExpectedEndingDuration(nodes, initial, "contact");
        var expectedExpiration = ExpectedEndingDuration(nodes, initial, "expiration");
        int[] normalized = [0, 130, 290, 470, 640, 820, 1000];
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in phases.EnumerateArray())
        {
            var phase = row.GetProperty("phase").GetString()!;
            if (phase is not ("appearance" or "active" or "contact" or "expiration") || result.ContainsKey(phase))
                throw new InvalidDataException("visual_capture_phase_identity");
            var duration = row.GetProperty("duration_seconds").GetDouble();
            // Initial and active phase durations come directly from the packet. Terminal
            // phases can include a just-created child's entrance followed by its ending.
            if (!double.IsFinite(duration) || duration < .1 - requestTolerance || duration > 6 + requestTolerance ||
                phase == "appearance" && Math.Abs(duration - expectedIntro) > requestTolerance ||
                phase == "active" && Math.Abs(duration - expectedActive) > requestTolerance ||
                phase == "contact" && Math.Abs(duration - expectedContact) > observedTolerance ||
                phase == "expiration" && Math.Abs(duration - expectedExpiration) > observedTolerance)
                throw new InvalidDataException("visual_capture_phase_duration");
            var samples = row.GetProperty("samples");
            if (samples.ValueKind != JsonValueKind.Array || samples.GetArrayLength() != 7)
                throw new InvalidDataException("visual_capture_phase_sample_count");
            var previous = -1d;
            var index = 0;
            foreach (var sample in samples.EnumerateArray())
            {
                var file = phase + "_" + index + ".png";
                var requested = sample.GetProperty("requested_time_seconds").GetDouble();
                var observed = sample.GetProperty("time_seconds").GetDouble();
                if (sample.GetProperty("index").GetInt32() != index ||
                    sample.GetProperty("normalized_milli").GetInt32() != normalized[index] ||
                    sample.GetProperty("file").GetString() != file || !double.IsFinite(requested) || !double.IsFinite(observed) ||
                    Math.Abs(requested - duration * normalized[index] / 1000d) > requestTolerance ||
                    observed < 0 || observed <= previous || Math.Abs(observed - requested) > observedTolerance)
                    throw new InvalidDataException("visual_capture_phase_sample_time");
                var data = await ReadBoundedAsync(Path.Combine(directory, "output", file), VisualReferencePng.MaxBytes, ct);
                var dimensions = VisualReferencePng.Validate(data);
                if (dimensions.Width != 1024 || dimensions.Height != 1024 ||
                    Hash(data) != sample.GetProperty("sha256").GetString())
                    throw new InvalidDataException("visual_capture_phase_sample_hash");
                previous = observed;
                index++;
            }
            result.Add(phase, row.GetRawText());
        }
        return result;
    }

    private static int LifecycleNumber(JsonElement node, string phase, string number) =>
        node.GetProperty("appearance").GetProperty("lifecycle").GetProperty(phase).GetProperty(number).GetInt32();

    private static double ExpectedEndingDuration(JsonElement[] nodes, JsonElement[] initial, string phase)
    {
        // Mirrors the fixed renderer's BeginSheetEnding: retire existing subjects;
        // children of this terminal event finish their entrance and then retire.
        var duration = Math.Max(.1, initial.Max(n => Math.Clamp(LifecycleNumber(n, phase, "duration_ms") / 1000d, .1, 3)));
        foreach (var node in nodes)
        {
            var activation = node.GetProperty("activation");
            if (activation.GetProperty("parent_id").ValueKind == JsonValueKind.Null) continue;
            var trigger = activation.GetProperty("event").GetString();
            if (!(phase == "contact" ? trigger is "hit" or "block" or "trigger" : trigger == "expire")) continue;
            duration = Math.Max(duration, Math.Clamp(LifecycleNumber(node, "intro", "duration_ms") / 1000d, .1, 3) +
                Math.Clamp(LifecycleNumber(node, phase, "duration_ms") / 1000d, .1, 3));
        }
        return duration;
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[8192];
        while (await reader.ReadAsync(buffer.AsMemory(), ct) != 0) { }
    }

    private static async Task<byte[]> ReadBoundedAsync(string path, long maximum, CancellationToken ct)
    {
        if (CodexSettings.HasReparsePoint(path)) throw new InvalidDataException("visual_capture_reparse");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length < 1 || stream.Length > maximum) throw new InvalidDataException("visual_capture_file_size");
        var bytes = new byte[checked((int)stream.Length)]; await stream.ReadExactlyAsync(bytes, ct); return bytes;
    }

    [SupportedOSPlatform("windows")]
    private void ValidateRenderer(string executable, string expectedManifestHash, string? minimumClient)
    {
        if (!Path.IsPathFullyQualified(executable) || Path.GetFileName(executable) != "Palimpseste.exe" ||
            expectedManifestHash.Length != 64 || !File.Exists(executable) || CodexSettings.HasReparsePoint(executable))
            throw new InvalidDataException("visual_renderer_not_configured");
        var directory = Path.GetDirectoryName(executable)!;
        if (CodexSettings.IsPathInside(directory, settings.TrustedInputRoot, true) ||
            CodexSettings.IsPathInside(directory, settings.AttemptRoot, true) ||
            CodexSettings.IsPathInside(directory, settings.DevelopmentRoot, true))
            throw new InvalidDataException("visual_renderer_untrusted_directory");
        var manifestPath = Path.Combine(directory, "renderer-manifest.json");
        var bytes = File.ReadAllBytes(manifestPath);
        if (bytes.Length > 128_000 || Hash(bytes) != expectedManifestHash) throw new InvalidDataException("visual_renderer_manifest_hash");
        var serviceSid = ((SecurityIdentifier)new NTAccount(Environment.MachineName, settings.ExpectedServiceUser)
            .Translate(typeof(SecurityIdentifier))).Value;
        var forbidden = new HashSet<string> { serviceSid, "S-1-1-0", "S-1-5-11", "S-1-5-32-545" };
        var entries = new DirectoryInfo(directory).EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            .Prepend(new DirectoryInfo(directory)).ToArray();
        foreach (var item in entries)
        {
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("visual_renderer_reparse");
            FileSystemSecurity acl = item is DirectoryInfo folder ? folder.GetAccessControl() : ((FileInfo)item).GetAccessControl();
            if (acl.GetOwner(typeof(SecurityIdentifier)) is not { } owner || forbidden.Contains(owner.Value)) throw new InvalidDataException("visual_renderer_owner");
            foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                if (rule.AccessControlType == AccessControlType.Allow && forbidden.Contains(rule.IdentityReference.Value) &&
                    (rule.FileSystemRights & (FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership)) != 0)
                    throw new InvalidDataException("visual_renderer_writable_by_worker");
        }
        using var manifest = JsonDocument.Parse(bytes);
        var rendererVersion = manifest.RootElement.GetProperty("version").GetString();
        if (rendererVersion is not ("1.6.0" or "1.7.0") ||
            !System.Version.TryParse(minimumClient, out var requiredVersion) ||
            System.Version.Parse(rendererVersion) < requiredVersion)
            throw new InvalidDataException("visual_renderer_version");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            var name = file.GetProperty("file").GetString()!;
            var path = Path.GetFullPath(Path.Combine(directory, name));
            if (!CodexSettings.IsPathInside(path, directory, false) || !names.Add(name.Replace('\\', '/')) ||
                !File.Exists(path) || CodexSettings.ComputeExecutableSha256(path) != file.GetProperty("sha256").GetString())
                throw new InvalidDataException("visual_renderer_file_hash");
        }
        if (!names.Contains("Palimpseste.exe") || !names.Contains("GameAssembly.dll") || !names.Contains("UnityPlayer.dll") ||
            entries.OfType<FileInfo>().Any(f => f.Name != "renderer-manifest.json" && !names.Contains(Path.GetRelativePath(directory, f.FullName).Replace('\\', '/'))))
            throw new InvalidDataException("visual_renderer_manifest_incomplete");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
