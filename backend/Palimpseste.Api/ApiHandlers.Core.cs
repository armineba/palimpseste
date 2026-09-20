using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Palimpseste.Storage;

namespace Palimpseste.Api;

public static partial class ApiHandlers
{
    public static async Task<IResult> Capabilities(HttpContext context, ApiConfig config, ReferenceManager reference, CancellationToken ct)
    {
        try
        {
            var (referenceId, _) = await reference.EnsureAsync("free_canvas_v2", ct);
            using var catalog = JsonDocument.Parse(await File.ReadAllBytesAsync(config.CatalogPath, ct));
            var carriers = catalog.RootElement.GetProperty("carriers").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
            return Results.Json(new
            {
                principal_id = Owner(context).Id.ToString("N"),
                catalog_version = config.CatalogVersion,
                rules_profile = config.RulesProfile,
                layout_version = "free_canvas_v2",
                reference_artifact_id = PublicIds.Artifact(referenceId),
                minimum_client_version = config.MinimumClientVersion,
                carriers,
                inks = new[]
                {
                    new { id = "fire", display_name = "Braise", rgba_hex = "7D271FDC" },
                    new { id = "water", display_name = "Eau", rgba_hex = "1E5C84DC" },
                    new { id = "stone", display_name = "Pierre", rgba_hex = "4A4231EB" },
                    new { id = "air", display_name = "Souffle", rgba_hex = "505467CD" }
                }
            });
        }
        catch (Exception) { return ApiProblem.Result(context, 503, "capabilities_unavailable", "Capacités temporairement indisponibles.", true); }
    }

