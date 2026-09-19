param(
    [Parameter(Mandatory)][string]$EnvFile,
    [Parameter(Mandatory)][string]$ArtifactRoot,
    [Parameter(Mandatory)][string]$Destination,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\17\bin'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DbEnv.ps1')

$source = (Resolve-Path -LiteralPath $ArtifactRoot).Path.TrimEnd('\', '/')
$destinationFull = [IO.Path]::GetFullPath($Destination).TrimEnd('\', '/')
if ($destinationFull.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $source.StartsWith($destinationFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Destination et stockage source doivent être disjoints.'
}
if (Test-Path -LiteralPath $destinationFull) { throw 'Le dossier de sauvegarde existe déjà ; aucune écriture par-dessus.' }
$connection = Read-PalimpsesteConnection -EnvFile $EnvFile
$dumpExecutable = Join-Path $PostgresBin 'pg_dump.exe'
if (-not (Test-Path -LiteralPath $dumpExecutable)) { throw 'pg_dump.exe introuvable.' }
New-Item -ItemType Directory -Path $destinationFull | Out-Null
Set-PrivateAcl -Path $destinationFull
$artifactDestination = Join-Path $destinationFull 'artifacts'
New-Item -ItemType Directory -Path $artifactDestination | Out-Null
$dumpPath = Join-Path $destinationFull 'database.dump'
$oldPassword = [Environment]::GetEnvironmentVariable('PGPASSWORD', 'Process')
try {
    $env:PGPASSWORD = $connection.Password
    & $dumpExecutable --format=custom --no-owner --no-acl --host=$($connection.Host) --port=$($connection.Port) --username=$($connection.Username) --dbname=$($connection.Database) --file=$dumpPath
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump a échoué.' }
}
finally {
    if ($null -eq $oldPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
    else { $env:PGPASSWORD = $oldPassword }
}
$entries = @()
foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
    $relative = $file.FullName.Substring($source.Length).TrimStart('\', '/').Replace('\', '/')
    if (-not (Test-ArtifactKey $relative)) { continue }
    $target = Get-ChildArtifactPath -Root $artifactDestination -Key $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
    $originalHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $copiedHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($originalHash -ne $copiedHash) { throw "Copie d'artefact invalide : $relative" }
    $entries += [ordered]@{ storage_key = $relative; sha256 = $originalHash; bytes = $file.Length }
}
$manifest = [ordered]@{
    schema_version = 'sp.backup/1.0'
    created_at_utc = [DateTime]::UtcNow.ToString('O')
    source_database = $connection.Database
    dump_sha256 = (Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256).Hash.ToLowerInvariant()
    artifact_count = $entries.Count
    artifacts = @($entries | Sort-Object { $_.storage_key })
    condition = 'Artifact files are immutable; run without artifact garbage collection during pg_dump and copy.'
}
$manifestPath = Join-Path $destinationFull 'manifest.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Output ("Sauvegarde vérifiée : base={0}, artefacts={1}, dossier={2}" -f $connection.Database, $entries.Count, $destinationFull)
