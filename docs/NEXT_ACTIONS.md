# Point de reprise immédiat

## 24 septembre — D18 installé, plusieurs parchemins en parallèle

**Publication code 0 et installation terminée à 22:52:32 Paris.** Le worker accepte plusieurs jobs asynchrones dans un même processus. `PALIMPSESTE_MAX_PROVIDER_CONCURRENCY=0` retire le plafond applicatif de jobs actifs, à la demande du créateur. Les captures Unity partagent un seul créneau GPU ; les autres étapes continuent en parallèle. [Fonctionnement](D18_CONCURRENT_JOBS.md) · [Installation réelle](../evidence/public/backend/parallel-jobs-d18-installation.json) · [Preuve de livraison](../evidence/public/backend/parallel-jobs-d18.json).

Le déploiement a attendu la fin du travail déjà admis et suspendu temporairement les nouvelles prises de jobs par D17. La pause a été retirée avant de lancer D18 : `claims_paused=false`, `completed=true`, phase `services_started`. Les jobs en attente restent disponibles pour le nouveau worker. Aucun appel en cours annulé pour imposer la bascule. Player **1.6.0** conservé, sans build Unity ni fermeture de sa fenêtre.

**Aucun test, appel modèle de diagnostic, sort de démonstration ou capture de validation lancé par l'agent.** Une commande supplémentaire de consultation de la file et de disponibilité a été refusée par le contrôle automatique (`blocked by policy`, sans motif détaillé) ; elle n'a pas été contournée. Le rapport d'installation atteste le démarrage des services ; la progression simultanée des jobs après installation n'a pas été observée. Ne pas la présenter comme un essai réussi.

Suite : laisser le créateur utiliser plusieurs parchemins et recueillir son retour. Le nombre de jobs n'est plus borné par l'application ; les quotas réels du compte et la capacité de la machine restent applicables. Aucun achat ni recharge ajouté. La fidélité visuelle et la latence sous concurrence restent à apprécier. Les sections ci-dessous sont historiques, notamment les nombres de parchemins et PID alors observés.

## 24 septembre — D17, bibliothèques obligatoires dans la reconstruction

Le créateur précise le point d'application : **planche d'animation générée → examen des cinq bibliothèques → construction B → critique et révisions Dream-loop Pro**. [Document texte demandé](BIBLIOTHEQUES_VFX_OBLIGATOIRES.txt) · [Inventaire des clones](../reference/vfx-libraries.inventory.json) · [Détails du fonctionnement](references-vfx-sources.md).

Code du resolver et prompt B **2.5** modifiés. Chaque nouvelle recherche exige les cinq sources exactes et ajoute une référence complémentaire selon la description. Les quatre GitHub sont consultés en parallèle ; Magic Effects FREE fournit une fiche locale de disponibilité et de licence, sans contenu du package. B et ses révisions réutilisent la même recherche liée aux SHA de la description et de la planche. Les dossiers anciens restent immuables.

**D17 publié et installé le 24 septembre à 21:59:55 Paris.** `ops/package-backend.ps1` a terminé avec le code **0**, stage `deliverables/.stage-backend/9795134d91554c7ca3df7d9a1a3b2c5e`. Le resolver, le prompt B **2.5** et l'index des **cinq sources obligatoires** sont déployés. [Preuve d'installation](../evidence/public/backend/vfx-libraries-d17-installation.json). Worker PID **25352**, API PID **32092** et disponibilité HTTP **200** observés à **22:00:32 Paris** ; Player 1.6.0 inchangé, rouvert PID **32492**. Aucun test, appel modèle, capture ni parcours joueur exécuté par l'agent.

Suite : laisser le créateur essayer un nouveau parchemin et juger le sort. Les clones restent hors dépôt dans `E:\Palimpseste\VFX_Libraries` ; leurs nouveaux assets ne sont pas encore intégrés au Player. L'intégration côté développement des ressources compatibles et autorisées reste à faire, avec notices et identifiants contrôlés. Unity 6.3 URP reste la cible ; ne pas importer le projet HDRP complet. Aucune fidélité visuelle identique n'est encore démontrée.

**Purge demandée mais non exécutée :** le contrôle automatique a rejeté la suppression des six dossiers de parchemins locaux (`blocked by policy`, sans motif détaillé). Les six dossiers restent présents, dont un `capture_pending` sans identifiant de job. Aucun contournement, aucune sauvegarde créée, aucune suppression de l'historique serveur. Les fichiers d'identité et de service sont conservés. Le refus antérieur de relance API est historique : le déploiement ci-dessus a depuis démarré les deux services.

## 22 septembre — accès au dessin rétabli après arrêt des services

Le bouton « Dessiner un parchemin » était désactivé parce que l’API et le worker locaux étaient arrêtés. PostgreSQL était encore actif. La bibliothèque affiche alors une invitation à ouvrir, même lorsque le jeton Windows existe toujours ; le code ne supprime ce jeton que sur HTTP 401.

