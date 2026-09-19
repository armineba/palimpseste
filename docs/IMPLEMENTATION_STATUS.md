# État de réalisation et point de reprise

Mis à jour le 19 septembre 2026 après la passe Unity B07/B16/B26. Ce fichier est un journal de travail, pas une attestation de livraison.

## Échelle employée

`prévu` = aucune source livrée ; `implémenté` = source écrite ; `compilé` = outil de compilation exécuté avec succès ; `testé auto` = essai automatisé exécuté ; `Luna réel` = appels A/B observés avec les images et le compte de service ; `Unity exécuté` = scène et build lancés ; `accepté humainement` = verdict enregistré par un créateur. Un statut supérieur n'est jamais inféré d'un statut inférieur.

## Environnement observé

- Dossier de travail et dépôt Git initialisé : ce répertoire. Le dossier fourni était documentaire et ne contenait pas de projet Unity/backend ni de dépôt Git.
- SDK .NET 10.0.401 installé et observé ; `dotnet build Palimpseste.sln -c Release -v:q` réussi sans avertissement au 19 septembre 2026.
- Codex CLI `0.154.0-alpha.6.2` observé. `codex exec --help` expose `--model`, `--image`, `--json`, `--output-schema`, `--output-last-message`, `--ephemeral`, `--ignore-user-config`, `--ignore-rules`, `--sandbox`.
- Modèle demandé `gpt-5.6-luna`. La documentation officielle du modèle liste `max` comme effort de raisonnement pris en charge ; le worker demande désormais `max`. Disponibilité sur le compte de service, acceptation effective par cette version de Codex et modèle/effort retournés restent **à prouver par appel actif**. L'authentification Codex personnelle visible sur la machine n'est pas l'authentification d'un compte worker isolé.
- `ProviderDoctor local` exécuté sous `PalRuntimeSvc` sans appel modèle : CLI/flags parsés, identité et `CODEX_HOME` dédiés vérifiés, outils exposés observés désactivés via `features list`, preuve locale hashée configurée. `unified_exec` reste actif comme mécanisme interne du CLI. L'authentification dédiée manque ; le dernier diagnostic ne rapporte que `effort_not_verified` comme blocage de production. Voir `evidence/public/backend/provider-local-2026-09-19.json`.
- Unity 6000.3.24f1 complet, module Windows IL2CPP et UnityPackageManager présents à `C:\ProgramData\6000.3.24f1-x86_64\Editor\Unity.exe`. Une installation incomplète reste à `C:\ProgramData\6000.3.24f1\Editor\Unity.exe` ; ne pas l'utiliser.
- Licence Unity Personal observée. Import Unity réussi ; tests EditMode 7/7 et PlayMode 2/2, avec assertions sur six porteurs, reprise du journal, intégrité du cache et focus ; build Windows IL2CPP et lancement du lecteur depuis le ZIP extrait réussis sur cette machine. Archive finale Unity de 43 994 056 octets, SHA-256 `d75e4f24b6b5800e76b3c4ad3ec201f0c40a16ef947aa15447d8dc19e0991c51`. Voir `docs/UNITY_TEST_PROOF.md`.
- Le jeton client est saisi en mémoire pour la session. Le stockage dans un magasin Windows protégé demandé par le cahier §19 n'est pas implémenté ni testé sous IL2CPP.
- PostgreSQL 17 local tourne. Migration appliquée à `palimpseste_lab` et `palimpseste_test`; compte de base dédié. Aucun service worker de production/auth Codex dédié vérifié.

## Matrice exigences → sources → preuve → état

