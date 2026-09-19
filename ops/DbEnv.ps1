Set-StrictMode -Version Latest

function Read-PalimpsesteConnection {
    param([Parameter(Mandatory)][string]$EnvFile, [string]$Name = 'DATABASE_URL')
    $line = Get-Content -LiteralPath $EnvFile -Encoding UTF8 | Where-Object { $_.StartsWith($Name + '=') } | Select-Object -First 1
    if (-not $line) { throw "$Name absent du fichier d'environnement." }
    $value = $line.Substring($Name.Length + 1)
    $parts = @{}
    foreach ($segment in $value.Split(';')) {
        $position = $segment.IndexOf('=')
        if ($position -gt 0) { $parts[$segment.Substring(0, $position)] = $segment.Substring($position + 1) }
    }
    foreach ($field in @('Host', 'Port', 'Database', 'Username', 'Password')) {
        if (-not $parts.ContainsKey($field) -or [string]::IsNullOrWhiteSpace($parts[$field])) { throw "Champ $field manquant dans $Name." }
    }
    return $parts
}

function Set-PrivateAcl {
    param([Parameter(Mandatory)][string]$Path)
    $acl = Get-Acl -LiteralPath $Path
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($entry in @($acl.Access)) { [void]$acl.RemoveAccessRule($entry) }
    $current = [Security.Principal.WindowsIdentity]::GetCurrent().User
    foreach ($sid in @($current, (New-Object Security.Principal.SecurityIdentifier 'S-1-5-18'), (New-Object Security.Principal.SecurityIdentifier 'S-1-5-32-544'))) {
        $rule = New-Object Security.AccessControl.FileSystemAccessRule($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Test-ArtifactKey {
    param([Parameter(Mandatory)][string]$Key)
    return $Key -match '^[0-9a-f]{2}/[0-9a-f]{32}\.(png|json|gz|bin)$'
}

function Get-ChildArtifactPath {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Key)
    if (-not (Test-ArtifactKey $Key)) { throw "Clé d'artefact invalide." }
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $path = [IO.Path]::GetFullPath((Join-Path $rootFull $Key.Replace('/', [IO.Path]::DirectorySeparatorChar)))
    if (-not $path.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Chemin d'artefact hors racine." }
    return $path
}
