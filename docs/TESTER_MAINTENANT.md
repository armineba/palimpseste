# Tester le lecteur actuel

## Sur ce PC, maintenant

Lancer `game/Build/WindowsPlayerBeamGeometryReady/Palimpseste.exe`. Ce lecteur
Windows x64 IL2CPP/URP a été construit avec Unity 6000.3.24f1. Il contient
29 fichiers, 122 093 149 octets, et le shader `LavaBeam` dans
`resources.assets` ; l'exécutable a pour SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`,
`GameAssembly.dll` pour SHA-256
`200BB3A65EC799D316C769D54CDB4F03A90BB7627B58B22FECA8A2CFEEB81A9E`.
Cette copie de dossier est le build actuel ; aucun ZIP correspondant n'a été
créé.
L'API propriétaire locale tourne sous `PalRuntimeSvc` sur
`http://127.0.0.1:18080` ; `/health/ready` a répondu 200. Le Player actuel a
été ouvert en fenêtre visible, PID 35552 répondant au contrôle. La session
conservée et l'écran « Capture reçue / En file d'attente » ont été observés
dans le Player LavaReady précédent pour une vraie capture propriétaire reçue
par l'API. La base contenait un job
`production` `queued` et quatre artefacts : référence, dessin, encre et
journal. Ce dessin est une trace rouge-brun **courbe**, différente du trait
droit utilisé pour calibrer A `1.3`. Aucun appel Luna n'a encore traité ce
job. Voir les [preuves du build actuel](../evidence/public/unity/beam-geometry-luna-build-2026-09-20.md)
et de la [capture mise en file](../evidence/public/unity/owner-player-capture-queued-2026-09-20.md).

Pour juger le **trait droit de calibration** (distinct du dessin courbe en
file), la nouvelle sortie A `1.3` dit : « Un faisceau rectiligne de matière
rouge-brun évoquant la lave est émis suivant l’axe de la trace dans la
couronne. Aucun effet de contact ni cible ne sont justifiés. » Le
[rendu Unity isolé du même plan](../evidence/public/unity/lava-beam-ab13-b11.png)
montre le faisceau avec son shader de lave. Cette image et le texte attendent
encore le verdict de l'auteur ; ils ne prouvent pas un sort publié dans le
Player.

Il n'y a plus de champs « Service » ou « Jeton privé », ni de créations locales
de démonstration dans le parcours. Le formulaire « Signaler une lecture
incorrecte » est dans ce build ; son envoi **depuis Unity** n'a pas été testé.
Sa route API a passé 9/9 contrôles HTTP et base synthétiques, avec nettoyage
vérifié et sans appel Luna ; voir la
[preuve du smoke](../evidence/public/backend/interpretation-feedback-owner-lab-2026-09-20.md).

Le fichier `game/Build/WindowsPlayerBeamGeometryReady/Palimpseste_Data/StreamingAssets/service.json`
ne contient pas d'adresse de service public, et aucune invitation joueur n'est
livrée dans le dépôt. **Le dessin suivi d'un sort Luna n'est pas encore testable
de bout en bout** : le worker FINAL reste non installé et arrêté, avec
`PALIMPSESTE_EFFORT_VERIFIED=false`. Le job courbe du Player est en file et n'a
reçu aucun appel A/B. La lecture A `1.3` du trait droit de calibration attend
encore votre verdict.

Pour ce trait droit, A `1.3` et B `1.1` ont été obtenus réellement sous le
compte Codex du service, avec `gpt-5.6-luna` et `max` rapportés. Le premier B
de cette configuration a été refusé pour géométrie provisoire. Une reprise B
seule sur la A figée et la géométrie issue de l'encre a produit un plan visuel
sans cible ni effet, validé et compilé en mémoire. Le doctor FINAL a de nouveau
validé et compilé hors ligne les octets A/B réels et l'encre sous
`PalRuntimeSvc`, sans appel modèle ; rapport privé SHA-256
`D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA`.
Cette validation utilise des identifiants de contrôle synthétiques. La fixture
de géométrie a passé ses deux tests ciblés 2/2. Séparément, le paquet issu
des sorties Luna réelles, SHA-256
`8DAC5FEB3293184564623C1F259567CB025078563F8BC788A9928806BC7672E2`,
a passé les tests Direct3D 12 2/2. Le rendu suit 111 points source, dont 94 visibles ;
36 440 pixels rouges ont été mesurés, puis zéro après nettoyage, sans dégât.
La suite PlayMode complète a passé 4 tests et ignoré 2 sondes facultatives.
Le shader `LavaBeam` est dans le Player BeamGeometryReady. La visibilité du sort depuis la caméra du
Player et l'écoute humaine du son restent non testées.

