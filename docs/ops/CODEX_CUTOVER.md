# Bascule contrôlée de Codex tag + correctif

Cette procédure conserve la marche de bascule et de retour arrière. **La bascule CLI/doctor a été exécutée le 20 septembre 2026**, puis le doctor local et une première sonde active A/B ont réussi sous `PalRuntimeSvc` ; voir `evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`. L'utilisateur a jugé incorrecte la lecture artistique A de ce cas et a précisé attendre un laser unidirectionnel de lave. La seconde sonde A `1.2`/B `1.0` a inventé une cible `hostile` et une cadence invalide ; son plan a été rejeté. Une troisième sonde A `1.3`/B `1.1` a rapporté `gpt-5.6-luna` et `max` pour les deux appels, avec une description visuelle de faisceau de lave sans effet ni cible. Le contrôle structurel a passé, mais `RealProbe` a refusé `signature_geometry` car le doctor avait encore envoyé une géométrie provisoire. Voir `evidence/public/backend/doctor-ab13-b11-2026-09-20.md`. Ce premier B de la troisième sonde n'a pas été compilé ; la reprise B seule décrite ci-dessous a ensuite réussi avec la géométrie réelle. Le worker reste arrêté et `PALIMPSESTE_EFFORT_VERIFIED=false`. Le CLI provient du tag `rust-v0.154.0-alpha.6.2` (commit `b5bffd3ec4db487e7e3dec59663875b0ef7b72ca`) et du correctif local revu. Conserver commit, diff, tests et SHA-256 du `.exe` dans le dossier opérateur privé. La compilation a été faite dans `E:\Palimpseste\.runtime\codex-source-attestation`, avec la cible Cargo protégée `E:\Palimpseste\.runtime\codex-target-rust-v0.154.0-alpha.6.2`. Le compte `PalRuntimeSvc` n'a aucun accès à ces deux dossiers.

Les publications .NET candidates sont dans E:\Palimpseste\.runtime\operator-staging, sous ACL protégée Administrateurs/SYSTEM seulement. Le dernier worker candidat FINAL est `worker-offline-compiled-20260920/Palimpseste.Worker.exe`, 76 231 207 octets, SHA-256 `987946213762B3E4052D1218377F3CFA748EF3F42BD7FF935E52BBE48CFF2F4A` ; son script enfant candidat a pour SHA-256 `D776764F843BC2B2BD2D6A39FAFB6D3B77006C92ECCAA915DC8909E4B124CCC8`. Il est publié en privé, non installé et arrêté. Le précédent `worker-composite-20260920` SHA-256 `93DA3D403E14A01BFF5BF1DA96E3551C1DB21EFCCF5072EF17A01B1997B719AC` et `worker-ab13-b11-20260920` SHA-256 `C07BBEC13D0DB82B6311B8F5D76095F352B5FC1934C803444D30880F608D7547` sont historiques. Le candidat API avec route de retour a été installé séparément dans `E:\PalimpsesteRuntime\api`. Le compte `PalRuntimeSvc` ne peut pas lire le staging opérateur. Voir `evidence/public/backend/composite-worker-prep-2026-09-20.md` et `docs/ops/OWNER_WORKER_CUTOVER.md`.

**Candidat API actuel :** `operator-staging/api-feedback-2026-09-20/Palimpseste.Api.exe`,
SHA-256 `B9A5AD2EF920C1955333CD2F21B47E567DCBE7E3396CA123C971B4F8D2219545`.
Il ajoute le retour de lecture A ; le candidat `api-core-2026-09-20`
mentionné ci-dessus est historique. La migration 005 a été appliquée à
`palimpseste_lab` après sauvegarde vérifiée. Le candidat API est maintenant
installé dans `E:\PalimpsesteRuntime\api` et démarré sous `PalRuntimeSvc` sur
`http://127.0.0.1:18080` par `ops/start-owner-api.ps1` ; `/health/ready` a
répondu 200. Le Player actuel a échangé une invitation privée et affiché un
parchemin vierge. La route de retour A a passé un smoke HTTP synthétique 9/9,
sans appel modèle et avec nettoyage vérifié. Le worker n'est pas installé ni
démarré. Voir `evidence/public/backend/interpretation-feedback-owner-lab-2026-09-20.md`
et `evidence/public/unity/owner-player-live-2026-09-20.md`.

