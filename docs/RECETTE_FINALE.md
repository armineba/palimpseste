# Recette M7 — dossier à remplir par les créateurs

Ce protocole applique le cahier et `contracts/human-review.schema.json`. Il ne constitue aucune approbation. Le 20 septembre 2026, l'utilisateur a rejeté dans la conversation la lecture A « faisceau de feu visuel, sans cible ni dégâts » d'un trait rouge-brun diagonal. Ce verdict oriente le recalibrage ; il n'a pas été soumis comme revue signée à l'API et ne vaut pas campagne M7.

## Préparer la série

1. Authentifier le compte Codex dédié au worker, puis exécuter le doctor actif avec les deux images de la capture Unity. Garder le JSON complet, son SHA-256 et les dossiers de tentative hors du dépôt public. Ne lever la porte `PALIMPSESTE_EFFORT_VERIFIED` qu'après vérification du modèle `gpt-5.6-luna`, de l'effort `max` **retournés** et des restrictions d'outils observées pour A et B.
2. Figer un commit, les hashes des deux archives, les versions Unity/Codex, les prompts A/B, les contrats et le catalogue. Consigner ces identifiants dans le dossier privé de recette. Toute modification de l'un d'eux ouvre une nouvelle campagne.
3. Conserver séparément 30 dessins de conception et 30 dessins inédits de recette. Dans ces derniers, prévoir dix paires de deux auteurs à intention proche, cinq dessins maladroits ou partiels et cinq compositions à plusieurs étapes. Enregistrer l'intention avant de lire la description de A. Ne pas entraîner ou ajuster les prompts sur les 30 cas inédits.

Le cas rejeté sert au corpus de conception : recueillir d'abord l'intention
attendue, corriger le prompt A dans une **nouvelle version**, comparer au plus
deux configurations sur les mêmes dessins, puis archiver le nouvel appel et
son verdict. Le formulaire de retour du laboratoire recueille un avis ; il
ne modifie pas automatiquement le prompt, ni un parchemin déjà figé.

## Pour chaque cas inédit

1. Capturer le dessin via le lecteur Windows livré, avec la référence et le journal. Conserver les SHA-256 des images, du journal et de la requête API, le `parchment_id`, le `job_id` et les réponses HTTP. Vérifier que le job va jusqu'à une publication unique et que les deux tentatives Codex viennent du compte de service.
2. Archiver la description A figée, le plan B, la banque géométrique, le paquet compilé, les rapports de validation et leurs hashes. Si une étape échoue, enregistrer l'incident et la reprise ; ne pas transformer l'échec en sort prétendument réussi.
3. Télécharger le paquet, couper l'API et le réseau, rouvrir le lecteur, lancer le sort dans la scène d'épreuve et enregistrer vidéo ou captures ainsi que le journal des effets, collisions, rendu et son. Tester un second chargement hors ligne du paquet immuable.
4. Faire juger le cas séparément par deux créateurs. Le propriétaire créateur délègue d'abord le couple exact sort/cas via `POST /v1/spells/{id}/reviewers`, en indiquant le `principal_id` du second compte obtenu lors de `token-create creator`. Le second créateur soumet ensuite `POST /v1/reviews` avec son propre jeton et son `reviewer_id` authentifié ; le propriétaire retire la délégation après la recette avec `DELETE /v1/spells/{id}/reviewers/{reviewerId}?case_id=...`. Chaque revue doit indiquer le dessin, la fidélité, les règles, la signature visuelle, la présentation, les capacités promises mais absentes et un commentaire. Le champ `submitted_by_human` est renseigné par la personne ; aucun agent ne doit l'envoyer à sa place. Une correction crée une nouvelle revue immuable.

La délégation est limitée au cas et au sort enregistrés ; elle n'accorde aucune lecture générale des sorts et la route de lecture du sort reste propriétaire uniquement. Le paquet de revue doit donc être remis au second créateur par le dossier de recette ou un canal opérateur contrôlé. `reviewer_id` est lié au principal authentifié et ne peut plus être choisi pour usurper l'autre compte. Une ligne de revue synthétique ne vaut toujours pas validation humaine.

## Décision

Les seuils proposés dans le cahier exigent zéro capacité promise mais absente, au moins 24 cas sur 30 cohérents avec l'image selon **les deux** créateurs, et aucune paire différenciée seulement par titre ou identifiant. Vérifier ces comptes sur les revues et les artefacts, puis consigner un verdict signé pour M0 à M7. Les seuils restent à accepter explicitement par l'équipe ; le logiciel ne peut pas s'autoattribuer cet accord.

En plus des 30 cas, exécuter la matrice de coupures, de restauration, de corruption, de collisions et de résolutions du chapitre 21 du cahier. Installer les deux ZIPs sur une autre machine Windows propre et vérifier la licence Unity, les dépendances de lancement, les permissions et le mode hors ligne. Reporter toute absence de preuve dans `docs/IMPLEMENTATION_STATUS.md` avant de prononcer B29/B30 ou M7 achevé.
