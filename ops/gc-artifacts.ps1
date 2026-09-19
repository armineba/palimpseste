param(
    [Parameter(Mandatory)][string]$EnvFile,
    [Parameter(Mandatory)][string]$ArtifactRoot,
    [ValidateRange(24, 8760)][int]$GraceHours = 168,
    [switch]$Execute,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\17\bin'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DbEnv.ps1')
$root = (Resolve-Path -LiteralPath $ArtifactRoot).Path.TrimEnd('\', '/')
$connection = Read-PalimpsesteConnection -EnvFile $EnvFile
$psql = Join-Path $PostgresBin 'psql.exe'
if (-not (Test-Path -LiteralPath $psql)) { throw 'psql.exe introuvable.' }
$oldPassword = [Environment]::GetEnvironmentVariable('PGPASSWORD', 'Process')
try {
    $env:PGPASSWORD = $connection.Password
    $keys = @(& $psql -w -tA --host=$($connection.Host) --port=$($connection.Port) --username=$($connection.Username) --dbname=$($connection.Database) -c 'SELECT storage_key FROM artifacts')
    if ($LASTEXITCODE -ne 0) { throw 'Impossible de lire les références DB ; aucune suppression.' }
}
finally {
    if ($null -eq $oldPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
    else { $env:PGPASSWORD = $oldPassword }
}
$referenced = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($key in $keys) { if (-not [string]::IsNullOrWhiteSpace($key)) { [void]$referenced.Add($key) } }
$cutoff = [DateTime]::UtcNow.AddHours(-$GraceHours)
$candidates = 0
$deleted = 0
foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse) {
    $relative = $file.FullName.Substring($root.Length).TrimStart('\', '/').Replace('\', '/')
    if (-not (Test-ArtifactKey $relative) -or $referenced.Contains($relative) -or $file.LastWriteTimeUtc -gt $cutoff) { continue }
    $checkedPath = Get-ChildArtifactPath -Root $root -Key $relative
    if ($checkedPath -ne $file.FullName) { throw 'Résolution de chemin incohérente ; aucune suppression.' }
    $candidates++
    if ($Execute) { Remove-Item -LiteralPath $checkedPath -Force; $deleted++ }
}
Write-Output ("GC artefacts : mode={0}, référencés={1}, candidats={2}, supprimés={3}, grâce={4}h" -f $(if($Execute){'execute'}else{'dry-run'}), $referenced.Count, $candidates, $deleted, $GraceHours)
