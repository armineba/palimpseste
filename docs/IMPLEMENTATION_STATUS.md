# État de réalisation et point de reprise

Mis à jour le 20 septembre 2026 après la bascule du CLI, le diagnostic Luna A/B réel et le contrôle des limites de déploiement. Ce fichier est un journal de travail, pas une attestation de livraison.

**Point de reprise du 20 septembre.** Le lecteur IL2CPP/URP actuel avec
rémanence visuelle et formulaire de retour a été construit. L'API locale
`http://127.0.0.1:18080` tourne sous `PalRuntimeSvc`, lancée avec
`ops/start-owner-api.ps1` : `/health/ready` répond 200. Le Player visible a
accepté une invitation privée, puis a transmis une vraie capture propriétaire
au service. Le laboratoire montre un job `production` en état `queued` et
quatre artefacts (référence, dessin, encre, journal). Aucun appel A/B n'a été
exécuté pour ce job, car le worker reste fermé ; voir
`evidence/public/unity/owner-player-capture-queued-2026-09-20.md`. Le smoke HTTP
du retour de lecture A a passé 9/9 contrôles synthétiques sur cette API, avec
nettoyage vérifié ; voir
`evidence/public/backend/interpretation-feedback-owner-lab-2026-09-20.md`.
La boucle Luna complète dans le parcours joueur n'est pas livrée. Le worker
demeure arrêté.
**Verdict humain reçu dans la conversation le 20 septembre : la lecture A du
trait rouge-brun diagonal comme « faisceau de feu visuel, sans cible ni dégâts »
est incorrecte.** L'intention communiquée ensuite est « laser unidirectionnel
de lave (car rouge/brun) ». Le prompt A `1.2` a été essayé une fois avec les
deux images ; A a décrit un faisceau de lave unidirectionnel, mais a ajouté
des dégâts sans preuve visuelle distincte. B `1.0` a inventé la cible
`hostile` et demandé une cadence d'un tick pour un faisceau entretenu ; le
compilateur contrôlé a rejeté ce plan (`carrier_target`/`target_fact`,
`beam_interval`). Une troisième sonde réelle A `1.3`/B `1.1` a ensuite
rapporté `gpt-5.6-luna`/`max` pour les deux étapes. A a décrit « Faisceau de
lave » sans effet ni cible et B un faisceau visuel sans effet. Le contrôle
structurel a réussi, mais `RealProbe` a refusé `signature_geometry` : B avait
reçu une géométrie provisoire qui ne correspondait pas à cette A. Voir
`evidence/public/backend/doctor-ab13-b11-2026-09-20.md`. Ce refus historique
a été résolu en réutilisant A `1.3` figée sans nouvel appel A : le doctor
corrigé a recalculé les géométries depuis l'encre et obtenu un nouveau B
`1.1` réel, SHA-256
`E8164D8D654F470101BDCE77A4B0AF46476FF7AE48EA9DF1A92A5B9A84D9AEA1`.
Le doctor a validé ce plan et `RealProbe` l'a compilé en mémoire ; paquet de
test SHA-256
`8DAC5FEB3293184564623C1F259567CB025078563F8BC788A9928806BC7672E2`,
un tick, zéro effet. L'itération finale du rendu Unity a passé ses deux tests
PlayMode isolés 2/2, dont le contrôle Direct3D 12 ; voir
`evidence/public/backend/doctor-plan-b11-resolved-2026-09-20.md` et
`evidence/public/unity/lava-beam-ab13-b11-2026-09-20.md`. Aucun job joueur,
sort publié ou verdict humain positif n'est prouvé.
Le rejet initial n'était pas un échec du réglage `max` ou du test Unity isolé ;
il ne constitue pas une revue signée dans l'API.
Le CLI Codex patché (SHA-256 `8AA8BF5CC27C55331C29C1050CD666174076E3C83D9AE84C6AE2BD54D9C7A72D`)
et le doctor publié sont installés dans le runtime. Le doctor local sous
`PalRuntimeSvc` a réussi sans appel modèle. Une première sonde active a ensuite
obtenu A et B réels, avec `gpt-5.6-luna` et `max` rapportés pour les deux.
Voir `evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`.
Ce premier rapport actif contient des métadonnées réelles de modèle et d'effort,
mais son B a été rejeté pour géométrie provisoire ; le nouveau vérificateur de
production refuse ce rapport seul. La reprise B seule, liée à la même A figée,
a été validée séparément. Le doctor FINAL a ensuite revalidé et compilé hors
ligne les sorties A/B réelles avec l'encre, sans appel modèle. La preuve
composite v2 exige les trois rapports intégraux et les fichiers A/B/encre,
et le verdict humain sur A `1.3` manque encore ; le verrou
`PALIMPSESTE_EFFORT_VERIFIED=false` demeure fermé. L'API
contient un plafond transactionnel de 3 nouveaux jobs par joueur et 12 au
total sur 24 h, dont le test PostgreSQL concurrent est réussi ; voir
`docs/GENERATION_QUOTA.md`. L'exécutable API avec la route de retour de
lecture est publié dans le staging opérateur avec SHA-256
`B9A5AD2EF920C1955333CD2F21B47E567DCBE7E3396CA123C971B4F8D2219545`
dans `operator-staging/api-feedback-2026-09-20` et installé dans
`E:\PalimpsesteRuntime\api`. Cette API est démarrée sur loopback uniquement ;
aucune exposition HTTPS publique n'est en place.
Le candidat précédent `operator-staging/api-core-2026-09-20` SHA-256
`F01901EBF2D1A71F670FC609F8696F322D8DD0489379B8935BA322FCEF1C9F52`
ne contient pas cette route.
L'ancien `operator-staging/api` SHA-256
`66B253C16D695F06658C96DE38F53F69C26EAB13911FCD55C531B55B185C39EB`
est périmé pour ce correctif.
Le dernier worker candidat FINAL est publié en staging privé :
`worker-offline-compiled-20260920/Palimpseste.Worker.exe`, 76 231 207 octets,
SHA-256 `987946213762B3E4052D1218377F3CFA748EF3F42BD7FF935E52BBE48CFF2F4A`.
Son script enfant candidat a pour SHA-256
`D776764F843BC2B2BD2D6A39FAFB6D3B77006C92ECCAA915DC8909E4B124CCC8`.
Le doctor FINAL `doctor-offline-compiled-20260920/ProviderDoctor.exe`,
SHA-256 `2F323942747724AAC599052854947C8C4341A561BD40F075F15989A24B7E527A`,
a été installé dans `E:\PalimpsesteRuntime\bin` après sauvegarde privée. Son
mode `validate` sous `PalRuntimeSvc` sur les fichiers A/B réels et l'encre a
quitté avec le code 0 : validation et compilation réussies, aucun appel modèle,
stderr vide ; rapport privé SHA-256
`D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA`.
Le worker FINAL reste non installé et arrêté. Les candidats worker composite
précédent SHA-256 `93DA3D403E14A01BFF5BF1DA96E3551C1DB21EFCCF5072EF17A01B1997B719AC`
et doctor précédent SHA-256 `B054C91BD2DD09B2811FFD93024E11A50397DAD62D76DA02ABA0C02A15E821E1`
sont historiques. Les tests de sécurité synthétiques du vérificateur final
ont quitté avec le code 0, sans nouvel appel Luna ; voir
`evidence/public/backend/composite-worker-prep-2026-09-20.md` et
`docs/ops/OWNER_WORKER_CUTOVER.md`. Le worker n'a pas été démarré.
`DATABASE_URL` reste volontairement absent
du `runtime.env` fermé ; il doit être injecté depuis le coffre privé à
l'activation du worker après la revue humaine. Les migrations 003 à 006
ont été appliquées à `palimpseste_lab` après sauvegardes vérifiées ; la
migration 005 a créé une table à huit colonnes et 006 ajoute la version du
prompt B aux plans. La route de retour a passé
un smoke HTTP synthétique 9/9 sur l'API locale et ce laboratoire, sans appel
modèle. Voir `evidence/public/backend/lab-migrations-2026-09-20.md`,
`evidence/public/backend/interpretation-feedback-2026-09-20.md` et
`evidence/public/backend/interpretation-feedback-owner-lab-2026-09-20.md`.
Le doctor précédent, SHA-256
`210090E05E6978284C3C841141F10FE0D6D9B963D91B3E365A1FA693124479BC`,
avait calculé la géométrie depuis A et l'encre et validé B ; il est maintenant
historique après les installations des doctors suivants mentionnées ci-dessus.
Son contrôle local sous `PalRuntimeSvc` avait réussi sans appel modèle. Le mode
`plan` B seul sur A `1.3` figée a réussi avec compilation contrôlée, comme
consigné ci-dessus ; voir
`evidence/public/backend/doctor-geometry-build-2026-09-20.md`.
La capture de compte montre l'écran
d'activation de la recharge automatique, sans preuve exploitable dans le
service de son état réel. Aucun achat ni recharge n'a été déclenché par le
projet. Ce plafond applicatif n'est pas un plafond monétaire OpenAI.

