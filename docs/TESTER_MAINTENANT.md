# Tester le lecteur actuel

## Sur ce PC, maintenant

Lancer `game/Build/WindowsPlayerFlowOwnerFinal/Palimpseste.exe`. Le lecteur Windows
x64 IL2CPP construit avec Unity 6000.3.24f1 doit afficher « Accès au laboratoire
nécessaire », une bibliothèque vide et « Ouvrir mon invitation ». Le bouton ouvre
le sélecteur de fichier Windows ; « Annuler » revient au jeu. Cette ouverture a
été vérifiée dans le Player brut. Il n'y a plus de champs « Service » ou « Jeton
privé », ni de créations locales de démonstration visibles.

Le fichier `game/Assets/StreamingAssets/service.json` ne contient pas encore
d'adresse de service public, et aucune invitation joueur n'est livrée dans le
dépôt. **Le dessin suivi d'un sort Luna n'est donc pas testable dans ce Player
seul aujourd'hui.** La dernière sonde réelle a produit une description A conforme
mais refusée faute de preuve du modèle/effort effectifs ; B et le parcours Unity
connecté n'ont pas été exécutés.

## Parcours à vérifier après ouverture contrôlée du laboratoire

L'opérateur déploie l'API HTTPS et le worker isolé après contrôle des migrations 003 et 004,
inscrit l'adresse du service dans chaque fichier d'invitation privé et le remet
au joueur. Dans le jeu, le joueur choisit ce fichier une seule fois,
dessine sur le parchemin, termine la capture, lit l'interprétation textuelle de
Luna A, puis ouvre le sort validé dans le laboratoire. Un sort téléchargé doit
rester accessible hors ligne au même joueur. Les instructions opérateur figurent
dans [SETUP.md](SETUP.md) ; la marche à suivre du joueur est dans
[MANUEL_JOUEUR.md](MANUEL_JOUEUR.md). La recette et ses verdicts humains restent
à enregistrer dans [RECETTE_FINALE.md](RECETTE_FINALE.md).
