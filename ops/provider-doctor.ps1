[CmdletBinding()]
param(
    [ValidateSet('local', 'active')]
    [string]$Mode = 'local',
    [Parameter(Mandatory = $true)]
    [string]$EnvFile,
    [string]$SpecRoot = '',
    [string]$ReferencePng = '',
    [string]$DrawingPng = '',
    [string]$GeometryJson = '',
    [string]$EvidencePath = '',
    [string]$DoctorExecutable = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-FullPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { throw 'Chemin requis.' }
    return [IO.Path]::GetFullPath($PathValue)
}

function Import-EnvironmentFile([string]$PathValue) {
    $path = Resolve-FullPath $PathValue
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Env introuvable : $path" }
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { throw "Ligne env invalide : $line" }
        $name = $Matches[1]
        $value = $Matches[2].Trim()
        if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}

Import-EnvironmentFile $EnvFile
$repoRoot = Resolve-FullPath (Join-Path $PSScriptRoot '..')
if ([string]::IsNullOrWhiteSpace($DoctorExecutable)) { $DoctorExecutable = $env:PALIMPSESTE_PROVIDER_DOCTOR_EXE }
if ([string]::IsNullOrWhiteSpace($DoctorExecutable) -and -not [string]::IsNullOrWhiteSpace($env:PALIMPSESTE_RUNTIME_ROOT)) {
    $DoctorExecutable = Join-Path $env:PALIMPSESTE_RUNTIME_ROOT 'bin\ProviderDoctor.exe'
}
if ([string]::IsNullOrWhiteSpace($DoctorExecutable)) {
    $DoctorExecutable = Join-Path $repoRoot 'doctor\ProviderDoctor.exe'
    if (-not (Test-Path -LiteralPath $DoctorExecutable -PathType Leaf)) {
        $DoctorExecutable = Join-Path $repoRoot 'deliverables\doctor\ProviderDoctor.exe'
    }
}
$DoctorExecutable = Resolve-FullPath $DoctorExecutable
if (-not (Test-Path -LiteralPath $DoctorExecutable -PathType Leaf)) {
    throw "Executable ProviderDoctor introuvable : $DoctorExecutable. Publier le backend avant le preflight."
}

if ([string]::IsNullOrWhiteSpace($EvidencePath)) {
    $evidenceRoot = $env:PALIMPSESTE_PROVIDER_EVIDENCE_ROOT
    if ([string]::IsNullOrWhiteSpace($evidenceRoot)) { $evidenceRoot = Join-Path $repoRoot 'ops\evidence' }
    New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
    $EvidencePath = Join-Path $evidenceRoot ("provider-doctor-{0}-{1}.json" -f $Mode, [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
}
$EvidencePath = Resolve-FullPath $EvidencePath

$doctorArgs = @($Mode, '--write', $EvidencePath)
if ($Mode -eq 'active') {
    if ([string]::IsNullOrWhiteSpace($SpecRoot) -or [string]::IsNullOrWhiteSpace($ReferencePng) -or
        [string]::IsNullOrWhiteSpace($DrawingPng) -or [string]::IsNullOrWhiteSpace($GeometryJson)) {
        throw 'Le mode active exige SpecRoot, ReferencePng, DrawingPng et GeometryJson.'
    }
    $doctorArgs += @(
        (Resolve-FullPath $SpecRoot),
        (Resolve-FullPath $ReferencePng),
        (Resolve-FullPath $DrawingPng),
        (Resolve-FullPath $GeometryJson)
    )
    # The doctor parser accepts --write after positional arguments too.
    $doctorArgs = @('active', (Resolve-FullPath $SpecRoot), (Resolve-FullPath $ReferencePng), (Resolve-FullPath $DrawingPng), (Resolve-FullPath $GeometryJson), '--write', $EvidencePath)
}

Push-Location ([IO.Path]::GetDirectoryName($DoctorExecutable))
try {
    & $DoctorExecutable @doctorArgs
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $exitCode
