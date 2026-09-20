# Bascule du worker du laboratoire propriétaire

État observé le 20 septembre 2026 : l'API locale et le worker isolé tournent sous `PalRuntimeSvc` (PID `16420` et `8180` après redémarrage de l'API). Le job `production` capturé dans le Player est passé de `queued` à `interpreting`, puis à `ready`. La base indique deux tentatives fournisseur réussies, une description, un plan et un `compiled_spell`, sans tentative en échec. Le Player a montré la description A « Faisceau courbe de lave », téléchargé et vérifié la fiche du sort, ouvert le laboratoire et enregistré un lancement (`Lancers : 1`, `Dégâts : 0`). Après arrêt de l'API précédente (PID `25848`), le Player `WindowsPlayerBeamVisibleReady` a été relancé hors service (PID `35804`) : bibliothèque, fiche, laboratoire et lancement du même sort sont restés accessibles. L'API a ensuite été redémarrée et `/health/ready` a répondu 200.

La demande actuelle du propriétaire de faire fonctionner la boucle autorise ce **test technique privé** sur son dessin. Le [prompt maître](../../prompts/00_AGENT_BUILD.md) réserve toujours aux créateurs le jugement de fidélité et de qualité. Le trait droit de calibration A `1.3` attend leur verdict ; la capture courbe du Player est différente et son texte ainsi que son sort doivent recevoir leur propre verdict. L'ouverture du verrou technique de compatibilité modèle/effort et le job `ready` ne valent pas acceptation artistique ou recette M7.

## Preuves techniques et état de l'installation

1. Le diagnostic `active` a réellement appelé A `1.3` et B `1.1` sous `PalRuntimeSvc`. Son premier B utilisait une géométrie provisoire et n'est pas retenu. Seule son A figée est réutilisée, SHA-256 `E54DEC5810E00AABD0FFCB8F152DF06468DD8505593B6718C5B806C46B3254B4`.
2. Le diagnostic `plan` a appelé B seul sur cette A et sur la géométrie calculée depuis l'encre. `ProviderDoctor validate` a ensuite revérifié les octets A/B et l'encre, validé le plan et compilé un paquet de contrôle en mémoire **sans appel modèle**. Son rapport privé a pour SHA-256 `D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA` ; il indique `validation_status=success`, `compilation_status=success`, `model_calls_executed=false`.
3. Les trois rapports intégraux ont été copiés dans `E:\PalimpsesteRuntime\approved-evidence` avec leurs hashes et ACL protégés. Le manifeste `provider_doctor_composite` version 2, `doctor-composite-ab13-b11.json`, a pour SHA-256 `56F349016A5B63D352DD37225AF9253889D496C2399F58F97F70D0DAAC353E3F`. Il lie les appels réels A/B à la compilation contrôlée ; il ne contient aucune nouvelle sortie Luna. `runtime.env` est en UTF-8 sans BOM, pointe vers ce manifeste et porte `PALIMPSESTE_EFFORT_VERIFIED=true`. La preuve locale de désactivation des outils reste séparée.
4. Le Doctor installé dans `E:\PalimpsesteRuntime\bin` a pour SHA-256 `AAAAB2686CD9A33ADB6130B205D10596A63AF8207CBAE580ACD723C862196811`. Son diagnostic local après le correctif BOM a quitté avec le code 0, sans appel modèle et sans `production_issues` ; rapport privé SHA-256 `4FAF8EE53CA5DA823A53038D7DB3407FBA3349CAE9A164EA11C52B59ECA195AE`.
5. Le worker installé et le candidat `operator-staging/worker-composite-bom-20260920/Palimpseste.Worker.exe` ont pour SHA-256 `9243F9600973B1AAF5F97F07AD0983FF60A7EFA2EB0A7F7D42D142C05015847D`. Le script enfant installé et celui du dépôt ont pour SHA-256 `03CE0108D67A4AC27B332A41F36F1F12B2B362FA9A2AEBCF25EB21E8BDB4C104`. Le coffre PostgreSQL et les identifiants Windows restent hors du dépôt.

Commande historique utilisée pour la validation hors ligne de la calibration. Le rapport indiqué existe déjà : ne pas l'écraser.

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

Contenu vérifié du manifeste technique installé (les chemins restent privés) :

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

Les trois hashes portent sur les rapports originaux observés. Le hash du manifeste installé est la valeur actuelle de `PALIMPSESTE_EFFORT_EVIDENCE_SHA256`. Le doctor final marque explicitement la validation **et** la compilation du plan pour les futurs rapports actifs ; voir la [preuve](../../evidence/public/backend/composite-worker-prep-2026-09-20.md).

## Vérifications après le premier sort joueur

Le lanceur `ops/start-owner-worker.ps1` a été exécuté. Son enfant a contrôlé sous `PalRuntimeSvc` les preuves immuables, le modèle `gpt-5.6-luna`, l'effort `max`, la concurrence `1` et le coffre de base privé. Il efface les variables de clés API connues avant de démarrer le worker. Le job joueur a ensuite terminé A/B et la compilation ; cette observation est distincte du diagnostic de calibration.

Le **même** job a publié sa description A et son paquet compilé. Le Player a affiché A « Faisceau courbe de lave », puis la fiche téléchargée et vérifiée. Le laboratoire s'est ouvert ; un clic a porté le compteur à `Lancers : 1` et `Dégâts : 0`, conforme au plan visuel sans dégâts. Le correctif de visibilité à `0,6 s` a passé 2/2 tests PlayMode sur le paquet joueur. Unity a terminé avec le code 0 le build Windows `WindowsPlayerBeamVisibleReady` (29 fichiers, 122 093 149 octets ; EXE SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`, `GameAssembly.dll` SHA-256 `9FB651D09680CAF09B0A8668BEF2A914EFDE6B61F930EDBAE1CE90AF3B5C18C9`). Dans ce Player, une capture de l'effet visible puis disparu a été observée. Après arrêt de l'API, le même paquet a été rouvert et lancé depuis le laboratoire du Player relancé : la réutilisation sans service a été observée. L'écoute humaine du son et les verdicts humains sur la lecture, le rendu et l'intérêt du sort restent à consigner.

La [preuve du parcours joueur](../../evidence/public/unity/owner-player-end-to-end-2026-09-20.md) détaille les captures, les hashes du paquet, le rapport PlayMode et l'essai sans API. Une commande ultérieure de confort pour fermer puis relancer le Player après restauration de l'API a été refusée avant exécution (`blocked by policy`, sans motif détaillé) ; aucun essai équivalent n'a suivi. Le Player PID `35804` est resté ouvert dans sa session issue de l'essai sans API.

Pour les prochains jobs, si le processus s'arrête ou si une tentative fournisseur devient incertaine, inspecter les journaux et l'état durable avant tout redémarrage : un appel Codex peut avoir consommé du quota même sans réponse publiée. Ne pas lancer un second worker ou une reprise aveugle. Les ACL du runtime accordent à Administrateurs/SYSTEM le contrôle total et à `PalRuntimeSvc` seulement la lecture/exécution des binaires et des preuves approuvées. Ce laboratoire reste local au propriétaire ; aucune route de génération publique n'est ouverte.
