# État de réalisation et point de reprise

Mis à jour le 20 septembre 2026 après la bascule du CLI, le diagnostic Luna A/B réel et le contrôle des limites de déploiement. Ce fichier est un journal de travail, pas une attestation de livraison.

**Point de reprise du 20 septembre.** Le lecteur IL2CPP/URP actuel avec
rémanence visuelle et formulaire de retour a été construit et lancé hors ligne,
mais la boucle Luna
complète dans le parcours joueur n'est pas livrée. Le worker demeure arrêté.
**Verdict humain reçu dans la conversation le 20 septembre : la lecture A du
trait rouge-brun diagonal comme « faisceau de feu visuel, sans cible ni dégâts »
est incorrecte.** Il s'agit d'un rejet de fidélité artistique pour ce cas,
pas d'un échec des schémas, du réglage `max` ou du test Unity isolé. L'intention
attendue n'a pas encore été enregistrée ; aucun nouveau prompt ni appel Luna
recalibré n'est prétendu. Ce verdict n'est pas une revue signée dans l'API.
Le CLI Codex patché (SHA-256 `8AA8BF5CC27C55331C29C1050CD666174076E3C83D9AE84C6AE2BD54D9C7A72D`)
et le doctor publié sont installés dans le runtime. Le doctor local sous
`PalRuntimeSvc` a réussi sans appel modèle. Une seule sonde active a ensuite
obtenu A et B réels, avec `gpt-5.6-luna` et `max` rapportés pour les deux.
Voir `evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`.
La preuve active est techniquement valide pour modèle/effort, mais son résultat
artistique a été rejeté ; le verrou
`PALIMPSESTE_EFFORT_VERIFIED=false` demeure fermé. L'API
contient un plafond transactionnel de 3 nouveaux jobs par joueur et 12 au
total sur 24 h, dont le test PostgreSQL concurrent est réussi ; voir
`docs/GENERATION_QUOTA.md`. L'exécutable API avec la route de retour de
lecture est publié dans le staging opérateur avec SHA-256
`B9A5AD2EF920C1955333CD2F21B47E567DCBE7E3396CA123C971B4F8D2219545`
dans `operator-staging/api-feedback-2026-09-20`, sans installation ni démarrage.
Le candidat précédent `operator-staging/api-core-2026-09-20` SHA-256
`F01901EBF2D1A71F670FC609F8696F322D8DD0489379B8935BA322FCEF1C9F52`
ne contient pas cette route.
L'ancien `operator-staging/api` SHA-256
`66B253C16D695F06658C96DE38F53F69C26EAB13911FCD55C531B55B185C39EB`
est périmé pour ce correctif.
Le worker contenant le compilateur corrigé a été republié en staging privé
(76 223 015 octets, SHA-256
`D04A81E54EF0FBFE8FFFCE6A121DABA1DCE0FB2C16F7B163CCF3EA1EA0E08CAA`) ;
il n'a pas été installé ni démarré. `DATABASE_URL` reste volontairement absent
du `runtime.env` fermé ; il doit être injecté depuis le coffre privé à
l'activation du worker après la revue humaine. Les migrations 003, 004 et 005
ont été appliquées à `palimpseste_lab` après sauvegardes vérifiées ; la
migration 005 a créé une table à huit colonnes. L'API actualisée n'y est pas
installée. Voir `evidence/public/backend/lab-migrations-2026-09-20.md` et
`evidence/public/backend/interpretation-feedback-2026-09-20.md`. La capture de compte montre l'écran
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

