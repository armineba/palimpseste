# Décisions de réalisation

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
