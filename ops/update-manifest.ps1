[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$paths = @(& git -C $root ls-files --cached)
if ($LASTEXITCODE -ne 0 -or $paths.Count -eq 0) { throw 'Index Git indisponible ou vide ; ajouter les fichiers avant de générer le manifeste.' }
$lines = New-Object 'System.Collections.Generic.List[string]'
foreach ($relative in ($paths | Sort-Object -CaseSensitive)) {
    if ($relative -eq 'MANIFEST.sha256') { continue }
    $native = $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $full = [IO.Path]::GetFullPath((Join-Path $root $native))
    if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Chemin Git hors dépôt : $relative"
    }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Fichier indexé absent : $relative" }
    $hash = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines.Add("$hash  $relative")
}
$target = Join-Path $root 'MANIFEST.sha256'
[IO.File]::WriteAllLines($target, $lines, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "Manifest: $target ($($lines.Count) fichiers ; le manifeste lui-même est exclu)."
