using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Palimpseste.Storage;

public sealed record StoredArtifact(Guid Id, string StorageKey, string Sha256, long ByteLength, string ContentType);

public static class PublicIds
{
    public static string Artifact(Guid id) => "a" + id.ToString("N");
    public static bool TryParseArtifact(string value, out Guid id)
    {
        id = default;
        return value.Length == 33 && value[0] == 'a' && Guid.TryParseExact(value[1..], "N", out id);
    }
}

public interface IArtifactStore
{
    Task<StoredArtifact> PutAsync(ReadOnlyMemory<byte> data, string extension, string contentType, CancellationToken cancellationToken = default);
    Task<byte[]> ReadAsync(string storageKey, CancellationToken cancellationToken = default);
    string PathForKey(string storageKey);
}

public sealed class FileArtifactStore(string root) : IArtifactStore
{
    private static readonly Regex KeyPattern = new("^[0-9a-f]{2}/[0-9a-f]{32}\\.(png|json|gz|bin)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly string _root = Path.GetFullPath(root);

    public async Task<StoredArtifact> PutAsync(ReadOnlyMemory<byte> data, string extension, string contentType, CancellationToken cancellationToken = default)
    {
        if (extension is not ("png" or "json" or "gz" or "bin")) throw new ArgumentOutOfRangeException(nameof(extension));
        var id = Guid.NewGuid();
        var hash = Convert.ToHexStringLower(SHA256.HashData(data.Span));
        var key = $"{hash[..2]}/{id:N}.{extension}";
        var finalPath = PathForKey(key);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var temporaryDirectory = Path.Combine(_root, ".tmp");
        Directory.CreateDirectory(temporaryDirectory);
        var temporaryPath = Path.Combine(temporaryDirectory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(data, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }
            File.Move(temporaryPath, finalPath);
            return new StoredArtifact(id, key, hash, data.Length, contentType);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public async Task<byte[]> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
        => await File.ReadAllBytesAsync(PathForKey(storageKey), cancellationToken);

    public string PathForKey(string storageKey)
    {
        if (!KeyPattern.IsMatch(storageKey)) throw new ArgumentException("Invalid artifact storage key", nameof(storageKey));
        var path = Path.GetFullPath(Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Artifact path escapes storage root", nameof(storageKey));
        return path;
    }
}

