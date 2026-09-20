# Bascule du worker du laboratoire propriétaire

État observé le 20 septembre 2026 : l'API locale tourne sous `PalRuntimeSvc`, un job `production` issu du Player est `queued`, et **aucun worker ne tourne**. Le dernier candidat `E:\Palimpseste\.runtime\operator-staging\worker-offline-compiled-20260920\Palimpseste.Worker.exe` a pour SHA-256 `987946213762B3E4052D1218377F3CFA748EF3F42BD7FF935E52BBE48CFF2F4A`. Le doctor correspondant, candidat `doctor-offline-compiled-20260920`, a pour SHA-256 `2F323942747724AAC599052854947C8C4341A561BD40F075F15989A24B7E527A` et est installé dans `E:\PalimpsesteRuntime\bin`. Le worker reste non installé. Dans `runtime.env`, `PALIMPSESTE_EFFORT_VERIFIED=false` et les deux champs de preuve d'effort sont vides. Le coffre PostgreSQL et les identifiants Windows restent hors du dépôt.

Le [prompt maître](../../prompts/00_AGENT_BUILD.md) réserve aux créateurs le jugement de fidélité du dessin. Le dessin droit de calibration a reçu une nouvelle description A `1.3`, mais son verdict humain n'est pas encore enregistré. Le dessin courbe actuellement en file est une autre capture. Aucune lecture A ni aucun sort de ce job n'a encore été produit. Ne pas ouvrir le worker en traitant une preuve technique isolée comme une acceptation humaine.

## Conditions avant le premier démarrage

1. Consigner la réponse humaine à la lecture A `1.3` et l'identifiant de la capture évaluée. Si elle est rejetée, recalibrer A et reprendre les contrôles. La lecture du dessin droit ne préjuge pas du résultat du dessin courbe en file.
2. Examiner les deux rapports réels déjà produits. Le premier `active` a appelé A `1.3` et B `1.1` sous `PalRuntimeSvc`, mais son B a été ensuite rejeté pour géométrie provisoire. Utiliser **seulement son A**, sortie SHA-256 `E54DEC5810E00AABD0FFCB8F152DF06468DD8505593B6718C5B806C46B3254B4`. Le second rapport `plan` a réutilisé cette A exacte et appelé seulement B sur la géométrie résolue depuis l'encre, avec `active_result=success`. Vérifier les deux rapports, leurs hashes d'origine publiés dans les preuves, le même SHA de `codex.exe`, l'identité `PalRuntimeSvc`, le modèle `gpt-5.6-luna` et l'effort rapporté `max`. Le vérificateur composite lie les deux rapports et le hash A ; aucun nouvel appel modèle n'est nécessaire pour cette preuve si elle passe.
3. `ProviderDoctor validate` a été exécuté sous `PalRuntimeSvc` sur **les octets A/B réels** et `artifacts\ink.png`. Ce mode ne lance aucun modèle : il revérifie les hashes, résout la géométrie depuis l'encre, valide le plan et compile un paquet de contrôle en mémoire avec des identifiants synthétiques. Le rapport privé `evidence\pending\doctor-validate-ab13-b11-20260920.json`, SHA-256 `D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA`, indique `validation_status=success`, `compilation_status=success` et `model_calls_executed=false`. Il reste en attente tant que le verdict humain n'est pas enregistré.
4. Après revue humaine, copier les trois **rapports intégraux** (actif A, plan B, validation hors ligne) dans `E:\PalimpsesteRuntime\approved-evidence` ; y créer un manifeste `provider_doctor_composite` version 2 contenant leurs chemins absolus et leurs SHA-256. Le manifeste ne prétend pas être une nouvelle sortie de Luna : il lie les deux sorties réelles et leur compilation contrôlée. Appliquer Administrateurs/SYSTEM `FullControl` et `PalRuntimeSvc` `ReadAndExecute`, sans droit de modification du service. Le nouveau `CodexSettings.Check(production: true)` exige le hash de chaque rapport, les fichiers originaux A/B et encre encore présents avec leurs hashes, A et B lancés réellement, la correspondance A figée / A active, la validation/compilation hors ligne et les métadonnées identiques. Il rejette l'ancien rapport actif seul, même si son champ historique `active_result` vaut `success`. Dans le fichier protégé `runtime.env`, changer uniquement `PALIMPSESTE_EFFORT_EVIDENCE_PATH`, `PALIMPSESTE_EFFORT_EVIDENCE_SHA256` et `PALIMPSESTE_EFFORT_VERIFIED=true`. Garder la preuve locale de désactivation des outils inchangée si ses hashes et ACL restent valides. Vérifier que `CodexSettings.Check(production: true)` ne rapporte aucun problème sous le compte de service.
5. Vérifier la migration `006`, la sauvegarde, la file et les baux avec les outils de `ops/`. Le job déjà en file sera acquis automatiquement au démarrage du worker ; un test de lancement est donc un vrai appel fournisseur possible, pas un démarrage neutre.

