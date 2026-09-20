# Exploitation du worker Luna/Codex

Ces scripts configurent le profil du worker runtime hors du dÃ©pÃ´t. Ils ne
copient pas `auth.json`, ne lisent pas le `CODEX_HOME` personnel et ne crÃ©ent
aucun endpoint de prompt libre.

## Mise à jour D15 / 1.5.0

Les scripts sont préparés pour **`WindowsBehaviorRelease` → `WindowsBehaviorPlayable`**, journal `game/Logs/behavior-build.log`. Cette préparation ne constitue pas une preuve de build ou de déploiement ; consulter [l'état réel](../docs/IMPLEMENTATION_STATUS.md).

- `package-lifecycle-player.ps1` attend un build réussi `1.5.0`. Il conserve le chemin historique `evidence/public/unity/lifecycle-delivery.json`, avec version et nom D15 explicites, sauvegarde la livraison précédente et ajoute les deux notices CC0 des textures ainsi que la licence MIT du bruit HLSL sous `ThirdPartyNotices/`. Le manifeste distingue fichiers du build et licences ajoutées au packaging. Le script n'ouvre jamais le jeu.
- `package-backend.ps1` publie les programmes et inclut les migrations `001` à `010`. La spécification exporte `assets/sourced-vfx/catalogue.json`, `references.json`, les licences et les PNG. Les exemples HLSL `reference-code` et scripts d'import restent hors de la spécification runtime.
- `deploy-lifecycle.ps1 -StageRoot <stage-publié>` installe le Player protégé `1.5.0` et met à jour **le laboratoire existant**. Il vérifie le prérequis de schéma D14 et applique **uniquement `010_behavior_research.sql`** provenant du stage, après arrêt de l'admission et du worker sans job actif. Il ne lance aucun diagnostic, modèle ou essai de jeu.

**Ne pas rejouer `009_visual_review.sql` après D15** : sa contrainte n'autorise que les versions de parcours `0,1,2`, alors que `010` permet aussi `3`. Pour une **base neuve**, l'opérateur applique une seule fois toutes les migrations `001` à `010` dans l'ordre des noms, après préparation de sa connexion privée. Le script de mise à jour ci-dessus ne remplace pas cette initialisation complète. Les archives conservent les migrations triées pour cette installation.

La recherche avant construction consulte les sources primaires sélectionnées dans `references.json` ; les PNG réutilisés restent liés à 16 identifiants connus. Le renderer s'appuie sur des données contrôlées, sans chargement de shader source ou installation de plugin à la demande du joueur. Voir [les ressources et licences](../docs/references-vfx-sources.md).

## Archive réservée à l'opérateur

Le ZIP backend contient des scripts `ops` de provisionnement, sauvegarde et
restauration. L'extraire uniquement dans un dossier auquel le compte worker
`PalRuntimeSvc` n'a aucun accès. Sur une nouvelle installation, poser l'ACL
avant l'extraction pour que les fichiers héritent de cette restriction :

```powershell
$package = 'E:\PalimpsesteBackend'
New-Item -ItemType Directory -Path $package -Force | Out-Null
icacls.exe $package /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)(F)' '*S-1-5-32-544:(OI)(CI)(F)'
if ($LASTEXITCODE -ne 0) { throw 'ACL du dossier opérateur non appliquée' }
Expand-Archive -LiteralPath 'E:\Palimpseste-Backend-Windows-x64.zip' -DestinationPath $package
```

Ne pas lancer le worker depuis ce dossier. L'opérateur utilise ses scripts
`ops`, copie `worker/Palimpseste.Worker.exe` et `doctor/ProviderDoctor.exe`
dans `E:\PalimpsesteRuntime\bin` après le provisionnement, puis vérifie que
seuls ces exécutables et `codex.exe` y sont accessibles en lecture/exécution
au compte worker. Copier l'API publiée vers son emplacement de service avec
une ACL propre au compte API. Garder l'archive extraite et tous les scripts
`ops` inaccessibles au compte worker. La spécification du ZIP sert uniquement
de source au provisionneur, qui installe sa copie contrôlée sous runtime.

Avant de lancer le provisionneur avec -ApplyAcl, arreter le worker joueur.
Le script ecrit runtime.env, copie la specification et pose ensuite les ACL;
il ne doit pas courir en parallele d'une generation.

## Provisionner un profil dÃ©diÃ©

Depuis une console dâ€™administration, aprÃ¨s avoir crÃ©Ã© le compte Windows de
service :

```powershell
.\ops\provision-runtime.ps1 `
  -ServiceUser 'PalRuntimeSvc' `
  -RuntimeRoot 'E:\PalimpsesteRuntime' `
  -DevelopmentRoot 'E:\Palimpseste\Palimpseste_Unity_Dossier_Luna_Codex\Palimpseste_Unity_Dossier' `
  -SourceSpecRoot 'E:\PalimpsesteBackend\spec' `
  -SpecRoot 'E:\PalimpsesteRuntime\spec' `
  -CodexExecutable 'E:\PalimpsesteRuntime\bin\codex.exe' `
  -ApplyAcl
```

`DevelopmentRoot` est lâ€™arbre source Ã  refuser au compte joueur; il ne doit pas
Ãªtre le rÃ©pertoire du ZIP dÃ©ployÃ©. `SourceSpecRoot` peut Ãªtre `spec` dans ce
ZIP ou les trois dossiers de spÃ©cification du dÃ©pÃ´t pendant un staging local.
La commande Ã©crit `runtime.env`, un `codex-home` dÃ©diÃ©, les dossiers
`attempts`, `inputs`, `evidence/pending`, `approved-evidence` et `logs`, ainsi quâ€™un manifeste. Si `SpecRoot`
est la valeur runtime par defaut, elle copie seulement `contracts`, `prompts` et `reference` depuis `SourceSpecRoot` vers la racine deployee.
Le service recoit
RX sur la specification et le binaire Codex; l ACL lui refuse l arbre de
dÃ©veloppement. Le fichier env ne contient aucun secret. Lâ€™authentification doit
Ãªtre rÃ©alisÃ©e par lâ€™opÃ©rateur dans ce `CODEX_HOME` dÃ©diÃ©, sous le compte de
service.

La racine runtime, `runtime.env`, `provisioning-manifest.json`,
`evidence` et `approved-evidence` sont en lecture seule pour le compte worker.
Seul `evidence/pending` recoit ses sorties doctor. Apres revue, l'operateur
copie la preuve approuvee dans `approved-evidence`, calcule le SHA-256 de
cette copie, puis met a jour `runtime.env` en tant qu'administrateur. Le compte
worker ne doit pouvoir supprimer aucun parent des fichiers approuves.

Le provisionneur exige aussi que SPEC_ROOT et ARTIFACT_ROOT soient sous cette
racine, dans deux dossiers distincts des dossiers reserves, et que le binaire
Codex soit dans runtime/bin. Il reconstruit
les ACL de chaque fichier runtime avec exactement SYSTEM, Administrateurs
et le compte worker, afin qu'une ancienne autorisation de groupe ne rende
pas la configuration ou une preuve modifiable par le service.
Avant toute ecriture, le script refuse les reparses sur les chemins d'entree,
la specification source/cible et les DACL protegees sous DevelopmentRoot, car
un deny herite sur le depot ne couvrirait pas ces enfants.

La racine runtime doit rester hors du dÃ©pÃ´t et hors des rÃ©pertoires utilisateur.
Le worker doit Ãªtre lancÃ© avec le mÃªme compte que `PALIMPSESTE_SERVICE_USER`.
`PALIMPSESTE_SPEC_ROOT` pointe vers la copie dÃ©ployÃ©e hors du dÃ©pÃ´t; les
entrÃ©es utilisateur sont copiÃ©es dans une tentative privÃ©e avant lancement.

## Diagnostics

Le diagnostic local est gratuit et ne lance pas de tour modÃ¨le :

```powershell
.\ops\provider-doctor.ps1 `
  -Mode local `
  -EnvFile 'E:\PalimpsesteRuntime\runtime.env' `
  -DoctorExecutable 'E:\PalimpsesteRuntime\bin\ProviderDoctor.exe' `
  -EvidencePath 'E:\PalimpsesteRuntime\evidence\pending\doctor-local.json'
```

Le backend doit Ãªtre publiÃ© avant cette commande; elle appelle un exÃ©cutable
`ProviderDoctor.exe` publiÃ© et ne fait aucun `dotnet run` ni build du dÃ©pÃ´t. Il
vÃ©rifie la version, `codex exec --help`, les options effectivement utilisÃ©es,
le profil dÃ©diÃ©, lâ€™identitÃ© du service, les racines autorisÃ©es, lâ€™Ã©tat dâ€™auth
sans secret et le niveau demandÃ©. Une sortie locale rÃ©ussie ne prouve pas que
Luna a acceptÃ© lâ€™effort.
Dans le ZIP backend, lâ€™exÃ©cutable se trouve sous `doctor\ProviderDoctor.exe`;
aprÃ¨s installation runtime, le chemin conseillÃ© est la copie RX
`E:\PalimpsesteRuntime\bin\ProviderDoctor.exe` passÃ©e explicitement.

Le diagnostic actif est une action opÃ©rateur sÃ©parÃ©e et consomme deux appels
normaux :

```powershell
.\ops\provider-doctor.ps1 `
  -Mode active `
  -EnvFile 'E:\PalimpsesteRuntime\runtime.env' `
  -DoctorExecutable 'E:\PalimpsesteRuntime\bin\ProviderDoctor.exe' `
  -SpecRoot 'E:\PalimpsesteRuntime\spec' `
  -ReferencePng 'E:\PalimpsesteRuntime\artifacts\reference.png' `
  -DrawingPng 'E:\PalimpsesteRuntime\artifacts\drawing.png' `
  -InkPng 'E:\PalimpsesteRuntime\artifacts\ink.png' `
  -EvidencePath 'E:\PalimpsesteRuntime\evidence\pending\doctor-active.json'
```

Le mode actif exige que les fichiers soient sous les racines dÃ©clarÃ©es, envoie
les deux images Ã  A, rÃ©sout la gÃ©omÃ©trie depuis sa description et l'encre avec
le mÃªme `GeometryResolver` que le worker, puis envoie cette gÃ©omÃ©trie Ã  B.
`GeometryJson` est refusÃ© pour Ã©viter une gÃ©omÃ©trie provisoire dÃ©synchronisÃ©e.
Il nâ€™est jamais appelÃ© par les routes `/health`.

Pour ajuster B sans rÃ©pÃ©ter un appel A, `-Mode plan` prend le fichier JSON A
figÃ© sous `artifacts` ou `attempts`, son SHA-256 exact avec `-FrozenASha256`,
et la mÃªme encre avec `-InkPng`. Seul B appelle Luna. La preuve indique
explicitement que A a Ã©tÃ© rÃ©utilisÃ©e; ce mode ne constitue pas une nouvelle
preuve A/B de compatibilitÃ© d'effort.

Apres un actif reussi, l'operateur examine les preuves A/B et l'attestation
fournisseur, copie le JSON dans `approved-evidence`, calcule le SHA-256 de la copie et
inscrit son chemin, ce hash et `PALIMPSESTE_EFFORT_VERIFIED=true` dans le
fichier env externe. Le worker exige alors que le JSON contienne A et B rÃ©ussis,
le compte de service attendu, le modÃ¨le demandÃ© et `reported_effort=max`; un
simple boolÃ©en ne suffit pas. Une modification silencieuse du modÃ¨le, une
baisse dâ€™effort, un fallback ou lâ€™activation dâ€™outils runtime bloque le worker.

## Tests locaux

```powershell
.\ops\run-provider-security-tests.ps1
```

Le test lance un faux exÃ©cutable uniquement dans un rÃ©pertoire temporaire de
test. Il contrÃ´le les arguments `codex exec`, lâ€™effacement de lâ€™environnement,
les racines de fichiers, les limites dâ€™images A/B et le fait quâ€™un prompt avec
des mÃ©tacaractÃ¨res ne devient jamais une commande shell. Il ne prÃ©tend pas
tester lâ€™authentification Luna ni le confinement OS du serveur.

## Sauvegarde et restauration B27

ArrÃªter lâ€™API et le worker, ou les placer dans un Ã©tat sans Ã©criture, pendant
toute la sauvegarde :

```powershell
.\ops\backup-runtime.ps1 `
  -DatabaseUrl $env:DATABASE_URL `
  -ArtifactRoot $env:ARTIFACT_ROOT `
  -BackupRoot 'E:\PalimpsesteBackups' `
  -Label 'avant-recette'
```

Le dump PostgreSQL et chaque artefact sont hashÃ©s dans le manifeste. La
connexion nâ€™est pas enregistrÃ©e. La restauration exige `-AllowDatabaseReplace`,
vÃ©rifie tous les hashes et refuse de fusionner un rÃ©pertoire dâ€™artefacts non
vide :

```powershell
.\ops\restore-runtime.ps1 `
  -BackupRoot 'E:\PalimpsesteBackups\avant-recette' `
  -DatabaseUrl $env:DATABASE_URL `
  -ArtifactRoot $env:ARTIFACT_ROOT `
  -ServiceUser 'PalRuntimeSvc' `
  -AllowDatabaseReplace
```

Pour le laboratoire PostgreSQL local, `backup-lab.ps1` accepte `-EnvFile`,
`-ArtifactRoot` et `-Destination`. `restore-lab.ps1` accepte `-BackupDirectory`,
`-AdminEnvFile`, `-TargetDatabase` et `-TargetArtifactRoot` ; il crÃ©e uniquement
une base et un stockage absents. `status-lab.ps1 -EnvFile ...` lit les comptes
par Ã©tat, les tentatives et les baux expirÃ©s. `gc-artifacts.ps1` affiche les
candidats orphelins en mode sec par dÃ©faut et impose un dÃ©lai de grÃ¢ce.
La sauvegarde et la restauration Ã  blanc effectivement vÃ©rifiÃ©es sont dÃ©crites
dans [`evidence/public/backend/api-restauration-2026-09-19.md`](../evidence/public/backend/api-restauration-2026-09-19.md).
## Preuve locale des capacites et copie du doctor

Le provisionneur ne copie pas de binaire depuis le dépôt source. Après
provisionnement, l'opérateur copie explicitement le worker et le doctor
publiés depuis le dossier opérateur, puis vérifie leurs ACL avant lancement :

```powershell
$runtime = 'E:\PalimpsesteRuntime'
$package = 'E:\PalimpsesteBackend'
New-Item -ItemType Directory -Path (Join-Path $runtime 'bin') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $package 'worker\Palimpseste.Worker.exe') `
  -Destination (Join-Path $runtime 'bin\Palimpseste.Worker.exe') -Force
Copy-Item -LiteralPath (Join-Path $package 'doctor\ProviderDoctor.exe') `
  -Destination (Join-Path $runtime 'bin\ProviderDoctor.exe') -Force
icacls.exe (Join-Path $runtime 'bin\Palimpseste.Worker.exe')
icacls.exe (Join-Path $runtime 'bin\ProviderDoctor.exe')
```

Le resultat doit montrer une autorisation de lecture/execution pour
`PALIMPSESTE_SERVICE_USER`. Le chemin doit rester hors du depot et hors du
profil Codex personnel. Quand `SourceSpecRoot` pointe vers `ZIP\spec`, seule
la copie des contrats, prompts A/B/reparation et references est necessaire;
`prompts\00_AGENT_BUILD.md` n est jamais installe dans le runtime joueur.

Le doctor lance aussi `codex --disable ... features list` sans modele. Il
enregistre les valeurs observees pour chaque capacite demandee. Cette commande
prouve le comportement observe du binaire local et l acceptation syntaxique
des flags; elle ne suffit pas a marquer les features runtime verifiees pour
le worker. Sur cette version, `unified_exec` peut rester `true` meme avec
`--disable unified_exec`; le preflight conserve cette observation et refuse
une preuve ou un outil expose reste actif.

Les scripts de sauvegarde et restauration exigent explicitement hote, port
(5432 par defaut), utilisateur, mot de passe et base dans `DATABASE_URL`. Le
mot de passe passe a PostgreSQL par `PGPASSWORD` dans l environnement du
processus enfant et jamais comme argument; `--no-password` interdit un
fallback interactif vers une autre instance. Fournir `-PostgresBin` si les
binaires ne sont pas dans `C:\Program Files\PostgreSQL\17\bin` ou le `PATH`.
La restauration exige `-ServiceUser` (ou `PALIMPSESTE_SERVICE_USER`) et
reapplique les ACL Modify au compte worker ainsi qu aux comptes optionnels
separes par virgule ou point-virgule dans `PALIMPSESTE_ARTIFACT_USERS`, apres
le deplacement du staging valide.

Pour activer la porte features apres revue du doctor local :

```powershell
$evidence = 'E:\PalimpsesteRuntime\approved-evidence\doctor-local-sha-service.json'
$hash = (Get-FileHash -LiteralPath $evidence -Algorithm SHA256).Hash.ToLowerInvariant()
# Verifier aussi que cli_executable_sha256 egale le hash du codex.exe deploye.
# Reporter ces trois valeurs dans runtime.env hors du depot.
PALIMPSESTE_RUNTIME_FEATURES_VERIFIED=true
PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_PATH=$evidence
PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_SHA256=$hash
```

Le JSON doit rester immuable apres son hash; relancer le doctor avec `--write`
sur le meme chemin exige une nouvelle revue et un nouveau hash.

Pour le laboratoire proprietaire actuel, `start-owner-api.ps1` lance l'API
sur `127.0.0.1:18080` sous `PalRuntimeSvc`. Le worker reste arrete tant que
la revue humaine et la preuve A/B composite ne sont pas admises. La procedure
exacte, le binaire candidat et ses hashes sont dans
[`OWNER_WORKER_CUTOVER.md`](../docs/ops/OWNER_WORKER_CUTOVER.md) ;
`start-owner-worker.ps1` refuse le verrou ferme avant tout lancement.
