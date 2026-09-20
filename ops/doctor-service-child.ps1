[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('local', 'interpreter', 'astra', 'active', 'plan', 'validate')]
    [string]$Mode,
    [Parameter(Mandatory = $true)]
    [string]$RuntimeRoot,
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,
    [string]$ReferencePng = '',
    [string]$DrawingPng = '',
    [string]$InkPng = '',
    [string]$FrozenAJson = '',
    [string]$FrozenASha256 = '',
    [string]$FrozenBJson = '',
    [string]$FrozenBSha256 = '',
    [string]$GeometryJson = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$expected = "$env:COMPUTERNAME\PalRuntimeSvc"
if (-not [string]::Equals([Security.Principal.WindowsIdentity]::GetCurrent().Name,
        $expected, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Doctor child must run as PalRuntimeSvc.'
}
$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\', '/')
$pending = Join-Path $runtime 'evidence\pending'
$evidence = [IO.Path]::GetFullPath($EvidencePath)
if (-not $evidence.StartsWith($pending + '\', [StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $evidence)) {
    throw 'Evidence path must be new and inside evidence/pending.'
}
$envFile = Join-Path $runtime 'runtime.env'
$doctor = Join-Path $runtime 'bin\ProviderDoctor.exe'
$spec = Join-Path $runtime 'spec'
if (-not (Test-Path -LiteralPath $envFile -PathType Leaf) -or
    -not (Test-Path -LiteralPath $doctor -PathType Leaf)) {
    throw 'Runtime doctor or environment missing.'
}
foreach ($line in Get-Content -LiteralPath $envFile) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line -match '^\s*#') { continue }
    if ($line -notmatch '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { throw 'Invalid runtime.env line.' }
    [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2].Trim(), 'Process')
}
foreach ($name in @('OPENAI_API_KEY', 'CODEX_API_KEY', 'CODEX_ACCESS_TOKEN', 'CHATGPT_TOKEN')) {
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}

if (-not [string]::IsNullOrWhiteSpace($GeometryJson)) {
    throw 'GeometryJson is deprecated. Supply InkPng.'
}
if ($Mode -eq 'local') {
    & $doctor local --write $evidence
} else {
    $requiredArtifacts = if ($Mode -in @('interpreter', 'astra')) { @($ReferencePng, $DrawingPng) }
        elseif ($Mode -eq 'active') { @($ReferencePng, $DrawingPng, $InkPng) } else { @($InkPng) }
    foreach ($path in $requiredArtifacts) {
        $full = [IO.Path]::GetFullPath($path)
        if (-not $full.StartsWith((Join-Path $runtime 'artifacts') + '\',
                [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $full -PathType Leaf)) {
            throw 'Active input must be a file under runtime/artifacts.'
        }
    }
    if ($Mode -in @('interpreter', 'astra')) {
        & $doctor interpreter $spec $ReferencePng $DrawingPng --write $evidence
    } elseif ($Mode -eq 'active') {
        & $doctor active $spec $ReferencePng $DrawingPng --ink $InkPng --write $evidence
    } elseif ($Mode -eq 'plan') {
        $frozen = [IO.Path]::GetFullPath($FrozenAJson)
        $artifacts = Join-Path $runtime 'artifacts'
        $attempts = Join-Path $runtime 'attempts'
        if ((-not $frozen.StartsWith($artifacts + '\', [StringComparison]::OrdinalIgnoreCase) -and
             -not $frozen.StartsWith($attempts + '\', [StringComparison]::OrdinalIgnoreCase)) -or
            -not (Test-Path -LiteralPath $frozen -PathType Leaf) -or
            $FrozenASha256 -notmatch '^[0-9A-Fa-f]{64}$') {
            throw 'Frozen A must be under artifacts or attempts with a SHA-256.'
        }
        & $doctor plan $spec $frozen --a-sha256 $FrozenASha256 --ink $InkPng --write $evidence
    } else {
        $aFinal = [IO.Path]::GetFullPath($FrozenAJson)
        $bFinal = [IO.Path]::GetFullPath($FrozenBJson)
        $attempts = Join-Path $runtime 'attempts'
        foreach ($path in @($aFinal, $bFinal)) {
            if (-not $path.StartsWith($attempts + '\', [StringComparison]::OrdinalIgnoreCase) -or
                -not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw 'Offline validation requires A/B under runtime/attempts.'
            }
        }
        if ($FrozenASha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
            $FrozenBSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
            throw 'Offline validation requires exact A/B SHA-256 values.'
        }
        & $doctor validate $spec $aFinal $bFinal --a-sha256 $FrozenASha256 `
            --b-sha256 $FrozenBSha256 --ink $InkPng --write $evidence
    }
}
exit $LASTEXITCODE
