using Npgsql;
using Palimpseste.Provider;
using Palimpseste.Worker;

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
var artifactRoot = Environment.GetEnvironmentVariable("ARTIFACT_ROOT");
var specRoot = Environment.GetEnvironmentVariable("PALIMPSESTE_SPEC_ROOT");
var settings = CodexSettings.FromEnvironment();
var issues = settings.Check(production: true);
if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(artifactRoot) ||
    string.IsNullOrWhiteSpace(specRoot) || issues.Count != 0)
{
    Console.Error.WriteLine("Worker préflight bloqué: " + string.Join(',', issues) +
        (string.IsNullOrWhiteSpace(connectionString) ? ",database_missing" : "") +
        (string.IsNullOrWhiteSpace(artifactRoot) ? ",artifact_root_missing" : "") +
        (string.IsNullOrWhiteSpace(specRoot) ? ",spec_root_missing" : ""));
    return 2;
}

await using var dataSource = NpgsqlDataSource.Create(connectionString);
var processor = new JobProcessor(dataSource, Path.GetFullPath(artifactRoot), Path.GetFullPath(specRoot),
    new LunaCodexProvider(new CodexProcessRunner(settings), Path.GetFullPath(specRoot)), settings);
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
await processor.RunAsync(shutdown.Token);
return 0;
