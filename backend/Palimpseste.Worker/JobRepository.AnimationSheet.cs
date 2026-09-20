using System.Text.Json;
using Npgsql;
using Palimpseste.Provider;
using Palimpseste.Storage;

namespace Palimpseste.Worker;

public sealed record StoredVisualAtlas(string StorageKey, string Sha256, Guid ArtifactId, int SizeBytes,
    string DescriptionSha256, string PromptVersion, int Width, int Height,
    Guid ProviderAttemptId, string InputSha256, string AnimationSheetJson);

public sealed partial class JobRepository
{
    public async Task<StoredVisualAtlas?> GetVisualAtlasAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT a.storage_key,a.sha256,a.id,a.byte_length,v.description_sha256,v.prompt_version,v.width_px,v.height_px,
                   v.provider_attempt_id,v.input_sha256,v.animation_sheet::text
            FROM visual_atlases v
            JOIN jobs j ON j.id=v.job_id
            JOIN artifacts a ON a.id=v.artifact_id AND a.owner_id=j.owner_id
            JOIN provider_attempts pa ON pa.id=v.provider_attempt_id AND pa.job_id=j.id
                AND pa.stage='G' AND pa.status='success' AND pa.output_sha256=a.sha256 AND pa.input_sha256=v.input_sha256
            WHERE j.id=$1 AND j.fence_token=$2 AND j.visual_pipeline_version>=4
              AND a.kind='visual_atlas' AND a.content_type='image/png'
            """);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(r.GetString(0), r.GetString(1), r.GetGuid(2), checked((int)r.GetInt64(3)),
            r.GetString(4), r.GetString(5), r.GetInt32(6), r.GetInt32(7), r.GetGuid(8), r.GetString(9), r.GetString(10)) : null;
    }

    public async Task SaveVisualAtlasAsync(ClaimedJob job, Guid attemptId, StoredArtifact artifact,
        string descriptionHash, string inputHash, string promptVersion, int width, int height,
        string animationSheetJson, CodexResult transport, CancellationToken ct)
    {
        if (job.VisualPipelineVersion < 4 || job.Kind != "production") throw new InvalidOperationException("atlas_pipeline_required");
        ValidateAnimationSheetMetadata(animationSheetJson);
        if (artifact.ContentType != "image/png" || artifact.ByteLength is < 24 or > 8_388_608 ||
            transport.Outcome != ProviderOutcome.Success || transport.GeneratedImage is null ||
            transport.GeneratedImage.Sha256 != artifact.Sha256 || transport.GeneratedImage.Width != width ||
            transport.GeneratedImage.Height != height)
            throw new InvalidOperationException("atlas_native_provenance");
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await LockUnpublishedJobAsync(conn, tx, job, ct);
        await InsertArtifactAsync(conn, tx, job, artifact, "visual_atlas", ct);
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO visual_atlases(job_id,provider_attempt_id,artifact_id,description_sha256,prompt_version,
                input_sha256,width_px,height_px,animation_sheet)
            SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb
            FROM jobs j JOIN provider_attempts pa ON pa.job_id=j.id
            WHERE j.id=$1 AND j.fence_token=$10 AND j.kind='production' AND j.visual_pipeline_version>=4
              AND pa.id=$2 AND pa.fence_token=$10 AND pa.stage='G' AND pa.status='running' AND pa.input_sha256=$6
              AND EXISTS (SELECT 1 FROM interpretations i JOIN artifacts d ON d.id=i.description_artifact_id
                  WHERE i.job_id=j.id AND i.description_sha256=$4 AND d.sha256=$4
                    AND d.owner_id=j.owner_id AND d.kind='description' AND d.content_type='application/json')
            ON CONFLICT(job_id) DO NOTHING
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(attemptId);
            cmd.Parameters.AddWithValue(artifact.Id); cmd.Parameters.AddWithValue(descriptionHash);
            cmd.Parameters.AddWithValue(promptVersion); cmd.Parameters.AddWithValue(inputHash);
            cmd.Parameters.AddWithValue(width); cmd.Parameters.AddWithValue(height);
            cmd.Parameters.AddWithValue(animationSheetJson); cmd.Parameters.AddWithValue(job.Fence);
            if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("atlas_freeze_conflict_or_provenance");
        }
        // The native PNG, not the subsequently framed sheet, is the successful G output.
        await CompleteSuccessfulAttemptInTransactionAsync(conn, tx, job, attemptId, artifact.Sha256, transport, ct);
        await MoveInTransactionAsync(conn, tx, job, "generating_visual_reference", ct);
        await tx.CommitAsync(ct);
    }

    private async Task SaveComposedVisualReferenceAsync(ClaimedJob job, Guid attemptId, StoredArtifact artifact,
        string descriptionHash, string inputHash, string promptVersion, int width, int height,
        string? animationSheetJson, Guid? sourceAtlasArtifactId, CancellationToken ct)
    {
        if (job.VisualPipelineVersion < 4 || job.Kind != "production" || !sourceAtlasArtifactId.HasValue)
            throw new InvalidOperationException("animation_sheet_pipeline_required");
        ValidateAnimationSheetMetadata(animationSheetJson);
        if (artifact.ContentType != "image/png" || artifact.ByteLength is < 24 or > 8_388_608 || width != 1536 || height != 1152)
            throw new InvalidOperationException("animation_sheet_format");
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await LockUnpublishedJobAsync(conn, tx, job, ct);
        await InsertArtifactAsync(conn, tx, job, artifact, "visual_reference", ct);
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO visual_references(job_id,provider_attempt_id,artifact_id,description_sha256,prompt_version,
                input_sha256,width_px,height_px,animation_sheet,source_atlas_artifact_id)
            SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb,$10
            FROM jobs j JOIN visual_atlases v ON v.job_id=j.id
            JOIN artifacts native ON native.id=v.artifact_id AND native.owner_id=j.owner_id
            JOIN provider_attempts pa ON pa.id=v.provider_attempt_id AND pa.job_id=j.id
            WHERE j.id=$1 AND j.fence_token=$11 AND j.kind='production' AND j.visual_pipeline_version>=4
              AND v.artifact_id=$10 AND v.provider_attempt_id=$2 AND v.description_sha256=$4
              AND v.prompt_version=$5 AND v.input_sha256=$6 AND v.animation_sheet=$9::jsonb
              AND native.kind='visual_atlas' AND native.content_type='image/png'
              AND pa.stage='G' AND pa.status='success' AND pa.output_sha256=native.sha256 AND pa.input_sha256=v.input_sha256
            ON CONFLICT(job_id) DO NOTHING
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(attemptId);
            cmd.Parameters.AddWithValue(artifact.Id); cmd.Parameters.AddWithValue(descriptionHash);
            cmd.Parameters.AddWithValue(promptVersion); cmd.Parameters.AddWithValue(inputHash);
            cmd.Parameters.AddWithValue(width); cmd.Parameters.AddWithValue(height);
            cmd.Parameters.AddWithValue(animationSheetJson!); cmd.Parameters.AddWithValue(sourceAtlasArtifactId.Value);
            cmd.Parameters.AddWithValue(job.Fence);
            if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("animation_sheet_freeze_conflict_or_provenance");
        }
        // The successful G attempt may belong to a previous job fence after resume.
        // Its native output and completion are immutable; do not finish it a second time.
        await MoveInTransactionAsync(conn, tx, job, "resolving_geometry", ct);
        await tx.CommitAsync(ct);
    }

    private static void ValidateAnimationSheetMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 1024) throw new InvalidDataException("animation_sheet_metadata");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4 ||
            !root.TryGetProperty("layout_version", out var layout) || layout.ValueKind != JsonValueKind.String || layout.GetString() != "sp.animation-sheet/1.0" ||
            !root.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Number || !rows.TryGetInt32(out var rowCount) || rowCount != 3 ||
            !root.TryGetProperty("columns", out var columns) || columns.ValueKind != JsonValueKind.Number || !columns.TryGetInt32(out var columnCount) || columnCount != 7 ||
            !root.TryGetProperty("ending_basis", out var ending) || ending.ValueKind != JsonValueKind.String || ending.GetString() is not ("contact" or "expiration"))
            throw new InvalidDataException("animation_sheet_metadata");
    }
}
