# Spectre : fidélité visuelle et mouvement — 20 septembre 2026

## État de cette preuve

Les captures Unity et les contrôles techniques ci-dessous ont réellement été
exécutés. Le build Windows `1.2.2` et sa livraison restent à renseigner après
leur exécution. La validation artistique humaine demeure ouverte : le joueur
a explicitement rejeté l'itération 02, jugée trop proche d'un corps 3D solide.
Ni une correspondance exacte « 1:1 » à l'image de référence, ni une acceptation
humaine des itérations suivantes ne sont revendiquées.

Le travail porte sur le rendu du spectre conservé, son mouvement et son impact.
Aucun appel à Sol, Astra, Luna ou à un autre fournisseur n'a été effectué pour
ces reprises. Elles n'ajoutent aucune attente à la génération d'un sort joueur.

## Paquet réel rejoué

Chaque capture recharge **« Envol du spectre aux longs voiles »**, depuis le
cache joueur, avec ce SHA-256 vérifié avant lancement :

`0ed08fee15298d8e61fc396eddc4f2a5ed2827d0b8efd51639e2771f2162e467`

Le paquet, les trajectoires, les collisions et les valeurs d'effet sont
conservés. Les [métriques de l'itération 03](spectral-fidelity/iteration-03/capture.json)
relèvent **une touche, 12 000 unités internes de dégâts et une impulsion**.
La santé de la cible passe de 100 000 à 88 000. Son déplacement final est
d'environ **1,54 mm** : cette mesure ne prouve pas une projection spectaculaire.

## Exécutions et décisions artistiques

| Itération | Résultat Unity observé | Décision ou portée |
| --- | --- | --- |
| 01 | [1/1 réussi](spectral-fidelity/iteration-01/capture.xml) | Première comparaison du spectre retouché, avec vue du labo et vue latérale. La réussite porte sur le replay et les effets physiques. |
| 02 | [1/1 réussi](spectral-fidelity/iteration-02/capture.xml) | Voiles prolongés et déchirures plus lisibles, mais capuche et visage encore trop solides. **Rendu rejeté par le joueur** ; reprise artistique requise. |
| 03 | [5/5 réussis](../../../game/Logs/spectral-fidelity-03.xml), 17:34:52–17:35:14 UTC | Présence en brume volumétrique, mouvement suivant l'historique de vol, dissipation après impact. Capture du paquet réel et quatre contrôles du renderer. La revue graphique relève encore une masse de tête trop ronde. |
| 04 | [1/1 réussi](../../../game/Logs/spectral-fidelity-04.xml), 17:37:15–17:37:35 UTC | Reprise de la seule capture réelle après allègement et ouverture du halo de tête. Aucun nouveau verdict humain enregistré. |

Les défauts graphiques ayant motivé les reprises 02 et 04 sont des résultats
de revue artistique. Ils ne sont pas présentés comme des échecs du test
automatisé. Inversement, un test réussi ne valide pas la beauté du rendu.

Les cinq contrôles de l'itération 03 couvrent :

- le replay réel, les dégâts, l'impulsion et la disparition du voile retiré ;
- les volumes bornés des formes contrôlées et la libération de leurs ressources ;
- le choix du sujet sémantique malgré la présence du dessin source ;
- l'impact décoratif sans composant de gameplay et sa destruction ;
- la représentation de l'étendue physique de la barrière.

Les journaux bruts correspondants sont conservés dans
`game/Logs/spectral-fidelity-01.log` à `spectral-fidelity-04.log` avec leurs XML.

## Changements rendus

La capuche opaque et le visage plein ont été retirés. Le volume de tête est
produit par un shader de brume intégrant **24 échantillons par rayon**, associé
à trois courants lumineux interrompus. Le mesh enveloppant sert de domaine
de calcul à la brume ; sa surface n'est pas affichée comme un objet solide.

Six nappes translucides constituent le sillage. Leur déformation utilise un
historique borné à **160 positions et orientations réelles**. Elles se déploient
avec la distance parcourue et conservent la courbure des positions précédentes,
avec des ondulations de phases distinctes. Les tableaux et meshes sont réutilisés
pendant le vol. Le propriétaire initial continue de libérer les meshes.

Après la collision, le porteur mécanique est retiré. Le sillage décoratif
s'efface sur **0,62 s**, sans nouvelle touche ni nouveaux dégâts. L'impact
comprend un éclair de **0,24 s**, puis des fragments et particules dont la
durée maximale d'affichage est **1,25 s**. L'annulation du sort libère aussi
les rendus conservés pour leur dissipation.

## Images et animation réellement capturées

Chaque itération conserve **12 PNG** : six images avec la caméra du labo et
six avec une caméra latérale de comparaison. La caméra du jeu reste inchangée.
Les images latérales sont prises depuis une caméra supplémentaire, à champ
de vision de 40°, suivant le même porteur. Les poses sont enregistrées dans
chaque `capture.json`.

- Itération 02 : [vue latérale rejetée](spectral-fidelity/iteration-02/spectre-side-frame-02.png).
- Itération 03 : [vue du labo](spectral-fidelity/iteration-03/spectre-frame-02.png),
  [vue latérale](spectral-fidelity/iteration-03/spectre-side-frame-02.png).
- Itération 04 : [vue du labo](spectral-fidelity/iteration-04/spectre-frame-02.png),
  [vue latérale](spectral-fidelity/iteration-04/spectre-side-frame-02.png),
  [impact](spectral-fidelity/iteration-04/spectre-frame-04.png),
  [métadonnées](spectral-fidelity/iteration-04/capture.json).

La [séquence 03](../../../game/Logs/spectral-sequence-03/sequence.json) est
complète : **73 images par vue à 30 images/s**, soit **2,433 s** lorsqu'elles
sont encodées à cette cadence. Elle suit le lancement, le vol, la collision
et la fin de la dissipation. Les JPEG source et leurs hashes restent dans
`game/Logs/spectral-sequence-03/`. La capture utilise une cible HDR
`ARGBHalf`, le post-traitement URP du labo et une conversion sRGB avant
encodage JPEG de qualité 95.

L'itération 04 possède également sa [séquence complète et horodatée](spectral-fidelity/iteration-04/sequence.json)
et deux vidéos H.264 effectivement encodées, chacune de **73 images**, à
**30 images/s**, d'une durée de **2,433333 s** :

- [Animation avec la caméra du labo](spectral-fidelity/iteration-04/spectre-lab.mp4)
  — 902 026 octets.
- [Animation avec la caméra latérale de comparaison](spectral-fidelity/iteration-04/spectre-side-review.mp4)
  — 1 811 426 octets.

Le [manifeste vidéo](spectral-fidelity/iteration-04/video.json) conserve leurs
SHA-256 et précise que **le son n'a pas été enregistré**. Les JPEG originaux
restent dans `game/Logs/spectral-sequence-04/`. Ces vidéos rendent le mouvement
et la dissipation inspectables ; leur présence ne vaut pas acceptation du style.

Le pas de présentation est fixé à 60 images/s et une image est conservée
toutes les deux images rendues. **Cette cadence fixe n'est pas une mesure
de performance du jeu.** Le périphérique observé est une NVIDIA GeForce RTX 3070.

## Livraison et limites restantes

Le build Windows x64 IL2CPP **1.2.2** a terminé avec le **code 0** sous Unity
6000.3.24f1 : [journal final](spectral-fidelity/build-windows.log), sans erreur
ni avertissement shader trouvé. Le [Player livré](../../../game/Build/WindowsSpectralEnergyPlayable/Palimpseste.exe)
contient **29 fichiers vérifiés, 122 554 797 octets**, hors dossiers `DoNotShip`.
Le [manifeste](spectral-energy-delivery.json) consigne chaque empreinte.

Le [ZIP du jeu](../../../deliverables/Palimpseste-Windows-x64-IL2CPP.zip) fait
**44 089 295 octets**, SHA-256
`6836f2cac5941a58d28b5a07c1b7f5e0b88cc5c51430320a6c9d9d199c1c542e`.
Le Player a été lancé sous le PID **13972**, fenêtre réactive ; le raccourci
Bureau est actualisé et l'API locale répond 200. Aucun appel fournisseur
n'a été effectué pour cette reprise graphique. Le backend reste inchangé.

Restent ouverts : appréciation artistique du joueur, fidélité complète à la
référence, écoute du résultat sonore, vérification de toutes les recettes et
combinaisons de styles, essais sur d'autres machines et benchmark de charge.
Cette reprise ne comporte pas de nouveau parcours complet dessin → modèles
→ téléchargement du sort ; elle rejoue un sort réel déjà conservé.