**Historique et limite actuelle :** le compte Codex partagé a été connecté à `PalRuntimeSvc` par code d'appareil dans son `CODEX_HOME` isolé. Les premières tentatives du doctor ont conduit à la correction du faux positif `model_calls_executed`, des 17 `const` sans `type` des schémas A/B, puis du manque de métadonnées modèle/effort du CLI standard. Après installation du CLI patché, A et B ont réussi dans une sonde indépendante du worker. A décrit un faisceau rectiligne teinté de feu, B le traduit en faisceau visuel sans effet. Une vérification séparée a recalculé la géométrie à partir de A et de l'encre réelle, puis compilé B avec succès en mémoire ; les identifiants d'artefacts et la provenance de ce paquet sont synthétiques. Ce paquet a été chargé et lancé dans un test Unity PlayMode isolé `-nographics` (1/1 passé, faisceau deux points, source audio, zéro dégât, expiration). Une rémanence graphique de 0,16 s après l'expiration logique a été vérifiée sans prolonger physique ou effet ; un test Direct3D 12 a mesuré 13 616 pixels rouges sur `RenderTexture` isolée, puis zéro après nettoyage. Le nouveau Player Windows IL2CPP/URP avec rémanence a été construit et lancé hors ligne 12 s, répondant, sans exception relevée ; exécutable SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`. Aucun sort issu de ce plan n'a été publié ni exécuté par le parcours réseau joueur ou le Player ; aucune visibilité depuis sa caméra ni écoute humaine du son n'est attestée. L'API expose la description A validée au propriétaire (`description_artifact_id`), un échange d'invitation à usage unique et `principal_id` dans les capacités ; tests HTTP 18/18 et 4/4 sur la base de test avant le dernier champ, compilation Release 0/0 et QA documentaire 61/61 après. Unity a passé EditMode 11/11 sur le build antérieur, PlayMode standard 2/2 sur le code actuel ; le Player précédent avait affiché invitation et bibliothèque vide. Les ZIPs et hash cités plus bas concernent **la version précédente**. La création du nouveau ZIP et un démarrage supplémentaire d'API test avec environnement privé ont été refusés avant exécution par la revue automatique de l'outil (`blocked by policy`) ; aucun smoke du Player actuel connecté à cette API n'est revendiqué.

**Dernier build, après ajout du retour :** `game/Build/WindowsPlayerFeedbackPlayable/`
contient 29 fichiers et 122 074 957 octets. Unity IL2CPP/URP 6000.3.24f1 a
réussi le build, EditMode 11/11 et PlayMode 2 réussis, 0 échec, 2 sondes
privées ignorées. Le Player a été lancé caché et hors ligne 12 secondes,
répondant, avec D3D12 et PhysX. Son `GameAssembly.dll` SHA-256
`2EE47CF35B6EC20C6BD4B9D29E6C5220A0659E192D51511DC5A99F0736B3F1F2`
diffère du build précédent ; `Palimpseste.exe` conserve le même SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`.
Le formulaire est dans ce build, mais ni son affichage humain ni son envoi à
l'API ne sont prouvés. Voir
`evidence/public/unity/feedback-player-build-2026-09-20.md`.

## Échelle employée

`prévu` = aucune source livrée ; `implémenté` = source écrite ; `compilé` = outil de compilation exécuté avec succès ; `testé auto` = essai automatisé exécuté ; `Luna réel` = appels A/B observés avec les images et le compte de service ; `Unity exécuté` = scène et build lancés ; `accepté humainement` = verdict enregistré par un créateur. Un statut supérieur n'est jamais inféré d'un statut inférieur.

## Environnement observé

