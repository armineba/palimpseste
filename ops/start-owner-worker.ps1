[CmdletBinding()]
param([string]$RuntimeRoot = 'E:\PalimpsesteRuntime')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedWorkerSha256 = 'FC78432166EAED3E646885F68B36832C2F1FC691AE4F271017D2056FD76530DA'
$expectedChildSha256 = '230079D87F6853D654E6B71229BA229F7689CA359D38997216B7054537BB3FCA'
$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\', '/')
if (-not [string]::Equals($runtime, 'E:\PalimpsesteRuntime', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Owner lab worker accepts only E:\PalimpsesteRuntime.'
}
$child = Join-Path $runtime 'bin\WorkerService.Child.ps1'
$worker = Join-Path $runtime 'bin\Palimpseste.Worker.exe'
$envFile = Join-Path $runtime 'runtime.env'
$credentialPath = 'C:\ProgramData\Palimpseste\operator-credentials\PalRuntimeSvc.credential.xml'
$pending = Join-Path $runtime 'evidence\pending'

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

function Assert-TrustedAcl([string]$Path, [bool]$AllowServiceWrite) {
    $acl = Get-Acl -LiteralPath $Path -ErrorAction Stop
    $ownerSid = ([Security.Principal.NTAccount]$acl.Owner).
        Translate([Security.Principal.SecurityIdentifier]).Value
    $trusted = @('S-1-5-18', 'S-1-5-32-544')
    if ($ownerSid -notin $trusted) { throw 'Owner lab protected path has an untrusted owner.' }
    $serviceSid = ([Security.Principal.NTAccount]"$env:COMPUTERNAME\PalRuntimeSvc").
        Translate([Security.Principal.SecurityIdentifier]).Value
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
        if ($sid -notin $trusted -and -not ($AllowServiceWrite -and $sid -eq $serviceSid)) {
            throw 'Owner lab protected path grants write access to an untrusted principal.'
        }
    }
}

function Assert-SecureRuntimePath([string]$Path, [switch]$AllowServiceWrite) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    if (-not [string]::Equals($full, $runtime, [StringComparison]::OrdinalIgnoreCase) -and
        -not $full.StartsWith($runtime + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Owner lab path is outside the runtime.'
    }
    Assert-NoReparseAncestors $full
    $cursor = $runtime
    Assert-TrustedAcl $cursor $false
    $relative = $full.Substring($runtime.Length).TrimStart('\', '/')
    foreach ($part in $relative.Split([char[]]@('\', '/'), [StringSplitOptions]::RemoveEmptyEntries)) {
        $cursor = Join-Path $cursor $part
        Assert-TrustedAcl $cursor ($AllowServiceWrite -and
            [string]::Equals($cursor, $full, [StringComparison]::OrdinalIgnoreCase))
    }
}

foreach ($required in @($child, $worker, $envFile, $credentialPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw 'Owner lab worker deployment incomplete.'
    }
}
if (-not (Test-Path -LiteralPath $pending -PathType Container)) {
    throw 'Private log folder missing.'
}
foreach ($protected in @($child, $worker, $envFile)) {
    Assert-SecureRuntimePath $protected
}
Assert-SecureRuntimePath $pending -AllowServiceWrite
Assert-NoReparseAncestors $credentialPath
if (-not [string]::Equals((Get-FileHash -LiteralPath $child -Algorithm SHA256).Hash,
        $expectedChildSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installed child script SHA-256 differs from reviewed source.'
}
if (-not [string]::Equals((Get-FileHash -LiteralPath $worker -Algorithm SHA256).Hash,
        $expectedWorkerSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installed worker SHA-256 differs from reviewed candidate.'
}

$gate = @(Get-Content -LiteralPath $envFile -Encoding UTF8 |
    Where-Object { $_.StartsWith('PALIMPSESTE_EFFORT_VERIFIED=', [StringComparison]::Ordinal) })
if ($gate.Count -ne 1 -or $gate[0] -cne 'PALIMPSESTE_EFFORT_VERIFIED=true') {
    throw 'Owner lab technical effort gate remains closed.'
}
$interpreterGate = @(Get-Content -LiteralPath $envFile -Encoding UTF8 |
    Where-Object { $_.StartsWith('PALIMPSESTE_INTERPRETER_VERIFIED=', [StringComparison]::Ordinal) })
if ($interpreterGate.Count -ne 1 -or $interpreterGate[0] -cne 'PALIMPSESTE_INTERPRETER_VERIFIED=true') {
    throw 'Owner lab interpreter technical gate remains closed.'
}

$running = @(Get-CimInstance Win32_Process -Filter "Name = 'Palimpseste.Worker.exe'")
if ($running.Count -ne 0) { throw 'A worker is already running; inspect it before launch.' }

$credential = Import-Clixml -LiteralPath $credentialPath
if ($credential -isnot [System.Management.Automation.PSCredential] -or
    -not [string]::Equals($credential.UserName, "$env:COMPUTERNAME\PalRuntimeSvc",
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unexpected service credential.'
}

$stamp = [Guid]::NewGuid().ToString('N')
$stdout = Join-Path $pending "owner-worker-$stamp.stdout.txt"
$stderr = Join-Path $pending "owner-worker-$stamp.stderr.txt"
$arguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
    '-File', ('"' + $child + '"'), '-RuntimeRoot', ('"' + $runtime + '"'))
$powerShellExe = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
Assert-NoReparseAncestors $powerShellExe
$process = Start-Process -FilePath $powerShellExe -ArgumentList $arguments `
    -Credential $credential -LoadUserProfile -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput $stdout -RedirectStandardError $stderr

$workerPid = $null
for ($attempt = 0; $attempt -lt 40; $attempt++) {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
    if ($process.HasExited) { break }
    $workerProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'Palimpseste.Worker.exe'")
    if ($workerProcesses.Count -ne 1) { continue }
    $item = $workerProcesses[0]
    # CIM can see the new process before publishing its executable path.
    # Wait within the existing deadline; never accept an unverified path.
    if ([string]::IsNullOrWhiteSpace($item.ExecutablePath)) { continue }
    if (-not [string]::Equals($item.ExecutablePath, $worker,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Worker process path differs from reviewed executable.'
    }
    $owner = Invoke-CimMethod -InputObject $item -MethodName GetOwner
    if (-not [string]::Equals("$($owner.Domain)\$($owner.User)",
            "$env:COMPUTERNAME\PalRuntimeSvc", [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Worker process identity differs from PalRuntimeSvc.'
    }
    $workerPid = $item.ProcessId
    break
}
[pscustomobject]@{
    launcher_process_id = $process.Id
    worker_process_id = $workerPid
    ready = ($null -ne $workerPid -and -not $process.HasExited)
    exited = $process.HasExited
    exit_code = if ($process.HasExited) { $process.ExitCode } else { $null }
    stdout_path = $stdout
    stderr_path = $stderr
}
if ($null -eq $workerPid -or $process.HasExited) { exit 2 }
