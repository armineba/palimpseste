# Décisions de réalisation

## D15 — Intentions physiques contrôlées et ressources gratuites avant construction

Le créateur demande une correction systémique pour les prochains parchemins : le phénomène annoncé doit correspondre à un mouvement réel et le déploiement doit être décrit, notamment pour différencier un effet sur le lanceur d'un effet ciblé au sol. Aucun sort existant n'est corrigé par son titre ou identifiant.

A déclare des intentions typées ; B les copie et règle des paramètres bornés ; le compilateur contrôle ces correspondances ; le moteur précompilé applique les déplacements et animations. La description conserve son rôle d'autorité. Les six paramètres d'écoulement visuel peuvent être affinés par la critique sans modifier l'intention, le placement, la gravité ni l'angle de lancement.

Avant B, une recherche obligatoire par job choisit et consulte des références dans un index primaire revu, puis fournit les ressources gratuites importées et leurs licences. La sélection finale dépend aussi de l'image présentée à B. Les téléchargements et imports de code restent des opérations de développement ; aucun worker de génération joueur n'obtient d'outil d'installation, de shell ou de build. Gratuit uniquement, aucun achat ou nouvel abonnement.

Dream-loop Pro reçoit désormais plusieurs instants actifs. Les anciens paquets conservent leur rendu historique ; les nouveaux nécessitent le client 1.5.0. La demande de laisser les essais au créateur reste en vigueur. [Architecture, état et limites D15](D15_BEHAVIOR_AND_RESEARCH.md).

## D14 — Le texte pilote le cycle complet, la critique indépendante compare le rendu

La demande actuelle prolonge D13 avec une boucle **Dream-loop Pro**. La description est l'autorité pour chaque sujet du sort : **apparition, activité, réaction au contact et disparition naturelle sans contact**. Ces quatre phases doivent être décrites puis traduites en paramètres d'animation contrôlés. L'image générée représente un moment actif caractéristique et en fixe la cible d'apparence ; elle ne suffit pas à définir la chronologie ou les mécaniques.

Les modèles restent **A `gpt-5.6-sol` / `high` ; G, B et J `gpt-6-astra` / `high`**. A utilise `sp.prompt.a/2.3`, B `sp.prompt.b/2.2` et le nouveau critique J `sp.prompt.j/1.0`. J reçoit dans une nouvelle session la description, l'image cible, le plan et quatre captures produites réellement par un **Player Unity de rendu précompilé**. Il fournit un verdict structuré et des corrections ; une éventuelle reprise B peut modifier seulement `appearance.construction`, `appearance.vfx` et `appearance.lifecycle`, avec conservation des mécaniques et validation des bornes. La boucle conserve les versions, captures et verdicts ; elle reste limitée en tours et appels. Son score ne constitue pas un verdict humain ni une garantie de ressemblance parfaite.

Le créateur a autorisé les captures runtime et la critique à la génération du sort dans ce parcours Pro. Cette autorisation remplace les anciens passages interdisant toute capture par job. Le service lance une action de rendu fixe ; **aucun modèle ne reçoit un outil de capture, d'exécution, de modification de code ou de build**. Le binaire Codex durci reste utilisé : registre d'outils vide pour A/B/J, uniquement l'image native en G. Aucun C#, shader ou commande généré n'est exécuté. Les captures décoratives des phases ne prouvent pas la fluidité, le son, la collision ou les dégâts.

Séparément, le créateur demande de finir la livraison et de le laisser tester lui-même : **ne plus lancer de tests, appels modèle ou captures de validation depuis l'agent de développement pour cette livraison**. Un CoreSmoke avait déjà réussi avant cette demande. Le build et le packaging Player `1.4.0` ont réussi ; le [manifeste](../evidence/public/unity/lifecycle-delivery.json) est conservé. Le backend D14 est déployé localement, la migration `009` appliquée, le renderer protégé installé et le Player ouvert pour le créateur : [preuve](../evidence/public/backend/lifecycle-2026-09-20.json). Aucun parcours joueur ni validation visuelle D14 n'est déclaré réalisé.

Sur demande explicite du créateur, les **21 dossiers locaux de sorts** ont été supprimés, puis leur sauvegarde définitivement supprimée. La bibliothèque était vide à l'observation ; l'historique serveur n'a pas été purgé. Ce nettoyage demandé ne change pas le principe d'immuabilité des documents conservés côté serveur.

