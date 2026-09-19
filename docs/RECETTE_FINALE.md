# Recette M7 — dossier à remplir par les créateurs

Ce protocole applique le cahier et `contracts/human-review.schema.json`. Il ne constitue aucune approbation : aucun cas issu de Luna ni verdict humain n'a été enregistré au 19 septembre 2026.

## Préparer la série

1. Authentifier le compte Codex dédié au worker, puis exécuter le doctor actif avec les deux images de la capture Unity. Garder le JSON complet, son SHA-256 et les dossiers de tentative hors du dépôt public. Ne lever la porte `PALIMPSESTE_EFFORT_VERIFIED` qu'après vérification du modèle `gpt-5.6-luna`, de l'effort `max` **retournés** et des restrictions d'outils observées pour A et B.
2. Figer un commit, les hashes des deux archives, les versions Unity/Codex, les prompts A/B, les contrats et le catalogue. Consigner ces identifiants dans le dossier privé de recette. Toute modification de l'un d'eux ouvre une nouvelle campagne.
3. Conserver séparément 30 dessins de conception et 30 dessins inédits de recette. Dans ces derniers, prévoir dix paires de deux auteurs à intention proche, cinq dessins maladroits ou partiels et cinq compositions à plusieurs étapes. Enregistrer l'intention avant de lire la description de A. Ne pas entraîner ou ajuster les prompts sur les 30 cas inédits.

## Pour chaque cas inédit

1. Capturer le dessin via le lecteur Windows livré, avec la référence et le journal. Conserver les SHA-256 des images, du journal et de la requête API, le `parchment_id`, le `job_id` et les réponses HTTP. Vérifier que le job va jusqu'à une publication unique et que les deux tentatives Codex viennent du compte de service.
2. Archiver la description A figée, le plan B, la banque géométrique, le paquet compilé, les rapports de validation et leurs hashes. Si une étape échoue, enregistrer l'incident et la reprise ; ne pas transformer l'échec en sort prétendument réussi.
3. Télécharger le paquet, couper l'API et le réseau, rouvrir le lecteur, lancer le sort dans la scène d'épreuve et enregistrer vidéo ou captures ainsi que le journal des effets, collisions, rendu et son. Tester un second chargement hors ligne du paquet immuable.
4. Faire juger le cas séparément par deux créateurs via `POST /v1/reviews` avec le jeton `creator` du propriétaire du sort. Chaque revue doit indiquer le dessin, la fidélité, les règles, la signature visuelle, la présentation, les capacités promises mais absentes et un commentaire. Le champ `submitted_by_human` est renseigné par la personne ; aucun agent ne doit l'envoyer à sa place. Une correction crée une nouvelle revue immuable.

La route actuelle rattache la revue au compte propriétaire du sort : elle ne prouve pas que deux personnes différentes ont utilisé ce compte. `reviewer_id` et la procédure de recette doivent être vérifiés par l'équipe ; la séparation de jetons de relecteurs n'est pas implémentée. Ne pas traiter deux lignes issues du même compte comme une double validation humaine sans cette vérification.

## Décision

Les seuils proposés dans le cahier exigent zéro capacité promise mais absente, au moins 24 cas sur 30 cohérents avec l'image selon **les deux** créateurs, et aucune paire différenciée seulement par titre ou identifiant. Vérifier ces comptes sur les revues et les artefacts, puis consigner un verdict signé pour M0 à M7. Les seuils restent à accepter explicitement par l'équipe ; le logiciel ne peut pas s'autoattribuer cet accord.

En plus des 30 cas, exécuter la matrice de coupures, de restauration, de corruption, de collisions et de résolutions du chapitre 21 du cahier. Installer les deux ZIPs sur une autre machine Windows propre et vérifier la licence Unity, les dépendances de lancement, les permissions et le mode hors ligne. Reporter toute absence de preuve dans `docs/IMPLEMENTATION_STATUS.md` avant de prononcer B29/B30 ou M7 achevé.
