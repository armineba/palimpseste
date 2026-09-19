using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Palimpseste.Storage;

namespace Palimpseste.Api;

public static partial class ApiHandlers
{
    private static Principal Owner(HttpContext context) => (Principal)context.Items["principal"]!;
    private static bool Id(string value, out Guid id) => Guid.TryParseExact(value, "N", out id);
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool CaseId(string? value) => value is { Length: >= 1 and <= 64 } &&
        System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z][a-z0-9_.-]{0,63}$");

    private static string? Key(HttpContext context)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        return key.Length is >= 16 and <= 120 && key.All(c => c is >= '!' and <= '~') ? key : null;
    }

    private static async Task<JsonDocument?> ReadJsonAsync(HttpRequest request, int maxBytes, CancellationToken ct)
    {
        if (request.ContentLength > maxBytes) return null;
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await request.Body.ReadAsync(buffer, ct);
            if (read == 0) break;
            if (memory.Length + read > maxBytes) return null;
            memory.Write(buffer, 0, read);
        }
        try
        {
            var document = JsonDocument.Parse(memory.ToArray(), new JsonDocumentOptions { MaxDepth = 64 });
            if (!NoDuplicateKeys(document.RootElement)) { document.Dispose(); return null; }
            return document;
        }
        catch (JsonException) { return null; }
    }

    private static bool NoDuplicateKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
                if (!keys.Add(property.Name) || !NoDuplicateKeys(property.Value)) return false;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var element in value.EnumerateArray()) if (!NoDuplicateKeys(element)) return false;
        return true;
    }

    private static async Task LockKeyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid ownerId, string operation, string key, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@value,0))", connection, transaction);
        command.Parameters.AddWithValue("value", $"{ownerId:N}|{operation}|{key}");
        await command.ExecuteNonQueryAsync(ct);
    }

    private sealed record SavedResponse(string RequestHash, int Status, string Body);

    private static async Task<SavedResponse?> ExistingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid ownerId, string operation, string key, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT request_sha256,response_status,response_body::text FROM idempotency_keys WHERE owner_id=@owner AND operation=@operation AND key=@key", connection, transaction);
        command.Parameters.AddWithValue("owner", ownerId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new SavedResponse(reader.GetString(0), reader.GetInt32(1), reader.GetString(2)) : null;
    }

    private static async Task SaveResponseAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid ownerId, string operation, string key, string requestHash, int status, string json, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("INSERT INTO idempotency_keys(owner_id,operation,key,request_sha256,response_status,response_body) VALUES (@owner,@operation,@key,@hash,@status,@body::jsonb)", connection, transaction);
        command.Parameters.AddWithValue("owner", ownerId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("hash", requestHash);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("body", json);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static IResult ReplayOrConflict(HttpContext context, SavedResponse saved, string requestHash)
        => saved.RequestHash == requestHash
            ? Results.Content(saved.Body, "application/json", Encoding.UTF8, saved.Status)
            : ApiProblem.Result(context, 409, "idempotency_conflict", "La clé a déjà été utilisée pour un contenu différent.");

    private static string Json(object body) => JsonSerializer.Serialize(body, ApiJson.Options);

    private static object Parchment(Guid id, string state, string layout, Guid? jobId, Guid? spellId)
        => new { parchment_id = id.ToString("N"), state, layout_version = layout, job_id = jobId?.ToString("N"), spell_id = spellId?.ToString("N") };

    private static object Job(Guid id, Guid? parchmentId, string state, string? resumeStage, Guid? spellId, int attempts, string message, string? errorCode, bool retryable, int pollAfterMs = 2000, Guid? descriptionArtifactId = null)
        => new
        {
            job_id = id.ToString("N"),
            parchment_id = parchmentId?.ToString("N"),
            state,
            resume_stage = resumeStage,
            spell_id = spellId?.ToString("N"),
            description_artifact_id = descriptionArtifactId is { } description ? PublicIds.Artifact(description) : null,
            attempt_count = attempts,
            message,
            error_code = errorCode,
            retryable,
            poll_after_ms = pollAfterMs
        };
}
