param(
    [Parameter(Mandatory)][string]$BackupDirectory,
    [Parameter(Mandatory)][string]$AdminEnvFile,
    [Parameter(Mandatory)][string]$TargetDatabase,
    [Parameter(Mandatory)][string]$TargetArtifactRoot,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\17\bin',
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DbEnv.ps1')

if ($TargetDatabase -notmatch '^palimpseste_[a-z0-9_]{1,48}$') { throw 'Nom de base cible refusé.' }
$backup = (Resolve-Path -LiteralPath $BackupDirectory).Path.TrimEnd('\', '/')
$artifactSource = Join-Path $backup 'artifacts'
$dump = Join-Path $backup 'database.dump'
$manifestPath = Join-Path $backup 'manifest.json'
if (-not (Test-Path -LiteralPath $dump) -or -not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $artifactSource)) { throw 'Sauvegarde incomplète.' }
$targetRoot = [IO.Path]::GetFullPath($TargetArtifactRoot).TrimEnd('\', '/')
if ($targetRoot.StartsWith($backup + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $backup.StartsWith($targetRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Restauration et sauvegarde doivent être disjointes.' }
if (Test-Path -LiteralPath $targetRoot) { throw 'Le stockage cible existe déjà ; restauration à blanc requise.' }
$manifest = Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
if ($manifest.schema_version -ne 'sp.backup/1.0') { throw 'Version de sauvegarde inconnue.' }
$dumpHash = (Get-FileHash -LiteralPath $dump -Algorithm SHA256).Hash.ToLowerInvariant()
if ($dumpHash -ne $manifest.dump_sha256) { throw 'Empreinte du dump invalide.' }
$backupEntries = @($manifest.artifacts)
if ($backupEntries.Count -ne [int]$manifest.artifact_count) { throw 'Nombre d’artefacts incohérent.' }
foreach ($entry in $backupEntries) {
    $file = Get-ChildArtifactPath -Root $artifactSource -Key $entry.storage_key
    if (-not (Test-Path -LiteralPath $file)) { throw "Artefact manquant : $($entry.storage_key)" }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Artefact altéré : $($entry.storage_key)" }
}
$admin = Read-PalimpsesteConnection -EnvFile $AdminEnvFile -Name 'PGADMIN_URL'
$lab = Read-PalimpsesteConnection -EnvFile $AdminEnvFile -Name 'DATABASE_URL'
$psql = Join-Path $PostgresBin 'psql.exe'
$pgRestore = Join-Path $PostgresBin 'pg_restore.exe'
if (-not (Test-Path -LiteralPath $psql) -or -not (Test-Path -LiteralPath $pgRestore)) { throw 'Outils PostgreSQL introuvables.' }
$oldPassword = [Environment]::GetEnvironmentVariable('PGPASSWORD', 'Process')
try {
    $env:PGPASSWORD = $admin.Password
    $exists = & $psql -w -tA --host=$($admin.Host) --port=$($admin.Port) --username=$($admin.Username) --dbname=$($admin.Database) -c "SELECT 1 FROM pg_database WHERE datname='$TargetDatabase'"
    if ($LASTEXITCODE -ne 0) { throw 'Contrôle de base cible impossible.' }
    if ($exists -match '1') { throw 'La base cible existe déjà ; aucune restauration par-dessus.' }
    & $psql -w --host=$($admin.Host) --port=$($admin.Port) --username=$($admin.Username) --dbname=$($admin.Database) -v ON_ERROR_STOP=1 -c "CREATE DATABASE $TargetDatabase OWNER $($lab.Username)"
    if ($LASTEXITCODE -ne 0) { throw 'Création de la base cible échouée.' }
    $env:PGPASSWORD = $lab.Password
    & $pgRestore --exit-on-error --single-transaction --no-owner --no-acl --host=$($lab.Host) --port=$($lab.Port) --username=$($lab.Username) --dbname=$TargetDatabase $dump
    if ($LASTEXITCODE -ne 0) { throw 'pg_restore a échoué.' }
    New-Item -ItemType Directory -Path $targetRoot | Out-Null
    Set-PrivateAcl -Path $targetRoot
    foreach ($entry in $backupEntries) {
        $source = Get-ChildArtifactPath -Root $artifactSource -Key $entry.storage_key
        $target = Get-ChildArtifactPath -Root $targetRoot -Key $entry.storage_key
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Restauration d'artefact altérée : $($entry.storage_key)" }
    }
    $rows = @(& $psql -w -tA --host=$($lab.Host) --port=$($lab.Port) --username=$($lab.Username) --dbname=$TargetDatabase -c "SELECT storage_key||'|'||sha256 FROM artifacts ORDER BY storage_key")
    if ($LASTEXITCODE -ne 0) { throw 'Lecture des artefacts restaurés échouée.' }
    $verified = 0
    foreach ($row in $rows) {
        if ([string]::IsNullOrWhiteSpace($row)) { continue }
        $pair = $row.Split('|')
        if ($pair.Length -ne 2) { throw 'Ligne artefact restaurée invalide.' }
        $path = Get-ChildArtifactPath -Root $targetRoot -Key $pair[0]
        if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pair[1]) { throw "Artefact DB/fichier incohérent : $($pair[0])" }
        $verified++
    }
    $counts = & $psql -w -tA --host=$($lab.Host) --port=$($lab.Port) --username=$($lab.Username) --dbname=$TargetDatabase -c 'SELECT (SELECT count(*) FROM parchments)||(chr(124))||(SELECT count(*) FROM captures)||(chr(124))||(SELECT count(*) FROM jobs)||(chr(124))||(SELECT count(*) FROM spells)'
    if ($LASTEXITCODE -ne 0) { throw 'Vérification des lignes restaurées échouée.' }
    if (-not $ReportPath) { $ReportPath = $targetRoot + '.restore-report.json' }
    $report = [ordered]@{ schema_version = 'sp.restore-report/1.0'; checked_at_utc = [DateTime]::UtcNow.ToString('O'); source_database = $manifest.source_database; target_database = $TargetDatabase; dump_sha256 = $dumpHash; artifacts_in_backup = $backupEntries.Count; artifacts_verified_against_db = $verified; rows_parchments_captures_jobs_spells = [string]$counts; result = 'passed' }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    Write-Output ("Restauration vérifiée : base={0}, artefacts DB={1}, lignes={2}, rapport={3}" -f $TargetDatabase, $verified, $counts, $ReportPath)
}
finally {
    if ($null -eq $oldPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
    else { $env:PGPASSWORD = $oldPassword }
}
