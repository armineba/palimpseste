using System.Text;
using System.Text.Json;
using Npgsql;
using Palimpseste.Core;
using Palimpseste.Storage;

namespace Palimpseste.Api;

public static partial class ApiHandlers
{
    public static async Task<IResult> GrantSpellReviewAccess(HttpContext context, string id, NpgsqlDataSource db, CancellationToken ct)
    {
        var principal = Owner(context);
        if (principal.Role != "creator") return ApiProblem.Result(context, 403, "creator_required", "Rôle créateur requis.");
        if (!Id(id, out var spellId)) return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        var key = Key(context);
        if (key is null) return ApiProblem.Result(context, 400, "idempotency_key_required", "Clé d'idempotence invalide.");
        using var body = await ReadJsonAsync(context.Request, 8192, ct);
        if (body is null || body.RootElement.ValueKind != JsonValueKind.Object || body.RootElement.EnumerateObject().Count() != 2 ||
            !body.RootElement.TryGetProperty("reviewer_principal_id", out var reviewerElement) || reviewerElement.ValueKind != JsonValueKind.String ||
            !Id(reviewerElement.GetString() ?? "", out var reviewerId) ||
            !body.RootElement.TryGetProperty("case_id", out var caseElement) || caseElement.ValueKind != JsonValueKind.String ||
            !CaseId(caseElement.GetString()))
            return ApiProblem.Result(context, 422, "review_access_invalid", "Délégation de revue invalide.");
        if (reviewerId == principal.Id) return ApiProblem.Result(context, 422, "reviewer_must_be_distinct", "Le second créateur doit utiliser un compte distinct.");
        var caseId = caseElement.GetString()!;
        var requestHash = ApiJson.Sha256(Encoding.UTF8.GetBytes(ApiJson.Canonicalize(body.RootElement)));
        const string operation = "spell_review_access";
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await LockKeyAsync(connection, transaction, principal.Id, $"{operation}:{spellId:N}", key, ct);
        var saved = await ExistingAsync(connection, transaction, principal.Id, $"{operation}:{spellId:N}", key, ct);
        if (saved is not null) return ReplayOrConflict(context, saved, requestHash);
        await using (var check = new NpgsqlCommand("SELECT p.role FROM spells s JOIN lab_principals p ON p.id=@reviewer WHERE s.id=@spell AND s.owner_id=@owner", connection, transaction))
        {
            check.Parameters.AddWithValue("spell", spellId);
            check.Parameters.AddWithValue("owner", principal.Id);
            check.Parameters.AddWithValue("reviewer", reviewerId);
            var role = await check.ExecuteScalarAsync(ct) as string;
            if (role is null) return ApiProblem.Result(context, 404, "not_found", "Sort ou créateur introuvable.");
            if (role != "creator") return ApiProblem.Result(context, 422, "reviewer_creator_required", "Le relecteur doit être un créateur.");
        }
        await using (var grant = new NpgsqlCommand("INSERT INTO spell_review_grants(spell_id,reviewer_id,case_id,granted_by) VALUES (@spell,@reviewer,@case,@owner) ON CONFLICT DO NOTHING", connection, transaction))
        {
            grant.Parameters.AddWithValue("spell", spellId);
            grant.Parameters.AddWithValue("reviewer", reviewerId);
            grant.Parameters.AddWithValue("case", caseId);
            grant.Parameters.AddWithValue("owner", principal.Id);
            await grant.ExecuteNonQueryAsync(ct);
        }
        var response = Json(new { spell_id = spellId.ToString("N"), reviewer_principal_id = reviewerId.ToString("N"), case_id = caseId, granted = true });
        await SaveResponseAsync(connection, transaction, principal.Id, $"{operation}:{spellId:N}", key, requestHash, 201, response, ct);
        await transaction.CommitAsync(ct);
        return Results.Content(response, "application/json", Encoding.UTF8, 201);
    }

