param(
    [Parameter(Mandatory)][string]$InvitationFile,
    [string]$ProductDataRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ProductDataRoot) {
    $localAppData = [Environment]::GetFolderPath('LocalApplicationData')
    $localLow = Join-Path ([IO.Path]::GetDirectoryName($localAppData)) 'LocalLow'
    $ProductDataRoot = Join-Path $localLow 'Palimpseste\Palimpseste Spell Lab\Palimpseste'
}
$source = [IO.Path]::GetFullPath($InvitationFile)
$targetDir = [IO.Path]::GetFullPath($ProductDataRoot)
$target = Join-Path $targetDir 'access.json'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Invitation absente.' }
if (Test-Path -LiteralPath $target) { throw 'Une invitation est déjà installée pour ce profil Windows.' }

$data = Get-Content -LiteralPath $source -Raw -Encoding utf8 | ConvertFrom-Json
if (@($data.PSObject.Properties).Count -ne 2 -or
    -not ($data.PSObject.Properties.Name -contains 'service_url') -or
    -not ($data.PSObject.Properties.Name -contains 'invitation_code') -or
    $data.invitation_code -cnotmatch '^[A-Za-z0-9_-]{64}$') {
    throw 'Format d’invitation invalide.'
}
$uri = [Uri]$data.service_url
if (-not $uri.IsAbsoluteUri -or ($uri.Scheme -ne 'https' -and -not ($uri.Scheme -eq 'http' -and $uri.IsLoopback)) -or
    $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/') {
    throw 'Adresse du laboratoire invalide.'
}

New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
$json = @{ service_url = $uri.AbsoluteUri.TrimEnd('/'); invitation_code = $data.invitation_code } | ConvertTo-Json -Compress
[IO.File]::WriteAllText($target, $json, [Text.UTF8Encoding]::new($false))

$acl = Get-Acl -LiteralPath $target
$acl.SetAccessRuleProtection($true, $false)
foreach ($entry in @($acl.Access)) { [void]$acl.RemoveAccessRule($entry) }
foreach ($sid in @(
    [Security.Principal.WindowsIdentity]::GetCurrent().User,
    [Security.Principal.SecurityIdentifier]::new('S-1-5-18'),
    [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
)) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
        $sid, 'FullControl', 'None', 'None', 'Allow'))
}
Set-Acl -LiteralPath $target -AclObject $acl
Write-Output 'Accès joueur installé dans ce profil Windows. Lancer Palimpseste.'
