using Npgsql;
using NpgsqlTypes;
using Palimpseste.Provider;
using Palimpseste.Storage;

namespace Palimpseste.Worker;

public sealed record ClaimedJob(Guid Id, Guid? ParchmentId, Guid? CaptureId, long Fence, string Kind, string State, int VisualPipelineVersion = 0);
public sealed record StoredVisualReference(string StorageKey, string Sha256, Guid ArtifactId, int SizeBytes,
    string DescriptionSha256, string PromptVersion, int Width, int Height,
    string? AnimationSheetJson = null, string? SourceAtlasSha256 = null);
public sealed record CaptureFiles(string ManifestJson, string ManifestSha, string SignatureSeedHex, string DrawingKey, string DrawingSha, string InkKey, string InkSha,
    string ReferenceKey, string ReferenceSha);
public sealed record StoredDocument(string StorageKey, string Sha256, Guid ArtifactId, string? PromptVersion = null, int Revision = 0);
public sealed record StoredGeometry(string GeometryId, Guid ArtifactId, string StorageKey, string Sha256);
public sealed record StoredMask(string FileName, Guid ArtifactId, string StorageKey, string Sha256);
public sealed record AuthoringInput(string DescriptionKey, string DescriptionSha, IReadOnlyList<Guid> GeometryArtifactIds);

public sealed partial class JobRepository
{
    private readonly NpgsqlDataSource source;
    public JobRepository(NpgsqlDataSource source) => this.source = source;

