[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$CodexExecutable = '',
    [string]$CodexSha256 = ''
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
Publish-App 'backend/SpellVisualDoctor/SpellVisualDoctor.csproj' 'visual-doctor'
Get-ChildItem -LiteralPath $stage -Filter '*.pdb' -File -Recurse | Remove-Item -Force

$spec = Join-Path $stage 'spec'
New-Item -ItemType Directory -Force -Path $spec | Out-Null
foreach ($directory in @('contracts', 'reference')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $directory) -Destination (Join-Path $spec $directory) -Recurse
}
$runtimePrompts = Join-Path $spec 'prompts'
New-Item -ItemType Directory -Force -Path $runtimePrompts | Out-Null
foreach ($name in @('01_MODEL_A_INTERPRETE.md', '02_MODEL_B_TRADUCTEUR.md', '03_REPARATION_TECHNIQUE.md', '04_IMAGE_REFERENCE.md', '05_VISUAL_CRITIC.md')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $projectRoot 'prompts') $name) -Destination (Join-Path $runtimePrompts $name)
}
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'migrations') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'backend/migrations') -Filter '*.sql' -File |
    Sort-Object Name |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path (Join-Path $stage 'migrations') $_.Name) }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'ops') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ops') -File |
    Where-Object { $_.Name.EndsWith('.ps1') -or $_.Name.EndsWith('.env.example') -or $_.Name.EndsWith('.patch') -or $_.Name.EndsWith('.md') } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path (Join-Path $stage 'ops') $_.Name) }

if (-not [string]::IsNullOrWhiteSpace($CodexExecutable)) {
    if ($CodexSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        (Get-FileHash -LiteralPath $CodexExecutable -Algorithm SHA256).Hash -ine $CodexSha256) {
        throw 'Le binaire Codex doit correspondre au SHA explicitement vérifié.'
    }
    $native = Join-Path $stage 'codex'
    New-Item -ItemType Directory -Path $native | Out-Null
    Copy-Item -LiteralPath $CodexExecutable -Destination (Join-Path $native 'codex-image.exe')
    Copy-Item -Path (Join-Path $projectRoot 'ops/codex-licenses/*.txt') -Destination $native
    [ordered]@{
        base_tag='rust-v0.154.0-alpha.6.2'; modified=$true;
        sha256=$CodexSha256.ToLowerInvariant();
        patches=@('ops/codex-attestation.patch','ops/codex-image-generation.patch');
        runtime_guard='PALIMPSESTE_IMAGEGEN_TEXT_ONLY=1';
        tools='A/B/J: empty native registry. G: only image_gen.imagegen.'
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $native 'BUILD.json') -Encoding UTF8
}

# Single-file publishing may change the executable bytes across publishes.
# Bind the archive's worker launch scripts to the executable in this archive.
function Set-ArchiveHashPin([string]$Path, [string]$Variable, [string]$Hash) {
    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $pattern = '(?m)^\$' + [regex]::Escape($Variable) + " = '[0-9A-Fa-f]{64}'(?=\r?$)"
    if ([regex]::Matches($content, $pattern).Count -ne 1) { throw "Archive pin missing: $Variable" }
    $line = [string]::Concat('$', $Variable, ' = ', [char]39, $Hash, [char]39)
    $content = [regex]::Replace($content, $pattern,
        [System.Text.RegularExpressions.MatchEvaluator] { param($match) $line })
    [IO.File]::WriteAllText($Path, $content, [Text.UTF8Encoding]::new($false))
}
$archiveWorkerHash = (Get-FileHash -LiteralPath (Join-Path $stage 'worker/Palimpseste.Worker.exe') -Algorithm SHA256).Hash
$archiveWorkerChild = Join-Path $stage 'ops/worker-service-child.ps1'
$archiveWorkerLauncher = Join-Path $stage 'ops/start-owner-worker.ps1'
Set-ArchiveHashPin $archiveWorkerChild 'expectedWorkerSha256' $archiveWorkerHash
$archiveWorkerChildHash = (Get-FileHash -LiteralPath $archiveWorkerChild -Algorithm SHA256).Hash
Set-ArchiveHashPin $archiveWorkerLauncher 'expectedWorkerSha256' $archiveWorkerHash
Set-ArchiveHashPin $archiveWorkerLauncher 'expectedChildSha256' $archiveWorkerChildHash
$archiveApiHash = (Get-FileHash -LiteralPath (Join-Path $stage 'api/Palimpseste.Api.exe') -Algorithm SHA256).Hash
$archiveApiChildHash = (Get-FileHash -LiteralPath (Join-Path $stage 'ops/api-service-child.ps1') -Algorithm SHA256).Hash
$archiveApiLauncher = Join-Path $stage 'ops/start-owner-api.ps1'
Set-ArchiveHashPin $archiveApiLauncher 'expectedApiSha256' $archiveApiHash
Set-ArchiveHashPin $archiveApiLauncher 'expectedChildSha256' $archiveApiChildHash

Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/IMPLEMENTATION_STATUS.md') -Destination (Join-Path $stage 'IMPLEMENTATION_STATUS.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence/public/backend') -Destination (Join-Path $stage 'evidence') -Recurse