    public static async Task<IResult> ListParchments(HttpContext context, NpgsqlDataSource db, CancellationToken ct)
    {
        var principal = Owner(context);
        var limitText = context.Request.Query["limit"].ToString();
        var limit = string.IsNullOrEmpty(limitText) ? 50 : int.TryParse(limitText, out var parsedLimit) ? parsedLimit : 0;
        if (limit is < 1 or > 50) return ApiProblem.Result(context, 400, "invalid_limit", "La limite doit être entre 1 et 50.");
        DateTimeOffset? cursorTime = null;
        Guid? cursorId = null;
        var cursor = context.Request.Query["cursor"].ToString();
        if (!string.IsNullOrEmpty(cursor))
        {
            try
            {
                if (cursor.Length > 512) throw new FormatException();
                var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Replace('-', '+').Replace('_', '/') + new string('=', (4 - cursor.Length % 4) % 4))).Split('|');
                if (parts.Length != 2 || !DateTimeOffset.TryParse(parts[0], out var time) || !Id(parts[1], out var id)) throw new FormatException();
                cursorTime = time;
                cursorId = id;
            }
            catch (FormatException) { return ApiProblem.Result(context, 400, "invalid_cursor", "Curseur invalide."); }
        }
        await using var connection = await db.OpenConnectionAsync(ct);
        var sql = "SELECT p.id,p.state,p.layout_version,j.id,s.id,p.created_at FROM parchments p LEFT JOIN jobs j ON j.parchment_id=p.id AND j.kind='production' LEFT JOIN spells s ON s.parchment_id=p.id WHERE p.owner_id=@owner";
        if (cursorTime is not null) sql += " AND (p.created_at,p.id)<(@cursor_time,@cursor_id)";
        sql += " ORDER BY p.created_at DESC,p.id DESC LIMIT @limit";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("owner", principal.Id);
        if (cursorTime is { } t && cursorId is { } i)
        {
            command.Parameters.AddWithValue("cursor_time", t.UtcDateTime);
            command.Parameters.AddWithValue("cursor_id", i);
        }
        command.Parameters.AddWithValue("limit", limit + 1);
        var rows = new List<(Guid Id, string State, string Layout, Guid? JobId, Guid? SpellId, DateTimeOffset Created)>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) rows.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetFieldValue<DateTimeOffset>(5)));
        var hasMore = rows.Count > limit;
        var shown = rows.Take(limit).ToArray();
        string? next = null;
        if (hasMore && shown.Length > 0)
        {
            var last = shown[^1];
            next = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{last.Created:O}|{last.Id:N}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
        return Results.Json(new { items = shown.Select(x => Parchment(x.Id, x.State, x.Layout, x.JobId, x.SpellId)).ToArray(), next_cursor = next });
    }

    public static async Task<IResult> AllocateParchment(HttpContext context, NpgsqlDataSource db, CancellationToken ct)
    {
        var key = Key(context);
        if (key is null) return ApiProblem.Result(context, 400, "idempotency_key_required", "Clé d'idempotence invalide.");
        using var body = await ReadJsonAsync(context.Request, 4096, ct);
        if (body is null || body.RootElement.ValueKind != JsonValueKind.Object || body.RootElement.EnumerateObject().Count() != 1 || !body.RootElement.TryGetProperty("layout_version", out var layout) || layout.ValueKind != JsonValueKind.String || layout.GetString() is not ("three_regions_v1" or "free_canvas_v2"))
            return ApiProblem.Result(context, 400, "invalid_layout", "Layout invalide.");
        var requestedLayout = layout.GetString()!;
        var principal = Owner(context);
        var requestHash = ApiJson.Sha256(Encoding.UTF8.GetBytes(ApiJson.Canonicalize(body.RootElement)));
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await LockKeyAsync(connection, transaction, principal.Id, "allocate_parchment", key, ct);
        var saved = await ExistingAsync(connection, transaction, principal.Id, "allocate_parchment", key, ct);
        if (saved is not null) return ReplayOrConflict(context, saved, requestHash);
        var id = Guid.NewGuid();
        var signature = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        await using (var command = new NpgsqlCommand("INSERT INTO parchments(id,owner_id,state,layout_version,budget_micro_units,signature_seed_hex) VALUES (@id,@owner,'blank',@layout,1000000000,@signature)", connection, transaction))
        {
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("owner", principal.Id);
            command.Parameters.AddWithValue("layout", requestedLayout);
            command.Parameters.AddWithValue("signature", signature);
            await command.ExecuteNonQueryAsync(ct);
        }
        var json = Json(Parchment(id, "blank", requestedLayout, null, null));
        await SaveResponseAsync(connection, transaction, principal.Id, "allocate_parchment", key, requestHash, 201, json, ct);
        await transaction.CommitAsync(ct);
        return Results.Content(json, "application/json", Encoding.UTF8, 201);
    }

    public static async Task<IResult> BeginParchment(HttpContext context, string id, NpgsqlDataSource db, CancellationToken ct)
    {
        if (!Id(id, out var parchmentId)) return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        var key = Key(context);
        if (key is null) return ApiProblem.Result(context, 400, "idempotency_key_required", "Clé d'idempotence invalide.");
        using var body = await ReadJsonAsync(context.Request, 4096, ct);
        if (body is null || body.RootElement.ValueKind != JsonValueKind.Object || body.RootElement.EnumerateObject().Count() != 2 || !body.RootElement.TryGetProperty("first_sequence", out var sequence) || sequence.ValueKind != JsonValueKind.Number || !sequence.TryGetInt64(out var firstSequence) || firstSequence < 1 || !body.RootElement.TryGetProperty("first_block_sha256", out var hashElement) || hashElement.ValueKind != JsonValueKind.String || !Hash(hashElement.GetString()))
            return ApiProblem.Result(context, 400, "invalid_begin", "Premier bloc invalide.");
        var firstHash = hashElement.GetString()!;
        var principal = Owner(context);
        var requestHash = ApiJson.Sha256(Encoding.UTF8.GetBytes(ApiJson.Canonicalize(body.RootElement)));
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await LockKeyAsync(connection, transaction, principal.Id, $"begin:{id}", key, ct);
        var saved = await ExistingAsync(connection, transaction, principal.Id, $"begin:{id}", key, ct);
        if (saved is not null) return ReplayOrConflict(context, saved, requestHash);
        await using (var update = new NpgsqlCommand("UPDATE parchments SET state='writing',first_sequence=@sequence,first_block_sha256=@hash,first_written_at=now(),updated_at=now() WHERE id=@id AND owner_id=@owner AND state='blank' RETURNING id", connection, transaction))
        {
            update.Parameters.AddWithValue("sequence", firstSequence);
            update.Parameters.AddWithValue("hash", firstHash);
            update.Parameters.AddWithValue("id", parchmentId);
            update.Parameters.AddWithValue("owner", principal.Id);
            var changed = await update.ExecuteScalarAsync(ct);
            if (changed is null)
            {
                await using var read = new NpgsqlCommand("SELECT state,first_sequence,first_block_sha256 FROM parchments WHERE id=@id AND owner_id=@owner", connection, transaction);
                read.Parameters.AddWithValue("id", parchmentId);
                read.Parameters.AddWithValue("owner", principal.Id);
                await using var reader = await read.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return ApiProblem.Result(context, 404, "not_found", "Support introuvable.");
                if (reader.IsDBNull(1) || reader.IsDBNull(2) || reader.GetInt64(1) != firstSequence || reader.GetString(2) != firstHash)
                    return ApiProblem.Result(context, 409, "parchment_committed", "Ce support possède déjà une première inscription différente.");
            }
        }
        string parchmentLayout;
        await using (var layoutRead = new NpgsqlCommand("SELECT layout_version FROM parchments WHERE id=@id AND owner_id=@owner", connection, transaction))
        {
            layoutRead.Parameters.AddWithValue("id", parchmentId);
            layoutRead.Parameters.AddWithValue("owner", principal.Id);
            parchmentLayout = (string?)await layoutRead.ExecuteScalarAsync(ct) ?? "three_regions_v1";
        }
        var json = Json(Parchment(parchmentId, "writing", parchmentLayout, null, null));
        await SaveResponseAsync(connection, transaction, principal.Id, $"begin:{id}", key, requestHash, 200, json, ct);
        await transaction.CommitAsync(ct);
        return Results.Content(json, "application/json", Encoding.UTF8, 200);
    }

    public static async Task<IResult> GetJob(HttpContext context, string id, NpgsqlDataSource db, CancellationToken ct)
    {
        if (!Id(id, out var jobId)) return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        var principal = Owner(context);
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT j.parchment_id,j.state,j.resume_stage,j.spell_id,j.attempt_count,j.message,j.error_code,j.retryable,
                   da.id,j.kind,
                   (SELECT count(*) FROM provider_attempts pa WHERE pa.job_id=j.id AND pa.stage='B'),
                   EXISTS(SELECT 1 FROM spell_plans sp WHERE sp.job_id=j.id),
                   EXISTS(SELECT 1 FROM provider_attempts pa WHERE pa.job_id=j.id AND pa.status IN ('running','transport_uncertain')),
                   j.created_at,j.updated_at,
                   greatest(0,floor(extract(epoch from ((CASE WHEN j.state IN ('ready','needs_operator') THEN j.updated_at ELSE clock_timestamp() END)-j.created_at))*1000))::bigint,
                   (SELECT max(pa.started_at) FROM provider_attempts pa WHERE pa.job_id=j.id AND pa.status='running')
            FROM jobs j
            LEFT JOIN interpretations i ON i.job_id=j.id
            LEFT JOIN artifacts da ON da.id=i.description_artifact_id
                AND da.owner_id=j.owner_id
                AND da.kind='description'
                AND da.content_type='application/json'
            WHERE j.id=@id AND j.owner_id=@owner
            """, connection);
        command.Parameters.AddWithValue("id", jobId);
        command.Parameters.AddWithValue("owner", principal.Id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return ApiProblem.Result(context, 404, "not_found", "Tâche introuvable.");
        var state = reader.GetString(1);
        Guid? descriptionArtifactId = reader.IsDBNull(8) ? null : reader.GetGuid(8);
        var retryable = reader.GetBoolean(7) || CanOwnerResumePlanningFailure(
            state, reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(9),
            descriptionArtifactId, reader.GetInt64(10), reader.GetBoolean(11), reader.GetBoolean(12), reader.GetInt32(4));
        return Results.Json(Job(jobId, reader.IsDBNull(0) ? null : reader.GetGuid(0), state, reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetInt32(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), retryable, state == "waiting_retry" ? 10000 : 2000, descriptionArtifactId,
            reader.GetDateTime(13), reader.GetDateTime(14), reader.GetInt64(15), reader.IsDBNull(16) ? null : reader.GetDateTime(16)));
    }

    public static async Task<IResult> ResumeJob(HttpContext context, string id, NpgsqlDataSource db, CancellationToken ct)
    {
        if (!Id(id, out var jobId)) return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        var key = Key(context);
        if (key is null) return ApiProblem.Result(context, 400, "idempotency_key_required", "Clé d'idempotence invalide.");
        var principal = Owner(context);
        var requestHash = ApiJson.Sha256(Encoding.UTF8.GetBytes(id));
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await LockKeyAsync(connection, transaction, principal.Id, $"resume:{id}", key, ct);
        var saved = await ExistingAsync(connection, transaction, principal.Id, $"resume:{id}", key, ct);
        if (saved is not null) return ReplayOrConflict(context, saved, requestHash);
        Guid? parchmentId;
        Guid? spellId;
        Guid? descriptionArtifactId;
        string state, message, kind;
        string? errorCode;
        int attempts;
        long bAttempts;
        bool hasPlan, hasUncertainAttempt;
        bool retryable;
        await using (var read = new NpgsqlCommand("""
            SELECT j.parchment_id,j.state,j.spell_id,j.attempt_count,j.message,j.retryable,
                   (SELECT da.id
                    FROM interpretations i
                    JOIN artifacts da ON da.id=i.description_artifact_id
                    WHERE i.job_id=j.id AND da.owner_id=j.owner_id
                      AND da.kind='description' AND da.content_type='application/json'),
                   j.error_code,j.kind,
                   (SELECT count(*) FROM provider_attempts pa WHERE pa.job_id=j.id AND pa.stage='B'),
                   EXISTS(SELECT 1 FROM spell_plans sp WHERE sp.job_id=j.id),
                   EXISTS(SELECT 1 FROM provider_attempts pa WHERE pa.job_id=j.id AND pa.status IN ('running','transport_uncertain'))
            FROM jobs j WHERE j.id=@id AND j.owner_id=@owner FOR UPDATE
            """, connection, transaction))
        {
            read.Parameters.AddWithValue("id", jobId);
            read.Parameters.AddWithValue("owner", principal.Id);
            await using var reader = await read.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return ApiProblem.Result(context, 404, "not_found", "Tâche introuvable.");
            parchmentId = reader.IsDBNull(0) ? null : reader.GetGuid(0);
            state = reader.GetString(1);
            spellId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
            attempts = reader.GetInt32(3);
            message = reader.GetString(4);
            retryable = reader.GetBoolean(5);
            descriptionArtifactId = reader.IsDBNull(6) ? null : reader.GetGuid(6);
            errorCode = reader.IsDBNull(7) ? null : reader.GetString(7);
            kind = reader.GetString(8);
            bAttempts = reader.GetInt64(9);
            hasPlan = reader.GetBoolean(10);
            hasUncertainAttempt = reader.GetBoolean(11);
        }
        if (state != "ready")
        {
            var ownerPlanningRetry = CanOwnerResumePlanningFailure(state, errorCode, kind,
                descriptionArtifactId, bAttempts, hasPlan, hasUncertainAttempt, attempts);
            if (state is not ("waiting_retry" or "needs_operator") ||
                (!ownerPlanningRetry && (!retryable || attempts >= 10 || (state == "needs_operator" && principal.Role != "creator"))))
                return ApiProblem.Result(context, 409, "resume_not_allowed", "Cette tâche ne peut pas être reprise par ce compte.");
            await using var update = new NpgsqlCommand("UPDATE jobs SET state='queued',resume_stage=CASE WHEN @owner_planning_retry THEN 'B' ELSE resume_stage END,next_attempt_at=NULL,lease_until=NULL,leased_by=NULL,error_code=NULL,retryable=false,message='Reprise demandée',updated_at=now() WHERE id=@id AND owner_id=@owner", connection, transaction);
            update.Parameters.AddWithValue("id", jobId);
            update.Parameters.AddWithValue("owner", principal.Id);
            update.Parameters.AddWithValue("owner_planning_retry", ownerPlanningRetry);
            await update.ExecuteNonQueryAsync(ct);
            state = "queued"; message = "Reprise demandée"; retryable = false;
        }
        var json = Json(Job(jobId, parchmentId, state, null, spellId, attempts, message, null, retryable, descriptionArtifactId: descriptionArtifactId));
        await SaveResponseAsync(connection, transaction, principal.Id, $"resume:{id}", key, requestHash, 202, json, ct);
        await transaction.CommitAsync(ct);
        return Results.Content(json, "application/json", Encoding.UTF8, 202);
    }

    private static bool CanOwnerResumePlanningFailure(string state, string? errorCode, string kind,
        Guid? descriptionArtifactId, long bAttempts, bool hasPlan, bool hasUncertainAttempt, int attempts)
        => state == "needs_operator" && errorCode == "provider_b_processfailure" && kind == "production" &&
           descriptionArtifactId != null && !hasPlan && !hasUncertainAttempt && bAttempts is > 0 and < 3 && attempts < 10;

    public static async Task<IResult> GetSpell(HttpContext context, string id, NpgsqlDataSource db, IArtifactStore store, CancellationToken ct)
    {
        if (!Id(id, out var spellId)) return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT a.storage_key,s.payload_sha256 FROM spells s JOIN artifacts a ON a.id=s.payload_artifact_id WHERE s.id=@id AND s.owner_id=@owner", connection);
        command.Parameters.AddWithValue("id", spellId);
        command.Parameters.AddWithValue("owner", Owner(context).Id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return ApiProblem.Result(context, 404, "not_found", "Sort introuvable.");
        var key = reader.GetString(0); var hash = reader.GetString(1);
        var bytes = await store.ReadAsync(key, ct);
        if (ApiJson.Sha256(bytes) != hash) return ApiProblem.Result(context, 503, "artifact_corrupted", "Paquet endommagé.");
        context.Response.Headers["X-Content-SHA256"] = hash;
        return Results.Bytes(bytes, "application/json");
    }

    public static async Task<IResult> GetArtifact(HttpContext context, string id, NpgsqlDataSource db, IArtifactStore store, CancellationToken ct)
    {
        if (!PublicIds.TryParseArtifact(id, out var artifactId)) return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT storage_key,sha256,content_type FROM artifacts WHERE id=@id AND (owner_id=@owner OR (owner_id IS NULL AND kind IN ('reference_layout','reference_free_canvas')))", connection);
        command.Parameters.AddWithValue("id", artifactId);
        command.Parameters.AddWithValue("owner", Owner(context).Id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return ApiProblem.Result(context, 404, "not_found", "Artefact introuvable.");
        var key = reader.GetString(0); var hash = reader.GetString(1); var contentType = reader.GetString(2);
        var bytes = await store.ReadAsync(key, ct);
        if (ApiJson.Sha256(bytes) != hash) return ApiProblem.Result(context, 503, "artifact_corrupted", "Artefact endommagé.");
        context.Response.Headers["X-Content-SHA256"] = hash;
        return Results.Bytes(bytes, contentType);
    }
}
