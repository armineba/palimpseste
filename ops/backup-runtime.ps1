[CmdletBinding()]
param(
    [string]$DatabaseUrl = $env:DATABASE_URL,
    [string]$ArtifactRoot = $env:ARTIFACT_ROOT,
    [string]$BackupRoot = 'E:\PalimpsesteBackups',
    [string]$Label = '',
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\17\bin'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($DatabaseUrl)) { throw 'DATABASE_URL doit être fourni par l’environnement du service.' }
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) { throw 'ARTIFACT_ROOT doit être fourni par l’environnement du service.' }
$ArtifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
$BackupRoot = [IO.Path]::GetFullPath($BackupRoot)
if (-not (Test-Path -LiteralPath $ArtifactRoot -PathType Container)) { throw "ARTIFACT_ROOT absent : $ArtifactRoot" }
function Test-PathInside([string]$Candidate, [string]$Root) {
    $candidateFull = [IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/')
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    return $candidateFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase) -or
        $candidateFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}
if ((Test-PathInside $BackupRoot $ArtifactRoot) -or (Test-PathInside $ArtifactRoot $BackupRoot)) { throw 'BackupRoot et ARTIFACT_ROOT ne doivent pas se contenir.' }
if ([string]::IsNullOrWhiteSpace($Label)) { $Label = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') }
if ($Label -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Label contient des caractères interdits.' }

function Set-PostgresEnvironment([string]$Connection) {
    $values = @{}
    if ($Connection -match '^(?i)(postgres|postgresql)://') {
        $uri = [Uri]$Connection
        if (-not [string]::IsNullOrWhiteSpace($uri.Host)) { $values.PGHOST = $uri.Host }
        if ($uri.Port -gt 0) { $values.PGPORT = [string]$uri.Port }
        if (-not [string]::IsNullOrWhiteSpace($uri.UserInfo)) {
            $separator = $uri.UserInfo.IndexOf(':')
            $rawUser = if ($separator -ge 0) { $uri.UserInfo.Substring(0, $separator) } else { $uri.UserInfo }
            $values.PGUSER = [Uri]::UnescapeDataString($rawUser)
            if ($separator -ge 0) { $values.PGPASSWORD = [Uri]::UnescapeDataString($uri.UserInfo.Substring($separator + 1)) }
        }
        $database = $uri.AbsolutePath.Trim('/').Split('/')[0]
        if (-not [string]::IsNullOrWhiteSpace($database)) { $values.PGDATABASE = [Uri]::UnescapeDataString($database) }
        foreach ($queryPart in $uri.Query.TrimStart('?').Split('&')) {
            if ([string]::IsNullOrWhiteSpace($queryPart)) { continue }
            $parts = $queryPart.Split('=', 2)
            if ($parts.Length -eq 2 -and $parts[0] -ieq 'sslmode') { $values.PGSSLMODE = [Uri]::UnescapeDataString($parts[1]) }
        }
    } else {
        foreach ($part in $Connection.Split(';')) {
            $separator = $part.IndexOf('=')
            if ($separator -le 0) { continue }
            $key = $part.Substring(0, $separator).Trim().ToLowerInvariant()
            $value = $part.Substring($separator + 1).Trim()
            switch ($key) {
                'host' { $values.PGHOST = $value }
                'server' { $values.PGHOST = $value }
                'port' { $values.PGPORT = $value }
                'user' { $values.PGUSER = $value }
                'username' { $values.PGUSER = $value }
                'password' { $values.PGPASSWORD = $value }
                'database' { $values.PGDATABASE = $value }
                'database name' { $values.PGDATABASE = $value }
                'sslmode' { $values.PGSSLMODE = $value }
            }
        }
    }
    if ([string]::IsNullOrWhiteSpace($values.PGHOST)) { throw 'DATABASE_URL doit contenir PGHOST/Host.' }
    if ([string]::IsNullOrWhiteSpace($values.PGUSER)) { throw 'DATABASE_URL doit contenir PGUSER/User.' }
    if ([string]::IsNullOrWhiteSpace($values.PGPASSWORD)) { throw 'DATABASE_URL doit contenir PGPASSWORD/Password.' }
    if ([string]::IsNullOrWhiteSpace($values.PGDATABASE)) { throw 'DATABASE_URL doit contenir PGDATABASE/Database.' }
    if ([string]::IsNullOrWhiteSpace($values.PGPORT)) { $values.PGPORT = '5432' }
    $names = @('PGHOST', 'PGPORT', 'PGUSER', 'PGPASSWORD', 'PGDATABASE', 'PGSSLMODE')
    $previous = @{}
    foreach ($name in $names) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        if ($values.ContainsKey($name)) { [Environment]::SetEnvironmentVariable($name, [string]$values[$name], 'Process') }
    }
    return [ordered]@{ Database = [string]$values.PGDATABASE; Previous = $previous; Names = $names }
}

function Restore-PostgresEnvironment($state) {
    foreach ($name in $state.Names) {
        if ($null -eq $state.Previous[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($name, $state.Previous[$name], 'Process') }
    }
}

function Resolve-PostgresExecutable([string]$BinPath, [string]$Name) {
    if (-not [string]::IsNullOrWhiteSpace($BinPath)) {
        $candidate = Join-Path ([IO.Path]::GetFullPath($BinPath)) $Name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $command -and -not [string]::IsNullOrWhiteSpace($command.Source)) { return $command.Source }
    throw "$Name introuvable. Fournir -PostgresBin avec le dossier PostgreSQL contenant $Name."
}

function Protect-BackupDirectory([string]$PathValue) {
    $sid = ([Security.Principal.NTAccount]("$env:USERDOMAIN\$env:USERNAME")).Translate([Security.Principal.SecurityIdentifier]).Value
    & icacls.exe $PathValue /inheritance:r /grant:r ('*{0}:(OI)(CI)(F)' -f $sid) '*S-1-5-18:(OI)(CI)(F)' '*S-1-5-32-544:(OI)(CI)(F)' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "ACL backup failed: $PathValue" }
}

$dumpExecutable = Resolve-PostgresExecutable $PostgresBin 'pg_dump.exe'
$destination = Join-Path $BackupRoot $Label
$dumpPath = Join-Path $destination 'database.dump'
$artifactDestination = Join-Path $destination 'artifacts'
$manifestPath = Join-Path $destination 'artifact-manifest.json'
$errorPath = Join-Path $destination '.pgdump-error.tmp'
if (Test-Path -LiteralPath $destination) { throw "La destination existe déjà : $destination" }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Protect-BackupDirectory $destination
New-Item -ItemType Directory -Path $artifactDestination -Force | Out-Null

$postgresState = Set-PostgresEnvironment $DatabaseUrl
try {
    & $dumpExecutable '--format=custom' '--no-owner' '--no-privileges' '--no-password' '--file' $dumpPath '--dbname' $postgresState.Database 2> $errorPath
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump a échoué; consulter l’état du serveur, aucun détail de connexion n’est journalisé.' }
} finally {
    if (Test-Path -LiteralPath $errorPath) { Remove-Item -LiteralPath $errorPath -Force }
    Restore-PostgresEnvironment $postgresState
}

$entries = [System.Collections.Generic.List[object]]::new()
foreach ($file in Get-ChildItem -LiteralPath $ArtifactRoot -Recurse -File) {
    $tmpPrefix = [IO.Path]::Combine($ArtifactRoot, '.tmp') + [IO.Path]::DirectorySeparatorChar
    if ($file.Name -eq '.keep' -or $file.FullName.IndexOf($tmpPrefix, [StringComparison]::OrdinalIgnoreCase) -ge 0) { continue }
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point interdit dans ARTIFACT_ROOT : $($file.FullName)" }
    if ($file.Extension.ToLowerInvariant() -notin @('.png', '.json', '.gz', '.bin')) { continue }
    $artifactPrefix = $ArtifactRoot.TrimEnd('\', '/') + '\'
    $relative = $file.FullName.Substring($artifactPrefix.Length)
    $target = Join-Path $artifactDestination $relative
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $entries.Add([ordered]@{ storage_key = $relative.Replace('\', '/'); size_bytes = $file.Length; sha256 = $hash })
}

$manifest = [ordered]@{
    kind = 'palimpseste_runtime_backup'
    created_at = [DateTimeOffset]::UtcNow
    label = $Label
    database_dump = [ordered]@{ file = 'database.dump'; size_bytes = (Get-Item -LiteralPath $dumpPath).Length; sha256 = (Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    artifact_root = 'artifacts'
    artifact_count = $entries.Count
    artifacts = $entries
    consistency_protocol = 'stop-worker-before-dump-and-keep-worker-stopped-through-artifact-copy'
    database_url_recorded = $false
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Output ("Sauvegarde créée : {0}; artefacts manifestés : {1}." -f $destination, $entries.Count)