## D13 — Chaque nouveau sort reçoit une vraie image avant sa construction

La dernière demande du créateur impose le parcours **dessin → description → image réellement générée depuis cette description → reconstruction 3D/VFX guidée par cette image → sort dans le laboratoire**. Cette autorisation remplace les restrictions D10–D12 qui réservaient les images générées au développement. L'image est un artefact du nouveau job, affichable au joueur et conservé pour la reprise ; une sélection de forme prédéfinie ou une capture du renderer existant ne remplit pas cette étape.

Les modèles A/B de D10 restent les réglages en vigueur : A `gpt-5.6-sol` / `high`, puis B `gpt-6-astra` / `high`. L'emploi courant du nom « Astra » pour l'interprétation ne constitue pas un changement silencieux de ces réglages. Le nouveau stade G utilise `gpt-6-astra` / `high` et l'outil natif `image_generation` de Codex avec l'abonnement connecté, sans clé API. Le même nouveau binaire durci `codex-image.exe` doit servir A/B/G. L'audit de l'ancien CLI a trouvé `apply_patch` encore exposé via `code_mode_only` malgré les flags de fonctions désactivées : le nouveau filtre retire les outils du registre lui-même. Avec `PALIMPSESTE_IMAGEGEN_TEXT_ONLY=1`, le registre A/B doit être vide ; G conserve seulement l'outil image natif, sans lecture de fichier ni seconde génération. L'ancien `codex.exe` et ses preuves sont conservés comme archives, **jamais réutilisés pour valider le nouveau binaire**. Trois nouveaux rapports liés à son SHA sont requis : isolation locale, A multimodal seul, puis G+B réellement validés/compilés. Ni la disponibilité effective de G sur le compte ni sa livraison ne se déduisent de cette décision.

B `sp.prompt.b/2.1` reçoit réellement l'image figée, son hash et la description. Il décrit une composition `appearance.construction.parts` : volumes, éclats, plumes, rubans, anneaux et arcs, matériaux autorisés, couleurs, dimensions, transparence et mouvements bornés. La limite est de 64 parties par nœud, 128 parties déclarées par plan et 1024 après expansion des activations. La révision `2.1` rend explicite `turn_mdeg_s=0` hors `homing`, après un vrai plan rejeté pour `turn_rate` ; elle ne relâche pas le compilateur. Celui-ci lie l'image à la description et au plan, exige une construction pour chaque nœud du nouveau parcours et impose le client `1.3.0`. Les anciens paquets restent lisibles sans génération rétroactive. La référence PNG est un artefact distinct des masques de collision.

L'image et sa construction restent des données : aucun code, shader, chemin serveur ou commande produit par un joueur ne s'exécute. Le worker n'accède pas au dépôt ni aux secrets du serveur ; seuls les fichiers d'entrée/sortie nécessaires à la tentative sont exposés. Les tests et builds Unity demeurent des opérations de développement. La publication reste contrôlée, les checkpoints A/G/B durables et les erreurs d'issue incertaine ne déclenchent pas un deuxième appel automatique. Les règles de compte D05, d'identité et de cache hors ligne demeurent applicables.

État au point de reprise D13 : CoreSmoke/DbSmoke réussis, build final Unity et binaire natif durci construits avec exit 0, quatre tests ciblés du validateur Unity passés. A réel a réussi en 34,091 s et G réel en 55,784 s. B1 a répondu en 218,064 s, mais son plan a été rejeté pour `turn_rate` ; cet échec est conservé. B repris seul sous le prompt `2.1` a réussi en 204,725 s avec le même PNG : plan validé et compilation réussie. Un seul nouvel appel B, soit quatre appels A/G/B1/B2 au total, sans deuxième G. Le service D13 est déployé localement (API 30084, worker 15760, readiness prêt, diagnostics locaux sans issues) et le Player `1.3.0` est ouvert, PID 39012, fenêtre réactive. La capture Unity 04 a réussi 1/1 : impact 1, dégâts 12000, impulsion 1, aucun porteur/renderer restant et cache inchangé. Le dernier réglage du verre est postérieur à cette capture ; l'acceptation artistique reste ouverte. Ces sondes ne constituent pas un job joueur de production. [L'oiseau d'orage de développement](art-direction/storm-bird-target.png) reste distinct du PNG réellement produit par G et n'est pas une capture Unity. Les [preuves courantes](IMPLEMENTATION_STATUS.md) et [prochaines actions](NEXT_ACTIONS.md) doivent être actualisées après exécution.

