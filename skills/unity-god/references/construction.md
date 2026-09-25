# Disséquer puis adapter un effet Unity

## Les cinq sources

| Bibliothèque | Ce qu'il faut chercher | Limite à conserver |
|---|---|---|
| TinyPlay URPShadersCollection | Flux de textures, Fresnel, profondeur, Voronoi, entrée des matériaux et déformation | MIT ; certains ports V2 ne reprennent qu'une partie du graphe |
| Keijiro VfxGraphAssets | Domaines de bruit, forces, strips, axes locaux, graines cohérentes | Unlicense ; bruit de surface et advection sont des mécanismes différents |
| Unity VisualEffectGraph-Samples | Chaînes Spawn/Initialize/Update/Output, populations, événements, historique des traînes | Exemples HDRP, licences tierces et fichiers LFS incomplets à examiner |
| xtaja VFX-Shader | Horloges âge/temps, masque et distorsion indépendants, dissolution, contact profondeur | Aucune licence explicite au commit inspecté : étude des concepts, pas de copie du code |
| Magic Effects FREE | Décomposition de prefabs et coordination des systèmes après acquisition officielle | Package non acquis : aucun détail interne de prefab prétendument inspecté |

Les commits, chemins et SHA exacts sont dans [methods.json](methods.json). Les fiches décrivent les versions inspectées, pas une vérification permanente de la dernière version en ligne.

## Lecture des sources

Pour un Shader Graph, suivre le chemin des sorties couleur/alpha/vertex jusqu'aux textures, coordonnées, paramètres et horloges. Résoudre les GUID par les `.meta` et les matériaux réels. Pour un VFX Graph, relever chaque contexte et son rôle, les attributs conservés entre mises à jour, l'espace de simulation, les événements GPU et les paramètres exposés. Pour un shader texte, identifier blend, culling, profondeur, vertex streams et données de caméra nécessaires. Pour un prefab, relever la hiérarchie et les décalages de démarrage sans déduire le mouvement de la seule forme du mesh.

Ne pas lancer les scripts d'un dépôt pour l'explorer. Lire les fichiers. Un fichier qui commence par `version https://git-lfs.github.com/spec/v1` est un pointeur, pas l'asset. Les graphes matérialisés peuvent rester étudiables même si leurs textures d'exemple manquent ; signaler chaque dépendance absente.

## Fiche d'adaptation utile

- **Phénomène** : ce qui se déplace, tourne, s'accumule ou se dissipe réellement.
- **Source** : fichier et mécanisme observé ; licence et accès aux octets.
- **Structure** : support continu choisi, orientation, proportions et invariants.
- **Temps** : vitesse physique, déformation, UV, âge des particules, enveloppes ; raccord de boucle explicite.
- **Couches** : le core porte la reconnaissance ; matière et atmosphère ont des amplitudes et budgets distincts.
- **Contact** : propriétaire gameplay, normale, transition de pose, émission secondaire et extinction.
- **Innovation** : changement motivé de paramètres, combinaison compatible ou nouveau mécanisme à implémenter.
- **Preuve** : fichier livré, mapping des paramètres, capture ou mesure réellement obtenue ; écart restant.

## Adapter sans recopier les défauts de l'exemple

Un ruban peut reprendre le principe tête/historique sans reprendre la capacité de centaines de milliers de particules du sample. Un portail peut reprendre plusieurs populations aux fonctions différentes sans les superposer arbitrairement. Un bouclier peut utiliser le principe du Fresnel même si son contact profondeur reste à porter. Ne pas emprunter le nom de l'effet pour prétendre avoir repris toute sa construction.

Pour une nouveauté du moteur : préparer un composant déterministe et borné, préserver les chemins V1, documenter la provenance et fournir les ressources nécessaires. Autoriser la sélection runtime uniquement après livraison du programme. Le test artistique reste distinct de la compilation et de l'exécution.
