<#
.SYNOPSIS
Deploys the reviewed D13 candidate after a successful real G/B image doctor.
.DESCRIPTION
Operator script. Never calls a model, builds software, or purchases credits.
The historical codex.exe stays on disk unchanged, but A/B/G all select the newly
hardened codex-image.exe with fresh compatibility and isolation evidence.
The proofs and staged executables must already exist. Backups include private
runtime.env and a database dump: keep them private.
On failure after stopping services, it leaves the deployment stopped and records
the completed phase. It never rewinds the database or silently starts old code.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceEvidence,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$SourceEvidenceSha256,
    [Parameter(Mandatory)][string]$SourceInterpreterEvidence,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$SourceInterpreterEvidenceSha256,
    [Parameter(Mandatory)][string]$SourceFeatureEvidence,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$SourceFeatureEvidenceSha256,
    [string]$SourceImageExecutable = 'E:\PalimpsesteRuntime\bin\codex-image.exe',
    [string]$StageRoot = 'E:\PalimpsesteBuildStage\image-visual-v13',
    [string]$SourceSpecRoot = 'E:\PalimpsesteRuntime\spec-image-v13',
    [string]$RuntimeRoot = 'E:\PalimpsesteRuntime',
    [string]$DatabaseEnvFile = 'C:\ProgramData\Palimpseste\lab-db.env',
    [string]$ExpectedDatabase = 'palimpseste_lab',
    [string]$BackupRoot = 'C:\ProgramData\Palimpseste\backups',
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\17\bin'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Elevated operator required.' }
. (Join-Path $PSScriptRoot 'DbEnv.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\', '/')
$stage = [IO.Path]::GetFullPath($StageRoot).TrimEnd('\', '/')
$specSource = [IO.Path]::GetFullPath($SourceSpecRoot).TrimEnd('\', '/')
if ($runtime -ine 'E:\PalimpsesteRuntime' -or $stage -ine 'E:\PalimpsesteBuildStage\image-visual-v13') {
    throw 'Deployment accepts only the reviewed runtime and D13 staging roots.'
}
$envFile = Join-Path $runtime 'runtime.env'
$abCodex = Join-Path $runtime 'bin\codex.exe'
$imageCodex = Join-Path $runtime 'bin\codex-image.exe'
$apiExe = Join-Path $runtime 'api\Palimpseste.Api.exe'
$workerExe = Join-Path $runtime 'bin\Palimpseste.Worker.exe'
$credentialPath = 'C:\ProgramData\Palimpseste\operator-credentials\PalRuntimeSvc.credential.xml'
$serviceName = "$env:COMPUTERNAME\PalRuntimeSvc"
$psql = Join-Path $PostgresBin 'psql.exe'
$pgdump = Join-Path $PostgresBin 'pg_dump.exe'
$migration = Join-Path $repo 'backend\migrations\008_visual_reference.sql'
$utf8 = [Text.UTF8Encoding]::new($false)

function Assert-RegularPath([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Deployment path contains a reparse point.'
            }
        }
        $parent = [IO.Path]::GetDirectoryName($cursor)
        if ($parent -eq $cursor) { break }; $cursor = $parent
    }
}
function Assert-Inside([string]$Path, [string]$Boundary) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($Boundary.TrimEnd('\', '/') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Deployment path is outside its reviewed boundary.'
    }
    Assert-RegularPath $full
}
function File-Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Assert-Hash([string]$Path, [string]$Hash) {
    Assert-RegularPath $Path
    if ($Hash -notmatch '^[0-9a-fA-F]{64}$' -or (File-Hash $Path) -ine $Hash) { throw 'Reviewed file hash mismatch.' }
}
function Read-Entries([string]$Path) {
    $entries = @{}
    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line -match '^\s*#') { continue }
        if ($line -notmatch '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$' -or $entries.ContainsKey($Matches[1])) {
            throw 'Invalid or duplicate private environment setting.'
        }
        $entries[$Matches[1]] = $Matches[2].Trim()
    }
    return $entries
}
function Assert-Protected([string]$Path) {
    $acl = Get-Acl -LiteralPath $Path
    $trusted = @('S-1-5-18', 'S-1-5-32-544')
    if ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin $trusted) {
        throw 'Runtime protected path has an untrusted owner.'
    }
    $write = [Security.AccessControl.FileSystemRights]::Write -bor
        [Security.AccessControl.FileSystemRights]::Delete -bor
        [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor
        [Security.AccessControl.FileSystemRights]::ChangePermissions -bor
        [Security.AccessControl.FileSystemRights]::TakeOwnership
    foreach ($rule in $acl.Access) {
        if ($rule.AccessControlType -eq 'Allow' -and ($rule.FileSystemRights -band $write) -ne 0 -and
            $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -notin $trusted) {
            throw 'Runtime protected path is writable outside SYSTEM/Administrators.'
        }
    }
}
function Read-ServiceProcess([string]$Executable) {
    $name = [IO.Path]::GetFileName($Executable)
    $processes = @(Get-CimInstance Win32_Process -Filter "Name='$name'")
    if ($processes.Count -gt 1) { throw 'Multiple runtime service processes require operator inspection.' }
    foreach ($process in $processes) {
        if ($process.ExecutablePath -ine $Executable) { throw 'A same-name process belongs to another deployment.' }
        $owner = Invoke-CimMethod -InputObject $process -MethodName GetOwner
        if ("$($owner.Domain)\$($owner.User)" -ine $serviceName) { throw 'Unexpected runtime process owner.' }
    }
    return $processes
}
function Stop-ExactService([string]$Executable) {
    foreach ($process in @(Read-ServiceProcess $Executable)) {
        $live = Get-Process -Id $process.ProcessId -ErrorAction Stop
        if ($live.Path -ine $Executable) { throw 'Runtime PID changed before stopping.' }
        Stop-Process -InputObject $live -Force
        if (-not $live.WaitForExit(10000)) { throw 'Runtime process did not stop.' }
    }
}
function Set-Pin([string]$Path, [string]$Variable, [string]$Hash) {
    $content = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    $pattern = '(?m)^\$' + [regex]::Escape($Variable) + " = '[0-9A-Fa-f]{64}'(?=\r?$)"
    if ([regex]::Matches($content, $pattern).Count -ne 1) { throw 'Launcher hash pin is missing or ambiguous.' }
    $line = '$' + $Variable + " = '" + $Hash + "'"
    $content = [regex]::Replace($content, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m) $line })
    [IO.File]::WriteAllText($Path, $content, $utf8)
}
function Copy-ProtectedFile([string]$Source, [string]$Destination, $DefaultAcl) {
    Assert-Inside $Destination $runtime
    $acl = if (Test-Path -LiteralPath $Destination) { Get-Acl -LiteralPath $Destination } else { $DefaultAcl }
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    Set-Acl -LiteralPath $Destination -AclObject $acl
    Assert-Hash $Destination (File-Hash $Source)
    Assert-Protected $Destination
}