## D12 — Fidélité à l'image et itérations visuelles obligatoires

Le créateur rejette explicitement le spectre de la version 1.2.1 et demande une correspondance « 1 pour 1 » avec `art-direction/spectral-veils-target.png`, puis précise qu'il faut itérer. Le verdict artistique D11 est donc **refusé** pour ce spectre. Une compilation réussie, des particules supplémentaires ou une capture du moteur ne constituent pas une acceptation de sa ressemblance.

La cible est une capuche drapée creuse avec lumière nacrée, de très longues nappes de tissu violet ajouré, une silhouette étirée dans le sens du vol et un impact qui disperse cette même matière. Chaque itération conserve des captures Unity comparables, observe les écarts de proportions, de silhouette et de matière, puis les corrige. Une seconde caméra de revue peut faciliter la comparaison à l'image, mais doit être identifiée comme telle ; elle ne remplace pas la vue livrée dans le laboratoire. La correspondance exacte reste une exigence à vérifier, jamais une réussite présumée.

Le paquet joueur, sa physique et l'isolation du worker restent inchangés. Le travail de création des assets et du renderer se fait pendant le développement ; aucune étape de capture, test, build ou génération d'image n'est ajoutée au parcours joueur.

Précisions après la deuxième itération : le créateur rejette l'aspect « modèle 3D » et demande une apparition magique **sans corps réel**. La capuche ne doit donc pas devenir un costume opaque : brume lumineuse, filaments et nappes d'énergie suggèrent une présence. Il demande une animation fluide, organique lorsque le matériau s'y prête, et un impact marqué. La caméra du labo reste inchangée ; les corrections portent sur les VFX.

## D11 — Magie stylisée et mouvements superposés

Le créateur juge le rendu 1.2.0 trop simple et fournit deux références : soin vert en spirale, puis jaillissement arcanique violet avec une couronne et des particules verticales. Le travail demandé porte sur des VFX plus complexes et stylisés : rubans larges et effilés, plusieurs rythmes de rotation et d'élévation, éclats en étoile, brumes légères, naissance et extinction soignées. La forme centrale reste celle de l'objet interprété ; les effets ne copient pas les pixels du dessin.

Cette évolution est embarquée dans le renderer URP. Elle utilise les données contrôlées déjà disponibles et améliore aussi les sorts sauvegardés. Aucun appel modèle, test, build ou achat d'asset supplémentaire n'est ajouté au parcours joueur. La chaîne Sol/high → Astra/high et ses contrats demeurent ceux de D10. La ressemblance artistique aux références et le plaisir visuel restent à apprécier dans le jeu construit.

Mis à jour le 20 septembre 2026. Les arbitrages P01–P10 du cahier restent des bases de réalisation, sans validation humaine implicite. Les décisions D07 et D08 ci-dessous remplacent le découpage en trois régions et le choix Luna A des versions antérieures pour les nouveaux parchemins.

## D10 — Interprétation rapide et VFX composés

Le créateur veut des sorts visuellement spectaculaires dans le laboratoire et une attente de génération mesurable. La chaîne cible est Sol avec effort `high` pour interpréter le dessin, puis Astra avec effort `high` pour planifier le sort. Ce changement de modèles doit être vérifié sur le transport Codex installé et sur un vrai job avant d'être déclaré en production ; la version antérieure Astra → Luna reste un fait historique des preuves déjà enregistrées.

Chaque nœud peut contenir un profil visuel décoratif `appearance.vfx` : style, motif, densité, rayon d'aura, durée de charge et forme d'impact, dans des listes et bornes fixes. Le profil sert à superposer des VFX du moteur Unity autour de la forme et des effets du sort. Il ne change ni les dégâts, ni les cibles, ni les collisions, ni les limites de porteurs. Un paquet sans profil reste lisible ; un paquet avec profil exige le client 1.2.0. Les images d'inspiration créées pendant le développement peuvent guider les assets du jeu, mais aucun appel d'image générative supplémentaire n'est déclenché pour chaque joueur.

