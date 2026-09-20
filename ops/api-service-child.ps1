[CmdletBinding()]
param([string]$RuntimeRoot = 'E:\PalimpsesteRuntime')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedIdentity = "$env:COMPUTERNAME\PalRuntimeSvc"
if (-not [string]::Equals([Security.Principal.WindowsIdentity]::GetCurrent().Name,
        $expectedIdentity, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Owner lab API must run as PalRuntimeSvc.'
}

$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\', '/')
$apiRoot = Join-Path $runtime 'api'
$apiExecutable = Join-Path $apiRoot 'Palimpseste.Api.exe'
$runtimeEnv = Join-Path $runtime 'runtime.env'
$databaseEnv = 'C:\ProgramData\Palimpseste\lab-db.env'
foreach ($required in @($apiExecutable, $runtimeEnv, $databaseEnv)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw 'Owner lab API input missing.' }
}

function Read-Value([string]$Path, [string]$Name) {
    $matches = @(Get-Content -LiteralPath $Path -Encoding UTF8 |
        Where-Object { $_.StartsWith($Name + '=', [StringComparison]::Ordinal) })
    if ($matches.Count -ne 1) { throw "Expected one $Name entry." }
    $value = $matches[0].Substring($Name.Length + 1).Trim()
    if ([string]::IsNullOrWhiteSpace($value)) { throw "Empty $Name entry." }
    return $value
}

$databaseUrl = Read-Value $databaseEnv 'DATABASE_URL'
$artifactRoot = [IO.Path]::GetFullPath((Read-Value $runtimeEnv 'ARTIFACT_ROOT'))
$specRoot = [IO.Path]::GetFullPath((Read-Value $runtimeEnv 'PALIMPSESTE_SPEC_ROOT'))
if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container) -or
    -not (Test-Path -LiteralPath (Join-Path $specRoot 'reference\reference_layout.png') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $specRoot 'reference\reference_free_canvas.png') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $specRoot 'reference\layout-v2.json') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $specRoot 'contracts\capability-catalog.json') -PathType Leaf)) {
    throw 'Owner lab API assets missing.'
}

[Environment]::SetEnvironmentVariable('DATABASE_URL', $databaseUrl, 'Process')
[Environment]::SetEnvironmentVariable('ARTIFACT_ROOT', $artifactRoot, 'Process')
[Environment]::SetEnvironmentVariable('PALIMPSESTE_SPEC_ROOT', $specRoot, 'Process')
[Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://127.0.0.1:18080', 'Process')
[Environment]::SetEnvironmentVariable('REFERENCE_PNG',
    (Join-Path $specRoot 'reference\reference_layout.png'), 'Process')
[Environment]::SetEnvironmentVariable('REFERENCE_FREE_PNG',
    (Join-Path $specRoot 'reference\reference_free_canvas.png'), 'Process')
[Environment]::SetEnvironmentVariable('CAPABILITY_CATALOG',
    (Join-Path $specRoot 'contracts\capability-catalog.json'), 'Process')
[Environment]::SetEnvironmentVariable('CATALOG_VERSION', 'sp.capabilities/1.0', 'Process')
[Environment]::SetEnvironmentVariable('RULES_PROFILE', 'lab_v1', 'Process')
[Environment]::SetEnvironmentVariable('ALLOWED_CLIENT_VERSION', '0.1.0', 'Process')
foreach ($name in @('OPENAI_API_KEY', 'CODEX_API_KEY', 'CODEX_ACCESS_TOKEN', 'CHATGPT_TOKEN')) {
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}

Set-Location -LiteralPath $apiRoot
& $apiExecutable
exit $LASTEXITCODE