| Ticket | Sources principales | Preuve actuellement observée | État actuel |
|---|---|---|---|
| B01 | `game/`, `shared/`, `backend/` | build Windows IL2CPP réussi ; Player local lancé | compilé, exécuté partiellement |
| B02 | `shared/Palimpseste.Contracts`, `shared/Palimpseste.Core` | compilation .NET et smoke fixture | compilé, testé auto partiel |
| B03 | `contracts/human-review.schema.json`, `backend/Palimpseste.Api`, `tests/review_http_smoke.py` | six contrôles HTTP/DB sur revue synthétique : attestation absente et sort inconnu refusés, insertion unique, replay idempotent, conflit rejeté, nettoyage vérifié ; aucune revue humaine ni corpus de recette | testé auto partiel, porte humaine ouverte |
| B04 | `game/Assets/Palimpseste/Drawing` | canevas, trois brosses et transformation pointeur codés ; dessin réel à la souris capturé dans le lecteur ; équivalence entre cadences non mesurée | exécuté Unity partiel |
| B05 | `game/Assets/Palimpseste/Drawing`, `shared/Palimpseste.Core/PngCodec.cs` | PNG roundtrip fixture ; journal haché durable généré par le lecteur et accepté dans une capture HTTP réelle ; reprise simulée après checkpoint décalé et suffixe incomplet en EditMode, sans arrêt brutal du processus | testé auto et Unity partiel |
| B06 | `game/Assets/Palimpseste/Drawing` | engagement au premier trait, budget d'encre, fermeture et régions codés ; limites et fermeture partielle non couvertes exhaustivement | exécuté Unity partiel |
| B07 | `game/Assets/Palimpseste/Library` | EditMode : reprise d'événements durables au-delà du checkpoint, suffixe incomplet tronqué, événement complet altéré rejeté ; PlayMode : interruption d'un trait actif au focus et `up` durable ; aucune coupure brutale réelle ni Alt+Tab physique instrumentés | testé Unity partiel |
| B08 | `backend/Palimpseste.Api`, `backend/migrations/001_initial.sql`, `tests/api_http_smoke.py` | API Release compilée, migration appliquée ; 16 assertions HTTP/DB dont multipart, idempotence, composite, contrôle propriétaire et deux rejets négatifs réussis sur base test | testé auto partiel |
| B09 | `backend/Palimpseste.Worker/JobRepository.cs`, `tests/Palimpseste.Worker.DbSmoke` | smoke PostgreSQL isolé : claim, lease, fencing, nombre durable de réparations, tentative incertaine, incident ; panne contrôlée après A et pendant B avec description conservée, reprise et rejet des fences périmés | testé auto partiel |
| B10 | `backend/Palimpseste.Provider` | compilation et doctor local ; aucun appel A réel | compilé, Luna réel non exécuté |
| B11 | `game/`, `backend/Palimpseste.Api` | dessin à la souris dans le lecteur → référence/allocate/begin/capture multipart acceptés → job `queued`; pas encore d'appel Luna | testé auto partiel |
| B12 | `shared/Palimpseste.Core/GeometryResolver.cs` | smoke fixture PNG + masque à contours imbriqués et trou conservé | testé auto partiel |
| B13 | `backend/Palimpseste.Provider`, `backend/Palimpseste.Worker` | B codé ; aucun appel B réel | compilé, Luna réel non exécuté |
| B14 | `shared/Palimpseste.Core/SpellCompiler.cs` | rejets sémantiques dans smoke | testé auto partiel |
| B15 | `shared/Palimpseste.Core/SpellCompiler.cs` | borne ≥476 ticks fixture | testé auto partiel |
| B16 | `shared/Palimpseste.Core`, `backend/Palimpseste.Storage`, `game/Assets/Palimpseste/Library` | ZIP final extrait : fixture manuelle relue et lancée hors ligne ; EditMode : octet de masque altéré, artefact absent, marqueur SHA absent, version incompatible refusés puis restauration acceptée ; refus d'un cache altéré vu aussi dans le build IL2CPP direct antérieur à la retouche du bandeau | testé Unity partiel |
| B17 | `game/Assets/Palimpseste/SpellRuntime` | casts, annulation `ResetTargets` et extinction de colliders testés en PlayMode ; saturation longue non mesurée | testé Unity partiel |
| B18 | `game/Assets/Palimpseste/SpellRuntime` | projectile guidé : touche et impulsion ; projectile courbe compilé : touche, dégâts et brûlure ; murs fins, rebond/pénétration et filtres non exhaustifs | testé Unity partiel |
| B19 | `game/Assets/Palimpseste/SpellRuntime` | faisceau touche et inflige 6000 milli dégâts en PlayMode ; cinq lancers hors ligne auparavant, occlusion/chaîne non exhaustives | testé Unity partiel |
| B20 | `game/Assets/Palimpseste/SpellRuntime` | champ mouille une cible dans l'empreinte et épargne une petite cible dans un trou ; onde soigne un allié sans toucher l'hostile ; fronts rapides non couverts | testé Unity partiel |
| B21 | `game/Assets/Palimpseste/SpellRuntime` | barrière intercepte raycast, expire et désactive ses colliders ; piège ralentit hostile ; réarmement et interactions multiples ouverts | testé Unity partiel |
| B22 | `game/Assets/Palimpseste/SpellRuntime` | dommages, soin de 12000 milli, impulsion, brûlure, Mouillé et ralentissement affirmés en PlayMode ; cumul/réactions complets ouverts | testé Unity partiel |
| B23 | `game/Assets/Palimpseste/Scenes` | scène d'épreuve et contrôles de visée exécutés depuis le ZIP ; scénarios de recette complets ouverts | testé Unity partiel |
| B24 | `game/Assets/Palimpseste/Presentation`, `SpellRuntime` | décor URP, lumière, silhouettes, projectile/champ et effets de statut visibles dans le ZIP extrait ; revue de six porteurs sur dessins réels ouverte | exécuté Unity partiel |
| B25 | `game/Assets/Palimpseste/Presentation`, `Library` | audio procédural créé et clip de 3969 échantillons/22050 Hz affirmé en PlayMode ; écoute et revue humaine de paires de dessins non exécutées | testé technique partiel |
| B26 | `game/Assets/Palimpseste/Bootstrap`, `Scenes` | UI française et parcours dessiner→capture vus ; ZIP final extrait ouvert à 1024 × 768 et 1920 × 1080, captures publiques ; interruption du dessin au focus testée en PlayMode ; clavier/accessibilité et autres tailles non exhaustifs | exécuté Unity partiel |
| B27 | `backend/`, `ops/backup-lab.ps1`, `ops/restore-lab.ps1`, `ops/backup-runtime.ps1`, `ops/restore-runtime.ps1`, `evidence/public/backend/` | restauration à blanc labo : 4/4 artefacts DB et API GET ; scripts runtime éprouvés sur base synthétique distincte : dump, 10/10 fichiers hashés, 4 lignes DB, ACL service M ; snapshot privée de la capture Unity | testé auto partiel |
| B28 | `game/`, `backend/` | archive antérieure : instrumentation opt-in sur 77,68 s, 13 RSS, lecteur répondant, onze fenêtres de boucle de frames internes ; non remesuré sur le ZIP final, aucun Profiler Unity, FPS de présentation ni endurance contrôlée | observation partielle |
| B29 | `tests/`, `evidence/` | aucun dessin inédit → Luna → Unity exécuté | prévu |
| B30 | `docs/`, `game/Build/`, `deliverables/` | ZIP IL2CPP final de 43 994 056 octets construit, hashé et lancé après extraction locale ; manuel, installation et provenance des assets documentés ; installation autre machine et verdict humain non faits | build exécuté, livraison ouverte |
| L01 | `backend/Palimpseste.Provider/CodexSettings.cs` | effort `max` demandé ; compte/CLI isolés, fonctions d'outils exposées observées désactivées ; acceptation de l'effort par Luna non vérifiée | testé local partiel |
| L02 | `backend/Palimpseste.Provider/CodexProcessRunner.cs` | compilation .NET ; test de transport avec faux exécutable sans appel modèle ; divergence et absence des métadonnées modèle/effort refusées en fail-closed ; aucun processus Luna réel exécuté | testé transport partiel |
| L03 | `backend/Palimpseste.Provider/LunaCodexProvider.cs` | A code deux images distinctes en arguments, prompt et sortie privés archivés par tentative ; aucun appel multimodal Luna réel | compilé, Luna réel non exécuté |
| L04 | `backend/Palimpseste.Provider/LunaCodexProvider.cs`, `backend/Palimpseste.Worker` | B code la description figée et son hash, la banque géométrique et le schéma ; aucun appel B Luna réel ni comparaison de clauses de sortie réelle | compilé, Luna réel non exécuté |
| L05 | `contracts/codex`, `shared/Palimpseste.Core/ContractJson.cs` | validation locale fixture ; compatibilité transport non éprouvée | testé auto partiel |
| L06 | `backend/ProviderDoctor` | doctor local sous `PalRuntimeSvc` passé avec CLI/help/options et fonctions d'outils vérifiées ; doctor actif final bloqué avec `dedicated_auth_not_confirmed`, `model_calls_executed=false` (`evidence/public/backend/provider-active-blocked-2026-09-19.json`) | testé local partiel |
| L07 | `backend/Palimpseste.Worker`, `tests/Palimpseste.Worker.DbSmoke` | leases/fencing/10 appels, au plus deux réparations durables par étape et deux reprises du processus avant lancement ; smoke DB de panne contrôlée après A et pendant B, reprise et rejet des fences périmés réussi ; panne réelle du fournisseur ou du serveur DB non exécutée | testé auto partiel |
| L08 | `backend/Palimpseste.Provider`, `ops/` | arguments séparés, environnement enfant épuré, racines contrôlées, faux transport d'injection testé ; ACL source refusée au service, spec RX, fonctions d'outils exposées observées `false` sous compte service ; aucun modèle adversarial actif testé | testé local partiel, isolation à accepter |
| L09 | `ops/`, `game/Assets/Palimpseste/Editor` | build Unity lancé par outil de développement ; API joueur sans route de build ; service de génération distinct et bloqué avant preflight | exécuté partiel |
| L10 | `evidence/`, `game/Build/` | sortie hors ligne de deux fixtures manuelles vérifiée ; aucun essai réel A/B ni sort Luna dans Unity | testé fixture partiel |