La durée du parcours doit être instrumentée par étapes observables (file, interprétation, planification, compilation, téléchargement) sans afficher un pourcentage inventé. Les budgets, quotas et permissions du worker demeurent bornés ; le rendu ne donne jamais à un parchemin accès au code, au build ou aux fichiers du serveur.

## D09 — L'objet interprété détermine le modèle 3D

Le créateur demande que le sort représente ce qu'Astra imagine : si le gribouillis évoque un rocher, le laboratoire affiche un rocher en volume ; il ne doit plus recopier automatiquement la silhouette du dessin. Cette demande remplace l'obligation historique de faire provenir la forme visible ou l'emprise de chaque sort des pixels d'encre. Le dessin continue d'alimenter l'interprétation multimodale et ses observations ; la description choisit ensuite une forme visuelle contrôlée, son mouvement, sa palette et ses effets.

Luna conserve ce choix dans le plan, le compilateur vérifie sa traçabilité, et Unity construit les volumes, matériaux, traînées et impacts avec ses composants embarqués. Aucun code, shader ou commande fourni par un parchemin n'est exécuté. Les créatures visuelles utilisent les porteurs réellement disponibles ; cette décision ne prétend pas livrer une IA de compagnon autonome. L'objectif artistique demandé est un rendu 3D propre et impressionnant ; son acceptation reste un verdict du créateur après observation du build.

## D07 — Dessin libre et fermeture explicite

Un nouveau parchemin `free_canvas_v2` accepte des traits sur tout le carré. Relâcher la souris ou le stylet termine seulement le trait en cours ; le joueur peut ajouter d'autres traits, couleurs et styles. Seul le bouton « Dessin terminé » ferme le parchemin avec `closed_reason=user_finished` et déclenche la capture. Une interruption ou fermeture de fenêtre garde l'état local pour reprise. Les anciens parchemins `three_regions_v1` restent lisibles avec leur contrat et leur raster d'origine.

## D08 — Astra interprète l'apparence, Luna construit le plan

Un appel multimodal `gpt-6-astra` avec effort `max` reçoit la référence neutre et le dessin complet et produit la description du sort. Son interprétation est libre dans les capacités du jeu ; elle ne déduit pas le sens des trois anciennes régions. Un appel `gpt-5.6-luna` avec effort `max` reçoit la description figée et la géométrie extraite de l'encre, puis fournit un plan déclaratif. Le compilateur serveur vérifie ce plan avant publication. Aucun modèle ne fournit de C# exécutable, de commande de build ou de chemin serveur au Player.

## D01 — Compte Codex partagé, identité joueur séparée

Le créateur a précisé qu'un seul abonnement Codex déjà utilisé sur le serveur doit alimenter tous les joueurs. Le worker `PalRuntimeSvc` utilise donc le **même compte d'abonnement**, après une connexion distincte dans son `CODEX_HOME` isolé. Aucune clé API OpenAI n'est demandée ou embarquée dans Unity. Cette connexion n'identifie pas les joueurs entre eux ; l'API conserve ses identités et ses contrôles de propriétaire.

## D02 — Parcours du jeu

L'écran joueur doit montrer le dessin, l'interprétation textuelle d'Astra dès qu'elle est disponible, puis le sort compilé depuis le plan déclaratif de Luna dans le laboratoire. Le joueur peut signaler une lecture incorrecte ; aucune approbation artistique automatique n'est déduite de la validation technique. Les champs « Service » et « Jeton privé » sont retirés. Les fixtures locales de démonstration ne sont pas présentées comme des créations joueur. Les vrais sorts téléchargés restent mis en cache pour la réutilisation hors ligne demandée initialement.

## D03 — Accès privé sans secret dans le binaire

Un opérateur délivre une invitation aléatoire à usage unique et durée de 24 heures. L'installation la place dans le profil du joueur ; Unity l'échange sur `POST /v1/session/redeem` via HTTPS, puis range le jeton joueur obtenu dans Windows Credential Manager. Le code d'invitation et le jeton ne sont pas intégrés au build. Ce jeton n'est ni une connexion Codex, ni un droit d'exécuter un processus ou de lire le serveur. L'opérateur conserve le contrôle de l'admission au laboratoire et du coût du compte partagé.

