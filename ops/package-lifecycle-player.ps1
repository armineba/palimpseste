[CmdletBinding()]
param(
    [switch]$UpdateDesktopShortcut,
    [ValidateRange(1, 48)][int]$MaximumBuildAgeHours = 6
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot = Join-Path $repo 'game\Build'
$source = Join-Path $buildRoot 'WindowsAnimationSheetRelease'
$playable = Join-Path $buildRoot 'WindowsAnimationSheetPlayable'
$zipPath = Join-Path $repo 'deliverables\Palimpseste-Windows-x64-IL2CPP.zip'
$proofPath = Join-Path $repo 'evidence\public\unity\lifecycle-delivery.json'
$logPath = Join-Path $repo 'game\Logs\animation-sheet-build.log'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$stage = Join-Path $buildRoot ('animation-sheet-package-stage-' + $stamp)
$pendingZip = $stage + '.zip'
$backup = Join-Path $buildRoot ('AnimationSheetDeliveryBackups\' + $stamp)

function Assert-Under([string]$Path, [string]$Scope) {
    $absolute = [IO.Path]::GetFullPath($Path)
    $boundary = [IO.Path]::GetFullPath($Scope).TrimEnd('\')
    if (-not $absolute.StartsWith($boundary + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Chemin hors périmètre : $Path" }
    for ($cursor = $absolute; $cursor.Length -gt $boundary.Length; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Jonction ou lien interdit pour le packaging : $cursor"
        }
    }
}
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Relative([string]$Path) { $Path.Substring($repo.Length + 1).Replace('\', '/') }
function Save-Proof($Value) {
    $temporary = $proofPath + '.pending'
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $proofPath) { [IO.File]::Replace($temporary, $proofPath, [NullString]::Value) }
    else { [IO.File]::Move($temporary, $proofPath) }
}

# Reject an old or unrelated build before creating a staging directory.
foreach ($path in @($source, $playable, $stage, $pendingZip, $backup, $zipPath, $proofPath, $logPath)) { Assert-Under $path $repo }
$log = Get-Item -LiteralPath $logPath
$now = [DateTime]::UtcNow
if ($log.LastWriteTimeUtc -lt $now.AddHours(-$MaximumBuildAgeHours) -or $log.LastWriteTimeUtc -gt $now.AddMinutes(2)) { throw 'Journal de build absent, trop ancien ou daté dans le futur.' }
$logText = [IO.File]::ReadAllText($logPath).Replace('\', '/')
$expectedExe = (Join-Path $source 'Palimpseste.exe').Replace('\', '/')
$exitMarkers = [regex]::Matches($logText, 'Application will terminate with return code (\d+)')
$resultMarkers = [regex]::Matches($logText, 'Build Finished, Result: (\w+)')
if ($logText -notmatch ('PALIMPSESTE_BUILD_OK ' + [regex]::Escape($expectedExe) + ' bytes=') -or
    $resultMarkers.Count -eq 0 -or $resultMarkers[$resultMarkers.Count - 1].Groups[1].Value -ne 'Success' -or
    $exitMarkers.Count -eq 0 -or $exitMarkers[$exitMarkers.Count - 1].Groups[1].Value -ne '0') { throw 'Le journal ne prouve pas la réussite de ce build et sa sortie 0.' }
if ([IO.File]::ReadAllText((Join-Path $repo 'game\ProjectSettings\ProjectSettings.asset')) -notmatch '(?m)^\s*bundleVersion:\s*1\.6\.0\s*$') { throw 'La version du projet doit être 1.6.0.' }
$required = @('Palimpseste.exe', 'GameAssembly.dll', 'UnityPlayer.dll', 'Palimpseste_Data\globalgamemanagers', 'Palimpseste_Data\il2cpp_data\Metadata\global-metadata.dat')
foreach ($relative in $required) {
    $file = Get-Item -LiteralPath (Join-Path $source $relative)
    # Unity can preserve the original engine timestamps when copying these two binaries.
    $requiresRecentBuild = $relative -notin @('Palimpseste.exe', 'UnityPlayer.dll')
    if ($file.Length -le 0 -or ($requiresRecentBuild -and (
        $file.LastWriteTimeUtc -lt $now.AddHours(-$MaximumBuildAgeHours) -or
        $file.LastWriteTimeUtc -gt $log.LastWriteTimeUtc.AddSeconds(2)))) {
        throw "Fichier de build manquant, vide ou non récent : $relative"
    }
}

# Record the actual working tree; a Git commit alone cannot identify uncommitted sources.
$sourceFiles = @()
foreach ($folder in @('game\Assets', 'game\Packages', 'game\ProjectSettings', 'shared\Palimpseste.Contracts')) {
    $sourceFiles += @(Get-ChildItem -LiteralPath (Join-Path $repo $folder) -Recurse -File -Force |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
}
$sourceRecords = @($sourceFiles | Sort-Object FullName -Unique | ForEach-Object {
    if ($_.LastWriteTimeUtc -gt $log.LastWriteTimeUtc) { throw "Source modifiée après le build : $(Relative $_.FullName)" }
    [ordered]@{ file = Relative $_.FullName; bytes = $_.Length; sha256 = Hash $_.FullName }
})
$sourceListing = ($sourceRecords | ForEach-Object { $_.sha256 + '  ' + $_.file }) -join "`n"
$sha = [Security.Cryptography.SHA256]::Create()
try { $sourceDigest = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($sourceListing))).Replace('-', '').ToLowerInvariant() }
finally { $sha.Dispose() }
$head = & git -c "safe.directory=$repo" -C $repo rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Impossible de lire le commit source.' }

# Copy distributable files and compare every byte stream with the successful build.
New-Item -ItemType Directory -Path $stage | Out-Null
$files = @()
foreach ($file in (Get-ChildItem -LiteralPath $source -Recurse -File -Force | Sort-Object FullName)) {
    $relative = $file.FullName.Substring($source.Length + 1)
    if ($relative -match '(?i)(^|[\\/])[^\\/]*(DoNotShip|DontShipItWithYourGame)[^\\/]*([\\/]|$)') { continue }
    Assert-Under $file.FullName $source
    $target = Join-Path $stage $relative
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
    $digest = Hash $file.FullName
    if ((Hash $target) -ne $digest) { throw "Copie différente du build : $relative" }
    $files += [pscustomobject][ordered]@{ file = $relative.Replace('\', '/'); bytes = $file.Length; sha256 = $digest }
}
if ($files.Count -lt $required.Count) { throw 'Build distribuable incomplet.' }
$buildFilesVerified = $files.Count
# Supplement the compiled files with notices for the reused PNGs and HLSL noise.
# These are packaging inputs, not files claimed to have been produced by Unity.
$licenseFiles = @()
foreach ($name in @('Kenney-Particle-Pack-CC0.txt', 'Kenney-Smoke-Particles-CC0.txt', 'NoiseShader-MIT.txt')) {
    $licenseSource = Join-Path $repo ('assets\sourced-vfx\licenses\' + $name)
    Assert-Under $licenseSource $repo
    $relative = 'ThirdPartyNotices/' + $name
    $target = Join-Path $stage $relative
    Assert-Under $target $stage
    if (Test-Path -LiteralPath $target) { throw 'Third-party notice destination already exists in the build.' }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    Copy-Item -LiteralPath $licenseSource -Destination $target
    $digest = Hash $licenseSource
    if ((Hash $target) -cne $digest) { throw 'Third-party notice copy differs.' }
    $files += [pscustomobject][ordered]@{ file=$relative; bytes=(Get-Item -LiteralPath $target).Length; sha256=$digest; origin='reviewed_asset_license' }
    $licenseFiles += $relative
}
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
# Explicit ZIP names keep directory separators portable on .NET Framework as well.
$zipWriter = [IO.Compression.ZipFile]::Open($pendingZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zipWriter,
            (Join-Path $stage $file.file), $file.file, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $zipWriter.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($pendingZip)
$sha = [Security.Cryptography.SHA256]::Create()
try {
    if ($archive.Entries.Count -ne $files.Count) { throw 'Nombre de fichiers ZIP incohérent.' }
    foreach ($file in $files) {
        $entry = $archive.GetEntry($file.file)
        if ($null -eq $entry -or $entry.Length -ne $file.bytes) { throw "Entrée ZIP absente ou tronquée : $($file.file)" }
        $stream = $entry.Open()
        try { $digest = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $stream.Dispose() }
        if ($digest -ne $file.sha256) { throw "Hash ZIP incorrect : $($file.file)" }
    }
}
finally { $sha.Dispose(); $archive.Dispose() }

# Preserve any previous playable, archive and manifest. No recursive deletion.
New-Item -ItemType Directory -Path $backup -Force | Out-Null
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($zipPath)), ([IO.Path]::GetDirectoryName($proofPath)) -Force | Out-Null
foreach ($old in @($playable, $zipPath, $proofPath)) {
    if (Test-Path -LiteralPath $old) {
        $destination = Join-Path $backup ([IO.Path]::GetFileName($old))
        Assert-Under $old $repo
        Assert-Under $destination $buildRoot
        Move-Item -LiteralPath $old -Destination $destination
    }
}
Assert-Under $stage $buildRoot
Assert-Under $playable $buildRoot
Move-Item -LiteralPath $stage -Destination $playable
Move-Item -LiteralPath $pendingZip -Destination $zipPath
$proof = [ordered]@{
    observed_at = [DateTime]::UtcNow.ToString('O'); client_version = '1.6.0'; delivery_name='D16 strict 3x7 animation sheet'; unity = '6000.3.24f1'; backend = 'IL2CPP'
    build_exit = 0; build_success_marker_observed = $true; build_log = Relative $logPath; build_log_sha256 = Hash $logPath
    source = Relative $source; playable = Relative $playable; zip = Relative $zipPath
    zip_bytes = (Get-Item -LiteralPath $zipPath).Length; zip_sha256 = Hash $zipPath
    files_verified = $files.Count; playable_bytes = ($files | Measure-Object -Property bytes -Sum).Sum; files = $files
    build_files_verified = $buildFilesVerified; supplemental_license_files = $licenseFiles
    source_commit = ($head -join '').Trim(); source_snapshot_sha256 = $sourceDigest; source_files = $sourceRecords
    source_snapshot = 'working tree hashes observed after successful build; not a claim that the tree is committed'
    excluded_patterns = @('*DoNotShip*', '*DontShipItWithYourGame*'); previous_delivery_backup = Relative $backup
    model_calls = 0; tests_executed_by_packager = $false; art_acceptance = 'not_claimed'; player_launch = $null
}
Save-Proof $proof

# Packaging never opens the game. The operator may update the desktop entry
# after all distributable and archive integrity checks have completed.
if ($UpdateDesktopShortcut) {
    $executable = Join-Path $playable 'Palimpseste.exe'
    $shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Palimpseste Spell Lab.lnk'
    if (Test-Path -LiteralPath $shortcutPath) {
        if ((Get-Item -LiteralPath $shortcutPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Le raccourci existant est un lien de fichiers, modification refusée.'
        }
        Copy-Item -LiteralPath $shortcutPath -Destination (Join-Path $backup 'Palimpseste Spell Lab.lnk')
    }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $executable; $shortcut.WorkingDirectory = $playable
        $shortcut.Description = 'Palimpseste Spell Lab 1.6.0'; $shortcut.Save()
        $verifiedShortcut = $shell.CreateShortcut($shortcutPath)
        if ($verifiedShortcut.TargetPath -ne $executable -or $verifiedShortcut.WorkingDirectory -ne $playable) {
            throw 'Le raccourci enregistré ne cible pas la livraison 1.6.0.'
        }
    }
    finally {
        if ($null -ne $verifiedShortcut) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($verifiedShortcut) }
        if ($null -ne $shortcut) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
        if ($null -ne $shell) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    }
    $proof.desktop_shortcut = [ordered]@{
        updated_at = [DateTime]::UtcNow.ToString('O'); name = 'Palimpseste Spell Lab.lnk'
        target = Relative $executable; launch_executed = $false
    }
    Save-Proof $proof
}
Write-Output "Player 1.6.0 : $playable"
Write-Output "ZIP SHA256 : $($proof.zip_sha256) ($($proof.zip_bytes) octets)"
Write-Output "Preuve : $proofPath"
Write-Output "Livraison précédente conservée : $backup"
