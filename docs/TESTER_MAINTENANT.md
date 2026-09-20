# Tester le lecteur actuel

## Sur ce PC, maintenant

Lancer `game/Build/WindowsPlayerLunaAfterimagePlayable/Palimpseste.exe`. Ce
nouveau lecteur Windows x64 IL2CPP/URP a été construit avec Unity 6000.3.24f1
et lancé hors ligne pendant 12 secondes : processus répondant, D3D12 et PhysX
initialisés, aucune exception relevée. Le parcours d'invitation testé dans le
Player précédent doit afficher « Accès au laboratoire nécessaire », une
bibliothèque vide et « Ouvrir mon invitation » ; la fenêtre du nouveau build
n'a pas été examinée visuellement lors de son lancement caché. Il n'y a plus
de champs « Service » ou « Jeton privé », ni de créations locales de
démonstration dans le code du parcours. Voir la [preuve du build actuel](../evidence/public/unity/real-luna-player-build-2026-09-20.md).

Le fichier `game/Build/WindowsPlayerLunaAfterimagePlayable/Palimpseste_Data/StreamingAssets/service.json` ne contient pas encore
d'adresse de service public, et aucune invitation joueur n'est livrée dans le
dépôt. **Le dessin suivi d'un sort Luna n'est donc pas testable dans ce Player
seul aujourd'hui.** Une sonde réelle sous le compte Codex du service a obtenu
une interprétation textuelle A puis un plan B, tous deux avec le modèle
`gpt-5.6-luna` et l'effort `max` rapportés. Une vérification séparée a compilé
ce plan à partir de l'encre réelle avec des identifiants d'artefacts synthétiques.
Ce paquet a aussi été lancé dans un test Unity PlayMode isolé `-nographics`
(1/1 passé : faisceau deux points, source audio créée, zéro dégât). Une
rémanence de 0,16 s conserve uniquement le graphisme après le tick logique ;
un test Direct3D 12 a mesuré ses pixels sur une caméra isolée, puis leur
disparition. La visibilité depuis la caméra du Player et l'écoute du son ne
sont pas encore prouvées. La preuve A/B attend la revue humaine. Cette
sonde n'a pas traité un job joueur, publié ce paquet ni exécuté son sort dans
le Player IL2CPP ; le
parcours Unity connecté n'a pas été exécuté.

## Parcours à vérifier après ouverture contrôlée du laboratoire

L'ouverture à plusieurs joueurs exige d'abord la clarification du type de
compte et de l'autorisation d'usage partagé décrite dans
[NEXT_ACTIONS.md](NEXT_ACTIONS.md). Aucun service public n'est ouvert ici.

L'opérateur déploie l'API HTTPS et le worker isolé après contrôle des migrations 003 et 004,
inscrit l'adresse du service dans chaque fichier d'invitation privé et le remet
au joueur. Dans le jeu, le joueur choisit ce fichier une seule fois,
dessine sur le parchemin, termine la capture, lit l'interprétation textuelle de
Luna A, puis ouvre le sort validé dans le laboratoire. Un sort téléchargé doit
rester accessible hors ligne au même joueur. Les instructions opérateur figurent
dans [SETUP.md](SETUP.md) ; la marche à suivre du joueur est dans
[MANUEL_JOUEUR.md](MANUEL_JOUEUR.md). La recette et ses verdicts humains restent
à enregistrer dans [RECETTE_FINALE.md](RECETTE_FINALE.md).
