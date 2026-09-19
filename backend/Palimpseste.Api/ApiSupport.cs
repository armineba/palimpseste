using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

namespace Palimpseste.Api;

public sealed class ApiConfig
{
    public required string ConnectionString { get; init; }
    public required string ArtifactRoot { get; init; }
    public required string ReferencePath { get; init; }
    public required string CatalogPath { get; init; }
    public string CatalogVersion { get; init; } = "sp.capabilities/1.0";
    public string RulesProfile { get; init; } = "lab_v1";
    public string MinimumClientVersion { get; init; } = "0.1.0";

    public static ApiConfig FromEnvironment()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var specificationRoot = Environment.GetEnvironmentVariable("PALIMPSESTE_SPEC_ROOT");
        if (string.IsNullOrWhiteSpace(specificationRoot)) specificationRoot = repositoryRoot;
        specificationRoot = Path.GetFullPath(specificationRoot);
        var connection = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "";
        if (connection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || connection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(connection);
            var userInfo = uri.UserInfo.Split(':', 2);
            connection = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.Trim('/'),
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length == 2 ? Uri.UnescapeDataString(userInfo[1]) : "",
                SslMode = uri.Query.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase) ? SslMode.Require : SslMode.Prefer
            }.ConnectionString;
        }
        return new ApiConfig
        {
            ConnectionString = connection,
            ArtifactRoot = Environment.GetEnvironmentVariable("ARTIFACT_ROOT") ?? Path.Combine(repositoryRoot, ".local-artifacts"),
            ReferencePath = Environment.GetEnvironmentVariable("REFERENCE_PNG") ?? Path.Combine(specificationRoot, "reference", "reference_layout.png"),
            CatalogPath = Environment.GetEnvironmentVariable("CAPABILITY_CATALOG") ?? Path.Combine(specificationRoot, "contracts", "capability-catalog.json"),
            CatalogVersion = Environment.GetEnvironmentVariable("CATALOG_VERSION") ?? "sp.capabilities/1.0",
            RulesProfile = Environment.GetEnvironmentVariable("RULES_PROFILE") ?? "lab_v1",
            MinimumClientVersion = Environment.GetEnvironmentVariable("ALLOWED_CLIENT_VERSION") ?? "0.1.0"
        };
    }
}

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static string Canonicalize(JsonElement root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false })) WriteSorted(writer, root);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSorted(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(p.Name);
                    WriteSorted(writer, p.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteSorted(writer, item);
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}

public sealed record Principal(Guid Id, string Role);

public static class ApiProblem
{
    public static IResult Result(HttpContext context, int status, string code, string message, bool retryable = false)
        => Results.Json(new { code, message, trace_id = context.TraceIdentifier, retryable }, statusCode: status);
}
