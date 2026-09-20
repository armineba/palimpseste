using System.Text;
using System.Text.Json;
using Npgsql;
using Palimpseste.Storage;

namespace Palimpseste.Api;

public static partial class ApiHandlers
{
    public static async Task<IResult> CommitCapture(HttpContext context, string id, NpgsqlDataSource db, IArtifactStore store, ReferenceManager reference, ApiConfig config, CancellationToken ct)
    {
        if (!Id(id, out var parchmentId)) return ApiProblem.Result(context, 400, "invalid_id", "Identifiant invalide.");
        var key = Key(context);
        if (key is null) return ApiProblem.Result(context, 400, "idempotency_key_required", "Clé d'idempotence invalide.");
        if (!context.Request.HasFormContentType) return ApiProblem.Result(context, 400, "multipart_required", "Capture multipart requise.");
        IFormCollection form;
        try { form = await context.Request.ReadFormAsync(ct); }
        catch (InvalidDataException) { return ApiProblem.Result(context, 413, "upload_too_large", "Téléversement trop volumineux."); }
        var drawingFile = form.Files.GetFile("drawing");
        var inkFile = form.Files.GetFile("ink");
        var journalFile = form.Files.GetFile("journal");
        var captureFile = form.Files.GetFile("capture");
        if (drawingFile is null || inkFile is null || journalFile is null || (captureFile is null && !form.ContainsKey("capture")) || form.Files.Count != (captureFile is null ? 3 : 4))
            return ApiProblem.Result(context, 400, "multipart_invalid", "Parties de capture incomplètes ou supplémentaires.");
        if (drawingFile.Length is < 1 or > 8_388_608 || inkFile.Length is < 1 or > 8_388_608 || journalFile.Length is < 1 or > 8_388_608 || (captureFile?.Length ?? 0) > 65536)
            return ApiProblem.Result(context, 413, "upload_too_large", "Fichier de capture trop volumineux.");
        if (drawingFile.ContentType != "image/png" || inkFile.ContentType != "image/png" || journalFile.ContentType != "application/gzip")
            return ApiProblem.Result(context, 400, "invalid_media_type", "Types de fichiers invalides.");
        byte[] drawing, ink, journal, captureBytes;
        try
        {
            drawing = await FileBytes(drawingFile, 8_388_608, ct);
            ink = await FileBytes(inkFile, 8_388_608, ct);
            journal = await FileBytes(journalFile, 8_388_608, ct);
            captureBytes = captureFile is null ? Encoding.UTF8.GetBytes(form["capture"].ToString()) : await FileBytes(captureFile, 65536, ct);
        }
        catch (InvalidDataException) { return ApiProblem.Result(context, 413, "upload_too_large", "Fichier de capture trop volumineux."); }
        JsonDocument manifestDocument;
        try
        {
            manifestDocument = JsonDocument.Parse(captureBytes, new JsonDocumentOptions { MaxDepth = 16 });
            if (!NoDuplicateKeys(manifestDocument.RootElement)) throw new JsonException();
        }
        catch (JsonException) { return ApiProblem.Result(context, 400, "capture_manifest_invalid", "Manifeste JSON invalide."); }
        using (manifestDocument)
        {
            var principal = Owner(context);
            long budget, firstSequence;
            string layoutVersion;
            string firstHash;
            await using (var connection = await db.OpenConnectionAsync(ct))
            await using (var command = new NpgsqlCommand("SELECT budget_micro_units,first_sequence,first_block_sha256,layout_version FROM parchments WHERE id=@id AND owner_id=@owner", connection))
            {
                command.Parameters.AddWithValue("id", parchmentId);
                command.Parameters.AddWithValue("owner", principal.Id);
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return ApiProblem.Result(context, 404, "not_found", "Support introuvable.");
                if (reader.IsDBNull(1) || reader.IsDBNull(2)) return ApiProblem.Result(context, 409, "parchment_blank", "Le support n'a pas de première inscription durable.");
                budget = reader.GetInt64(0); firstSequence = reader.GetInt64(1); firstHash = reader.GetString(2);
                layoutVersion = reader.GetString(3);
            }
            (Guid referenceId, string referenceHash) referenceInfo;
            try { referenceInfo = await reference.EnsureAsync(layoutVersion, ct); }
            catch (Exception) { return ApiProblem.Result(context, 503, "reference_unavailable", "Référence indisponible.", true); }
            CaptureManifest manifest;
            try { manifest = CaptureValidation.Validate(manifestDocument.RootElement, parchmentId,
                layoutVersion, referenceInfo.referenceHash,
                await File.ReadAllBytesAsync(config.ReferencePathForLayout(layoutVersion), ct),
                drawing, ink, journal, budget, firstSequence, firstHash); }
            catch (Exception ex) when (ex is InvalidDataException or JsonException or KeyNotFoundException or FormatException or ArgumentOutOfRangeException or InvalidOperationException)
            { return ApiProblem.Result(context, 422, "capture_incompatible", "Capture endommagée ou incompatible."); }

            await using var dbConnection = await db.OpenConnectionAsync(ct);
            await using var transaction = await dbConnection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            await LockKeyAsync(dbConnection, transaction, principal.Id, $"capture:{id}", key, ct);
            var saved = await ExistingAsync(dbConnection, transaction, principal.Id, $"capture:{id}", key, ct);
            if (saved is not null) return ReplayOrConflict(context, saved, manifest.RequestHash);
            await using (var lockParchment = new NpgsqlCommand("SELECT state FROM parchments WHERE id=@id AND owner_id=@owner FOR UPDATE", dbConnection, transaction))
            {
                lockParchment.Parameters.AddWithValue("id", parchmentId);
                lockParchment.Parameters.AddWithValue("owner", principal.Id);
                var state = (string?)await lockParchment.ExecuteScalarAsync(ct);
                if (state is null) return ApiProblem.Result(context, 404, "not_found", "Support introuvable.");
                if (state == "blank") return ApiProblem.Result(context, 409, "parchment_blank", "Le support n'est pas engagé.");
            }
            Guid? existingJobId = null;
            string? existingRequestHash = null;
            string? existingState = null, existingResumeStage = null, existingMessage = null, existingErrorCode = null;
            Guid? existingSpellId = null;
            var existingAttemptCount = 0;
            var existingRetryable = false;
            await using (var existing = new NpgsqlCommand("SELECT c.request_sha256,j.id,j.state,j.resume_stage,j.spell_id,j.attempt_count,j.message,j.error_code,j.retryable FROM captures c JOIN jobs j ON j.capture_id=c.id WHERE c.parchment_id=@id AND c.owner_id=@owner", dbConnection, transaction))
            {
                existing.Parameters.AddWithValue("id", parchmentId);
                existing.Parameters.AddWithValue("owner", principal.Id);
                await using var reader = await existing.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    existingRequestHash = reader.GetString(0); existingJobId = reader.GetGuid(1); existingState = reader.GetString(2);
                    existingResumeStage = reader.IsDBNull(3) ? null : reader.GetString(3);
                    existingSpellId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
                    existingAttemptCount = reader.GetInt32(5); existingMessage = reader.GetString(6);
                    existingErrorCode = reader.IsDBNull(7) ? null : reader.GetString(7); existingRetryable = reader.GetBoolean(8);
                }
            }
            if (existingJobId is { } already)
            {
                if (existingRequestHash != manifest.RequestHash) return ApiProblem.Result(context, 409, "capture_already_committed", "Un dessin différent est déjà attaché à ce support.");
                var existingBody = Json(Job(already, parchmentId, existingState!, existingResumeStage, existingSpellId, existingAttemptCount, existingMessage!, existingErrorCode, existingRetryable));
                await SaveResponseAsync(dbConnection, transaction, principal.Id, $"capture:{id}", key, manifest.RequestHash, 202, existingBody, ct);
                await transaction.CommitAsync(ct);
                return Results.Content(existingBody, "application/json", Encoding.UTF8, 202);
            }
            var admission = await GenerationQuota.CheckAsync(dbConnection, transaction, principal.Id, config, ct);
            if (admission != GenerationQuotaDecision.Allowed)
                return ApiProblem.Result(context, 429, "generation_quota_exceeded",
                    admission == GenerationQuotaDecision.GlobalExceeded
                        ? "Capacité quotidienne du laboratoire atteinte. Réessayez plus tard."
                        : "Votre quota quotidien de générations est atteint. Réessayez plus tard.", true);
            // The files become durable before their DB rows are attached. Holding the
            // parchment row lock avoids writing orphan files on ordinary duplicate uploads.
            var drawingArtifact = await store.PutAsync(drawing, "png", "image/png", ct);
            var inkArtifact = await store.PutAsync(ink, "png", "image/png", ct);
            var journalArtifact = await store.PutAsync(journal, "gz", "application/gzip", ct);
            await InsertArtifact(dbConnection, transaction, principal.Id, "drawing", drawingArtifact, ct);
            await InsertArtifact(dbConnection, transaction, principal.Id, "ink", inkArtifact, ct);
            await InsertArtifact(dbConnection, transaction, principal.Id, "journal", journalArtifact, ct);
            await using (var insertCapture = new NpgsqlCommand("INSERT INTO captures(id,parchment_id,owner_id,manifest,manifest_sha256,request_sha256,drawing_artifact_id,ink_artifact_id,journal_artifact_id,reference_artifact_id) VALUES (@id,@parchment,@owner,@manifest::jsonb,@manifest_hash,@request_hash,@drawing,@ink,@journal,@reference)", dbConnection, transaction))
            {
                insertCapture.Parameters.AddWithValue("id", manifest.CaptureId);
                insertCapture.Parameters.AddWithValue("parchment", parchmentId);
                insertCapture.Parameters.AddWithValue("owner", principal.Id);
                insertCapture.Parameters.AddWithValue("manifest", manifest.CanonicalJson);
                insertCapture.Parameters.AddWithValue("manifest_hash", manifest.ManifestHash);
                insertCapture.Parameters.AddWithValue("request_hash", manifest.RequestHash);
                insertCapture.Parameters.AddWithValue("drawing", drawingArtifact.Id);
                insertCapture.Parameters.AddWithValue("ink", inkArtifact.Id);
                insertCapture.Parameters.AddWithValue("journal", journalArtifact.Id);
                insertCapture.Parameters.AddWithValue("reference", referenceInfo.referenceId);
                await insertCapture.ExecuteNonQueryAsync(ct);
            }
            var jobId = Guid.NewGuid();
            await using (var insertJob = new NpgsqlCommand("INSERT INTO jobs(id,owner_id,parchment_id,capture_id,kind,state,message,created_at) VALUES (@id,@owner,@parchment,@capture,'production','queued','Capture reçue',clock_timestamp())", dbConnection, transaction))
            {
                insertJob.Parameters.AddWithValue("id", jobId);
                insertJob.Parameters.AddWithValue("owner", principal.Id);
                insertJob.Parameters.AddWithValue("parchment", parchmentId);
                insertJob.Parameters.AddWithValue("capture", manifest.CaptureId);
                await insertJob.ExecuteNonQueryAsync(ct);
            }
            await using (var update = new NpgsqlCommand("UPDATE parchments SET state='processing',updated_at=now() WHERE id=@id", dbConnection, transaction))
            {
                update.Parameters.AddWithValue("id", parchmentId);
                await update.ExecuteNonQueryAsync(ct);
            }
            var json = Json(Job(jobId, parchmentId, "queued", null, null, 0, "Capture reçue", null, false));
            await SaveResponseAsync(dbConnection, transaction, principal.Id, $"capture:{id}", key, manifest.RequestHash, 202, json, ct);
            await transaction.CommitAsync(ct);
            return Results.Content(json, "application/json", Encoding.UTF8, 202);
        }
    }

    private static async Task<byte[]> FileBytes(IFormFile file, int max, CancellationToken ct)
    {
        if (file.Length > max) throw new InvalidDataException();
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        if (buffer.Length > max) throw new InvalidDataException();
        return buffer.ToArray();
    }

    private static async Task InsertArtifact(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid ownerId, string kind, StoredArtifact artifact, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("INSERT INTO artifacts(id,owner_id,kind,storage_key,sha256,content_type,byte_length) VALUES (@id,@owner,@kind,@key,@sha,@content_type,@length)", connection, transaction);
        command.Parameters.AddWithValue("id", artifact.Id);
        command.Parameters.AddWithValue("owner", ownerId);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("key", artifact.StorageKey);
        command.Parameters.AddWithValue("sha", artifact.Sha256);
        command.Parameters.AddWithValue("content_type", artifact.ContentType);
        command.Parameters.AddWithValue("length", artifact.ByteLength);
        await command.ExecuteNonQueryAsync(ct);
    }
}