Le doctor géométrique précédent, SHA-256
`210090E05E6978284C3C841141F10FE0D6D9B963D91B3E365A1FA693124479BC`,
a recalculé la géométrie depuis A et le PNG d'encre, puis validé le plan B.
Le doctor composite SHA-256
`B054C91BD2DD09B2811FFD93024E11A50397DAD62D76DA02ABA0C02A15E821E1`
l'a remplacé historiquement. Le doctor FINAL, installé après sauvegarde privée
dans `E:\PalimpsesteRuntime\bin`, a pour SHA-256
`2F323942747724AAC599052854947C8C4341A561BD40F075F15989A24B7E527A` ;
son script enfant installé a pour SHA-256
`052DF97B79EAB11025CA3BF8F2538A8F0B104A52ED2657BAEE2D0F7A45A27F1B`.
Son mode `validate` sous `PalRuntimeSvc` sur les sorties A/B réelles et l'encre
a quitté avec le code 0 : validation et compilation réussies, aucun appel
modèle (`model_calls_executed=false`), stderr vide. Le rapport privé a pour
SHA-256 `D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA`.
Les ACL installées donnent lecture/exécution au service et contrôle total aux
Administrateurs/SYSTEM. Voir `evidence/public/backend/doctor-geometry-build-2026-09-20.md`
et `evidence/public/backend/composite-worker-prep-2026-09-20.md`.
Le mode `plan` a ensuite réutilisé A `1.3` figée (SHA-256
`E54DEC5810E00AABD0FFCB8F152DF06468DD8505593B6718C5B806C46B3254B4`)
sans nouvel appel A. B seul a rapporté `gpt-5.6-luna`/`max`, SHA-256
`E8164D8D654F470101BDCE77A4B0AF46476FF7AE48EA9DF1A92A5B9A84D9AEA1`,
avec les géométries réelles `full.silhouette.0` et `ring.path.0`. Le doctor
a validé le plan et `RealProbe` l'a compilé en mémoire, paquet de test SHA-256
`8DAC5FEB3293184564623C1F259567CB025078563F8BC788A9928806BC7672E2`,
un tick, zéro effet. Voir
`evidence/public/backend/doctor-plan-b11-resolved-2026-09-20.md`.
Le rendu BeamGeometryReady a passé les tests ciblés de géométrie 2/2 et les
tests Direct3D 12 du paquet Luna réel 2/2 : 111 points source dont 94
visibles, 36 440 pixels rouges puis zéro après nettoyage, sans dégât ; voir
`evidence/public/unity/beam-geometry-luna-build-2026-09-20.md`. Le paquet n'a pas
été publié ou lancé dans le Player joueur.

1. **Geler la génération.** Arrêter le worker et vérifier qu'aucun `codex.exe` issu de `E:\PalimpsesteRuntime\bin` ne tourne. Garder l'API en mode mise en file seulement; vérifier les jobs en attente avant tout redémarrage. Ne copier ni `auth.json`, ni `runtime.env`, ni aucun secret dans le dépôt ou l'archive. Lors de la bascule effectuée, le binaire précédent avait pour SHA-256 `2271526227B06CA13AB2B975B88546460FC61B2A29225B6DDA0FDC803024CCC9` ; le CLI vivant actuel a pour SHA-256 `8AA8BF5CC27C55331C29C1050CD666174076E3C83D9AE84C6AE2BD54D9C7A72D`.
2. **Sauvegarder et installer.** Dans un dossier opérateur sur E: dont les ACL sont protégées (Administrateurs/SYSTEM FullControl, `PalRuntimeSvc` refusé), copier seulement le `codex.exe` courant et son SDDL, puis noter leur SHA-256. Vérifier le SHA-256 du candidat construit et ses tests. Copier le candidat sous un nom temporaire dans `E:\PalimpsesteRuntime\bin`, vérifier ses octets, lui appliquer Administrateurs/SYSTEM FullControl et `PalRuntimeSvc` ReadAndExecute. Le dossier `bin` doit rester ReadAndExecute pour le service, sans DeleteChild. Avec le worker arrêté, déplacer l'ancien exécutable vers le dossier opérateur et renommer le candidat `bin\codex.exe` sur le même volume. Recalculer le SHA-256 et contrôler l'ACL effective sous le compte de service. Garder les exécutables compagnons de la version officielle en place sauf besoin prouvé par les tests.
3. **Installer le doctor publié avant tout diagnostic actif.** Avec le worker arrêté, vérifier le SHA-256 du candidat dans operator-staging\doctor, sauvegarder l'ancien runtime\bin\ProviderDoctor.exe et son SDDL dans le dossier opérateur protégé, puis copier le candidat dans runtime\bin. Vérifier ses octets et appliquer Administrateurs/SYSTEM FullControl, PalRuntimeSvc ReadAndExecute; confirmer sous le service que le binaire est exécutable mais non modifiable. Le doctor local et la sonde active des étapes suivantes doivent utiliser ce même nouvel exécutable. En cas de divergence de hash ou d'ACL, restaurer l'ancien doctor et garder le worker arrêté.

