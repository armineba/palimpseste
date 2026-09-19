using Npgsql;

namespace Palimpseste.Api;

public enum GenerationQuotaDecision
{
    Allowed,
    PrincipalExceeded,
    GlobalExceeded
}

public static class GenerationQuota
{
    // This lock serializes admission across every API process using the same database.
    // The subsequent COUNT runs under PostgreSQL READ COMMITTED after the lock is held,
    // so it sees jobs committed by the previous admission transaction.
    public static async Task<GenerationQuotaDecision> CheckAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid principalId,
        ApiConfig config, CancellationToken ct)
    {
        await using (var gate = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended('palimpseste:generation-admission:v1',0))",
            connection, transaction))
            await gate.ExecuteNonQueryAsync(ct);

        await using var count = new NpgsqlCommand("""
            SELECT COUNT(*) FILTER (WHERE owner_id=@owner), COUNT(*)
            FROM jobs
            WHERE created_at > statement_timestamp() - INTERVAL '24 hours'
            """, connection, transaction);
        count.Parameters.AddWithValue("owner", principalId);
        await using var reader = await count.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Generation quota count unavailable.");
        var forPrincipal = reader.GetInt64(0);
        var global = reader.GetInt64(1);
        if (global >= config.NewGenerationsGlobal24h) return GenerationQuotaDecision.GlobalExceeded;
        if (forPrincipal >= config.NewGenerationsPerPrincipal24h) return GenerationQuotaDecision.PrincipalExceeded;
        return GenerationQuotaDecision.Allowed;
    }
}
