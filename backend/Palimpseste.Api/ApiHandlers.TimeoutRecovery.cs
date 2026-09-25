using Npgsql;
using Palimpseste.Storage;

namespace Palimpseste.Api;

public static partial class ApiHandlers
{
    // Historical D22 provenance: the trusted runner emits attempt_timeout only for its own
    // deadline, after Kill(entireProcessTree:true) and WaitForExitAsync. The worker records
    // that result through CompleteAttemptAsync under the current job fence. External
    // cancellation has a different error/status; refusals and isolation failures do too.
    // Older runners did not finish attempt.json on this branch: trust this exact terminal
    // database marker, never invent a retroactive disk attestation or infer it from a PID.
    // GET only advertises this option; POST reevaluates it under the existing job row lock.
    private static async Task<bool> CanOwnerResumeV2BTimeoutAsync(Guid jobId, Guid ownerId,
        NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        // Like the other narrowly scoped owner recoveries, this retries only
        // the authenticated owner's controlled data job. It grants no creator
        // privilege and must work for the normal player identity used by Unity.
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM jobs j
                JOIN LATERAL (
                    SELECT a.stage,a.status,a.error_code,a.output_sha256,a.input_sha256,
                           a.fence_token,a.started_at,a.finished_at
                    FROM provider_attempts a WHERE a.job_id=j.id
                    ORDER BY a.started_at DESC,a.id DESC LIMIT 1
                ) latest ON true
                WHERE j.id=@id AND j.owner_id=@owner AND j.kind='production' AND j.visual_pipeline_version=5
                  AND j.state='needs_operator' AND j.error_code='attempt_timeout' AND j.resume_stage='planning'
                  AND j.v2_builder_version=@builder_version AND j.attempt_count<@attempt_budget
                  AND j.lease_until IS NULL AND j.leased_by IS NULL AND j.spell_id IS NULL
                  AND latest.stage='B' AND latest.status='invalid' AND latest.error_code='attempt_timeout'
                  AND latest.fence_token=j.fence_token AND latest.output_sha256 IS NULL
                  AND latest.input_sha256 IS NOT NULL AND latest.finished_at IS NOT NULL
                  AND latest.finished_at>=latest.started_at
                  AND NOT EXISTS (
                      SELECT 1 FROM provider_attempts a WHERE a.job_id=j.id
                        AND a.status IN ('running','transport_uncertain')
                  )
                  AND NOT EXISTS (SELECT 1 FROM spells s WHERE s.job_id=j.id OR s.parchment_id=j.parchment_id)
                  AND EXISTS (
                      SELECT 1 FROM interpretations i JOIN artifacts a ON a.id=i.description_artifact_id
                      WHERE i.job_id=j.id AND a.owner_id=j.owner_id AND a.kind='description'
                        AND a.content_type='application/json' AND a.sha256=i.description_sha256
                  )
                  AND EXISTS (
                      SELECT 1 FROM spell_v2_passes r JOIN artifacts a ON a.id=r.artifact_id
                      JOIN interpretations i ON i.job_id=r.job_id
                      WHERE r.job_id=j.id AND r.pass='research' AND r.accepted
                        AND r.input_sha256=i.description_sha256 AND a.owner_id=j.owner_id
                        AND a.kind='v2_research' AND a.content_type='application/json'
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM spell_plans p LEFT JOIN artifacts a ON a.id=p.plan_artifact_id
                      WHERE p.job_id=j.id AND (a.id IS NULL OR a.owner_id IS DISTINCT FROM j.owner_id
                        OR a.kind<>'plan' OR a.content_type<>'application/json' OR a.sha256<>p.plan_sha256)
                  )
                  AND EXISTS (
                      SELECT 1 FROM generate_series(j.v2_revision_base,j.v2_revision_base+@window_size-1) slot(revision)
                      WHERE slot.revision>=coalesce((SELECT max(p.revision) FROM spell_plans p WHERE p.job_id=j.id),j.v2_revision_base)
                        AND NOT EXISTS (SELECT 1 FROM spell_plans p WHERE p.job_id=j.id AND p.revision=slot.revision)
                        AND NOT EXISTS (SELECT 1 FROM spell_v2_passes p WHERE p.job_id=j.id
                          AND p.revision=slot.revision AND p.pass IN ('blueprint','blueprint_recovery'))
                  )
            )
            """, connection, transaction);
        query.Parameters.AddWithValue("id", jobId);
        query.Parameters.AddWithValue("owner", ownerId);
        query.Parameters.AddWithValue("builder_version", V2ConstructionPolicy.Version);
        query.Parameters.AddWithValue("attempt_budget", V2ConstructionPolicy.AttemptBudget);
        query.Parameters.AddWithValue("window_size", V2ConstructionPolicy.RevisionsPerWindow);
        return await query.ExecuteScalarAsync(ct) is true;
    }
}
