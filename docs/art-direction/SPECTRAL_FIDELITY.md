# Spectre : direction artistique et critères de revue

## Intention actuelle du joueur

Le joueur veut un effet magique stylisé, riche et fluide, proche de la
[référence de direction artistique](spectral-veils-target.png). Il a refusé
la capuche et le visage trop solides de l'itération 02, puis demandé une
présence magique sans corps réel, un sillage organique et un impact travaillé.
La caméra du labo doit rester inchangée.

L'image de référence a été produite pour guider le travail artistique.
Elle ne constitue pas une capture du moteur Unity. Elle donne une cible de
silhouette, de matière et de lumière ; une correspondance exacte « 1:1 »
n'a pas été démontrée et n'est pas enregistrée comme acquise.

## Lecture visuelle recherchée

La partie avant doit suggérer une apparition par des courants lumineux et
des variations de densité. Elle doit laisser voir le décor à travers des
zones ouvertes. Le foyer perlé éclaire la brume violette tout en conservant
des détails autour de lui. Une masse ronde uniforme, une coque opaque ou
un disque blanc plein ne satisfait pas cette intention.

Le sillage doit conserver plusieurs nappes distinctes, longues et asymétriques.
Des différences de largeur, de hauteur, de courbure et de phase doivent
produire des espaces lisibles entre elles. Les ruptures de matière et les
particules doivent accompagner les courants. Le mouvement doit être visible
sur une séquence, avec une continuité entre apparition, vol et impact.

La trajectoire visuelle du sillage doit conserver la mémoire du déplacement.
Lorsqu'une direction change, toutes les nappes ne doivent pas pivoter comme
une pièce rigide. Leur longueur se déploie progressivement après le lancement.
À la collision, une accentuation lumineuse courte précède la dispersion des
fragments et l'extinction du sillage.

## Mise en œuvre actuelle

| Élément | Mise en œuvre |
| --- | --- |
| Présence avant | Brume volumétrique à 24 échantillons par rayon, trois courants lumineux discontinus ; aucun visage ou vêtement opaque. |
| Sillage | Six meshes de nappes translucides, formes asymétriques, déchirures et variations d'énergie. |
| Mouvement | Historique de 160 positions/orientations au maximum, déformation des rangées selon le chemin réellement parcouru, ondulations de phases distinctes. |
| Apparition | Déploiement lié à la distance parcourue et sceau de lancement. |
| Collision | Éclair de 0,24 s, fragments et particules durant au maximum 1,25 s. |
| Extinction | Sillage décoratif pendant 0,62 s après retrait du porteur mécanique ; aucune touche supplémentaire. |

Le mesh servant au calcul de brume ne doit pas apparaître comme une sphère
solide. L'itération 03 restait visuellement trop ronde ; l'itération 04
allège la densité et ouvre cette forme. Ces ajustements attendent le verdict
du joueur. La réussite des tests techniques ne remplace pas ce verdict.

## Comparaison dans Unity

La caméra du labo regarde une grande partie du vol dans son axe. Les nappes
orientées derrière le projectile sont donc raccourcies par la perspective.
La référence les montre principalement de côté. Les deux vues sont conservées
pour permettre de juger séparément la silhouette et sa lisibilité en jeu :

- **Vue du labo** : pose, optique et post-traitement de la caméra du jeu,
  capture de son arène sans l'interface du parchemin.
- **Vue de comparaison** : caméra supplémentaire placée bas sur le côté et
  légèrement en avant, champ de vision de 40°, suivant le même porteur.
  Ses poses sont explicitement consignées ; elle ne change pas la caméra du jeu.

Le test `CachedSpectrePresentationTests.CaptureExistingSpectreInLab` recharge
le paquet joueur existant et vérifie son hash avant de le rejouer. Il conserve
les contrôles de dégâts, d'impulsion, de retrait du porteur et de libération
du sillage. Aucun appel modèle n'est nécessaire pour cette revue.

Les six instants fixes montrent naissance, vol et impact. L'option
`PALIMPSESTE_SPECTRE_SEQUENCE_DIR` enregistre aussi l'animation réelle à
30 images/s ; `PALIMPSESTE_SPECTRE_SEQUENCE_SIDE=1` ajoute la vue latérale.
Le pas de présentation de 60 images/s rend les comparaisons temporelles
reproductibles. Il ne représente pas un benchmark de FPS.

## Critères encore soumis à appréciation humaine

- Présence magique lisible, sans impression de personnage plein ou d'œuf lumineux.
- Nappes distinctes, transparence, détails et teintes préservés pendant le mouvement.
- Ondulations souples et continuité du sillage dans les courbes.
- Impact visible puis disparition progressive, sans coupure brutale ni dégâts tardifs.
- Résultat satisfaisant avec la caméra habituelle du labo.

Les [preuves du 20 septembre](../../evidence/public/unity/spectral-fidelity-2026-09-20.md)
séparent les captures exécutées, les résultats techniques, le refus artistique
de l'itération 02 et les validations encore ouvertes. Le Player 1.2.2 a été
construit et livré ; les [empreintes de livraison](../../evidence/public/unity/spectral-energy-delivery.json)
identifient l'exécutable, ses fichiers et le ZIP réellement produits.

Pour revoir l'animation réellement capturée de l'itération 04 :

- [Caméra du labo](../../evidence/public/unity/spectral-fidelity/iteration-04/spectre-lab.mp4).
- [Caméra latérale de comparaison](../../evidence/public/unity/spectral-fidelity/iteration-04/spectre-side-review.mp4).

Ces vidéos durent 2,433333 s à 30 images/s et ne comportent pas de son.
La fidélité exacte à la référence et la satisfaction artistique du joueur
restent ouvertes ; les prochains ajustements doivent partir de son verdict
sur ces images et sur le rendu dans le jeu.
