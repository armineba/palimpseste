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

/// <summary>A fixed, prebuilt renderer. This is application code, never a model tool or a software build.</summary>
public sealed class TrustedVisualCapture(CodexSettings settings)
{
    public async Task<VisualCaptureOutput> CaptureAsync(Guid jobId, byte[] packet, byte[] description,
        IReadOnlyDictionary<string, byte[]> artifacts, CancellationToken ct)
    {
        var executable = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_RENDERER_EXE") ?? "";
        var manifestHash = Environment.GetEnvironmentVariable("PALIMPSESTE_VISUAL_RENDERER_MANIFEST_SHA256") ?? "";
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Unity capture requires the configured Windows renderer");
        ValidateRenderer(executable, manifestHash);
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
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
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
        var frames = new List<RenderedSpellFrame>();
        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            var phase = frame.GetProperty("phase").GetString()!;
            var file = frame.GetProperty("file").GetString();
            if (phase is not ("appearance" or "active" or "contact" or "expiration") || file != phase + ".png" ||
                frames.Any(f => f.Phase == phase)) throw new InvalidDataException("visual_capture_frame");
            var path = Path.Combine(directory, "output", file);
            var data = await ReadBoundedAsync(path, 8 * 1024 * 1024, ct);
            var dimensions = VisualReferencePng.Validate(data);
            var hash = Hash(data);
            if (dimensions.Width != 1024 || dimensions.Height != 1024 || hash != frame.GetProperty("sha256").GetString())
                throw new InvalidDataException("visual_capture_frame_hash");
            frames.Add(new(phase, path, hash));
        }
        if (frames.Count != 4) throw new InvalidDataException("visual_capture_phases_incomplete");
        // Re-read the immutable packet after rendering; the helper is not an editor.
        if (Hash(await File.ReadAllBytesAsync(Path.Combine(directory, "spell.json"), ct)) != packetHash)
            throw new InvalidDataException("visual_capture_packet_modified");
        return new(manifest, frames, fps);
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
    private void ValidateRenderer(string executable, string expectedManifestHash)
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
        if (manifest.RootElement.GetProperty("version").GetString() != "1.4.0") throw new InvalidDataException("visual_renderer_version");
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
