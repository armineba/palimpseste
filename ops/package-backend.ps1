[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$CodexExecutable = '',
    [string]$CodexSha256 = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# Check the exact V2 schemas before publishing. This is local structural
# validation, not a live model/provider probe and consumes no account quota.
& python (Join-Path $PSScriptRoot 'validate-codex-schemas.py')
if ($LASTEXITCODE -ne 0) { throw 'Codex V2 output schema structural preflight failed.' }
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
Publish-App 'backend/BlueprintV2Doctor/BlueprintV2Doctor.csproj' 'blueprint-doctor'
Get-ChildItem -LiteralPath $stage -Filter '*.pdb' -File -Recurse | Remove-Item -Force

# Keep the compositor's MIT notice alongside every published application that
# carries its managed dependency, including with single-file publication.
$drawingNotice = Join-Path $projectRoot 'backend\Palimpseste.Provider\ThirdPartyNotices\System.Drawing.Common-MIT.txt'
foreach ($application in @('api', 'worker', 'doctor', 'visual-doctor', 'blueprint-doctor')) {
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
foreach ($name in @('01_MODEL_A_INTERPRETE.md', '02_MODEL_B_TRADUCTEUR.md', '03_REPARATION_TECHNIQUE.md', '04_IMAGE_REFERENCE.md', '05_VISUAL_CRITIC.md', '06_BLUEPRINT_V2.md', '07_V2_CRITIC.md', '08_V2_INTERPRETATION.md', '09_V2_NUMERIC_RULES.md', '10_UNITY_GOD_RUNTIME.md')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $projectRoot 'prompts') $name) -Destination (Join-Path $runtimePrompts $name)
}
$runtimePromptHistory = Join-Path $runtimePrompts 'history'
New-Item -ItemType Directory -Force -Path $runtimePromptHistory | Out-Null
foreach ($name in @('01_MODEL_A_INTERPRETE_2_3.md', '04_IMAGE_REFERENCE_1_2.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('prompts/history/' + $name)) -Destination (Join-Path $runtimePromptHistory $name)
}
# The generation worker consumes only this reviewed knowledge snapshot. Never
# copy authoring scripts, import helpers or arbitrary skill attachments into spec.
foreach ($relative in @('SKILL.md', 'references/runtime.md', 'references/methods.json')) {
    $source = Join-Path $projectRoot ('skills/unity-god/' + $relative)
    for ($cursor = [IO.Path]::GetFullPath($source); $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'UNITY GOD source links are not distributable.'
        }
    }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "UNITY GOD data missing: $relative" }
    $destination = Join-Path $spec ('skills/unity-god/' + $relative)
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -cne
        (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) { throw 'UNITY GOD runtime data copy differs.' }
}
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'migrations') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'backend/migrations') -Filter '*.sql' -File |
    Sort-Object Name |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path (Join-Path $stage 'migrations') $_.Name) }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'ops') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ops') -File |
    Where-Object { $_.Name.EndsWith('.ps1') -or $_.Name.EndsWith('.env.example') -or $_.Name.EndsWith('.patch') -or $_.Name.EndsWith('.md') -or $_.Name -eq 'validate-codex-schemas.py' } |
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
foreach ($name in @('D18_CONCURRENT_JOBS.md','D19_SOURCED_SURFACES.md','D20_SPELL_PIPELINE_V2.md','D21_UNITY_GOD.md','D21_1_SCHEMA_RECOVERY.md','D21_2_APPEND_ONLY_RECOVERY.md','D22_CONSTRUCTION_REPAIR.md','V2_BLUEPRINT_CONTRACT.md','V2_UNITY_STRUCTURAL_RENDERER.md','V2_VALIDATION_ENGINE.md','NEXT_ACTIONS.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('docs/' + $name)) -Destination (Join-Path $stage ('docs/' + $name))
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence/public/backend') -Destination (Join-Path $stage 'evidence') -Recurse

@"
Palimpseste backend Windows x64 — D22.3 UNITY GOD / Player 1.8.0 inchangé

Les exécutables API, worker et diagnostics sont autoportants. Nouveaux sorts : dessin -> description -> cinq bibliothèques -> SpellBlueprintV2 -> structure canonique et mouvement continu -> CORE_ONLY et critique aveugle -> VFX -> impacts réels et caméra gameplay -> verdict -> planche 3x7 et paquet Unity. Aucun candidat refusé par les gates obligatoires n'est publié. Quatre candidats maximum par admission ; étapes conservées et corrections ciblées.

Les admissions historiques 0..4 restent en V1. L'admission 5 utilise V2, client 1.8.0 requis. Aucun sort historique régénéré ni asset historique modifié. La planche échantillonne le même modèle temporel que le Player ; huit représentations structurelles sont disponibles.

D21 ajoute UNITY GOD aux nouvelles recherches V2 : fiches de construction issues des bibliothèques, sélection des méthodes pertinentes, réutilisation/adaptation/combinaison et bindings contrôlés contre les vrais paramètres du blueprint. Le reçu des méthodes est privé, lié par hash au plan et sauvegardé atomiquement avec sa révision. Les critiques non aveugles reçoivent ces méthodes ; le contrôle aveugle reste aveugle. Le Player et le contrat des sorts restent inchangés. Les anciens dossiers de recherche figés gardent leur parcours. Le skill constitue une base de connaissances consultée, pas un nouvel entraînement des poids du modèle.

Le worker utilise codex exec sous PalRuntimeSvc et conserve les protections du binaire natif épinglé. A/B/J ne peuvent ni télécharger, ni exécuter du code, ni lire les secrets, ni modifier Unity. Les blueprints sont des données bornées exécutées par le renderer précompilé protégé. Les scripts ops sont réservés à l'opérateur et ne doivent jamais être exposés au worker.

PALIMPSESTE_MAX_PROVIDER_CONCURRENCY=0 conserve l'absence de plafond applicatif de jobs ; le GPU de capture est partagé. Les quotas du compte et la capacité de la machine restent applicables. Aucun achat, recharge ou clé API ajouté.

Ressources intégrées : textures CC0 sélectionnées, portages TinyPlay MIT / Keijiro Unlicense. Les cinq bibliothèques sont consultées ; xtaja sans licence, Magic Effects non acquis et exemples Unity HDRP/LFS restent des références à disponibilité explicite. Toutes les bibliothèques ne sont pas embarquées. Conserver les notices du Player.

D22 conserve les brouillons et critiques dans les corrections, transmet les capacités géométriques exactes et permet une nouvelle fenêtre après correction du constructeur. Voir docs/D22_CONSTRUCTION_REPAIR.md. Les corrections de schéma D21 restent incluses.

Installation : lire IMPLEMENTATION_STATUS.md, docs/D20_SPELL_PIPELINE_V2.md et docs/D21_UNITY_GOD.md. Pour ce paquet Player 1.8, deploy-lifecycle.ps1 vérifie les données UNITY GOD et les migrations avant l'arrêt des services, puis applique 012 et 016 dans cet ordre (016 inclut 013/014/015) après drainage et vérification de 011. Les mises à jour 1.6/1.7 conservent leur chemin 011. Ne pas rejouer 009 ou 010 sur une base plus récente. Pour une base neuve, appliquer 001..016 dans l'ordre. Chaque migration exécutée et son SHA sont consignés. L'archive ne contient aucun auth.json, jeton joueur, secret DB ou clé API. Installer un renderer Player 1.8 protégé avec manifeste SHA complet.

L'API exige DATABASE_URL, ARTIFACT_ROOT et PALIMPSESTE_SPEC_ROOT. Le worker exige son compte dédié, CODEX_HOME isolé, les preuves natives et les pins du renderer. Le binaire et l'identité Codex existants sont conservés. Le service local est 127.0.0.1 ; cette archive ne configure ni HTTPS public ni identités distantes.

Les tests de contrat et la fixture Unity séparée sont documentés dans les preuves V2. Le rendu de la fixture n'est pas accepté artistiquement. completed=true signifie fin de capture, pas acceptation du sort. GPU/overdraw non mesurés restent inconnus. Consulter les preuves et le point de reprise pour l'état des appels Codex réels et de l'installation D21 : aucune compilation de paquet ne vaut déploiement ni acceptation visuelle.

blueprint-doctor est un outil opérateur pour vrais appels B/J isolés, sans écriture DB ni bibliothèque joueur. Seul son rapport atteste les appels réellement exécutés. Consulter le point de reprise pour finaliser.

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
