# ProviderDoctor : géométrie réelle et réutilisation de A

Le 20 septembre 2026, `ProviderDoctor active` a été modifié pour résoudre la géométrie à partir de la sortie A validée et du PNG d'encre canonique. Le tableau JSON envoyé à B est assemblé par `geometry_id` en ordre ordinal, comme dans `JobProcessor`. Le mode `plan` reçoit une sortie A figée sous `artifacts` ou `attempts`, vérifie son SHA-256 fourni par l'opérateur, recalcule la même géométrie et n'appelle que B. La preuve privée inclura les hashes A, encre et contexte, les identifiants de géométrie et l'indication explicite de la réutilisation de A. L'ancien argument `GeometryJson` est refusé par les lanceurs.

Vérifications exécutées sans appel Luna :

- `dotnet build backend/ProviderDoctor/ProviderDoctor.csproj -c Release --no-restore` : réussi, 0 avertissement, 0 erreur.
- `dotnet run --project tests/Palimpseste.Core.Smoke/Palimpseste.Core.Smoke.csproj -c Release --no-restore` : réussi, tests de décodage PNG, géométrie et compilation contrôlée.
- Analyse syntaxique PowerShell des trois lanceurs : 0 erreur chacun.
- `git diff --check` sur les fichiers modifiés : aucune erreur de whitespace.
- `dotnet publish backend/ProviderDoctor/ProviderDoctor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o E:\Palimpseste\.runtime\operator-staging\doctor-geometry-20260920 -v:q` : réussi. `ProviderDoctor.exe` = 74 524 035 octets, SHA-256 `30CB7E3610186D31CD234720550A58FDDCE9CF3D60278E5A22E8BA48F38E579D`.

Après ce premier candidat, le doctor a été renforcé : il appelle `SpellCompiler.ValidatePlanJson` sur la réponse B avec les géométries et masques recalculés. Une réponse B au transport valide mais sémantiquement irrecevable produit `active_result=plan_rejected` et ses codes d'erreur, avec code de sortie 1. Le second `dotnet build` a réussi, 0 avertissement et 0 erreur. Un second publish single-file, distinct du premier, a réussi vers `E:\Palimpseste\.runtime\operator-staging\doctor-geometry-validated-20260920\ProviderDoctor.exe` : 74 524 035 octets, SHA-256 `210090E05E6978284C3C841141F10FE0D6D9B963D91B3E365A1FA693124479BC`.

À la fin de cette première étape de publication, le second candidat était
uniquement en staging. Le premier avait été installé et validé en mode local
sous `PalRuntimeSvc` ; le mode `plan` restait alors à exécuter. La publication
en staging n'avait remplacé aucun exécutable du runtime joueur.

## Installation et vérification ultérieures

Le second candidat, SHA-256
`210090E05E6978284C3C841141F10FE0D6D9B963D91B3E365A1FA693124479BC`,
a ensuite été installé comme `E:\PalimpsesteRuntime\bin\ProviderDoctor.exe`.
Le SHA-256 du binaire vivant a été recalculé et correspond à cette valeur.
Le script enfant installé `ProviderDoctor.Service.ps1` a pour SHA-256
`668AA8309A6488DF44BBB54DA8191722E5EF147D1DE330A2B8C850832C8A0114`.
Les deux fichiers appartiennent à `Administrateurs`, possèdent une ACL à
héritage désactivé et accordent `ReadAndExecute` à `PalRuntimeSvc` ; ces
propriétés ont été observées après installation. Le doctor en mode `local`
sous ce compte a quitté avec le code `0`, sans appel modèle. Son rapport
privé `doctor-local-geometry-validated-20260920.json` a pour SHA-256 vérifié
`EEF500FD305BFC2CF40656A0BCB3240CE36FA2FB4B78520DF3C5A5EFAC25E862`.

Le mode `plan` a ensuite repris A `1.3` figée sans nouvel appel A, recalculé
les géométries depuis A et l'encre réelle, appelé B `1.1` sous
`PalRuntimeSvc` et validé le plan. `RealProbe` l'a compilé en mémoire ; voir
la [preuve séparée de la reprise B](doctor-plan-b11-resolved-2026-09-20.md).
Le worker joueur est resté arrêté. Cette installation du doctor ne vaut ni
sort joueur publié ni validation humaine.
