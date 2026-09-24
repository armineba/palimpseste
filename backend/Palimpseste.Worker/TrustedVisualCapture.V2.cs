using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Palimpseste.Provider;

namespace Palimpseste.Worker;

public sealed record V2CaptureOutput(byte[] Manifest, IReadOnlyList<RenderedSpellFrame> Frames,
    double MeasuredFps, string BlueprintsSha256);

public sealed partial class TrustedVisualCapture
{
    /// <summary>Fixed offline renderer, sharing V1's protected executable checks and GPU lease.</summary>
    public async Task<V2CaptureOutput> CaptureV2Async(Guid jobId, byte[] packet, byte[] description,
        IReadOnlyDictionary<string, byte[]> artifacts, string mode, CancellationToken ct)
    {
        if (mode is not ("core" or "full")) throw new ArgumentException("visual_capture_v2_mode");
        await RendererGate.WaitAsync(ct);
        try { return await CaptureV2CoreAsync(jobId, packet, description, artifacts, mode, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or
            ArgumentException or KeyNotFoundException or JsonException or FormatException or OverflowException or
            System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            var reason = System.Text.RegularExpressions.Regex.IsMatch(e.Message, "^visual_(capture|renderer)_[a-z0-9_]{1,100}$")
                ? e.Message : e is OperationCanceledException ? "visual_renderer_v2_timeout" : "visual_capture_v2_failed";
            throw new VisualCaptureException(reason, e);
        }
        finally { RendererGate.Release(); }
    }

    private async Task<V2CaptureOutput> CaptureV2CoreAsync(Guid jobId, byte[] packet, byte[] description,
        IReadOnlyDictionary<string, byte[]> artifacts, string mode, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("V2 capture requires Windows");
        var executable = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_RENDERER_EXE") ?? "";
        var manifestHash = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_RENDERER_MANIFEST_SHA256") ?? "";
        using var parsedPacket = JsonDocument.Parse(packet);
        if (parsedPacket.RootElement.GetProperty("versions").GetProperty("min_client").GetString() != "1.8.0")
            throw new InvalidDataException("visual_capture_v2_version");
        ValidateRenderer(executable, manifestHash, "1.8.0");
        var blueprints = parsedPacket.RootElement.GetProperty("plan").GetProperty("nodes").EnumerateArray()
            .Select(node => node.GetProperty("blueprint_v2")).ToArray();
        if (blueprints.Length is < 1 or > 32 || blueprints.Any(b => b.GetProperty("schema_version").GetString() != "sp.blueprint/2.0"))
            throw new InvalidDataException("visual_capture_v2_blueprint");
        var blueprintBytes = JsonSerializer.SerializeToUtf8Bytes(blueprints, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var blueprintHash = Hash(blueprintBytes);
        var directory = Path.Combine(settings.TrustedInputRoot, "visual-captures-v2", jobId.ToString("N") + "-" + mode + "-" + Guid.NewGuid().ToString("N"));
        if (!CodexSettings.IsPathInside(directory, settings.TrustedInputRoot, false) || CodexSettings.HasReparsePoint(directory))
            throw new InvalidDataException("visual_capture_v2_directory");
        foreach (var child in new[] { "artifacts", "output", "temp" }) Directory.CreateDirectory(Path.Combine(directory, child));
        var packetHash = Hash(packet);
        await File.WriteAllBytesAsync(Path.Combine(directory, "spell.json"), packet, ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "spell.json.sha256"), packetHash, Encoding.ASCII, ct);
        await File.WriteAllBytesAsync(Path.Combine(directory, "description.json"), description, ct);
        await File.WriteAllBytesAsync(Path.Combine(directory, "blueprints.json"), blueprintBytes, ct);
        foreach (var (id, bytes) in artifacts)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z][a-z0-9_.-]{0,63}$"))
                throw new InvalidDataException("visual_capture_v2_artifact_id");
            await File.WriteAllBytesAsync(Path.Combine(directory, "artifacts", id), bytes, ct);
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
        foreach (var arg in new[] { "-batchmode", "-force-d3d11", "--palimpseste-v2-validation", directory,
            "--v2-mode", mode, "-logFile", Path.Combine(directory, "output", "renderer.log") }) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("visual_renderer_v2_not_started");
        var stdout = DrainAsync(process.StandardOutput, ct); var stderr = DrainAsync(process.StandardError, ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(180));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None); throw;
        }
        await Task.WhenAll(stdout, stderr);
        if (process.ExitCode != 0) throw new IOException("visual_renderer_v2_failed_" + process.ExitCode);
        var manifest = await ReadBoundedAsync(Path.Combine(directory, "output", "validation.json"), 4_000_000, ct);
        using var parsed = JsonDocument.Parse(manifest);
        var root = parsed.RootElement;
        if (root.GetProperty("schema_version").GetString() != "sp.v2-validation/1.0" ||
            !root.GetProperty("completed").GetBoolean() || root.GetProperty("mode").GetString() != mode ||
            root.GetProperty("spell_sha256").GetString() != packetHash || root.GetProperty("blueprints_sha256").GetString() != blueprintHash ||
            root.GetProperty("provider_calls").GetInt32() != 0)
            throw new InvalidDataException("visual_capture_v2_manifest");
        var frames = new List<RenderedSpellFrame>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            var phase = frame.GetProperty("phase").GetString()!;
            var file = frame.GetProperty("file").GetString()!;
            if (!System.Text.RegularExpressions.Regex.IsMatch(phase, "^[a-z][a-z0-9_]{0,63}$") || file != phase + ".png" || !seen.Add(phase) || seen.Count > 160)
                throw new InvalidDataException("visual_capture_v2_frame_name");
            var path = Path.Combine(directory, "output", file);
            var bytes = await ReadBoundedAsync(path, 8 * 1024 * 1024, ct);
            var size = VisualReferencePng.Validate(bytes);
            if (size.Width != frame.GetProperty("width_px").GetInt32() || size.Height != frame.GetProperty("height_px").GetInt32() ||
                Hash(bytes) != frame.GetProperty("sha256").GetString()) throw new InvalidDataException("visual_capture_v2_frame_hash");
            frames.Add(new(phase, path, Hash(bytes)));
        }
        if (!seen.Contains(mode == "core" ? "core_sheet" : "normal_sheet") || mode == "core" && !seen.Contains("core_gallery") ||
            mode == "full" && (!seen.Contains("game_camera") || !seen.Contains("motion_sheet") ||
            !new[] { "impact_ground", "impact_wall", "impact_target", "impact_oblique", "impact_sheet" }.All(seen.Contains)))
            throw new InvalidDataException("visual_capture_v2_missing_evidence");
        var fps = root.GetProperty("measured_fps").GetDouble();
        if (!double.IsFinite(fps) || fps <= 0 || fps > 10000) throw new InvalidDataException("visual_capture_v2_fps");
        if (Hash(await File.ReadAllBytesAsync(Path.Combine(directory, "spell.json"), ct)) != packetHash ||
            Hash(await File.ReadAllBytesAsync(Path.Combine(directory, "blueprints.json"), ct)) != blueprintHash)
            throw new InvalidDataException("visual_capture_v2_input_modified");
        // Independent critics have a bounded image budget. Lead with complete evidence,
        // never the intentionally empty first appearance frame.
        var preferred = mode == "core" ? new[] { "core_gallery", "core_sheet", "core_sheet_1_0", "core_sheet_0_3", "core_sheet_2_3" }
            : new[] { "normal_sheet", "game_camera", "motion_sheet", "impact_sheet" };
        var ordered = frames.OrderBy(f => Array.IndexOf(preferred, f.Phase) is var priority && priority >= 0 ? priority : preferred.Length).ToArray();
        return new(manifest, ordered, fps, blueprintHash);
    }
}
