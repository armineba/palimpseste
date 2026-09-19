using System.Text.Json;
using Npgsql;
using Palimpseste.Storage;

namespace Palimpseste.Api;

public sealed class ReferenceManager(ApiConfig settings, NpgsqlDataSource db, IArtifactStore store)
{
    public async Task<(Guid Id, string Sha256)> EnsureAsync(CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(settings.ReferencePath, ct);
        var sha = ApiJson.Sha256(bytes);
        var layoutPath = Path.Combine(Path.GetDirectoryName(settings.ReferencePath)!, "layout-v1.json");
        using (var layout = JsonDocument.Parse(await File.ReadAllBytesAsync(layoutPath, ct)))
        {
            var expected = layout.RootElement.GetProperty("reference_file_sha256").GetString();
            if (!string.Equals(sha, expected, StringComparison.Ordinal))
                throw new InvalidOperationException("Reference PNG does not match layout-v1.json");
        }
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock(1774950115)", connection, transaction))
            await guard.ExecuteNonQueryAsync(ct);
        Guid? existingId = null;
        await using (var read = new NpgsqlCommand("SELECT id, sha256 FROM artifacts WHERE owner_id IS NULL AND kind='reference_layout'", connection, transaction))
        await using (var reader = await read.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                existingId = reader.GetGuid(0);
                if (reader.GetString(1) != sha) throw new InvalidOperationException("Installed reference changed without versioning");
            }
        }
        if (existingId is { } id)
        {
            await transaction.CommitAsync(ct);
            return (id, sha);
        }
        var artifact = await store.PutAsync(bytes, "png", "image/png", ct);
        await using (var insert = new NpgsqlCommand("INSERT INTO artifacts(id,owner_id,kind,storage_key,sha256,content_type,byte_length) VALUES (@id,NULL,'reference_layout',@key,@sha,'image/png',@size)", connection, transaction))
        {
            insert.Parameters.AddWithValue("id", artifact.Id);
            insert.Parameters.AddWithValue("key", artifact.StorageKey);
            insert.Parameters.AddWithValue("sha", artifact.Sha256);
            insert.Parameters.AddWithValue("size", artifact.ByteLength);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return (artifact.Id, sha);
    }
}