foreach ($path in @($runtime, $stage, $specSource, $envFile, $abCodex, $apiExe, $workerExe,
        $SourceImageExecutable, $SourceEvidence, $SourceInterpreterEvidence, $SourceFeatureEvidence,
        $DatabaseEnvFile, $credentialPath, $psql, $pgdump, $migration)) {
    if (-not (Test-Path -LiteralPath $path)) { throw 'A required deployment input is missing.' }
    Assert-RegularPath $path
}
foreach ($tree in @($stage, $specSource, (Join-Path $runtime 'bin'), (Join-Path $runtime 'api'), (Join-Path $runtime 'spec'))) {
    foreach ($item in Get-ChildItem -LiteralPath $tree -Recurse -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Deployment tree contains a reparse point.' }
    }
}
foreach ($path in @($runtime, $envFile, $abCodex, $apiExe, $workerExe,
        (Join-Path $runtime 'bin'), (Join-Path $runtime 'api'), (Join-Path $runtime 'spec'),
        (Join-Path $runtime 'approved-evidence'))) { Assert-Protected $path }
$config = Read-Entries $envFile
foreach ($entry in @{ PALIMPSESTE_SERVICE_USER = 'PalRuntimeSvc';
        PALIMPSESTE_INTERPRETER_MODEL = 'gpt-5.6-sol'; PALIMPSESTE_INTERPRETER_EFFORT = 'high';
        PALIMPSESTE_PLANNER_MODEL = 'gpt-6-astra'; PALIMPSESTE_PLANNER_EFFORT = 'high' }.GetEnumerator()) {
    if ($config[$entry.Key] -cne $entry.Value) { throw ('Existing stage policy differs: ' + $entry.Key) }
}
if ($config.PALIMPSESTE_CODEX_EXE -notin @($abCodex, $imageCodex)) { throw 'Unexpected previously configured Codex executable.' }
$abHash = File-Hash $abCodex
$oldEvidence = @{}
foreach ($prefix in @('PALIMPSESTE_INTERPRETER_EVIDENCE', 'PALIMPSESTE_EFFORT_EVIDENCE', 'PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE')) {
    if ([string]::IsNullOrWhiteSpace($config[$prefix + '_PATH'])) { continue }
    Assert-Inside $config[$prefix + '_PATH'] (Join-Path $runtime 'approved-evidence')
    Assert-Hash $config[$prefix + '_PATH'] $config[$prefix + '_SHA256']
    $oldEvidence[$config[$prefix + '_PATH']] = $config[$prefix + '_SHA256']
}
$credential = Import-Clixml -LiteralPath $credentialPath
if ($credential -isnot [Management.Automation.PSCredential] -or $credential.UserName -ine $serviceName) {
    throw 'Dedicated service credential is invalid.'
}
$credential = $null
Assert-Inside $SourceEvidence (Join-Path $runtime 'evidence\pending')
Assert-Hash $SourceEvidence $SourceEvidenceSha256
if ((Get-Item -LiteralPath $SourceEvidence).Length -gt 100000) { throw 'Image proof is too large.' }
$proof = Get-Content -LiteralPath $SourceEvidence -Raw -Encoding UTF8 | ConvertFrom-Json
if ($proof.kind -cne 'provider_image_generation_doctor' -or $proof.active_result -cne 'success' -or
    $proof.mode -cne 'image' -or $proof.model_calls_executed -cne $true -or
    $proof.service_identity -cne 'PalRuntimeSvc' -or $proof.expected_service_identity -cne 'PalRuntimeSvc' -or
    $proof.requested_model -cne 'gpt-6-astra' -or $proof.requested_effort -cne 'high' -or
    $proof.cli_image_generation_contract -cne 'palimpseste.codex-image/1.0' -or
    $proof.image_generation_text_only -cne $true -or $proof.image_generation_one_shot -cne $true -or
    $proof.plan_validation_status -cne 'success' -or $proof.compilation_status -cne 'success') {
    throw 'The real image doctor did not validate the required G/B contract.'
}
Assert-Hash $SourceImageExecutable ([string]$proof.cli_executable_sha256)
foreach ($stageName in @('stage_g', 'stage_b')) {
    $observed = $proof.$stageName
    if ($observed.outcome -cne 'Success' -or $observed.process_started -cne $true -or $observed.exit_code -ne 0 -or
        $observed.requested_model -cne 'gpt-6-astra' -or $observed.reported_model -cne 'gpt-6-astra' -or
        $observed.requested_effort -cne 'high' -or $observed.reported_effort -cne 'high') {
        throw 'Image doctor stage attestation mismatch.'
    }
    $final = Join-Path ([string]$observed.attempt_directory) 'final.json'
    Assert-Inside $final (Join-Path $runtime 'attempts')
    Assert-Hash $final ([string]$observed.final_sha256)
}
$features = @('shell_tool','computer_use','browser_use','browser_use_external','browser_use_full_cdp_access',
    'apps','plugins','hooks','multi_agent','skill_mcp_dependency_install','shell_snapshot','web_search',
    'standalone_web_search','web_search_cached','web_search_request','remote_plugin','goals','memories','personality',
    'code_mode','in_app_browser','in_app_chat','in_app_dictation','in_app_local_automation','in_app_updates',
    'image_generation','skill_search','tool_suggest','view_image','workspace_dependencies','sleep_tool',
    'tool_call_mcp_elicitation','auth_elicitation','code_mode_host')
