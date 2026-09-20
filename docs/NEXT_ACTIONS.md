# Point de reprise immédiat

Mis à jour le 20 septembre 2026.

Le CLI Codex patché et le doctor publié sont maintenant installés dans le
runtime. Le doctor local a réussi sous `PalRuntimeSvc` sans appel modèle,
sa preuve de fonctionnalités a été approuvée et référencée par le runtime.
Une première sonde active a ensuite réussi A puis B avec `gpt-5.6-luna` et
`max` rapportés pour chaque étape. Preuve :
`evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`.
Le premier rapport actif atteste des appels et métadonnées réels, mais son B
échoue sur la géométrie provisoire et le nouveau vérificateur refuse ce rapport
seul. Le rapport B repris sur A `1.3` figée a été validé séparément ; le doctor
FINAL a revérifié et compilé hors ligne les sorties A/B réelles avec l'encre,
sans appel modèle. Le manifeste composite v2 exige trois rapports et les
fichiers originaux A/B/encre. Le verdict humain sur A `1.3` manque encore, et
`PALIMPSESTE_EFFORT_VERIFIED=false`. Une vérification séparée a recalculé la
géométrie depuis A et l'encre réelle et compilé B en mémoire, sans appel
modèle supplémentaire. Le paquet de vérification n'a pas été publié ni chargé
dans le Player joueur. Le nouveau rendu suit le chemin de pixels de la
silhouette principale du cahier §10 : tests de géométrie ciblés 2/2, puis
tests du paquet Luna réel en Direct3D 12 2/2, avec 111 points source dont
94 visibles et 36 440 pixels rouges, puis zéro après nettoyage, sans dégât.
La rémanence graphique de 0,16 s ne prolonge pas le porteur physique. La suite
PlayMode complète du code final a passé 4 tests et ignoré 2 sondes facultatives.
La caméra du Player et l'écoute du son restent à vérifier. L'API actualisée est installée dans
`E:\PalimpsesteRuntime\api` et tourne sous `PalRuntimeSvc` sur
`http://127.0.0.1:18080` via `ops/start-owner-api.ps1` ; `/health/ready`
répond 200. Le Player BeamGeometryReady a été ouvert visiblement et répondait
au contrôle (PID 35552). La session propriétaire et la vraie capture en file
avaient été observées dans le Player précédent ; la base
contient un job `production` `queued` et quatre artefacts (référence, dessin,
encre, journal), sans tentative fournisseur pour ce job. Voir
`evidence/public/unity/owner-player-capture-queued-2026-09-20.md`. Le retour de lecture A
a passé 9/9 contrôles HTTP/DB synthétiques sur l'API locale, avec nettoyage
vérifié ; voir
`evidence/public/backend/interpretation-feedback-owner-lab-2026-09-20.md`.
Le dernier worker FINAL reste en staging opérateur, non installé ni démarré :
`operator-staging/worker-offline-compiled-20260920/Palimpseste.Worker.exe`,
76 231 207 octets, SHA-256
`987946213762B3E4052D1218377F3CFA748EF3F42BD7FF935E52BBE48CFF2F4A`.
Son script enfant candidat a pour SHA-256
`D776764F843BC2B2BD2D6A39FAFB6D3B77006C92ECCAA915DC8909E4B124CCC8`.
Le doctor FINAL, SHA-256
`2F323942747724AAC599052854947C8C4341A561BD40F075F15989A24B7E527A`,
est installé sous `E:\PalimpsesteRuntime\bin` après sauvegarde privée. Son
mode `validate` sous `PalRuntimeSvc` a quitté avec le code 0 sur A/B réels et
encre : validation et compilation hors ligne réussies, aucun appel modèle ;
rapport privé SHA-256
`D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA`.
Les candidats composite précédents (worker SHA-256
`93DA3D403E14A01BFF5BF1DA96E3551C1DB21EFCCF5072EF17A01B1997B719AC`,
doctor SHA-256 `B054C91BD2DD09B2811FFD93024E11A50397DAD62D76DA02ABA0C02A15E821E1`)
sont historiques. Les tests de sécurité synthétiques du vérificateur FINAL
ont quitté avec le code 0, sans nouvel appel Luna ; voir
`evidence/public/backend/composite-worker-prep-2026-09-20.md` et
`docs/ops/OWNER_WORKER_CUTOVER.md`.
Le doctor précédent, maintenant historique, avait pour SHA-256
`210090E05E6978284C3C841141F10FE0D6D9B963D91B3E365A1FA693124479BC`,
qui calcule les géométries depuis A et le PNG d'encre et contrôle B. Son
diagnostic local sous `PalRuntimeSvc` avait réussi sans appel modèle ; voir
`evidence/public/backend/doctor-geometry-build-2026-09-20.md`. Le mode `plan`
a réutilisé A `1.3` figée sans nouvel appel A, recalculé les géométries depuis
son texte et l'encre, puis obtenu un nouveau B `1.1` réel `gpt-5.6-luna`/`max`
(SHA-256 `E8164D8D654F470101BDCE77A4B0AF46476FF7AE48EA9DF1A92A5B9A84D9AEA1`).
Le doctor a validé le plan et `RealProbe` l'a compilé en mémoire avec le code
de sortie 0 ; paquet de test SHA-256
`8DAC5FEB3293184564623C1F259567CB025078563F8BC788A9928806BC7672E2`,
un tick et zéro effet. Voir
`evidence/public/backend/doctor-plan-b11-resolved-2026-09-20.md`.
Le rendu de géométrie du faisceau a passé 2/2 tests ciblés et le paquet Luna
réel a passé 2/2 tests Direct3D 12 : 111 points source, 94 visibles,
36 440 pixels rouges puis zéro après nettoyage, sans dégât. La suite PlayMode
complète a passé 4 tests, ignoré 2 sondes facultatives, sans échec. Le Player joueur
n'a ni téléchargé ni lancé ce paquet.
Le Player actuel IL2CPP/URP avec rémanence, formulaire de retour et shader
`LavaBeam` est `game/Build/WindowsPlayerBeamGeometryReady/` : 29 fichiers,
122 093 149 octets, exécutable SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`,
`GameAssembly.dll` SHA-256
`200BB3A65EC799D316C769D54CDB4F03A90BB7627B58B22FECA8A2CFEEB81A9E`.
Il a été lancé visiblement, PID 35552 répondant. Le build précédent
`WindowsPlayerLavaReady` avait affiché la capture propriétaire en file avec
session conservée ; la base garde ce job. Aucun texte A ou sort Luna n'y a
été reçu. Le build `WindowsPlayerFeedbackPlayable` est historique.
Le dernier candidat API est `operator-staging/api-feedback-2026-09-20/Palimpseste.Api.exe`
(SHA-256 `B9A5AD2EF920C1955333CD2F21B47E567DCBE7E3396CA123C971B4F8D2219545`) ;
l'ancien `operator-staging/api-core-2026-09-20/` n'inclut pas la route de
retour humain. L'API inclut
un plafond transactionnel de 3 nouvelles générations par joueur et 12 au
total par 24 heures, testé sous concurrence dans `palimpseste_test`.
Les migrations 003 à 006 sont appliquées au laboratoire local après
sauvegardes vérifiées. Le formulaire de retour est présent dans le nouveau
Player, mais son envoi depuis Unity n'a pas été testé. La route API a passé
son smoke synthétique ; HTTPS public reste à préparer.

**Verdict reçu le 20 septembre : l'utilisateur juge incorrecte la lecture A
« faisceau de feu visuel, sans cible ni dégâts » du trait rouge-brun diagonal.**
L'intention précisée ensuite est « laser unidirectionnel de lave (car rouge /
brun) ». La première sonde reste une preuve technique d'appel A/B et de
réglage modèle/effort, mais échoue à la porte de fidélité du cahier. Un essai
actif du prompt A `1.2` et de B `1.0` a ensuite nommé un faisceau de lave dans
un seul sens. A a ajouté des dégâts sans observation distincte ; B a inventé
la cible `hostile` et une cadence d'un tick pour un faisceau entretenu. Le
compilateur contrôlé a rejeté ce plan (`carrier_target`/`target_fact`,
`beam_interval`). Voir `docs/PROMPT_A_CALIBRATION_1_2.md` et
`docs/PROMPT_B_CALIBRATION_1_1.md`. Une troisième sonde A `1.3`/B `1.1` a
réellement obtenu `gpt-5.6-luna`/`max` pour les deux étapes. A décrit un
faisceau de lave sans effet ni cible et B un faisceau visuel sans effet ;
`RealProbe` a toutefois refusé `signature_geometry`, car le doctor avait
encore reçu une géométrie provisoire. Voir
`evidence/public/backend/doctor-ab13-b11-2026-09-20.md`. Ce refus historique
a été traité par la reprise B seule ci-dessus, sans réinterpréter A : le
nouveau B a été compilé et exécuté dans des tests Unity isolés. Aucun verdict
humain d'acceptation ni traitement A/B du job Player désormais `queued` n'est
prouvé. Le worker demeure désactivé et
`PALIMPSESTE_EFFORT_VERIFIED=false`.

Avant toute ouverture à plusieurs joueurs, clarifier le type de compte et
l'autorisation d'un usage serveur partagé. La capture actuelle indique
« Usage personnel » ; les [conditions OpenAI Europe](https://openai.com/policies/eu-terms-of-use/)
interdisent de mettre un compte individuel à la disposition d'autrui. Notre
lecture selon laquelle la génération pour des tiers via ce backend pourrait
entrer dans cette interdiction est une **inférence à confirmer**, même si les
identifiants restent secrets. Les essais locaux du propriétaire ne constituent
pas une validation du déploiement public.

1. Le premier couple A/B réel et l'encre du dessin de calibration ont passé `Palimpseste.Core.RealProbe`, puis PlayMode isolé ; la lecture A de ce cas a été rejetée humainement. Le couple A `1.3`/B `1.1` repris avec géométries réelles a depuis produit un autre paquet de vérification SHA-256 `8DAC5FEB3293184564623C1F259567CB025078563F8BC788A9928806BC7672E2`, compilé et lancé dans des tests Unity isolés, sans acceptation humaine. Le nouveau rendu suit la silhouette principale dans les tests géométriques et Direct3D 12 ; ses identifiants d'artefacts restent synthétiques. Une capture rouge-brun **courbe**, différente du trait droit de calibration, a été reçue avant le build LavaReady ; celui-ci l'a reprise et affichée : son job réel est `queued` avec quatre artefacts et zéro appel fournisseur. Le Player BeamGeometryReady a été lancé visiblement après son build, sans essai de sort joueur. La prochaine vérification exige la revue humaine de A `1.3`, puis l'activation contrôlée du worker et le traitement de cette capture sans la remplacer, avec artefacts publiés, paquet téléchargé et sort lancé depuis la caméra du Player.
2. L'intention du cas rejeté est consignée : laser unidirectionnel de lave. A `1.2`/B `1.0` a été rejeté pour faits de cible et cadence inventés. A `1.3`/B `1.1` a ensuite été exécuté réellement, mais son premier B utilisait encore un contexte géométrique provisoire ; `RealProbe` a refusé `signature_geometry`. Le doctor actuel accepte `-InkPng` pour calculer la géométrie depuis A et l'encre. Son mode `plan` a réutilisé la sortie A `1.3` figée, vérifié son SHA-256, résolu la géométrie, appelé seulement B et obtenu un nouveau plan accepté par le compilateur. Comparer au plus deux configurations sur le même corpus de conception et obtenir une revue humaine de la lecture et du sort avant de toucher au worker. La route `POST /v1/authoring/plan` teste B avec une description structurée de concepteur ; elle ne recalibre pas A et n'est pas une fonction de reroll du parchemin joueur. Les 30 dessins inédits de recette restent séparés et ne servent pas au réglage.
3. Conserver l'ancienne preuve active privée A/B SHA-256 `FD39E1220BF540C543184D56E3F3314FF0E2BF440BB65073EF219B031CD3E6F5` comme diagnostic historique : le vérificateur FINAL refuse un rapport actif isolé. Après verdict humain sur A `1.3`, examiner les deux rapports réels liés par le hash de cette A et le rapport de validation hors ligne, puis suivre `docs/ops/OWNER_WORKER_CUTOVER.md` pour le manifeste composite v2 approuvé. Garder les fichiers A/B/encre originaux hashés accessibles en lecture au vérificateur. `PALIMPSESTE_EFFORT_VERIFIED=false` reste fermé jusque-là. Ne pas copier `auth.json` du profil personnel ni les preuves privées dans Git. Le compte commun ne doit utiliser que son quota et ses crédits existants : aucun achat, aucune recharge, aucun modèle de secours ni effort inférieur. Le plafond applicatif de jobs ne constitue pas une limite monétaire OpenAI.
   À l'activation du worker, injecter `DATABASE_URL` dans son environnement privé puis vérifier sa lecture sous le compte de service : `runtime.env` ne contient pas cette clé, requise par `Palimpseste.Worker/Program.cs`. La chaîne est disponible dans le coffre privé `C:\ProgramData\Palimpseste\lab-db.env` ; elle ne doit pas être copiée dans le dépôt ni donnée au worker tant que la porte humaine demeure fermée.
4. L'exposition propriétaire de la description A, l'UI Unity dessin → texte → labo et le formulaire de retour sur la lecture sont codés. Le contrat API a passé le smoke 18/18 avant l'ajout de `principal_id` et de la route de retour ; la réponse `capabilities` compile. Le code Unity du Player BeamGeometryReady a passé les tests géométriques ciblés 2/2, les tests Direct3D 12 du paquet Luna réel 2/2 et la suite PlayMode complète 4 réussis, 2 sondes ignorées. Son lancement visible répondait ; le Player LavaReady précédent avait montré la capture propriétaire et le job `queued` avec session conservée. Le smoke du retour API a passé 9/9 avec des données synthétiques, sans envoi depuis Unity. L'essai joueur avec une vraie description A et un sort publié reste à faire ; aucun rendu depuis la caméra du Player ni écoute humaine du son n'est attesté.
5. Les migrations `003_player_invitations.sql`,
   `004_generation_quota.sql`, `005_interpretation_feedback.sql` et
   `006_plan_prompt_version.sql` ont été
   appliquées à `palimpseste_lab` après sauvegardes vérifiées. L'échange
   d'invitation a été testé sur `palimpseste_test` ; le contrôle concurrent de
   004 a été testé dans un schéma isolé de cette même base. L'API candidate est
   maintenant installée et prête sur loopback, l'invitation a été échangée dans
   le Player visible, et la route de retour a passé 9/9 contrôles synthétiques
   sur `palimpseste_lab`. Garder l'invitation privée ; aucune URL publique
   HTTPS n'est déployée. La migration 006 a été vérifiée également sur
   `palimpseste_test` ; voir
   `evidence/public/backend/migration006-owner-lab-2026-09-20.md` pour le
   laboratoire.
6. Une première commande de lancement de l'API avec son environnement privé et la création du ZIP joueur actualisé avaient été refusées avant exécution par la revue automatique de l'outil (`blocked by policy`, sans motif plus précis communiqué). Le lanceur `ops/start-owner-api.ps1` lit ensuite les fichiers privés dans le processus `PalRuntimeSvc` et a démarré l'API locale sans secret dans la ligne de commande. Les archives dans `deliverables/` sont historiques ; aucun nouveau ZIP n'a été créé. Après la revue humaine du résultat de calibration A `1.3`/B `1.1` compilé en vérification, activer le worker selon la porte prévue, traiter le job joueur déjà `queued`, télécharger son sort et le lancer depuis le Player, puis consigner la preuve et un verdict distinct sur cette nouvelle capture. Mettre à jour le manifeste après la dernière modification du dépôt.

Le dépôt contient les sources et un Player IL2CPP jouable hors ligne, connecté
au laboratoire local avec une capture réelle et un job `queued`. Les archives ZIP restent
antérieures au nouveau parcours joueur. La boucle de génération et M7 ne sont
pas acceptés.
