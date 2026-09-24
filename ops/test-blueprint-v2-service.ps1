# Operator-only, isolated V2 provider test. No DB, player library or service replacement.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$StageRoot,
    [Parameter(Mandatory)][string]$PlayerRoot,
    [ValidateSet('core','full')][string]$StopAfter='core')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtime='E:\PalimpsesteRuntime'
$admin=[Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
if(-not ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole($admin)){throw 'Administrator required'}
$service=([Security.Principal.NTAccount]"$env:COMPUTERNAME\PalRuntimeSvc").Translate([Security.Principal.SecurityIdentifier])
function Ordinary([string]$path){for($p=[IO.Path]::GetFullPath($path);$p;$p=[IO.Path]::GetDirectoryName($p)){if((Test-Path -LiteralPath $p)-and((Get-Item -LiteralPath $p).Attributes-band[IO.FileAttributes]::ReparsePoint)){throw 'Reparse path refused'}}}
function Under([string]$path,[string]$root){Ordinary $path;if(-not [IO.Path]::GetFullPath($path).StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside isolated test'}}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Protect([string]$path){
 Under $path $runtime
 $dir=(Get-Item -LiteralPath $path).PSIsContainer
 $acl=if($dir){[Security.AccessControl.DirectorySecurity]::new()}else{[Security.AccessControl.FileSecurity]::new()}
 $acl.SetOwner($admin);$acl.SetAccessRuleProtection($true,$false)
 $inherit=if($dir){[Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'}else{[Security.AccessControl.InheritanceFlags]::None}
 foreach($sid in @($admin,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl',$inherit,'None','Allow'))}
 $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($service,'ReadAndExecute',$inherit,'None','Allow'));Set-Acl -LiteralPath $path -AclObject $acl
}
function Copy-Protected([string]$source,[string]$target){
 Ordinary $source;Under $target $runtime;New-Item -ItemType Directory -Force -Path $target|Out-Null;Protect $target
 foreach($item in Get-ChildItem -LiteralPath $source -Recurse -Force|Sort-Object{$_.FullName.Length}){
  if($item.Extension -eq '.pdb' -or $item.FullName -match 'BackUpThisFolder_ButDontShipItWithYourGame|BurstDebugInformation_DoNotShip'){continue}
  Ordinary $item.FullName;$to=Join-Path $target $item.FullName.Substring($source.Length+1)
  if($item.PSIsContainer){New-Item -ItemType Directory -Force -Path $to|Out-Null}else{Copy-Item -LiteralPath $item.FullName -Destination $to;if((Hash $to)-cne(Hash $item.FullName)){throw 'Test copy hash differs'}}
  Protect $to
 }
}
$stage=[IO.Path]::GetFullPath($StageRoot).TrimEnd('\');$player=[IO.Path]::GetFullPath($PlayerRoot).TrimEnd('\')
Under $stage (Join-Path $repo 'deliverables\.stage-backend');Under $player (Join-Path $repo 'game\Build')
$stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$installed=Join-Path $runtime ('v2-operator-tests\'+$stamp);$pending=Join-Path $runtime ('evidence\pending\v2-test-'+$stamp+'.json')
New-Item -ItemType Directory -Force -Path $installed|Out-Null;Protect $installed
Copy-Protected (Join-Path $stage 'spec') (Join-Path $installed 'spec')
Copy-Protected (Join-Path $stage 'blueprint-doctor') (Join-Path $installed 'doctor')
Copy-Protected $player (Join-Path $installed 'renderer')
$renderer=Join-Path $installed 'renderer'
$manifestFiles=@(Get-ChildItem -LiteralPath $renderer -File -Recurse|Where-Object{$_.Name-ne'renderer-manifest.json'}|ForEach-Object{[ordered]@{file=$_.FullName.Substring($renderer.Length+1).Replace('\','/');sha256=Hash $_.FullName}})
$manifestPath=Join-Path $renderer 'renderer-manifest.json'
[IO.File]::WriteAllText($manifestPath,([ordered]@{version='1.8.0';files=$manifestFiles}|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false));Protect $manifestPath
$fixtureRoot=Join-Path $runtime ('artifacts\v2-operator-fixture-'+$stamp)
New-Item -ItemType Directory -Path $fixtureRoot|Out-Null
$description=Join-Path $fixtureRoot 'description.json';$drawing=Join-Path $fixtureRoot 'drawing.png'
Copy-Item -LiteralPath (Join-Path $repo 'examples\v2-validation\description.json') -Destination $description
Copy-Item -LiteralPath (Join-Path $repo 'examples\v2-validation\drawing.png') -Destination $drawing
$doctor=Join-Path $installed 'doctor\BlueprintV2Doctor.exe';$doctorHash=Hash $doctor;$manifestHash=Hash $manifestPath
$child=Join-Path $installed 'run.ps1'
$childText=@'
param([Parameter(Mandatory)][string]$InstallRoot,[Parameter(Mandatory)][string]$Description,[Parameter(Mandatory)][string]$Drawing,[Parameter(Mandatory)][string]$Report,[Parameter(Mandatory)][string]$StopAfter)
$ErrorActionPreference='Stop'
if([Security.Principal.WindowsIdentity]::GetCurrent().Name -ine "$env:COMPUTERNAME\PalRuntimeSvc"){throw 'Wrong service identity'}
foreach($line in Get-Content -LiteralPath 'E:\PalimpsesteRuntime\runtime.env'){
 if([string]::IsNullOrWhiteSpace($line)-or$line-match'^\s*#'){continue}
 if($line-notmatch'^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$'){throw 'Invalid environment'}
 [Environment]::SetEnvironmentVariable($Matches[1],$Matches[2].Trim(),'Process')
}
foreach($name in @('OPENAI_API_KEY','CODEX_API_KEY','CODEX_ACCESS_TOKEN','CHATGPT_TOKEN','DATABASE_URL')){[Environment]::SetEnvironmentVariable($name,$null,'Process')}
$exe=Join-Path $InstallRoot 'doctor\BlueprintV2Doctor.exe'
if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ine '__DOCTOR_SHA__'){throw 'Doctor hash differs'}
$env:PALIMPSESTE_SPEC_ROOT=Join-Path $InstallRoot 'spec'
$env:PALIMPSESTE_VISUAL_RENDERER_EXE=Join-Path $InstallRoot 'renderer\Palimpseste.exe'
$env:PALIMPSESTE_VISUAL_RENDERER_MANIFEST_SHA256='__RENDERER_SHA__'
& $exe --mode plan --spec $env:PALIMPSESTE_SPEC_ROOT --description $Description --drawing $Drawing --report $Report --stop-after $StopAfter
exit $LASTEXITCODE
'@
$childText=$childText.Replace('__DOCTOR_SHA__',$doctorHash).Replace('__RENDERER_SHA__',$manifestHash)
[IO.File]::WriteAllText($child,$childText,[Text.UTF8Encoding]::new($false));Protect $child
$credential=Import-Clixml -LiteralPath 'C:\ProgramData\Palimpseste\operator-credentials\PalRuntimeSvc.credential.xml'
$process=Start-Process powershell.exe -Credential $credential -LoadUserProfile -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$child,'-InstallRoot',$installed,'-Description',$description,'-Drawing',$drawing,'-Report',$pending,'-StopAfter',$StopAfter) -RedirectStandardOutput (Join-Path $runtime ('evidence\pending\v2-test-'+$stamp+'.stdout')) -RedirectStandardError (Join-Path $runtime ('evidence\pending\v2-test-'+$stamp+'.stderr'))
$launch=[ordered]@{schema_version='sp.v2-operator-launch/1.0';started_utc=[DateTime]::UtcNow.ToString('O');process_id=$process.Id;doctor_sha256=$doctorHash;renderer_manifest_sha256=$manifestHash;report=$pending;install=$installed;description_sha256=Hash $description;drawing_sha256=Hash $drawing;stop_after=$StopAfter;model_calls='pending_private_report';database_touched=$false;player_library_touched=$false}
[IO.File]::WriteAllText((Join-Path $repo 'evidence\public\v2\operator-launch.json'),($launch|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
$launch|ConvertTo-Json -Depth 4
# Export only the reviewed report fields. Raw provider stdout/stderr, credentials
# and private attempt files remain in the protected service evidence directory.
$deadline=[DateTime]::UtcNow.AddMinutes(25)
while(-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Seconds 2;$process.Refresh()}
if(Test-Path -LiteralPath $pending){
 Ordinary $pending
 if((Get-Item -LiteralPath $pending).Length -gt 2097152){throw 'Operator report oversized'}
 $privateReport=Get-Content -LiteralPath $pending -Raw -Encoding UTF8|ConvertFrom-Json
 $export=[ordered]@{schema_version='sp.v2-operator-result/1.0';source_report_sha256=Hash $pending;observed_utc=[DateTime]::UtcNow.ToString('O');process_exited=$process.HasExited}
 foreach($key in @('mode','stop_after','inputs_record','preflight_issues','native_executable_sha256','auth_mode','spec_sha256','input_sha256','description_validation_issues','plan_validation_issues','compilation_issues','core_accepted','full_validation_executed','full_accepted','result','phase','completed_at','failed_at','failure_code','exception_type','attempts_submitted','model_calls_executed','provider_call_pending','artifacts')){
  if($null -ne $privateReport.PSObject.Properties[$key]){$export[$key]=$privateReport.$key}
 }
 $export.calls=@($privateReport.calls|Select-Object stage,outcome,process_started,exit_code,requested_model,requested_effort,reported_model,reported_effort,usage,response_sha256,diagnostic_category,stdout_sha256)
 [IO.File]::WriteAllText((Join-Path $repo 'evidence\public\v2\operator-result.json'),($export|ConvertTo-Json -Depth 18),[Text.UTF8Encoding]::new($false))
}
