param(
    [Parameter(Mandatory)][string]$ApiExecutable,
    [Parameter(Mandatory)][string]$DatabaseEnvFile,
    [Parameter(Mandatory)][string]$ServiceUrl,
    [Parameter(Mandatory)][string]$PlayerLabel,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$uri = [Uri]$ServiceUrl
if (-not $uri.IsAbsoluteUri -or ($uri.Scheme -ne 'https' -and -not ($uri.Scheme -eq 'http' -and $uri.IsLoopback)) -or
    $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/') {
    throw 'ServiceUrl doit être HTTPS, ou HTTP sur loopback pour le laboratoire local.'
}
if ($PlayerLabel.Length -lt 1 -or $PlayerLabel.Length -gt 120) { throw 'PlayerLabel invalide.' }
$apiPath = [IO.Path]::GetFullPath($ApiExecutable)
$envPath = [IO.Path]::GetFullPath($DatabaseEnvFile)
$outputFull = [IO.Path]::GetFullPath($OutputPath)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
if ($outputFull.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Le fichier d’invitation doit être créé hors du dépôt.'
}
if (-not (Test-Path -LiteralPath $apiPath -PathType Leaf)) { throw 'API publiée absente.' }
if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) { throw 'Fichier DB privé absent.' }
if (Test-Path -LiteralPath $outputFull) { throw 'Le fichier de sortie existe déjà.' }

$databaseLine = Get-Content -LiteralPath $envPath -Encoding utf8 |
    Where-Object { $_.StartsWith('DATABASE_URL=') } | Select-Object -First 1
if (-not $databaseLine) { throw 'DATABASE_URL absent.' }
$previousDatabaseUrl = $env:DATABASE_URL
try {
    $env:DATABASE_URL = $databaseLine.Substring('DATABASE_URL='.Length)
    $output = @(& $apiPath invite-create $PlayerLabel)
    if ($LASTEXITCODE -ne 0) { throw 'Création de l’invitation échouée.' }
}
finally {
    $env:DATABASE_URL = $previousDatabaseUrl
}
$codeLine = @($output | Where-Object { $_ -match '^invitation_code=[A-Za-z0-9_-]{64}$' })
if ($codeLine.Count -ne 1) { throw 'Sortie invitation inattendue.' }
$code = $codeLine[0].Substring('invitation_code='.Length)
$parent = [IO.Path]::GetDirectoryName($outputFull)
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$json = @{ service_url = $ServiceUrl.TrimEnd('/'); invitation_code = $code } | ConvertTo-Json -Compress
[IO.File]::WriteAllText($outputFull, $json, [Text.UTF8Encoding]::new($false))

$acl = Get-Acl -LiteralPath $outputFull
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
Set-Acl -LiteralPath $outputFull -AclObject $acl
Write-Output "Invitation créée : $outputFull (usage unique, 24 h). Remettre ce fichier au joueur par canal privé."
