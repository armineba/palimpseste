# D22 — corriger la construction sans perdre son contexte

## Causes observées sur le parcours réel

Le job `5e763870284e41679c6b850b9b02ceff` a dépassé les erreurs de schéma D21. Codex a produit un plan compilable et le renderer a réellement capturé sa structure. La critique indépendante a refusé la silhouette, lue comme cinq lobes empilés. La révision suivante proposait un ruban hélicoïdal, que la matrice `area` du serveur interdisait alors que le Player sait l'exécuter. Le message de refus ne donnait aucun choix autorisé. La dernière révision est repartie du plan ancien et a perdu la proposition corrigée et le verdict visuel.

Cela associe un refus visuel réel à trois défauts du système : capacités mal décrites, combinaison valide interdite et contexte de correction perdu. Aucun verdict négatif n'est transformé en réussite.

## Corrections générales

- La matrice et les limites géométriques proviennent du même validateur que la compilation. Elles sont transmises au constructeur avec des erreurs précises. `vortex_surface` est une surface de révolution complète ; `ribbon` suit réellement un chemin XYZ avec largeur. Il peut former une hélice ouverte, mais ne crée pas automatiquement des dalles volumétriques indépendantes.
- `area → ribbon` est admis parce que le renderer, les déformations et les collisions contrôlées le prennent déjà en charge. La collision reste la boîte du champ. Continuité, taille, budget et critique restent obligatoires. Aucun asset ni shader nouveau revendiqué.
- La correction transmet le brouillon effectivement refusé, y compris son dossier de méthodes, séparément du plan validé qui sert de verrou. Son hash et la critique précédente sont conservés dans la passe refusée pour une reprise identique. Le brouillon n'est jamais exécuté directement.
- Les versions de prompt sont `sp.prompt.blueprint/2.2` et `sp.prompt.blueprint/2.3` avec UNITY GOD. Les reçus historiques 2.1 restent contrôlés ; la publication exige également le reçu 2.3.
- Le contexte JSON est compacté sans suppression de champs ; le plafond du worker reste inchangé.

## Reprise après correction du logiciel

La migration 016 ajoute une version de construction et une fenêtre de révisions au job. Une reprise propriétaire n'est proposée qu'après changement de cette version, avec un plan conservé, aucune publication ni tentative en cours/incertaine et un historique de construction terminé. Elle ajoute quatre révisions après les précédentes. Aucun ancien enregistrement ni compteur n'est effacé ; le budget total reste 32 tentatives fournisseur.

Pour ce job, les révisions 0..3 restent intactes et la nouvelle fenêtre est 4..7. Une nouvelle relance sous la même version du constructeur ne peut pas recommencer indéfiniment les mêmes échecs. Les contrôles artistiques et techniques continuent à déterminer la publication.

## Livraison et limites

Player 1.8.0 réutilisé : le contrôle de compatibilité modifié appartient au backend, pas au lecteur de paquet Unity. Compilation, installation, reprise et résultat réel sont consignés séparément dans `evidence/public/backend/construction-repair-d22*.json` et `NEXT_ACTIONS.md`. Aucun diagnostic modèle ou nouveau dessin de démonstration n'est lancé. Les captures et critiques éventuelles appartiennent au traitement du parchemin du créateur. Son acceptation visuelle reste distincte de tous ces contrôles.

## Complément D22.1 — prévenir les mauvaises reprises et intentions impossibles

La nouvelle fenêtre restaure aussi les mesures du rendu conservé, après vérification de leur liaison au même plan. Elle emploie le même routage que la boucle normale : impact refusé vers `physics`, performances insuffisantes vers `optimization`, sinon l'étape explicitement demandée par la critique. Une critique complète sans demande de retour ne remet plus le core en chantier par défaut. Les mesures absentes ou incohérentes ne sont pas remplacées par une supposition.

L'audit d'un autre parchemin (`c05961d1e0a74377a8567664f37f64c6`) a révélé une intention hors des contrôles disponibles : un serpent unique en trois tronçons était représenté par trois copies entières du même serpent. `controlled_swarm` répète le même chemin à des offsets elliptiques ; il ne découpe pas un corps longitudinalement. Les trois critiques de structure ont réellement refusé cette construction. Aucune réussite n'est revendiquée pour ce sort et une simple relance ne lui ajouterait pas cette capacité.

Le contexte fourni décrit donc chaque core réellement implémenté et ses limites. Les futures interprétations A reçoivent également ces capacités pour proposer dès la description un phénomène réalisable, en conservant l'inspiration du dessin. Les descriptions et paquets existants restent figés. Cela ne constitue ni un nouveau moteur géométrique ni une garantie automatique de qualité visuelle.
