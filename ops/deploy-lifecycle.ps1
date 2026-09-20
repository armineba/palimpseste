<# Operator installation only. Does not generate a spell, run a diagnostic, or build software.
   Reuses the existing native Codex identity and observed D13 binary evidence unchanged.
   D16 / 1.6.0 gameplay and artistic acceptance remain pending the owner's own trial.
   Updates an existing D15 database with migration011 only; migration010 is a prerequisite. #>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$StageRoot,
    [string]$PlayerRoot = '', [string]$RuntimeRoot = 'E:\PalimpsesteRuntime')
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
foreach ($relative in @('worker\Palimpseste.Worker.exe','api\Palimpseste.Api.exe','doctor\ProviderDoctor.exe','visual-doctor\SpellVisualDoctor.exe','spec\prompts\05_VISUAL_CRITIC.md',
    'spec\assets\sourced-vfx\catalogue.json','spec\assets\sourced-vfx\references.json',
    'spec\assets\sourced-vfx\licenses\Kenney-Particle-Pack-CC0.txt','spec\assets\sourced-vfx\licenses\Kenney-Smoke-Particles-CC0.txt',
    'spec\prompts\history\01_MODEL_A_INTERPRETE_2_3.md','spec\prompts\history\04_IMAGE_REFERENCE_1_2.md',
    'worker\ThirdPartyNotices\System.Drawing.Common-MIT.txt','migrations\011_animation_sheet.sql')) {
    if (-not (Test-Path -LiteralPath (Join-Path $stage $relative) -PathType Leaf)) { throw 'D16 stage incomplete.' }
}
$stageVfx = Join-Path $stage 'spec\assets\sourced-vfx'
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
if ($delivery.client_version -cne '1.6.0' -or $delivery.build_exit -ne 0) { throw 'Successful D16 / 1.6.0 animation-sheet build required.' }
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
$record = [ordered]@{version='1.6.0'; delivery_name='D16 strict 3x7 animation sheet'; started_at=[DateTimeOffset]::UtcNow.ToString('o'); phase='staging'; completed=$false;
    spell_generations=0; diagnostics_executed=$false; gameplay_tested=$false; visual_acceptance='pending_owner'; native_sha256=$originalNative}
function Save-Record { [IO.File]::WriteAllText((Join-Path $logRoot 'installation.json'),($record | ConvertTo-Json -Depth 7),$utf8) }
Save-Record
$render = Join-Path $runtime ('render-animation-sheet-' + $stamp)
Install-Tree $player $render
$renderFiles = @(Get-ChildItem -LiteralPath $render -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{file=$_.FullName.Substring($render.Length+1).Replace('\','/');sha256=Digest $_.FullName}
})
$renderManifest = Join-Path $render 'renderer-manifest.json'
[IO.File]::WriteAllText($renderManifest,([ordered]@{version='1.6.0';files=$renderFiles} | ConvertTo-Json -Depth 5),$utf8)
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
    $lines=@(& $psql -X -w -A -t -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM jobs WHERE state NOT IN ('ready','needs_operator');" 2> (Join-Path $logRoot 'postgres.stderr.txt'))
    $code=$global:LASTEXITCODE
    if ($code -ne 0 -or $null -eq $code -or $lines.Count -ne 1 -or $lines[0] -notmatch '^\d+$') { throw 'Could not read pending jobs.' }
    return [long]$lines[0]
}
function Assert-D15Schema {
    $ErrorActionPreference='Continue'; $global:LASTEXITCODE=$null
    $sql = "SELECT CASE WHEN to_regclass('public.visual_reviews') IS NOT NULL AND to_regclass('public.spell_reference_research') IS NOT NULL AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='jobs' AND column_name='visual_pipeline_version') THEN 1 ELSE 0 END;"
    $lines=@(& $psql -X -w -A -t -v ON_ERROR_STOP=1 -c $sql 2> (Join-Path $logRoot 'schema-prerequisite.stderr.txt'))
    $code=$global:LASTEXITCODE
    if ($code -ne 0 -or $null -eq $code -or $lines.Count -ne 1 -or $lines[0] -ne '1') {
        throw 'D15 schema prerequisite missing. Fresh installations must apply migrations001 through011 in order; this updater applies011 only.'
    }
    $record.migration_010_prerequisite_present=$true; Save-Record
}
try {
    Assert-D15Schema
    if ((Active-Jobs) -ne 0) { throw 'Existing job active; deployment has not stopped services.' }
    Stop-ServiceExecutable (Join-Path $runtime 'api\Palimpseste.Api.exe')
    if ((Active-Jobs) -ne 0) { throw 'Job arrived during admission shutdown; worker left running.' }
    Stop-ServiceExecutable (Join-Path $runtime 'bin\Palimpseste.Worker.exe')
    $record.phase='services_stopped'; Save-Record
    $savedPreference=$ErrorActionPreference; $ErrorActionPreference='Continue'; $global:LASTEXITCODE=$null
    # Never replay009 or010 here: their older constraints reject newer visual pipeline versions.
    $migration = Join-Path $stage 'migrations\011_animation_sheet.sql'
    & $psql -X -w -v ON_ERROR_STOP=1 -f $migration 1> (Join-Path $logRoot 'migration.stdout.txt') 2> (Join-Path $logRoot 'migration.stderr.txt')
    $code=$global:LASTEXITCODE; $ErrorActionPreference=$savedPreference
    if ($code -ne 0 -or $null -eq $code) { throw 'Migration011 failed; see private installation logs.' }
    $record.migration_011_applied=$true; $record.migration_011_sha256=Digest $migration; $record.phase='migration_complete'; Save-Record
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
    $powershell='C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
    & $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $launch 'start-owner-worker.ps1') -RuntimeRoot $runtime
    if ($LASTEXITCODE -ne 0) { throw 'Worker startup failed.' }
    & $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $launch 'start-owner-api.ps1') -RuntimeRoot $runtime
    if ($LASTEXITCODE -ne 0) { throw 'API startup failed.' }
    $record.phase='services_started'; $record.completed=$true; $record.completed_at=[DateTimeOffset]::UtcNow.ToString('o'); Save-Record
    Write-Output "D16 / 1.6.0 installed. Owner gameplay trial pending. Record: $logRoot\installation.json"
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') }
}