4. **Recréer la preuve locale.** Le SHA du nouveau binaire invalide l'ancienne preuve de fonctionnalités. Exécuter le `ProviderDoctor.exe` publié et testé **sous `PalRuntimeSvc`**, avec son `CODEX_HOME` dédié, en mode `local`; écrire la nouvelle sortie unique dans `E:\PalimpsesteRuntime\evidence\pending`. Ce mode ne lance aucun modèle. Vérifier identité, authentification ChatGPT, modèle demandé `gpt-5.6-luna`, effort demandé `max`, SHA CLI exact, options et fonctionnalités désactivées observées. Copier après revue la preuve dans `E:\PalimpsesteRuntime\approved-evidence` avec ACL Administrateurs/SYSTEM FullControl et service ReadAndExecute; calculer son hash. Modifier uniquement `PALIMPSESTE_RUNTIME_FEATURES_VERIFIED`, `PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_PATH` et `PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_SHA256` dans `runtime.env` avec l'opérateur, sans afficher les autres valeurs. `runtime.env` doit rester lecture seule pour le service. Exécuter de nouveau le doctor local et vérifier que sa seule réserve de production est `effort_not_verified`.

Le lanceur opérateur `ops/run-service-doctor.ps1` exécute le doctor sous ce compte à partir de la credential DPAPI privée. Le script `ops/doctor-service-child.ps1` est installé comme `E:\PalimpsesteRuntime\bin\ProviderDoctor.Service.ps1`, SHA-256 `052DF97B79EAB11025CA3BF8F2538A8F0B104A52ED2657BAEE2D0F7A45A27F1B`, avec Administrateurs/SYSTEM FullControl et service ReadAndExecute. La vérification locale du lanceur a quitté avec le code 0 sans appel modèle ; voir `evidence/public/backend/doctor-service-launcher-2026-09-20.md`. Pour `active`, fournir référence, dessin et PNG d'encre canonique sous `runtime\artifacts` avec `-InkPng` ; le doctor calcule la géométrie depuis la sortie A. Pour `plan`, fournir le PNG d'encre et une sortie A figée sous `artifacts` ou `attempts`, avec son SHA-256 attendu ; ce mode réutilise A et n'appelle que B. Le mode `validate` final reprend A et B figés avec leurs SHA-256 et le PNG d'encre, sans appel modèle. L'ancien argument `GeometryJson` est refusé. Garder le worker arrêté.
5. **Limiter chaque diagnostic actif au quota existant.** Avec les mêmes référence et dessin Unity privés 1024 × 1024 et le PNG d'encre canonique, lancer `ProviderDoctor active` une fois par configuration sous `PalRuntimeSvc`, sortie unique dans `evidence\pending`. A utilise les deux images ; B ne reçoit que les géométries recalculées depuis cette A et cette encre, et n'est lancé que si A et ses métadonnées passent. Aucune recharge, aucun achat, aucun autre modèle, aucun effort inférieur et aucun retry automatique. Exiger que A **et** B retournent le modèle effectif `gpt-5.6-luna` et l'effort effectif `max`, avec sorties JSON valides et hashes, usage et identité de service consignés. Les trois premières sondes utilisaient encore une géométrie provisoire. Le premier plan a été compilé séparément après recalcul, le deuxième a été rejeté pour cible et cadence inventées, le troisième A `1.3`/B `1.1` a échoué `signature_geometry` dans `RealProbe` malgré un transport A/B réussi. Le doctor corrigé et le mode `plan` ont ensuite recalculé la géométrie depuis A `1.3` figée et l'encre, appelé seulement B et validé/compilé le nouveau B. Cette compilation reste une preuve isolée, pas un job joueur.
6. **Recalibrer puis approuver la preuve composite v2.** La première lecture A a été rejetée par l'utilisateur. A `1.2`/B `1.0` a été rejeté pour cible et cadence inventées. La sonde A `1.3`/B `1.1` a décrit un faisceau de lave sans effet ni cible, mais son premier B a été refusé par `RealProbe` pour géométrie provisoire. Le doctor corrigé a réutilisé A `1.3` figée, recalculé la géométrie depuis l'encre, appelé B seul et validé/compilé le nouveau plan. Le doctor FINAL a ensuite exécuté `validate` sous `PalRuntimeSvc` sur les octets A/B réels et l'encre : sortie 0, validation et compilation réussies, `model_calls_executed=false`, rapport privé SHA-256 `D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA`. Les tests de sécurité synthétiques du vérificateur ont quitté avec le code 0, sans nouvel appel Luna. Le vérificateur refuse le rapport `active` seul malgré son ancien `active_result=success` ; le manifeste `provider_doctor_composite` version 2 doit lier trois rapports intégraux hashés, la même identité service et CLI, le modèle/effort, la liaison à A figée, la validation/compilation hors ligne et les fichiers A/B/encre originaux encore présents avec leurs hashes. Le résultat A `1.3` et le sort attendent toujours le verdict humain. Ne promouvoir aucune preuve dans `approved-evidence` avant cette revue ; `PALIMPSESTE_EFFORT_VERIFIED=false` reste fermé. Après la revue seulement, suivre `docs/ops/OWNER_WORKER_CUTOVER.md` pour les copies, le manifeste v2, les ACL et les trois champs `PALIMPSESTE_EFFORT_EVIDENCE_PATH`, `PALIMPSESTE_EFFORT_EVIDENCE_SHA256`, `PALIMPSESTE_EFFORT_VERIFIED=true`. Le worker reste arrêté jusqu'à son installation contrôlée. Conserver sorties, encre, tentatives et secrets hors Git. Le rendu BeamGeometryReady suit la silhouette principale dans les tests isolés ; voir `evidence/public/unity/beam-geometry-luna-build-2026-09-20.md`. Cela ne prouve pas la fidélité du sort dans le Player.

