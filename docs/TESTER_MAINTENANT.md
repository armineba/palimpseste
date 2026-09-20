# Tester le lecteur actuel

## Essai actuel sur ce PC

Ouvrir [Palimpseste.exe](../game/Build/WindowsEffectLibraryPlayable/Palimpseste.exe) en conservant ensemble les 29 fichiers du dossier. Dans la bibliothèque, ouvrir « Enclume filante », cliquer sur **Actualiser** si l'ancien échec s'affiche, puis sur **Lancer dans le laboratoire**. Ce dessin a déjà un sort compilé. Pour essayer un dessin neuf, tracer autant de traits que souhaité, cliquer sur **Dessin terminé**, lire l'interprétation d'Astra, puis ouvrir et lancer le sort une fois sa fiche prête. Si un échec de traduction admissible apparaît, **Réessayer ce dessin** conserve la capture.

Le service local déployé propose maintenant **141 recettes nommées** composées des **24 effets que ce Player sait exécuter**. Le compilateur a contrôlé les 141 recettes sur leurs cinq porteurs, soit **705 couples**, et la QA des contrats a passé **61/61**. Il s'agit de tests automatiques : aucun nouveau dessin généré avec le catalogue des 141 recettes, ni verdict humain sur leurs effets visuels et sonores, n'est encore attesté. Le service répond sur ce PC, sous `127.0.0.1` ; voir l'[état de déploiement](IMPLEMENTATION_STATUS.md). Les sections ci-dessous consignent les essais des 24 effets et les builds antérieurs.

## Build Player actuel : sort prêt et 24 effets primitifs

Le nouveau [Palimpseste.exe](../game/Build/WindowsEffectLibraryPlayable/Palimpseste.exe) a été lancé sur ce PC ; garder les 29 fichiers de son dossier ensemble. Le service local répond. Dans la bibliothèque, ouvrir le parchemin « Enclume filante », cliquer sur **Actualiser** si son ancien écran d'échec est encore visible, puis **Lancer dans le laboratoire**. Le même dessin a déjà produit un sort compilé : aucun redessin ni intervention technique ne sont nécessaires. Pour un nouveau dessin, relâcher le stylet entre plusieurs traits, puis cliquer sur **Dessin terminé**. Astra décrit le dessin, Luna planifie dans le catalogue jouable, puis la fiche permet le lancement dans le labo. Un échec B que le serveur juge reprenable affiche **Réessayer ce dessin** et conserve la capture.