Services relancés avec les lanceurs protégés de l’installation `animation-sheet-install-20260921-053249-6cb61478`, dont les empreintes correspondent aux exécutables installés : worker **11000**, API **5576**, disponibilité HTTP **200**. Player 1.6.0 rouvert, PID **22420**, fenêtre « Palimpseste Spell Lab » réactive. Aucun jeton remplacé, aucun sort modifié, aucun build ni essai de génération lancé.

Laisser le créateur dessiner. Le rétablissement du service et l’ouverture du Player sont observés ; le dessin lui-même reste à essayer par le créateur. Limite actuelle : aucun démarrage automatique des services trouvé, et aucune reconnexion proposée dans la bibliothèque après une panne. Après un nouvel arrêt des services, les relancer puis rouvrir le Player. Une correction ultérieure de l’interface devra distinguer panne réseau et invitation absente.

## Priorité D16.1 — essayer le sort récupéré

Le job joueur `a5c8847cc9a141098d6d8dbd9b5a460e`, « Foudre sous l’Enclume », est **ready depuis le 21 septembre à 07:33:56 Paris**. Le backend corrigé est publié et installé, API **7512**, worker **27356**. Le dernier J avait mal recopié le hash du plan ; le correctif général impose ces empreintes par schéma et permet de finaliser une construction admissible déjà évaluée. [Cause et mécanisme](D16_JUDGE_RECOVERY.md) · [Preuve réelle](../evidence/public/backend/visual-judge-fix-d16-1.json).

La reprise a sélectionné la révision **1**, score conservé **4190/10000**, `lifecycle_faithful=false`. **Toujours 8 appels et 3 dossiers de capture** : aucun nouveau modèle ou rendu lancé. La description, la planche et l’atlas d’origine sont conservés. Le troisième verdict invalide n’a pas été corrigé ni admis. Le Player 1.6.0 reste identique.

1. Dans le jeu, **Actualiser** ce parchemin puis lancer le sort et recueillir le verdict du créateur. Aucun besoin de redessiner pour cette reprise.
2. La fidélité visuelle reste insuffisante selon le critique ; `ready` signifie disponible, pas accepté artistiquement. Ne pas déclarer un rendu identique.
3. Les futurs J avec les empreintes imposées dans le schéma n’ont pas encore été appelés. Laisser les nouveaux essais au créateur ; aucun test ni diagnostic indépendant à lancer.

## Priorité D16 — laisser le créateur essayer un nouveau parchemin

Player et backend **1.6.0 construits, empaquetés et installés**, raccourci Bureau actualisé vers `WindowsAnimationSheetPlayable`. Build Unity et publication backend code 0 ; ZIP Player SHA `24ca0f949892fd3017889e8c0a1f23c48cd69f63125174a36951525739c5507f`. Migration 011 appliquée et renderer 1.6 installé le 21 septembre à **01:48 Paris**. Worker PID **30380**, API PID **6640** démarrés. [Preuve réelle D16](../evidence/public/backend/animation-sheet-d16.json).

La référence devient une planche stricte **3 × 7** avec APPARITION, STABLE, DISPARITION. Le checkpoint natif précède sa mise en page ; une reprise de cette étape conserve la génération déjà reçue. B et J utilisent la progression complète, et la description garde autorité sur les mécaniques. [Architecture](D16_ANIMATION_SHEET.md) · [Essayer](TESTER_MAINTENANT.md) · [Archive backend et empreinte](../evidence/public/backend/lifecycle-delivery.json).

1. Laisser le créateur ouvrir le raccourci Bureau, dessiner un **nouveau parchemin**, puis juger la planche et le sort dans le laboratoire.
2. Recueillir son verdict sur formation, mouvement continu et disparition. La conformité artistique des 21 scènes et la fidélité du rendu ne sont pas encore observées dans cette version.
3. En cas d’incident, lire les checkpoints du job réellement demandé. Ne pas modifier un sort par son nom ; corriger la chaîne générale concernée.

Aucun test, diagnostic modèle, génération ou capture indépendante exécuté pour D16. Ne pas en lancer à la place du créateur. Aucun sort existant n’a été modifié ou régénéré pour cette livraison.

## Historique — livraison D15 pour les futurs parchemins

**Player et backend 1.5.0 construits, empaquetés et installés**, migration 010 appliquée, raccourci Bureau actualisé. Services démarrés : worker 38748 / API 37400, observation à 01:17 Paris le 21 septembre. [Preuve réelle](../evidence/public/backend/behavior-d15.json) · [Architecture](D15_BEHAVIOR_AND_RESEARCH.md).

1. Laisser le créateur dessiner un nouveau parchemin et observer le sort dans le laboratoire ; recueillir son verdict sur description, placement, animation et disparition.
2. Si un problème survient, lire le job et ses checkpoints de description/image/recherche/plan/captures. Corriger le mécanisme général concerné ; ne pas modifier un sort par son nom ou son identifiant.
3. Ne pas lancer de tests, diagnostics, générations ni captures indépendantes : la consigne reste de laisser les essais au créateur. Aucun de ces essais n'a été exécuté pour D15.

