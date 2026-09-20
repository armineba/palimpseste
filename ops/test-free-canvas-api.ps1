[CmdletBinding()]
param(
    [string]$ApiBin = 'E:\PalimpsesteBuildStage\api-free-canvas',
    [string]$ArtifactRoot = 'E:\PalimpsesteBuildStage\test-artifacts-free-canvas',
    [string]$TestDbEnv = 'C:\ProgramData\Palimpseste\test-db.env',
    [string]$SmokeTokenEnv = 'C:\ProgramData\Palimpseste\smoke-token.env'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$databaseLine = @(Get-Content -LiteralPath $TestDbEnv -Encoding UTF8 |
    Where-Object { $_.StartsWith('DATABASE_URL=', [StringComparison]::Ordinal) })
$tokenLine = @(Get-Content -LiteralPath $SmokeTokenEnv -Encoding UTF8 |
    Where-Object { $_.StartsWith('token=', [StringComparison]::Ordinal) })
if ($databaseLine.Count -ne 1 -or $tokenLine.Count -ne 1 -or
    $databaseLine[0] -notmatch 'Database=palimpseste_test(?:;|$)') {
    throw 'Disposable test database or smoke token is missing.'
}
if (@(Get-NetTCPConnection -LocalPort 18081 -State Listen -ErrorAction SilentlyContinue).Count -ne 0) {
    throw 'Test API port 18081 is already in use.'
}
$executable = Join-Path $ApiBin 'Palimpseste.Api.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Published test API missing.' }
New-Item -ItemType Directory -Force -Path $ArtifactRoot | Out-Null
$env:DATABASE_URL = $databaseLine[0].Substring('DATABASE_URL='.Length)
$env:ARTIFACT_ROOT = [IO.Path]::GetFullPath($ArtifactRoot)
$env:PALIMPSESTE_SPEC_ROOT = $projectRoot
$env:ASPNETCORE_URLS = 'http://127.0.0.1:18081'
$env:REFERENCE_PNG = Join-Path $projectRoot 'reference\reference_layout.png'
$env:REFERENCE_FREE_PNG = Join-Path $projectRoot 'reference\reference_free_canvas.png'
$env:CAPABILITY_CATALOG = Join-Path $projectRoot 'contracts\capability-catalog.json'
$env:PALIMPSESTE_API_URL = 'http://127.0.0.1:18081'
$env:PALIMPSESTE_SMOKE_TOKEN = $tokenLine[0].Substring('token='.Length)
$env:PALIMPSESTE_TEST_DB_ENV = $TestDbEnv
$env:PALIMPSESTE_TEST_ARTIFACT_ROOT = $ArtifactRoot
$process = Start-Process -FilePath $executable -WorkingDirectory $ApiBin -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $ApiBin 'smoke-stdout.txt') `
    -RedirectStandardError (Join-Path $ApiBin 'smoke-stderr.txt') -PassThru
try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { break }
        try {
            $response = Invoke-WebRequest -Uri 'http://127.0.0.1:18081/health/ready' `
                -UseBasicParsing -TimeoutSec 1
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        } catch { }
    }
    if (-not $ready) { throw 'Disposable test API did not become ready.' }
    & python (Join-Path $projectRoot 'tests\api_http_smoke.py')
    if ($LASTEXITCODE -ne 0) { throw 'Free canvas HTTP smoke failed.' }
} finally {
    $process.Refresh()
    if (-not $process.HasExited) { & taskkill.exe /PID $process.Id /F | Out-Null }
    foreach ($name in @('DATABASE_URL','PALIMPSESTE_SMOKE_TOKEN')) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
}
