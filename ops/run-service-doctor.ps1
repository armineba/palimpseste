[CmdletBinding()]
param(
    [ValidateSet('local', 'active', 'plan', 'validate')]
    [string]$Mode = 'local',
    [Parameter(Mandatory = $true)]
    [string]$CredentialFile,
    [string]$RuntimeRoot = 'E:\PalimpsesteRuntime',
    [string]$ReferencePng = '',
    [string]$DrawingPng = '',
    [string]$InkPng = '',
    [string]$FrozenAJson = '',
    [string]$FrozenASha256 = '',
    [string]$FrozenBJson = '',
    [string]$FrozenBSha256 = '',
    [string]$GeometryJson = '',
    [string]$EvidencePath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function FullPath([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw 'Required path missing.' }
    return [IO.Path]::GetFullPath($Value)
}
function IsInside([string]$Candidate, [string]$Root) {
    $candidateFull = (FullPath $Candidate).TrimEnd('\', '/')
    $rootFull = (FullPath $Root).TrimEnd('\', '/')
    return $candidateFull.StartsWith($rootFull + '\', [StringComparison]::OrdinalIgnoreCase)
}

$runtime = FullPath $RuntimeRoot
$pending = FullPath (Join-Path $runtime 'evidence\pending')
$artifactRoot = FullPath (Join-Path $runtime 'artifacts')
$attemptRoot = FullPath (Join-Path $runtime 'attempts')
$credentialPath = FullPath $CredentialFile
$doctor = FullPath (Join-Path $runtime 'bin\ProviderDoctor.exe')
$childScript = FullPath (Join-Path $runtime 'bin\ProviderDoctor.Service.ps1')
if (-not (Test-Path -LiteralPath $pending -PathType Container) -or
    -not (Test-Path -LiteralPath $doctor -PathType Leaf) -or
    -not (Test-Path -LiteralPath $childScript -PathType Leaf) -or
    -not (Test-Path -LiteralPath $credentialPath -PathType Leaf)) {
    throw 'Runtime, doctor, child script or private credential missing.'
}
if (IsInside $credentialPath $runtime) { throw 'CredentialFile must remain outside the player runtime.' }
if ([string]::IsNullOrWhiteSpace($EvidencePath)) {
    $EvidencePath = Join-Path $pending ('doctor-{0}-{1}.json' -f $Mode, [Guid]::NewGuid().ToString('N'))
}
$EvidencePath = FullPath $EvidencePath
if (-not (IsInside $EvidencePath $pending) -or (Test-Path -LiteralPath $EvidencePath)) {
    throw 'Evidence must be a new file under evidence/pending.'
}
if (-not [string]::IsNullOrWhiteSpace($GeometryJson)) {
    throw 'GeometryJson is deprecated. Supply InkPng so geometry is resolved from the frozen description.'
}
if ($Mode -in @('active', 'plan', 'validate')) {
    $requiredArtifacts = if ($Mode -eq 'active') { @($ReferencePng, $DrawingPng, $InkPng) } else { @($InkPng) }
    foreach ($path in $requiredArtifacts) {
        if ([string]::IsNullOrWhiteSpace($path) -or
            -not (IsInside $path $artifactRoot) -or
            -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw 'Doctor input must be an existing file under artifacts.'
        }
    }
}
if ($Mode -eq 'plan') {
    if ([string]::IsNullOrWhiteSpace($FrozenAJson) -or
        -not ((IsInside $FrozenAJson $artifactRoot) -or (IsInside $FrozenAJson $attemptRoot)) -or
        -not (Test-Path -LiteralPath $FrozenAJson -PathType Leaf) -or
        $FrozenASha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw 'Plan doctor requires frozen A under artifacts or attempts and its exact SHA-256.'
    }
}
if ($Mode -eq 'validate') {
    if ([string]::IsNullOrWhiteSpace($FrozenAJson) -or
        [string]::IsNullOrWhiteSpace($FrozenBJson) -or
        -not (IsInside $FrozenAJson $attemptRoot) -or
        -not (IsInside $FrozenBJson $attemptRoot) -or
        -not (Test-Path -LiteralPath $FrozenAJson -PathType Leaf) -or
        -not (Test-Path -LiteralPath $FrozenBJson -PathType Leaf) -or
        $FrozenASha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
        $FrozenBSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw 'Offline validation requires frozen A/B final files under attempts and exact SHA-256 values.'
    }
}
$credential = Import-Clixml -LiteralPath $credentialPath
if ($credential -isnot [System.Management.Automation.PSCredential] -or
    -not [string]::Equals($credential.UserName,
        "$env:COMPUTERNAME\PalRuntimeSvc", [StringComparison]::OrdinalIgnoreCase)) {
    throw 'CredentialFile does not designate PalRuntimeSvc on this PC.'
}
$logStem = [IO.Path]::Combine([IO.Path]::GetDirectoryName($EvidencePath),
    [IO.Path]::GetFileNameWithoutExtension($EvidencePath))
$stdout = $logStem + '.stdout.txt'
$stderr = $logStem + '.stderr.txt'
$arguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
    '-File', ('"' + $childScript + '"'), '-Mode', $Mode,
    '-RuntimeRoot', ('"' + $runtime + '"'),
    '-EvidencePath', ('"' + $EvidencePath + '"'))
if ($Mode -eq 'active') {
    $arguments += @('-ReferencePng', ('"' + (FullPath $ReferencePng) + '"'),
        '-DrawingPng', ('"' + (FullPath $DrawingPng) + '"'),
        '-InkPng', ('"' + (FullPath $InkPng) + '"'))
} elseif ($Mode -eq 'plan') {
    $arguments += @('-FrozenAJson', ('"' + (FullPath $FrozenAJson) + '"'),
        '-FrozenASha256', $FrozenASha256,
        '-InkPng', ('"' + (FullPath $InkPng) + '"'))
} elseif ($Mode -eq 'validate') {
    $arguments += @('-FrozenAJson', ('"' + (FullPath $FrozenAJson) + '"'),
        '-FrozenASha256', $FrozenASha256,
        '-FrozenBJson', ('"' + (FullPath $FrozenBJson) + '"'),
        '-FrozenBSha256', $FrozenBSha256,
        '-InkPng', ('"' + (FullPath $InkPng) + '"'))
}
$process = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments `
    -Credential $credential -LoadUserProfile -WindowStyle Hidden -Wait -PassThru `
    -RedirectStandardOutput $stdout -RedirectStandardError $stderr
Write-Output ([pscustomobject]@{
    mode = $Mode
    exit_code = $process.ExitCode
    evidence_path = $EvidencePath
    evidence_created = (Test-Path -LiteralPath $EvidencePath -PathType Leaf)
    stdout_path = $stdout
    stderr_path = $stderr
})
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
