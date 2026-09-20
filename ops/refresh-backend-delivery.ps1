# Refresh documentation/evidence around already compiled binaries; no build or generation.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$StageRoot)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$boundary = Join-Path $repo 'deliverables\.stage-backend'
$stage = [IO.Path]::GetFullPath($StageRoot).TrimEnd('\')
if (-not $stage.StartsWith($boundary + '\',[StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $stage 'worker\Palimpseste.Worker.exe'))) { throw 'Unexpected compiled stage.' }
$deliveryProof = Get-Content -LiteralPath (Join-Path $repo 'evidence\public\backend\behavior-d15.json') -Raw -Encoding UTF8 | ConvertFrom-Json
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
$files['IMPLEMENTATION_STATUS.md'] = Join-Path $repo 'docs\IMPLEMENTATION_STATUS.md'
foreach ($name in @('D15_BEHAVIOR_AND_RESEARCH.md','UNITY_BEHAVIORS_D15.md','references-vfx-sources.md','NEXT_ACTIONS.md')) {
    $files['docs/' + $name] = Join-Path $repo ('docs\' + $name)
}
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Name.EndsWith('.ps1') -or $_.Name.EndsWith('.env.example') -or $_.Name.EndsWith('.patch') -or $_.Name.EndsWith('.md') }) {
    $files['ops/' + $file.Name] = $file.FullName
}
$evidenceRoot = Join-Path $repo 'evidence\public\backend'
foreach ($file in Get-ChildItem -LiteralPath $evidenceRoot -Recurse -File) {
    # A distributable cannot contain its own eventual digest. That index lives beside it in Git.
    if ($file.FullName -eq (Join-Path $evidenceRoot 'lifecycle-delivery.json')) { continue }
    $files['evidence/' + $file.FullName.Substring($evidenceRoot.Length+1).Replace('\','/')] = $file.FullName
}
Copy-Item -LiteralPath $zip -Destination $pending
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::Open($pending,[IO.Compression.ZipArchiveMode]::Update)
try {
    $oldIndex = $archive.GetEntry('evidence/lifecycle-delivery.json')
    if ($oldIndex) { $oldIndex.Delete() }
    foreach ($entry in $files.GetEnumerator()) {
        $destination = Join-Path $stage $entry.Key
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $entry.Value -Destination $destination -Force
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
    tests_executed=$false; gameplay_acceptance='pending_owner'; evidence='evidence/public/backend/behavior-d15.json'}
[IO.File]::WriteAllText((Join-Path $evidenceRoot 'lifecycle-delivery.json'),($proof | ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
Write-Output ('Refreshed backend ZIP: ' + $hash + ' (' + $proof.bytes + ' bytes)')
