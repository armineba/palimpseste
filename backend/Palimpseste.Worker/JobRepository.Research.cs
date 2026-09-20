using Npgsql;
using Palimpseste.Storage;

namespace Palimpseste.Worker;

public sealed partial class JobRepository
{
    public async Task<StoredDocument?> GetReferenceResearchAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT a.storage_key,a.sha256,a.id FROM spell_reference_research r
            JOIN jobs j ON j.id=r.job_id
            JOIN artifacts a ON a.id=r.artifact_id AND a.owner_id=j.owner_id
            WHERE j.id=$1 AND j.fence_token=$2 AND a.kind='reference_research'
              AND a.content_type='application/json'
            """);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetString(0), reader.GetString(1), reader.GetGuid(2)) : null;
    }

    public async Task SaveReferenceResearchAsync(ClaimedJob job, StoredArtifact artifact,
        string descriptionSha, string imageSha, CancellationToken ct)
    {
        if (job.VisualPipelineVersion < 3 || job.Kind != "production") throw new InvalidOperationException("research_pipeline_required");
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await LockUnpublishedJobAsync(conn, tx, job, ct);
        await InsertArtifactAsync(conn, tx, job, artifact, "reference_research", ct);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO spell_reference_research(job_id,artifact_id,description_sha256,image_sha256)
            SELECT $1,$2,$3,$4 WHERE EXISTS (
                SELECT 1 FROM visual_references v WHERE v.job_id=$1 AND v.description_sha256=$3
                  AND EXISTS (SELECT 1 FROM artifacts a WHERE a.id=v.artifact_id AND a.sha256=$4))
            ON CONFLICT(job_id) DO NOTHING
            """, conn, tx);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(artifact.Id);
        cmd.Parameters.AddWithValue(descriptionSha); cmd.Parameters.AddWithValue(imageSha);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("research_freeze_conflict_or_provenance");
        await tx.CommitAsync(ct);
    }
}
