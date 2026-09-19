using Npgsql;
using Palimpseste.Api;

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException("DATABASE_URL is required for the isolated test database.");
var builder = new NpgsqlConnectionStringBuilder(connectionString);
if (builder.Database != "palimpseste_test")
    throw new InvalidOperationException("Generation quota smoke only runs on palimpseste_test.");

var schema = "quota_test_" + Guid.NewGuid().ToString("N");
var principalA = Guid.NewGuid();
var principalB = Guid.NewGuid();
var config = new ApiConfig
{
    ConnectionString = connectionString,
    ArtifactRoot = "unused",
    ReferencePath = "unused",
    CatalogPath = "unused",
    NewGenerationsPerPrincipal24h = 2,
    NewGenerationsGlobal24h = 3
};
var principalOnlyConfig = new ApiConfig
{
    ConnectionString = connectionString,
    ArtifactRoot = "unused",
    ReferencePath = "unused",
    CatalogPath = "unused",
    NewGenerationsPerPrincipal24h = 1,
    NewGenerationsGlobal24h = 10
};
await using var dataSource = NpgsqlDataSource.Create(connectionString);

async Task<NpgsqlConnection> OpenAsync()
{
    var connection = await dataSource.OpenConnectionAsync();
    // schema contains only characters generated above and cannot carry user input.
    await using var setPath = new NpgsqlCommand($"SET search_path TO {schema}", connection);
    await setPath.ExecuteNonQueryAsync();
    return connection;
}

async Task<GenerationQuotaDecision> AdmitAsync(Guid owner, bool rollback = false)
{
    await using var connection = await OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
    var decision = await GenerationQuota.CheckAsync(connection, transaction, owner, config, CancellationToken.None);
    if (decision == GenerationQuotaDecision.Allowed)
    {
        await using var insert = new NpgsqlCommand(
            "INSERT INTO jobs(id,owner_id,created_at) VALUES (@id,@owner,clock_timestamp())", connection, transaction);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("owner", owner);
        await insert.ExecuteNonQueryAsync();
        // Hold the database transaction lock briefly to expose admission races.
        await Task.Delay(20);
        if (!rollback) await transaction.CommitAsync();
    }
    return decision;
}

try
{
    await using (var connection = await dataSource.OpenConnectionAsync())
    {
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", connection))
            await create.ExecuteNonQueryAsync();
        await using (var table = new NpgsqlCommand($"CREATE TABLE {schema}.jobs (id uuid PRIMARY KEY, owner_id uuid NOT NULL, created_at timestamptz NOT NULL)", connection))
            await table.ExecuteNonQueryAsync();
    }

    await using (var connection = await OpenAsync())
    await using (var oldJob = new NpgsqlCommand(
        "INSERT INTO jobs(id,owner_id,created_at) VALUES (@id,@owner,clock_timestamp() - interval '25 hours')", connection))
    {
        oldJob.Parameters.AddWithValue("id", Guid.NewGuid());
        oldJob.Parameters.AddWithValue("owner", principalA);
        await oldJob.ExecuteNonQueryAsync();
    }

    if (await AdmitAsync(principalA, rollback: true) != GenerationQuotaDecision.Allowed)
        throw new Exception("A rolled-back admission was rejected despite an empty window.");

    var owners = Enumerable.Range(0, 20).Select(index => index % 2 == 0 ? principalA : principalB).ToArray();
    var decisions = await Task.WhenAll(owners.Select(owner => AdmitAsync(owner)));
    if (decisions.Count(item => item == GenerationQuotaDecision.Allowed) != 3)
        throw new Exception("Concurrent global admission exceeded or undershot three jobs.");

    await using (var connection = await OpenAsync())
    await using (var count = new NpgsqlCommand(
        "SELECT owner_id, count(*) FROM jobs WHERE created_at > clock_timestamp() - interval '24 hours' GROUP BY owner_id", connection))
    await using (var reader = await count.ExecuteReaderAsync())
    {
        long total = 0;
        while (await reader.ReadAsync())
        {
            var ownerCount = reader.GetInt64(1);
            if (ownerCount > 2) throw new Exception("Concurrent principal admission exceeded two jobs.");
            total += ownerCount;
        }
        if (total != 3) throw new Exception("Committed jobs do not match three allowed admissions.");
    }

    var acceptedIndex = Array.FindIndex(decisions, item => item == GenerationQuotaDecision.Allowed);
    if (acceptedIndex < 0) throw new Exception("No admitted principal to probe.");
    await using (var connection = await OpenAsync())
    await using (var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
    {
        var decision = await GenerationQuota.CheckAsync(connection, transaction,
            owners[acceptedIndex], principalOnlyConfig, CancellationToken.None);
        if (decision != GenerationQuotaDecision.PrincipalExceeded)
            throw new Exception("A principal bypassed the individual window.");
    }

    await using (var connection = await OpenAsync())
    await using (var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
    {
        var decision = await GenerationQuota.CheckAsync(connection, transaction, Guid.NewGuid(), config, CancellationToken.None);
        if (decision != GenerationQuotaDecision.GlobalExceeded)
            throw new Exception("A new principal bypassed the full global window.");
    }
    Console.WriteLine("Generation quota DB smoke: 20 concurrent admissions, 3 committed, per-principal <=2, individual and global overflow rejected, expired and rolled-back jobs ignored.");
}
finally
{
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schema} CASCADE", connection);
    await drop.ExecuteNonQueryAsync();
}
