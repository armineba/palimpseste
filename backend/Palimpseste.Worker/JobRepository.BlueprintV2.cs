using Npgsql;
using Palimpseste.Provider;
using Palimpseste.Storage;

namespace Palimpseste.Worker;

public sealed record V2PassDocument(string StorageKey, string Sha256, Guid ArtifactId,
    string InputSha256, string? LockedCoreSha256, bool Accepted);

public sealed partial class JobRepository
{
    public async Task<int> GetV2ConstructionBaseAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var query = source.CreateCommand("""
            UPDATE jobs SET v2_builder_version=COALESCE(v2_builder_version,$3)
            WHERE id=$1 AND fence_token=$2 AND visual_pipeline_version=5
            RETURNING v2_revision_base,v2_builder_version
            """);
        query.Parameters.AddWithValue(job.Id); query.Parameters.AddWithValue(job.Fence);
        query.Parameters.AddWithValue(V2ConstructionPolicy.Version);
        await using var reader = await query.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.GetString(1) != V2ConstructionPolicy.Version)
            throw new InvalidOperationException("v2_policy_upgrade_requires_explicit_resume");
        var first = reader.GetInt32(0);
        if (first < 0 || first > V2ConstructionPolicy.MaxRevision - V2ConstructionPolicy.RevisionsPerWindow + 1)
            throw new InvalidDataException("v2_revision_window_invalid");
        return first;
    }

    public async Task<bool> VerifyLatestV2SchemaFailureIfPresentAsync(ClaimedJob job,
        string attemptRoot, string specificationRoot, CancellationToken ct)
    {
        await using var query = source.CreateCommand("""
            SELECT a.id,a.status,a.output_sha256,a.error_code
            FROM provider_attempts a JOIN jobs j ON j.id=a.job_id
            WHERE j.id=$1 AND j.fence_token=$2 AND j.visual_pipeline_version=5 AND a.stage='B'
            ORDER BY a.started_at DESC LIMIT 1
            """);
        query.Parameters.AddWithValue(job.Id); query.Parameters.AddWithValue(job.Fence);
        await using var reader = await query.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.IsDBNull(3) || reader.GetString(3) != "codex_output_schema_rejected") return true;
        return await SchemaFailureEvidence.VerifyAsync(attemptRoot, specificationRoot, job.Id,
            reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), ct);
    }

    public async Task<bool> IsLegacyBlueprintSchemaFailureAsync(ClaimedJob job, int revision,
        string attemptRoot, string specificationRoot, CancellationToken ct)
    {
        // The historical row did not retain provider_attempt_id. Match its frozen
        // input and original timestamp; later successful retries may share that input.
        var attempts = new List<(Guid Id, string Status, string? Output, string? Error)>();
        await using (var query = source.CreateCommand("""
            SELECT a.id,a.status,a.output_sha256,a.error_code
            FROM spell_v2_passes p JOIN jobs j ON j.id=p.job_id
            JOIN provider_attempts a ON a.job_id=j.id AND a.stage='B' AND a.input_sha256=p.input_sha256
                AND a.finished_at<=p.created_at
            WHERE j.id=$1 AND j.fence_token=$2 AND j.visual_pipeline_version=5
              AND p.revision=$3 AND p.pass='blueprint' AND NOT p.accepted
            """))
        {
            query.Parameters.AddWithValue(job.Id); query.Parameters.AddWithValue(job.Fence); query.Parameters.AddWithValue(revision);
            await using var reader = await query.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) attempts.Add((reader.GetGuid(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        if (attempts.Count == 0) return false;
        foreach (var attempt in attempts)
            if (!await SchemaFailureEvidence.VerifyAsync(attemptRoot, specificationRoot, job.Id,
                attempt.Id, attempt.Status, attempt.Output, attempt.Error, ct)) return false;
        return true;
    }

    public async Task<string> GetV2CreationTimeAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("SELECT created_at FROM jobs WHERE id=$1 AND fence_token=$2 AND visual_pipeline_version=5");
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is DateTime time ? time.ToUniversalTime().ToString("O") : throw new InvalidDataException("v2_job_clock");
    }

    public async Task MarkV2ValidationAcceptedAsync(ClaimedJob job, int revision, string planHash, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            UPDATE spell_v2_passes v SET accepted=true FROM jobs j
            WHERE v.job_id=j.id AND j.id=$1 AND j.fence_token=$2 AND j.visual_pipeline_version=5
              AND v.revision=$3 AND v.pass='validation' AND v.input_sha256=$4
              AND EXISTS(SELECT 1 FROM spell_v2_passes p WHERE p.job_id=j.id AND p.revision=v.revision AND p.pass='structure' AND p.accepted AND p.input_sha256=v.input_sha256)
              AND EXISTS(SELECT 1 FROM spell_v2_passes p WHERE p.job_id=j.id AND p.revision=v.revision AND p.pass='optimization' AND p.accepted)
              AND EXISTS(SELECT 1 FROM spell_v2_passes p WHERE p.job_id=j.id AND p.revision=v.revision AND p.pass='impact' AND p.accepted)
            """);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(revision); cmd.Parameters.AddWithValue(planHash);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("v2_publish_gates_missing");
    }

    public async Task MarkV2StructureAcceptedAsync(ClaimedJob job, int revision, string planHash, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            UPDATE spell_v2_passes p SET accepted=true FROM jobs j
            WHERE p.job_id=j.id AND j.id=$1 AND j.fence_token=$2 AND j.visual_pipeline_version=5
              AND p.revision=$3 AND p.pass='structure' AND p.input_sha256=$4
            """);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(revision); cmd.Parameters.AddWithValue(planHash);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("v2_structure_gate_missing");
    }

    public async Task<V2PassDocument?> GetV2PassAsync(ClaimedJob job, int revision, string pass, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT a.storage_key,a.sha256,a.id,p.input_sha256,p.locked_core_sha256,p.accepted
            FROM spell_v2_passes p JOIN artifacts a ON a.id=p.artifact_id
            JOIN jobs j ON j.id=p.job_id AND j.owner_id=a.owner_id
            WHERE j.id=$1 AND j.fence_token=$2 AND j.visual_pipeline_version=5 AND p.revision=$3 AND p.pass=$4
            """);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(revision); cmd.Parameters.AddWithValue(pass);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetString(0), reader.GetString(1), reader.GetGuid(2),
            reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetBoolean(5)) : null;
    }

    public async Task SaveV2PassAsync(ClaimedJob job, int revision, string pass, StoredArtifact artifact,
        string inputHash, string? lockedCoreHash, bool accepted, CancellationToken ct,
        Guid? attemptId = null, CodexResult? transport = null)
    {
        if (job.VisualPipelineVersion != 5 || revision is < 0 or > V2ConstructionPolicy.MaxRevision) throw new InvalidOperationException("v2_pass_version");
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await InsertArtifactAsync(conn, tx, job, artifact, "v2_" + pass, ct);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO spell_v2_passes(job_id,revision,pass,artifact_id,input_sha256,locked_core_sha256,accepted,provider_attempt_id)
            SELECT j.id,$3,$4,$5,$6,$7,$8,$9 FROM jobs j WHERE j.id=$1 AND j.fence_token=$2 AND j.visual_pipeline_version=5
            """, conn, tx);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence); cmd.Parameters.AddWithValue(revision);
        cmd.Parameters.AddWithValue(pass); cmd.Parameters.AddWithValue(artifact.Id); cmd.Parameters.AddWithValue(inputHash);
        cmd.Parameters.AddWithValue((object?)lockedCoreHash ?? DBNull.Value); cmd.Parameters.AddWithValue(accepted);
        cmd.Parameters.AddWithValue((object?)attemptId ?? DBNull.Value);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("v2_pass_fence_lost");
        if (attemptId.HasValue && transport is not null)
            await CompleteSuccessfulAttemptInTransactionAsync(conn, tx, job, attemptId.Value, artifact.Sha256, transport, ct);
        await tx.CommitAsync(ct);
    }
}
