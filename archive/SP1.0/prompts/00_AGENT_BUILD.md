# Mission de réalisation · PALIMPSESTE SP-1.0

Tu dois réaliser la version finale d’un sous-système de jeu, pas un prototype : dessin de parchemin → vraie interprétation multimodale → description → plan de sort → compilation contrôlée → sort réellement lançable dans Unity. Ne développe pas le reste d’un RPG.

## Lecture préalable obligatoire

Lis `docs/01_CAHIER_DE_REALISATION.md`, `docs/02_ANNEXE_CONTRATS.md`, les schémas de `contracts/`, le catalogue, les prompts A/B et `examples/README.md`. Les exemples sont manuels et ne doivent jamais être présentés comme un résultat de modèle ou de Unity. La QA de ce dossier vérifie des contrats, pas un moteur déjà implémenté.

Unity est choisi. Les brouillons, gomme, annulation, presets de sorts, confirmations après découverte du résultat et rerolls joueur sont exclus. Le modèle lit réellement l’image complète, avec une image de référence. Les pinceaux comptent par leur trace, pas par un rôle tactique codé. Une interprétation peut donner un mauvais sort ; une panne technique ne doit jamais être masquée par un sort de secours.

## Architecture à construire

Client Unity 6.3 LTS / URP Windows x64. Serveur ASP.NET Core .NET 10. Noyau partagé compatible .NET Standard 2.1. Modules A et B séparés et inspectables. Compilateur déterministe. Runtime Unity sans génération de code libre. Stockage durable et reprise de tâches. Six porteurs et tous les effets du catalogue sont inclus dans la livraison finale.

La version exacte d’éditeur, les packages, les bibliothèques de sérialisation et la chaîne de build sont vérifiés puis verrouillés. Ne suppose pas que .NET 10 est le runtime de Unity. Ne charge aucune DLL ou shader venant d’un modèle. Ne livre pas seulement des scripts exigeant de reconstruire les scènes manuellement.

## Méthode de travail

Commence par un audit concret de l’environnement : fichiers disponibles, Unity réellement installé ou absent, version et licence de build, outils .NET, possibilité de construire Windows et accès au fournisseur. Distingue « prévu », « implémenté », « compilé », « testé dans Unity », « testé avec un vrai appel » et « accepté par les créateurs ».

Respecte l’ordre M0 à M7 du cahier. Les tâches techniques indépendantes peuvent avancer, mais ne transforme jamais un jalon humain en auto-validation. À chaque jalon, fournis le build ou outil lançable, les changements, les commandes, les preuves obtenues, les défauts et le test précis que les créateurs doivent effectuer. Ne t’arrête pas à la description textuelle ou à une scène de sphères génériques : l’objectif final inclut géométrie, collisions, effets, rendu, son, sauvegarde et reprise.

Toute fonction exposée au modèle doit être réellement implémentée. Une fonction manquante reste une tâche ouverte ; elle ne devient pas un succès de test en étant remplacée par un mock. Les mocks sont autorisés seulement dans les tests explicitement nommés et ne valident pas le chemin produit.

## Tests et qualité

Réalise tests de contrat, tests unitaires du noyau, tests d’intégration backend, tests de scène, build autonome, appels réels et cas de panne. Garde un corpus de validation inédit distinct des dessins utilisés pour régler les prompts. L’agent ne rédige pas le verdict esthétique ou de gameplay à la place de l’équipe. Des sorties JSON valides ne prouvent pas une bonne lecture du dessin.

La géométrie doit venir des pixels et agir réellement sur au moins une dimension majeure du sort. Une simple couleur ou un nom différent n’est pas une variation suffisante. La description factuelle finale doit dériver des règles compilées. Les signatures sont figées et les sorts déjà créés se lancent sans nouvel appel fournisseur.

## Remise attendue

Un dépôt complet, un client Windows autonome, le backend installable, les assets et scènes, la configuration d’exemple sans secrets, les migrations, les scripts de build/test, la procédure de restauration, les exports de cas et les défauts résiduels. Fournis une preuve de création depuis un dessin inédit, de lancement, de relance hors ligne, et de reprise après panne sans deuxième support ou publication.

Ne déclare pas le produit final « validé » avant les essais humains requis. Si ton environnement ne permet pas un test, indique ce qui n’a pas été exécuté et prépare le test exact ; n’invente ni rapport Unity, ni benchmark, ni appel réel. Continue les travaux réalisables sans prétendre que les preuves manquantes existent.
