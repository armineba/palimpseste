using System.Text.Json;
using Npgsql;
using Palimpseste.Storage;

namespace Palimpseste.Api;

public sealed class ReferenceManager(ApiConfig settings, NpgsqlDataSource db, IArtifactStore store)
{
    public async Task<(Guid Id, string Sha256)> EnsureAsync(string layoutVersion, CancellationToken ct)
    {
        var path = settings.ReferencePathForLayout(layoutVersion);
        var kind = layoutVersion == "free_canvas_v2" ? "reference_free_canvas" : "reference_layout";
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var sha = ApiJson.Sha256(bytes);
        var layoutPath = Path.Combine(Path.GetDirectoryName(path)!,
            layoutVersion == "free_canvas_v2" ? "layout-v2.json" : "layout-v1.json");
        using (var layout = JsonDocument.Parse(await File.ReadAllBytesAsync(layoutPath, ct)))
        {
            if (layout.RootElement.GetProperty("id").GetString() != layoutVersion)
                throw new InvalidOperationException("Reference layout ID mismatch");
            var expected = layout.RootElement.GetProperty("reference_file_sha256").GetString();
            if (!string.Equals(sha, expected, StringComparison.Ordinal))
                throw new InvalidOperationException("Reference PNG does not match its layout manifest");
        }
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock(1774950115)", connection, transaction))
            await guard.ExecuteNonQueryAsync(ct);
        Guid? existingId = null;
        await using (var read = new NpgsqlCommand("SELECT id, sha256 FROM artifacts WHERE owner_id IS NULL AND kind=@kind", connection, transaction))
        {
            read.Parameters.AddWithValue("kind", kind);
            await using var reader = await read.ExecuteReaderAsync(ct);
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
        await using (var insert = new NpgsqlCommand("INSERT INTO artifacts(id,owner_id,kind,storage_key,sha256,content_type,byte_length) VALUES (@id,NULL,@kind,@key,@sha,'image/png',@size)", connection, transaction))
        {
            insert.Parameters.AddWithValue("id", artifact.Id);
            insert.Parameters.AddWithValue("kind", kind);
            insert.Parameters.AddWithValue("key", artifact.StorageKey);
            insert.Parameters.AddWithValue("sha", artifact.Sha256);
            insert.Parameters.AddWithValue("size", artifact.ByteLength);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return (artifact.Id, sha);
    }
}
