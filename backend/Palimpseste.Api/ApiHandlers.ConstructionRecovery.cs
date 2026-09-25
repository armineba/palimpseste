using Npgsql;
using Palimpseste.Storage;

namespace Palimpseste.Api;

public static partial class ApiHandlers
{
    // This returns a new, unused window only when the deployed construction policy changed.
    // ResumeJob calls it while holding the job row lock, then records the version and base in
    // that same transaction. GET merely advertises eligibility and never changes the job.
    private static async Task<int?> OwnerV2ConstructionRecoveryBaseAsync(Guid jobId, Guid ownerId,
        NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("""
            SELECT next_window.revision_base
            FROM jobs j
            CROSS JOIN LATERAL (
                SELECT coalesce(max(history.revision),-1)+1 AS revision_base
                FROM (
                    SELECT p.revision FROM spell_v2_passes p WHERE p.job_id=j.id
                    UNION ALL
                    SELECT p.revision FROM spell_plans p WHERE p.job_id=j.id
                ) history
            ) next_window
            JOIN LATERAL (
                SELECT a.stage,a.status,a.output_sha256,a.error_code,a.finished_at
                FROM provider_attempts a WHERE a.job_id=j.id AND a.stage IN ('B','J')
                ORDER BY a.started_at DESC,a.id DESC LIMIT 1
            ) latest ON true
            WHERE j.id=@id AND j.owner_id=@owner AND j.kind='production' AND j.visual_pipeline_version=5
              AND j.state='needs_operator' AND j.error_code='v2_validation_rejected'
              AND j.attempt_count<@attempt_budget AND j.spell_id IS NULL
              AND j.lease_until IS NULL AND j.leased_by IS NULL
              AND next_window.revision_base>j.v2_revision_base
              AND next_window.revision_base BETWEEN 1 AND @max_revision_base
              AND (
                  (j.v2_builder_version IS NOT NULL AND j.v2_builder_version<>@builder_version)
                  OR (j.v2_builder_version IS NULL AND NOT EXISTS (
                      SELECT 1 FROM spell_plans p WHERE p.job_id=j.id
                        AND p.prompt_version NOT IN ('sp.prompt.blueprint/2.0','sp.prompt.blueprint/2.1')
                  ))
              )
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
              AND EXISTS (
                  SELECT 1 FROM spell_plans p JOIN artifacts a ON a.id=p.plan_artifact_id
                  WHERE p.job_id=j.id AND a.owner_id=j.owner_id AND a.kind='plan'
                    AND a.content_type='application/json' AND a.sha256=p.plan_sha256
              )
              AND NOT EXISTS (SELECT 1 FROM spells s WHERE s.job_id=j.id OR s.parchment_id=j.parchment_id)
              AND NOT EXISTS (
                  SELECT 1 FROM provider_attempts a WHERE a.job_id=j.id
                    AND a.status IN ('running','transport_uncertain')
              )
              AND latest.finished_at IS NOT NULL AND latest.output_sha256 IS NOT NULL
              AND (
                  (latest.status='success' AND latest.error_code IS NULL)
                  OR (latest.stage='B' AND latest.status='invalid' AND latest.error_code IN (
                      'v2_blueprint_invalid','v2_locked_stage_changed','v2_methods_invalid',
                      'unity_god_build_invalid','unity_god_design_invalid'
                  ))
              )
            """, connection, transaction);
        query.Parameters.AddWithValue("id", jobId);
        query.Parameters.AddWithValue("owner", ownerId);
        query.Parameters.AddWithValue("attempt_budget", V2ConstructionPolicy.AttemptBudget);
        query.Parameters.AddWithValue("max_revision_base", V2ConstructionPolicy.MaxRevisionBase);
        query.Parameters.AddWithValue("builder_version", V2ConstructionPolicy.Version);
        return await query.ExecuteScalarAsync(ct) is int revisionBase ? revisionBase : null;
    }
}
