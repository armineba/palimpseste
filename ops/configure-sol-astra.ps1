[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('prepare', 'approve-local', 'approve-active')][string]$Mode,
    [string]$RuntimeRoot = 'E:\PalimpsesteRuntime',
    [string]$SourceEvidence = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\', '/')
if ($runtime -ine 'E:\PalimpsesteRuntime') { throw 'Unexpected runtime root.' }
$approved = Join-Path $runtime 'approved-evidence'
$pending = Join-Path $runtime 'evidence\pending'
$envFile = Join-Path $runtime 'runtime.env'
$attemptRoot = Join-Path $runtime 'attempts'
$cli = Join-Path $runtime 'bin\codex.exe'
foreach ($path in @($envFile, $cli)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Runtime file missing.' }
}
function Verify-File([string]$Path, [string]$Sha, [string]$Boundary) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($Boundary + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $Sha -notmatch '^[0-9a-fA-F]{64}$' -or -not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw 'Evidence file outside trusted boundary or hash missing.'
    }
    if (((Get-Item -LiteralPath $full).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash -ine $Sha) { throw 'Evidence artifact hash mismatch.' }
}
$entries = @{}
if ($Mode -eq 'prepare') {
    $entries = @{
        PALIMPSESTE_INTERPRETER_MODEL = 'gpt-5.6-sol'
        PALIMPSESTE_INTERPRETER_EFFORT = 'high'
        PALIMPSESTE_PLANNER_MODEL = 'gpt-6-astra'
        PALIMPSESTE_PLANNER_EFFORT = 'high'
        PALIMPSESTE_INTERPRETER_VERIFIED = 'false'
        PALIMPSESTE_INTERPRETER_EVIDENCE_PATH = ''
        PALIMPSESTE_INTERPRETER_EVIDENCE_SHA256 = ''
        PALIMPSESTE_EFFORT_VERIFIED = 'false'
        PALIMPSESTE_EFFORT_EVIDENCE_PATH = ''
        PALIMPSESTE_EFFORT_EVIDENCE_SHA256 = ''
        PALIMPSESTE_RUNTIME_FEATURES_VERIFIED = 'false'
        PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_PATH = ''
        PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_SHA256 = ''
    }
} else {
    $source = [IO.Path]::GetFullPath($SourceEvidence)
    if (-not $source.StartsWith($pending + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Expected a pending doctor report.' }
    $proof = Get-Content -LiteralPath $source -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($proof.kind -cne 'provider_doctor' -or $proof.service_identity -cne 'PalRuntimeSvc' -or
        $proof.expected_service_identity -cne 'PalRuntimeSvc' -or $proof.requested_model -cne 'gpt-6-astra' -or
        $proof.requested_effort -cne 'high' -or $proof.interpreter_requested_model -cne 'gpt-5.6-sol' -or
        $proof.interpreter_requested_effort -cne 'high') { throw 'Doctor stage policy does not match Sol high / Astra high.' }
    Verify-File $cli ([string]$proof.cli_executable_sha256) (Join-Path $runtime 'bin')
    if ($Mode -eq 'approve-local') {
        if ($proof.mode -cne 'local' -or $proof.model_calls_executed -cne $false -or
            $proof.disable_flag_parser_status -cne 'ok' -or $proof.feature_list_status -cne 'ok' -or
            $proof.feature_list_observation_complete -cne $true -or
            $proof.feature_list_effective_disable_observed -cne $true -or
            @($proof.active_test_issues).Count -ne 0) { throw 'Local isolation observations did not pass.' }
        $prefix = 'PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE'
        $verified = 'PALIMPSESTE_RUNTIME_FEATURES_VERIFIED'
    } else {
        if ($proof.mode -cne 'active' -or $proof.active_result -cne 'success' -or
            $proof.plan_validation_status -cne 'success' -or $proof.compilation_status -cne 'success' -or
            $proof.compiler_version -cne 'sp.compiler/1.0' -or $proof.compiled_probe_sha256 -notmatch '^[0-9a-f]{64}$') {
            throw 'Active pair must have actually executed, validated and compiled.'
        }
        foreach ($stageName in @('stage_a', 'stage_b')) {
            $stage = $proof.$stageName
            $expectedModel = if ($stageName -eq 'stage_a') { 'gpt-5.6-sol' } else { 'gpt-6-astra' }
            if ($stage.outcome -cne 'Success' -or $stage.requested_model -cne $expectedModel -or
                $stage.reported_model -cne $expectedModel -or $stage.requested_effort -cne 'high' -or
                $stage.reported_effort -cne 'high' -or $stage.process_started -cne $true -or $stage.exit_code -ne 0) {
                throw 'Active stage transport or model attestation mismatch.'
            }
            Verify-File (Join-Path ([string]$stage.attempt_directory) 'final.json') ([string]$stage.final_sha256) $attemptRoot
        }
        if ($proof.geometry.source -notin @('controlled_geometry_from_interpretation', 'resolver_from_ink_and_description') -or
            $proof.geometry.description_sha256 -cne $proof.stage_a.final_sha256) { throw 'Compiled geometry is not bound to the observed interpretation.' }
        $prefix = 'PALIMPSESTE_EFFORT_EVIDENCE'
        $verified = 'PALIMPSESTE_EFFORT_VERIFIED'
    }
    $target = Join-Path $approved ('doctor-sol-astra-' + $Mode + '-' + [Guid]::NewGuid().ToString('N') + '.json')
    Copy-Item -LiteralPath $source -Destination $target
    Set-Acl -LiteralPath $target -AclObject (Get-Acl -LiteralPath $envFile)
    $sha = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    $entries[$prefix + '_PATH'] = $target
    $entries[$prefix + '_SHA256'] = $sha
    $entries[$verified] = 'true'
    if ($Mode -eq 'approve-active') {
        $entries['PALIMPSESTE_INTERPRETER_EVIDENCE_PATH'] = $target
        $entries['PALIMPSESTE_INTERPRETER_EVIDENCE_SHA256'] = $sha
        $entries['PALIMPSESTE_INTERPRETER_VERIFIED'] = 'true'
    }
}
$original = [IO.File]::ReadAllText($envFile, [Text.Encoding]::UTF8)
$envAcl = Get-Acl -LiteralPath $envFile
$backup = Join-Path $approved ('runtime-env-before-sol-astra-' + [Guid]::NewGuid().ToString('N') + '.bak')
Copy-Item -LiteralPath $envFile -Destination $backup
Set-Acl -LiteralPath $backup -AclObject $envAcl
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$lines = [Collections.Generic.List[string]]::new()
foreach ($line in ($original -split "`r?`n")) {
    if ($line -match '^([A-Za-z_][A-Za-z0-9_]*)=' -and $entries.ContainsKey($Matches[1])) {
        $name = $Matches[1]
        if (-not $seen.Add($name)) { throw "Duplicate setting $name." }
        $lines.Add($name + '=' + $entries[$name])
    } else { $lines.Add($line) }
}
foreach ($name in $entries.Keys) { if (-not $seen.Contains($name)) { $lines.Add($name + '=' + $entries[$name]) } }
[IO.File]::WriteAllText($envFile, (($lines -join "`r`n").TrimEnd("`r", "`n") + "`r`n"), [Text.UTF8Encoding]::new($false))
Set-Acl -LiteralPath $envFile -AclObject $envAcl
[pscustomobject]@{ mode = $Mode; runtime_env_backup = $backup; updated_setting_names = @($entries.Keys); human_visual_acceptance = 'not_asserted' }
