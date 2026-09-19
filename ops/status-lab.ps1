param(
    [Parameter(Mandatory)][string]$EnvFile,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\17\bin'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DbEnv.ps1')
$connection = Read-PalimpsesteConnection -EnvFile $EnvFile
$psql = Join-Path $PostgresBin 'psql.exe'
if (-not (Test-Path -LiteralPath $psql)) { throw 'psql.exe introuvable.' }
$oldPassword = [Environment]::GetEnvironmentVariable('PGPASSWORD', 'Process')
try {
    $env:PGPASSWORD = $connection.Password
    $query = @'
SELECT 'jobs', state, count(*)::text FROM jobs GROUP BY state
UNION ALL SELECT 'provider_attempts', status, count(*)::text FROM provider_attempts GROUP BY status
UNION ALL SELECT 'artifacts', kind, count(*)::text FROM artifacts GROUP BY kind
UNION ALL SELECT 'leases', 'expired', count(*)::text FROM jobs WHERE lease_until < now() AND state NOT IN ('ready','needs_operator')
ORDER BY 1,2;
'@
    & $psql -w -tA -F '|' --host=$($connection.Host) --port=$($connection.Port) --username=$($connection.Username) --dbname=$($connection.Database) -c $query
    if ($LASTEXITCODE -ne 0) { throw 'Lecture des métriques impossible.' }
}
finally {
    if ($null -eq $oldPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
    else { $env:PGPASSWORD = $oldPassword }
}
