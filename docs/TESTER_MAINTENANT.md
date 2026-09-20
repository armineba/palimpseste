# Tester le lecteur actuel

## Sur ce PC, maintenant

Lancer `game/Build/WindowsPlayerBeamVisibleReady/Palimpseste.exe`. Ce lecteur
Windows x64 IL2CPP/URP a été construit avec Unity 6000.3.24f1. Il contient
29 fichiers, 122 093 149 octets, et le shader `LavaBeam` dans
`resources.assets` ; l'exécutable a pour SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`,
`GameAssembly.dll` pour SHA-256
`9FB651D09680CAF09B0A8668BEF2A914EFDE6B61F930EDBAE1CE90AF3B5C18C9`.
C'est le build actuel, terminé par Unity avec le code 0 ; aucun ZIP
correspondant n'a été créé.
L'API propriétaire locale tourne sous `PalRuntimeSvc` sur
`http://127.0.0.1:18080` ; `/health/ready` a répondu 200 après redémarrage de
l'API sous le PID `16420`. Le worker reste actif sous le PID `8180`. Le Player
actuel a été ouvert en fenêtre visible, PID `35804`. La session
conservée et l'écran « Capture reçue / En file d'attente » ont été observés
dans le Player LavaReady précédent pour une vraie capture propriétaire reçue
par l'API. La base contenait alors un job `production` et quatre artefacts :
référence, dessin, encre et journal. Ce dessin est une trace rouge-brun
**courbe**, différente du trait droit utilisé pour calibrer A `1.3`.
Le 20 septembre à 11 h 54 (Paris), le worker isolé tournait sous
`PalRuntimeSvc` (PID `8180`) ; ce même job était passé à `interpreting`. Il a
ensuite atteint `ready` : deux tentatives fournisseur réussies, une
description, un plan et un `compiled_spell`, sans échec enregistré. Le Player
a montré A « Faisceau courbe de lave », puis la fiche du sort téléchargée et
vérifiée. Le laboratoire s'est ouvert ; un clic a donné `Lancers : 1` et
`Dégâts : 0`. Ce dernier chiffre correspond au plan visuel sans dégâts. Dans
le nouveau Player, une capture montre l'effet visible puis disparu. L'écoute
humaine du son reste à vérifier.
Voir la [preuve du parcours joueur et du build actuel](../evidence/public/unity/owner-player-end-to-end-2026-09-20.md),
la [capture du sort visible](../evidence/public/unity/owner-player-lava-beam-2026-09-20.png)
et la [capture de sa relecture sans API](../evidence/public/unity/owner-player-offline-lava-beam-2026-09-20.png).

Pour juger le **trait droit de calibration** (distinct du dessin courbe
traité), la nouvelle sortie A `1.3` dit : « Un faisceau rectiligne de matière
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

Le fichier `game/Build/WindowsPlayerBeamVisibleReady/Palimpseste_Data/StreamingAssets/service.json`
ne contient pas d'adresse de service public, et aucune invitation joueur n'est
livrée dans le dépôt. **Le test privé a désormais produit la description et
un sort réel pour la capture du Player.** La fiche est téléchargée, vérifiée
et conservée localement. L'API précédente (PID `25848`) a été arrêtée ; le
Player a été relancé sans ce service et a affiché la bibliothèque, la fiche,
le laboratoire et un lancement du même sort. L'API a ensuite été redémarrée
et a répondu 200 à `/health/ready`. Une commande de confort pour fermer puis
relancer le Player avec l'API restaurée a été refusée avant exécution par la
revue automatique (`blocked by policy`, sans motif détaillé) ; elle n'a pas été
réessayée par une commande équivalente. Le Player PID `35804` reste ouvert dans
la session issue de l'essai sans API ; aucun nouveau lancement du Player après
restauration de l'API n'est attesté. Le verrou technique d'effort est
ouvert sur un manifeste composite v2 vérifié, SHA-256
`56F349016A5B63D352DD37225AF9253889D496C2399F58F97F70D0DAAC353E3F`.
Le Doctor installé, SHA-256
`AAAAB2686CD9A33ADB6130B205D10596A63AF8207CBAE580ACD723C862196811`,
a passé son diagnostic local après correction du BOM sans `production_issues`.
Le worker installé a pour SHA-256
`9243F9600973B1AAF5F97F07AD0983FF60A7EFA2EB0A7F7D42D142C05015847D` ;
son script enfant pour SHA-256
`03CE0108D67A4AC27B332A41F36F1F12B2B362FA9A2AEBCF25EB21E8BDB4C104`.
La lecture A `1.3` du trait droit de calibration attend encore votre verdict.

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
Le shader `LavaBeam` est dans le Player BeamVisibleReady. Le clic du sort
joueur a été observé dans le laboratoire, avec `Lancers : 1` et `Dégâts : 0`.
Les tests PlayMode sur ce paquet joueur ont passé 2/2 après un correctif de
visibilité à `0,6 s`. Le build Windows correspondant,
`WindowsPlayerBeamVisibleReady`, a terminé avec le code 0 ; une capture du
Player montre l'effet visible, puis disparu. Le son n'a pas encore reçu
d'écoute humaine.

