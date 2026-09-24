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

# Keep the compositor's MIT notice alongside every published application that
# carries its managed dependency, including with single-file publication.
$drawingNotice = Join-Path $projectRoot 'backend\Palimpseste.Provider\ThirdPartyNotices\System.Drawing.Common-MIT.txt'
foreach ($application in @('api', 'worker', 'doctor', 'visual-doctor')) {
    $noticeFolder = Join-Path (Join-Path $stage $application) 'ThirdPartyNotices'
    New-Item -ItemType Directory -Path $noticeFolder -Force | Out-Null
    $noticeTarget = Join-Path $noticeFolder 'System.Drawing.Common-MIT.txt'
    Copy-Item -LiteralPath $drawingNotice -Destination $noticeTarget -Force
    if ((Get-FileHash -LiteralPath $drawingNotice -Algorithm SHA256).Hash -cne
        (Get-FileHash -LiteralPath $noticeTarget -Algorithm SHA256).Hash) { throw 'Compositor license copy differs.' }
}

$spec = Join-Path $stage 'spec'
New-Item -ItemType Directory -Force -Path $spec | Out-Null
foreach ($directory in @('contracts', 'reference')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $directory) -Destination (Join-Path $spec $directory) -Recurse
}
# Only reviewed runtime data is distributed. The HLSL reference snapshots and
# any authoring/import scripts under the source asset root stay in development.
$sourceVfx = Join-Path $projectRoot 'assets\sourced-vfx'
$runtimeVfx = Join-Path $spec 'assets\sourced-vfx'
New-Item -ItemType Directory -Path $runtimeVfx -Force | Out-Null
foreach ($name in @('catalogue.json', 'references.json')) {
    Copy-Item -LiteralPath (Join-Path $sourceVfx $name) -Destination (Join-Path $runtimeVfx $name)
}
foreach ($folder in @('licenses', 'textures')) {
    $sourceFolder = Join-Path $sourceVfx $folder
    foreach ($file in Get-ChildItem -LiteralPath $sourceFolder -Recurse -File) {
        $extension = if ($folder -eq 'licenses') { '.txt' } else { '.png' }
        if ($file.Extension -ine $extension) { continue }
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'VFX source links are not distributable.' }
        $relative = $file.FullName.Substring($sourceVfx.Length + 1)
        $destination = Join-Path $runtimeVfx $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
        if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne
            (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) { throw 'VFX runtime data copy differs.' }
    }
}
$runtimePrompts = Join-Path $spec 'prompts'
New-Item -ItemType Directory -Force -Path $runtimePrompts | Out-Null
foreach ($name in @('01_MODEL_A_INTERPRETE.md', '02_MODEL_B_TRADUCTEUR.md', '03_REPARATION_TECHNIQUE.md', '04_IMAGE_REFERENCE.md', '05_VISUAL_CRITIC.md')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $projectRoot 'prompts') $name) -Destination (Join-Path $runtimePrompts $name)
}
$runtimePromptHistory = Join-Path $runtimePrompts 'history'
New-Item -ItemType Directory -Force -Path $runtimePromptHistory | Out-Null
foreach ($name in @('01_MODEL_A_INTERPRETE_2_3.md', '04_IMAGE_REFERENCE_1_2.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('prompts/history/' + $name)) -Destination (Join-Path $runtimePromptHistory $name)
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
New-Item -ItemType Directory -Path (Join-Path $stage 'docs') -Force | Out-Null
foreach ($name in @('D18_CONCURRENT_JOBS.md','NEXT_ACTIONS.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('docs/' + $name)) -Destination (Join-Path $stage ('docs/' + $name))
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence/public/backend') -Destination (Join-Path $stage 'evidence') -Recurse

@"
Palimpseste backend Windows x64. API, worker et doctor sont des exécutables .NET autoportants.
Backend D18 / Player 1.6.0. La chaîne privée est : dessin libre -> Sol high (description et cycle complet) -> atlas natif Codex 7x3 -> compositeur serveur fixe -> planche VFX 1536x1152 -> consultation des cinq bibliothèques obligatoires et d'une référence complémentaire -> Astra high (construction depuis la description, la planche et la recherche conservée, prompt B 2.5) -> compilateur contrôlé -> rendu Unity précompilé -> critique visuelle indépendante J -> ajustements bornés -> paquet Unity 1.6.
PALIMPSESTE_MAX_PROVIDER_CONCURRENCY=0 : aucun plafond applicatif de jobs en parallèle dans le worker. Les captures Unity partagent un seul créneau GPU. Les limites du compte et de la machine restent applicables. Voir docs/D18_CONCURRENT_JOBS.md et les preuves de déploiement ; aucun essai de concurrence n'est ajouté par le développement.
La planche comporte APPARITION, STABLE, DISPARITION, sept cases numérotées 1 à 7 par ligne, titre et sous-titre VFX ANIMATION SHEET. L'atlas réellement produit par le fournisseur et la planche mise en page par le serveur ont des artefacts et SHA distincts ; la planche n'est pas présentée comme la sortie native du modèle. System.Drawing.Common 10.0.12 est gratuit sous MIT, Windows uniquement ; sa notice accompagne les programmes publiés. Aucune police n'est distribuée.
Le worker utilise codex exec sous un compte Windows de service isolé. Il n'exécute ni C# issu d'un dessin, ni build Unity.
L'archive contient les contrats, références, prompts A/G/B/J et migrations 001 à 011, mais aucun auth.json, jeton joueur, secret DB ou clé API. Les versions historiques A 2.3 et G 1.2 restent incluses pour les anciens parcours admis. spec/assets/sourced-vfx contient seulement les catalogues, licences et PNG contrôlés : aucun reference-code, plugin ni script d'import. Le binaire durci A/B/G/J et son attestation native sont décrits dans ops/codex-image-generation.md. Si codex/ est présent, son exécutable a été inclus avec un SHA vérifié et les notices amont ; sinon le construire à partir des correctifs fournis. Dans les deux cas, établir les preuves sur le compte de service avant activation.
Lire IMPLEMENTATION_STATUS.md puis ops/provision-runtime.ps1 avant toute installation.
Extraire l'archive dans un dossier opérateur inaccessible au compte worker : elle contient des scripts ops d'administration.
Copier api, worker et doctor publiés vers leurs emplacements de service avec ACL minimales ; ne pas lancer le worker depuis le dossier extrait.
L'API attend DATABASE_URL, ARTIFACT_ROOT et PALIMPSESTE_SPEC_ROOT pointant vers la copie runtime de spec.
Le worker attend en plus le compte Windows dédié, CODEX_HOME isolé et la preuve du doctor actif. D16 conserve le binaire natif D13 et ses preuves ; les nouveaux prompts, le compositeur et le rendu D16 restent à essayer par le propriétaire.
Le rendu de critique demande PALIMPSESTE_VISUAL_RENDERER_EXE et PALIMPSESTE_VISUAL_RENDERER_MANIFEST_SHA256. Installer une copie du Player 1.6.0 livré, avec manifeste SHA de tous les fichiers, hors sources et dossiers modifiables du worker. ops/deploy-lifecycle.ps1 met à jour le PC existant : migration 010 et sa table spell_reference_research doivent déjà être présentes, seule migration 011 est appliquée. Ne pas rejouer 009 ou 010 sur des jobs plus récents : elles restreignent l'ancienne contrainte de version. Pour une base neuve, appliquer toutes les migrations 001 à 011 dans l'ordre avant de lancer les services. Ne pas exposer les scripts ops au worker.
La recherche par job examine TinyPlay URPShadersCollection, xtaja VFX-Shader, Unity VisualEffectGraph-Samples, Keijiro VfxGraphAssets et la fiche locale de disponibilité/licence Magic Effects FREE. Les nouveaux packages ne sont pas embarqués dans le Player ; les 16 textures CC0 déjà intégrées restent sélectionnables. La recherche ne donne aucun droit de téléchargement libre ou d'installation de code au joueur. Conserver les notices des textures dans la distribution du Player.
Le programme visual-doctor est un diagnostic manuel facultatif, non exécuté pour cette livraison. Les captures automatiques de la génération joueur servent à la critique visuelle ; elles ne prouvent pas le gameplay ni une fidélité visuelle parfaite.
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
