# Manuel du lecteur Palimpseste

Sur ce PC, lancer [Palimpseste.exe](../game/Build/WindowsPlayerBeamVisibleReady/Palimpseste.exe)
depuis son dossier `game/Build/WindowsPlayerBeamVisibleReady/`. Garder les autres
fichiers du dossier avec l'exécutable. L'ancienne archive ZIP contient une
version antérieure. Le jeu ne demande ni connexion Codex par joueur, ni adresse
de service, ni jeton à saisir. L'accès au laboratoire déjà installé permet
d'ouvrir ou de reprendre le parchemin du lecteur. En ligne, le jeu peut ouvrir
automatiquement un nouveau parchemin vierge : cliquer sur « Fermer le
parchemin » sans dessiner pour revenir à la bibliothèque et retrouver le sort
déjà enregistré.

## Du dessin au texte

Dessiner directement sur le parchemin. Le premier trait l'engage ; chaque
région encrée se verrouille quand le pinceau est relevé. Choisir l'encre, le
type de trait et son épaisseur dans l'atelier. Cliquer sur « Fermer le
parchemin » pour figer la capture. Le dessin et son journal restent enregistrés
sur cet appareil. Le jeu tente de transmettre la capture ; si elle est encore
en attente, utiliser « Transmettre » sur l'écran « Du dessin au sort ».

Cet écran montre la trace et les étapes signalées par le laboratoire : lecture
du dessin par Luna A, extraction des formes, traduction par Luna B, contrôle du
sort et téléchargement. Le panneau « Interprétation de Luna A » affiche le
titre, le résumé, les observations du dessin et les règles proposées dès que
la description validée est disponible. Le texte peut être défilé. « Actualiser »
relance le suivi si celui-ci s'est interrompu. « Lire et signaler » permet de
relire la description et d'envoyer une correction ; ce retour ne change pas le
sort du parchemin déjà engagé.

## Du texte au sort dans le laboratoire

Quand le sort a été validé et téléchargé, « Voir le sort » ouvre sa fiche.
Elle présente la description et les règles du sort et donne accès à « Lire
l'interprétation Luna » et « Lancer dans le laboratoire ». Dans le laboratoire,
viser dans la scène à droite du panneau puis cliquer pour lancer. Maintenir le
clic droit pour tourner la caméra, utiliser la molette pour zoomer et « Remise
à zéro » pour replacer les cibles. Le bouton « Lire ou signaler
l'interprétation » ramène au dessin et au texte. « Bibliothèque » revient aux
parchemins.

Un sort téléchargé et ses ressources sont conservés localement. Pour le
réutiliser hors ligne, rouvrir le jeu avec le même profil Windows, choisir
« Ouvrir » sur ce parchemin dans la bibliothèque, puis « Lancer dans le
laboratoire ». Le jeu revérifie les empreintes du paquet et des ressources.
Il ne montre que les parchemins liés à l'identité du lecteur ; les anciennes
créations locales de démonstration n'apparaissent pas.

## Premier accès et état de l'essai actuel

Sur une installation sans accès au laboratoire, choisir « Ouvrir mon
invitation » et sélectionner le fichier privé remis par l'opérateur. Le jeu
échange son code à usage unique et conserve l'accès dans le coffre Windows.
Les sorts déjà téléchargés restent accessibles si le laboratoire est
temporairement indisponible ; créer un nouveau sort nécessite sa connexion.

Le dessin rouge-brun courbe envoyé depuis le Player propriétaire a été traité
par de vrais appels Luna A et B. Son sort est passé à l'état « ready », a été
téléchargé, vérifié et ouvert dans le laboratoire du Player. Un clic de lancer
a fait passer le compteur à **1 lancer** et **0 dégât**, conformément au plan
visuel sans effet de dégâts. La reprise de ce même sort hors ligne a aussi été
testée en arrêtant l'API puis en redémarrant le Player. Ces observations ne
constituent ni un verdict artistique ni une écoute humaine du son. Les
diagnostics A/B exécutés auparavant sur un autre trait droit sont distincts de
ce sort joueur.