Commande de validation hors ligne, après installation du doctor candidat et du script `ProviderDoctor.Service.ps1` correspondant :

```powershell
.\ops\run-service-doctor.ps1 `
  -Mode validate `
  -CredentialFile 'C:\ProgramData\Palimpseste\operator-credentials\PalRuntimeSvc.credential.xml' `
  -RuntimeRoot 'E:\PalimpsesteRuntime' `
  -FrozenAJson 'E:\PalimpsesteRuntime\attempts\125b01cd45a6449693272e203b9c1e14\final.json' `
  -FrozenASha256 'E54DEC5810E00AABD0FFCB8F152DF06468DD8505593B6718C5B806C46B3254B4' `
  -FrozenBJson 'E:\PalimpsesteRuntime\attempts\9a0807143861481c92f71479351ed276\final.json' `
  -FrozenBSha256 'E8164D8D654F470101BDCE77A4B0AF46476FF7AE48EA9DF1A92A5B9A84D9AEA1' `
  -InkPng 'E:\PalimpsesteRuntime\artifacts\ink.png' `
  -EvidencePath 'E:\PalimpsesteRuntime\evidence\pending\doctor-validate-ab13-b11-20260920.json'
```

Conserver ces deux `final.json` et `ink.png` ainsi que leurs sauvegardes privées. Le verrou de production les relit à chaque démarrage et échoue si un fichier manque, change ou devient un point de jonction. `gc-artifacts.ps1` ne nettoie pas le dossier `attempts`, mais toute purge opérateur de ces fichiers rendrait la preuve inutilisable. Les hashes sont A `E54DEC5810E00AABD0FFCB8F152DF06468DD8505593B6718C5B806C46B3254B4`, B `E8164D8D654F470101BDCE77A4B0AF46476FF7AE48EA9DF1A92A5B9A84D9AEA1`, encre `18CDA98D1EF7E2A6F2148CFB81998D0B4494EA1E46BC288477459E33F1482AFC`.

Forme du manifeste après copie et calcul des trois hashes réels :

```json
{
  "kind": "provider_doctor_composite",
  "format_version": 2,
  "active_a_report": {
    "path": "E:\\PalimpsesteRuntime\\approved-evidence\\doctor-active-ab13-b11.json",
    "sha256": "256E564490E1A0A7E5BC50FBA216F4DC29F40EF496C867E03F3BE14291349610"
  },
  "plan_b_report": {
    "path": "E:\\PalimpsesteRuntime\\approved-evidence\\doctor-plan-b11-from-a13.json",
    "sha256": "BFAFCA2DE085F60AF2A974C60D74CE02CAE58082E28E8A0A740B27EEB22E3787"
  },
  "offline_validation_report": {
    "path": "E:\\PalimpsesteRuntime\\approved-evidence\\doctor-validate-ab13-b11.json",
    "sha256": "D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA"
  }
}
```

Les trois hashes portent sur les rapports originaux observés. Recalculer les hashes des copies et arrêter la bascule s'ils diffèrent. Le hash du manifeste lui-même est ensuite la valeur de `PALIMPSESTE_EFFORT_EVIDENCE_SHA256`. Le doctor final marque explicitement la validation **et** la compilation du plan pour les futurs rapports actifs ; voir la [preuve](../../evidence/public/backend/composite-worker-prep-2026-09-20.md).

## Installation et lancement après ces conditions

Depuis une console opérateur administrateur, copier les deux scripts et le binaire *après* avoir vérifié le hash du candidat et sauvegardé l'ancien exécutable et son ACL s'il existe. `E:\PalimpsesteRuntime\bin` possède déjà des ACE héritables : Administrateurs/SYSTEM `FullControl`, `PalRuntimeSvc` `ReadAndExecute`. Vérifier les ACL effectives des fichiers copiés ; ne pas accorder l'écriture au service. Les chemins ci-dessous restent privés et ne doivent pas être envoyés au Player.

```powershell
$source = 'E:\Palimpseste\.runtime\operator-staging\worker-offline-compiled-20260920\Palimpseste.Worker.exe'
$runtime = 'E:\PalimpsesteRuntime'
$expected = '987946213762B3E4052D1218377F3CFA748EF3F42BD7FF935E52BBE48CFF2F4A'
$childSource = '.\ops\worker-service-child.ps1'
$expectedChild = 'D776764F843BC2B2BD2D6A39FAFB6D3B77006C92ECCAA915DC8909E4B124CCC8'
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -cne $expected) {
    throw 'Candidate SHA-256 mismatch.'
}
if ((Get-FileHash -LiteralPath $childSource -Algorithm SHA256).Hash -cne $expectedChild) {
    throw 'Worker child script SHA-256 mismatch.'
}
Copy-Item -LiteralPath $source -Destination (Join-Path $runtime 'bin\Palimpseste.Worker.exe')
Copy-Item -LiteralPath $childSource `
    -Destination (Join-Path $runtime 'bin\WorkerService.Child.ps1')
if ((Get-FileHash -LiteralPath (Join-Path $runtime 'bin\Palimpseste.Worker.exe') -Algorithm SHA256).Hash -cne $expected -or
    (Get-FileHash -LiteralPath (Join-Path $runtime 'bin\WorkerService.Child.ps1') -Algorithm SHA256).Hash -cne $expectedChild) {
    throw 'Installed worker or child script hash mismatch.'
}
icacls.exe (Join-Path $runtime 'bin\Palimpseste.Worker.exe')
icacls.exe (Join-Path $runtime 'bin\WorkerService.Child.ps1')
```

