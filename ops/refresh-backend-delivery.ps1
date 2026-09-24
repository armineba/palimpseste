# Refresh documentation/evidence around already compiled binaries; no build or generation.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$StageRoot,
    [string]$ProofPath = 'evidence/public/backend/animation-sheet-d16.json')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$boundary = Join-Path $repo 'deliverables\.stage-backend'
$stage = [IO.Path]::GetFullPath($StageRoot).TrimEnd('\')
if (-not $stage.StartsWith($boundary + '\',[StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $stage 'worker\Palimpseste.Worker.exe'))) { throw 'Unexpected compiled stage.' }
$evidenceRoot = Join-Path $repo 'evidence\public\backend'
$proofFile = if ([IO.Path]::IsPathRooted($ProofPath)) { [IO.Path]::GetFullPath($ProofPath) }
    else { [IO.Path]::GetFullPath((Join-Path $repo $ProofPath)) }
if (-not $proofFile.StartsWith($evidenceRoot + '\',[StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetExtension($proofFile) -ine '.json') { throw 'Delivery proof must be a public backend JSON evidence file.' }
$deliveryProof = Get-Content -LiteralPath $proofFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($deliveryProof.client_version -notmatch '^\d+\.\d+\.\d+$') { throw 'Delivery proof has no valid client version.' }
if ($deliveryProof.backend.publish_exit -ne 0 -or
    (Get-FileHash -LiteralPath (Join-Path $stage 'worker\Palimpseste.Worker.exe')).Hash -ine $deliveryProof.backend.worker_sha256 -or
    (Get-FileHash -LiteralPath (Join-Path $stage 'api\Palimpseste.Api.exe')).Hash -ine $deliveryProof.backend.api_sha256) {
    throw 'Stage binaries differ from the recorded successful publication.'
}
$zip = Join-Path $repo 'deliverables\Palimpseste-Backend-Windows-x64.zip'
$pending = Join-Path $boundary ([Guid]::NewGuid().ToString('N') + '.zip')
foreach ($path in @($stage,$zip,$pending)) {
    for ($cursor=$path; $cursor; $cursor=[IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse path refused.' }
    }
}
$files = [ordered]@{}
$files['README.txt'] = Join-Path $stage 'README.txt'
$files['spec/assets/sourced-vfx/catalogue.json'] = Join-Path $repo 'assets\sourced-vfx\catalogue.json'
$files['IMPLEMENTATION_STATUS.md'] = Join-Path $repo 'docs\IMPLEMENTATION_STATUS.md'
$files['docs/V2_FILES_CHANGED.md'] = Join-Path $repo 'docs\V2_FILES_CHANGED.md'
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo 'evidence\public\v2') -Recurse -File) {
    $files['evidence/v2/' + $file.FullName.Substring((Join-Path $repo 'evidence\public\v2').Length+1).Replace('\','/')] = $file.FullName
}
foreach ($name in @('D15_BEHAVIOR_AND_RESEARCH.md','D18_CONCURRENT_JOBS.md','D19_SOURCED_SURFACES.md','D20_SPELL_PIPELINE_V2.md','V2_BLUEPRINT_CONTRACT.md','V2_UNITY_STRUCTURAL_RENDERER.md','V2_VALIDATION_ENGINE.md','UNITY_BEHAVIORS_D15.md','UNITY_ANIMATION_SHEET_D16.md','references-vfx-sources.md','BIBLIOTHEQUES_VFX_OBLIGATOIRES.txt','NEXT_ACTIONS.md','TESTER_MAINTENANT.md')) {
    $files['docs/' + $name] = Join-Path $repo ('docs\' + $name)
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo 'docs') -Filter 'D16*.md' -File) {
    $files['docs/' + $file.Name] = $file.FullName
}
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Name.EndsWith('.ps1') -or $_.Name.EndsWith('.env.example') -or $_.Name.EndsWith('.patch') -or $_.Name.EndsWith('.md') }) {
    $files['ops/' + $file.Name] = $file.FullName
}
foreach ($file in Get-ChildItem -LiteralPath $evidenceRoot -Recurse -File) {
    # A distributable cannot contain its own eventual digest. That index lives beside it in Git.
    if ($file.FullName -eq (Join-Path $evidenceRoot 'lifecycle-delivery.json')) { continue }
    $files['evidence/' + $file.FullName.Substring($evidenceRoot.Length+1).Replace('\','/')] = $file.FullName
}
foreach ($entry in $files.GetEnumerator()) {
    $destination = Join-Path $stage $entry.Key
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    if ([IO.Path]::GetFullPath($entry.Value) -ine [IO.Path]::GetFullPath($destination)) {
        Copy-Item -LiteralPath $entry.Value -Destination $destination -Force
    }
}
# Recopying the source ops scripts must not restore the previous delivery's pins.
# Bind the refreshed launchers to these already verified stage binaries and child scripts.
function Set-DeliveryPin([string]$Path, [string]$Variable, [string]$Hash) {
    $content = [IO.File]::ReadAllText($Path)
    $pattern = '(?m)^\$' + [regex]::Escape($Variable) + " = '[0-9a-fA-F]{64}'(?=\r?$)"
    if ([regex]::Matches($content,$pattern).Count -ne 1) { throw 'Delivery launcher pin missing.' }
    $line = '$' + $Variable + " = '" + $Hash.ToLowerInvariant() + "'"
    [IO.File]::WriteAllText($Path,[regex]::Replace($content,$pattern,
        [Text.RegularExpressions.MatchEvaluator]{param($match) $line}),[Text.UTF8Encoding]::new($false))
}
$workerChild = Join-Path $stage 'ops\worker-service-child.ps1'
$workerLauncher = Join-Path $stage 'ops\start-owner-worker.ps1'
$apiLauncher = Join-Path $stage 'ops\start-owner-api.ps1'
Set-DeliveryPin $workerChild 'expectedWorkerSha256' $deliveryProof.backend.worker_sha256
Set-DeliveryPin $workerLauncher 'expectedWorkerSha256' $deliveryProof.backend.worker_sha256
Set-DeliveryPin $workerLauncher 'expectedChildSha256' (Get-FileHash -LiteralPath $workerChild -Algorithm SHA256).Hash
Set-DeliveryPin $apiLauncher 'expectedApiSha256' $deliveryProof.backend.api_sha256
Set-DeliveryPin $apiLauncher 'expectedChildSha256' (Get-FileHash -LiteralPath (Join-Path $stage 'ops\api-service-child.ps1') -Algorithm SHA256).Hash
Copy-Item -LiteralPath $zip -Destination $pending
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::Open($pending,[IO.Compression.ZipArchiveMode]::Update)
try {
    $oldIndex = $archive.GetEntry('evidence/lifecycle-delivery.json')
    if ($oldIndex) { $oldIndex.Delete() }
    foreach ($entry in $files.GetEnumerator()) {
        $destination = Join-Path $stage $entry.Key
        $old = $archive.GetEntry($entry.Key)
        if ($old) { $old.Delete() }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$destination,$entry.Key,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
[IO.File]::Replace($pending,$zip,[NullString]::Value)
$hash = (Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant()
$proof = [ordered]@{version=$deliveryProof.client_version; updated_at=[DateTimeOffset]::UtcNow.ToString('o'); build_exit=$deliveryProof.backend.publish_exit;
    binary_stage=$stage.Substring($repo.Length+1).Replace('\','/'); documentation_refreshed=$true;
    zip='deliverables/Palimpseste-Backend-Windows-x64.zip'; bytes=(Get-Item -LiteralPath $zip).Length; sha256=$hash;
    worker_sha256=(Get-FileHash -LiteralPath (Join-Path $stage 'worker\Palimpseste.Worker.exe')).Hash.ToLowerInvariant();
    api_sha256=(Get-FileHash -LiteralPath (Join-Path $stage 'api\Palimpseste.Api.exe')).Hash.ToLowerInvariant();
    tests_executed_by_refresh=$false; gameplay_acceptance='pending_owner'; evidence=$proofFile.Substring($repo.Length+1).Replace('\','/')}
[IO.File]::WriteAllText((Join-Path $evidenceRoot 'lifecycle-delivery.json'),($proof | ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
Write-Output ('Refreshed backend ZIP: ' + $hash + ' (' + $proof.bytes + ' bytes)')
