using Npgsql;
using Palimpseste.Provider;
using Palimpseste.Storage;
using Palimpseste.Worker;

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? throw new InvalidOperationException("DATABASE_URL required");
var parsed = new NpgsqlConnectionStringBuilder(connectionString);
if (parsed.Database != "palimpseste_test") throw new InvalidOperationException("This test only runs on palimpseste_test");
await using var db = NpgsqlDataSource.Create(connectionString);
var repository = new JobRepository(db);
var principal = Guid.NewGuid(); var parchment = Guid.NewGuid(); var capture = Guid.NewGuid();
var job = Guid.NewGuid(); var retryJob = Guid.NewGuid(); var recoveryJob = Guid.NewGuid();
var reference = Guid.NewGuid(); var drawing = Guid.NewGuid();
var ink = Guid.NewGuid(); var journal = Guid.NewGuid();
var recoveryParchment = Guid.NewGuid(); var recoveryCapture = Guid.NewGuid();
var recoveryDescriptionArtifact = Guid.NewGuid();
var recoveryImageArtifact = Guid.NewGuid(); var replacementImageArtifact = Guid.NewGuid();
var artifacts = new[] { reference, drawing, ink, journal };
var cleanupArtifacts = new[] { reference, drawing, ink, journal, recoveryDescriptionArtifact, recoveryImageArtifact, replacementImageArtifact };
void Require(bool value, string message) { if (!value) throw new Exception(message); }

