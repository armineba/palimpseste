using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Palimpseste.Storage;

/// <summary>Read-only proof that a closed B request failed before any spell output.</summary>
public static class SchemaFailureEvidence
{
    public static bool IsLegacyPlaceholder(byte[] bytes, string expectedSha)
    {
        try
        {
            if (Hash(bytes) != expectedSha) return false;
            using var doc = JsonDocument.Parse(bytes);
            var root = doc.RootElement;
            return !root.TryGetProperty("failure_kind", out _) &&
                String(root, "provider_error") == "codex_exit_nonzero" &&
                root.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array && issues.GetArrayLength() == 0 &&
                root.TryGetProperty("locked_stage_changed", out var changed) && changed.ValueKind == JsonValueKind.False &&
                root.TryGetProperty("methods_invalid", out var methods) && methods.ValueKind is JsonValueKind.True or JsonValueKind.False;
        }
        catch (JsonException) { return false; }
    }

    public static async Task<bool> VerifyAsync(string attemptRoot, string specificationRoot,
        Guid jobId, Guid attemptId, string status, string? outputSha, string? databaseError,
        CancellationToken ct)
    {
        if (status != "invalid" || outputSha is not null ||
            databaseError is not ("v2_blueprint_invalid" or "codex_output_schema_rejected")) return false;
        try
        {
            if (!Path.IsPathFullyQualified(attemptRoot) || !Path.IsPathFullyQualified(specificationRoot)) return false;
            var directory = Path.Combine(attemptRoot, attemptId.ToString("N"));
            var metadataPath = Path.Combine(directory, "attempt.json");
            var failedSchemaPath = Path.Combine(directory, "schema.json");
            if (!TrustedFile(metadataPath, attemptRoot) || !TrustedFile(failedSchemaPath, attemptRoot)) return false;
            var metadataBytes = await BoundedReadAsync(metadataPath, 256_000, ct);
            if (metadataBytes is null) return false;
            using var doc = JsonDocument.Parse(RemoveBom(metadataBytes));
            var root = doc.RootElement;
            if (String(root, "AttemptId") != attemptId.ToString("N") || String(root, "JobId") != jobId.ToString("N") ||
                String(root, "Stage") != "B" || String(root, "outcome") != "ProcessFailure" ||
                String(root, "error_code") is not ("codex_exit_nonzero" or "codex_output_schema_rejected") ||
                !root.TryGetProperty("exit_code", out var exit) || !exit.TryGetInt32(out var exitCode) || exitCode == 0 ||
                !root.TryGetProperty("event_error_truncated", out var truncated) || truncated.ValueKind != JsonValueKind.False)
                return false;
            var diagnostic = String(root, "event_error");
            if (diagnostic is null || Hash(Encoding.UTF8.GetBytes(diagnostic)) != String(root, "event_error_sha256") ||
                !IsRequestSchemaError(diagnostic)) return false;

            // Require an actual deployed schema change. Repeating the identical rejected
            // request cannot recover anything and must never be offered as an owner retry.
            var failedBytes = await BoundedReadAsync(failedSchemaPath, 2_000_000, ct);
            if (failedBytes is null || Hash(failedBytes) != String(root, "schema_sha256")) return false;
            var failed = JsonNode.Parse(failedBytes);
            if (failed?["properties"] is not JsonObject properties) return false;
            var file = properties.ContainsKey("method_design")
                ? "model-b-unity-god-v2.output-schema.json" : "model-b-v2.output-schema.json";
            var deployedPath = Path.Combine(specificationRoot, "contracts", "codex", file);
            if (!TrustedFile(deployedPath, specificationRoot)) return false;
            var deployedBytes = await BoundedReadAsync(deployedPath, 2_000_000, ct);
            if (deployedBytes is null) return false;
            var deployed = JsonNode.Parse(deployedBytes);
            if (deployed is null || JsonNode.DeepEquals(failed, deployed)) return false;
            var compact = Encoding.UTF8.GetBytes(deployed.ToJsonString(new JsonSerializerOptions
                { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            return Hash(compact) != String(root, "schema_sha256");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or
            ArgumentException or InvalidOperationException or NotSupportedException)
        { return false; }
    }

    private static bool IsRequestSchemaError(string text)
    {
        // Native CLI stores either the server JSON or {"message":"<server JSON>"}.
        // Parse both; an occurrence of the words in arbitrary error text is not proof.
        using var outer = JsonDocument.Parse(text);
        var root = outer.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
        {
            using var inner = JsonDocument.Parse(message.GetString()!);
            return IsRequestSchemaError(inner.RootElement);
        }
        return IsRequestSchemaError(root);
    }

    private static bool IsRequestSchemaError(JsonElement root) => root.ValueKind == JsonValueKind.Object &&
        String(root, "type") == "error" && root.TryGetProperty("status", out var status) &&
        status.TryGetInt32(out var code) && code == 400 && root.TryGetProperty("error", out var error) &&
        error.ValueKind == JsonValueKind.Object && String(error, "type") == "invalid_request_error" &&
        String(error, "code") == "invalid_json_schema" && String(error, "param") == "text.format.schema";

    private static string? String(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static ReadOnlyMemory<byte> RemoveBom(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? bytes.AsMemory(3) : bytes;

    private static async Task<byte[]?> BoundedReadAsync(string path, int maximum, CancellationToken ct)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length <= 0 || file.Length > maximum) return null;
        var bytes = new byte[checked((int)file.Length)];
        await file.ReadExactlyAsync(bytes, ct);
        return bytes;
    }

    private static bool TrustedFile(string path, string root)
    {
        var boundary = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return false;
        // Reject junctions/symlinks anywhere in the full chain, including the trusted root.
        for (var current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }
}