**Condition de déploiement multi-joueurs.** La capture du compte indique
« Usage personnel ». Les [conditions OpenAI Europe](https://openai.com/policies/eu-terms-of-use/)
pour les comptes individuels disent qu'un compte ne doit pas être mis à la
disposition d'autrui. Il s'agit d'une **inférence contractuelle à clarifier** :
faire générer des sorts pour plusieurs joueurs depuis cet unique abonnement
personnel pourrait constituer cette mise à disposition, même si les identifiants
restent sur le serveur. Le type exact de compte et l'autorisation applicable
doivent être confirmés avant d'ouvrir l'API au public. Les essais locaux du
propriétaire et les tests techniques ci-dessous restent les faits observés ;
aucune ouverture multi-joueurs n'est revendiquée.

**Historique des diagnostics et limite actuelle :** le compte Codex partagé a été connecté à `PalRuntimeSvc` par code d'appareil dans son `CODEX_HOME` isolé. Les premières tentatives du doctor ont conduit à la correction du faux positif `model_calls_executed`, des 17 `const` sans `type` des schémas A/B, puis du manque de métadonnées modèle/effort du CLI standard. La première lecture A décrivait un faisceau teinté de feu ; l'auteur l'a rejetée. La sonde A `1.3` a ensuite décrit un faisceau de lave sans effet ni cible, et B `1.1` repris sur cette A figée et la géométrie réelle a produit un plan visuel sans effet. Le doctor FINAL a validé et compilé hors ligne les octets A/B réels avec l'encre, sans appel modèle ; ce paquet de contrôle garde des identifiants synthétiques et n'est pas publié. Une fixture courbe a passé les tests ciblés de géométrie Unity 2/2. Séparément, le paquet A `1.3`/B `1.1` issu des sorties Luna réelles a passé les tests Direct3D 12 2/2 du rendu sur la silhouette principale : 111 points source, 94 visibles, 36 440 pixels rouges puis zéro après nettoyage, sans dégât. Le Player Windows IL2CPP/URP actuel BeamGeometryReady est construit et a été lancé visiblement, PID 35552 répondant au contrôle. Le Player LavaReady précédent a affiché la capture réelle du propriétaire en file ; aucun appel Luna ni sort n'existe pour ce job. Aucun sort issu de ce plan n'a été publié ni exécuté par le parcours réseau joueur ou le Player ; aucune visibilité depuis sa caméra ni écoute humaine du son n'est attestée. L'API expose la description A validée au propriétaire (`description_artifact_id`), un échange d'invitation à usage unique et `principal_id` dans les capacités ; tests HTTP 18/18 et 4/4 sur la base de test avant le dernier champ, compilation Release 0/0 et QA documentaire 61/61 après. Unity a passé EditMode 11/11 sur un build antérieur et PlayMode complet 4 passés/2 facultatifs ignorés sur le code actuel. L'ancien Player a échangé une invitation privée et affiché un parchemin vierge. Une vraie capture courbe distincte du dessin droit de calibration a été reçue à 06:17, avant le build LavaReady ; ce Player précédent l'a reprise et affichait son job en file. Les ZIPs cités plus bas concernent **la version précédente**. Une première commande de démarrage d’API avec environnement privé et la création du nouveau ZIP avaient été refusées avant exécution par la revue automatique de l’outil (`blocked by policy`). Un lanceur lisant les fichiers privés dans le processus du service a ensuite démarré l’API locale sans secret sur la ligne de commande. Le worker et le parcours dessin → Luna → sort dans le Player restent non exécutés.

**Build actuel avec trajectoire issue de l'encre :**
`game/Build/WindowsPlayerBeamGeometryReady/` contient 29 fichiers et
122 093 149 octets. Unity 6000.3.24f1 IL2CPP/URP a réussi le build.
Les tests ciblés de géométrie ont passé 2/2. Le paquet Luna A `1.3`/B `1.1`
a passé les tests Direct3D 12 2/2 : 111 points source, 94 visibles et
36 440 pixels rouges, puis zéro après nettoyage, sans dégât. La suite PlayMode
complète a passé 4 tests, ignoré 2 sondes facultatives, sans échec.
Le shader URP `LavaBeam` est inclus dans
`resources.assets` SHA-256
`62F6544B03BD5E70F556ACE80EDC282E684073B5DE6E8C7CF0998E2C2D072F5F`.
`Palimpseste.exe` a pour SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`
et `GameAssembly.dll`
`200BB3A65EC799D316C769D54CDB4F03A90BB7627B58B22FECA8A2CFEEB81A9E`.
Le Player a été lancé en fenêtre visible, PID 35552 répondant au contrôle.
La session conservée et l'écran « Capture reçue / En file d'attente » ont été
observés sur le Player LavaReady précédent pour un parchemin réellement
transmis à l'API ; le job est toujours en file. Le formulaire de retour existe
dans ce build, sans envoi Unity observé. Le build `WindowsPlayerLavaReady`
reste historique. Voir
`evidence/public/unity/beam-geometry-luna-build-2026-09-20.md` et
`evidence/public/unity/owner-player-capture-queued-2026-09-20.md`.

## Échelle employée

`prévu` = aucune source livrée ; `implémenté` = source écrite ; `compilé` = outil de compilation exécuté avec succès ; `testé auto` = essai automatisé exécuté ; `Luna réel` = appels A/B observés avec les images et le compte de service ; `Unity exécuté` = scène et build lancés ; `accepté humainement` = verdict enregistré par un créateur. Un statut supérieur n'est jamais inféré d'un statut inférieur.

## Environnement observé

- Dossier de travail et dépôt Git initialisé : ce répertoire. Le dossier fourni était documentaire et ne contenait pas de projet Unity/backend ni de dépôt Git.
- SDK .NET 10.0.401 installé et observé ; `dotnet build Palimpseste.sln -c Release -v:q` réussi sans avertissement au 19 septembre 2026.
- Codex CLI `0.154.0-alpha.6.2` observé. `codex exec --help` expose `--model`, `--image`, `--json`, `--output-schema`, `--output-last-message`, `--ephemeral`, `--ignore-user-config`, `--ignore-rules`, `--sandbox`.
- Modèle demandé `gpt-5.6-luna`, effort `max`. Les deux valeurs ont été rapportées par A et B lors de trois sondes actives du CLI patché, puis par B seul dans le mode `plan` avec A figée. Ces constats ne valent pas acceptation du worker joueur ; la deuxième sonde a été rejetée pour cible/cadence, la troisième pour géométrie provisoire, et le B repris a été validé/compilé séparément avec géométries réelles.
- `ProviderDoctor local` exécuté sous `PalRuntimeSvc` sans appel modèle : CLI/flags parsés, outils exposés observés désactivés via `features list`, binaire CLI vivant SHA-256 `8AA8BF5CC27C55331C29C1050CD666174076E3C83D9AE84C6AE2BD54D9C7A72D` lié à la preuve features approuvée SHA-256 `DAD87BC114E80BEE70848C1C4FFDF83E042456554252AAE8ED53291278C1F3FE`. `codex login status` a confirmé ChatGPT sous ce profil ; le doctor local a réussi, avec `effort_not_verified` comme seule réserve de production avant approbation active. `unified_exec` reste actif comme mécanisme interne du CLI. Le doctor actif a exécuté A puis B avec succès ; voir `evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`.
- Unity 6000.3.24f1 complet, module Windows IL2CPP et UnityPackageManager présents à `C:\ProgramData\6000.3.24f1-x86_64\Editor\Unity.exe`. Une installation incomplète reste à `C:\ProgramData\6000.3.24f1\Editor\Unity.exe` ; ne pas l'utiliser.
- Licence Unity Personal observée. Import Unity réussi. Le code Unity inclus dans le build actuel `WindowsPlayerBeamGeometryReady` a passé les tests ciblés de géométrie 2/2, les tests Direct3D 12 du paquet Luna réel 2/2 et la suite PlayMode complète 4 réussis, 2 sondes facultatives ignorées, 0 échec. Le rendu suit 111 points source, dont 94 visibles, et compte 36 440 pixels rouges puis zéro après nettoyage ; voir `evidence/public/unity/beam-geometry-luna-build-2026-09-20.md`. Le Player Windows IL2CPP/URP contient 29 fichiers, 122 093 149 octets ; son `GameAssembly.dll` a pour SHA-256 `200BB3A65EC799D316C769D54CDB4F03A90BB7627B58B22FECA8A2CFEEB81A9E`. Son lancement visible a répondu au contrôle. Le Player LavaReady précédent a montré une capture propriétaire en file d'attente, sans traitement Luna ; le job reste en file. L'archive précédente de 44 000 288 octets, SHA-256 `343a585873220c1511c53c30d37412208fdbc5ad29845f6060c817e4833791c2`, reste historique. Voir `evidence/public/unity/owner-player-capture-queued-2026-09-20.md` et `docs/UNITY_TEST_PROOF.md`.
- Jeton client protégé dans Windows Credential Manager, associé à l'URL du service ; écriture/lecture/suppression et redémarrage du Player IL2CPP final testés sur ce compte Windows. `PlayerPrefs` ne conserve que l'URL. Autre compte/poste et coffre indisponible non testés.
- PostgreSQL 17 local tourne. Migrations 001–003 appliquées à `palimpseste_lab` et `palimpseste_test`; migration 004 appliquée à `palimpseste_lab` et testée dans un schéma isolé de `palimpseste_test` ; migrations 005 et 006 appliquées aux deux bases après sauvegarde vérifiée du laboratoire. Compte de base dédié. L'API du laboratoire fonctionne sur loopback et sa route de retour a passé 9/9 contrôles HTTP synthétiques. Le worker de production n'a pas été exécuté après connexion Codex ; l'effort Luna est attesté pour les sondes seulement et aucune lecture artistique de ce cas n'est acceptée.

## Matrice exigences → sources → preuve → état

| Ticket | Sources principales | Preuve actuellement observée | État actuel |
|---|---|---|---|
| B01 | `game/`, `shared/`, `backend/` | Player Windows IL2CPP/URP BeamGeometryReady construit et lancé ; session privée et vraie capture observées dans le Player précédent, job `queued` et quatre artefacts ; A/B réels et paquet testés séparément hors pipeline joueur | compilé, exécuté partiellement |
| B02 | `shared/Palimpseste.Contracts`, `shared/Palimpseste.Core` | compilation .NET et smoke fixture | compilé, testé auto partiel |
| B03 | `contracts/human-review.schema.json`, `backend/Palimpseste.Api`, `backend/migrations/002_review_access.sql`, `tests/review_http_smoke.py`, `tests/review_access_http_smoke.py` | route de revue liée au principal authentifié ; smoke HTTP/DB à deux comptes creator : délégation sort/cas exacte, soumission du second compte, lecture du sort toujours propriétaire, révocation et nettoyage ; rejet humain de la lecture A exprimé dans la conversation, sans revue signée dans l'API ni corpus de recette | testé auto partiel, fidélité A rejetée |
| B04 | `game/Assets/Palimpseste/Drawing` | canevas, trois brosses et transformation pointeur codés ; dessin réel à la souris capturé dans le lecteur ; équivalence entre cadences non mesurée | exécuté Unity partiel |
| B05 | `game/Assets/Palimpseste/Drawing`, `shared/Palimpseste.Core/PngCodec.cs` | PNG roundtrip fixture ; journal haché durable généré par le lecteur et accepté dans une capture HTTP réelle ; reprise simulée après checkpoint décalé et suffixe incomplet en EditMode, sans arrêt brutal du processus | testé auto et Unity partiel |
| B06 | `game/Assets/Palimpseste/Drawing` | engagement au premier trait, budget d'encre, fermeture et régions codés ; limites et fermeture partielle non couvertes exhaustivement | exécuté Unity partiel |
| B07 | `game/Assets/Palimpseste/Library` | EditMode : reprise d'événements durables au-delà du checkpoint, suffixe incomplet tronqué, événement complet altéré rejeté ; PlayMode : interruption d'un trait actif au focus et `up` durable ; aucune coupure brutale réelle ni Alt+Tab physique instrumentés | testé Unity partiel |
| B08 | `backend/Palimpseste.Api`, `backend/migrations/001_initial.sql`, `tests/api_http_smoke.py` | API Release installée sur loopback sous `PalRuntimeSvc`, `/health/ready` 200 ; invitation échangée et vraie capture propriétaire reçue avant le build actuel avec job `production` `queued` et quatre artefacts ; smoke HTTP/DB historique sur base test | testé auto et Player partiel |
| B09 | `backend/Palimpseste.Worker/JobRepository.cs`, `tests/Palimpseste.Worker.DbSmoke` | smoke PostgreSQL isolé : claim, lease, fencing, nombre durable de réparations, tentative incertaine, incident ; panne contrôlée après A et pendant B avec description conservée, reprise et rejet des fences périmés | testé auto partiel |
| B10 | `backend/Palimpseste.Provider` | doctor local et connexion ChatGPT dédiée réussis ; trois diagnostics réels A/B sous service, puis B seul sur A `1.3` figée, modèle `gpt-5.6-luna` et effort `max` rapportés ; refus provisoire `signature_geometry` résolu par géométrie issue de A et de l'encre, nouveau B validé/compilé ; worker arrêté | Luna réel en doctor, pipeline joueur fermé |
| B11 | `game/`, `backend/Palimpseste.Api` | Player BeamGeometryReady visible et répondant ; Player LavaReady précédent avec session propriétaire conservée a repris une capture réelle reçue auparavant par l'API, job `production` `queued`, référence/dessin/encre/journal présents ; aucun appel Luna pour ce job | trajet Player → file antérieur vérifié, nouveau build lancé, suite fermée |
| B12 | `shared/Palimpseste.Core/GeometryResolver.cs` | smoke fixture PNG avec trou conservé ; l'encre Unity réelle et A produisent `full.silhouette.0`, `ring.path.0` et un masque dans RealProbe | testé auto sur fixture et encre réelle, pipeline joueur ouvert |
| B13 | `backend/Palimpseste.Provider`, `backend/Palimpseste.Worker` | B `1.1` réel repris sur A `1.3` figée avec géométries recalculées, plan visuel sans effet validé/compilé ; paquet de test lancé dans le rendu Unity Direct3D 12 actuel 2/2 ; aucun job joueur | Luna B réel et paquet testé en PlayMode, pipeline joueur fermé |
| B14 | `shared/Palimpseste.Core/SpellCompiler.cs` | nouveau B `1.1` avec géométries réelles validé et compilé dans `RealProbe` (code 0), paquet de test SHA-256 `8DAC5FEB…7672E2`, un tick, zéro effet ; paquet lancé en PlayMode isolé, sans publication joueur | compilateur et Unity isolé testés, publication ouverte |
| B15 | `shared/Palimpseste.Core/SpellCompiler.cs` | borne ≥476 ticks fixture ; paquet de vérification du vrai B borné à une instance, un tick et zéro application d'effet | testé auto partiel |
| B16 | `shared/Palimpseste.Core`, `backend/Palimpseste.Storage`, `game/Assets/Palimpseste/Library` | archive avant ajout du coffre : fixture manuelle relue et lancée hors ligne ; ZIP actuel extrait et lecteur lancé trois fois ; EditMode final : octet de masque altéré, artefact absent, marqueur SHA absent, version incompatible refusés puis restauration acceptée ; refus d'un cache altéré vu aussi dans un build IL2CPP direct antérieur | testé Unity partiel |
| B17 | `game/Assets/Palimpseste/SpellRuntime` | casts, annulation `ResetTargets` et extinction de colliders testés en PlayMode ; saturation longue non mesurée | testé Unity partiel |
| B18 | `game/Assets/Palimpseste/SpellRuntime` | projectile guidé : touche et impulsion ; projectile courbe compilé : touche, dégâts et brûlure ; murs fins, rebond/pénétration et filtres non exhaustifs | testé Unity partiel |
| B19 | `game/Assets/Palimpseste/SpellRuntime` | fixture avec faisceau dommageable testée auparavant ; le vrai plan Luna sans effet a produit un `LineRenderer` de deux points et zéro dégât en PlayMode isolé, puis a expiré après un tick ; rémanence graphique vérifiée sans porteur actif ni collider ; occlusion/chaîne non exhaustives | testé Unity partiel |
| B20 | `game/Assets/Palimpseste/SpellRuntime` | champ mouille une cible dans l'empreinte et épargne une petite cible dans un trou ; onde soigne un allié sans toucher l'hostile ; fronts rapides non couverts | testé Unity partiel |
| B21 | `game/Assets/Palimpseste/SpellRuntime` | barrière intercepte raycast, expire et désactive ses colliders ; piège ralentit hostile ; réarmement et interactions multiples ouverts | testé Unity partiel |
| B22 | `game/Assets/Palimpseste/SpellRuntime` | dommages, soin de 12000 milli, impulsion, brûlure, Mouillé et ralentissement affirmés en PlayMode ; cumul/réactions complets ouverts | testé Unity partiel |
| B23 | `game/Assets/Palimpseste/Scenes` | scène d'épreuve et contrôles de visée exécutés depuis le ZIP ; scénarios de recette complets ouverts | testé Unity partiel |
| B24 | `game/Assets/Palimpseste/Presentation`, `SpellRuntime` | fixture de géométrie ciblée 2/2 ; séparément, paquet A `1.3`/B `1.1` réel avec shader `LavaBeam` embarqué : Direct3D 12 2/2, 111 points source dont 94 visibles, 36 440 pixels rouges puis zéro après nettoyage, sans dégât ; caméra du Player non vérifiée | rendu géométrique isolé testé, parcours joueur ouvert |
| B25 | `game/Assets/Palimpseste/Presentation`, `Library` | audio procédural et clip fixture affirmés en PlayMode ; vrai paquet Luna : `AudioSource` créé à la naissance, sans écoute humaine ; revue des paires de dessins non exécutée | testé technique partiel |
| B26 | `game/Assets/Palimpseste/Bootstrap`, `Scenes`, `Service/WindowsCredentialStore.cs` | UI française et parcours dessiner→capture vus ; deux tailles 1024 × 768 et 1920 × 1080 sur archive précédente, 1920 × 1080 sur ZIP final ; coffre Windows écrit/relu/effacé après redémarrages du Player extrait ; focus testé en PlayMode ; clavier/accessibilité et autres tailles non exhaustifs | exécuté Unity partiel |
| B27 | `backend/`, `ops/backup-lab.ps1`, `ops/restore-lab.ps1`, `ops/backup-runtime.ps1`, `ops/restore-runtime.ps1`, `evidence/public/backend/` | restauration à blanc labo : 4/4 artefacts DB et API GET ; scripts runtime éprouvés sur base synthétique distincte : dump, 10/10 fichiers hashés, 4 lignes DB, ACL service M ; snapshot privée de la capture Unity | testé auto partiel |
| B28 | `game/`, `backend/`, `evidence/public/unity/b28-final-endurance.md` | ZIP final SHA `343a5858…` : 662,734 s, 133 points Windows tous répondants, RSS +13 099 008 octets, privés +27 426 816 ; 13 clics périodiques, bursts 20/30 et deux resets sur fixture manuelle, deux instances actives au maximum ; 79 fenêtres de frames internes ; aucun Profiler, FPS de présentation, coût CPU p95 ni saturation forte | observation partielle |
| B29 | `tests/`, `evidence/` | A `1.3` réel réutilisé sans nouvel appel A, nouveau B `1.1` compilé et lancé en Unity Direct3D 12 isolé 2/2 ; capture réelle transmise avant le build LavaReady et reprise par celui-ci, job `queued` et quatre artefacts, sans A/B pour ce job ; BeamGeometryReady lancé visiblement sans sort joueur ; aucun parcours dessin → job → téléchargement → sort dans le Player | essais partiels, recette finale ouverte |
| B30 | `docs/`, `game/Build/`, `deliverables/` | copie jouable actuelle `game/Build/WindowsPlayerBeamGeometryReady/` : 29 fichiers, 122 093 149 octets, exe SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`, GameAssembly `200BB3A6…EEB81A9E`, shader embarqué ; capture propriétaire mise en file par le Player précédent ; ZIP antérieur historique, nouveau ZIP non créé ; installation autre machine et verdict final humain non faits | build actuel exécutable, livraison ouverte |
| L01 | `backend/Palimpseste.Provider/CodexSettings.cs` | effort `max` demandé, effort inférieur refusé ; A et B de la sonde rapportent `max` avec le CLI patché | effort réel attesté en doctor, approbation ouverte |
| L02 | `backend/Palimpseste.Provider/CodexProcessRunner.cs` | transport `codex exec` réel sous compte ChatGPT du service, A/B réussis ; environnement sans clé API, classification des crédits épuisés testée par faux CLI | transport réel en doctor, joueur non testé |
| L03 | `backend/Palimpseste.Provider/LunaCodexProvider.cs` | A `1.3` a transmis les deux images, rapporté `gpt-5.6-luna`/`max` et décrit « Faisceau de lave » sans effet ni cible ; réutilisé par hash dans le nouveau B ; verdict humain encore absent | appel A multimodal réel, fidélité non acceptée |
| L04 | `backend/Palimpseste.Provider/LunaCodexProvider.cs`, `backend/Palimpseste.Worker` | Après rejet du B `1.0` pour cible/cadence et du premier B `1.1` pour géométrie provisoire, le mode `plan` a réutilisé A `1.3` figée, appelé seulement B `1.1` avec géométries réelles et obtenu un plan validé/compilé dans `RealProbe` ; aucun job joueur | B réel compilé en vérification, intégration ouverte |
| L05 | `contracts/codex`, `shared/Palimpseste.Core/ContractJson.cs` | 17 `const` des schémas de transport typés ; 61/61 contrôles documentaires ; sorties A et B réelles conformes au schéma | contrats réels testés partiellement |
| L06 | `backend/ProviderDoctor` | doctor FINAL SHA-256 `2F323942747724AAC599052854947C8C4341A561BD40F075F15989A24B7E527A` installé ; mode `validate` sous `PalRuntimeSvc` sur A/B réels et encre : validation et compilation hors ligne réussies sans appel modèle, rapport SHA-256 `D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA` ; l'ancien rapport actif isolé est refusé ; preuve composite v2 non promue, `PALIMPSESTE_EFFORT_VERIFIED=false` faute de verdict humain | contrôle technique réussi en doctor, porte humaine fermée |
| L07 | `backend/Palimpseste.Worker`, `tests/Palimpseste.Worker.DbSmoke` | leases/fencing/10 appels, au plus deux réparations durables par étape et deux reprises du processus avant lancement ; smoke DB de panne contrôlée après A et pendant B, reprise et rejet des fences périmés réussi ; panne réelle du fournisseur ou du serveur DB non exécutée | testé auto partiel |
| L08 | `backend/Palimpseste.Provider`, `ops/` | arguments séparés, environnement enfant épuré, racines contrôlées, faux transport d'injection testé ; ACE source héritable complète et refus effectif sous service de lister la racine et d'ouvrir un source en lecture/écriture ; préflight ouvre quatre sentinelles ; spec RX et fonctions exposées observées `false` ; autres enfants, changement ACL ultérieur et modèle adversarial non éprouvés | testé local partiel, isolation à accepter |
| L09 | `ops/`, `game/Assets/Palimpseste/Editor` | build Unity lancé par outil de développement ; API joueur sans route de build ; service de génération distinct et bloqué avant preflight | exécuté partiel |
| L10 | `evidence/`, `game/Build/` | nouveau B `1.1` réel compilé depuis A `1.3` figée et géométries réelles, paquet lancé en Direct3D 12 isolé 2/2 avec trajectoire source ; Player BeamGeometryReady lancé visiblement, capture réelle en file dans l'API et affichée par le Player LavaReady précédent, sans A/B ni lancement de sort pour ce job | testé technique partiel, boucle joueur ouverte |

M0 à M7 restent **ouverts**. Les portes de validation humaine restent ouvertes ; aucun statut `accepté humainement` n'est attribué. Les exemples et tests sur fixtures ne prouvent ni la fidélité d'interprétation ni le fonctionnement final dans Unity.

| Jalon | Réalisation technique observée | Porte encore ouverte |
|---|---|---|
| M0 | dépôt, contrats, API health et build Unity réels | arbitrages P01–P10 et périmètre non validés humainement |
| M1 | dessin réel, raster, journal et capture réseau ; Player BeamGeometryReady lancé visiblement, capture propriétaire reçue et affichée par le Player précédent, job `queued`, quatre artefacts ; reprise simulée et focus testés | essai humain de l'engagement au premier trait ; panne brutale complète |
| M2 | job issu du dessin en file, runner A codé, doctor local et authentification dédiée réussis ; A `1.3` réel avec deux images, modèle/effort rapportés, lecture de faisceau de lave sans effet ni cible, réutilisée par hash pour B | verdict humain de la nouvelle lecture et essai dans le job joueur encore ouverts |
| M3 | géométrie, compilateur et paquets fixtures testés ; nouveau B `1.1` réel sur A `1.3` figée et géométries issues de l'encre, validation et compilation contrôlée réussies, paquet lancé dans le rendu Unity PlayMode actuel 2/2 | paquet joueur avec artefacts et provenance réels, Player connecté et comparaison humaine clauses/règles |
| M4 | six porteurs et effets principaux testés en PlayMode ; faisceau Luna sans effet lancé en PlayMode isolé | scénarios physiques exhaustifs et essai humain du comportement |
| M5 | rendu URP, audio procédural et bibliothèque hors ligne ; paquet Luna A `1.3`/B `1.1` lance un faisceau et crée un `AudioSource` en PlayMode ; trajectoire sur 111 points source dont 94 visibles, tests Direct3D 12 2/2, 36 440 pixels rouges puis zéro, sans dégât ; shader `LavaBeam` embarqué | visibilité depuis la caméra du Player, son écouté, dix paires et reconnaissance humaine des signatures |
| M6 | isolation locale avec probe effectif sous service, preuve locale liée au SHA-256 du CLI, A `1.3` et B `1.1` attestés modèle/effort en doctor avec géométrie réelle et validation, reprise DB, sauvegarde/restauration, cache hors ligne avec rejets de corruption/chargement partiel, jeton dans Credential Manager testé sur le lecteur IL2CPP | approbation humaine de la preuve active, panne fournisseur réelle, autre compte/poste Windows et démonstration humaine |
| M7 | Player Windows IL2CPP/URP BeamGeometryReady exécutable et lancé, API locale prête, capture propriétaire reçue et job `queued` avec quatre artefacts ; ZIPs précédents historiques | archive du Player actuel, A/B pour ce job, paquet publié/téléchargé/lancé hors ligne, 30 dessins inédits, machine propre et verdict humain signé ; M7 non accepté |

## Point de reprise technique

1. API/migration et smoke HTTP sont exécutés sur la base test. Pour une nouvelle recette, restaurer la vraie capture Unity depuis le dossier privé indiqué dans `evidence/public/backend/api-restauration-2026-09-19.md` dans une nouvelle base et un stockage vide ; ne pas rejouer à l'aveugle une tentative fournisseur incertaine.
2. Le smoke DB couvre maintenant la panne contrôlée après A et pendant B, la conservation de la description, la reprise avec fencing et l'arrêt d'une tentative `running` incertaine en `needs_operator`. Il reste à exécuter une coupure réelle du processus/DB et à rapprocher son coût et son résultat avant toute reprise automatique.
3. Le profil OS `PalRuntimeSvc`, ses ACL et le doctor local lié au SHA-256 du CLI sont prêts. Une sonde sous ce compte a constaté le refus de lister la source et d'ouvrir un fichier source en lecture/écriture ; le préflight exige l'ACE héritable complète et quatre sentinelles, sans couvrir tous les enfants. La `PSCredential` DPAPI privée dépend du profil opérateur Windows actuel et n'est pas dans le dépôt. La connexion ChatGPT dédiée a réussi par code d'appareil, sans copie du profil personnel. Les entrées privées du premier doctor actif, issues du vrai dessin Unity, sont sous `E:\PalimpsesteRuntime\artifacts` : `reference.png`, `drawing.png`, `ink.png` et l'ancien `geometry.json` provisoire ; voir `evidence/public/backend/doctor-inputs-2026-09-19.json`. Le doctor actuel reçoit `-InkPng` et calcule lui-même les géométries ; le mode `plan` reprend une A figée hashée et n'appelle que B. L'ancien argument `GeometryJson` est refusé. La géométrie du premier diagnostic utilisait des requêtes provisoires ; RealProbe l'a ensuite recalculée à partir de la vraie description A et de l'encre, puis a compilé B avec des identifiants synthétiques. Le job de production doit encore suivre ce chemin avec des artefacts réels. Les tentatives antérieures et leurs corrections sont consignées dans `evidence/public/backend/provider-device-auth-and-doctor-block-2026-09-19.md`. La première sonde du CLI patché a réussi A et B : voir `evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`. L'utilisateur a rejeté sa lecture artistique ; la seconde sonde A `1.2`/B `1.0` a été refusée à la compilation. La troisième sonde A `1.3`/B `1.1` a rapporté Luna/max mais échoué sur une géométrie provisoire. Le doctor actuel utilise `-InkPng` pour calculer la géométrie depuis A ; son mode `plan` a réutilisé A `1.3` figée sans nouvel appel A, puis validé/compilé un nouveau B `1.1` avec les géométries réelles. Garder le worker fermé jusqu'à la revue humaine et à la preuve du job joueur. Ne pas utiliser le profil personnel comme preuve runtime ni relancer un modèle sans contrôle du quota existant.
4. Le build IL2CPP final et les tests Unity ont été refaits après les correctifs de reprise/cache/coffre ; le ZIP extrait a été lancé trois fois à 1920 × 1080 pour vérifier le jeton protégé. Un essai hors ligne de la fixture manuelle et une endurance B28 de 662,734 s concernent désormais ce ZIP final, avec preuves dans `evidence/public/unity/b28-final-endurance.md`. Les captures de 1024 × 768 et la mesure courte B28 initiale concernent l'archive précédente. Couvrir ensuite les scénarios manquants des porteurs/effets, une vraie coupure du lecteur, une saturation plus forte avec Profiler, le clavier/accessibilité et un autre compte/poste Windows.
5. Produire corpus de dessins inédits, tests complets, sauvegarde/restauration, dossier de preuve et verdicts de créateurs ; ne marquer M7 complet qu'après cette recette.

Commandes déjà exécutées, avec résultat observé : `dotnet build Palimpseste.sln -c Release -v:q` (succès, 0 avertissement), `dotnet run --project tests/Palimpseste.Core.Smoke -c Release` (succès, y compris après correction des règles du compilateur), `dotnet run --project tests/Palimpseste.Provider.Security -c Release` (succès, y compris ACL partielle, preuve hashée et refus simulé de crédits épuisés), `dotnet run --project tests/Palimpseste.Worker.DbSmoke -c Release` sur `palimpseste_test` (succès), test concurrent de quota dans `palimpseste_test` (20 admissions, 3 validées), `python qa/validate_contracts.py` (61/61 contrôles documentaires), doctor local sous `PalRuntimeSvc` (succès sans appel modèle), anciennes tentatives A documentées, première sonde active A/B réussie sous le CLI patché, puis seconde sonde A `1.2`/B `1.0` techniquement achevée mais plan rejeté par le compilateur, troisième sonde A `1.3`/B `1.1` avec géométrie provisoire refusée `signature_geometry`, et reprise B seule sur A `1.3` figée avec géométries réelles, validation réussie et rapport privé SHA-256 `BFAFCA2DE085F60AF2A974C60D74CE02CAE58082E28E8A0A740B27EEB22E3787`. `dotnet run --project tests/Palimpseste.Core.RealProbe -- <A.json> <B.json> <ink.png>` a quitté avec le code 0 pour le premier plan, validant et compilant en mémoire ses vraies sorties A/B avec l'encre réelle. Unity PlayMode batch `-nographics` a passé 1/1 sur le premier paquet, puis un test graphique Direct3D 12 distinct a mesuré 13 616 pixels sur caméra isolée après expiration et zéro après nettoyage. Le nouveau paquet A `1.3`/B `1.1` a passé l’itération finale de rendu PlayMode 2/2, avec 34 239 pixels rouges puis zéro en Direct3D 12 isolé ; voir `evidence/public/unity/lava-beam-ab13-b11-2026-09-20.md`. Le Player LavaReady visible a repris une capture propriétaire reçue avant son build, maintenant en `queued` avec quatre artefacts et sans A/B ; voir `evidence/public/unity/owner-player-capture-queued-2026-09-20.md`. Aucun son n'a été écouté. Le nouveau contrat API de description A a été testé HTTP 18/18 ; l'échange d'invitation a été testé 4/4 sur la base de test. L'API locale a répondu 200 à `/health/ready`, le Player visible a affiché un parchemin vierge après invitation et la route de retour A a passé 9/9 contrôles synthétiques avec nettoyage. Les tests Unity et le build ont leurs propres logs/artefacts sous `game/Logs`, `deliverables/`, `docs/UNITY_TEST_PROOF.md` et les preuves PlayMode dédiées.

Validation Unity BeamGeometryReady ultérieure à la séquence historique ci-dessus : fixture de géométrie ciblée 2/2 ; paquet A `1.3`/B `1.1` réel en Direct3D 12 2/2 avec 111 points source, 94 visibles, 36 440 pixels rouges puis zéro après nettoyage et zéro dégât ; suite PlayMode complète 4 réussis, 2 sondes ignorées ; build Unity code 0 avec PALIMPSESTE_BUILD_OK ; Player canonique lancé visiblement et répondant, sans sort joueur. Voir evidence/public/unity/beam-geometry-luna-build-2026-09-20.md.
