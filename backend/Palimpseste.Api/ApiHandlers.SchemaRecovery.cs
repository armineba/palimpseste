using Npgsql;
using Palimpseste.Storage;

namespace Palimpseste.Api;

public static partial class ApiHandlers
{
    private static async Task<bool> CanOwnerResumeV2SchemaFailureAsync(Guid jobId, Guid ownerId,
        NpgsqlConnection connection, NpgsqlTransaction? transaction, ApiConfig config,
        IArtifactStore store, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.AttemptRoot)) return false;
        var attempts = new List<(Guid Id, string Input, string Status, string? Output, string? Error)>();
        string? jobError = null;
        await using (var query = new NpgsqlCommand("""
            SELECT pa.id,pa.input_sha256,pa.status,pa.output_sha256,pa.error_code,j.error_code
            FROM jobs j JOIN provider_attempts pa ON pa.job_id=j.id AND pa.stage='B'
            WHERE j.id=@id AND j.owner_id=@owner AND j.kind='production' AND j.visual_pipeline_version=5
              AND j.state='needs_operator' AND j.error_code IN ('v2_validation_rejected','codex_output_schema_rejected')
              AND j.attempt_count<32 AND j.spell_id IS NULL AND j.lease_until IS NULL AND j.leased_by IS NULL
              AND EXISTS(SELECT 1 FROM interpretations i JOIN artifacts a ON a.id=i.description_artifact_id
                  WHERE i.job_id=j.id AND a.owner_id=j.owner_id AND a.kind='description'
                    AND a.content_type='application/json' AND a.sha256=i.description_sha256)
              AND EXISTS(SELECT 1 FROM spell_v2_passes r WHERE r.job_id=j.id AND r.pass='research' AND r.accepted)
              AND NOT EXISTS(SELECT 1 FROM spell_plans p WHERE p.job_id=j.id)
              AND NOT EXISTS(SELECT 1 FROM spells s WHERE s.job_id=j.id)
              AND NOT EXISTS(SELECT 1 FROM provider_attempts a WHERE a.job_id=j.id AND a.status IN ('running','transport_uncertain'))
              AND NOT EXISTS(SELECT 1 FROM spell_v2_passes p WHERE p.job_id=j.id AND p.pass='blueprint_recovery')
            ORDER BY pa.started_at
            """, connection, transaction))
        {
            query.Parameters.AddWithValue("id", jobId); query.Parameters.AddWithValue("owner", ownerId);
            await using var reader = await query.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                attempts.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
                jobError = reader.GetString(5);
            }
        }
        if (attempts.Count == 0 || attempts.Count > 31) return false;
        foreach (var attempt in attempts)
            if (!await SchemaFailureEvidence.VerifyAsync(config.AttemptRoot, config.SpecificationRoot,
                jobId, attempt.Id, attempt.Status, attempt.Output, attempt.Error, ct)) return false;

        var placeholders = new List<(string Input, string Key, string Sha)>();
        await using (var query = new NpgsqlCommand("""
            SELECT p.input_sha256,p.accepted,a.storage_key,a.sha256,a.owner_id,a.kind,a.content_type
            FROM spell_v2_passes p LEFT JOIN artifacts a ON a.id=p.artifact_id
            WHERE p.job_id=@id AND p.pass='blueprint'
            """, connection, transaction))
        {
            query.Parameters.AddWithValue("id", jobId);
            await using var reader = await query.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (reader.GetBoolean(1) || reader.IsDBNull(2) || reader.IsDBNull(4) || reader.GetGuid(4) != ownerId ||
                    reader.GetString(5) != "v2_blueprint" || reader.GetString(6) != "application/json") return false;
                placeholders.Add((reader.GetString(0), reader.GetString(2), reader.GetString(3)));
            }
        }
        if (jobError == "v2_validation_rejected" && placeholders.Count == 0) return false;
        foreach (var pass in placeholders)
        {
            if (!attempts.Any(a => a.Input == pass.Input)) return false;
            try
            {
                if (!SchemaFailureEvidence.IsLegacyPlaceholder(await store.ReadAsync(pass.Key, ct), pass.Sha)) return false;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
        }
        return true;
    }
}
