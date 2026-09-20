[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidencePath,
    [ValidateSet('local','interpreter','image')][string]$Mode = 'image',
    [string]$FeatureEvidencePath = '',
    [string]$FeatureEvidenceSha256 = '',
    [string]$ReuseVisualEvidencePath = '',
    [string]$ReuseVisualEvidenceSha256 = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$runtime = 'E:\PalimpsesteRuntime'
$expected = "$env:COMPUTERNAME\PalRuntimeSvc"
if ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ine $expected) { throw 'Dedicated service identity required.' }
$evidence = [IO.Path]::GetFullPath($EvidencePath)
if (-not $evidence.StartsWith($runtime + '\evidence\pending\', [StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $evidence)) { throw 'Expected a new private evidence file.' }
if ($ReuseVisualEvidencePath -or $ReuseVisualEvidenceSha256) {
    if ($Mode -ne 'image' -or [string]::IsNullOrWhiteSpace($ReuseVisualEvidencePath) -or
        $ReuseVisualEvidenceSha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw 'Visual evidence reuse is an explicit image-mode option requiring its reviewed SHA-256.'
    }
    $ReuseVisualEvidencePath = [IO.Path]::GetFullPath($ReuseVisualEvidencePath)
    if (-not $ReuseVisualEvidencePath.StartsWith($runtime + '\evidence\pending\', [StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $ReuseVisualEvidencePath -Algorithm SHA256).Hash -ine $ReuseVisualEvidenceSha256) {
        throw 'Visual reuse source must be the hash-bound original private pending report.'
    }
}
foreach ($line in Get-Content -LiteralPath (Join-Path $runtime 'runtime.env')) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line -match '^\s*#') { continue }
    if ($line -notmatch '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { throw 'Invalid runtime configuration.' }
    [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2].Trim(), 'Process')
}
foreach ($name in @('OPENAI_API_KEY','CODEX_API_KEY','CODEX_ACCESS_TOKEN','CHATGPT_TOKEN')) {
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}
$env:PALIMPSESTE_SPEC_ROOT = Join-Path $runtime 'spec-image-v13'
$env:PALIMPSESTE_IMAGE_CODEX_EXE = Join-Path $runtime 'bin\codex-image.exe'
$env:PALIMPSESTE_CODEX_EXE = $env:PALIMPSESTE_IMAGE_CODEX_EXE
$env:PALIMPSESTE_IMAGE_GENERATION_VERIFIED = 'false'
if ($Mode -ne 'local') {
    if (-not $FeatureEvidencePath.StartsWith($runtime + '\approved-evidence\', [StringComparison]::OrdinalIgnoreCase) -or
        $FeatureEvidenceSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        (Get-FileHash -LiteralPath $FeatureEvidencePath -Algorithm SHA256).Hash -ine $FeatureEvidenceSha256) {
        throw 'A reviewed local feature proof for the filtered binary is required.'
    }
    $env:PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_PATH = $FeatureEvidencePath
    $env:PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_SHA256 = $FeatureEvidenceSha256
    $env:PALIMPSESTE_RUNTIME_FEATURES_VERIFIED = 'true'
}
$doctor = Join-Path $runtime 'bin\ProviderImageDoctor.exe'
if ($Mode -eq 'local') {
    & $doctor local --write $evidence
    exit $LASTEXITCODE
}
if ($Mode -eq 'interpreter') {
    & $doctor interpreter $env:PALIMPSESTE_SPEC_ROOT (Join-Path $runtime 'artifacts\image-reference-bird-reference.png') (Join-Path $runtime 'artifacts\image-reference-bird-drawing.png') --write $evidence
    exit $LASTEXITCODE
}
$frozen = Join-Path $runtime 'artifacts\image-reference-bird-description.json'
$ink = Join-Path $runtime 'artifacts\image-reference-bird-ink.png'
$sha = (Get-FileHash -LiteralPath $frozen -Algorithm SHA256).Hash.ToLowerInvariant()
$imageArguments = @('image', $env:PALIMPSESTE_SPEC_ROOT, $frozen, '--a-sha256', $sha, '--ink', $ink, '--write', $evidence)
if ($ReuseVisualEvidencePath) {
    $imageArguments += @('--reuse-visual-evidence', $ReuseVisualEvidencePath,
        '--reuse-visual-evidence-sha256', $ReuseVisualEvidenceSha256)
}
& $doctor @imageArguments
exit $LASTEXITCODE
