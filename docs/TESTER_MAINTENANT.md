# Tester le lecteur actuel

## D19 / 1.7.0 — profils issus des bibliothèques

Une fois [l'installation D19](../evidence/public/backend/sourced-surfaces-d19-installation.json) terminée, ouvrir le raccourci Bureau et créer **un nouveau parchemin**. Le constructeur peut choisir pour chaque partie les surfaces plasma, champ de force, matière toxique et flux spectral livrées dans le moteur. La sélection dépend de la description et de la planche ; il ne faut pas ajouter les quatre à tous les sorts.

Suivre **Dessin terminé → description → planche → construction → laboratoire**. Comparer la matière, son mouvement et sa disparition à la planche, puis donner le verdict. Rejouer un ancien sort utilise ses matériaux déjà enregistrés ; les nouveaux profils ne lui sont pas appliqués automatiquement. Plusieurs nouveaux parchemins peuvent progresser simultanément ; les captures de critique partagent le GPU.

Aucun essai visuel D19 n'a été exécuté par le développement. Le résultat doit être apprécié par le créateur. Les sections suivantes sont historiques.

## Dernier sort récupéré — D16.1

« **Foudre sous l’Enclume** » est disponible après correction du blocage de critique. Cliquer **Actualiser** sur ce parchemin, puis lancer le sort dans le laboratoire. Le dessin et la planche sont conservés ; aucun besoin de redessiner pour cette reprise. La version récupérée a réellement été évaluée à 4190/10000, avec une animation encore jugée imparfaite : elle est disponible pour recueillir les corrections du créateur. [Preuve](../evidence/public/backend/visual-judge-fix-d16-1.json).

## Parcours D16 / 1.6.0 — planche d’animation

Ouvrir **Palimpseste Spell Lab** depuis le raccourci Bureau actualisé. L’état réel du service et de son installation figure dans [le point de reprise](NEXT_ACTIONS.md).

1. Dessiner **un nouveau parchemin**, avec autant de traits que souhaité, puis cliquer **Dessin terminé**.
2. Lire la description et attendre la **planche VFX** : APPARITION, STABLE, DISPARITION, sept cases numérotées par ligne. L’agrandir pour examiner les détails ; molette pour zoomer et glisser pour se déplacer.
3. Laisser la recherche de ressources, la construction et la critique Pro se terminer, puis ouvrir le laboratoire et lancer le sort dans l’arène.
4. Comparer la formation, le mouvement actif et la fin au texte et à la planche. Donner son verdict et les corrections souhaitées.

La planche est une référence artistique du sort, pas une animation préenregistrée affichée dans le laboratoire. Les anciens parchemins conservent leur image et leur sort ; un nouveau dessin est nécessaire pour essayer D16. Aucun essai de cette version n’a été effectué à la place du créateur.

## Historique — parcours D15 / 1.5.0

Le Player 1.5.0 a été construit et empaqueté. L'état d'installation du service est indiqué dans [le point de reprise](NEXT_ACTIONS.md) et [la preuve D15](../evidence/public/backend/behavior-d15.json).

Ouvrir le raccourci Bureau **Palimpseste Spell Lab**, puis dessiner **un nouveau parchemin**. Relâcher la souris entre les traits ; cliquer **Dessin terminé** une fois le dessin complet. Attendre la description, l'image cible, la recherche de ressources, la construction et les ajustements Pro. Ouvrir ensuite le laboratoire et cliquer dans l'arène pour lancer le sort.

Les nouveaux sorts portent leurs intentions de placement, déplacement et animation. Le mouvement de la matière est distinct du voyage du projectile et de ses effets. Dans un sort au sol ciblé, viser le lieu où l'on veut le poser. Les paquets déjà enregistrés gardent leurs données ; les rouvrir ne les convertit pas au nouveau système.

Comparer librement ce qui est décrit au placement, au mouvement, au contact et à la disparition observés. Les compilations réussies ne valent pas validation de ces comportements ni acceptation artistique : les essais de cette version sont laissés au créateur.

## Historique — parcours D13 / 1.3.0

**Le jeu `1.3.0` est ouvert et le service local est prêt.** Le Player PID `39012` a été lancé le 20 septembre à 20:11:04 UTC, fenêtre réactive, et le raccourci Bureau **Palimpseste Spell Lab** est actualisé. [Preuve de livraison](../evidence/public/unity/image-reference-delivery.json) · [Déploiement local](../evidence/public/backend/image-reference-deployment-2026-09-20.json). Dans le jeu :

1. Cliquer **Dessiner un parchemin** et dessiner librement. Relâcher la souris entre les traits pour ajouter plusieurs éléments.
2. Cliquer **Dessin terminé** une fois le dessin complet.
3. Attendre les étapes **Description → Image du sort → Construction → Compilation → Sort**. Une nouvelle génération peut prendre **plusieurs minutes** ; le dessin est conservé pendant l'attente.
4. Sur la fiche prête, cliquer **Lancer dans le laboratoire**, puis cliquer dans l'arène pour lancer le sort vers une cible.
5. Comparer l'image de référence et le sort en mouvement, puis écouter le son. Le rendu reste à juger par le joueur.

Les anciens sorts restent dans la bibliothèque et se rejouent **sans régénération**. Le nouveau parcours avec image s'applique aux nouvelles générations ; il ne transforme pas rétroactivement les anciens résultats.

La [capture Unity réelle du sort d'orage](../evidence/public/unity/image-reference-2026-09-20.md) a réussi les contrôles de lancement, impact et nettoyage. Un dernier réglage du verre a été effectué après cette capture et n'a pas été recapturé. Ces contrôles ne constituent pas une acceptation artistique ni un parcours joueur complet dans le Player final.

## Historique — essai du spectre magique livré, D12 / 1.2.2

Ouvrir le raccourci Bureau **Palimpseste Spell Lab** ou [le Player `1.2.2`](../game/Build/WindowsSpectralEnergyPlayable/Palimpseste.exe), déjà lancé sur ce PC. Conserver les **29 fichiers du dossier** ensemble ; [preuves de livraison](../evidence/public/unity/spectral-energy-delivery.json).

1. Dans la bibliothèque, ouvrir **« Envol du spectre aux longs voiles »**.
2. Ouvrir le laboratoire puis lancer le sort.
3. Observer la présence en brume, les longues nappes qui suivent le vol et leur dispersion après l'impact ; écouter également le résultat sonore.

Il n'est pas nécessaire de redessiner ni de lancer une nouvelle génération pour cet essai. La caméra habituelle du labo reste inchangée. Le spectre emploie désormais de l'énergie translucide ; la capuche et le visage opaques de l'itération 02, refusés par le créateur, ont été retirés.

Voir l'[animation réelle avec la caméra du labo](../evidence/public/unity/spectral-fidelity/iteration-04/spectre-lab.mp4) et la [vue latérale supplémentaire](../evidence/public/unity/spectral-fidelity/iteration-04/spectre-side-review.mp4). Ces vidéos de 2,433333 s sont sans son. Les [preuves détaillées](../evidence/public/unity/spectral-fidelity-2026-09-20.md) établissent le replay, les dégâts et la disparition du sillage ; **la correspondance exacte à la référence et la satisfaction artistique restent à apprécier**.

Pour créer un autre sort, dessiner puis cliquer **Dessin terminé**, lire l'interprétation et lancer le sort prêt. La chaîne demeure **Sol/high → Astra/high**. Cette mise à jour du spectre ne démontre pas la finition de tous les autres sorts ni l'accès de joueurs distants.

## Historique — version stylisée D11 livrée, 1.2.1

La finition ajoute des spirales, des couronnes et plusieurs couches de particules, avec un soin vert plus doux et des impacts plus développés. Les [captures Unity réelles](../evidence/public/unity/stylized-vfx-2026-09-20.md) sont disponibles ; trois compositions sont des fixtures de présentation, et le spectre provient d'un vrai sort conservé. Elles ne constituent pas une validation artistique humaine.

Ouvrir le raccourci Bureau **Palimpseste Spell Lab** ou [ce Player `1.2.1`](../game/Build/WindowsStylizedVfxPlayable/Palimpseste.exe). Il a été construit avec le code 0 puis lancé sous le PID `30968`, fenêtre réactive ; le raccourci est actualisé. Reprendre **« Envol du spectre aux longs voiles »** dans la bibliothèque, puis lancer le sort au labo. Il n'est pas nécessaire de redessiner pour voir ce rendu amélioré. Conserver ensemble les 29 fichiers du dossier ; [preuves et empreintes de livraison](../evidence/public/unity/stylized-vfx-delivery.json).

Pour créer un autre sort : dessiner librement, cliquer **Dessin terminé**, lire l'interprétation, puis lancer le sort prêt. La chaîne reste **Sol/high → Astra/high**. Cette finition graphique n'ajoute aucun appel modèle, test ou build à chaque génération joueur ; elle ne garantit pas un temps fixe de génération. Le service reste local ; l'accès de joueurs distants n'est pas livré.

## Historique — essayer la version D10 livrée

Ouvrir le raccourci Bureau **Palimpseste Spell Lab** ou [ce Player `1.2.0`](../game/Build/WindowsComposedVfxPlayable/Palimpseste.exe). Le jeu a déjà été lancé sous le PID `32064`. Conserver ensemble les 29 fichiers du dossier ; le [ZIP vérifié](../deliverables/Palimpseste-Windows-x64-IL2CPP.zip) permet de transporter ce dossier complet. Le build final Windows IL2CPP/URP a terminé avec le code 0 ; [preuves de livraison](../evidence/public/unity/composed-vfx-delivery.json).

Dans la bibliothèque, ouvrir **« Envol du spectre aux longs voiles »**, puis lancer le sort au laboratoire. Ce sort sauvegardé bénéficie du nouveau renderer sans redessiner ni consommer une nouvelle génération. Pour essayer la nouvelle chaîne de modèles, créer ensuite un dessin, ajouter autant de traits que souhaité, cliquer sur **Dessin terminé**, lire l'interprétation, puis lancer le sort quand sa fiche est prête. L'écran affiche le temps écoulé mesuré ; aucun délai fixe n'est garanti.

Le backend **Sol/high → Astra/high** est déployé (prompts A `2.2` / B `1.9`). Un [diagnostic réel](../evidence/public/backend/sol-astra-latency-2026-09-20.md) a obtenu un plan compilé avec **60,403 s d'appels**, sans réparation, environ 63 s au total. Ce résultat sur un dessin ne garantit pas le temps des suivants. Le renderer a passé **5/5 tests ciblés**, avec [six captures réelles du spectre conservé](../evidence/public/unity/composed-vfx-2026-09-20.md). L'essai humain et le son restent à apprécier ; aucun verdict artistique n'est enregistré. Les sections suivantes concernent des livraisons historiques.

L'image [spectre de référence](art-direction/spectral-veils-target.png) est un concept artistique généré, pas une capture du Player. L'appréciation du rendu réel et du son reste à donner dans le laboratoire. La génération de chaque sort ne lance ni tests, ni build Unity, ni génération de cette image.

## Historique — essai du rendu 3D D09

La [décision D09](DECISIONS.md) demande que le sort affiché soit **l'objet imaginé par Astra en 3D**, par exemple un rocher, et ne reprenne plus automatiquement le contour du dessin. Le [Player Windows final](../game/Build/WindowsSemanticVfxPlayable/Palimpseste.exe) est livré avec ses 29 fichiers et le [ZIP vérifié](../deliverables/Palimpseste-Windows-x64-IL2CPP.zip). Il a été lancé sur ce PC, PID `26864`, fenêtre réactive. A `2.1`/B `1.8` et le compilateur préparent 21 formes contrôlées ; le backend local est déployé. Le renderer a passé **5/5 tests PlayMode ciblés** et le cache hors ligne **1/1 EditMode**. Le build final inclut explicitement le matériau d'émission IL2CPP. Voir [la preuve Unity](../evidence/public/unity/semantic-visuals-2026-09-20.md).

Dans ce Player final, faire **un dessin neuf**, cliquer **Dessin terminé**, lire l'objet choisi par Astra, puis lancer le sort dans le laboratoire. Comparer l'objet visible en volume avec cette lecture et écouter le son ; dire si le résultat correspond au dessin et si le rendu plaît. Les anciens parchemins gardent leurs anciens résultats et leur ancien rendu. Un [diagnostic réel A/B](../evidence/public/backend/semantic-visual-generation-2026-09-20.md) a déjà compilé un golem sur un dessin conservé, et une [capture de fixture Unity](../evidence/public/unity/semantic-forms-preview-20260920.png) montre les composants ; aucun de ces deux essais n'est un nouveau sort joueur lancé dans ce Player final.

## Vérifier une nouvelle interprétation

Deux dessins récents ont donné des sorts `ready` aux titres proches, « Nœud de cuivre » et « Nœud de cuivre mordant » ; leurs images et recettes diffèrent ([comparaison](../evidence/public/backend/interpretation-repetition-2026-09-20.md)). Le prompt A `2.0` qui retire un exemple de couleur a été **déployé** ; un troisième vrai job avec ce prompt est aussi `ready`, sous le titre **« Le Nœud filant »**. La répétition du thème persiste. A `2.1` ajoute les formes 3D et une consigne sur les croisements du dessin ; il est déployé et a produit un golem dans un diagnostic opérateur, sans vérifier encore la variété sur un nouveau job joueur. Dessiner une **nouvelle forme** dans le Player 3D avec **Dessin terminé** et comparer le titre, la lecture de l'objet et son rendu dans le labo au dessin. Recharger les anciens parchemins montrera toujours leurs anciennes interprétations.

Les indications qui suivent sur le refus de quota et l'absence de nouvel envoi décrivent l'état **avant** les trois jobs récents.

## Historique — dessin conservé après le refus de quota

Le message « Votre quota quotidien de générations est atteint » provenait du plafond local du backend, qui a été retiré. Au moment de cette correction, l'envoi réel de la capture après correction n'était pas encore attesté. Les trois jobs récents décrits ci-dessus ont depuis atteint `ready` ; cette ancienne consigne de retransmission n'est plus le point de test actuel.

La limite d'utilisation du compte Codex sur le serveur reste distincte. Si ce compte est à sa limite, il peut encore refuser la génération ; Palimpseste ne commande ni achat ni recharge. Voir [l'explication du quota](GENERATION_QUOTA.md) et [l'état technique](IMPLEMENTATION_STATUS.md).

## Historique — essai du Player à 24 effets

Ouvrir [Palimpseste.exe](../game/Build/WindowsEffectLibraryPlayable/Palimpseste.exe) en conservant ensemble les 29 fichiers du dossier. Dans la bibliothèque, ouvrir « Enclume filante », cliquer sur **Actualiser** si l'ancien échec s'affiche, puis sur **Lancer dans le laboratoire**. Ce dessin a déjà un sort compilé. Pour essayer un dessin neuf, tracer autant de traits que souhaité, cliquer sur **Dessin terminé**, lire l'interprétation d'Astra, puis ouvrir et lancer le sort une fois sa fiche prête. Si un échec de traduction admissible apparaît, **Réessayer ce dessin** conserve la capture.

Le service local déployé propose **141 recettes nommées** composées des **24 effets que ce Player sait exécuter**. Le compilateur a contrôlé les 141 recettes sur leurs cinq porteurs, soit **705 couples**, et la QA des contrats a passé **61/61**. Trois jobs joueur réels ont depuis utilisé ce catalogue et produit des sorts `ready` ; leur lancement en laboratoire et le verdict humain sur leurs effets visuels et sonores ne sont pas attestés. Le service répond sur ce PC, sous `127.0.0.1` ; voir l'[état de déploiement](IMPLEMENTATION_STATUS.md). Les sections ci-dessous consignent les essais des 24 effets et les builds antérieurs.

## Historique — build du Player à 24 effets

Le nouveau [Palimpseste.exe](../game/Build/WindowsEffectLibraryPlayable/Palimpseste.exe) a été lancé sur ce PC ; garder les 29 fichiers de son dossier ensemble. Le service local répond. Dans la bibliothèque, ouvrir le parchemin « Enclume filante », cliquer sur **Actualiser** si son ancien écran d'échec est encore visible, puis **Lancer dans le laboratoire**. Le même dessin a déjà produit un sort compilé : aucun redessin ni intervention technique ne sont nécessaires. Pour un nouveau dessin, relâcher le stylet entre plusieurs traits, puis cliquer sur **Dessin terminé**. Astra décrit le dessin, Luna planifie dans le catalogue jouable, puis la fiche permet le lancement dans le labo. Un échec B que le serveur juge reprenable affiche **Réessayer ce dessin** et conserve la capture.

Ce Player historique utilise 24 effets contrôlés avec VFX de statut ; hémorragie, poison, vol de vie, protection et caisse destructible marquée en font partie. Les 6 tests PlayMode ciblés ont passé ; le build Windows Unity 6000.3.24f1 IL2CPP/URP contient 29 fichiers, 122 136 545 octets, `GameAssembly.dll` SHA-256 `A003ED515F512965E5D4A88E0151FB976157DD0F24FE7E155302D8E3A5658B8F`. Son ZIP de l'époque faisait 44 054 317 octets, SHA-256 `8F8ADC123388801065234D0E1179D137C03B0B574D80F5D2B8A2690BBF768587` ; l'archive de livraison a depuis été remplacée par le Player 3D décrit en tête. Aucun verdict humain sur ce rendu historique n'est consigné. Voir [l'état précis](IMPLEMENTATION_STATUS.md) et [la bibliothèque d'effets](EFFECT_LIBRARY.md).

## Historique : premier build du dessin libre

Lancer [Palimpseste.exe](../game/Build/WindowsAppearanceVfxPlayable/Palimpseste.exe) sur **ce PC et ce profil Windows**, avec l'API locale disponible. Le parchemin humain à deux traits est déjà prêt : le job `cd4fec7e-aae8-45ba-b5f3-16a807a9771e` a produit la description Astra « Estoc à crochet » et une fiche de sort compilé. Rouvrir ce sort dans la bibliothèque et examiner en priorité son nouveau VFX dans le laboratoire face à la description. Le Player VFX a été relancé, mais cet examen visuel n'est pas encore consigné. L'essai de ciblage manuel par clics successifs a été arrêté à la demande du créateur. Pour un nouveau parchemin, dessiner plusieurs traits sur tout le papier, relâcher entre les traits, puis cliquer sur **« Dessin terminé »**. Un clic sur page vierge laisse le dessin ouvert et indique d'ajouter un trait. Une génération peut prendre plusieurs minutes avec les efforts `max`. Le bouton « Reconnecter » permet de reprendre une transmission restée locale après une coupure.

Ce build historique a été construit avec Unity 6000.3.24f1 en IL2CPP/URP, retour 0 et `PALIMPSESTE_BUILD_OK` : dossier nettoyé de 29 fichiers, 122 113 753 octets ; test PlayMode VFX/palette 1/1 réussi. EXE SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`, `GameAssembly.dll` SHA-256 `BBA75A8B708DD404D877CC7E4CA5A3448049F391EC9C5AACB4446E03EF54FF55`. Son archive Player de l'époque faisait 44 045 012 octets, SHA-256 `4AF6EF5D7449CBE89094755A3B6A045D5EF75DA21706C40484403FB17B419A3F` ; l'archive de livraison actuelle est le Player 3D décrit en tête. Le Player précédent a montré [la fiche prête](../evidence/public/unity/free-canvas-player-spell-ready-20260920.png) puis [un lancement visible](../evidence/public/unity/free-canvas-player-cast-visible-20260920.png) du vrai sort. Ces images ne valident pas le nouveau VFX ni le son. Les 17/17 tests EditMode du dessin libre et des erreurs API avaient passé sur le build précédent. Voir [le rapport complet](../evidence/public/unity/free-canvas-appearance-2026-09-20.md). Les anciennes sections ci-dessous concernent des builds précédents.

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
