[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('local', 'active')]
    [string]$Mode,
    [Parameter(Mandatory = $true)]
    [string]$RuntimeRoot,
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,
    [string]$ReferencePng = '',
    [string]$DrawingPng = '',
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

if ($Mode -eq 'local') {
    & $doctor local --write $evidence
} else {
    foreach ($path in @($ReferencePng, $DrawingPng, $GeometryJson)) {
        $full = [IO.Path]::GetFullPath($path)
        if (-not $full.StartsWith((Join-Path $runtime 'artifacts') + '\',
                [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $full -PathType Leaf)) {
            throw 'Active input must be a file under runtime/artifacts.'
        }
    }
    & $doctor active $spec $ReferencePng $DrawingPng $GeometryJson --write $evidence
}
exit $LASTEXITCODE
