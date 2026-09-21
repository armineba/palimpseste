using System.Text;
using System.Text.Json;
using Npgsql;
using Palimpseste.Core;
using Palimpseste.Provider;
using Palimpseste.Storage;

namespace Palimpseste.Worker;

public sealed record StoredVisualReview(int Score, bool LifecycleFaithful, string VerdictJson);
public sealed record StoredPlanProviderIdentity(string? Model, string? SessionId, string PromptVersion);

public sealed partial class JobRepository
{
    private static readonly string[] DefinitiveVisualJudgementErrors =
    [
        "visual_judgement_invalid", "visual_judgement_keys", "visual_judgement_description_hash",
        "visual_judgement_plan_hash", "visual_judgement_reference_hash", "visual_judgement_score_range",
        "visual_judgement_score_sum", "visual_judgement_issues_count", "visual_judgement_issue_text"
    ];

    public async Task<StoredVisualReview?> GetVisualReviewAsync(ClaimedJob job, string planSha256, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT r.score,r.lifecycle_faithful,r.verdict::text
            FROM visual_reviews r
            JOIN jobs j ON j.id=r.job_id
            JOIN provider_attempts pa ON pa.id=r.provider_attempt_id AND pa.job_id=j.id
            JOIN artifacts v ON v.id=r.verdict_artifact_id AND v.owner_id=j.owner_id
            JOIN artifacts c ON c.id=r.capture_artifact_id AND c.owner_id=j.owner_id
            WHERE r.job_id=$1 AND j.fence_token=$2 AND r.plan_sha256=$3
              AND pa.stage='J' AND pa.status='success'
              AND v.kind='visual_review' AND c.kind='visual_capture'
              AND EXISTS (SELECT 1 FROM spell_plans p JOIN artifacts a ON a.id=p.plan_artifact_id
                  WHERE p.job_id=j.id AND p.plan_sha256=r.plan_sha256 AND p.validation_errors IS NULL
                    AND a.owner_id=j.owner_id AND a.sha256=p.plan_sha256 AND a.kind='plan')
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(planSha256);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetInt32(0), reader.GetBoolean(1), reader.GetString(2)) : null;
    }

    public async Task SaveVisualReviewAsync(ClaimedJob job, Guid attemptId, string planSha256, int score,
        bool lifecycleFaithful, StoredArtifact verdictArtifact, StoredArtifact captureArtifact,
        string verdictJson, CodexResult transport, CancellationToken ct)
    {
        if (job.VisualPipelineVersion < 2 || job.Kind != "production")
            throw new InvalidOperationException("visual_review_pipeline_required");
        if (score is < 0 or > 10000) throw new ArgumentOutOfRangeException(nameof(score));
        if (planSha256.Length != 64 || planSha256.Any(c => !char.IsAsciiHexDigitLower(c)))
            throw new ArgumentException("Invalid plan digest", nameof(planSha256));
        var verdictBytes = Encoding.UTF8.GetBytes(verdictJson);
        if (verdictArtifact.ContentType != "application/json" || verdictArtifact.ByteLength != verdictBytes.Length ||
            verdictArtifact.Sha256 != SpellCompiler.Sha256(verdictBytes))
            throw new InvalidDataException("visual_review_artifact_mismatch");
        using (var verdict = JsonDocument.Parse(verdictJson))
            if (verdict.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("visual_review_object_required");

        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await LockUnpublishedJobAsync(conn, tx, job, ct);
        await using (var gate = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM jobs j
                JOIN provider_attempts pa ON pa.job_id=j.id
                WHERE j.id=$1 AND j.fence_token=$2 AND j.visual_pipeline_version>=2 AND j.kind='production'
                  AND pa.id=$3 AND pa.fence_token=$2 AND pa.stage='J' AND pa.status='running'
                  AND EXISTS (SELECT 1 FROM spell_plans p JOIN artifacts a ON a.id=p.plan_artifact_id
                      WHERE p.job_id=j.id AND p.plan_sha256=$4 AND p.validation_errors IS NULL
                        AND a.owner_id=j.owner_id AND a.sha256=p.plan_sha256 AND a.kind='plan')
            )
            """, conn, tx))
        {
            gate.Parameters.AddWithValue(job.Id);
            gate.Parameters.AddWithValue(job.Fence);
            gate.Parameters.AddWithValue(attemptId);
            gate.Parameters.AddWithValue(planSha256);
            if (await gate.ExecuteScalarAsync(ct) is not true)
                throw new InvalidOperationException("visual_review_attempt_or_plan_not_owned");
        }
        await InsertArtifactAsync(conn, tx, job, verdictArtifact, "visual_review", ct);
        await InsertArtifactAsync(conn, tx, job, captureArtifact, "visual_capture", ct);
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO visual_reviews(job_id,plan_sha256,provider_attempt_id,score,lifecycle_faithful,
                verdict,verdict_artifact_id,capture_artifact_id)
            VALUES($1,$2,$3,$4,$5,$6::jsonb,$7,$8)
            ON CONFLICT(job_id,plan_sha256) DO NOTHING
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(job.Id);
            cmd.Parameters.AddWithValue(planSha256);
            cmd.Parameters.AddWithValue(attemptId);
            cmd.Parameters.AddWithValue(score);
            cmd.Parameters.AddWithValue(lifecycleFaithful);
            cmd.Parameters.AddWithValue(verdictJson);
            cmd.Parameters.AddWithValue(verdictArtifact.Id);
            cmd.Parameters.AddWithValue(captureArtifact.Id);
            if (await cmd.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("visual_review_already_frozen");
        }
        await CompleteSuccessfulAttemptInTransactionAsync(conn, tx, job, attemptId, verdictArtifact.Sha256, transport, ct);
        await MoveInTransactionAsync(conn, tx, job, "refining_visuals", ct);
        await tx.CommitAsync(ct);
    }

    public async Task<int> CountTotalAttemptsAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT greatest(j.attempt_count,count(a.id)) FROM jobs j
            LEFT JOIN provider_attempts a ON a.job_id=j.id
            WHERE j.id=$1 AND j.fence_token=$2 GROUP BY j.id,j.attempt_count
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Fence);
        var count = await cmd.ExecuteScalarAsync(ct);
        if (count is null) throw new InvalidOperationException("fence_lost_on_attempt_count");
        return checked((int)(long)count);
    }

    public async Task<int> CountVisualReviewsAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT count(r.job_id) FROM jobs j LEFT JOIN visual_reviews r ON r.job_id=j.id
            WHERE j.id=$1 AND j.fence_token=$2 GROUP BY j.id
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Fence);
        var count = await cmd.ExecuteScalarAsync(ct);
        if (count is null) throw new InvalidOperationException("fence_lost_on_visual_review_count");
        return checked((int)(long)count);
    }

    public async Task<StoredDocument?> GetReviewedFallbackAfterInvalidJudgeAsync(ClaimedJob job,
        string currentPlanSha256, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT EXISTS (
                SELECT 1 FROM jobs j
                JOIN spell_plans current_plan ON current_plan.job_id=j.id AND current_plan.plan_sha256=$3
                    AND current_plan.validation_errors IS NULL
                JOIN artifacts current_artifact ON current_artifact.id=current_plan.plan_artifact_id
                    AND current_artifact.owner_id=j.owner_id AND current_artifact.sha256=current_plan.plan_sha256
                    AND current_artifact.kind='plan' AND current_artifact.content_type='application/json'
                JOIN provider_attempts planner ON planner.id=current_plan.provider_attempt_id AND planner.job_id=j.id
                    AND planner.stage IN ('B','repair_B') AND planner.status='success'
                JOIN LATERAL (
                    SELECT pa.stage,pa.status,pa.error_code,pa.started_at,pa.finished_at
                    FROM provider_attempts pa WHERE pa.job_id=j.id
                    ORDER BY pa.started_at DESC,pa.id DESC LIMIT 1
                ) latest ON true
                WHERE j.id=$1 AND j.fence_token=$2 AND j.kind='production' AND j.visual_pipeline_version>=2
                  AND j.spell_id IS NULL AND NOT EXISTS(SELECT 1 FROM spells published WHERE published.job_id=j.id)
                  AND latest.stage='J' AND latest.status='invalid' AND latest.error_code=ANY($4)
                  AND latest.finished_at IS NOT NULL AND latest.started_at>current_plan.created_at
                  AND NOT EXISTS(SELECT 1 FROM spell_plans newer WHERE newer.job_id=j.id
                      AND newer.revision>current_plan.revision AND newer.validation_errors IS NULL)
                  AND NOT EXISTS(SELECT 1 FROM provider_attempts uncertain WHERE uncertain.job_id=j.id
                      AND uncertain.status IN ('running','transport_uncertain'))
            )
            """);
        cmd.Parameters.AddWithValue(job.Id); cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(currentPlanSha256); cmd.Parameters.AddWithValue(DefinitiveVisualJudgementErrors);
        if (await cmd.ExecuteScalarAsync(ct) is not true) return null;
        // The rejected judgement contributes no score, verdict or candidate. The normal
        // selector admits only separately persisted, successfully reviewed plans.
        return await GetBestReviewedPlanAsync(job, ct);
    }

    public async Task<StoredDocument?> GetBestReviewedPlanAsync(ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT a.storage_key,p.plan_sha256,a.id,p.prompt_version,p.revision
            FROM spell_plans p JOIN jobs j ON j.id=p.job_id
            JOIN artifacts a ON a.id=p.plan_artifact_id AND a.owner_id=j.owner_id
            JOIN visual_reviews r ON r.job_id=p.job_id AND r.plan_sha256=p.plan_sha256
            JOIN provider_attempts pa ON pa.id=r.provider_attempt_id AND pa.job_id=j.id
            JOIN artifacts v ON v.id=r.verdict_artifact_id AND v.owner_id=j.owner_id
            JOIN artifacts c ON c.id=r.capture_artifact_id AND c.owner_id=j.owner_id
            WHERE j.id=$1 AND j.fence_token=$2 AND p.validation_errors IS NULL
              AND a.sha256=p.plan_sha256 AND a.kind='plan' AND v.kind='visual_review' AND c.kind='visual_capture'
              AND pa.stage='J' AND pa.status='success'
            ORDER BY r.lifecycle_faithful DESC,r.score DESC,p.revision DESC LIMIT 1
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Fence);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetString(0), reader.GetString(1), reader.GetGuid(2),
            reader.GetString(3), reader.GetInt32(4)) : null;
    }

    public async Task<StoredPlanProviderIdentity?> GetPlanProviderIdentityAsync(ClaimedJob job,
        string planSha256, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT pa.requested_model,pa.session_id,p.prompt_version
            FROM spell_plans p JOIN jobs j ON j.id=p.job_id
            JOIN artifacts a ON a.id=p.plan_artifact_id AND a.owner_id=j.owner_id
            LEFT JOIN provider_attempts pa ON pa.id=p.provider_attempt_id AND pa.job_id=j.id
                AND pa.stage IN ('B','repair_B') AND pa.status='success'
            WHERE j.id=$1 AND j.fence_token=$2 AND p.plan_sha256=$3
              AND p.validation_errors IS NULL AND a.sha256=p.plan_sha256 AND a.kind='plan'
            ORDER BY p.revision DESC LIMIT 1
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Fence);
        cmd.Parameters.AddWithValue(planSha256);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2)) : null;
    }

    private static async Task LockUnpublishedJobAsync(NpgsqlConnection conn, NpgsqlTransaction tx,
        ClaimedJob job, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT j.id FROM jobs j WHERE j.id=$1 AND j.fence_token=$2
              AND j.state NOT IN ('ready','needs_operator') AND j.spell_id IS NULL
              AND NOT EXISTS (SELECT 1 FROM spells s WHERE s.job_id=j.id)
            FOR UPDATE
            """, conn, tx);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Fence);
        if (await cmd.ExecuteScalarAsync(ct) is null)
            throw new InvalidOperationException("job_published_or_fence_lost");
    }
}
