# Palimpseste — consignes de travail

## Construction et amélioration des sorts

Lire `docs/BIBLIOTHEQUES_VFX_OBLIGATOIRES.txt` avant toute construction ou modification du rendu d'un sort. Le créateur impose l'exploration des cinq bibliothèques listées, puis la réutilisation des ressources et techniques pertinentes comme point de départ.

- Explorer les cinq sources et leurs exemples ; utiliser le cache local et l'inventaire épinglé pour éviter de tout télécharger à chaque passage.
- Choisir selon la description complète, la planche APPARITION / STABLE / DISPARITION et le mouvement physique attendu. Consulter toutes les sources ne signifie pas superposer tous leurs effets.
- Partir des shaders, textures, graphes, prefabs ou techniques adaptés et autorisés. Documenter les chemins amont, commits, licences, adaptations URP et éléments retenus dans le travail de développement.
- Une absence de licence, un fichier LFS manquant ou une ressource non acquise doit rester explicite. Ne jamais présenter un téléchargement comme une intégration dans le Player.
- Unity 6.3 / URP reste la cible. Étudier les exemples HDRP puis adapter les éléments compatibles ; ne pas remplacer le pipeline du projet.
- Après l'intégration des ressources dans le moteur, utiliser Dream-loop Pro pour rapprocher le rendu de la planche, en respectant la description et le cycle complet du sort.

## Séparation développement / génération joueur

Les téléchargements, imports et modifications de code appartiennent au développement. Le worker reçoit des fiches de recherche contrôlées et les ressources déjà livrées dans le Player. Aucun parchemin ne peut installer un package, exécuter un script, modifier le projet ou lancer un build logiciel.

## Livraison

Le créateur teste lui-même : ne pas lancer de tests, de génération de démonstration ni de capture de validation depuis l'agent. Distinguer recherche, téléchargement, compilation, déploiement et acceptation visuelle. Maintenir `docs/NEXT_ACTIONS.md` avec les opérations réellement réalisées et ce qui reste à faire.
