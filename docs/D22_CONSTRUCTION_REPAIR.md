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

## Complément D22.2 — reprise manuelle après le délai de construction

L'appel réel `89f18b6bd6874121b5c956db8d5f4440` a expiré après 1800 secondes le 25 septembre à 12:06:27 Paris, sans candidat. La revue a trouvé un défaut d'attente : le runner attendait la sortie du processus avant d'observer les erreurs des lecteurs. Un flux dépassant sa limite pouvait donc cesser d'être vidé et bloquer le processus. Ce défaut est établi dans le code ; sa présence dans cet appel historique n'est pas démontrée, car ses diagnostics partiels n'étaient pas conservés.

Le runner surveille désormais ensemble l'envoi du prompt, les deux lecteurs et la sortie du processus. Une erreur est observée immédiatement ; l'arrêt de l'arbre et le nettoyage sont bornés. Les échecs conservent des diagnostics privés limités à 16 Ko et leurs métadonnées même après annulation. Une violation d'attestation ou un refus déjà observé reste protégé ; une terminaison non confirmée reste `TransportUncertain`. Aucun seuil de flux ni droit du worker n'est relâché.

Le GET du job peut proposer au propriétaire authentifié une reprise après un `attempt_timeout` de B effectivement terminé. Le POST de reprise existant vérifie de nouveau, sous verrou du job, la dernière tentative terminée avec le même fence, l'absence de sortie, de tentative active ou incertaine, de bail et de publication, les documents conservés, la politique courante, une place libre dans la fenêtre et le budget restant. Il ne lance aucune reprise automatique et ne change ni fenêtre, ni version, ni compteur, ni historique ; le prochain appel consomme normalement une tentative.

La provenance historique est le marqueur écrit par le worker de confiance : l'ancien runner ne retournait `attempt_timeout` qu'après expiration locale, arrêt du processus et attente de sa sortie. Son `attempt.json` pouvait rester incomplet sur cette branche ; aucune attestation sur disque n'est inventée rétrospectivement. Une annulation externe, un transport incertain, un refus ou une violation d'isolation n'est pas admis par ce chemin. Cette modification du code ne prouve pas son déploiement ni une reprise réelle ; ces opérations sont consignées séparément.

## Complément D22.3 — identité normale du joueur

D22.2 s’est installé à 12:13:20 Paris. Le GET du jeu restait non reprenable parce que le helper exigeait à tort le rôle `creator`. La lecture réelle des données confirme que le propriétaire utilise `player`, comme le parcours Unity normal. D22.3 retire seulement cette exigence supplémentaire : le job doit toujours appartenir au principal authentifié et satisfaire toutes les conditions de fin certaine, d’intégrité, de budget et de fenêtre. Aucun rôle ni droit du compte n’est changé. La requête d’admissibilité exacte a été lue sur le job arrêté et a renvoyé vrai avant cette publication ; aucun job n’a été remis en file par cette lecture.