    public static async Task<IResult> RevokeSpellReviewAccess(HttpContext context, string id, string reviewerId, NpgsqlDataSource db, CancellationToken ct)
    {
        var principal = Owner(context);
        if (principal.Role != "creator") return ApiProblem.Result(context, 403, "creator_required", "Rôle créateur requis.");
        if (!Id(id, out var spellId) || !Id(reviewerId, out var reviewerPrincipalId)) return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        var caseId = context.Request.Query["case_id"].ToString();
        if (!CaseId(caseId)) return ApiProblem.Result(context, 400, "invalid_case_id", "Cas de revue invalide.");
        var key = Key(context);
        if (key is null) return ApiProblem.Result(context, 400, "idempotency_key_required", "Clé d'idempotence invalide.");
        var operation = $"spell_review_access_revoke:{spellId:N}:{reviewerPrincipalId:N}:{caseId}";
        var requestHash = ApiJson.Sha256(Encoding.UTF8.GetBytes(operation));
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await LockKeyAsync(connection, transaction, principal.Id, operation, key, ct);
        var saved = await ExistingAsync(connection, transaction, principal.Id, operation, key, ct);
        if (saved is not null) return ReplayOrConflict(context, saved, requestHash);
        await using (var revoke = new NpgsqlCommand("DELETE FROM spell_review_grants g USING spells s WHERE g.spell_id=s.id AND s.id=@spell AND s.owner_id=@owner AND g.reviewer_id=@reviewer AND g.case_id=@case", connection, transaction))
        {
            revoke.Parameters.AddWithValue("spell", spellId);
            revoke.Parameters.AddWithValue("owner", principal.Id);
            revoke.Parameters.AddWithValue("reviewer", reviewerPrincipalId);
            revoke.Parameters.AddWithValue("case", caseId);
            if (await revoke.ExecuteNonQueryAsync(ct) != 1) return ApiProblem.Result(context, 404, "not_found", "Délégation de revue introuvable.");
        }
        var response = Json(new { spell_id = spellId.ToString("N"), reviewer_principal_id = reviewerPrincipalId.ToString("N"), case_id = caseId, revoked = true });
        await SaveResponseAsync(connection, transaction, principal.Id, operation, key, requestHash, 200, response, ct);
        await transaction.CommitAsync(ct);
        return Results.Content(response, "application/json", Encoding.UTF8, 200);
    }