## D04 — Limite de déploiement actuelle

Le laboratoire local utilise `127.0.0.1` pour ses essais. Un nom HTTPS de serveur public, la distribution privée des invitations et les politiques de quota/admission doivent être configurés et vérifiés sur le VPS réel avant ouverture à plusieurs joueurs. Aucun déploiement distant n'est déclaré par ce journal.

## D05 — Usage de l'abonnement existant seulement

Le créateur demande de consommer uniquement le quota inclus et les crédits déjà présents sur le compte partagé, sans clé API ni achat/recharge. Sa capture montre la fenêtre **d'activation** d'une recharge automatique, bouton désactivé faute de moyen de paiement sélectionné ; elle ne prouve pas à elle seule tous les paramètres du compte. Le serveur n'intègre aucun achat de crédits. Le worker s'arrête sur refus de quota/identité ou de crédits épuisés et ne relance pas un appel déjà démarré à l'issue incertaine. Codex peut employer des crédits existants après le quota inclus selon les réglages du compte ; l'absence de recharge automatique doit rester vérifiée dans les paramètres du compte par son titulaire. Selon [OpenAI](https://help.openai.com/fr-fr/articles/12642688-utilisation-de-cr%C3%A9dits-pour-une-consommation-flexible-dans-chatgpt-freegopluspro-et-sora), une tâche Codex commencée avec un solde positif peut finir avec un solde négatif si l'usage simultané épuise les crédits. Le service limite la concurrence à une demande et ne fait aucun achat, mais il ne peut pas garantir un plafonnement exact du coût d'un tour déjà lancé. Le doctor actif coûte lui-même des appels normaux : la dernière sonde A a rapporté 13 662 jetons d'entrée et 3 648 de sortie, sans preuve du montant imputé au quota ou aux crédits.

## D06 — Cache associé au joueur authentifié

La réponse authentifiée `GET /v1/capabilities` renvoie `principal_id`. Le Player
enregistre ce propriétaire avec chaque nouveau parchemin et filtre la bibliothèque
sur l'identité courante. Hors ligne, il ne retrouve cette identité qu'avec le jeton
du coffre Windows associé à l'URL et son empreinte locale. Les anciens parchemins
sans propriétaire prouvé restent sur disque mais sont masqués ; aucune attribution
automatique à un autre joueur n'est faite.

## D16 — Planche d’animation 3 × 7 (21 septembre 2026)

Le créateur remplace l’image cible unique par sa référence de planche : APPARITION, STABLE, DISPARITION, sept cases par ligne. Un atlas natif G précède une mise en page fixe du serveur ; les deux SHA et artefacts sont conservés séparément. La description demeure l’autorité du cycle et des mécaniques. B construit un seul sort animé et J examine sept poses réelles par phase. Pipeline 4 / client 1.6.0 ; aucun sort existant modifié. Les essais restent au créateur. Voir [D16](D16_ANIMATION_SHEET.md).

## D17 — Bibliothèques obligatoires avant reconstruction (24 septembre 2026)

Le créateur impose TinyPlay URPShadersCollection, xtaja VFX-Shader, Magic Effects FREE, Unity VisualEffectGraph-Samples et Keijiro VfxGraphAssets comme bases de recherche pour reconstruire le visuel de la planche. Les cinq sources doivent être examinées à l'étape **planche → recherche → construction B**, puis les ressources et techniques pertinentes servir de point de départ à Dream-loop Pro. La description conserve l'autorité sur la chronologie et la physique. Les URL, conditions de réutilisation et organisation sont dans le [document texte demandé](BIBLIOTHEQUES_VFX_OBLIGATOIRES.txt).

Les imports se font dans le développement, avec licences et compatibilité URP vérifiées. Le worker consulte les fiches contrôlées ; aucun accès au code du projet, script tiers, installation ou build logiciel n'est ouvert au joueur. Les recherches déjà enregistrées restent immuables. Le prompt B passe à 2.5 pour exploiter les cinq fiches lorsqu'elles sont présentes. L'état de téléchargement et d'intégration doit rester distinct de la fidélité visuelle réellement observée.