Le nouveau Player utilise 24 effets contrôlés avec VFX de statut ; hémorragie, poison, vol de vie, protection et caisse destructible marquée en font partie. Les 6 tests PlayMode ciblés ont passé ; le build Windows Unity 6000.3.24f1 IL2CPP/URP contient 29 fichiers, 122 136 545 octets, `GameAssembly.dll` SHA-256 `A003ED515F512965E5D4A88E0151FB976157DD0F24FE7E155302D8E3A5658B8F`. Le [ZIP Player](../deliverables/Palimpseste-Windows-x64-IL2CPP.zip) fait 44 054 317 octets, SHA-256 `8F8ADC123388801065234D0E1179D137C03B0B574D80F5D2B8A2690BBF768587`. Aucun essai manuel de ce nouveau rendu ni appel réel avec le catalogue de 24 effets n'est encore consigné. Voir [l'état précis](IMPLEMENTATION_STATUS.md) et [la bibliothèque d'effets](EFFECT_LIBRARY.md).

## Historique : premier build du dessin libre

Lancer [Palimpseste.exe](../game/Build/WindowsAppearanceVfxPlayable/Palimpseste.exe) sur **ce PC et ce profil Windows**, avec l'API locale disponible. Le parchemin humain à deux traits est déjà prêt : le job `cd4fec7e-aae8-45ba-b5f3-16a807a9771e` a produit la description Astra « Estoc à crochet » et une fiche de sort compilé. Rouvrir ce sort dans la bibliothèque et examiner en priorité son nouveau VFX dans le laboratoire face à la description. Le Player VFX a été relancé, mais cet examen visuel n'est pas encore consigné. L'essai de ciblage manuel par clics successifs a été arrêté à la demande du créateur. Pour un nouveau parchemin, dessiner plusieurs traits sur tout le papier, relâcher entre les traits, puis cliquer sur **« Dessin terminé »**. Un clic sur page vierge laisse le dessin ouvert et indique d'ajouter un trait. Une génération peut prendre plusieurs minutes avec les efforts `max`. Le bouton « Reconnecter » permet de reprendre une transmission restée locale après une coupure.

Le build courant a été construit avec Unity 6000.3.24f1 en IL2CPP/URP, retour 0 et `PALIMPSESTE_BUILD_OK` : dossier nettoyé de 29 fichiers, 122 113 753 octets ; test PlayMode VFX/palette 1/1 réussi. EXE SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`, `GameAssembly.dll` SHA-256 `BBA75A8B708DD404D877CC7E4CA5A3448049F391EC9C5AACB4446E03EF54FF55`. L'[archive Player actuelle](../deliverables/Palimpseste-Windows-x64-IL2CPP.zip) contient ce dossier : 44 045 012 octets, SHA-256 `4AF6EF5D7449CBE89094755A3B6A045D5EF75DA21706C40484403FB17B419A3F`. Le Player précédent a montré [la fiche prête](../evidence/public/unity/free-canvas-player-spell-ready-20260920.png) puis [un lancement visible](../evidence/public/unity/free-canvas-player-cast-visible-20260920.png) du vrai sort. Ces images ne valident pas le nouveau VFX ni le son. Les 17/17 tests EditMode du dessin libre et des erreurs API avaient passé sur le build précédent. Voir [le rapport complet](../evidence/public/unity/free-canvas-appearance-2026-09-20.md). Les anciennes sections ci-dessous concernent des builds précédents.

## Historique du correctif de reconnexion

### Nouveau build de reconnexion sur ce PC

Lancer [Palimpseste.exe](../game/Build/WindowsPlayerReconnectPlayable/Palimpseste.exe)
depuis `game/Build/WindowsPlayerReconnectPlayable/`, en conservant les 29
fichiers ensemble. Ce dossier jouable contient 122 095 741 octets. L'EXE a
pour SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`
et `GameAssembly.dll` pour SHA-256
`E379AD0EC9CB79533E319BDEB97826E480BE55D614697A66F4183AB81669D75F`.
Les 29 fichiers correspondent au build brut ; les deux dossiers `DoNotShip`
du build brut ont été exclus. Le journal Unity contient `PALIMPSESTE_BUILD_OK`,
« Application will terminate with return code 0 » et aucune erreur `CS` ; le
code de retour de la commande PowerShell parente n'a pas été capturé. Le
nouveau Player et la reprise du dessin en attente n'ont pas encore été
observés en exécution.

Le Player précédent, PID `35804`, avait démarré pendant que l'API était
indisponible. Après un nouveau tracé, son écran est resté sur « transmission
de la capture en attente ». Son enregistrement local est `capture_pending`,
avec `needs_begin` et `needs_capture` vrais et 151 entrées de journal. L'API
répondait de nouveau 200 et le worker était actif, mais aucun nouveau job
n'avait été créé : attendre seul sur cet écran ne lançait donc pas Luna.
Le correctif ajoute « Reconnecter » sur l'écran de traitement hors ligne.
Dans le nouveau build, rouvrir le même parchemin depuis la bibliothèque puis
cliquer sur ce bouton ; après reconnexion, le Player doit tenter la
transmission. Si « Transmettre » apparaît, cliquer dessus. Garder le même
profil Windows pour récupérer le journal ; il est possible de redessiner si
la reprise échoue. Consigner le résultat de cet essai avant de le déclarer
réussi. Voir la [preuve de l'incident et du correctif](../evidence/public/unity/player-reconnect-pending-capture-2026-09-20.md).

## Essai déjà observé avec le build précédent

Le lecteur `game/Build/WindowsPlayerBeamVisibleReady/Palimpseste.exe`
Windows x64 IL2CPP/URP a été construit avec Unity 6000.3.24f1. Il contient
29 fichiers, 122 093 149 octets, et le shader `LavaBeam` dans
`resources.assets` ; l'exécutable a pour SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`,
`GameAssembly.dll` pour SHA-256
`9FB651D09680CAF09B0A8668BEF2A914EFDE6B61F930EDBAE1CE90AF3B5C18C9`.
C'était le build testé, terminé par Unity avec le code 0 ; aucun ZIP
correspondant n'a été créé.
L'API propriétaire locale tourne sous `PalRuntimeSvc` sur
`http://127.0.0.1:18080` ; `/health/ready` a répondu 200 après redémarrage de
l'API sous le PID `16420`. Le worker reste actif sous le PID `8180`. Le Player
de ce build a été ouvert en fenêtre visible, PID `35804`. La session
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
ce Player précédent, une capture montre l'effet visible puis disparu. L'écoute
humaine du son reste à vérifier.
Voir la [preuve du parcours joueur et du build précédent testé](../evidence/public/unity/owner-player-end-to-end-2026-09-20.md),
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
