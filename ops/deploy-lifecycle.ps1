<# Operator installation only. Does not generate a spell, run a diagnostic, or build software.
   Reuses the existing native Codex identity and observed D13 binary evidence unchanged.
   Gameplay and artistic acceptance remain pending the owner's own trial.
   D21 applies migrations012 then013; migration011 is a prerequisite. Older Player updates apply011. #>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$StageRoot,
    [string]$PlayerRoot = '', [string]$RuntimeRoot = 'E:\PalimpsesteRuntime',
    [ValidatePattern('^D[0-9]+(?:\.[0-9]+)?$')][string]$BackendRevision = 'D21',
    [ValidateSet('1.6.0','1.7.0','1.8.0')][string]$ClientVersion = '1.8.0',
    [string]$PublicReportPath = '',
    [ValidateRange(-1,2147483647)][int]$JobConcurrency = -1,
    [ValidateRange(0,86400)][int]$WaitForIdleSeconds = 0,
    [switch]$DrainExistingJobs)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'DbEnv.ps1')
$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\')
$stage = [IO.Path]::GetFullPath($StageRoot).TrimEnd('\')
if ($runtime -ine 'E:\PalimpsesteRuntime') { throw 'Unexpected runtime root.' }
if (-not $PlayerRoot) { $PlayerRoot = Join-Path $repo 'game\Build\WindowsAnimationSheetPlayable' }
$player = [IO.Path]::GetFullPath($PlayerRoot).TrimEnd('\')
$utf8 = [Text.UTF8Encoding]::new($false)
$service = "$env:COMPUTERNAME\PalRuntimeSvc"
$admin = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
$system = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
$serviceSid = ([Security.Principal.NTAccount]$service).Translate([Security.Principal.SecurityIdentifier])
if (-not ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole($admin)) { throw 'Administrator required.' }
function Regular([string]$Path) {
    for ($cursor = [IO.Path]::GetFullPath($Path); $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse path refused.' }
    }
}
function Under([string]$Path, [string]$Boundary) {
    if (-not [IO.Path]::GetFullPath($Path).StartsWith($Boundary.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside installation scope.' }
    Regular $Path
}
function Digest([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Protect([string]$Path) {
    Under $Path $runtime
    $directory = (Get-Item -LiteralPath $Path).PSIsContainer
    $acl = if ($directory) { [Security.AccessControl.DirectorySecurity]::new() } else { [Security.AccessControl.FileSecurity]::new() }
    $acl.SetOwner($admin); $acl.SetAccessRuleProtection($true, $false)
    $inherit = if ($directory) { [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit' } else { [Security.AccessControl.InheritanceFlags]::None }
    foreach ($sid in @($admin,$system)) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl',$inherit,'None','Allow'))
    }
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($serviceSid,'ReadAndExecute',$inherit,'None','Allow'))
    Set-Acl -LiteralPath $Path -AclObject $acl
}
function Install-File([string]$From, [string]$To) {
    Regular $From; Under $To $runtime
    $parent = [IO.Path]::GetDirectoryName($To)
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Copy-Item -LiteralPath $From -Destination $To -Force
    Protect $To
    if ((Digest $From) -cne (Digest $To)) { throw 'Installed file hash differs.' }
}
function Install-Tree([string]$From, [string]$To) {
    Under $To $runtime; Regular $From
    New-Item -ItemType Directory -Path $To -Force | Out-Null; Protect $To
    foreach ($item in Get-ChildItem -LiteralPath $From -Recurse -Force | Sort-Object { $_.FullName.Length }) {
        Regular $item.FullName
        $target = Join-Path $To $item.FullName.Substring($From.Length + 1)
        if ($item.PSIsContainer) { New-Item -ItemType Directory -Path $target -Force | Out-Null; Protect $target }
        elseif ($item.Extension -ne '.pdb') { Install-File $item.FullName $target }
    }
}
function Stop-ServiceExecutable([string]$Path) {
    foreach ($item in @(Get-CimInstance Win32_Process -Filter ("Name='" + [IO.Path]::GetFileName($Path) + "'"))) {
        if ($item.ExecutablePath -ine $Path) { throw 'Same-name executable belongs to another installation.' }
        $owner = Invoke-CimMethod -InputObject $item -MethodName GetOwner
        if ("$($owner.Domain)\$($owner.User)" -ine $service) { throw 'Unexpected service process owner.' }
        $process = Get-Process -Id $item.ProcessId
        if ($process.Path -ine $Path) { throw 'Service process changed.' }
        Stop-Process -InputObject $process -Force
        if (-not $process.WaitForExit(10000)) { throw 'Service did not stop.' }
    }
}
function Pin([string]$Path,[string]$Variable,[string]$Hash) {
    $content = [IO.File]::ReadAllText($Path)
    $pattern = '(?m)^\$' + [regex]::Escape($Variable) + " = '[0-9a-fA-F]{64}'(?=\r?$)"
    if ([regex]::Matches($content,$pattern).Count -ne 1) { throw 'Missing launcher pin.' }
    $line = '$' + $Variable + " = '" + $Hash + "'"
    [IO.File]::WriteAllText($Path,[regex]::Replace($content,$pattern,[Text.RegularExpressions.MatchEvaluator]{param($m) $line}),$utf8)
}
foreach ($path in @($stage,$player,$runtime)) { Regular $path }
if ($PublicReportPath) {
    $PublicReportPath = [IO.Path]::GetFullPath($PublicReportPath)
    Under $PublicReportPath (Join-Path $repo 'evidence\public\backend')
    if ([IO.Path]::GetExtension($PublicReportPath) -ine '.json') { throw 'Public deployment report must be JSON.' }
    Regular $PublicReportPath
}
foreach ($relative in @('worker\Palimpseste.Worker.exe','api\Palimpseste.Api.exe','doctor\ProviderDoctor.exe','visual-doctor\SpellVisualDoctor.exe','spec\prompts\05_VISUAL_CRITIC.md',
    'spec\assets\sourced-vfx\catalogue.json','spec\assets\sourced-vfx\references.json',
    'spec\assets\sourced-vfx\licenses\Kenney-Particle-Pack-CC0.txt','spec\assets\sourced-vfx\licenses\Kenney-Smoke-Particles-CC0.txt',
    'spec\prompts\history\01_MODEL_A_INTERPRETE_2_3.md','spec\prompts\history\04_IMAGE_REFERENCE_1_2.md',
    'worker\ThirdPartyNotices\System.Drawing.Common-MIT.txt','migrations\011_animation_sheet.sql')) {
    if (-not (Test-Path -LiteralPath (Join-Path $stage $relative) -PathType Leaf)) { throw 'D16 stage incomplete.' }
}
$stageVfx = Join-Path $stage 'spec\assets\sourced-vfx'
if ($ClientVersion -eq '1.8.0') {
    foreach ($relative in @('migrations\012_blueprint_v2.sql','migrations\013_unity_god_methods.sql',
        'spec\prompts\06_BLUEPRINT_V2.md','spec\prompts\07_V2_CRITIC.md',
        'spec\prompts\08_V2_INTERPRETATION.md','spec\prompts\09_V2_NUMERIC_RULES.md','spec\prompts\10_UNITY_GOD_RUNTIME.md',
        'spec\contracts\spell-blueprint-v2.schema.json','spec\contracts\spell-plan-v2.schema.json',
        'spec\contracts\compiled-spell-v2.schema.json','spec\contracts\codex\model-b-v2.output-schema.json',
        'spec\contracts\codex\model-j-v2.output-schema.json','spec\contracts\codex\model-b-unity-god-v2.output-schema.json',
        'spec\skills\unity-god\SKILL.md','spec\skills\unity-god\references\runtime.md',
        'spec\skills\unity-god\references\methods.json','blueprint-doctor\BlueprintV2Doctor.exe')) {
        $requiredFile = Join-Path $stage $relative
        Regular $requiredFile
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) { throw "D21 stage incomplete: $relative" }
    }
    $stageUnityGod = Join-Path $stage 'spec\skills\unity-god'
    foreach ($file in Get-ChildItem -LiteralPath $stageUnityGod -Recurse -File) {
        Regular $file.FullName
        $relative = $file.FullName.Substring($stageUnityGod.Length + 1).Replace('\','/')
        if ($relative -cnotin @('SKILL.md','references/runtime.md','references/methods.json')) {
            throw 'UNITY GOD runtime package contains files outside the reviewed knowledge snapshot.'
        }
    }
}
$migrationNames = if ($ClientVersion -eq '1.8.0') { @('012_blueprint_v2.sql','013_unity_god_methods.sql') } else { @('011_animation_sheet.sql') }
$migrationInputs = @($migrationNames | ForEach-Object {
    $source = Join-Path $stage ('migrations\' + $_); Regular $source
    [ordered]@{file=$_;sha256=Digest $source}
})
foreach ($file in Get-ChildItem -LiteralPath $stageVfx -Recurse -File) {
    Regular $file.FullName
    $relative = $file.FullName.Substring($stageVfx.Length + 1).Replace('\','/')
    if ($relative -notmatch '^(catalogue\.json|references\.json|licenses/[^/]+\.txt|textures/[A-Za-z0-9_/-]+\.png)$') {
        throw 'Runtime VFX package contains files outside the reviewed data-only layout.'
    }
}
$vfxCatalogue = Get-Content -LiteralPath (Join-Path $stageVfx 'catalogue.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($texture in $vfxCatalogue.textures) {
    $source = Join-Path $stageVfx $texture.path; Under $source $stageVfx
    if ($texture.path -notmatch '^textures/[A-Za-z0-9_/-]+\.png$' -or (Digest $source) -cne $texture.sha256) {
        throw 'Runtime VFX texture differs from its reviewed catalogue.'
    }
}
$delivery = Get-Content -LiteralPath (Join-Path $repo 'evidence\public\unity\lifecycle-delivery.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($delivery.client_version -cne $ClientVersion -or $delivery.build_exit -ne 0) { throw 'Successful build of the selected client version required.' }
foreach ($file in $delivery.files) {
    $source = Join-Path $player $file.file; Under $source $player
    if ((Digest $source) -cne $file.sha256) { throw 'Player differs from compiled delivery.' }
}
$envFile = Join-Path $runtime 'runtime.env'
$originalNative = Digest (Join-Path $runtime 'bin\codex-image.exe')
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$logRoot = Join-Path $runtime ('evidence\pending\animation-sheet-install-' + $stamp)
New-Item -ItemType Directory -Path $logRoot | Out-Null
Protect $logRoot
$record = [ordered]@{version=$ClientVersion; backend_revision=$BackendRevision; delivery_name=($BackendRevision + ' backend / Player ' + $ClientVersion); started_at=[DateTimeOffset]::UtcNow.ToString('o'); phase='staging'; completed=$false;
    spell_generations=0; diagnostics_executed=$false; gameplay_tested=$false; visual_acceptance='pending_owner'; native_sha256=$originalNative;
    migrations_planned=$migrationInputs; migrations_applied=@()}
function Save-Record {
    $json = $record | ConvertTo-Json -Depth 7
    [IO.File]::WriteAllText((Join-Path $logRoot 'installation.json'),$json,$utf8)
    if ($PublicReportPath) { [IO.File]::WriteAllText($PublicReportPath,$json,$utf8) }
}
Save-Record
$render = Join-Path $runtime ('render-animation-sheet-' + $stamp)
Install-Tree $player $render
$renderFiles = @(Get-ChildItem -LiteralPath $render -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{file=$_.FullName.Substring($render.Length+1).Replace('\','/');sha256=Digest $_.FullName}
})
$renderManifest = Join-Path $render 'renderer-manifest.json'
[IO.File]::WriteAllText($renderManifest,([ordered]@{version=$ClientVersion;files=$renderFiles} | ConvertTo-Json -Depth 5),$utf8)
Protect $renderManifest
$record.renderer_manifest_sha256 = Digest $renderManifest
$record.renderer = Join-Path $render 'Palimpseste.exe'; Save-Record
$db = Read-PalimpsesteConnection -EnvFile 'C:\ProgramData\Palimpseste\lab-db.env'
if ($db.Database -cne 'palimpseste_lab' -or $db.Host -notin @('127.0.0.1','localhost','::1')) { throw 'Unexpected database.' }
$previous = @{}
foreach ($key in @('PGHOST','PGPORT','PGUSER','PGPASSWORD','PGDATABASE')) { $previous[$key]=[Environment]::GetEnvironmentVariable($key,'Process') }
$env:PGHOST=$db.Host; $env:PGPORT=$db.Port; $env:PGUSER=$db.Username; $env:PGPASSWORD=$db.Password; $env:PGDATABASE=$db.Database; $db=$null
$psql = 'C:\Program Files\PostgreSQL\17\bin\psql.exe'
function Active-Jobs {
    $ErrorActionPreference='Continue'; $global:LASTEXITCODE=$null
    $query = if ($DrainExistingJobs) {
        "SELECT count(*) FROM jobs j WHERE j.state NOT IN ('queued','waiting_retry','ready','needs_operator') OR EXISTS(SELECT 1 FROM provider_attempts a WHERE a.job_id=j.id AND a.status='running');"
    } else { "SELECT count(*) FROM jobs WHERE state NOT IN ('ready','needs_operator');" }
    $lines=@(& $psql -X -w -A -t -v ON_ERROR_STOP=1 -c $query 2> (Join-Path $logRoot 'postgres.stderr.txt'))
    $code=$global:LASTEXITCODE
    if ($code -ne 0 -or $null -eq $code -or $lines.Count -ne 1 -or $lines[0] -notmatch '^\d+$') { throw 'Could not read pending jobs.' }
    return [long]$lines[0]
}
function Set-ClaimPause([bool]$Enabled) {
    # The legacy worker has no drain command. Its sole admission UPDATE increments
    # fence_token; this temporary trigger skips only that UPDATE. Heartbeats,
    # provider attempts, completed work and new HTTP submissions keep running.
    $sql = if ($Enabled) { @'
BEGIN;
SET LOCAL statement_timeout = '5s';
SET LOCAL lock_timeout = '5s';
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='public.jobs'::regclass AND tgname='palimpseste_deployment_pause_claims')
 OR to_regprocedure('public.palimpseste_deployment_pause_claims()') IS NOT NULL THEN
  RAISE EXCEPTION 'An earlier claim pause requires operator inspection';
 END IF;
END $$;
CREATE FUNCTION public.palimpseste_deployment_pause_claims() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER palimpseste_deployment_pause_claims BEFORE UPDATE OF fence_token ON public.jobs
 FOR EACH ROW WHEN (NEW.fence_token > OLD.fence_token) EXECUTE FUNCTION public.palimpseste_deployment_pause_claims();
SELECT json_build_object('schema','public','table','jobs','name','palimpseste_deployment_pause_claims',
 'trigger_oid',oid,'function_oid',tgfoid) FROM pg_trigger
 WHERE tgrelid='public.jobs'::regclass AND tgname='palimpseste_deployment_pause_claims';
COMMIT;
'@ } else { @'
BEGIN;
SET LOCAL statement_timeout = '5s';
SET LOCAL lock_timeout = '5s';
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='public.jobs'::regclass
  AND tgname='palimpseste_deployment_pause_claims' AND oid=__TRIGGER_OID__ AND tgfoid=__FUNCTION_OID__)
 OR to_regprocedure('public.palimpseste_deployment_pause_claims()')::oid IS DISTINCT FROM __FUNCTION_OID__::oid THEN
  RAISE EXCEPTION 'Claim pause identity changed; refusing to remove it';
 END IF;
END $$;
DROP TRIGGER palimpseste_deployment_pause_claims ON public.jobs;
DROP FUNCTION public.palimpseste_deployment_pause_claims();
COMMIT;
'@ }
    if (-not $Enabled) {
        if ($null -eq $script:claimPauseIdentity) { throw 'Claim pause identity not recorded.' }
        $sql=$sql.Replace('__TRIGGER_OID__',([uint32]$script:claimPauseIdentity.trigger_oid).ToString()).Replace('__FUNCTION_OID__',([uint32]$script:claimPauseIdentity.function_oid).ToString())
    }
    $savedPreference=$ErrorActionPreference; $ErrorActionPreference='Continue'; $global:LASTEXITCODE=$null
    & $psql -X -w -q -A -t -v ON_ERROR_STOP=1 -c $sql 1> (Join-Path $logRoot 'claim-pause.stdout.txt') 2> (Join-Path $logRoot 'claim-pause.stderr.txt')
    $code=$global:LASTEXITCODE; $ErrorActionPreference=$savedPreference
    if ($code -ne 0 -or $null -eq $code) { throw 'Could not change the deployment claim pause; inspect private logs.' }
    if ($Enabled) { $script:claimPauseIdentity=Get-Content -LiteralPath (Join-Path $logRoot 'claim-pause.stdout.txt') -Raw | ConvertFrom-Json }
}
function Assert-D15Schema {
    $ErrorActionPreference='Continue'; $global:LASTEXITCODE=$null
    $sql = "SELECT CASE WHEN to_regclass('public.visual_reviews') IS NOT NULL AND to_regclass('public.spell_reference_research') IS NOT NULL AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='jobs' AND column_name='visual_pipeline_version') THEN 1 ELSE 0 END;"
    $lines=@(& $psql -X -w -A -t -v ON_ERROR_STOP=1 -c $sql 2> (Join-Path $logRoot 'schema-prerequisite.stderr.txt'))
    $code=$global:LASTEXITCODE
    if ($code -ne 0 -or $null -eq $code -or $lines.Count -ne 1 -or $lines[0] -ne '1') {
        throw 'D15 schema prerequisite missing. Fresh installations must apply the prerequisite migrations in order before this updater.'
    }
    $record.migration_010_prerequisite_present=$true; Save-Record
}
$claimPauseInstalled=$false
$script:claimPauseIdentity=$null
try {
    Assert-D15Schema
    if ($ClientVersion -eq '1.8.0') {
        $prerequisite = @(& $psql -X -w -A -t -v ON_ERROR_STOP=1 -c "SELECT CASE WHEN to_regclass('public.visual_atlases') IS NOT NULL THEN 1 ELSE 0 END;" 2> (Join-Path $logRoot 'v2-prerequisite.stderr.txt'))
        if ($LASTEXITCODE -ne 0 -or ($prerequisite -join '').Trim() -ne '1') { throw 'Pipeline V2 requires migration011 already applied; services have not been stopped.' }
        $record.migration_011_prerequisite_present=$true; Save-Record
    }
    if ($DrainExistingJobs) {
        Set-ClaimPause $true
        $claimPauseInstalled=$true
        $record.claims_paused=$true; $record.claim_pause_identity=$script:claimPauseIdentity; Save-Record
    }
    $idleDeadline = [DateTime]::UtcNow.AddSeconds($WaitForIdleSeconds)
    $pendingJobs = Active-Jobs
    while ($pendingJobs -ne 0) {
        $record.phase='waiting_for_existing_jobs'; $record.pending_jobs=$pendingJobs; Save-Record
        if ($WaitForIdleSeconds -eq 0 -or [DateTime]::UtcNow -ge $idleDeadline) {
            throw 'Existing jobs are still active; services have not been stopped.'
        }
        Start-Sleep -Seconds 5
        $pendingJobs = Active-Jobs
    }
    $record.pending_jobs=0; Save-Record
    foreach ($entry in $migrationInputs) {
        if ((Digest (Join-Path $stage ('migrations\' + $entry.file))) -cne $entry.sha256) {
            throw 'Migration changed after preflight; services have not been stopped.'
        }
    }
    Stop-ServiceExecutable (Join-Path $runtime 'api\Palimpseste.Api.exe')
    if ((Active-Jobs) -ne 0) { throw 'Job arrived during admission shutdown; worker left running.' }
    Stop-ServiceExecutable (Join-Path $runtime 'bin\Palimpseste.Worker.exe')
    $record.phase='services_stopped'; Save-Record
    # Never replay009 or010 here: their older constraints reject newer visual pipeline versions.
    foreach ($entry in $migrationInputs) {
        $migration = Join-Path $stage ('migrations\' + $entry.file)
        if ((Digest $migration) -cne $entry.sha256) { throw 'Migration differs from its preflight hash.' }
        $savedPreference=$ErrorActionPreference; $ErrorActionPreference='Continue'; $global:LASTEXITCODE=$null
        & $psql -X -w -v ON_ERROR_STOP=1 -f $migration 1> (Join-Path $logRoot ($entry.file + '.stdout.txt')) 2> (Join-Path $logRoot ($entry.file + '.stderr.txt'))
        $code=$global:LASTEXITCODE; $ErrorActionPreference=$savedPreference
        if ($code -ne 0 -or $null -eq $code) { throw 'Migration failed; see private installation logs.' }
        $record.migrations_applied += [ordered]@{file=$entry.file;sha256=$entry.sha256;applied_at=[DateTimeOffset]::UtcNow.ToString('o')}
        $record.migration_applied=$entry.file; $record.migration_sha256=$entry.sha256; $record.phase='migration_applied'; Save-Record
    }
    $record.phase='migration_complete'; Save-Record
    Install-File (Join-Path $stage 'worker\Palimpseste.Worker.exe') (Join-Path $runtime 'bin\Palimpseste.Worker.exe')
    Install-File (Join-Path $stage 'doctor\ProviderDoctor.exe') (Join-Path $runtime 'bin\ProviderDoctor.exe')
    Install-File (Join-Path $stage 'visual-doctor\SpellVisualDoctor.exe') (Join-Path $runtime 'bin\SpellVisualDoctor.exe')
    Install-Tree (Join-Path $stage 'api') (Join-Path $runtime 'api')
    Install-Tree (Join-Path $stage 'spec') (Join-Path $runtime 'spec')
    Install-Tree (Join-Path $stage 'worker\ThirdPartyNotices') (Join-Path $runtime 'bin\ThirdPartyNotices')
    $launch = Join-Path $logRoot 'launch'; New-Item -ItemType Directory -Path $launch | Out-Null; Protect $launch
    foreach ($name in @('start-owner-api.ps1','start-owner-worker.ps1','api-service-child.ps1','worker-service-child.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $launch $name)
        Protect (Join-Path $launch $name)
    }
    $workerHash=Digest (Join-Path $runtime 'bin\Palimpseste.Worker.exe'); $apiHash=Digest (Join-Path $runtime 'api\Palimpseste.Api.exe')
    Pin (Join-Path $launch 'worker-service-child.ps1') 'expectedWorkerSha256' $workerHash
    Install-File (Join-Path $launch 'worker-service-child.ps1') (Join-Path $runtime 'bin\WorkerService.Child.ps1')
    Install-File (Join-Path $launch 'api-service-child.ps1') (Join-Path $runtime 'bin\ApiService.Child.ps1')
    Pin (Join-Path $launch 'start-owner-worker.ps1') 'expectedWorkerSha256' $workerHash
    Pin (Join-Path $launch 'start-owner-worker.ps1') 'expectedChildSha256' (Digest (Join-Path $runtime 'bin\WorkerService.Child.ps1'))
    Pin (Join-Path $launch 'start-owner-api.ps1') 'expectedApiSha256' $apiHash
    Pin (Join-Path $launch 'start-owner-api.ps1') 'expectedChildSha256' (Digest (Join-Path $runtime 'bin\ApiService.Child.ps1'))
    $updates=@{PALIMPSESTE_VISUAL_RENDERER_EXE=$record.renderer; PALIMPSESTE_VISUAL_RENDERER_MANIFEST_SHA256=$record.renderer_manifest_sha256}
    if ($JobConcurrency -ge 0) {
        $updates.PALIMPSESTE_MAX_PROVIDER_CONCURRENCY=$JobConcurrency.ToString([Globalization.CultureInfo]::InvariantCulture)
        $updates.PALIMPSESTE_WORKER_DRAIN_FILE=Join-Path $runtime 'bin\worker-drain.request'
        $record.max_concurrent_jobs=$JobConcurrency
        $record.concurrency_policy=if ($JobConcurrency -eq 0) { 'no_application_job_count_ceiling' } else { 'operator_configured' }
        $record.gpu_capture_slots=1
    }
    $lines=[Collections.Generic.List[string]]::new(); $seen=@{}
    foreach ($line in Get-Content -LiteralPath $envFile -Encoding UTF8) {
        if ($line -match '^([A-Za-z_][A-Za-z0-9_]*)=' -and $updates.ContainsKey($Matches[1])) {
            $key=$Matches[1]; $seen[$key]=$true; $lines.Add($key+'='+$updates[$key])
        } else { $lines.Add($line) }
    }
    foreach ($key in $updates.Keys) { if (-not $seen.ContainsKey($key)) { $lines.Add($key+'='+$updates[$key]) } }
    $envAcl=Get-Acl -LiteralPath $envFile
    [IO.File]::WriteAllText($envFile,(($lines -join "`r`n")+"`r`n"),$utf8); Set-Acl -LiteralPath $envFile -AclObject $envAcl
    if ((Digest (Join-Path $runtime 'bin\codex-image.exe')) -cne $originalNative) { throw 'Native Codex unexpectedly changed.' }
    $record.worker_sha256=$workerHash; $record.api_sha256=$apiHash; $record.launcher_directory=$launch; $record.phase='installed'; Save-Record
    if ($claimPauseInstalled) {
        Set-ClaimPause $false
        $claimPauseInstalled=$false
        $record.claims_paused=$false; Save-Record
    }
    $powershell='C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
    & $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $launch 'start-owner-worker.ps1') -RuntimeRoot $runtime
    if ($LASTEXITCODE -ne 0) { throw 'Worker startup failed.' }
    & $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $launch 'start-owner-api.ps1') -RuntimeRoot $runtime
    if ($LASTEXITCODE -ne 0) { throw 'API startup failed.' }
    $record.phase='services_started'; $record.completed=$true; $record.completed_at=[DateTimeOffset]::UtcNow.ToString('o'); Save-Record
    Write-Output "$BackendRevision backend / Player $ClientVersion installed. Owner gameplay trial pending. Record: $logRoot\installation.json"
} finally {
    if ($claimPauseInstalled) {
        try { Set-ClaimPause $false; $record.claims_paused=$false; Save-Record }
        catch { Write-Warning 'Claim pause cleanup failed; inspect the recorded deployment before admitting new work.' }
    }
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') }
}
