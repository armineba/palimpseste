using System.Net;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Palimpseste.Api;
using Palimpseste.Storage;

var builder = WebApplication.CreateBuilder(args);
var settings = ApiConfig.FromEnvironment();
if (string.IsNullOrWhiteSpace(settings.ConnectionString))
    throw new InvalidOperationException("DATABASE_URL is required. Run migrations explicitly before starting the API.");
if (args.Length > 0)
{
    Environment.ExitCode = await AdminCommands.RunAsync(args, settings);
    return;
}
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024 * 1024);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(NpgsqlDataSource.Create(settings.ConnectionString));
builder.Services.AddSingleton<IArtifactStore>(new FileArtifactStore(settings.ArtifactRoot));
builder.Services.AddSingleton<ReferenceManager>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/health")) { await next(); return; }
    var remote = context.Connection.RemoteIpAddress;
    if (!context.Request.IsHttps && (remote is null || !IPAddress.IsLoopback(remote)))
    {
        await ApiProblem.Result(context, 403, "https_required", "HTTPS est requis hors de localhost.").ExecuteAsync(context);
        return;
    }
    var authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || authorization.Length < 40)
    {
        await ApiProblem.Result(context, 401, "auth_required", "Jeton de laboratoire requis.").ExecuteAsync(context);
        return;
    }
    var token = authorization[7..].Trim();
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
    try
    {
        var db = context.RequestServices.GetRequiredService<NpgsqlDataSource>();
        await using var connection = await db.OpenConnectionAsync(context.RequestAborted);
        await using var command = new NpgsqlCommand("SELECT p.id, p.role FROM lab_tokens t JOIN lab_principals p ON p.id=t.principal_id WHERE t.token_sha256=@hash AND t.revoked_at IS NULL", connection);
        command.Parameters.AddWithValue("hash", hash);
        await using var reader = await command.ExecuteReaderAsync(context.RequestAborted);
        if (!await reader.ReadAsync(context.RequestAborted))
        {
            await ApiProblem.Result(context, 401, "auth_invalid", "Jeton invalide ou révoqué.").ExecuteAsync(context);
            return;
        }
        context.Items["principal"] = new Principal(reader.GetGuid(0), reader.GetString(1));
    }
    catch (NpgsqlException)
    {
        await ApiProblem.Result(context, 503, "database_unavailable", "Service temporairement indisponible.", true).ExecuteAsync(context);
        return;
    }
    try { await next(); }
    catch (Exception exception)
    {
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ApiUnhandled").LogError(exception, "API request failed: {TraceId}", context.TraceIdentifier);
        if (context.Response.HasStarted) throw;
        context.Response.Clear();
        var status = exception is NpgsqlException ? 503 : 500;
        await ApiProblem.Result(context, status, status == 503 ? "database_unavailable" : "service_failure", "Service temporairement indisponible.", status == 503).ExecuteAsync(context);
    }
});

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (NpgsqlDataSource db, ApiConfig config, CancellationToken ct) =>
{
    if (!File.Exists(config.ReferencePath) || !File.Exists(config.CatalogPath)) return Results.StatusCode(503);
    try
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT 1 FROM lab_principals LIMIT 1", connection);
        await command.ExecuteScalarAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception) { return Results.StatusCode(503); }
});

app.MapGet("/v1/capabilities", ApiHandlers.Capabilities);
app.MapGet("/v1/parchments", ApiHandlers.ListParchments);
app.MapPost("/v1/parchments", ApiHandlers.AllocateParchment);
app.MapPost("/v1/parchments/{id}/begin", ApiHandlers.BeginParchment);
app.MapPut("/v1/parchments/{id}/capture", ApiHandlers.CommitCapture).DisableAntiforgery();
app.MapGet("/v1/jobs/{id}", ApiHandlers.GetJob);
app.MapPost("/v1/jobs/{id}/resume", ApiHandlers.ResumeJob);
app.MapGet("/v1/spells/{id}", ApiHandlers.GetSpell);
app.MapGet("/v1/artifacts/{id}", ApiHandlers.GetArtifact);
app.MapPost("/v1/reviews", ApiHandlers.SubmitReview);
app.MapPost("/v1/spells/{id}/reviewers", ApiHandlers.GrantSpellReviewAccess);
app.MapDelete("/v1/spells/{id}/reviewers/{reviewerId}", ApiHandlers.RevokeSpellReviewAccess);
app.MapPost("/v1/authoring/plan", ApiHandlers.AuthoringPlan);
app.MapGet("/v1/authoring/jobs/{id}/plan", ApiHandlers.GetAuthoringPlan);

app.Run();
