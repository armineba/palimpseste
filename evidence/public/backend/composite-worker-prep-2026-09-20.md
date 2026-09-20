# Préparation de la preuve A/B composite — 20 septembre 2026

Le diagnostic actif A `1.3` / B `1.1` a produit une A réelle avec le modèle `gpt-5.6-luna` et l'effort rapporté `max`, puis un B sur une géométrie provisoire. Son rapport privé, SHA-256 `256E564490E1A0A7E5BC50FBA216F4DC29F40EF496C867E03F3BE14291349610`, indiquait `active_result=success`, mais le plan B a ensuite échoué dans `RealProbe` avec `signature_geometry`. Ce rapport **ne peut plus ouvrir seul** la porte de preuve d'effort : le vérificateur exige maintenant un marqueur explicite de validation et de compilation du plan, une géométrie issue de l'encre et les fichiers finaux originaux toujours présents avec leurs hashes.

La reprise B seule sur A figée a produit un autre rapport réel, SHA-256 `BFAFCA2DE085F60AF2A974C60D74CE02CAE58082E28E8A0A740B27EEB22E3787`. Son hash d'A réutilisée correspond exactement au hash de sortie A du premier diagnostic, `E54DEC5810E00AABD0FFCB8F152DF06468DD8505593B6718C5B806C46B3254B4`. Ce B a reçu les géométries résolues depuis l'encre et sa validation contrôlée a réussi. Les deux rapports désignent le même exécutable Codex et la même identité `PalRuntimeSvc`. Le premier B n'est jamais pris comme preuve du plan final.

Le doctor final a exécuté `validate` sous `PalRuntimeSvc` sur les fichiers A et B réels et `artifacts\ink.png`, **sans appel Luna**. La sortie privée `evidence\pending\doctor-validate-ab13-b11-20260920.json` a pour SHA-256 `D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA`. Le processus a quitté avec le code `0` ; le rapport indique `validation_status=success`, `compilation_status=success`, `model_calls_executed=false`, les hashes A/B/encre attendus et un paquet de compilation de contrôle SHA-256 `A0612E811E976C0E1552DD8C9BB4E1B62EF6317AD90360FA5A924000895CBF93`. La sortie stderr privée fait 0 octet. Cette compilation en mémoire utilise des identifiants synthétiques et ne publie aucun sort joueur.

`CodexSettings` accepte maintenant un manifeste composite **version 2** qui référence les copies intégrales des trois rapports par chemin et SHA-256. Il exige le même exécutable, service, modèle et effort, des appels réels A et B, la liaison par hash de l'A figée, la validation/compilation hors ligne et la source de géométrie résolue. Il relit les fichiers A et B originaux dans `attempts`, ainsi que l'encre, en rejetant absence, modification ou point de jonction. Ces trois fichiers privés doivent être conservés et sauvegardés. Le manifeste et les rapports doivent se trouver ensemble dans le dossier approuvé, inaccessible en écriture au service. Aucune copie n'y a encore été promue et `PALIMPSESTE_EFFORT_VERIFIED=false` reste fermé : l'acceptation humaine du dessin de calibration n'est pas enregistrée.

Le test `dotnet run --project tests/Palimpseste.Provider.Security/Palimpseste.Provider.Security.csproj -c Release` a quitté avec le code `0` après ajout de cas de preuve composite valide, hash de rapport erroné, A non liée, octets A/B modifiés, validation sans compilation, ancien rapport actif seul et rapport B seul. Il utilise des rapports synthétiques pour les tests de refus ; il ne constitue pas une exécution du job Player. Les publications .NET autoportantes finales ont quitté avec le code `0` :

| Candidat privé publié | Taille | SHA-256 |
| --- | ---: | --- |
| `worker-offline-compiled-20260920/Palimpseste.Worker.exe` | 76 231 207 octets | `987946213762B3E4052D1218377F3CFA748EF3F42BD7FF935E52BBE48CFF2F4A` |
| `doctor-offline-compiled-20260920/ProviderDoctor.exe` | 74 536 835 octets | `2F323942747724AAC599052854947C8C4341A561BD40F075F15989A24B7E527A` |

Les deux candidats sont dans `E:\Palimpseste\.runtime\operator-staging`, hérité Administrateurs/SYSTEM seulement. Leur publication, le test de sécurité et l'inspection des hashes n'ont lancé aucun nouvel appel Luna. Le doctor final est installé dans `E:\PalimpsesteRuntime\bin` avec les ACL de lecture/exécution du service ; le worker n'a pas été installé ni démarré, et le job réel capturé dans le Player reste en file. La [procédure de bascule](../../../docs/ops/OWNER_WORKER_CUTOVER.md) décrit les conditions restantes.

Le doctor composite antérieur a été installé dans `E:\PalimpsesteRuntime\bin`
après copie privée de l'ancien exécutable (SHA-256 de la sauvegarde
`210090E05E6978284C3C841141F10FE0D6D9B963D91B3E365A1FA693124479BC`).
Le fichier installé a conservé son
SHA-256 `B054C91BD2DD09B2811FFD93024E11A50397DAD62D76DA02ABA0C02A15E821E1` ;
ses droits sont Administrateurs/SYSTEM contrôle total et `PalRuntimeSvc`
lecture/exécution. `ProviderDoctor local` sous ce compte a quitté avec le code
`0`, sans appel modèle (`model_calls_executed=false`), et a encore demandé
`gpt-5.6-luna`/`max`. Le rapport privé a pour SHA-256
`4BDE6E6133A9EBE9678926BA44701867437AD95B9B69BDFD9725C4CB9299BB9B`.
Cette installation historique a été remplacée par le doctor final cité ci-dessus. Ni son diagnostic local ni la validation hors ligne ne changent le verrou `PALIMPSESTE_EFFORT_VERIFIED=false`.

Un diagnostic `local` supplémentaire du doctor final installé a quitté avec le code `0` sous `PalRuntimeSvc`, sans appel modèle et sans incident de test actif. Son rapport privé `doctor-local-82194c836d394593816d7f55e31a3090.json` a pour SHA-256 `A4B8C1C65467E7EBD4E13683476953B52F4DBD1431C10E11083EAD0D3C761384`. Il conserve `production_issues=effort_not_verified` puisque la porte reste fermée. Le binaire installé a bien le SHA-256 final `2F323942747724AAC599052854947C8C4341A561BD40F075F15989A24B7E527A` et son script enfant `052DF97B79EAB11025CA3BF8F2538A8F0B104A52ED2657BAEE2D0F7A45A27F1B` ; leurs ACL accordent au service seulement lecture/exécution.