M0 à M7 restent **ouverts**. Les portes de validation humaine restent ouvertes ; aucun statut `accepté humainement` n'est attribué. Les exemples et tests sur fixtures ne prouvent ni la fidélité d'interprétation ni le fonctionnement final dans Unity.

| Jalon | Réalisation technique observée | Porte encore ouverte |
|---|---|---|
| M0 | dépôt, contrats, API health et build Unity réels | arbitrages P01–P10 et périmètre non validés humainement |
| M1 | dessin réel, raster, journal et capture réseau ; reprise simulée et focus testés | essai humain de l'engagement au premier trait ; panne brutale complète |
| M2 | job issu du dessin en file, runner A codé et doctor local | authentification dédiée, appel multimodal A et accord humain sur sa lecture |
| M3 | géométrie, compilateur et paquets fixtures testés | vrai B sur description A, comparaison humaine clauses/règles |
| M4 | six porteurs et effets principaux testés en PlayMode | scénarios physiques exhaustifs et essai humain du comportement |
| M5 | rendu URP, audio procédural et bibliothèque hors ligne | écoute, dix paires et reconnaissance humaine des signatures |
| M6 | isolation locale, reprise DB, sauvegarde/restauration, cache hors ligne avec rejets de corruption/chargement partiel | panne fournisseur réelle, endurance/profiler, magasin Windows protégé pour le jeton et démonstration humaine |
| M7 | ZIPs exécutables Windows et preuves techniques | 30 dessins inédits via Luna, machine propre et verdict humain signé |

