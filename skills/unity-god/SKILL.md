---
name: unity-god
description: Concevoir et améliorer des effets Unity 6.3 URP en étudiant la construction réelle des shaders, graphes et systèmes VFX des bibliothèques de Palimpseste, puis en adaptant leurs méthodes au blueprint, au mouvement et au rendu souhaités. Utiliser pour créer ou corriger un sort, enrichir le moteur VFX ou préparer sa construction dans la boucle V2.
---

# UNITY GOD

Ce skill est une base de méthodes vérifiables, pas un modèle réentraîné ni une promesse de qualité automatique. Commencer par des effets existants dont on comprend la construction. Adapter leurs principes à l'intention du nouveau sort, puis juger le résultat en mouvement.

## Choisir le contexte d'exécution

- **Développement Unity** : lire les sources pertinentes, adapter les programmes et importer les ressources autorisées dans le projet. Suivre les droits de la session et préserver les sorts historiques. Consulter [la démarche de construction](references/construction.md).
- **Génération joueur** : utiliser uniquement [les instructions runtime](references/runtime.md) et le catalogue figé du job. Produire un blueprint et un dossier de méthodes. Le skill ne donne au worker ni outils, ni accès aux sources, ni possibilité d'importer ou de construire du code.

## Étudier les effets avant de composer

Consulter les cinq sources de [methods.json](references/methods.json), puis lire les fiches correspondant à la matière, au mouvement et au core. Chaque fiche cite des fichiers réellement inspectés, leur commit et leur SHA ; elle distingue observation, adaptation et capacité du Player.

En développement, ouvrir les fichiers amont pertinents dans le cache indiqué par `reference/vfx-libraries.inventory.json` du projet. Suivre les connexions du graphe ou les étapes du shader et leurs entrées de matériau. Un README, une miniature, un GUID non résolu ou un pointeur LFS ne prouve pas la construction d'un effet. Vérifier les octets et la licence avant réutilisation. Si la source est indisponible, conserver cette limite au lieu d'en inventer le fonctionnement.

Extraire ce qui rend l'effet convaincant : représentation principale, coordonnées, fonctions temporelles, émission, vitesse, masque, matière, population secondaire, contact et extinction. Séparer le principe réutilisable des valeurs propres à l'exemple. Le catalogue propose un point de départ ; il ne dispense pas d'ouvrir le code lorsque le moteur doit apprendre une nouvelle technique.

Le catalogue de méthodes ne remplace pas le schéma du blueprint ni les capacités précompilées. Consulter aussi les contrats et règles numériques fournis : une animation du core ou un contact gameplay peut être disponible sans fiche source dédiée. Distinguer cette capacité de celle d'un shader, par exemple un impact contrôlé et une intersection lumineuse calculée par profondeur.

## Construire, adapter, inventer

1. Partir de l'intention et des invariants du blueprint. Choisir une structure adaptée au phénomène.
2. Sélectionner les méthodes sources qui résolvent ses besoins et expliciter leur rôle. Vérifier `runtime_selectable`, `runtime_scope` et les limites de chaque méthode.
3. Définir l'adaptation : échelle, palette, axes, fréquences, enveloppes, densité, hiérarchie des couches et réaction au contact. L'innovation doit décrire une combinaison ou une variation concrète, pas seulement « plus de particules ».
4. Vérifier la silhouette et le mouvement sans décoration, puis ajouter matière et atmosphère. Le défilement UV ne remplace pas la rotation d'un vortex ; un bruit de surface ne constitue pas une force physique ; une attraction centrale ne constitue pas une orbite.
5. Comparer le rendu réel au comportement et à la référence. Utiliser Dream-loop Pro lorsqu'une cible visuelle est fournie et que les captures sont autorisées. En V2, la planche échantillonne le blueprint continu ; elle ne crée pas de géométries indépendantes.
6. Renvoyer chaque écart à sa cause : structure, mouvement, matière, contact ou budget. Conserver les invariants approuvés et les acquis des passes précédentes.

## Trace de construction

Conserver la méthode choisie, la source et sa révision, les champs du blueprint qui l'exécutent, ce qui est repris, ce qui est adapté et ce qui reste indisponible. Vérifier la cohérence des assertions avec le programme effectivement livré.

Une technique nouvelle qui exige un shader, un graphe ou un composant absent devient un travail de développement explicite. Après implémentation et compilation réelles, mettre à jour sa capacité dans le catalogue avec une preuve. Ne pas présenter une méthode étudiée comme un asset importé ou un effet déjà accepté.
