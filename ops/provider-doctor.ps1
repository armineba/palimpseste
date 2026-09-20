[CmdletBinding()]
param(
    [ValidateSet('local', 'active', 'plan', 'validate')]
    [string]$Mode = 'local',
    [Parameter(Mandatory = $true)]
    [string]$EnvFile,
    [string]$SpecRoot = '',
    [string]$ReferencePng = '',
    [string]$DrawingPng = '',
    [string]$InkPng = '',
    [string]$FrozenAJson = '',
    [string]$FrozenASha256 = '',
    [string]$FrozenBJson = '',
    [string]$FrozenBSha256 = '',
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
if (-not [string]::IsNullOrWhiteSpace($GeometryJson)) {
    throw 'GeometryJson est obsolète. Fournir InkPng pour recalculer la géométrie.'
}
if ($Mode -eq 'active') {
    if ([string]::IsNullOrWhiteSpace($SpecRoot) -or [string]::IsNullOrWhiteSpace($ReferencePng) -or
        [string]::IsNullOrWhiteSpace($DrawingPng) -or [string]::IsNullOrWhiteSpace($InkPng)) {
        throw 'Le mode active exige SpecRoot, ReferencePng, DrawingPng et InkPng.'
    }
    $doctorArgs = @('active', (Resolve-FullPath $SpecRoot), (Resolve-FullPath $ReferencePng),
        (Resolve-FullPath $DrawingPng), '--ink', (Resolve-FullPath $InkPng), '--write', $EvidencePath)
} elseif ($Mode -eq 'plan') {
    if ([string]::IsNullOrWhiteSpace($SpecRoot) -or [string]::IsNullOrWhiteSpace($FrozenAJson) -or
        [string]::IsNullOrWhiteSpace($InkPng) -or $FrozenASha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw 'Le mode plan exige SpecRoot, FrozenAJson, FrozenASha256 et InkPng.'
    }
    $doctorArgs = @('plan', (Resolve-FullPath $SpecRoot), (Resolve-FullPath $FrozenAJson),
        '--a-sha256', $FrozenASha256, '--ink', (Resolve-FullPath $InkPng), '--write', $EvidencePath)
} elseif ($Mode -eq 'validate') {
    if ([string]::IsNullOrWhiteSpace($SpecRoot) -or [string]::IsNullOrWhiteSpace($FrozenAJson) -or
        [string]::IsNullOrWhiteSpace($FrozenBJson) -or [string]::IsNullOrWhiteSpace($InkPng) -or
        $FrozenASha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
        $FrozenBSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw 'Le mode validate exige SpecRoot, A/B figés, leurs SHA-256 et InkPng.'
    }
    $doctorArgs = @('validate', (Resolve-FullPath $SpecRoot), (Resolve-FullPath $FrozenAJson),
        (Resolve-FullPath $FrozenBJson), '--a-sha256', $FrozenASha256,
        '--b-sha256', $FrozenBSha256, '--ink', (Resolve-FullPath $InkPng), '--write', $EvidencePath)
}

Push-Location ([IO.Path]::GetDirectoryName($DoctorExecutable))
try {
    & $DoctorExecutable @doctorArgs
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $exitCode