Le lanceur `ops/start-owner-worker.ps1` exige le SHA du binaire et le verrou d'effort ouvert avant de créer le processus. Son enfant vérifie sous `PalRuntimeSvc` les deux preuves immuables, le modèle, l'effort et la concurrence, lit `DATABASE_URL` dans `C:\ProgramData\Palimpseste\lab-db.env`, efface les variables de clés API connues et démarre le worker sans secret dans sa ligne de commande. Il refuse une preuve hors de `approved-evidence`. Il ne s'agit pas d'une route HTTP ni d'une commande déclenchée par le parchemin.

```powershell
.\ops\start-owner-worker.ps1 -RuntimeRoot 'E:\PalimpsesteRuntime'
```

Une sortie `ready=true` signifie seulement que le processus worker attendu tourne sous le compte prévu. Contrôler ensuite les journaux privés, la transition du **même** job `queued` vers une description A visible, puis B, compilation et publication. Réouvrir ce sort dans le Player et le rejouer hors ligne avant de conclure à une boucle terminée. Ne pas déclarer le dessin courbe accepté sans verdict humain sur sa description et son sort. Un échec ou une tentative fournisseur incertaine impose inspection des traces avant reprise ; ne pas relancer automatiquement un appel qui a peut-être consommé du quota.

Les scripts de lancement ont été seulement analysés syntaxiquement dans le dépôt. Ils n'ont pas été copiés dans le runtime ni exécutés au 20 septembre 2026 ; le worker et le job Player restent arrêtés/en attente.