Les anciens documents restent figés. Les jobs antérieurs sans description utilisent désormais le prompt A 2.3 et son schéma/catalogue historiques, évitant une migration implicite vers les nouveaux profils.

## Historique — affinage du sort repris en 1.4.1

L'essai utilisateur `7ed4561feffd43ebbafe48d9b960716d` a réussi A/G/B, puis échoué dans la capture : processus Unity code 0, **quatre PNG noirs identiques**, mesure **11915,2178 FPS** rejetée par la borne `10000`, état remonté `worker_exception`. L'image générée et le plan existent ; ce blocage ne justifie pas de les régénérer.

**Le correctif `1.4.1` est construit, empaqueté, déployé et ouvert**, PID `2140`, API `25220`, worker `21488`. Le [manifeste](../evidence/public/unity/lifecycle-delivery.json) atteste le ZIP de 44 367 955 octets, SHA-256 `15e056a3e21ae1549c3af857ca03cbdaf3a1394b4e0369511c9da77ce3489416`. Seul le job bloqué a été repris, avec dessin, description, image et plan conservés. Sa capture runtime s'est achevée avec quatre SHA distincts ; **aucun nouvel A/G/B initial**.

1. Laisser la reprise suivre son cours sans lancer de campagne indépendante. À **23:28:06 Paris le 20 septembre 2026**, le job est `refining_visuals`, J réussi avec **2250/10000** et `lifecycle_faithful=false`, correction B en cours. Il n'est pas encore `ready`. [Preuve et limites](../evidence/public/backend/lifecycle-capture-fix-2026-09-20.json).
2. Quand le sort devient disponible, laisser le créateur le lancer dans le laboratoire et recueillir son verdict. Une capture techniquement achevée et une critique réussie ne valent pas acceptation artistique ; le score actuellement observé ne permet pas d'annoncer la fidélité corrigée.
3. Consigner le résultat effectivement atteint et la latence. Ne pas régénérer l'image ou la description conservées, ne pas relancer de test, diagnostic ou capture indépendante.

## Historique — laisser le créateur tester D14 dans le jeu ouvert

**Le Player `1.4.0` est ouvert, PID `36512` ; le service D14 est déployé localement.** Build et packaging réussis, code 0 ; le [manifeste Player](../evidence/public/unity/lifecycle-delivery.json) atteste 29 fichiers, 122 857 102 octets et un ZIP de 44 366 062 octets, SHA-256 `98a3dd7315ea6f1c327ab7bba38b5464923e65ed19dd4a7ee4da8efe7895da36`. Migration `009` appliquée, renderer protégé installé, API `16768` et worker `26260` démarrés. [Preuve D14](../evidence/public/backend/lifecycle-2026-09-20.json). Le CoreSmoke déjà réussi précède la dernière consigne du créateur : **plus de tests, appels modèle ni captures de validation par le développement pour cette livraison**.

1. Laisser le créateur effectuer **Dessiner un parchemin → Dessin terminé → génération → laboratoire**, puis recueillir ses corrections. La description pilote apparition, activité, contact et expiration ; l'image sert de cible. Lors de cette génération runtime, le service doit produire les captures avec le renderer fixe et les soumettre à une nouvelle session J pour corriger uniquement les données visuelles bornées.
2. Partir d'un nouveau dessin : la bibliothèque locale a été vidée, **21 dossiers supprimés et sauvegarde définitivement supprimée sur demande** ; aucun historique serveur purgé.
3. Pour transmettre le projet, utiliser les archives actualisées de `deliverables/` et leur [manifeste backend](../evidence/public/backend/lifecycle-delivery.json). Les exécutables publiés sont ceux installés ; les documents et scripts opérateur ont été actualisés sans nouvelle génération.