    public static async Task<IResult> SubmitReview(HttpContext context, NpgsqlDataSource db, CancellationToken ct)
    {
        var principal = Owner(context);
        if (principal.Role != "creator") return ApiProblem.Result(context, 403, "creator_required", "Rôle créateur requis.");
        var key = Key(context);
        if (key is null) return ApiProblem.Result(context, 400, "idempotency_key_required", "Clé d'idempotence invalide.");
        using var body = await ReadJsonAsync(context.Request, 64 * 1024, ct);
        if (body is null || !ValidReview(body.RootElement, out var spellId, out var caseId)) return ApiProblem.Result(context, 422, "review_invalid", "Revue invalide.");
        var reviewerId = body.RootElement.GetProperty("reviewer_id").GetString();
        if (!string.Equals(reviewerId, principal.Id.ToString("N"), StringComparison.Ordinal))
            return ApiProblem.Result(context, 403, "reviewer_identity_mismatch", "Le relecteur déclaré doit être le compte authentifié.");
        var jsonBody = ApiJson.Canonicalize(body.RootElement);
        var hash = ApiJson.Sha256(Encoding.UTF8.GetBytes(jsonBody));
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await LockKeyAsync(connection, transaction, principal.Id, "submit_review", key, ct);
        var saved = await ExistingAsync(connection, transaction, principal.Id, "submit_review", key, ct);
        if (saved is not null) return ReplayOrConflict(context, saved, hash);
        await using (var check = new NpgsqlCommand("""
            SELECT 1 FROM spells s
            WHERE s.id=@id AND (s.owner_id=@owner OR EXISTS (
                SELECT 1 FROM spell_review_grants g
                WHERE g.spell_id=s.id AND g.reviewer_id=@owner AND g.case_id=@case_id))
            """, connection, transaction))
        {
            check.Parameters.AddWithValue("id", spellId);
            check.Parameters.AddWithValue("owner", principal.Id);
            check.Parameters.AddWithValue("case_id", caseId);
            if (await check.ExecuteScalarAsync(ct) is null) return ApiProblem.Result(context, 404, "not_found", "Sort introuvable.");
        }
        var id = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("INSERT INTO reviews(id,owner_id,payload,payload_sha256) VALUES (@id,@owner,@payload::jsonb,@hash)", connection, transaction))
        {
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("owner", principal.Id);
            insert.Parameters.AddWithValue("payload", jsonBody);
            insert.Parameters.AddWithValue("hash", hash);
            await insert.ExecuteNonQueryAsync(ct);
        }
        var response = Json(new { review_id = id.ToString("N") });
        await SaveResponseAsync(connection, transaction, principal.Id, "submit_review", key, hash, 201, response, ct);
        await transaction.CommitAsync(ct);
        return Results.Content(response, "application/json", Encoding.UTF8, 201);
    }

    public static async Task<IResult> AuthoringPlan(HttpContext context, NpgsqlDataSource db, IArtifactStore store, CancellationToken ct)
    {
        var principal = Owner(context);
        if (principal.Role != "creator") return ApiProblem.Result(context, 403, "creator_required", "Rôle créateur requis.");
        var key = Key(context);
        if (key is null) return ApiProblem.Result(context, 400, "idempotency_key_required", "Clé d'idempotence invalide.");
        using var body = await ReadJsonAsync(context.Request, 1024 * 1024, ct);
        if (body is null || body.RootElement.ValueKind != JsonValueKind.Object || body.RootElement.EnumerateObject().Count() != 2 || !body.RootElement.TryGetProperty("description", out var description) || !body.RootElement.TryGetProperty("geometry_artifact_ids", out var geometryIds) || description.ValueKind != JsonValueKind.Object || !description.TryGetProperty("schema_version", out var version) || version.ValueKind != JsonValueKind.String || version.GetString() != "sp.description/1.0" || geometryIds.ValueKind != JsonValueKind.Array || geometryIds.GetArrayLength() is < 1 or > 32)
            return ApiProblem.Result(context, 422, "authoring_invalid", "Description ou géométries invalides.");
        var requestedIds = new HashSet<Guid>();
        foreach (var item in geometryIds.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !PublicIds.TryParseArtifact(item.GetString() ?? "", out var parsed) || !requestedIds.Add(parsed))
                return ApiProblem.Result(context, 422, "geometry_invalid", "IDs géométriques invalides.");
        }
        var canonicalBody = ApiJson.Canonicalize(body.RootElement);
        var requestHash = ApiJson.Sha256(Encoding.UTF8.GetBytes(canonicalBody));
        var descriptionBytes = Encoding.UTF8.GetBytes(ApiJson.Canonicalize(description));
        if (SpellCompiler.ValidateDescriptionJson(descriptionBytes).Count != 0)
            return ApiProblem.Result(context, 422, "description_invalid", "Description non conforme au contrat.");
        var descriptionHash = ApiJson.Sha256(descriptionBytes);
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await LockKeyAsync(connection, transaction, principal.Id, "authoring_plan", key, ct);
        var saved = await ExistingAsync(connection, transaction, principal.Id, "authoring_plan", key, ct);
        if (saved is not null) return ReplayOrConflict(context, saved, requestHash);
        Guid? sourceJob = null;
        await using (var source = new NpgsqlCommand("SELECT ga.job_id FROM geometry_assets ga JOIN jobs j ON j.id=ga.job_id WHERE ga.artifact_id=@artifact AND j.owner_id=@owner", connection, transaction))
        {
            source.Parameters.AddWithValue("artifact", requestedIds.First());
            source.Parameters.AddWithValue("owner", principal.Id);
            sourceJob = (Guid?)await source.ExecuteScalarAsync(ct);
        }
        if (sourceJob is null) return ApiProblem.Result(context, 404, "geometry_not_found", "Banque géométrique introuvable.");
        var allIds = new HashSet<Guid>();
        await using (var fullBank = new NpgsqlCommand("SELECT artifact_id FROM geometry_assets WHERE job_id=@job", connection, transaction))
        {
            fullBank.Parameters.AddWithValue("job", sourceJob.Value);
            await using var reader = await fullBank.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) allIds.Add(reader.GetGuid(0));
        }
        if (!allIds.SetEquals(requestedIds)) return ApiProblem.Result(context, 422, "geometry_incomplete", "Le diagnostic exige tous les JSON géométriques d'une seule banque appartenant au créateur.");
        var descriptionArtifact = await store.PutAsync(descriptionBytes, "json", "application/json", ct);
        await InsertArtifact(connection, transaction, principal.Id, "authored_description", descriptionArtifact, ct);
        var jobId = Guid.NewGuid();
        await using (var job = new NpgsqlCommand("INSERT INTO jobs(id,owner_id,kind,state,resume_stage,message,created_at) VALUES (@id,@owner,'authoring','queued','planning','Description de diagnostic reçue',clock_timestamp())", connection, transaction))
        {
            job.Parameters.AddWithValue("id", jobId);
            job.Parameters.AddWithValue("owner", principal.Id);
            await job.ExecuteNonQueryAsync(ct);
        }
        var orderedIds = requestedIds.OrderBy(x => x).Select(PublicIds.Artifact).ToArray();
        await using (var input = new NpgsqlCommand("INSERT INTO authoring_inputs(job_id,source_job_id,description_artifact_id,description_sha256,geometry_artifact_ids) VALUES (@job,@source,@artifact,@hash,@ids::jsonb)", connection, transaction))
        {
            input.Parameters.AddWithValue("job", jobId);
            input.Parameters.AddWithValue("source", sourceJob.Value);
            input.Parameters.AddWithValue("artifact", descriptionArtifact.Id);
            input.Parameters.AddWithValue("hash", descriptionHash);
            input.Parameters.AddWithValue("ids", Json(orderedIds));
            await input.ExecuteNonQueryAsync(ct);
        }
        var response = Json(Job(jobId, null, "queued", "planning", null, 0, "Description de diagnostic reçue", null, false));
        await SaveResponseAsync(connection, transaction, principal.Id, "authoring_plan", key, requestHash, 202, response, ct);
        await transaction.CommitAsync(ct);
        return Results.Content(response, "application/json", Encoding.UTF8, 202);
    }

    public static async Task<IResult> GetAuthoringPlan(HttpContext context, string id, NpgsqlDataSource db, IArtifactStore store, CancellationToken ct)
    {
        var principal = Owner(context);
        if (principal.Role != "creator") return ApiProblem.Result(context, 403, "creator_required", "Rôle créateur requis.");
        if (!Id(id, out var jobId)) return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT a.storage_key,p.plan_sha256 FROM jobs j JOIN spell_plans p ON p.job_id=j.id JOIN artifacts a ON a.id=p.plan_artifact_id WHERE j.id=@id AND j.owner_id=@owner AND j.kind='authoring' AND p.validation_errors IS NULL ORDER BY p.revision DESC LIMIT 1", connection);
        command.Parameters.AddWithValue("id", jobId);
        command.Parameters.AddWithValue("owner", principal.Id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return ApiProblem.Result(context, 404, "not_found", "Plan validé introuvable.");
        var key = reader.GetString(0); var hash = reader.GetString(1);
        var bytes = await store.ReadAsync(key, ct);
        if (ApiJson.Sha256(bytes) != hash) return ApiProblem.Result(context, 503, "artifact_corrupted", "Plan endommagé.");
        context.Response.Headers["X-Content-SHA256"] = hash;
        return Results.Bytes(bytes, "application/json");
    }

    private static bool ValidReview(JsonElement root, out Guid spellId, out string caseId)
    {
        spellId = default;
        caseId = "";
        try
        {
            if (root.ValueKind != JsonValueKind.Object) return false;
            string[] fields = ["schema_version", "case_id", "spell_id", "reviewer_id", "submitted_by_human", "verdict", "build_commit", "capture_sha256", "description_sha256", "plan_sha256", "compiled_sha256", "judgements", "comment", "evidence_files", "created_at"];
            if (root.EnumerateObject().Count() != fields.Length || fields.Any(x => !root.TryGetProperty(x, out _))) return false;
            if (root.GetProperty("schema_version").GetString() != "sp.review/1.0" || root.GetProperty("submitted_by_human").ValueKind != JsonValueKind.True || !Id(root.GetProperty("spell_id").GetString() ?? "", out spellId)) return false;
            caseId = root.GetProperty("case_id").GetString() ?? "";
            if (!CaseId(caseId)) return false;
            foreach (var field in new[] { "reviewer_id", "build_commit" }) if (root.GetProperty(field).GetString() is not { Length: >= 1 and <= 120 }) return false;
            if (root.GetProperty("verdict").GetString() is not ("accepted" or "changes_requested" or "rejected")) return false;
            foreach (var field in new[] { "capture_sha256", "description_sha256", "plan_sha256", "compiled_sha256" }) if (!Hash(root.GetProperty(field).GetString())) return false;
            var judgements = root.GetProperty("judgements");
            if (judgements.ValueKind != JsonValueKind.Object || judgements.EnumerateObject().Count() != 4) return false;
            foreach (var field in new[] { "drawing_fidelity", "mechanical_coherence", "visual_identity", "presentation" })
                if (judgements.GetProperty(field).GetString() is not ("pass" or "fail" or "uncertain")) return false;
            if (root.GetProperty("comment").GetString() is not { Length: >= 1 and <= 4000 }) return false;
            var evidence = root.GetProperty("evidence_files");
            if (evidence.ValueKind != JsonValueKind.Array || evidence.GetArrayLength() is < 1 or > 32) return false;
            foreach (var item in evidence.EnumerateArray()) if (item.GetString() is not { Length: >= 1 and <= 200 }) return false;
            if (!DateTimeOffset.TryParse(root.GetProperty("created_at").GetString(), out _)) return false;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
    }
}
