[CmdletBinding()]
param([string]$RuntimeRoot = 'E:\PalimpsesteRuntime')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not [string]::Equals([Security.Principal.WindowsIdentity]::GetCurrent().Name,
        "$env:COMPUTERNAME\PalRuntimeSvc", [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Owner lab worker must run as PalRuntimeSvc.'
}

$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\', '/')
if (-not [string]::Equals($runtime, 'E:\PalimpsesteRuntime', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Owner lab worker accepts only E:\PalimpsesteRuntime.'
}
$expectedWorkerSha256 = '987946213762B3E4052D1218377F3CFA748EF3F42BD7FF935E52BBE48CFF2F4A'
$child = Join-Path $runtime 'bin\WorkerService.Child.ps1'
$worker = Join-Path $runtime 'bin\Palimpseste.Worker.exe'
$envFile = Join-Path $runtime 'runtime.env'
$databaseEnv = 'C:\ProgramData\Palimpseste\lab-db.env'

function Assert-NoReparseAncestors([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetPathRoot($full)
    $cursor = $root
    $parts = $full.Substring($root.Length).TrimEnd('\', '/').Split(
        [char[]]@('\', '/'), [StringSplitOptions]::RemoveEmptyEntries)
    foreach ($part in @('') + $parts) {
        if ($part) { $cursor = Join-Path $cursor $part }
        $item = Get-Item -LiteralPath $cursor -Force -ErrorAction Stop
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Owner lab path contains a reparse point.'
        }
    }
}

function Assert-TrustedAcl([string]$Path) {
    $acl = Get-Acl -LiteralPath $Path -ErrorAction Stop
    $ownerSid = ([Security.Principal.NTAccount]$acl.Owner).
        Translate([Security.Principal.SecurityIdentifier]).Value
    $trusted = @('S-1-5-18', 'S-1-5-32-544')
    if ($ownerSid -notin $trusted) { throw 'Owner lab protected path has an untrusted owner.' }
    $writeBits = [Security.AccessControl.FileSystemRights]::WriteData -bor
        [Security.AccessControl.FileSystemRights]::AppendData -bor
        [Security.AccessControl.FileSystemRights]::WriteExtendedAttributes -bor
        [Security.AccessControl.FileSystemRights]::WriteAttributes -bor
        [Security.AccessControl.FileSystemRights]::Delete -bor
        [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor
        [Security.AccessControl.FileSystemRights]::ChangePermissions -bor
        [Security.AccessControl.FileSystemRights]::TakeOwnership
    foreach ($rule in $acl.Access) {
        if ($rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or
            ($rule.FileSystemRights -band $writeBits) -eq 0) { continue }
        $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if ($sid -notin $trusted) {
            throw 'Owner lab protected path grants write access to an untrusted principal.'
        }
    }
}

function Assert-SecureRuntimePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    if (-not [string]::Equals($full, $runtime, [StringComparison]::OrdinalIgnoreCase) -and
        -not $full.StartsWith($runtime + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Owner lab path is outside the runtime.'
    }
    Assert-NoReparseAncestors $full
    $cursor = $runtime
    Assert-TrustedAcl $cursor
    $relative = $full.Substring($runtime.Length).TrimStart('\', '/')
    foreach ($part in $relative.Split([char[]]@('\', '/'), [StringSplitOptions]::RemoveEmptyEntries)) {
        $cursor = Join-Path $cursor $part
        Assert-TrustedAcl $cursor
    }
}

if (-not [string]::Equals([IO.Path]::GetFullPath($PSCommandPath), $child,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Owner lab worker child path differs from reviewed deployment.'
}
foreach ($file in @($worker, $envFile, $databaseEnv)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw 'Owner lab worker deployment incomplete.'
    }
}
foreach ($protected in @($child, $worker, $envFile)) {
    Assert-SecureRuntimePath $protected
}
Assert-NoReparseAncestors $databaseEnv
if (-not [string]::Equals((Get-FileHash -LiteralPath $worker -Algorithm SHA256).Hash,
        $expectedWorkerSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installed worker SHA-256 differs from reviewed candidate.'
}

function Read-Entries([string]$Path) {
    $entries = @{}
    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line -match '^\s*#') { continue }
        if ($line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
            $name = $Matches[1]
            $value = $Matches[2].Trim()
        } else {
            throw 'Invalid environment line.'
        }
        if ($entries.ContainsKey($name)) { throw "Duplicate $name entry." }
        $entries.Add($name, $value)
    }
    return $entries
}

function Require-Value($Entries, [string]$Name, [string]$Expected) {
    if (-not $Entries.ContainsKey($Name) -or
        -not [string]::Equals($Entries[$Name], $Expected, [StringComparison]::Ordinal)) {
        throw "Owner lab gate $Name is not satisfied."
    }
}

function Require-Evidence($Entries, [string]$NamePrefix) {
    $pathName = $NamePrefix + '_PATH'
    $hashName = $NamePrefix + '_SHA256'
    if (-not $Entries.ContainsKey($pathName) -or
        -not $Entries.ContainsKey($hashName) -or
        $Entries[$hashName] -notmatch '^[0-9a-fA-F]{64}$') {
        throw "Owner lab evidence $NamePrefix is missing."
    }
    $path = [IO.Path]::GetFullPath($Entries[$pathName])
    $approved = (Join-Path $runtime 'approved-evidence').TrimEnd('\', '/') + '\'
    if (-not $path.StartsWith($approved, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Owner lab evidence $NamePrefix is outside approved-evidence."
    }
    Assert-SecureRuntimePath $path
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if (-not [string]::Equals($actual, $Entries[$hashName], [StringComparison]::OrdinalIgnoreCase)) {
        throw "Owner lab evidence $NamePrefix hash mismatch."
    }
}

$config = Read-Entries $envFile
Require-Value $config 'PALIMPSESTE_DEPLOYMENT_MODE' 'private_lab'
Require-Value $config 'PALIMPSESTE_PROVIDER' 'codex_exec'
Require-Value $config 'PALIMPSESTE_LUNA_MODEL' 'gpt-5.6-luna'
Require-Value $config 'PALIMPSESTE_LUNA_EFFORT' 'max'
Require-Value $config 'PALIMPSESTE_MAX_PROVIDER_CONCURRENCY' '1'
Require-Value $config 'PALIMPSESTE_RUNTIME_FEATURES_VERIFIED' 'true'
Require-Value $config 'PALIMPSESTE_EFFORT_VERIFIED' 'true'
Require-Evidence $config 'PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE'
Require-Evidence $config 'PALIMPSESTE_EFFORT_EVIDENCE'

$vault = Read-Entries $databaseEnv
if (-not $vault.ContainsKey('DATABASE_URL') -or
    [string]::IsNullOrWhiteSpace($vault['DATABASE_URL'])) {
    throw 'Owner lab database credential is missing.'
}

foreach ($entry in $config.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
}
[Environment]::SetEnvironmentVariable('DATABASE_URL', $vault['DATABASE_URL'], 'Process')
foreach ($name in @('OPENAI_API_KEY', 'CODEX_API_KEY', 'CODEX_ACCESS_TOKEN', 'CHATGPT_TOKEN')) {
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}

Set-Location -LiteralPath (Join-Path $runtime 'bin')
& $worker
exit $LASTEXITCODE