foreach ($feature in $features) {
    if ($proof.feature_list_observations.$feature -cne ($feature -eq 'image_generation')) {
        throw 'Image runtime feature isolation was not observed.'
    }
}
foreach ($source in @(@{path=$SourceInterpreterEvidence; sha=$SourceInterpreterEvidenceSha256},
        @{path=$SourceFeatureEvidence; sha=$SourceFeatureEvidenceSha256})) {
    Assert-Inside $source.path (Join-Path $runtime 'evidence\pending')
    Assert-Hash $source.path $source.sha
    if ((Get-Item -LiteralPath $source.path).Length -gt 100000) { throw 'Fresh compatibility proof is too large.' }
}
$interpreter = Get-Content -LiteralPath $SourceInterpreterEvidence -Raw -Encoding UTF8 | ConvertFrom-Json
$local = Get-Content -LiteralPath $SourceFeatureEvidence -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($fresh in @($interpreter, $local)) {
    if ($fresh.service_identity -cne 'PalRuntimeSvc' -or
        $fresh.expected_service_identity -cne 'PalRuntimeSvc' -or
        $fresh.cli_executable_sha256 -ine $proof.cli_executable_sha256) {
        throw 'Fresh proof identity or hardened executable hash mismatch.'
    }
}
if ($local.kind -cne 'provider_doctor' -or $local.mode -cne 'local' -or
    $local.requested_model -cne 'gpt-6-astra' -or $local.requested_effort -cne 'high' -or
    $local.interpreter_requested_model -cne 'gpt-5.6-sol' -or $local.interpreter_requested_effort -cne 'high' -or
    $local.model_calls_executed -cne $false -or
    $local.disable_flag_parser_status -cne 'ok' -or $local.feature_list_status -cne 'ok' -or
    $local.feature_list_observation_complete -cne $true -or
    $local.feature_list_effective_disable_observed -cne $true -or @($local.active_test_issues).Count -ne 0) {
    throw 'Fresh A/B isolation observations failed.'
}
foreach ($feature in $features) {
    if ($local.feature_list_observations.$feature -cne $false) { throw 'A/B feature isolation was not observed on hardened Codex.' }
}
if ($interpreter.kind -cne 'interpreter_multimodal_probe' -or $interpreter.result -cne 'success' -or
    $interpreter.model_calls_executed -cne $true -or $interpreter.process_started -cne $true -or
    $interpreter.exit_code -ne 0 -or $interpreter.output_schema_valid -cne $true -or
    $interpreter.requested_model -cne 'gpt-5.6-sol' -or $interpreter.reported_model -cne 'gpt-5.6-sol' -or
    $interpreter.requested_effort -cne 'high' -or $interpreter.reported_effort -cne 'high' -or
    $interpreter.reference_sha256 -notmatch '^[0-9a-f]{64}$' -or $interpreter.drawing_sha256 -notmatch '^[0-9a-f]{64}$' -or
    $interpreter.reference_sha256 -ceq $interpreter.drawing_sha256) {
    throw 'Fresh two-image A probe did not validate the required model and effort.'
}
Assert-Inside ([string]$interpreter.final_file) (Join-Path $runtime 'attempts')
Assert-Hash ([string]$interpreter.final_file) ([string]$interpreter.final_sha256)
# The successful image doctor supplies the B compatibility proof as well as G.
# Verify the same frozen-description/receipt/image/plan linkage as Settings.
if ($proof.compiler_version -cne 'sp.compiler/1.0' -or $proof.compiled_probe_sha256 -notmatch '^[0-9a-f]{64}$' -or
    $proof.stage_b_model_call_executed -cne $true -or $proof.stage_a_reuse.hash_verified -cne $true -or
    $proof.stage_a_reuse.model_call_executed -cne $false -or
    $proof.geometry.source -notin @('controlled_geometry_from_interpretation','resolver_from_ink_and_description') -or
    $proof.geometry.description_sha256 -cne $proof.stage_a_reuse.sha256 -or
    $proof.geometry.ink_sha256 -notmatch '^[0-9a-f]{64}$' -or $proof.geometry.context_sha256 -notmatch '^[0-9a-f]{64}$') {
    throw 'Image planner evidence does not prove frozen-description reuse and compilation.'
}
$frozenSource=[IO.Path]::GetFullPath([string]$proof.stage_a_reuse.source_file)
$insideInput=$frozenSource.StartsWith($config.PALIMPSESTE_INPUT_ROOT.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)
$insideAttempt=$frozenSource.StartsWith((Join-Path $runtime 'attempts') + '\', [StringComparison]::OrdinalIgnoreCase)
if (-not $insideInput -and -not $insideAttempt) { throw 'Image doctor description is outside trusted inputs and attempts.' }
Assert-Hash $frozenSource ([string]$proof.stage_a_reuse.sha256)
$gFinal=Join-Path ([string]$proof.stage_g.attempt_directory) 'final.json'
$bFinal=Join-Path ([string]$proof.stage_b.attempt_directory) 'final.json'
$receipt=Get-Content -LiteralPath $gFinal -Raw -Encoding UTF8 | ConvertFrom-Json
$imagePlan=Get-Content -LiteralPath $bFinal -Raw -Encoding UTF8 | ConvertFrom-Json
if ($receipt.schema_version -cne 'sp.visual-reference-receipt/1.0' -or $receipt.status -cne 'generated' -or
    $receipt.description_sha256 -cne $proof.stage_a_reuse.sha256 -or
    $imagePlan.description_sha256 -cne $proof.stage_a_reuse.sha256 -or
    $imagePlan.visual_reference_sha256 -cne $proof.image.sha256) {
    throw 'G receipt or B plan is not tied to the attested frozen description and PNG.'
}
Assert-Inside ([string]$proof.image.saved_path) (Join-Path $config.PALIMPSESTE_CODEX_HOME 'generated_images')
Assert-Hash ([string]$proof.image.saved_path) ([string]$proof.image.sha256)
if ((Get-Item -LiteralPath ([string]$proof.image.saved_path)).Length -gt 8388608) { throw 'Image evidence exceeds its byte budget.' }
$png = [IO.File]::ReadAllBytes([string]$proof.image.saved_path)
if ($png.Length -lt 24 -or $png.Length -gt 8388608 -or
    [BitConverter]::ToString($png, 0, 8) -cne '89-50-4E-47-0D-0A-1A-0A' -or
    [Text.Encoding]::ASCII.GetString($png, 12, 4) -cne 'IHDR') { throw 'Image evidence PNG is invalid.' }
$width = [long]$png[16] * 16777216 + [long]$png[17] * 65536 + [long]$png[18] * 256 + $png[19]
$height = [long]$png[20] * 16777216 + [long]$png[21] * 65536 + [long]$png[22] * 256 + $png[23]
if ($width -lt 512 -or $width -gt 2048 -or $height -lt 512 -or $height -gt 2048 -or
    $width -ne $proof.image.width -or $height -ne $proof.image.height) { throw 'Image evidence dimensions mismatch.' }
$png = $null
foreach ($relative in @('worker\Palimpseste.Worker.exe','api\Palimpseste.Api.exe','doctor\ProviderDoctor.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $stage $relative) -PathType Leaf)) { throw 'D13 publish is incomplete.' }
}
foreach ($relative in @('prompts\04_IMAGE_REFERENCE.md','prompts\02_MODEL_B_TRADUCTEUR.md',
        'contracts\codex\model-g.output-schema.json','contracts\compiled-spell.schema.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $specSource $relative) -PathType Leaf)) { throw 'D13 specification is incomplete.' }
}
$db = Read-PalimpsesteConnection -EnvFile $DatabaseEnvFile
if ($db.Database -cne $ExpectedDatabase -or $db.Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Expected local database does not match the private configuration.'
}
$initialApi = @(Read-ServiceProcess $apiExe)
$initialWorker = @(Read-ServiceProcess $workerExe)
$backupRootFull = [IO.Path]::GetFullPath($BackupRoot).TrimEnd('\', '/')
Assert-RegularPath $backupRootFull
if ($backupRootFull.StartsWith($runtime + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $runtime.StartsWith($backupRootFull + '\', [StringComparison]::OrdinalIgnoreCase) -or $backupRootFull -ieq $runtime) {
    throw 'Private backup must be outside the runtime tree.'
}
$backup = Join-Path $backupRootFull ('d13-deploy-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
Set-PrivateAcl $backup
$record = [ordered]@{ kind='d13_deployment'; started_at=[DateTimeOffset]::UtcNow.ToString('o');
    phase='preflight_passed'; completed=$false; migration_applied=$false; runtime=$runtime; stage=$stage;
    source_evidence_sha256=$SourceEvidenceSha256.ToLowerInvariant(); historical_codex_sha256=$abHash;
    interpreter_evidence_sha256=$SourceInterpreterEvidenceSha256.ToLowerInvariant();
    feature_evidence_sha256=$SourceFeatureEvidenceSha256.ToLowerInvariant();
    image_codex_sha256=[string]$proof.cli_executable_sha256; backup=$backup; human_acceptance='not_asserted' }
$recordPath = Join-Path $backup 'deployment.json'
function Save-Record { [IO.File]::WriteAllText($recordPath, ($record | ConvertTo-Json -Depth 8), $utf8) }
Save-Record
$pgPrevious = @{}
foreach ($name in @('PGHOST','PGPORT','PGUSER','PGPASSWORD','PGDATABASE')) {
    $pgPrevious[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$env:PGHOST=$db.Host; $env:PGPORT=$db.Port; $env:PGUSER=$db.Username; $env:PGPASSWORD=$db.Password; $env:PGDATABASE=$db.Database
$db = $null
function Read-ActiveJobs {
    # Windows PowerShell 5.1 turns native stderr (including PostgreSQL NOTICE)
    # into error records. Keep it in the private log and judge the process exit,
    # so a benign notice cannot abort a successful command or migration retry.
    $ErrorActionPreference = 'Continue'
    # Native commands update the global automatic variable in Windows PowerShell.
    # A local LASTEXITCODE would shadow it and stay null after a successful query.
    $global:LASTEXITCODE = $null
    $lines = @(& $psql -X -w -A -t -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM jobs WHERE state IN ('queued','interpreting','generating_visual_reference','resolving_geometry','planning','validating','waiting_retry');" 2> (Join-Path $backup 'postgres-private.stderr.txt'))
    $nativeExitCode = $global:LASTEXITCODE
    if ($null -eq $nativeExitCode -or $nativeExitCode -ne 0 -or $lines.Count -ne 1 -or $lines[0] -notmatch '^\d+$') { throw ('Database quiescence check failed (native exit {0}, output lines {1}); consult the private backup log.' -f $nativeExitCode, $lines.Count) }
    return [long]$lines[0]
}
function Invoke-LoggedPostgres([string]$Executable, [string[]]$CommandArguments, [string]$LogName) {
    $ErrorActionPreference = 'Continue'
    $global:LASTEXITCODE = $null
    & $Executable @CommandArguments 1> (Join-Path $backup ($LogName + '.stdout.txt')) 2> (Join-Path $backup ($LogName + '.stderr.txt'))
    $nativeExitCode = $global:LASTEXITCODE
    if ($null -eq $nativeExitCode -or $nativeExitCode -ne 0) {
        throw ('PostgreSQL command failed; consult private ' + $LogName + ' logs.')
    }
}
try {
    if ((Read-ActiveJobs) -ne 0) { throw 'Runtime jobs are active or queued; finish them before deploying.' }
    # Stop admission first, then verify that no provider job appeared meanwhile.
    Stop-ExactService $apiExe
    $record.phase='api_stopped'; Save-Record
    if ((Read-ActiveJobs) -ne 0) { throw 'A job arrived before API shutdown; worker remains running to finish it.' }
    Stop-ExactService $workerExe
    $record.phase='services_stopped'; Save-Record
    $aclRecords = [Collections.Generic.List[object]]::new()
    foreach ($name in @('bin','api','spec')) {
        $source = Join-Path $runtime $name
        $aclRecords.Add(@{ path=$source; sddl=(Get-Acl -LiteralPath $source).Sddl })
        foreach ($item in Get-ChildItem -LiteralPath $source -Recurse -Force) {
            $aclRecords.Add(@{ path=$item.FullName; sddl=(Get-Acl -LiteralPath $item.FullName).Sddl })
        }
        Copy-Item -LiteralPath $source -Destination (Join-Path $backup $name) -Recurse
    }
    Copy-Item -LiteralPath $envFile -Destination (Join-Path $backup 'runtime.env')
    $aclRecords.Add(@{ path=$envFile; sddl=(Get-Acl -LiteralPath $envFile).Sddl })
    [IO.File]::WriteAllText((Join-Path $backup 'original-acls.json'), ($aclRecords | ConvertTo-Json -Depth 5), $utf8)
    Invoke-LoggedPostgres $pgdump @('-w','--format=custom','--no-owner','--no-privileges','--file',
        (Join-Path $backup 'database-before-008.dump')) 'pgdump-private'
    $record.phase='backup_complete'; Save-Record
    Invoke-LoggedPostgres $psql @('-X','-w','-v','ON_ERROR_STOP=1','-f',$migration) 'migration-008'
    $record.migration_applied=$true; $record.phase='migration_complete'; Save-Record
    $binaryAcl = Get-Acl -LiteralPath $workerExe
    $apiFileAcl = Get-Acl -LiteralPath $apiExe
    $specFileAcl = Get-Acl -LiteralPath (Join-Path $runtime 'spec\contracts\capability-catalog.json')
    $specDirAcl = Get-Acl -LiteralPath (Join-Path $runtime 'spec\contracts')
    Copy-ProtectedFile (Join-Path $stage 'worker\Palimpseste.Worker.exe') $workerExe $binaryAcl
    Copy-ProtectedFile (Join-Path $stage 'doctor\ProviderDoctor.exe') (Join-Path $runtime 'bin\ProviderDoctor.exe') $binaryAcl
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $stage 'api') -File) {
        if ($file.Extension -eq '.pdb') { continue }
        Copy-ProtectedFile $file.FullName (Join-Path $runtime ('api\' + $file.Name)) $apiFileAcl
    }
    if ([IO.Path]::GetFullPath($SourceImageExecutable) -ine $imageCodex) {
        Copy-ProtectedFile $SourceImageExecutable $imageCodex $binaryAcl
    } else { Assert-Protected $imageCodex }
    foreach ($directory in Get-ChildItem -LiteralPath $specSource -Directory -Recurse | Sort-Object { $_.FullName.Length }) {
        $destination = Join-Path (Join-Path $runtime 'spec') $directory.FullName.Substring($specSource.Length + 1)
        Assert-Inside $destination (Join-Path $runtime 'spec')
        if (-not (Test-Path -LiteralPath $destination)) {
            New-Item -ItemType Directory -Path $destination | Out-Null
            Set-Acl -LiteralPath $destination -AclObject $specDirAcl
        }
        Assert-Protected $destination
    }
    foreach ($file in Get-ChildItem -LiteralPath $specSource -File -Recurse) {
        Copy-ProtectedFile $file.FullName (Join-Path (Join-Path $runtime 'spec') $file.FullName.Substring($specSource.Length + 1)) $specFileAcl
    }
    $launchRoot = Join-Path $backup 'launch'
    New-Item -ItemType Directory -Path $launchRoot | Out-Null
    foreach ($name in @('start-owner-api.ps1','start-owner-worker.ps1','api-service-child.ps1','worker-service-child.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $launchRoot $name)
    }
    $workerHash = File-Hash $workerExe; $apiHash = File-Hash $apiExe
    Set-Pin (Join-Path $launchRoot 'worker-service-child.ps1') 'expectedWorkerSha256' $workerHash
    Copy-ProtectedFile (Join-Path $launchRoot 'worker-service-child.ps1') (Join-Path $runtime 'bin\WorkerService.Child.ps1') $binaryAcl
    Copy-ProtectedFile (Join-Path $launchRoot 'api-service-child.ps1') (Join-Path $runtime 'bin\ApiService.Child.ps1') $binaryAcl
    Set-Pin (Join-Path $launchRoot 'start-owner-worker.ps1') 'expectedWorkerSha256' $workerHash
    Set-Pin (Join-Path $launchRoot 'start-owner-worker.ps1') 'expectedChildSha256' (File-Hash (Join-Path $runtime 'bin\WorkerService.Child.ps1'))
    Set-Pin (Join-Path $launchRoot 'start-owner-api.ps1') 'expectedApiSha256' $apiHash
    Set-Pin (Join-Path $launchRoot 'start-owner-api.ps1') 'expectedChildSha256' (File-Hash (Join-Path $runtime 'bin\ApiService.Child.ps1'))
    # Freeze the reviewed proof in a service-read-only folder. Its native PNG
    # and attempt final files remain at their attested paths for gate rechecks.
    $approved = Join-Path $runtime ('approved-evidence\doctor-image-' + [Guid]::NewGuid().ToString('N') + '.json')
    Assert-Hash $SourceEvidence $SourceEvidenceSha256
    Copy-ProtectedFile $SourceEvidence $approved (Get-Acl -LiteralPath $envFile)
    $approvedInterpreter=Join-Path $runtime ('approved-evidence\doctor-hardened-interpreter-' + [Guid]::NewGuid().ToString('N') + '.json')
    $approvedFeature=Join-Path $runtime ('approved-evidence\doctor-hardened-local-' + [Guid]::NewGuid().ToString('N') + '.json')
    Assert-Hash $SourceInterpreterEvidence $SourceInterpreterEvidenceSha256
    Assert-Hash $SourceFeatureEvidence $SourceFeatureEvidenceSha256
    Copy-ProtectedFile $SourceInterpreterEvidence $approvedInterpreter (Get-Acl -LiteralPath $envFile)
    Copy-ProtectedFile $SourceFeatureEvidence $approvedFeature (Get-Acl -LiteralPath $envFile)
    $updates = @{
        PALIMPSESTE_SPEC_ROOT=(Join-Path $runtime 'spec');
        PALIMPSESTE_CODEX_EXE=$imageCodex; PALIMPSESTE_IMAGE_CODEX_EXE=$imageCodex;
        PALIMPSESTE_IMAGEGEN_TEXT_ONLY='1';
        PALIMPSESTE_INTERPRETER_VERIFIED='true'; PALIMPSESTE_INTERPRETER_EVIDENCE_PATH=$approvedInterpreter;
        PALIMPSESTE_INTERPRETER_EVIDENCE_SHA256=(File-Hash $approvedInterpreter);
        PALIMPSESTE_EFFORT_VERIFIED='true'; PALIMPSESTE_EFFORT_EVIDENCE_PATH=$approved;
        PALIMPSESTE_EFFORT_EVIDENCE_SHA256=(File-Hash $approved);
        PALIMPSESTE_RUNTIME_FEATURES_VERIFIED='true'; PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_PATH=$approvedFeature;
        PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_SHA256=(File-Hash $approvedFeature);
        PALIMPSESTE_IMAGE_GENERATION_VERIFIED='true'; PALIMPSESTE_IMAGE_GENERATION_EVIDENCE_PATH=$approved;
        PALIMPSESTE_IMAGE_GENERATION_EVIDENCE_SHA256=(File-Hash $approved)
    }
    $lines = [Collections.Generic.List[string]]::new(); $seen = @{}
    foreach ($line in Get-Content -LiteralPath $envFile -Encoding UTF8) {
        if ($line -match '^([A-Za-z_][A-Za-z0-9_]*)=' -and $updates.ContainsKey($Matches[1])) {
            $key=$Matches[1]; $seen[$key]=$true; $lines.Add($key + '=' + $updates[$key])
        } else { $lines.Add($line) }
    }
    foreach ($key in $updates.Keys) { if (-not $seen.ContainsKey($key)) { $lines.Add($key + '=' + $updates[$key]) } }
    $envAcl=Get-Acl -LiteralPath $envFile
    [IO.File]::WriteAllText($envFile, (($lines -join "`r`n") + "`r`n"), $utf8)
    Set-Acl -LiteralPath $envFile -AclObject $envAcl
    Assert-Protected $envFile
    Assert-Hash $abCodex $abHash
    Assert-Hash $imageCodex ([string]$proof.cli_executable_sha256)
    foreach ($path in $oldEvidence.Keys) { Assert-Hash $path $oldEvidence[$path] }
    $record.phase='candidate_installed'; $record.worker_sha256=$workerHash; $record.api_sha256=$apiHash
    $record.approved_image_evidence=$approved; $record.approved_interpreter_evidence=$approvedInterpreter;
    $record.approved_feature_evidence=$approvedFeature; $record.active_codex_executable=$imageCodex;
    $record.launcher_directory=$launchRoot; Save-Record
    $powershell='C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
    # These reviewed launchers create hidden service processes using the
    # existing protected credential, and verify identity/path/health.
    & $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $launchRoot 'start-owner-api.ps1') -RuntimeRoot $runtime
    if ($LASTEXITCODE -ne 0) { throw 'D13 API failed readiness; inspect private service logs.' }
    & $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $launchRoot 'start-owner-worker.ps1') -RuntimeRoot $runtime
    if ($LASTEXITCODE -ne 0) { throw 'D13 worker failed startup; inspect private service logs.' }
    $record.api_pid=@(Read-ServiceProcess $apiExe)[0].ProcessId
    $record.worker_pid=@(Read-ServiceProcess $workerExe)[0].ProcessId
    $record.phase='services_running'; $record.completed=$true; $record.finished_at=[DateTimeOffset]::UtcNow.ToString('o'); Save-Record
    [pscustomobject]@{ deployed=$true; backup=$backup; manifest=$recordPath; api_pid=$record.api_pid;
        worker_pid=$record.worker_pid; model_calls_by_deployment=0; human_artistic_acceptance='not_asserted' }
} catch {
    $record.failure_type=$_.Exception.GetType().FullName
    # Private report only: retain the precise controlled error and source
    # location, never serialize environment values or the credential object.
    $record.failure_message=$_.Exception.Message
    $record.failure_location=$_.ScriptStackTrace
    $record.completed=$false
    # Once candidates are installed, leave no partially restarted service pair.
    if ($record.phase -eq 'candidate_installed') {
        try {
            Stop-ExactService $apiExe
            Stop-ExactService $workerExe
        } catch { $record.stop_failure_message=$_.Exception.Message }
    }
    Save-Record
    throw ('D13 deployment stopped at phase ' + $record.phase + '. Private backup/report: ' + $backup +
        '. Inspect the failure before restarting; no database rollback was attempted.')
} finally {
    foreach ($name in $pgPrevious.Keys) { [Environment]::SetEnvironmentVariable($name, $pgPrevious[$name], 'Process') }
}