await using (var conn = await db.OpenConnectionAsync())
await using (var tx = await conn.BeginTransactionAsync())
{
    await using (var cmd = new NpgsqlCommand("INSERT INTO lab_principals(id,role,label) VALUES($1,'player','db-smoke')", conn, tx))
    { cmd.Parameters.AddWithValue(principal); await cmd.ExecuteNonQueryAsync(); }
    await using (var cmd = new NpgsqlCommand("INSERT INTO parchments(id,owner_id,state,layout_version,budget_micro_units,signature_seed_hex) VALUES($1,$2,'processing','three_regions_v1',1000000000,'0123456789abcdef')", conn, tx))
    { cmd.Parameters.AddWithValue(parchment); cmd.Parameters.AddWithValue(principal); await cmd.ExecuteNonQueryAsync(); }
    foreach (var id in artifacts)
    {
        await using var cmd = new NpgsqlCommand("INSERT INTO artifacts(id,owner_id,kind,storage_key,sha256,content_type,byte_length) VALUES($1,$2,'test',$3,$4,'application/octet-stream',1)", conn, tx);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(id == reference ? DBNull.Value : principal);
        cmd.Parameters.AddWithValue("aa/" + id.ToString("N") + ".bin");
        cmd.Parameters.AddWithValue(new string('a', 64));
        await cmd.ExecuteNonQueryAsync();
    }
    await using (var cmd = new NpgsqlCommand("""
        INSERT INTO captures(id,parchment_id,owner_id,manifest,manifest_sha256,request_sha256,
            drawing_artifact_id,ink_artifact_id,journal_artifact_id,reference_artifact_id)
        VALUES($1,$2,$3,'{}'::jsonb,$4,$4,$5,$6,$7,$8)
        """, conn, tx))
    {
        cmd.Parameters.AddWithValue(capture); cmd.Parameters.AddWithValue(parchment); cmd.Parameters.AddWithValue(principal);
        cmd.Parameters.AddWithValue(new string('b', 64)); cmd.Parameters.AddWithValue(drawing);
        cmd.Parameters.AddWithValue(ink); cmd.Parameters.AddWithValue(journal); cmd.Parameters.AddWithValue(reference);
        await cmd.ExecuteNonQueryAsync();
    }
    await using (var cmd = new NpgsqlCommand("INSERT INTO jobs(id,owner_id,parchment_id,capture_id,kind,state) VALUES($1,$2,$3,$4,'production','queued')", conn, tx))
    { cmd.Parameters.AddWithValue(job); cmd.Parameters.AddWithValue(principal); cmd.Parameters.AddWithValue(parchment); cmd.Parameters.AddWithValue(capture); await cmd.ExecuteNonQueryAsync(); }
    await tx.CommitAsync();
}
try
{
    var first = await repository.ClaimAsync("smoke-one", CancellationToken.None);
    Require(first?.Id == job && first.Fence == 1, "First lease/fence incorrect");
    Require(await repository.ClaimAsync("smoke-two", CancellationToken.None) is null, "Leased job claimed twice");
    Require(await repository.RenewAsync(first!, "smoke-one", CancellationToken.None), "Lease renewal failed");
    var initial = await repository.BeginAttemptAsync(first!, "A", "gpt-5.6-luna", "max", new string('c', 64), CancellationToken.None);
    await repository.CompleteAttemptAsync(first!, initial, "invalid", null, "cli", null, null, "invalid_schema", CancellationToken.None);
    Require(await repository.CountAttemptsAsync(first!, "repair_A", CancellationToken.None) == 0, "Initial attempt counted as repair");
    var repairOne = await repository.BeginAttemptAsync(first!, "repair_A", "gpt-5.6-luna", "max", new string('c', 64), CancellationToken.None);
    await repository.CompleteAttemptAsync(first!, repairOne, "invalid", null, "cli", null, null, "invalid_schema", CancellationToken.None);
    var attempt = await repository.BeginAttemptAsync(first!, "repair_A", "gpt-5.6-luna", "max", new string('c', 64), CancellationToken.None);
    Require(await repository.CountAttemptsAsync(first!, "repair_A", CancellationToken.None) == 2, "Repair count was not durable");
    Require(await repository.HasUncertainAttemptAsync(first!, CancellationToken.None), "Running attempt not marked uncertain");
    await using (var conn = await db.OpenConnectionAsync())
    await using (var expire = new NpgsqlCommand("UPDATE jobs SET lease_until=now()-interval '1 second' WHERE id=$1", conn))
    { expire.Parameters.AddWithValue(job); await expire.ExecuteNonQueryAsync(); }
    var second = await repository.ClaimAsync("smoke-two", CancellationToken.None);
    Require(second?.Id == job && second.Fence == 2, "Reclaim did not advance fencing token");
    Require(!await repository.RenewAsync(first!, "smoke-one", CancellationToken.None), "Stale worker renewed lease");
    var oldFenced = false;
    try { await repository.SetStateAsync(first!, "ready", null, "", false, CancellationToken.None); }
    catch (InvalidOperationException) { oldFenced = true; }
    Require(oldFenced, "Stale worker changed job state");
    oldFenced = false;
    try { await repository.CompleteAttemptAsync(first!, attempt, "success", new string('d', 64), "cli", null, null, null, CancellationToken.None); }
    catch (InvalidOperationException) { oldFenced = true; }
    Require(oldFenced, "Stale worker completed provider attempt");
    Require(await repository.HasUncertainAttemptAsync(second!, CancellationToken.None), "Uncertain attempt disappeared after reclaim");
    await repository.SetStateAsync(second!, "needs_operator", "uncertain_provider_attempt", "À rapprocher", false, CancellationToken.None);
    await using (var conn = await db.OpenConnectionAsync())
    await using (var check = new NpgsqlCommand("SELECT j.state,j.attempt_count,p.state FROM jobs j JOIN parchments p ON p.id=j.parchment_id WHERE j.id=$1", conn))
    {
        check.Parameters.AddWithValue(job);
        await using var r = await check.ExecuteReaderAsync();
        Require(await r.ReadAsync() && r.GetString(0) == "needs_operator" && r.GetInt32(1) == 3 && r.GetString(2) == "incident",
            "Uncertain job/parchment state incorrect");
    }
    await using (var conn = await db.OpenConnectionAsync())
    await using (var insert = new NpgsqlCommand("INSERT INTO jobs(id,owner_id,kind,state) VALUES($1,$2,'authoring','queued')", conn))
    { insert.Parameters.AddWithValue(retryJob); insert.Parameters.AddWithValue(principal); await insert.ExecuteNonQueryAsync(); }
    var retryLease = await repository.ClaimAsync("smoke-retry", CancellationToken.None);
    Require(retryLease?.Id == retryJob && retryLease.Fence == 1, "Retry fixture claim failed");
    var failedStart = await repository.BeginAttemptAsync(retryLease!, "B", "gpt-5.6-luna", "max", new string('e', 64), CancellationToken.None);
    await repository.CompleteAttemptAsync(retryLease!, failedStart, "invalid", null, "cli", null, null, "process_start_failed", CancellationToken.None);
    await repository.ScheduleRetryAsync(retryLease!, "B", "process_start_failed", CancellationToken.None);
    Require(await repository.ClaimAsync("smoke-too-early", CancellationToken.None) is null, "Retry was claimed before backoff expired");
    await using (var conn = await db.OpenConnectionAsync())
    await using (var due = new NpgsqlCommand("UPDATE jobs SET next_attempt_at=now()-interval '1 second' WHERE id=$1", conn))
    { due.Parameters.AddWithValue(retryJob); await due.ExecuteNonQueryAsync(); }
    var resumed = await repository.ClaimAsync("smoke-resume", CancellationToken.None);
    Require(resumed?.Id == retryJob && resumed.Fence == 2 && resumed.State == "waiting_retry", "Scheduled retry did not resume with a new fence");

    // Add the recovery fixture only after the existing claim/retry assertions;
    // ClaimAsync deliberately orders all jobs by creation time.
    await using (var conn = await db.OpenConnectionAsync())
    await using (var tx = await conn.BeginTransactionAsync())
    {
        await using (var cmd = new NpgsqlCommand("INSERT INTO parchments(id,owner_id,state,layout_version,budget_micro_units,signature_seed_hex) VALUES($1,$2,'processing','three_regions_v1',1000000000,'fedcba9876543210')", conn, tx))
        { cmd.Parameters.AddWithValue(recoveryParchment); cmd.Parameters.AddWithValue(principal); await cmd.ExecuteNonQueryAsync(); }
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO captures(id,parchment_id,owner_id,manifest,manifest_sha256,request_sha256,
                drawing_artifact_id,ink_artifact_id,journal_artifact_id,reference_artifact_id)
            VALUES($1,$2,$3,'{}'::jsonb,$4,$4,$5,$6,$7,$8)
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(recoveryCapture); cmd.Parameters.AddWithValue(recoveryParchment); cmd.Parameters.AddWithValue(principal);
            cmd.Parameters.AddWithValue(new string('f', 64)); cmd.Parameters.AddWithValue(drawing);
            cmd.Parameters.AddWithValue(ink); cmd.Parameters.AddWithValue(journal); cmd.Parameters.AddWithValue(reference);
            await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = new NpgsqlCommand("INSERT INTO jobs(id,owner_id,parchment_id,capture_id,kind,state) VALUES($1,$2,$3,$4,'production','queued')", conn, tx))
        { cmd.Parameters.AddWithValue(recoveryJob); cmd.Parameters.AddWithValue(principal); cmd.Parameters.AddWithValue(recoveryParchment); cmd.Parameters.AddWithValue(recoveryCapture); await cmd.ExecuteNonQueryAsync(); }
        await tx.CommitAsync();
    }

    // Controlled crash after A: the durable description is visible to the
    // next fence and the stale worker cannot move the job or write B.
    var afterA = await repository.ClaimAsync("recovery-a", CancellationToken.None);
    Require(afterA?.Id == recoveryJob && afterA.Fence == 1 && afterA.State == "interpreting", "After-A recovery claim failed");
    var aAttempt = await repository.BeginAttemptAsync(afterA!, "A", "gpt-5.6-luna", "max", new string('1', 64), CancellationToken.None);
    var descriptionJson = "{\"schema_version\":\"sp.description/1.0\",\"title\":\"controlled recovery\"}";
    var description = new StoredArtifact(recoveryDescriptionArtifact, "dd/" + recoveryDescriptionArtifact.ToString("N") + ".json",
        new string('2', 64), descriptionJson.Length, "application/json");
    var successTransport = new CodexResult(ProviderOutcome.Success, descriptionJson, 0, "synthetic-a", null,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "synthetic", "gpt-5.6-luna", "max", "gpt-5.6-luna", "max", "{}");
    await repository.SaveDescriptionAsync(afterA!, aAttempt, description, descriptionJson, new string('3', 64),
        LunaCodexProvider.PromptAVersion, successTransport, CancellationToken.None);
    Require((await repository.GetDescriptionAsync(afterA!, CancellationToken.None))?.PromptVersion == LunaCodexProvider.PromptAVersion,
        "Description or its prompt version was not frozen after A");
    Require(await repository.GetSuccessfulRequestedModelAsync(afterA!, "A", CancellationToken.None) == "gpt-5.6-luna" &&
        await repository.GetSuccessfulRequestedModelAsync(afterA!, "B", CancellationToken.None) is null,
        "Persisted provider model provenance did not reflect the completed A attempt");
    await using (var conn = await db.OpenConnectionAsync())
    await using (var expire = new NpgsqlCommand("UPDATE jobs SET lease_until=now()-interval '1 second' WHERE id=$1", conn))
    { expire.Parameters.AddWithValue(recoveryJob); await expire.ExecuteNonQueryAsync(); }
    var afterAResume = await repository.ClaimAsync("recovery-a-resume", CancellationToken.None);
    Require(afterAResume?.Id == recoveryJob && afterAResume.Fence == 2 && afterAResume.State == "resolving_geometry",
        "After-A resume did not retain the frozen stage");
    Require((await repository.GetDescriptionAsync(afterAResume!, CancellationToken.None))?.PromptVersion == LunaCodexProvider.PromptAVersion,
        "After-A resume lost the description prompt version");
    var staleRejected = false;
    try { await repository.SetStateAsync(afterA!, "planning", null, "stale", false, CancellationToken.None); }
    catch (InvalidOperationException e) when (e.Message == "fence_lost_on_state_change") { staleRejected = true; }
    Require(staleRejected, "Stale worker changed state after A was persisted");

    // Synthetic image metadata exercises the durable checkpoint only: no
    // image model, PNG renderer, or provider process is invoked by this test.
    Require(afterAResume!.VisualPipelineVersion == 1, "New jobs did not opt into the generated-image pipeline");
    await repository.SetStateAsync(afterAResume, "generating_visual_reference", null, "synthetic G", false, CancellationToken.None);
    var gAttempt = await repository.BeginAttemptAsync(afterAResume, "G", "gpt-6-astra", "max", new string('6', 64), CancellationToken.None);
    var image = new StoredArtifact(recoveryImageArtifact, "ee/" + recoveryImageArtifact.ToString("N") + ".png",
        new string('7', 64), 2048, "image/png");
    staleRejected = false;
    try
    {
        await repository.SaveVisualReferenceAsync(afterA!, gAttempt, image, description.Sha256, new string('6', 64),
            "sp.prompt.g/1.0", 1024, 1024, successTransport, CancellationToken.None);
    }
    catch (InvalidOperationException e) when (e.Message == "fence_lost_on_artifact_insert" ||
        e.Message == "attempt_not_running_or_fence_lost" || e.Message == "fence_lost_on_stage_change")
    { staleRejected = true; }
    Require(staleRejected && await repository.GetVisualReferenceAsync(afterAResume, CancellationToken.None) is null,
        "Stale G writer published reference metadata");
    await using (var conn = await db.OpenConnectionAsync())
    await using (var check = new NpgsqlCommand("SELECT count(*) FROM artifacts WHERE id=$1", conn))
    {
        check.Parameters.AddWithValue(recoveryImageArtifact);
        Require((long)(await check.ExecuteScalarAsync() ?? -1L) == 0, "Stale G rollback left an orphan artifact");
    }
    await repository.SaveVisualReferenceAsync(afterAResume, gAttempt, image, description.Sha256, new string('6', 64),
        "sp.prompt.g/1.0", 1024, 1024, successTransport, CancellationToken.None);
    var frozenImage = await repository.GetVisualReferenceAsync(afterAResume, CancellationToken.None);
    Require(frozenImage?.ArtifactId == image.Id && frozenImage.Sha256 == image.Sha256 &&
        frozenImage.DescriptionSha256 == description.Sha256 && frozenImage.PromptVersion == "sp.prompt.g/1.0" &&
        frozenImage.Width == 1024 && frozenImage.Height == 1024 && frozenImage.SizeBytes == 2048,
        "G checkpoint lost image identity, dimensions, or description provenance");
    var replacementImage = new StoredArtifact(replacementImageArtifact, "ef/" + replacementImageArtifact.ToString("N") + ".png",
        new string('8', 64), 4096, "image/png");
    var replacementRejected = false;
    try
    {
        await repository.SaveVisualReferenceAsync(afterAResume, gAttempt, replacementImage, description.Sha256, new string('6', 64),
            "sp.prompt.g/1.0", 1024, 1024, successTransport, CancellationToken.None);
    }
    catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation) { replacementRejected = true; }
    Require(replacementRejected &&
        (await repository.GetVisualReferenceAsync(afterAResume, CancellationToken.None))?.ArtifactId == image.Id,
        "A second image replaced the frozen G checkpoint");
    await using (var conn = await db.OpenConnectionAsync())
    await using (var check = new NpgsqlCommand("SELECT count(*) FROM artifacts WHERE id=$1", conn))
    {
        check.Parameters.AddWithValue(replacementImageArtifact);
        Require((long)(await check.ExecuteScalarAsync() ?? -1L) == 0, "Duplicate G rollback left a replacement artifact");
    }
    await using (var conn = await db.OpenConnectionAsync())
    await using (var expire = new NpgsqlCommand("UPDATE jobs SET lease_until=now()-interval '1 second' WHERE id=$1", conn))
    { expire.Parameters.AddWithValue(recoveryJob); await expire.ExecuteNonQueryAsync(); }
    var afterGResume = await repository.ClaimAsync("recovery-g-resume", CancellationToken.None);
    Require(afterGResume?.Id == recoveryJob && afterGResume.Fence == 3 && afterGResume.State == "resolving_geometry",
        "After-G resume lost its stage or fencing token");
    Require(await repository.GetVisualReferenceAsync(afterAResume, CancellationToken.None) is null &&
        (await repository.GetVisualReferenceAsync(afterGResume!, CancellationToken.None))?.ArtifactId == image.Id &&
        await repository.CountAttemptsAsync(afterGResume!, "G", CancellationToken.None) == 1 &&
        !await repository.HasUncertainAttemptAsync(afterGResume!, CancellationToken.None),
        "After-G resume lost the frozen image or duplicated an image-generation attempt");

    // Move to planning, then crash while B is running. The next fence must
    // stop on the uncertain attempt instead of issuing a duplicate B call.
    await repository.SaveGeometryAsync(afterGResume!, new Dictionary<string, StoredArtifact>(),
        new Dictionary<string, StoredArtifact>(), CancellationToken.None);
    var bAttempt = await repository.BeginAttemptAsync(afterGResume!, "B", "gpt-5.6-luna", "max", new string('4', 64), CancellationToken.None);
    await using (var conn = await db.OpenConnectionAsync())
    await using (var expire = new NpgsqlCommand("UPDATE jobs SET lease_until=now()-interval '1 second' WHERE id=$1", conn))
    { expire.Parameters.AddWithValue(recoveryJob); await expire.ExecuteNonQueryAsync(); }
    var duringB = await repository.ClaimAsync("recovery-b-resume", CancellationToken.None);
    Require(duringB?.Id == recoveryJob && duringB.Fence == 4 && duringB.State == "planning", "During-B fencing claim failed");
    Require(await repository.HasUncertainAttemptAsync(duringB!, CancellationToken.None), "During-B uncertainty was not durable");
    Require(await repository.GetDescriptionAsync(duringB!, CancellationToken.None) is not null, "During-B resume lost A output");
    Require((await repository.GetVisualReferenceAsync(duringB!, CancellationToken.None))?.Sha256 == image.Sha256,
        "During-B resume lost the generated reference checkpoint");
    staleRejected = false;
    try { await repository.CompleteAttemptAsync(afterGResume!, bAttempt, "success", new string('5', 64), "synthetic", null, null, null, CancellationToken.None); }
    catch (InvalidOperationException e) when (e.Message == "fence_lost_on_attempt_completion") { staleRejected = true; }
    Require(staleRejected, "Stale B worker completed an attempt after lease reclaim");
    await repository.SetStateAsync(duringB!, "needs_operator", "uncertain_provider_attempt", "controlled B crash", false, CancellationToken.None);
    await using (var conn = await db.OpenConnectionAsync())
    await using (var check = new NpgsqlCommand("SELECT state,attempt_count FROM jobs WHERE id=$1", conn))
    {
        check.Parameters.AddWithValue(recoveryJob);
        await using var r = await check.ExecuteReaderAsync();
        Require(await r.ReadAsync() && r.GetString(0) == "needs_operator" && r.GetInt32(1) == 3,
            "During-B incident did not stop at operator review");
    }
    Console.WriteLine("Worker DB smoke passed: claim/lease, heartbeat, durable repair count, stale fence, uncertain incident, scheduled retry backoff, controlled crash after A/G and during B, immutable image checkpoint and transactional rollback. Image/provider metadata was synthetic; no model called.");
}
finally
{
    await using var conn = await db.OpenConnectionAsync();
    await using var tx = await conn.BeginTransactionAsync();
    foreach (var (sql, id) in new[] {
        ("DELETE FROM visual_references WHERE job_id=$1", recoveryJob),
        ("DELETE FROM interpretations WHERE job_id=$1", recoveryJob),
        ("DELETE FROM provider_attempts WHERE job_id=$1", job),
        ("DELETE FROM provider_attempts WHERE job_id=$1", retryJob),
        ("DELETE FROM provider_attempts WHERE job_id=$1", recoveryJob),
        ("DELETE FROM jobs WHERE id=$1", job),
        ("DELETE FROM jobs WHERE id=$1", retryJob),
        ("DELETE FROM jobs WHERE id=$1", recoveryJob),
        ("DELETE FROM captures WHERE id=$1", capture),
        ("DELETE FROM captures WHERE id=$1", recoveryCapture),
        ("DELETE FROM artifacts WHERE id=ANY($1)", Guid.Empty),
        ("DELETE FROM artifacts WHERE id=$1", recoveryDescriptionArtifact),
        ("DELETE FROM parchments WHERE id=$1", parchment),
        ("DELETE FROM parchments WHERE id=$1", recoveryParchment),
        ("DELETE FROM lab_principals WHERE id=$1", principal) })
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue(sql.Contains("ANY") ? cleanupArtifacts : id);
        await cmd.ExecuteNonQueryAsync();
    }
    await tx.CommitAsync();
}
