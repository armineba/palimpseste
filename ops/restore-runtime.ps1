[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupRoot,
    [string]$DatabaseUrl = $env:DATABASE_URL,
    [string]$ArtifactRoot = $env:ARTIFACT_ROOT,
    [string]$ServiceUser = $env:PALIMPSESTE_SERVICE_USER,
    [string]$AdditionalArtifactUsers = $env:PALIMPSESTE_ARTIFACT_USERS,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\17\bin',
    [switch]$AllowDatabaseReplace
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ServiceUser)) { throw 'PALIMPSESTE_SERVICE_USER ou -ServiceUser doit identifier le compte qui doit relire les artefacts.' }
if (-not $AllowDatabaseReplace) { throw 'Restauration bloquÃ©e : fournir -AllowDatabaseReplace aprÃ¨s validation humaine de la sauvegarde.' }
if ([string]::IsNullOrWhiteSpace($DatabaseUrl)) { throw 'DATABASE_URL doit Ãªtre fourni par lâ€™environnement du service.' }
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) { throw 'ARTIFACT_ROOT doit Ãªtre fourni par lâ€™environnement du service.' }
$BackupRoot = [IO.Path]::GetFullPath($BackupRoot)
$ArtifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
$artifactUsers = @($ServiceUser) + @($AdditionalArtifactUsers -split '[,;]' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

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

function Test-NoReparsePath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $false }
    $full = [IO.Path]::GetFullPath($PathValue)
    $root = [IO.Path]::GetPathRoot($full)
    if ($null -eq $root) { return $false }
    $current = $root
    $remainder = $full.Substring($root.Length)
    foreach ($part in ($remainder -split '[\\/]' | Where-Object { $_ })) {
        $current = Join-Path $current $part
        if (-not (Test-Path -LiteralPath $current)) { continue }
        if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { return $false }
    }
    return $true
}

function Test-NoReparseTree([string]$RootPath) {
    if (-not (Test-Path -LiteralPath $RootPath -PathType Container) -or -not (Test-NoReparsePath $RootPath)) { return $false }
    $rootFull = [IO.Path]::GetFullPath($RootPath).TrimEnd('\', '/')
    foreach ($item in @(Get-Item -LiteralPath $RootPath -Force) + @(Get-ChildItem -LiteralPath $RootPath -Force -Recurse)) {
        $full = [IO.Path]::GetFullPath($item.FullName)
        if (-not ($full.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase) -or
                $full.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) { return $false }
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
    }
    return $true
}

function Test-DirectRestoreStage([string]$StagePath, [string]$ParentPath) {
    if (-not (Test-NoReparsePath $ParentPath) -or -not (Test-NoReparsePath $StagePath)) { return $false }
    $stageFull = [IO.Path]::GetFullPath($StagePath).TrimEnd('\', '/')
    $parentFull = [IO.Path]::GetFullPath($ParentPath).TrimEnd('\', '/')
    $name = [IO.Path]::GetFileName($stageFull)
    return $stageFull.StartsWith($parentFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        $name -match '^\.restore-[0-9a-f]{32}$'
}

function Protect-RestoreDirectory([string]$PathValue) {
    $sid = ([Security.Principal.NTAccount]("$env:USERDOMAIN\$env:USERNAME")).Translate([Security.Principal.SecurityIdentifier]).Value
    & icacls.exe $PathValue /inheritance:r /grant:r ('*{0}:(OI)(CI)(F)' -f $sid) '*S-1-5-18:(OI)(CI)(F)' '*S-1-5-32-544:(OI)(CI)(F)' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "ACL restore staging failed: $PathValue" }
}

function Resolve-AccountSid([string]$Account) {
    try { return ([Security.Principal.NTAccount]$Account).Translate([Security.Principal.SecurityIdentifier]).Value }
    catch { throw "Compte d'artefacts introuvable : $Account" }
}

function Grant-ArtifactRuntimeAccess([string]$PathValue, [string[]]$Accounts) {
    if (-not (Test-NoReparseTree $PathValue)) { throw "Refus ACL artefacts : arbre absent ou reparse point : $PathValue" }
    $grants = [System.Collections.Generic.List[string]]::new()
    foreach ($account in $Accounts) {
        if ([string]::IsNullOrWhiteSpace($account)) { continue }
        $sid = Resolve-AccountSid $account.Trim()
        if (-not ($grants -contains $sid)) { $grants.Add($sid) }
    }
    if ($grants.Count -eq 0) { throw 'Aucun compte de service n''a Ã©tÃ© rÃ©solu pour ARTIFACT_ROOT.' }
    $currentSid = ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value)
    $items = @(Get-Item -LiteralPath $PathValue -Force) + @(Get-ChildItem -LiteralPath $PathValue -Force -Recurse)
    foreach ($item in $items) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point in artifact tree: $($item.FullName)" }
        $inheritance = if ($item.PSIsContainer) { '(OI)(CI)' } else { '' }
        $rules = [System.Collections.Generic.List[string]]::new()
        foreach ($sid in $grants) { $rules.Add(('*{0}:{1}(M)' -f $sid, $inheritance)) }
        $rules.Add(('*{0}:{1}(F)' -f $currentSid, $inheritance))
        $rules.Add(('*S-1-5-18:{0}(F)' -f $inheritance))
        $rules.Add(('*S-1-5-32-544:{0}(F)' -f $inheritance))
        & icacls.exe $item.FullName /inheritance:r /grant:r $rules.ToArray() /C | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "ACL artifact runtime failed: $($item.FullName)" }
    }
}