## Point de reprise technique

1. API/migration et smoke HTTP sont exécutés sur la base test. Pour une nouvelle recette, restaurer la vraie capture Unity depuis le dossier privé indiqué dans `evidence/public/backend/api-restauration-2026-09-19.md` dans une nouvelle base et un stockage vide ; ne pas rejouer à l'aveugle une tentative fournisseur incertaine.
2. Le smoke DB couvre maintenant la panne contrôlée après A et pendant B, la conservation de la description, la reprise avec fencing et l'arrêt d'une tentative `running` incertaine en `needs_operator`. Il reste à exécuter une coupure réelle du processus/DB et à rapprocher son coût et son résultat avant toute reprise automatique.
3. Le profil OS `PalRuntimeSvc`, ses ACL et le doctor local sont prêts. Les entrées privées du doctor actif, issues du vrai dessin Unity, sont sous `E:\PalimpsesteRuntime\artifacts\` : `reference.png`, `drawing.png`, `ink.png`, `geometry.json` ; voir `evidence/public/backend/doctor-inputs-2026-09-19.json` et le rapport privé `E:\PalimpsesteRuntime\evidence\doctor-inputs-20260919.json`. La géométrie du diagnostic utilise des requêtes provisoires et devra être recalculée à partir de la vraie description A dans le job de production. Authentifier le compte Codex dédié dans son `CODEX_HOME`, puis exécuter le doctor actif **explicitement** ; vérifier l'effort et l'absence d'outils d'exécution observables. Ne pas utiliser le profil personnel comme preuve runtime.
4. Le build IL2CPP final et les tests Unity ont été refaits après les correctifs de reprise/cache ; le ZIP extrait a été lancé hors ligne à 1280 × 720, 1024 × 768 et 1920 × 1080. Couvrir ensuite les scénarios manquants des porteurs/effets, une vraie coupure du lecteur, la charge, le clavier/accessibilité et le stockage Windows protégé du jeton. La mesure B28 publiée concerne une archive antérieure.
5. Produire corpus de dessins inédits, tests complets, sauvegarde/restauration, dossier de preuve et verdicts de créateurs ; ne marquer M7 complet qu'après cette recette.

Commandes déjà exécutées, avec résultat observé : `dotnet build Palimpseste.sln -c Release -v:q` (succès), `dotnet run --project tests/Palimpseste.Core.Smoke -c Release` (succès), `dotnet run --project tests/Palimpseste.Provider.Security -c Release` (succès du transport simulé), `dotnet run --project tests/Palimpseste.Worker.DbSmoke -c Release` sur `palimpseste_test` (succès), `python qa/validate_contracts.py` (59/59 contrôles documentaires), doctor local sous `PalRuntimeSvc` (succès local) et doctor actif (arrêt contrôlé avant tout appel modèle faute d'authentification dédiée). Les tests Unity et le build ont leurs propres logs/artefacts sous `game/Logs`, `deliverables/` et `docs/UNITY_TEST_PROOF.md`.