Les essais antérieurs restent historiques : la première lecture « faisceau de
feu visuel » a été rejetée par l'auteur ; A `1.2`/B `1.0` a été refusé pour
dégâts, cible et cadence inventés. Aucun de ces plans n'a traité un job joueur,
publié un sort ni lancé ce sort dans le Player IL2CPP. Voir la
[preuve de la troisième sonde](../evidence/public/backend/doctor-ab13-b11-2026-09-20.md),
la [preuve du plan B recalculé](../evidence/public/backend/doctor-plan-b11-resolved-2026-09-20.md)
et la [preuve Unity isolée actuelle](../evidence/public/unity/beam-geometry-luna-build-2026-09-20.md).
Le recalibrage se fait sur un corpus de conception, avec
comparaison d'au plus deux configurations et nouveau verdict humain. Le Player
ci-dessus contient
un formulaire de retour dans l'écran d'interprétation et le laboratoire ; il
recueille une correction sans modifier automatiquement le prompt ni le sort
existant. La route API et la migration 005 sont codées et la migration est
appliquée au laboratoire local ; la migration 006 pour la version de B est
également appliquée. L'envoi depuis le Player n'a pas encore été
observé, bien que l'API soit maintenant démarrée.
`POST /v1/authoring/plan` sert à tester B avec une
description structurée de concepteur ; il ne relit pas le dessin par A. Un
parchemin joueur engagé ne peut pas être effacé ou « rerollé » pour corriger
cette interprétation.

## Parcours à vérifier après ouverture contrôlée du laboratoire

L'ouverture à plusieurs joueurs exige d'abord la clarification du type de
compte et de l'autorisation d'usage partagé décrite dans
[NEXT_ACTIONS.md](NEXT_ACTIONS.md). Aucun service public n'est ouvert ici.

Pour l'essai réservé au propriétaire sur ce PC, un fichier d'invitation privé
`access.json` a fourni `http://127.0.0.1:18080` au Player malgré son
`service.json` vide. L'API et l'invitation ont fonctionné ; le Player LavaReady
précédent a repris la session conservée et affiché la capture en file. Le
Player BeamGeometryReady actuel a été lancé visiblement et répondait au
contrôle, sans traitement Luna de cette capture.
Le worker reste fermé, sans verdict humain sur A `1.3` ni preuve active de production approuvée ; aucune
nouvelle génération joueur ne peut donc encore aboutir. Une première commande
de démarrage d'API avec environnement privé avait été refusée avant exécution
par la revue automatique de l'outil (`blocked by policy`, sans détail). Le
lanceur `ops/start-owner-api.ps1` a ensuite démarré l'API locale en chargeant
ses fichiers privés dans le processus du service, sans secret sur la ligne de
commande.

L'opérateur déploie l'API HTTPS et le worker isolé après contrôle des migrations 003 à 006,
inscrit l'adresse du service dans chaque fichier d'invitation privé et le remet
au joueur. Dans le jeu, le joueur choisit ce fichier une seule fois,
dessine sur le parchemin, termine la capture, lit l'interprétation textuelle de
Luna A, puis ouvre le sort validé dans le laboratoire. Un sort téléchargé doit
rester accessible hors ligne au même joueur. Les instructions opérateur figurent
dans [SETUP.md](SETUP.md) ; la marche à suivre du joueur est dans
[MANUEL_JOUEUR.md](MANUEL_JOUEUR.md). La recette et ses verdicts humains restent
à enregistrer dans [RECETTE_FINALE.md](RECETTE_FINALE.md).
