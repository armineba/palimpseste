[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repoRoot 'tests\Palimpseste.Provider.Security\Palimpseste.Provider.Security.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "Projet de tests introuvable : $project" }
Push-Location $repoRoot
try {
    & dotnet run --project $project --no-restore
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $exitCode