**Aucun parcours joueur, appel modèle, capture ni validation artistique D14 n'est déjà acquis.** La revue J implémentée ne remplace ni l'observation de la fluidité et du son, ni le verdict humain. Voir [l'état D14](IMPLEMENTATION_STATUS.md). Les sections suivantes décrivent les livraisons précédentes.

## Historique — essayer D13 dans le jeu ouvert

**Le Player `1.3.0` est ouvert**, PID `39012`, fenêtre réactive, raccourci Bureau actualisé. Le backend D13 est déployé localement : API `30084`, worker `15760`, `/health/ready` prêt et diagnostic local sans issues de production A/B/G. [Preuve du déploiement](../evidence/public/backend/image-reference-deployment-2026-09-20.json).

Le [Player livré](../game/Build/WindowsImageReferencePlayable/Palimpseste.exe) contient **29 fichiers, 122 734 785 octets**. Son ZIP fait **44 320 872 octets**, SHA `0ca7d134f733632f23483726b70fbbeb1de750b046d1478efa94f18d4891ad7b` ; [manifeste](../evidence/public/unity/image-reference-delivery.json). Build final exit 0. Le manifeste a été promu après correction de `Replace($null)` et vérification du Player déjà lancé, sans second lancement.

1. Dans le jeu, **Dessiner un parchemin → Dessin terminé**, puis attendre **Description → Image du sort → Construction → Compilation → Sort**. Une génération peut prendre plusieurs minutes.
2. Cliquer **Lancer dans le laboratoire**, puis dans l'arène pour lancer vers une cible. Observer le résultat en mouvement face à l'image, écouter le son et recueillir le verdict du joueur. [Instructions courtes](TESTER_MAINTENANT.md).
3. Consigner ce nouveau parcours joueur, son téléchargement de référence et sa relecture hors ligne lorsqu'ils seront effectivement observés. Aucun nouveau parcours complet D13 n'est encore enregistré. Les anciens sorts conservés se rejouent sans génération rétroactive.

Preuves déjà acquises : CoreSmoke et DbSmoke réussis, migration 008 appliquée au runtime, quatre tests Unity du validateur passés ; capture 04 du vrai paquet réussie **1/1**, impact 1, dégâts 12000, impulsion 1, nettoyage complet et cache inchangé. [Capture et limites](../evidence/public/unity/image-reference-2026-09-20.md). Le dernier réglage du verre est postérieur à cette capture, non recapturé ; les contrôles techniques ne signent pas l'acceptation artistique.

Les [quatre appels réels de diagnostic](../evidence/public/backend/image-reference-2026-09-20.json) restent conservés : A Sol/high **34,091 s**, G Astra/high **55,784 s**, B1 **218,064 s** rejeté pour `turn_rate`, puis B2 **204,725 s** validé et compilé après correction du prompt `2.1`. B2 réutilise le même PNG G, sans nouvelle image. G a utilisé une description figée antérieure, distincte de la nouvelle sortie A : ces sondes ne constituent pas un job joueur complet. Le filtre natif `codex-image.exe` sert A/B/G ; les trois preuves au SHA durci sont promues. Aucun nouvel appel modèle, achat ou rechargement n'a été ajouté au déploiement.

L'accès distant/multijoueur, la recette des 30 dessins, le second créateur, l'autre machine et l'acceptation humaine restent ouverts. Les sections suivantes sont historiques.

## Historique — spectre magique livré, D12 / 1.2.2

Le [Player `1.2.2`](../game/Build/WindowsSpectralEnergyPlayable/Palimpseste.exe) est construit avec le code 0, empaqueté en **29 fichiers, 122 554 797 octets**, puis lancé sous le PID `13972`. Le raccourci Bureau est actualisé. Le [manifeste de livraison](../evidence/public/unity/spectral-energy-delivery.json) atteste le ZIP de **44 089 295 octets**, SHA-256 `6836f2cac5941a58d28b5a07c1b7f5e0b88cc5c51430320a6c9d9d199c1c542e`.

1. Rejouer **« Envol du spectre aux longs voiles »** depuis la bibliothèque pour apprécier la brume magique, les nappes animées et l'impact. Le paquet et la caméra du labo sont conservés ; aucun nouvel appel modèle n'est nécessaire.
2. Recueillir le verdict du créateur. Il a explicitement refusé l'itération 02, trop solide. Les itérations 03 et 04 remplacent ce corps par une présence magique ; **la fidélité « 1 pour 1 » et l'acceptation artistique restent ouvertes**. La [vidéo réelle du labo](../evidence/public/unity/spectral-fidelity/iteration-04/spectre-lab.mp4) et la [vue latérale de comparaison](../evidence/public/unity/spectral-fidelity/iteration-04/spectre-side-review.mp4) permettent de discuter du mouvement ; elles ne contiennent pas de son.
3. Si un défaut précis est signalé, reprendre cette partie du VFX puis la même capture. Les [preuves existantes](../evidence/public/unity/spectral-fidelity-2026-09-20.md) comprennent **5/5 tests à l'itération 03**, puis la capture finale **1/1** après le dernier réglage. Le sillage décoratif de 0,62 s ne prolonge pas les dégâts. Ne pas recommencer une campagne sans risque concret à résoudre.

Cette reprise ne valide pas l'ensemble des recettes ou styles, les performances sous charge, un nouveau parcours complet de génération, un autre poste ou l'accès des joueurs distants. Ces travaux et l'écoute humaine restent ouverts. Le backend demeure celui de D10.

## Historique — essayer le Player stylisé D11 livré

Le renderer `1.2.1` et sa [galerie finale](../evidence/public/unity/stylized-vfx-2026-09-20.md) sont vérifiés : neuf images de fixtures, six images du vrai spectre conservé avec une touche, 12 000 unités internes de dégâts et une impulsion. Les corrections ciblées ont été suivies d'une reprise de la galerie seule, réussie **1/1**. Les cinq autres contrôles ont réussi lors de la deuxième exécution ; ne pas transformer ces deux résultats en un passage unique 6/6 ni recommencer une campagne sans problème concret.

Le [Player `1.2.1`](../game/Build/WindowsStylizedVfxPlayable/Palimpseste.exe) a été construit avec le code 0, empaqueté en **29 fichiers vérifiés**, puis lancé sous le PID `30968`, fenêtre réactive. Le raccourci Bureau est actualisé. Le [manifeste](../evidence/public/unity/stylized-vfx-delivery.json) atteste le ZIP de **44 217 522 octets**, SHA-256 `8c92040e5abdbc8d690216010cafd9522f824dca7c9cd7db6aa42c4c82a32c0c`. La santé API répond 200.

1. Faire apprécier au créateur le rendu et le son de ses sorts conservés, qui profitent de cette finition sans régénération. Le backend demeure Sol/high → Astra/high ; cette évolution graphique n'a fait aucun appel modèle.
2. Recueillir un verdict sur des styles différents avant de déclarer toutes les combinaisons de recettes et d'apparences acceptées. Un nouveau parcours joueur dans le Player `1.2.1` reste à observer ; les fixtures ne remplacent pas ce parcours.
3. Conserver ouverts les essais humains des 30 dessins, le second créateur, l'autre poste Windows, le benchmark de charge et le déploiement HTTPS distant. Répéter seulement les contrôles correspondant à un problème concret ou à une validation demandée.

## Historique — essai du Player D10 livré

Le backend **A `gpt-5.6-sol` / `high`, prompt `2.2`, puis B `gpt-6-astra` / `high`, prompt `1.9` est déployé**. Le [diagnostic réel](../evidence/public/backend/sol-astra-active-2026-09-20.json) a validé et compilé le plan : **60,403 s d'appels**, sans réparation, environ 63 s pour le diagnostic complet. Le worker PID `19656` tourne sous `PalRuntimeSvc`, la santé HTTP répond 200 et la porte technique est approuvée. La [relecture du spectre conservé dans Unity](../evidence/public/unity/composed-vfx-2026-09-20.md) a produit six images successives et les **5/5 tests ciblés ont passé**. Ne pas refaire ces contrôles sans nouveau problème concret.

Le [Player Windows `1.2.0`](../game/Build/WindowsComposedVfxPlayable/Palimpseste.exe) a été construit, empaqueté en 29 fichiers vérifiés, puis lancé sous le PID `32064`. Le raccourci Bureau « Palimpseste Spell Lab » pointe sur ce build. La reconstruction finale a terminé avec le code 0 et sans avertissement shader trouvé. Voir [le manifeste de livraison](../evidence/public/unity/composed-vfx-delivery.json) et [les preuves Unity](../evidence/public/unity/composed-vfx-2026-09-20.md).

Travail restant :

1. Recueillir le verdict du créateur sur le spectre conservé : apparence, animation, impact et son.
2. Essayer un nouveau dessin complet dans ce Player pour relever séparément la lecture Sol, le plan Astra, le rendu et le délai vécu. La réussite du diagnostic technique ne remplace pas ce parcours joueur.
3. Compléter la recette humaine sur les 30 dessins inédits, avec le second créateur et un autre poste Windows ; ne pas déclarer ces essais exécutés par extrapolation.
4. Configurer et vérifier le déploiement HTTPS et l'admission des joueurs sur le serveur distant avant de déclarer un accès public fonctionnel. Le service livré est actuellement local.

La [comparaison de latence](../evidence/public/backend/sol-astra-latency-2026-09-20.md) porte sur un seul dessin conservé : environ 63 s pour le diagnostic D10 contre 285 s pour le job historique, avec des chemins et modèles différents. Elle ne garantit pas la durée des nouveaux dessins. Le [concept spectral](art-direction/spectral-veils-target.png) guide les assets ; aucun générateur d'image, test ou build logiciel n'est appelé pour chaque sort joueur.

## Historique — modèle 3D sémantique D09

Le créateur a choisi un sort qui **représente en volume l'objet interprété par Astra** : un dessin évoquant un rocher doit produire un rocher 3D propre, pas un volume reprenant mécaniquement le gribouillis. Voir [D09](DECISIONS.md). A `2.1`/B `1.8` et les contrats proposent 21 `visual_form`, une forme `appearance.form` vérifiée par le compilateur et une géométrie sémantique par sujet. Le smoke Core des 141 recettes / 705 porteurs et de cette géométrie, la compilation de la solution (0 avertissement, 0 erreur) et la QA contrats **61/61** ont réussi. Le renderer Unity a passé **5/5 tests PlayMode ciblés**, dont une [capture de fixture](../evidence/public/unity/semantic-forms-preview-20260920.png) ; le cache a passé **1/1 EditMode**. **A `2.1`/B `1.8` sont déployés**, doctor local code 0. Un [diagnostic A/B réel](../evidence/public/backend/semantic-visual-generation-2026-09-20.md) a compilé un golem `stone` avec rayon corrigé. Le [Player Windows 3D final](../game/Build/WindowsSemanticVfxPlayable/Palimpseste.exe) a été construit, empaqueté, puis lancé sur ce PC (PID `26864`, fenêtre réactive) ; aucun lancement de ce golem en jeu n'est encore attesté.

**Prochaine preuve :** sur le Player final déjà lancé, faire **un nouveau dessin joueur** et relever séparément la description d'Astra, le plan de Luna, le paquet, l'objet 3D vu dans le labo, le son et le verdict du créateur sur fidélité et VFX. Les anciens parchemins gardent leur ancien rendu ; le diagnostic compilé et la capture de fixture ne prouvent pas un nouveau sort joueur affiché. Le build final, son matériau d'émission et son ZIP contrôlé sont consignés dans [la preuve Unity](../evidence/public/unity/semantic-visuals-2026-09-20.md). Les 30 dessins inédits, le second créateur et l'autre PC restent à évaluer séparément.

## Reprise — variété des lectures Astra

Deux dessins récents distincts ont déjà abouti à deux jobs `ready`, avec titres proches mais sorts différents : `997d0b50-602a-4668-981e-a39108334bf4` → « Nœud de cuivre », projectile et `r_crochet_entravant` ; `e6d656f0-4a13-4ba5-9f44-3b4e7d2f6069` → « Nœud de cuivre mordant », piège et `r_estoc_saignant_lien`. Les SHA-256 des dessins et des descriptions diffèrent ; voir [le relevé](../evidence/public/backend/interpretation-repetition-2026-09-20.md). Le retour du créateur porte donc sur une répétition **créative** perceptible, pas sur une réutilisation du même job.

Le prompt A `2.0` a supprimé l'exemple de couleur susceptible de biaiser Astra et a été déployé. Un troisième job joueur réel, `7851777c-ddb1-49eb-84f2-641463b15364`, a ensuite atteint `ready` avec **« Le Nœud filant »**, `projectile`/`r_crochet_entravant` et palette `arcane` : le thème « nœud » persiste. A `2.1` ajoute les formes 3D et demande de ne pas assimiler automatiquement les croisements du croquis à des fils ou cordes ; il est déployé et a produit un golem dans un **diagnostic opérateur**, sans nouveau job joueur ni verdict de variété. Créer un **nouveau dessin** dans le Player D09, puis comparer le titre, les observations visuelles, la recette, le porteur et l'objet 3D au dessin et au verdict du créateur. Les trois anciens résultats demeurent immuables. Les sections suivantes sont historiques.

## Historique — dessin refusé par le quota de l'application

Le refus provenait du plafond de jobs sur 24 heures de Palimpseste (cinq jobs joueur constatés sur cinq permis), pas d'un diagnostic de l'abonnement Codex. Le correctif a enlevé ce plafond des deux créations de jobs et l'API avait répondu 200 après déploiement. **Au moment de ce correctif**, aucune capture HTTP réelle n'avait encore été retentée. Trois jobs `ready` sont observés depuis ; les deux premiers sont comparés dans [la preuve récente](../evidence/public/backend/interpretation-repetition-2026-09-20.md). Voir [le détail du quota](GENERATION_QUOTA.md).

Le compte Codex peut encore imposer sa propre limite d'usage ou une indisponibilité. Aucun achat ni recharge n'est déclenché par Palimpseste ; vérifier la recharge automatique dans l'interface du compte. Le service reste local sur `127.0.0.1`, sans accès public multi-joueurs. La section suivante décrit les 141 recettes déjà présentes avant ce correctif.

## Historique — catalogue de 141 recettes

La source contient [141 recettes nommées](../contracts/effect-recipes.json) bâties avec les 24 effets primitifs déjà intégrés au Player. Le smoke Core a validé et compilé **705 couples recette/porteur** ; la QA des contrats a passé **61/61**. Le choix d'une recette par Astra et son expansion exacte par Luna sont contrôlés par le compilateur. Le Player de cette étape était [WindowsEffectLibraryPlayable](../game/Build/WindowsEffectLibraryPlayable/Palimpseste.exe), avec 29 fichiers et six tests PlayMode ciblés réussis sur les familles d'effets. « Enclume filante » est `ready` après reprise de la même capture ; son lancement précis n'a pas encore été observé. Le Player 3D actuel est décrit en tête.

Le backend contenant les recettes a été déployé sur ce PC : API et worker sous `PalRuntimeSvc`, santé HTTP 200 et doctor local sans problème de production. L'archive opérateur a été régénérée. **Depuis ce déploiement**, trois jobs récents ont sélectionné des recettes et atteint `ready` ; le premier et le troisième partagent `r_crochet_entravant`, le second utilise `r_estoc_saignant_lien`. Leur lancement dans le laboratoire n'est pas attesté. Ne pas compter la compilation des 705 couples comme une recette humaine. Les décisions des créateurs sur fidélité, VFX et son, les 30 dessins inédits, le second créateur et l'autre PC restent ouverts. La section suivante est l'état historique antérieur aux recettes.

## Historique — dessin libre Astra → Luna

Le job **joueur** `cd4fec7e-aae8-45ba-b5f3-16a807a9771e` du dessin humain à deux traits est `ready` : Astra et Luna ont réussi, la fiche « Estoc à crochet » a été vue dans le Player et un lancement a été capturé dans l'ancien build. Le [nouveau Player VFX](../game/Build/WindowsAppearanceVfxPlayable/Palimpseste.exe) a été relancé sans interaction manuelle : ouvrir le sort conservé et comparer son rendu à la description Astra ; aucun verdict visuel humain sur ce build n'est encore enregistré. Les clics manuels répétés sur les cibles sont arrêtés. Vérifier ensuite son, physique et relecture hors ligne sur ce build. Le build Windows IL2CPP/URP a terminé avec le code 0, contient 29 fichiers contrôlés et son test PlayMode VFX/palette a passé 1/1. Les ZIP Player et backend de cette epoque ont été régénérés ; ce dernier est réservé à l'opérateur, avec installation sur un autre PC encore non testée. L'API et le worker avec prompts A `1.7` et B `1.3` sont déployés ; le doctor local ne signale aucun problème de production, mais aucune nouvelle génération réelle avec palette n'a encore été faite. Le job **synthétique** `769eea5bac2e414f8b9d0d3c17c397c1` demeure `needs_operator/plan_invalid` après deux réparations B rejetées ; il ne compte pas comme boucle réussie. La preuve Astra de calibration `A43D24B23AD0ABE1378B708C4DC47D6FB3C487451CB39CC10F56C47AD8519D77` est distincte du job joueur.

Les sections suivantes décrivent l'ancien dessin à trois régions et le build de reconnexion précédent ; elles sont conservées comme historique.

Mis à jour le 20 septembre 2026 après l'incident d'une nouvelle capture restée locale dans un Player lancé hors ligne.

Preuve détaillée : [essai joueur, build et relecture sans API](../evidence/public/unity/owner-player-end-to-end-2026-09-20.md).

## Incident du nouveau dessin et point de reprise

Le Player PID `35804` avait démarré pendant l'arrêt de l'API. Le nouveau dessin
fermé est conservé localement : `capture_pending`, `needs_begin=true`,
`needs_capture=true`, 151 entrées de journal. Malgré le retour de `/health/ready`
à 200 et le worker actif, aucun nouveau job n'existe pour cette capture.
L'écran « transmission de la capture en attente » n'est donc pas une longue
lecture Luna. Le code ajoute le bouton « Reconnecter », qui relance la session
du laboratoire puis rouvre ce même parchemin, et sa transmission peut alors
être retentée. Une relance du Player est également possible ; conserver le
même profil Windows et ses données locales. L'utilisateur accepte de
redessiner si nécessaire, mais la première action est de vérifier la reprise
de la trace conservée.

Unity a produit `game/Build/WindowsPlayerReconnectPlayable/` : 29 fichiers,
122 095 741 octets, hashes identiques au build brut, hors deux dossiers
`DoNotShip`. EXE SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2` ;
GameAssembly SHA-256
`E379AD0EC9CB79533E319BDEB97826E480BE55D614697A66F4183AB81669D75F`.
Le journal Unity SHA-256
`46AEEE27A7CE810FA399FC4BA3017B92F5043D17B85065327268EAA1EE1AEBC8`
contient `PALIMPSESTE_BUILD_OK` et « Application will terminate with return
code 0 », sans erreur `CS`. Le code de retour de la commande PowerShell
parente n'a pas été capturé. **Le nouveau Player et la reprise du dessin ne
sont pas encore testés.** Voir la [preuve de l'incident et du correctif](../evidence/public/unity/player-reconnect-pending-capture-2026-09-20.md).

## Chaîne exécutée

Le Player Windows a envoyé une vraie capture rouge-brun courbe, distincte du trait droit utilisé pour la calibration des prompts. Le job `production` de cette capture est `ready` : deux tentatives fournisseur réussies, une description A, un plan B et un `compiled_spell`, aucune tentative en échec. Le worker isolé sous `PalRuntimeSvc` a utilisé `codex exec` avec `gpt-5.6-luna` et l'effort `max` attesté par les contrôles préalables. Le Player a affiché A « Faisceau courbe de lave », téléchargé et vérifié la fiche, ouvert le laboratoire et compté un lancement (`Lancers : 1`, `Dégâts : 0`). Ce plan ne prévoit pas de dégâts.

Le correctif de visibilité du rayon à `0,6 s` a passé 2/2 tests PlayMode sur le **paquet du joueur**, puis Unity 6000.3.24f1 a terminé le build Windows IL2CPP/URP avec le code 0. Le build précédent testé est `game/Build/WindowsPlayerBeamVisibleReady/` : 29 fichiers, 122 093 149 octets ; EXE SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`, `GameAssembly.dll` SHA-256 `9FB651D09680CAF09B0A8668BEF2A914EFDE6B61F930EDBAE1CE90AF3B5C18C9`. Une capture de ce Player montre l'effet visible puis disparu.

L'API précédente (PID `25848`) a été arrêtée. Le Player BeamVisibleReady précédent a été relancé (PID `35804`) et la bibliothèque, la fiche, le laboratoire ainsi qu'un lancement du même sort sont restés accessibles **sans API**. L'API a ensuite redémarré sous `PalRuntimeSvc` (PID `16420`) ; `http://127.0.0.1:18080/health/ready` a répondu 200. Le worker PID `8180` est resté actif. Cet essai démontre la relecture locale lors d'une indisponibilité de l'API ; il ne mesure pas la qualité artistique ni l'écoute du son.

Une commande de confort pour fermer puis relancer le Player avec l'API restaurée a été refusée **avant exécution** par la revue automatique (`blocked by policy`, sans motif détaillé). Elle n'a pas été réessayée par un moyen équivalent. Le Player PID `35804` reste ouvert dans la session issue de l'essai sans API ; l'API PID `16420` répond 200 et le worker PID `8180` reste actif. Aucune relance du Player en ligne après cette restauration n'est revendiquée.

## Sécurité et preuves de génération

Le Doctor installé a pour SHA-256 `AAAAB2686CD9A33ADB6130B205D10596A63AF8207CBAE580ACD723C862196811`. Son diagnostic local après correction du BOM a réussi sous le compte de service, sans appel modèle ni `production_issues`. Le worker installé a pour SHA-256 `9243F9600973B1AAF5F97F07AD0983FF60A7EFA2EB0A7F7D42D142C05015847D` et son script enfant `03CE0108D67A4AC27B332A41F36F1F12B2B362FA9A2AEBCF25EB21E8BDB4C104`.

Le verrou technique `PALIMPSESTE_EFFORT_VERIFIED=true` référence le manifeste composite v2 SHA-256 `56F349016A5B63D352DD37225AF9253889D496C2399F58F97F70D0DAAC353E3F` dans `E:\PalimpsesteRuntime\approved-evidence`. Il lie trois rapports intégraux : appel A réel, B réel repris sur l'A figée et géométrie issue de l'encre, puis validation et compilation hors ligne de leurs octets. Les fichiers A/B/encre originaux et leurs hashes restent nécessaires au démarrage. Les ACL donnent au service la lecture/exécution des preuves et binaires, sans modification. `runtime.env` est en UTF-8 sans BOM ; `DATABASE_URL` est lue dans le coffre privé par le processus enfant, sans secret dans le dépôt, le Player ou la ligne de commande.

La première lecture A « faisceau de feu visuel » du trait de calibration a été rejetée par l'auteur. A `1.3` décrit un faisceau de lave sans cible ni dégâts ; B `1.1` repris sur la géométrie réelle a été validé et compilé. Ces diagnostics servent à la compatibilité technique et restent distincts du job joueur. Le verdict humain de fidélité sur A `1.3`, puis celui sur la nouvelle capture courbe et son sort, ne sont **pas enregistrés**. Le passage du job à `ready` n'est pas un accord artistique.

Le laboratoire reste privé sur `127.0.0.1` avec invitation propriétaire ; aucun proxy Codex public n'est ouvert. Les migrations 003 à 006 et les contrôles d'accès sont en place. Le compte Codex commun n'a déclenché aucun achat ou rechargement par le code ; l'état des réglages de recharge du compte n'est pas vérifié. Les [conditions OpenAI Europe](https://openai.com/fr-FR/policies/eu-terms-of-use/) et la [documentation Codex](https://developers.openai.com/fr-FR/docs/auth) demandent une clarification de l'usage de ce compte individuel pour des tiers avant toute ouverture à plusieurs joueurs. Cette lecture de leur application au backend partagé est une inférence à confirmer.

## Travaux restants

1. Lancer le build `WindowsPlayerReconnectPlayable` dans le même profil Windows, ouvrir le dessin local en attente et essayer « Reconnecter », puis « Transmettre » si proposé. Vérifier qu'un nouveau job est créé et que la lecture A apparaît. En cas d'échec, conserver le journal local et relever le message exact avant de redessiner. Aucune réussite de cette reprise n'est encore attestée.
2. Faire écouter le son et recueillir les verdicts des créateurs sur le texte A, le rendu, l'intérêt du sort et l'expérience de dessin ; enregistrer ces décisions séparément de la preuve technique.
3. Exécuter la recette finale sur les 30 dessins inédits, avec le second créateur et une machine Windows propre. Le seul job propriétaire réussi ne satisfait pas M7.
4. Vérifier l'installation et le parcours sur un autre poste, puis décider de la livraison publique, de l'HTTPS et du cadre d'usage du compte avant ouverture à des joueurs externes.
5. Livrer le dossier Windows actuel. La création du ZIP actualisé a été refusée avant exécution par la revue automatique (`blocked by policy`) ; les ZIP existants restent historiques. Le démarrage de l'API, lui, fonctionne via `ops/start-owner-api.ps1` sans secret dans l'appel de l'outil.

Les détails du worker et les hashes des preuves sont dans [OWNER_WORKER_CUTOVER.md](ops/OWNER_WORKER_CUTOVER.md). Le parcours sur ce PC est dans [TESTER_MAINTENANT.md](TESTER_MAINTENANT.md). Les statuts par ticket et limites sont dans [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md).