@"
Palimpseste backend Windows x64. API, worker et doctor sont des exécutables .NET autoportants.
La chaîne privée est : dessin libre -> Sol high (description et cycle complet) -> image native Codex -> Astra high (construction depuis la description, image cible) -> compilateur contrôlé -> rendu Unity précompilé -> critique visuelle indépendante J -> ajustements bornés -> paquet Unity 1.4.
Le worker utilise codex exec sous un compte Windows de service isolé. Il n'exécute ni C# issu d'un dessin, ni build Unity.
L'archive contient les contrats, références, prompts A/G/B et migrations, mais aucun auth.json, jeton joueur, secret DB ou clé API. Le binaire durci A/B/G et son attestation native sont décrits dans ops/codex-image-generation.md. Si codex/ est présent, son exécutable a été inclus avec un SHA vérifié et les notices amont ; sinon le construire à partir des correctifs fournis. Dans les deux cas, établir les preuves sur le compte de service avant activation.
Lire IMPLEMENTATION_STATUS.md puis ops/provision-runtime.ps1 avant toute installation.
Extraire l'archive dans un dossier opérateur inaccessible au compte worker : elle contient des scripts ops d'administration.
Copier api, worker et doctor publiés vers leurs emplacements de service avec ACL minimales ; ne pas lancer le worker depuis le dossier extrait.
L'API attend DATABASE_URL, ARTIFACT_ROOT et PALIMPSESTE_SPEC_ROOT pointant vers la copie runtime de spec.
Le worker attend en plus le compte Windows dédié, CODEX_HOME isolé et la preuve du doctor actif. D14 conserve le binaire natif D13 et ses preuves ; les nouveaux prompts et le rendu D14 restent à essayer par le propriétaire.
Le rendu de critique demande PALIMPSESTE_VISUAL_RENDERER_EXE et PALIMPSESTE_VISUAL_RENDERER_MANIFEST_SHA256. Installer une copie du Player 1.4 livré, avec manifeste SHA de tous les fichiers, hors sources et dossiers modifiables du worker. ops/deploy-lifecycle.ps1 effectue cette installation sur le PC existant, applique migration009 et relance les services, sans génération ni diagnostic. Ne pas exposer les scripts ops au worker.
Le programme visual-doctor est un diagnostic manuel facultatif, non exécuté pour cette livraison. Les captures automatiques de la génération joueur comparent quatre phases décoratives ; elles ne prouvent pas le gameplay ni une fidélité visuelle parfaite.
La génération reste bloquée tant que le compte de service Codex et le doctor actif ne sont pas validés.
Le service local actuel emploie 127.0.0.1 ; cette archive ne configure pas une URL HTTPS publique ni les identités des joueurs.
"@ | Set-Content -LiteralPath (Join-Path $stage 'README.txt') -Encoding UTF8

$zip = Join-Path $deliverables 'Palimpseste-Backend-Windows-x64.zip'
$pendingZip = Join-Path $stageRoot ([Guid]::NewGuid().ToString('N') + '.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $pendingZip,
    [IO.Compression.CompressionLevel]::Optimal, $false)
# Finish and close the archive before replacing the distributable file.
Move-Item -LiteralPath $pendingZip -Destination $zip -Force
$file = Get-Item -LiteralPath $zip
$sha = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Backend ZIP: $($file.FullName)"
Write-Output "Bytes: $($file.Length)"
Write-Output "SHA256: $sha"
Write-Output "Staging conservé: $stage"
