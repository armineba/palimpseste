[CmdletBinding()]
param([string]$RuntimeRoot = 'E:\PalimpsesteRuntime')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\', '/')
if (-not [string]::Equals($runtime, 'E:\PalimpsesteRuntime', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Owner lab runtime root differs from the reviewed path.'
}
$child = Join-Path $runtime 'bin\ApiService.Child.ps1'
$api = Join-Path $runtime 'api\Palimpseste.Api.exe'
$credentialPath = 'C:\ProgramData\Palimpseste\operator-credentials\PalRuntimeSvc.credential.xml'
$pending = Join-Path $runtime 'evidence\pending'
foreach ($required in @($child, $api, $credentialPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw 'Owner lab API deployment incomplete.' }
}
if (-not (Test-Path -LiteralPath $pending -PathType Container)) { throw 'Private log folder missing.' }
foreach ($path in @($runtime, $child, $api)) {
    $item = [IO.Path]::GetFullPath($path)
    while ($item) {
        if ((Get-Item -LiteralPath $item -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Owner lab API path uses a reparse point.'
        }
        $parent = [IO.Path]::GetDirectoryName($item)
        if ([string]::Equals($item, $parent, [StringComparison]::OrdinalIgnoreCase)) { break }
        $item = $parent
    }
}
if (-not [string]::Equals((Get-FileHash -LiteralPath $child -Algorithm SHA256).Hash,
        '6B71085864C175C96C538F9EF5507193DBE9304D1B15EBD776C47E50B8FEF02C',
        [StringComparison]::OrdinalIgnoreCase) -or
    -not [string]::Equals((Get-FileHash -LiteralPath $api -Algorithm SHA256).Hash,
        '8B280D15ADEA95D4322C8957913ED3A46659221E208A4938B391E9CDBFE1E825',
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Owner lab API binaries differ from reviewed hashes.'
}

function Get-OwnerApiListener {
    $listeners = @(Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 18080 `
        -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -eq 0) { return $null }
    if ($listeners.Count -ne 1) { throw 'Ambiguous owner API listener.' }
    $listenerPid = $listeners[0].OwningProcess
    $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId=$listenerPid"
    if ($null -eq $processInfo -or
        -not [string]::Equals($processInfo.ExecutablePath, $api, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Port 18080 is held by another executable.'
    }
    $owner = Invoke-CimMethod -InputObject $processInfo -MethodName GetOwner
    if (-not [string]::Equals("$($owner.Domain)\$($owner.User)",
            "$env:COMPUTERNAME\PalRuntimeSvc", [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Port 18080 is held by another identity.'
    }
    return $listenerPid
}

$existingPid = Get-OwnerApiListener
if ($null -ne $existingPid) {
    $response = Invoke-WebRequest -Uri 'http://127.0.0.1:18080/health/ready' `
        -UseBasicParsing -TimeoutSec 3
    if ($response.StatusCode -ne 200) { throw 'Existing owner API is not ready.' }
    [pscustomobject]@{ process_id = $existingPid; ready = $true; already_running = $true }
    return
}

$credential = Import-Clixml -LiteralPath $credentialPath
if ($credential -isnot [System.Management.Automation.PSCredential] -or
    -not [string]::Equals($credential.UserName, "$env:COMPUTERNAME\PalRuntimeSvc",
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unexpected service credential.'
}

$stamp = [Guid]::NewGuid().ToString('N')
$stdout = Join-Path $pending "owner-api-$stamp.stdout.txt"
$stderr = Join-Path $pending "owner-api-$stamp.stderr.txt"
$arguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
    '-File', ('"' + $child + '"'), '-RuntimeRoot', ('"' + $runtime + '"'))
$process = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments `
    -Credential $credential -LoadUserProfile -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput $stdout -RedirectStandardError $stderr

$ready = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
    if ($process.HasExited) { break }
    try {
        $response = Invoke-WebRequest -Uri 'http://127.0.0.1:18080/health/ready' -UseBasicParsing -TimeoutSec 1
        if ($response.StatusCode -eq 200 -and $null -ne (Get-OwnerApiListener)) {
            $ready = $true
            break
        }
    } catch { }
}
[pscustomobject]@{
    process_id = if ($ready) { Get-OwnerApiListener } else { $null }
    launcher_pid = $process.Id
    ready = $ready
    already_running = $false
    exited = $process.HasExited
    exit_code = if ($process.HasExited) { $process.ExitCode } else { $null }
    stdout_path = $stdout
    stderr_path = $stderr
}
if (-not $ready) { exit 2 }