7. **Installer puis démarrer le worker.** Après acceptation humaine de la preuve A/B composite v2 et du préflight, vérifier le SHA-256 `987946213762B3E4052D1218377F3CFA748EF3F42BD7FF935E52BBE48CFF2F4A` du candidat `operator-staging\worker-offline-compiled-20260920\Palimpseste.Worker.exe` (76 231 207 octets) et le SHA-256 `D776764F843BC2B2BD2D6A39FAFB6D3B77006C92ECCAA915DC8909E4B124CCC8` de son script enfant. La migration `006_plan_prompt_version.sql` est appliquée à `palimpseste_lab` après sauvegarde ; voir `evidence/public/backend/migration006-owner-lab-2026-09-20.md`. Suivre `docs/ops/OWNER_WORKER_CUTOVER.md` pour sauvegarder l'ancien binaire et son ACL, installer le candidat et le lanceur, contrôler les droits et le verrou avant tout démarrage. Le job Player déjà `queued` sera acquis au lancement et peut provoquer un véritable appel fournisseur. La clé `DATABASE_URL` reste absente de `runtime.env` pendant que la porte humaine est fermée ; elle sera lue par l'enfant sous le compte de service depuis le coffre privé au lancement autorisé. En cas d'échec, arrêter le worker et restaurer le binaire sauvegardé s'il existait.

L'API est déployée séparément du doctor et du worker. Au 20 septembre 2026,
le candidat avec plafond de génération, Core corrigé et retour de lecture A
est installé dans `E:\PalimpsesteRuntime\api` et lancé localement avec
`ops/start-owner-api.ps1`. Le lanceur charge les fichiers privés dans le
processus de `PalRuntimeSvc` sans exposer la chaîne de base dans la commande.
Une première commande de lancement avec environnement privé avait été refusée
avant exécution par la revue automatique (`blocked by policy`, sans motif
détaillé) ; le lanceur sûr a ensuite réussi. Le Player visible a atteint la
bibliothèque et un parchemin vierge. Le smoke HTTP de retour A a passé 9/9 sur
des lignes synthétiques, sans envoi Unity ni appel modèle. HTTPS public,
worker et boucle joueur de génération ne sont pas déployés ; M7 reste non
accepté. Une preuve doctor active ne vaut ni validation du sort dans le Player
ni ouverture publique.

**Porte d'ouverture publique.** La capture du compte utilisé affiche
« Usage personnel ». Les [conditions OpenAI Europe](https://openai.com/policies/eu-terms-of-use/)
indiquent qu'un compte individuel ne doit pas être mis à disposition d'autrui.
L'idée qu'un backend multi-joueurs sur cet abonnement commun puisse tomber sous
cette clause est une **inférence contractuelle**, pas un verdict juridique
définitif. Confirmer le type de compte et l'autorisation d'un service partagé
avant d'activer l'API pour des tiers. La sonde A/B du propriétaire et le
test Unity isolé restent des validations techniques locales.

**Retour arrière.** À toute erreur de hash, ACL, doctor ou sortie A/B, garder le worker arrêté et `PALIMPSESTE_EFFORT_VERIFIED=false`. Restaurer les exécutables Codex, ProviderDoctor et Worker effectivement remplacés depuis leurs sauvegardes opérateur, vérifier leurs SHA-256 et ACL ReadAndExecute, puis restaurer uniquement les anciennes clés de preuve de fonctionnalités (chemin/hash) dans `runtime.env`. Ne réutiliser aucune preuve active d'un autre SHA CLI. Relancer le doctor local gratuit et consigner son résultat. Ne pas relancer automatiquement A/B. Les sauvegardes portent sur le binaire et les ACL seulement; elles ne contiennent pas de profil Codex ni de secrets.