if (-not (Test-Path -LiteralPath $BackupRoot -PathType Container) -or -not (Test-NoReparsePath $BackupRoot)) { throw 'Backup root missing or reparse point detected.' }
$artifactParent = [IO.Path]::GetDirectoryName($ArtifactRoot)
if ([string]::IsNullOrWhiteSpace($artifactParent) -or -not (Test-Path -LiteralPath $artifactParent -PathType Container) -or -not (Test-NoReparsePath $artifactParent)) {
    throw 'The ARTIFACT_ROOT parent must exist and contain no reparse point.'
}
$manifestPath = Join-Path $BackupRoot 'artifact-manifest.json'
$dumpPath = Join-Path $BackupRoot 'database.dump'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or -not (Test-Path -LiteralPath $dumpPath -PathType Leaf)) { throw 'Sauvegarde incomplÃ¨te.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$expectedDumpHash = [string]$manifest.database_dump.sha256
$actualDumpHash = (Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualDumpHash -ne $expectedDumpHash) { throw 'Hash du dump DB invalide.' }
$restoreExecutable = Resolve-PostgresExecutable $PostgresBin 'pg_restore.exe'

$backupArtifacts = Join-Path $BackupRoot ([string]$manifest.artifact_root)
if (-not (Test-Path -LiteralPath $backupArtifacts -PathType Container) -or -not (Test-NoReparsePath $backupArtifacts)) { throw 'Backup artifact directory missing or reparse point detected.' }
$stage = Join-Path $artifactParent ('.restore-' + [Guid]::NewGuid().ToString('N'))
if (-not (Test-DirectRestoreStage $stage $artifactParent)) { throw 'Invalid restore staging path.' }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Protect-RestoreDirectory $stage
$restoredDatabaseName = $null
try {
    foreach ($entry in $manifest.artifacts) {
        $key = [string]$entry.storage_key
        if ($key -match '(^/|\\|\.\.|^[.]|\.$)') { throw "ClÃ© artefact invalide dans le manifeste : $key" }
        $source = Join-Path $backupArtifacts ($key.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $target = Join-Path $stage ($key.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or -not (Test-NoReparsePath $source)) { throw "Artifact missing or reparse point detected: $key" }
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($sourceHash -ne [string]$entry.sha256) { throw "Hash artefact invalide : $key" }
        $targetParent = [IO.Path]::GetDirectoryName($target)
        if ([string]::IsNullOrWhiteSpace($targetParent) -or -not (Test-NoReparsePath $targetParent)) { throw "Target path contains a reparse point: $key" }
        New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
    }
    if (-not (Test-NoReparseTree $stage)) { throw 'Restore staging tree contains a reparse point.' }
    # Keep the service/API identities on the staged tree before the move. The
    # ACL is re-applied after the move because a moved directory keeps its own
    # ACL rather than inheriting ARTIFACT_ROOT's old ACL.
    Grant-ArtifactRuntimeAccess $stage $artifactUsers
    $postgresState = Set-PostgresEnvironment $DatabaseUrl
    $restoredDatabaseName = $postgresState.Database
    try {
        & $restoreExecutable '--clean' '--if-exists' '--single-transaction' '--no-owner' '--no-privileges' '--no-password' '--dbname' $postgresState.Database $dumpPath 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'pg_restore a Ã©chouÃ© aprÃ¨s vÃ©rification des artefacts.' }
    } finally {
        Restore-PostgresEnvironment $postgresState
    }
    if (Test-Path -LiteralPath $ArtifactRoot) {
        if (-not (Test-NoReparsePath $ArtifactRoot)) { throw 'ARTIFACT_ROOT contains a reparse point.' }
        $existing = @(Get-ChildItem -LiteralPath $ArtifactRoot -Force)
        if ($existing.Count -ne 0) { throw 'ARTIFACT_ROOT doit Ãªtre absent ou vide pour Ã©viter une fusion ambiguÃ«.' }
        Remove-Item -LiteralPath $ArtifactRoot -Force
    }
    if (-not (Test-DirectRestoreStage $stage $artifactParent)) { throw 'Invalid restore staging path before move.' }
    if (-not (Test-NoReparseTree $stage)) { throw 'Restore staging tree contains a reparse point before move.' }
    Move-Item -LiteralPath $stage -Destination $ArtifactRoot
    Grant-ArtifactRuntimeAccess $ArtifactRoot $artifactUsers
} catch {
    if ((Test-Path -LiteralPath $stage -PathType Container -ErrorAction SilentlyContinue) -and
        (Test-DirectRestoreStage $stage $artifactParent) -and
        (Test-NoReparseTree $stage)) {
        Remove-Item -LiteralPath ([IO.Path]::GetFullPath($stage)) -Recurse -Force
    }
    throw
}
Write-Output ("Restauration verifiee depuis {0}; base PostgreSQL : {1}; artefacts : {2}." -f $BackupRoot, $restoredDatabaseName, $manifest.artifact_count)