- Dossier de travail et dépôt Git initialisé : ce répertoire. Le dossier fourni était documentaire et ne contenait pas de projet Unity/backend ni de dépôt Git.
- SDK .NET 10.0.401 installé et observé ; `dotnet build Palimpseste.sln -c Release -v:q` réussi sans avertissement au 19 septembre 2026.
- Codex CLI `0.154.0-alpha.6.2` observé. `codex exec --help` expose `--model`, `--image`, `--json`, `--output-schema`, `--output-last-message`, `--ephemeral`, `--ignore-user-config`, `--ignore-rules`, `--sandbox`.
- Modèle demandé `gpt-5.6-luna`, effort `max`. Les deux valeurs ont été rapportées par A et B lors de l'unique sonde active du CLI patché. Ce constat porte sur cette sonde et ne vaut pas acceptation du worker joueur.
- `ProviderDoctor local` exécuté sous `PalRuntimeSvc` sans appel modèle : CLI/flags parsés, outils exposés observés désactivés via `features list`, binaire CLI vivant SHA-256 `8AA8BF5CC27C55331C29C1050CD666174076E3C83D9AE84C6AE2BD54D9C7A72D` lié à la preuve features approuvée SHA-256 `DAD87BC114E80BEE70848C1C4FFDF83E042456554252AAE8ED53291278C1F3FE`. `codex login status` a confirmé ChatGPT sous ce profil ; le doctor local a réussi, avec `effort_not_verified` comme seule réserve de production avant approbation active. `unified_exec` reste actif comme mécanisme interne du CLI. Le doctor actif a exécuté A puis B avec succès ; voir `evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`.
- Unity 6000.3.24f1 complet, module Windows IL2CPP et UnityPackageManager présents à `C:\ProgramData\6000.3.24f1-x86_64\Editor\Unity.exe`. Une installation incomplète reste à `C:\ProgramData\6000.3.24f1\Editor\Unity.exe` ; ne pas l'utiliser.
- Licence Unity Personal observée. Import Unity réussi. Le dernier build avec formulaire a passé EditMode 11/11 et PlayMode standard 2/2 ; deux sondes privées facultatives ont été ignorées. La sonde PlayMode du vrai paquet A/B avait passé 1/1 en batch `-nographics` et le contrôle Direct3D 12 isolé 1/1 ; voir `evidence/public/unity/real-luna-playmode-2026-09-20.md` et `evidence/public/unity/real-luna-afterimage-2026-09-20.md`. Le Player actuel Windows IL2CPP/URP jouable hors ligne contient 29 fichiers, 122 074 957 octets ; son `GameAssembly.dll` a pour SHA-256 `2EE47CF35B6EC20C6BD4B9D29E6C5220A0659E192D51511DC5A99F0736B3F1F2`. Son lancement caché de 12 s a confirmé un processus répondant, D3D12 et PhysX, sans tester l'interface à l'écran. L'archive précédente de 44 000 288 octets, SHA-256 `343a585873220c1511c53c30d37412208fdbc5ad29845f6060c817e4833791c2`, est historique. Voir `evidence/public/unity/feedback-player-build-2026-09-20.md` et `docs/UNITY_TEST_PROOF.md`.
- Jeton client protégé dans Windows Credential Manager, associé à l'URL du service ; écriture/lecture/suppression et redémarrage du Player IL2CPP final testés sur ce compte Windows. `PlayerPrefs` ne conserve que l'URL. Autre compte/poste et coffre indisponible non testés.
- PostgreSQL 17 local tourne. Migrations 001–003 appliquées à `palimpseste_lab` et `palimpseste_test`; migration 004 appliquée à `palimpseste_lab` et testée dans un schéma isolé de `palimpseste_test` ; migration 005 appliquée aux deux bases après sauvegarde vérifiée du laboratoire. Compte de base dédié. Le worker de production n'a pas été exécuté après connexion Codex ; l'effort Luna est attesté pour la sonde seulement et la lecture artistique de ce cas a été rejetée.

## Matrice exigences → sources → preuve → état

