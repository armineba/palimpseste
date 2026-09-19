[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$deliverables = Join-Path $projectRoot 'deliverables'
$stageRoot = Join-Path $deliverables '.stage-backend'
$stage = Join-Path $stageRoot ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage | Out-Null

function Publish-App([string]$Project, [string]$Folder) {
    $destination = Join-Path $stage $Folder
    & dotnet publish (Join-Path $projectRoot $Project) -c $Configuration -r win-x64 --self-contained true '-p:PublishSingleFile=true' '-p:PublishTrimmed=false' -o $destination -v:q
    if ($LASTEXITCODE -ne 0) { throw "Échec de publication : $Project" }
}

Publish-App 'backend/Palimpseste.Api/Palimpseste.Api.csproj' 'api'
Publish-App 'backend/Palimpseste.Worker/Palimpseste.Worker.csproj' 'worker'
Publish-App 'backend/ProviderDoctor/ProviderDoctor.csproj' 'doctor'
Get-ChildItem -LiteralPath $stage -Filter '*.pdb' -File -Recurse | Remove-Item -Force

$spec = Join-Path $stage 'spec'
New-Item -ItemType Directory -Force -Path $spec | Out-Null
foreach ($directory in @('contracts', 'reference')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $directory) -Destination (Join-Path $spec $directory) -Recurse
}
$runtimePrompts = Join-Path $spec 'prompts'
New-Item -ItemType Directory -Force -Path $runtimePrompts | Out-Null
foreach ($name in @('01_MODEL_A_INTERPRETE.md', '02_MODEL_B_TRADUCTEUR.md', '03_REPARATION_TECHNIQUE.md')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $projectRoot 'prompts') $name) -Destination (Join-Path $runtimePrompts $name)
}
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'migrations') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'backend/migrations/001_initial.sql') -Destination (Join-Path $stage 'migrations/001_initial.sql')
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'ops') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ops') -File |
    Where-Object { $_.Name.EndsWith('.ps1') -or $_.Name.EndsWith('.env.example') -or $_.Name -eq 'README.md' } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path (Join-Path $stage 'ops') $_.Name) }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/IMPLEMENTATION_STATUS.md') -Destination (Join-Path $stage 'IMPLEMENTATION_STATUS.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence/public/backend') -Destination (Join-Path $stage 'evidence') -Recurse

@"
Palimpseste backend Windows x64. API, worker et doctor sont des exécutables .NET autoportants.
La configuration et les secrets ne sont pas inclus. Lire IMPLEMENTATION_STATUS.md puis ops/provision-runtime.ps1.
Extraire l'archive dans un dossier opérateur inaccessible au compte worker : elle contient des scripts ops d'administration.
Copier api, worker et doctor publiés vers leurs emplacements de service avec ACL minimales ; ne pas lancer le worker depuis le dossier extrait.
L'API attend DATABASE_URL, ARTIFACT_ROOT et PALIMPSESTE_SPEC_ROOT pointant vers la copie runtime de spec.
Le worker attend en plus le compte Windows dédié, CODEX_HOME isolé et la preuve du doctor actif.
La génération reste bloquée tant que le compte de service Codex et le doctor actif ne sont pas validés.
"@ | Set-Content -LiteralPath (Join-Path $stage 'README.txt') -Encoding UTF8

$zip = Join-Path $deliverables 'Palimpseste-Backend-Windows-x64.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal -Force
$file = Get-Item -LiteralPath $zip
$sha = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Backend ZIP: $($file.FullName)"
Write-Output "Bytes: $($file.Length)"
Write-Output "SHA256: $sha"
Write-Output "Staging conservé: $stage"
