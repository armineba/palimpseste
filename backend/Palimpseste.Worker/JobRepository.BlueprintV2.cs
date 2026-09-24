using Npgsql;
using Palimpseste.Provider;
using Palimpseste.Storage;

namespace Palimpseste.Worker;

public sealed record V2PassDocument(string StorageKey, string Sha256, Guid ArtifactId,
    string InputSha256, string? LockedCoreSha256, bool Accepted);

public sealed partial class JobRepository
{
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
        if (job.VisualPipelineVersion != 5 || revision is < 0 or > 3) throw new InvalidOperationException("v2_pass_version");
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