| Ticket | Sources principales | Preuve actuellement observée | État actuel |
|---|---|---|---|
| B01 | `game/`, `shared/`, `backend/` | Player Windows IL2CPP/URP actuel construit et lancé hors ligne 12 s, répondant ; A/B réels et paquet testés séparément hors pipeline joueur | compilé, exécuté partiellement |
| B02 | `shared/Palimpseste.Contracts`, `shared/Palimpseste.Core` | compilation .NET et smoke fixture | compilé, testé auto partiel |
| B03 | `contracts/human-review.schema.json`, `backend/Palimpseste.Api`, `backend/migrations/002_review_access.sql`, `tests/review_http_smoke.py`, `tests/review_access_http_smoke.py` | route de revue liée au principal authentifié ; smoke HTTP/DB à deux comptes creator : délégation sort/cas exacte, soumission du second compte, lecture du sort toujours propriétaire, révocation et nettoyage ; rejet humain de la lecture A exprimé dans la conversation, sans revue signée dans l'API ni corpus de recette | testé auto partiel, fidélité A rejetée |
| B04 | `game/Assets/Palimpseste/Drawing` | canevas, trois brosses et transformation pointeur codés ; dessin réel à la souris capturé dans le lecteur ; équivalence entre cadences non mesurée | exécuté Unity partiel |
| B05 | `game/Assets/Palimpseste/Drawing`, `shared/Palimpseste.Core/PngCodec.cs` | PNG roundtrip fixture ; journal haché durable généré par le lecteur et accepté dans une capture HTTP réelle ; reprise simulée après checkpoint décalé et suffixe incomplet en EditMode, sans arrêt brutal du processus | testé auto et Unity partiel |
| B06 | `game/Assets/Palimpseste/Drawing` | engagement au premier trait, budget d'encre, fermeture et régions codés ; limites et fermeture partielle non couvertes exhaustivement | exécuté Unity partiel |
| B07 | `game/Assets/Palimpseste/Library` | EditMode : reprise d'événements durables au-delà du checkpoint, suffixe incomplet tronqué, événement complet altéré rejeté ; PlayMode : interruption d'un trait actif au focus et `up` durable ; aucune coupure brutale réelle ni Alt+Tab physique instrumentés | testé Unity partiel |
| B08 | `backend/Palimpseste.Api`, `backend/migrations/001_initial.sql`, `tests/api_http_smoke.py` | API Release compilée, migration appliquée ; 16 assertions HTTP/DB dont multipart, idempotence, composite, contrôle propriétaire et deux rejets négatifs réussis sur base test | testé auto partiel |
| B09 | `backend/Palimpseste.Worker/JobRepository.cs`, `tests/Palimpseste.Worker.DbSmoke` | smoke PostgreSQL isolé : claim, lease, fencing, nombre durable de réparations, tentative incertaine, incident ; panne contrôlée après A et pendant B avec description conservée, reprise et rejet des fences périmés | testé auto partiel |
| B10 | `backend/Palimpseste.Provider` | doctor local et connexion ChatGPT dédiée réussis ; sonde réelle A/B sous service avec modèle `gpt-5.6-luna` et effort `max` rapportés ; lecture A rejetée par l'utilisateur, worker arrêté | Luna réel en doctor, pipeline joueur fermé |
| B11 | `game/`, `backend/Palimpseste.Api` | dessin à la souris dans le lecteur → référence/allocate/begin/capture multipart acceptés → job `queued`; pas encore d'appel Luna | testé auto partiel |
| B12 | `shared/Palimpseste.Core/GeometryResolver.cs` | smoke fixture PNG avec trou conservé ; l'encre Unity réelle et A produisent `full.silhouette.0`, `ring.path.0` et un masque dans RealProbe | testé auto sur fixture et encre réelle, pipeline joueur ouvert |
| B13 | `backend/Palimpseste.Provider`, `backend/Palimpseste.Worker` | B réel a retourné un plan JSON sans effet, lié à A ; validation et compilation séparées réussies, puis lancement du paquet dans Unity PlayMode isolé 1/1 ; aucun job joueur | Luna B réel et paquet testé en PlayMode, pipeline joueur fermé |
| B14 | `shared/Palimpseste.Core/SpellCompiler.cs` | validation du vrai B avec la géométrie recalculée et compilation contrôlée acceptées ; règles génériques de traçabilité visuelle et événements du porteur corrigées, régressions Core.Smoke réussies | testé auto sur B réel, publication et Unity ouverts |
| B15 | `shared/Palimpseste.Core/SpellCompiler.cs` | borne ≥476 ticks fixture ; paquet de vérification du vrai B borné à une instance, un tick et zéro application d'effet | testé auto partiel |
| B16 | `shared/Palimpseste.Core`, `backend/Palimpseste.Storage`, `game/Assets/Palimpseste/Library` | archive avant ajout du coffre : fixture manuelle relue et lancée hors ligne ; ZIP actuel extrait et lecteur lancé trois fois ; EditMode final : octet de masque altéré, artefact absent, marqueur SHA absent, version incompatible refusés puis restauration acceptée ; refus d'un cache altéré vu aussi dans un build IL2CPP direct antérieur | testé Unity partiel |
| B17 | `game/Assets/Palimpseste/SpellRuntime` | casts, annulation `ResetTargets` et extinction de colliders testés en PlayMode ; saturation longue non mesurée | testé Unity partiel |
| B18 | `game/Assets/Palimpseste/SpellRuntime` | projectile guidé : touche et impulsion ; projectile courbe compilé : touche, dégâts et brûlure ; murs fins, rebond/pénétration et filtres non exhaustifs | testé Unity partiel |
| B19 | `game/Assets/Palimpseste/SpellRuntime` | fixture avec faisceau dommageable testée auparavant ; le vrai plan Luna sans effet a produit un `LineRenderer` de deux points et zéro dégât en PlayMode isolé, puis a expiré après un tick ; rémanence graphique vérifiée sans porteur actif ni collider ; occlusion/chaîne non exhaustives | testé Unity partiel |
| B20 | `game/Assets/Palimpseste/SpellRuntime` | champ mouille une cible dans l'empreinte et épargne une petite cible dans un trou ; onde soigne un allié sans toucher l'hostile ; fronts rapides non couverts | testé Unity partiel |
| B21 | `game/Assets/Palimpseste/SpellRuntime` | barrière intercepte raycast, expire et désactive ses colliders ; piège ralentit hostile ; réarmement et interactions multiples ouverts | testé Unity partiel |
| B22 | `game/Assets/Palimpseste/SpellRuntime` | dommages, soin de 12000 milli, impulsion, brûlure, Mouillé et ralentissement affirmés en PlayMode ; cumul/réactions complets ouverts | testé Unity partiel |
| B23 | `game/Assets/Palimpseste/Scenes` | scène d'épreuve et contrôles de visée exécutés depuis le ZIP ; scénarios de recette complets ouverts | testé Unity partiel |
| B24 | `game/Assets/Palimpseste/Presentation`, `SpellRuntime` | décor URP, lumière et effets visibles dans ancien ZIP ; vrai paquet Luna : `LineRenderer` teinté feu en PlayMode, puis 13 616 pixels rouges sur `RenderTexture` Direct3D 12 isolée après expiration, zéro après nettoyage ; caméra du Player non vérifiée | rendu isolé testé, Player ouvert |
| B25 | `game/Assets/Palimpseste/Presentation`, `Library` | audio procédural et clip fixture affirmés en PlayMode ; vrai paquet Luna : `AudioSource` créé à la naissance, sans écoute humaine ; revue des paires de dessins non exécutée | testé technique partiel |
| B26 | `game/Assets/Palimpseste/Bootstrap`, `Scenes`, `Service/WindowsCredentialStore.cs` | UI française et parcours dessiner→capture vus ; deux tailles 1024 × 768 et 1920 × 1080 sur archive précédente, 1920 × 1080 sur ZIP final ; coffre Windows écrit/relu/effacé après redémarrages du Player extrait ; focus testé en PlayMode ; clavier/accessibilité et autres tailles non exhaustifs | exécuté Unity partiel |
| B27 | `backend/`, `ops/backup-lab.ps1`, `ops/restore-lab.ps1`, `ops/backup-runtime.ps1`, `ops/restore-runtime.ps1`, `evidence/public/backend/` | restauration à blanc labo : 4/4 artefacts DB et API GET ; scripts runtime éprouvés sur base synthétique distincte : dump, 10/10 fichiers hashés, 4 lignes DB, ACL service M ; snapshot privée de la capture Unity | testé auto partiel |
| B28 | `game/`, `backend/`, `evidence/public/unity/b28-final-endurance.md` | ZIP final SHA `343a5858…` : 662,734 s, 133 points Windows tous répondants, RSS +13 099 008 octets, privés +27 426 816 ; 13 clics périodiques, bursts 20/30 et deux resets sur fixture manuelle, deux instances actives au maximum ; 79 fenêtres de frames internes ; aucun Profiler, FPS de présentation, coût CPU p95 ni saturation forte | observation partielle |
| B29 | `tests/`, `evidence/` | réponses A/B sur dessin Unity réel, compilation et lancement PlayMode isolé du paquet ; aucun parcours joueur connecté dessin → job → téléchargement → Player ; premier verdict humain : interprétation A incorrecte | essais partiels, recette finale ouverte |
| B30 | `docs/`, `game/Build/`, `deliverables/` | copie jouable actuelle `game/Build/WindowsPlayerFeedbackPlayable/` : 29 fichiers, 122 074 957 octets, exe SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`, lancée hors ligne 12 s ; ZIP antérieur historique, nouveau ZIP bloqué avant création ; installation autre machine et verdict final humain non faits | build actuel exécutable hors ligne, livraison ouverte |
| L01 | `backend/Palimpseste.Provider/CodexSettings.cs` | effort `max` demandé, effort inférieur refusé ; A et B de la sonde rapportent `max` avec le CLI patché | effort réel attesté en doctor, approbation ouverte |
| L02 | `backend/Palimpseste.Provider/CodexProcessRunner.cs` | transport `codex exec` réel sous compte ChatGPT du service, A/B réussis ; environnement sans clé API, classification des crédits épuisés testée par faux CLI | transport réel en doctor, joueur non testé |
| L03 | `backend/Palimpseste.Provider/LunaCodexProvider.cs` | A a transmis les deux images et obtenu un JSON conforme, modèle et effort rapportés ; l'utilisateur a jugé incorrecte sa lecture du trait rouge-brun diagonal | appel A multimodal réel, fidélité rejetée pour ce cas |
| L04 | `backend/Palimpseste.Provider/LunaCodexProvider.cs`, `backend/Palimpseste.Worker` | B réel a utilisé la description figée et son hash, avec un plan de faisceau visuel sans effet ; la géométrie a été recalculée à partir de A et de l'encre pour une compilation de vérification en mémoire ; job joueur non prouvé | appel B réel et plan compilé en vérification, intégration ouverte |
| L05 | `contracts/codex`, `shared/Palimpseste.Core/ContractJson.cs` | 17 `const` des schémas de transport typés ; 61/61 contrôles documentaires ; sorties A et B réelles conformes au schéma | contrats réels testés partiellement |
| L06 | `backend/ProviderDoctor` | doctor local et sonde active A/B sous `PalRuntimeSvc` réussis, CLI patché hashé, A et B attestés `gpt-5.6-luna`/`max` ; lecture artistique de A rejetée, verrou de production fermé | porte technique active satisfaite en doctor, porte artistique rejetée |
| L07 | `backend/Palimpseste.Worker`, `tests/Palimpseste.Worker.DbSmoke` | leases/fencing/10 appels, au plus deux réparations durables par étape et deux reprises du processus avant lancement ; smoke DB de panne contrôlée après A et pendant B, reprise et rejet des fences périmés réussi ; panne réelle du fournisseur ou du serveur DB non exécutée | testé auto partiel |
| L08 | `backend/Palimpseste.Provider`, `ops/` | arguments séparés, environnement enfant épuré, racines contrôlées, faux transport d'injection testé ; ACE source héritable complète et refus effectif sous service de lister la racine et d'ouvrir un source en lecture/écriture ; préflight ouvre quatre sentinelles ; spec RX et fonctions exposées observées `false` ; autres enfants, changement ACL ultérieur et modèle adversarial non éprouvés | testé local partiel, isolation à accepter |
| L09 | `ops/`, `game/Assets/Palimpseste/Editor` | build Unity lancé par outil de développement ; API joueur sans route de build ; service de génération distinct et bloqué avant preflight | exécuté partiel |
| L10 | `evidence/`, `game/Build/` | deux fixtures manuelles hors ligne ; A/B réels en doctor, plan compilé et paquet lancé en PlayMode isolé 1/1 ; pas de Player IL2CPP ni de réseau joueur | testé technique Luna → PlayMode partiel |

M0 à M7 restent **ouverts**. Les portes de validation humaine restent ouvertes ; aucun statut `accepté humainement` n'est attribué. Les exemples et tests sur fixtures ne prouvent ni la fidélité d'interprétation ni le fonctionnement final dans Unity.

| Jalon | Réalisation technique observée | Porte encore ouverte |
|---|---|---|
| M0 | dépôt, contrats, API health et build Unity réels | arbitrages P01–P10 et périmètre non validés humainement |
| M1 | dessin réel, raster, journal et capture réseau ; reprise simulée et focus testés | essai humain de l'engagement au premier trait ; panne brutale complète |
| M2 | job issu du dessin en file, runner A codé, doctor local et authentification dédiée réussis ; A réel conforme avec modèle/effort rapportés en doctor | lecture A rejetée pour le cas sondé ; versionner et éprouver le recalibrage sur corpus de conception, puis obtenir un nouveau verdict |
| M3 | géométrie, compilateur et paquets fixtures testés ; vrai B sur A obtenu en doctor, géométrie recalculée et B compilé en mémoire dans RealProbe, puis paquet lancé en Unity PlayMode isolé | paquet joueur avec artefacts et provenance réels, Player connecté et comparaison humaine clauses/règles |
| M4 | six porteurs et effets principaux testés en PlayMode ; faisceau Luna sans effet lancé en PlayMode isolé | scénarios physiques exhaustifs et essai humain du comportement |
| M5 | rendu URP, audio procédural et bibliothèque hors ligne ; faisceau Luna et `AudioSource` créés en PlayMode, rémanence 0,16 s et pixels URP mesurés sur caméra isolée | visibilité depuis la caméra du Player, son écouté, dix paires et reconnaissance humaine des signatures |
| M6 | isolation locale avec probe effectif sous service, preuve locale liée au SHA-256 du CLI, sonde active A/B attestant modèle et effort, reprise DB, sauvegarde/restauration, cache hors ligne avec rejets de corruption/chargement partiel, jeton dans Credential Manager testé sur le lecteur IL2CPP | approbation de la preuve active, panne fournisseur réelle, autre compte/poste Windows et démonstration humaine |
| M7 | Player Windows IL2CPP/URP actuel jouable hors ligne, lancement 12 s vérifié et ZIPs précédents historiques | archive du Player actuel, parcours connecté, 30 dessins inédits via Luna, machine propre et verdict humain signé |

## Point de reprise technique

1. API/migration et smoke HTTP sont exécutés sur la base test. Pour une nouvelle recette, restaurer la vraie capture Unity depuis le dossier privé indiqué dans `evidence/public/backend/api-restauration-2026-09-19.md` dans une nouvelle base et un stockage vide ; ne pas rejouer à l'aveugle une tentative fournisseur incertaine.
2. Le smoke DB couvre maintenant la panne contrôlée après A et pendant B, la conservation de la description, la reprise avec fencing et l'arrêt d'une tentative `running` incertaine en `needs_operator`. Il reste à exécuter une coupure réelle du processus/DB et à rapprocher son coût et son résultat avant toute reprise automatique.
3. Le profil OS `PalRuntimeSvc`, ses ACL et le doctor local lié au SHA-256 du CLI sont prêts. Une sonde sous ce compte a constaté le refus de lister la source et d'ouvrir un fichier source en lecture/écriture ; le préflight exige l'ACE héritable complète et quatre sentinelles, sans couvrir tous les enfants. La `PSCredential` DPAPI privée dépend du profil opérateur Windows actuel et n'est pas dans le dépôt. La connexion ChatGPT dédiée a réussi par code d'appareil, sans copie du profil personnel. Les entrées privées du doctor actif, issues du vrai dessin Unity, sont sous `E:\PalimpsesteRuntime\artifacts` : `reference.png`, `drawing.png`, `ink.png`, `geometry.json` ; voir `evidence/public/backend/doctor-inputs-2026-09-19.json`. La géométrie du diagnostic utilisait des requêtes provisoires ; RealProbe l'a ensuite recalculée à partir de la vraie description A et de l'encre, puis a compilé B avec des identifiants synthétiques. Le job de production doit encore suivre ce chemin avec des artefacts réels. Les tentatives antérieures et leurs corrections sont consignées dans `evidence/public/backend/provider-device-auth-and-doctor-block-2026-09-19.md`. La sonde suivante, avec CLI patché, a réussi A et B : voir `evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`. L'utilisateur a rejeté la lecture artistique A de ce cas ; garder cette preuve comme diagnostic technique, fermer le worker et recalibrer A sur une nouvelle version. Ne pas utiliser le profil personnel comme preuve runtime ni relancer un modèle sans contrôle du quota existant.
4. Le build IL2CPP final et les tests Unity ont été refaits après les correctifs de reprise/cache/coffre ; le ZIP extrait a été lancé trois fois à 1920 × 1080 pour vérifier le jeton protégé. Un essai hors ligne de la fixture manuelle et une endurance B28 de 662,734 s concernent désormais ce ZIP final, avec preuves dans `evidence/public/unity/b28-final-endurance.md`. Les captures de 1024 × 768 et la mesure courte B28 initiale concernent l'archive précédente. Couvrir ensuite les scénarios manquants des porteurs/effets, une vraie coupure du lecteur, une saturation plus forte avec Profiler, le clavier/accessibilité et un autre compte/poste Windows.
5. Produire corpus de dessins inédits, tests complets, sauvegarde/restauration, dossier de preuve et verdicts de créateurs ; ne marquer M7 complet qu'après cette recette.

Commandes déjà exécutées, avec résultat observé : `dotnet build Palimpseste.sln -c Release -v:q` (succès, 0 avertissement), `dotnet run --project tests/Palimpseste.Core.Smoke -c Release` (succès, y compris après correction des règles du compilateur), `dotnet run --project tests/Palimpseste.Provider.Security -c Release` (succès, y compris ACL partielle, preuve hashée et refus simulé de crédits épuisés), `dotnet run --project tests/Palimpseste.Worker.DbSmoke -c Release` sur `palimpseste_test` (succès), test concurrent de quota dans `palimpseste_test` (20 admissions, 3 validées), `python qa/validate_contracts.py` (61/61 contrôles documentaires), doctor local sous `PalRuntimeSvc` (succès sans appel modèle), anciennes tentatives A documentées, puis une sonde active A/B réussie sous le CLI patché. `dotnet run --project tests/Palimpseste.Core.RealProbe -- <A.json> <B.json> <ink.png>` a quitté avec le code 0, validant et compilant en mémoire les vraies sorties A/B avec l'encre réelle. Unity PlayMode batch `-nographics` a passé 1/1 sur ce paquet, puis un test graphique Direct3D 12 distinct a mesuré 13 616 pixels sur caméra isolée après expiration et zéro après nettoyage. Aucun son n'a été écouté. Le nouveau contrat API de description A a été testé HTTP 18/18 ; l'échange d'invitation a été testé 4/4 sur la base de test. Les tests Unity et le build ont leurs propres logs/artefacts sous `game/Logs`, `deliverables/`, `docs/UNITY_TEST_PROOF.md` et les preuves PlayMode dédiées.