    public async Task<ClaimedJob?> ClaimAsync(string worker, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            WITH candidate AS (
                SELECT id FROM jobs
                WHERE state IN ('queued','interpreting','generating_visual_reference','resolving_geometry','planning','refining_visuals','validating','waiting_retry')
                  AND (lease_until IS NULL OR lease_until < now())
                  AND (next_attempt_at IS NULL OR next_attempt_at <= now())
                ORDER BY created_at, id FOR UPDATE SKIP LOCKED LIMIT 1
            )
            UPDATE jobs j SET fence_token=j.fence_token+1, leased_by=$1,
                lease_until=now()+interval '180 seconds',
                state=CASE WHEN j.state='queued' AND j.kind='authoring' THEN 'planning'
                           WHEN j.state='queued' THEN 'interpreting' ELSE j.state END,
                updated_at=now()
            FROM candidate WHERE j.id=candidate.id
            RETURNING j.id,j.parchment_id,j.capture_id,j.fence_token,j.kind,j.state,j.visual_pipeline_version
            """);
        cmd.Parameters.AddWithValue(worker);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetInt64(3), reader.GetString(4), reader.GetString(5), reader.GetInt32(6));
    }

    public async Task<bool> RenewAsync(ClaimedJob job, string worker, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            UPDATE jobs SET lease_until=now()+interval '180 seconds',updated_at=now()
            WHERE id=$1 AND fence_token=$2 AND leased_by=$3 AND state NOT IN ('ready','needs_operator')
            """);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence); cmd.Parameters.AddWithValue(worker);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<bool> SetStateAsync(ClaimedJob job, string state, string? error, string message, bool retryable, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            UPDATE jobs SET state=$3,error_code=$4,message=$5,retryable=$6,
                resume_stage=$7,updated_at=now(),
                lease_until=CASE WHEN $3 IN ('ready','needs_operator','waiting_retry') THEN NULL ELSE lease_until END,
                leased_by=CASE WHEN $3 IN ('ready','needs_operator','waiting_retry') THEN NULL ELSE leased_by END
            WHERE id=$1 AND fence_token=$2
            """, conn, tx);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(state); cmd.Parameters.AddWithValue((object?)error ?? DBNull.Value);
        cmd.Parameters.AddWithValue(message); cmd.Parameters.AddWithValue(retryable);
        cmd.Parameters.AddWithValue((object?)(state == "ready" ? null : state) ?? DBNull.Value);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("fence_lost_on_state_change");
        if (state == "needs_operator" && job.ParchmentId is Guid parchmentId)
        {
            await using var parchment = new NpgsqlCommand("""
                UPDATE parchments SET state='incident',updated_at=now()
                WHERE id=$1 AND state<>'ready'
                """, conn, tx);
            parchment.Parameters.AddWithValue(parchmentId);
            await parchment.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> HasUncertainAttemptAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT EXISTS(SELECT 1 FROM provider_attempts WHERE job_id=$1
              AND (status='running' OR status='transport_uncertain'))
            """);
        cmd.Parameters.AddWithValue(job.Id);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task<int> CountAttemptsAsync(ClaimedJob job, string stage, CancellationToken ct)
    {
        if (stage is not ("A" or "G" or "B" or "J" or "repair_A" or "repair_B")) throw new ArgumentOutOfRangeException(nameof(stage));
        await using var cmd = source.CreateCommand("SELECT count(*) FROM provider_attempts WHERE job_id=$1 AND stage=$2");
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(stage);
        return checked((int)(long)(await cmd.ExecuteScalarAsync(ct) ?? 0L));
    }

    public async Task<string?> GetSuccessfulRequestedModelAsync(ClaimedJob job, string stage, CancellationToken ct)
    {
        if (stage is not ("A" or "B")) throw new ArgumentOutOfRangeException(nameof(stage));
        await using var cmd = source.CreateCommand("""
            SELECT requested_model FROM provider_attempts
            WHERE job_id=$1 AND status='success' AND stage IN ($2,$3)
            ORDER BY finished_at DESC, id DESC LIMIT 1
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(stage);
        cmd.Parameters.AddWithValue("repair_" + stage);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }

    public async Task ScheduleRetryAsync(ClaimedJob job, string stage, string reason, CancellationToken ct)
    {
        if (stage is not ("A" or "G" or "B" or "J")) throw new ArgumentOutOfRangeException(nameof(stage));
        await using var cmd = source.CreateCommand("""
            UPDATE jobs SET state='waiting_retry',resume_stage=$3,error_code=$4,
                message='Nouvelle tentative de transport planifiée',retryable=true,
                next_attempt_at=now()+interval '10 seconds',lease_until=NULL,leased_by=NULL,updated_at=now()
            WHERE id=$1 AND fence_token=$2
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(stage);
        cmd.Parameters.AddWithValue(reason);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("fence_lost_on_retry_schedule");
    }

    public async Task<CaptureFiles> GetCaptureAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT c.manifest::text,c.manifest_sha256,p.signature_seed_hex,d.storage_key,d.sha256,i.storage_key,i.sha256,
                   r.storage_key,r.sha256
            FROM captures c JOIN parchments p ON p.id=c.parchment_id
                JOIN artifacts d ON d.id=c.drawing_artifact_id
                JOIN artifacts i ON i.id=c.ink_artifact_id
                JOIN artifacts r ON r.id=c.reference_artifact_id
            WHERE c.id=$1 AND c.parchment_id=$2 AND c.owner_id=p.owner_id
              AND d.owner_id=c.owner_id AND i.owner_id=c.owner_id
            """);
        cmd.Parameters.AddWithValue(job.CaptureId!.Value); cmd.Parameters.AddWithValue(job.ParchmentId!.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidDataException("capture_not_found");
        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8));
    }

    public async Task<StoredDocument?> GetDescriptionAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT a.storage_key,i.description_sha256,a.id,i.prompt_version FROM interpretations i
            JOIN artifacts a ON a.id=i.description_artifact_id WHERE i.job_id=$1
            """);
        cmd.Parameters.AddWithValue(job.Id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetString(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3)) : null;
    }

    public async Task<StoredDocument?> GetPlanAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT a.storage_key,p.plan_sha256,a.id,p.prompt_version,p.revision FROM spell_plans p
            JOIN artifacts a ON a.id=p.plan_artifact_id
            JOIN jobs j ON j.id=p.job_id AND j.owner_id=a.owner_id
            WHERE p.job_id=$1 AND j.fence_token=$2 AND p.validation_errors IS NULL AND a.kind='plan'
              AND a.sha256=p.plan_sha256
            ORDER BY CASE WHEN $3 THEN -p.revision ELSE p.revision END LIMIT 1
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(job.VisualPipelineVersion >= 2 && job.Kind == "production");
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetString(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3), reader.GetInt32(4)) : null;
    }

    public async Task<StoredVisualReference?> GetVisualReferenceAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT a.storage_key,a.sha256,a.id,a.byte_length,v.description_sha256,v.prompt_version,v.width_px,v.height_px,
                   v.animation_sheet::text,native.sha256
            FROM visual_references v JOIN artifacts a ON a.id=v.artifact_id
            JOIN jobs j ON j.id=v.job_id AND a.owner_id=j.owner_id
            LEFT JOIN visual_atlases atlas ON atlas.job_id=v.job_id AND atlas.artifact_id=v.source_atlas_artifact_id
            LEFT JOIN artifacts native ON native.id=atlas.artifact_id AND native.owner_id=j.owner_id
                AND native.kind='visual_atlas' AND native.content_type='image/png'
            WHERE v.job_id=$1 AND j.fence_token=$2 AND a.kind='visual_reference' AND a.content_type='image/png'
              AND (v.source_atlas_artifact_id IS NULL OR native.id IS NOT NULL)
            """);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(r.GetString(0), r.GetString(1), r.GetGuid(2), checked((int)r.GetInt64(3)),
            r.GetString(4), r.GetString(5), r.GetInt32(6), r.GetInt32(7),
            r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)) : null;
    }

    public async Task SaveVisualReferenceAsync(ClaimedJob job, Guid attemptId, StoredArtifact artifact,
        string descriptionHash, string inputHash, string promptVersion, int width, int height,
        CodexResult? transport, CancellationToken ct, string? animationSheetJson = null, Guid? sourceAtlasArtifactId = null)
    {
        if (job.VisualPipelineVersion >= 4 || animationSheetJson is not null || sourceAtlasArtifactId.HasValue)
        {
            await SaveComposedVisualReferenceAsync(job, attemptId, artifact, descriptionHash, inputHash,
                promptVersion, width, height, animationSheetJson, sourceAtlasArtifactId, ct);
            return;
        }
        ArgumentNullException.ThrowIfNull(transport);
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await InsertArtifactAsync(conn, tx, job, artifact, "visual_reference", ct);
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO visual_references(job_id,provider_attempt_id,artifact_id,description_sha256,prompt_version,input_sha256,width_px,height_px)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8)
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(attemptId);
            cmd.Parameters.AddWithValue(artifact.Id); cmd.Parameters.AddWithValue(descriptionHash);
            cmd.Parameters.AddWithValue(promptVersion); cmd.Parameters.AddWithValue(inputHash);
            cmd.Parameters.AddWithValue(width); cmd.Parameters.AddWithValue(height);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await CompleteSuccessfulAttemptInTransactionAsync(conn, tx, job, attemptId, artifact.Sha256, transport, ct);
        await MoveInTransactionAsync(conn, tx, job, "resolving_geometry", ct);
        await tx.CommitAsync(ct);
    }

    public async Task<AuthoringInput> GetAuthoringInputAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT a.storage_key,i.description_sha256,i.geometry_artifact_ids::text
            FROM authoring_inputs i JOIN artifacts a ON a.id=i.description_artifact_id
                JOIN jobs j ON j.id=i.job_id
            WHERE i.job_id=$1 AND j.fence_token=$2 AND a.owner_id=j.owner_id
            """);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new InvalidDataException("authoring_input_missing");
        var values = System.Text.Json.JsonSerializer.Deserialize<string[]>(r.GetString(2)) ?? [];
        if (values.Length is < 1 or > 32) throw new InvalidDataException("authoring_geometry_count");
        var ids = new List<Guid>();
        foreach (var value in values)
        {
            if (value.Length != 33 || value[0] != 'a' || !Guid.TryParseExact(value[1..], "N", out var id))
                throw new InvalidDataException("authoring_geometry_id");
            ids.Add(id);
        }
        if (ids.Count != ids.Distinct().Count()) throw new InvalidDataException("authoring_geometry_duplicate");
        return new(r.GetString(0), r.GetString(1), ids);
    }

    public async Task LinkAuthoringGeometryAsync(ClaimedJob job, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        Guid sourceJob;
        await using (var cmd = new NpgsqlCommand("""
            SELECT g.job_id,count(*) FROM geometry_assets g
                JOIN jobs source_job ON source_job.id=g.job_id
                JOIN jobs current_job ON current_job.id=$1
            WHERE g.artifact_id=ANY($2) AND source_job.owner_id=current_job.owner_id
                AND current_job.fence_token=$3
            GROUP BY g.job_id
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(job.Id);
            cmd.Parameters.AddWithValue(ids.ToArray());
            cmd.Parameters.AddWithValue(job.Fence);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct) || r.GetInt64(1) != ids.Count)
                throw new InvalidDataException("authoring_geometry_not_owned_or_mixed");
            sourceJob = r.GetGuid(0);
            if (await r.ReadAsync(ct)) throw new InvalidDataException("authoring_geometry_mixed_sources");
        }
        await using (var count = new NpgsqlCommand("SELECT count(*) FROM geometry_assets WHERE job_id=$1", conn, tx))
        {
            count.Parameters.AddWithValue(sourceJob);
            if ((long)(await count.ExecuteScalarAsync(ct) ?? 0L) != ids.Count)
                throw new InvalidDataException("authoring_geometry_incomplete_bank");
        }
        await using (var copy = new NpgsqlCommand("""
            INSERT INTO geometry_assets(id,job_id,artifact_id,geometry_id,metadata)
            SELECT gen_random_uuid(),$1,artifact_id,geometry_id,metadata FROM geometry_assets
            WHERE job_id=$2 ON CONFLICT(job_id,geometry_id) DO NOTHING
            """, conn, tx))
        {
            copy.Parameters.AddWithValue(job.Id); copy.Parameters.AddWithValue(sourceJob);
            await copy.ExecuteNonQueryAsync(ct);
        }
        await using (var copy = new NpgsqlCommand("""
            INSERT INTO geometry_masks(job_id,file_name,artifact_id)
            SELECT $1,file_name,artifact_id FROM geometry_masks WHERE job_id=$2
            ON CONFLICT(job_id,file_name) DO NOTHING
            """, conn, tx))
        {
            copy.Parameters.AddWithValue(job.Id); copy.Parameters.AddWithValue(sourceJob);
            await copy.ExecuteNonQueryAsync(ct);
        }
        await MoveInTransactionAsync(conn, tx, job, "planning", ct);
        await tx.CommitAsync(ct);
    }

    public async Task<(List<StoredGeometry> Geometry, List<StoredMask> Masks)> GetGeometryAsync(ClaimedJob job, CancellationToken ct)
    {
        var geometry = new List<StoredGeometry>(); var masks = new List<StoredMask>();
        await using (var cmd = source.CreateCommand("""
            SELECT g.geometry_id,g.artifact_id,a.storage_key,a.sha256 FROM geometry_assets g
            JOIN artifacts a ON a.id=g.artifact_id WHERE g.job_id=$1 ORDER BY g.geometry_id
            """))
        {
            cmd.Parameters.AddWithValue(job.Id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) geometry.Add(new(r.GetString(0), r.GetGuid(1), r.GetString(2), r.GetString(3)));
        }
        await using (var cmd = source.CreateCommand("""
            SELECT m.file_name,m.artifact_id,a.storage_key,a.sha256 FROM geometry_masks m
            JOIN artifacts a ON a.id=m.artifact_id WHERE m.job_id=$1 ORDER BY m.file_name
            """))
        {
            cmd.Parameters.AddWithValue(job.Id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) masks.Add(new(r.GetString(0), r.GetGuid(1), r.GetString(2), r.GetString(3)));
        }
        return (geometry, masks);
    }

    public async Task<(string? A, string? B)> GetSessionIdsAsync(ClaimedJob job, CancellationToken ct)
    {
        string? a = null, b = null;
        await using var cmd = source.CreateCommand("""
            SELECT stage,session_id FROM provider_attempts
            WHERE job_id=$1 AND status='success' AND stage IN ('A','B')
            ORDER BY started_at
            """);
        cmd.Parameters.AddWithValue(job.Id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            if (r.GetString(0) == "A") a = r.IsDBNull(1) ? null : r.GetString(1);
            else b = r.IsDBNull(1) ? null : r.GetString(1);
        }
        return (a, b);
    }

    public async Task<Guid> BeginAttemptAsync(ClaimedJob job, string stage, string model, string effort, string inputHash, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var gate = new NpgsqlCommand("""
            UPDATE jobs SET attempt_count=attempt_count+1,updated_at=now()
            WHERE id=$1 AND fence_token=$2 AND attempt_count<10 RETURNING attempt_count
            """, conn, tx);
        gate.Parameters.AddWithValue(job.Id); gate.Parameters.AddWithValue(job.Fence);
        if (await gate.ExecuteScalarAsync(ct) is null) throw new InvalidOperationException("attempt_budget_or_fence_lost");
        var id = Guid.NewGuid();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO provider_attempts(id,job_id,stage,fence_token,status,requested_model,requested_effort,
                sent_config,input_sha256)
            VALUES($1,$2,$3,$4,'running',$5,$6,$7::jsonb,$8)
            """, conn, tx);
        insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(job.Id);
        insert.Parameters.AddWithValue(stage); insert.Parameters.AddWithValue(job.Fence);
        insert.Parameters.AddWithValue(model); insert.Parameters.AddWithValue(effort);
        insert.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(new { model, model_reasoning_effort = effort }));
        insert.Parameters.AddWithValue(inputHash);
        await insert.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task CompleteAttemptAsync(ClaimedJob job, Guid attemptId, string status, string? outputHash,
        string? version, string? sessionId, string? usageJson, string? errorCode, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            UPDATE provider_attempts a SET status=$4,output_sha256=$5,codex_version=$6,
                session_id=$7,usage=$8::jsonb,error_code=$9,finished_at=now()
            FROM jobs j WHERE a.id=$1 AND a.job_id=$2 AND a.fence_token=$3
              AND j.id=a.job_id AND j.fence_token=$3
            """);
        cmd.Parameters.AddWithValue(attemptId); cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(status); cmd.Parameters.AddWithValue((object?)outputHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)version ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)sessionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)usageJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)errorCode ?? DBNull.Value);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("fence_lost_on_attempt_completion");
    }

    private static async Task InsertArtifactAsync(NpgsqlConnection conn, NpgsqlTransaction tx, ClaimedJob job,
        StoredArtifact artifact, string kind, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO artifacts(id,owner_id,kind,storage_key,sha256,content_type,byte_length)
            SELECT $1,j.owner_id,$2,$3,$4,$5,$6 FROM jobs j
            WHERE j.id=$7 AND j.fence_token=$8
            """, conn, tx);
        cmd.Parameters.AddWithValue(artifact.Id); cmd.Parameters.AddWithValue(kind);
        cmd.Parameters.AddWithValue(artifact.StorageKey); cmd.Parameters.AddWithValue(artifact.Sha256);
        cmd.Parameters.AddWithValue(artifact.ContentType); cmd.Parameters.AddWithValue(artifact.ByteLength);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("fence_lost_on_artifact_insert");
    }

    public async Task SaveDescriptionAsync(ClaimedJob job, Guid attemptId, StoredArtifact artifact,
        string descriptionJson, string inputHash, string promptVersion, CodexResult transport, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await InsertArtifactAsync(conn, tx, job, artifact, "description", ct);
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO interpretations(id,job_id,provider_attempt_id,description,description_artifact_id,
                description_sha256,prompt_version,input_sha256)
            VALUES($1,$2,$3,$4::jsonb,$5,$6,$7,$8)
            ON CONFLICT(job_id) DO NOTHING
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(Guid.NewGuid()); cmd.Parameters.AddWithValue(job.Id);
            cmd.Parameters.AddWithValue(attemptId); cmd.Parameters.AddWithValue(descriptionJson);
            cmd.Parameters.AddWithValue(artifact.Id); cmd.Parameters.AddWithValue(artifact.Sha256);
            cmd.Parameters.AddWithValue(promptVersion); cmd.Parameters.AddWithValue(inputHash);
            if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("description_already_frozen");
        }
        await CompleteSuccessfulAttemptInTransactionAsync(conn, tx, job, attemptId, artifact.Sha256, transport, ct);
        await MoveInTransactionAsync(conn, tx, job, "resolving_geometry", ct);
        await tx.CommitAsync(ct);
    }

    public async Task SaveGeometryAsync(ClaimedJob job,
        IReadOnlyDictionary<string, StoredArtifact> geometries,
        IReadOnlyDictionary<string, StoredArtifact> masks, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        foreach (var item in geometries)
        {
            await InsertArtifactAsync(conn, tx, job, item.Value, "geometry", ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO geometry_assets(id,job_id,artifact_id,geometry_id,metadata)
                VALUES($1,$2,$3,$4,'{}'::jsonb) ON CONFLICT(job_id,geometry_id) DO NOTHING
                """, conn, tx);
            cmd.Parameters.AddWithValue(Guid.NewGuid()); cmd.Parameters.AddWithValue(job.Id);
            cmd.Parameters.AddWithValue(item.Value.Id); cmd.Parameters.AddWithValue(item.Key);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        foreach (var item in masks)
        {
            await InsertArtifactAsync(conn, tx, job, item.Value, "geometry_mask", ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO geometry_masks(job_id,file_name,artifact_id)
                VALUES($1,$2,$3) ON CONFLICT(job_id,file_name) DO NOTHING
                """, conn, tx);
            cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(item.Key); cmd.Parameters.AddWithValue(item.Value.Id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await MoveInTransactionAsync(conn, tx, job, "planning", ct);
        await tx.CommitAsync(ct);
    }

    public async Task SavePlanAsync(ClaimedJob job, Guid attemptId, StoredArtifact artifact,
        string planJson, string promptVersion, CodexResult transport, CancellationToken ct, int revision = 0)
    {
        if (revision < 0 || revision > 3 || revision > 0 && (job.VisualPipelineVersion < 2 || job.Kind != "production"))
            throw new ArgumentOutOfRangeException(nameof(revision));
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await LockUnpublishedJobAsync(conn, tx, job, ct);
        await InsertArtifactAsync(conn, tx, job, artifact, "plan", ct);
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO spell_plans(id,job_id,provider_attempt_id,revision,plan,plan_artifact_id,plan_sha256,prompt_version)
            VALUES($1,$2,$3,$8,$4::jsonb,$5,$6,$7)
            ON CONFLICT(job_id,revision) DO NOTHING
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(Guid.NewGuid()); cmd.Parameters.AddWithValue(job.Id);
            cmd.Parameters.AddWithValue(attemptId); cmd.Parameters.AddWithValue(planJson);
            cmd.Parameters.AddWithValue(artifact.Id); cmd.Parameters.AddWithValue(artifact.Sha256);
            cmd.Parameters.AddWithValue(promptVersion);
            cmd.Parameters.AddWithValue(revision);
            if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("plan_already_frozen");
        }
        await CompleteSuccessfulAttemptInTransactionAsync(conn, tx, job, attemptId, artifact.Sha256, transport, ct);
        await MoveInTransactionAsync(conn, tx, job,
            job.VisualPipelineVersion >= 2 && job.Kind == "production" ? "refining_visuals" : "validating", ct);
        await tx.CommitAsync(ct);
    }

    public async Task PublishAsync(ClaimedJob job, Guid spellId, StoredArtifact artifact, CancellationToken ct,
        string readyMessage = "Sort disponible")
    {
        if (job.ParchmentId is null) throw new InvalidOperationException("authoring_cannot_publish_player_spell");
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        // Serialize V2 publication against SavePlanAsync: its latest accepted revision cannot change mid-publish.
        if (job.VisualPipelineVersion == 5) await LockUnpublishedJobAsync(conn, tx, job, ct);
        await InsertArtifactAsync(conn, tx, job, artifact, "compiled_spell", ct);
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO spells(id,owner_id,parchment_id,job_id,payload_artifact_id,payload_sha256,rules_profile,catalog_version)
            SELECT $1,j.owner_id,j.parchment_id,j.id,$2,$3,'lab_v1','sp.capabilities/1.0'
            FROM jobs j WHERE j.id=$4 AND j.fence_token=$5 AND j.kind='production'
              AND (j.visual_pipeline_version<>5 OR EXISTS (
                SELECT 1 FROM spell_v2_passes v
                JOIN spell_plans p ON p.job_id=v.job_id AND p.revision=v.revision AND p.plan_sha256=v.input_sha256
                JOIN artifacts pa ON pa.id=p.plan_artifact_id AND pa.owner_id=j.owner_id AND pa.kind='plan' AND pa.sha256=p.plan_sha256
                WHERE v.job_id=j.id AND v.pass='validation' AND v.accepted=true AND p.validation_errors IS NULL
                  AND p.revision=(SELECT MAX(latest.revision) FROM spell_plans latest WHERE latest.job_id=j.id)))
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(spellId); cmd.Parameters.AddWithValue(artifact.Id);
            cmd.Parameters.AddWithValue(artifact.Sha256); cmd.Parameters.AddWithValue(job.Id);
            cmd.Parameters.AddWithValue(job.Fence);
            if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("fence_lost_on_publish");
        }
        await using (var cmd = new NpgsqlCommand("""
            UPDATE jobs SET state='ready',spell_id=$3,message=$4,error_code=NULL,
                retryable=false,resume_stage=NULL,lease_until=NULL,leased_by=NULL,updated_at=now()
            WHERE id=$1 AND fence_token=$2
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence); cmd.Parameters.AddWithValue(spellId);
            cmd.Parameters.AddWithValue(readyMessage);
            if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("fence_lost_on_ready");
        }
        await using (var cmd = new NpgsqlCommand("UPDATE parchments SET state='ready',updated_at=now() WHERE id=$1", conn, tx))
        {
            cmd.Parameters.AddWithValue(job.ParchmentId.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private static async Task MoveInTransactionAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
        ClaimedJob job, string state, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            UPDATE jobs SET state=$3,resume_stage=$3,message='',error_code=NULL,updated_at=now()
            WHERE id=$1 AND fence_token=$2
            """, conn, tx);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence); cmd.Parameters.AddWithValue(state);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("fence_lost_on_stage_change");
    }

    private static async Task CompleteSuccessfulAttemptInTransactionAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
        ClaimedJob job, Guid attemptId, string outputHash, CodexResult transport, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            UPDATE provider_attempts SET status='success',output_sha256=$4,codex_version=$5,
                session_id=$6,usage=$7::jsonb,finished_at=now()
            WHERE id=$1 AND job_id=$2 AND fence_token=$3 AND status='running'
            """, conn, tx);
        cmd.Parameters.AddWithValue(attemptId); cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(outputHash); cmd.Parameters.AddWithValue(transport.CliVersion);
        cmd.Parameters.AddWithValue((object?)transport.SessionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)transport.UsageJson ?? DBNull.Value);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("attempt_not_running_or_fence_lost");
    }
}