Les essais antérieurs restent historiques : la première lecture « faisceau de
feu visuel » a été rejetée par l'auteur ; A `1.2`/B `1.0` a été refusé pour
dégâts, cible et cadence inventés. Aucun de ces plans n'a traité un job joueur,
publié un sort ni lancé ce sort dans le Player IL2CPP. Voir la
[preuve de la troisième sonde](../evidence/public/backend/doctor-ab13-b11-2026-09-20.md),
la [preuve du plan B recalculé](../evidence/public/backend/doctor-plan-b11-resolved-2026-09-20.md)
et la [preuve Unity isolée du build précédent](../evidence/public/unity/beam-geometry-luna-build-2026-09-20.md).
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

## Parcours à vérifier dans le laboratoire privé

L'ouverture à plusieurs joueurs exige d'abord la clarification du type de
compte et de l'autorisation d'usage partagé décrite dans
[NEXT_ACTIONS.md](NEXT_ACTIONS.md). Aucun service public n'est ouvert ici.

Pour l'essai réservé au propriétaire sur ce PC, un fichier d'invitation privé
`access.json` a fourni `http://127.0.0.1:18080` au Player malgré son
`service.json` vide. L'API et l'invitation ont fonctionné ; le Player LavaReady
précédent a repris la session conservée et affiché la capture en file. Le
Player BeamGeometryReady précédent a affiché la description A de cette
capture, sa fiche et le laboratoire ; un lancement a été comptabilisé. Le
nouveau Player BeamVisibleReady a ensuite montré l'effet, puis l'a rejoué
après arrêt de l'API et relance du jeu.
Le worker a été ouvert pour cet essai technique privé à la demande du
propriétaire. Le verdict humain sur A `1.3` et celui sur la nouvelle capture
restent en attente ; la preuve de compatibilité technique n'est pas une
acceptation artistique. Une première commande
de démarrage d'API avec environnement privé avait été refusée avant exécution
par la revue automatique de l'outil (`blocked by policy`, sans détail). Le
lanceur `ops/start-owner-api.ps1` a ensuite démarré l'API locale en chargeant
ses fichiers privés dans le processus du service, sans secret sur la ligne de
commande.

Sur ce PC, rouvrir la session propriétaire et le parchemin déjà engagé : le
Player a affiché l'interprétation A, la fiche du sort et le laboratoire. Un
lancement a été comptabilisé. Le nouveau build a montré l'effet visible puis
disparu ; le même paquet est resté lançable après redémarrage du Player sans
API. Il reste l'écoute humaine du son et les verdicts des créateurs sur la
lecture et le sort. Ne pas remplacer la capture
engagée ni relancer un job déjà `ready` pour fabriquer une nouvelle preuve.
Les instructions opérateur figurent dans [SETUP.md](SETUP.md) ; la marche à
suivre du joueur est dans [MANUEL_JOUEUR.md](MANUEL_JOUEUR.md). La recette et
ses verdicts humains restent à enregistrer dans
[RECETTE_FINALE.md](RECETTE_FINALE.md).
