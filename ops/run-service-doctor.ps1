[CmdletBinding()]
param(
    [ValidateSet('local', 'active')]
    [string]$Mode = 'local',
    [Parameter(Mandatory = $true)]
    [string]$CredentialFile,
    [string]$RuntimeRoot = 'E:\PalimpsesteRuntime',
    [string]$ReferencePng = '',
    [string]$DrawingPng = '',
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
if ($Mode -eq 'active') {
    foreach ($path in @($ReferencePng, $DrawingPng, $GeometryJson)) {
        if ([string]::IsNullOrWhiteSpace($path) -or
            -not (IsInside $path $artifactRoot) -or
            -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw 'Active doctor requires three existing files under artifacts.'
        }
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
        '-GeometryJson', ('"' + (FullPath $GeometryJson) + '"'))
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
