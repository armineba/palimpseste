using System.Text;
using System.Text.Json;
using Npgsql;

namespace Palimpseste.Api;

public static partial class ApiHandlers
{
    public static async Task<IResult> SubmitInterpretationFeedback(HttpContext context, string id,
        NpgsqlDataSource db, CancellationToken ct)
    {
        if (!Id(id, out var jobId))
            return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        var key = Key(context);
        if (key is null)
            return ApiProblem.Result(context, 400, "idempotency_key_required", "Clé d'idempotence invalide.");

        using var body = await ReadJsonAsync(context.Request, 8192, ct);
        if (body is null || !ValidInterpretationFeedback(body.RootElement, out var descriptionHash,
                out var verdict, out var correction))
            return ApiProblem.Result(context, 422, "feedback_invalid", "Retour de lecture invalide.");

        var principal = Owner(context);
        var operation = $"interpretation_feedback:{jobId:N}";
        var requestHash = ApiJson.Sha256(Encoding.UTF8.GetBytes(ApiJson.Canonicalize(body.RootElement)));
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await LockKeyAsync(connection, transaction, principal.Id, operation, key, ct);
        var saved = await ExistingAsync(connection, transaction, principal.Id, operation, key, ct);
        if (saved is not null) return ReplayOrConflict(context, saved, requestHash);

        string? actualHash = null;
        string? promptVersion = null;
        await using (var source = new NpgsqlCommand("""
            SELECT j.kind,i.description_sha256,i.prompt_version
            FROM jobs j LEFT JOIN interpretations i ON i.job_id=j.id
            WHERE j.id=@job AND j.owner_id=@owner
            """, connection, transaction))
        {
            source.Parameters.AddWithValue("job", jobId);
            source.Parameters.AddWithValue("owner", principal.Id);
            await using var reader = await source.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.GetString(0) != "production")
                return ApiProblem.Result(context, 404, "not_found", "Tâche introuvable.");
            if (!reader.IsDBNull(1)) actualHash = reader.GetString(1);
            if (!reader.IsDBNull(2)) promptVersion = reader.GetString(2);
        }
        if (actualHash is null || promptVersion is null)
            return ApiProblem.Result(context, 409, "interpretation_pending", "La lecture du dessin n'est pas encore disponible.");
        if (!string.Equals(actualHash, descriptionHash, StringComparison.Ordinal))
            return ApiProblem.Result(context, 409, "description_changed", "La description ne correspond pas à ce travail.");

        var feedbackId = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO interpretation_feedback
                (id,job_id,owner_id,description_sha256,prompt_version,verdict,correction)
            VALUES (@id,@job,@owner,@hash,@prompt,@verdict,@correction)
            ON CONFLICT (job_id,owner_id) DO NOTHING
            RETURNING id
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("id", feedbackId);
            insert.Parameters.AddWithValue("job", jobId);
            insert.Parameters.AddWithValue("owner", principal.Id);
            insert.Parameters.AddWithValue("hash", actualHash);
            insert.Parameters.AddWithValue("prompt", promptVersion);
            insert.Parameters.AddWithValue("verdict", verdict);
            insert.Parameters.AddWithValue("correction", correction);
            if (await insert.ExecuteScalarAsync(ct) is null)
                return ApiProblem.Result(context, 409, "feedback_already_recorded", "Un retour a déjà été enregistré pour cette lecture.");
        }
        var response = Json(new
        {
            feedback_id = feedbackId.ToString("N"), job_id = jobId.ToString("N"),
            description_sha256 = actualHash, recorded = true
        });
        await SaveResponseAsync(connection, transaction, principal.Id, operation, key, requestHash, 201, response, ct);
        await transaction.CommitAsync(ct);
        return Results.Content(response, "application/json", Encoding.UTF8, 201);
    }

    private static bool ValidInterpretationFeedback(JsonElement root, out string descriptionHash,
        out string verdict, out string correction)
    {
        descriptionHash = verdict = correction = "";
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 3 ||
            !root.TryGetProperty("description_sha256", out var hash) || hash.ValueKind != JsonValueKind.String ||
            !Hash(hash.GetString()) ||
            !root.TryGetProperty("verdict", out var status) || status.ValueKind != JsonValueKind.String ||
            status.GetString() is not ("correct" or "incorrect") ||
            !root.TryGetProperty("correction", out var note) || note.ValueKind != JsonValueKind.String)
            return false;
        var value = note.GetString()!;
        if (value.Length is < 1 or > 2000 || string.IsNullOrWhiteSpace(value) ||
            value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
            return false;
        descriptionHash = hash.GetString()!;
        verdict = status.GetString()!;
        correction = value;
        return true;
    }
}
