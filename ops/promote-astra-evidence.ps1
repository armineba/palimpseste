[CmdletBinding()]
param(
    [string]$RuntimeRoot = 'E:\PalimpsesteRuntime',
    [string]$SourceEvidence = 'E:\PalimpsesteRuntime\evidence\pending\doctor-astra-free-canvas-20260920.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\', '/')
if (-not [string]::Equals($runtime, 'E:\PalimpsesteRuntime', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unexpected runtime root.'
}
$pending = Join-Path $runtime 'evidence\pending'
$approved = Join-Path $runtime 'approved-evidence'
$source = [IO.Path]::GetFullPath($SourceEvidence)
if (-not $source.StartsWith($pending + '\', [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Pending proof missing.' }
$proof = Get-Content -LiteralPath $source -Raw -Encoding UTF8 | ConvertFrom-Json
$cli = Join-Path $runtime 'bin\codex.exe'
$reference = Join-Path $runtime 'artifacts\astra-probe-reference.png'
$drawing = Join-Path $runtime 'artifacts\astra-probe-drawing.png'
$attemptRoot = Join-Path $runtime 'attempts'
$final = [IO.Path]::GetFullPath([string]$proof.final_file)
if ($proof.kind -cne 'astra_multimodal_probe' -or $proof.result -cne 'success' -or
    $proof.service_identity -cne 'PalRuntimeSvc' -or
    $proof.requested_model -cne 'gpt-6-astra' -or $proof.reported_model -cne 'gpt-6-astra' -or
    $proof.requested_effort -cne 'max' -or $proof.reported_effort -cne 'max' -or
    $proof.model_calls_executed -cne $true -or $proof.process_started -cne $true -or
    $proof.exit_code -cne 0 -or $proof.output_schema_valid -cne $true -or
    -not $final.StartsWith($attemptRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $final -PathType Leaf)) {
    throw 'Astra active probe does not establish the technical gate.'
}
foreach ($check in @(
    @($cli, [string]$proof.cli_executable_sha256),
    @($reference, [string]$proof.reference_sha256),
    @($drawing, [string]$proof.drawing_sha256),
    @($final, [string]$proof.final_sha256)
)) {
    if (-not (Test-Path -LiteralPath $check[0] -PathType Leaf) -or
        (Get-FileHash -LiteralPath $check[0] -Algorithm SHA256).Hash -cne $check[1].ToUpperInvariant()) {
        throw 'Astra probe artifact hash mismatch.'
    }
}
$envFile = Join-Path $runtime 'runtime.env'
$lunaProof = Join-Path $approved 'doctor-composite-ab13-b11.json'
if (-not (Test-Path -LiteralPath $lunaProof -PathType Leaf)) { throw 'Existing Luna proof missing.' }
$target = Join-Path $approved 'doctor-astra-free-canvas-20260920.json'
if (Test-Path -LiteralPath $target) { throw 'Approved Astra proof already exists.' }
$backup = Join-Path $approved ('runtime-env-before-astra-' + [Guid]::NewGuid().ToString('N') + '.bak')
$original = [IO.File]::ReadAllText($envFile, [Text.Encoding]::UTF8)
$envAcl = Get-Acl -LiteralPath $envFile
Copy-Item -LiteralPath $envFile -Destination $backup
Set-Acl -LiteralPath $backup -AclObject $envAcl
Copy-Item -LiteralPath $source -Destination $target
Set-Acl -LiteralPath $target -AclObject (Get-Acl -LiteralPath $lunaProof)
$sha = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
$entries = @{
    PALIMPSESTE_LUNA_MODEL_A = 'gpt-6-astra'
    PALIMPSESTE_LUNA_MODEL_B = 'gpt-5.6-luna'
    PALIMPSESTE_ASTRA_MODEL = 'gpt-6-astra'
    PALIMPSESTE_ASTRA_EFFORT = 'max'
    PALIMPSESTE_ASTRA_VERIFIED = 'true'
    PALIMPSESTE_ASTRA_EVIDENCE_PATH = $target
    PALIMPSESTE_ASTRA_EVIDENCE_SHA256 = $sha
}
$lines = [System.Collections.Generic.List[string]]::new()
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in ($original -split "`r?`n")) {
    if ($line -match '^([A-Za-z_][A-Za-z0-9_]*)=') {
        $name = $Matches[1]
        if ($entries.ContainsKey($name)) {
            if (-not $seen.Add($name)) { throw "Duplicate runtime setting $name." }
            $lines.Add($name + '=' + $entries[$name])
            continue
        }
    }
    $lines.Add($line)
}
foreach ($name in $entries.Keys) {
    if (-not $seen.Contains($name)) { $lines.Add($name + '=' + $entries[$name]) }
}
[IO.File]::WriteAllText($envFile, (($lines -join "`r`n").TrimEnd("`r", "`n") + "`r`n"),
    [Text.UTF8Encoding]::new($false))
Set-Acl -LiteralPath $envFile -AclObject $envAcl
[pscustomobject]@{
    approved_evidence = $target
    evidence_sha256 = $sha
    runtime_env_backup = $backup
    technical_gate = 'open'
    artistic_verdict = 'pending'
}
