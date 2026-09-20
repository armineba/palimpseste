# Composition VFX URP — 20 septembre 2026

## Finition D11 / client 1.2.1

Les références de soin vert et de jaillissement violet conduisent à un vocabulaire plus stylisé : grandes spirales effilées, sceau et couronne en rotation inverse, jupe lumineuse ouverte, particules verticales, étoiles et brume. Les soins utilisent un mouvement ascendant plus calme ; les projectiles conservent une forme centrale distincte avec des traînées fines. Les volumes énergétiques ont un cœur compact et une enveloppe transparente ; les voiles sont plus lisses et moins criblés de découpes brillantes.

Le shader original `SpellStylized` fournit sept familles de surfaces. Chaque composition peut avoir quatre couches de particules, au maximum 48 par couche. Les impacts s'éteignent et sont supprimés après 1,25 seconde. Les couleurs et motifs sont toujours déterminés par les données de sort validées ; aucun nouvel appel de génération n'est ajouté. Voir [la direction de revue D11](stylized-vfx-review.md) pour la distinction entre fixtures et vrais sorts conservés.

Les captures D11 préservent le HDR avant bloom. Les anciennes captures D10 en ARGB32 coupaient l'intensité lumineuse dans le tampon de capture : elles ne décrivent pas fidèlement cet aspect du Player. Le jeu Windows utilisait déjà un tampon HDR ; 1.2.1 active en plus l'étalonnage HDR et affine le bloom.

Cette note décrit le code source de la composition visuelle. Son intégration et le build Windows sont suivis dans [l'état de réalisation](../IMPLEMENTATION_STATUS.md). Les captures Unity et la recette artistique sont des preuves séparées : cette description ne vaut pas approbation du résultat visuel.

## Direction

Les références utilisateur montrent une silhouette identifiable, un cœur lumineux compact, de larges mouvements secondaires et une extinction propre. La nouvelle présentation assemble ces éléments dans Unity. Elle ne projette pas la forme du dessin sur le projectile.

Le spectre possède une capuche ouverte en volume, une cavité sombre, un cœur nacré, un buste et quatre pans de tissu. Les pans sont de vrais maillages courbes de 2,5 à 3,15 unités avant l'échelle décorative ; un shader anime leurs plis, les ajoure et illumine les déchirures. Le concept original `spectral-veils-target.png` sert de cible artistique ; il n'est pas collé dans la scène comme un faux effet 3D.

Les autres formes gardent leur objet identifiable. Les objets massifs conservent leur surface solide ; les formes énergétiques disposent d'une membrane transparente. Un vocabulaire de sceaux, rubans, fractures, spirales, couronnes d'impact, piliers et particules compose le mouvement autour de cet objet.

### Provenance du concept

[spectral-veils-target.png](spectral-veils-target.png) est une image originale produite par l'outil de génération d'image pendant le développement du 20 septembre 2026. Elle représente une cible de direction artistique : capuche creuse, cœur nacré, volumes spectraux violets, longues traînes translucides, naissance et impact lisibles. Elle n'est **ni une capture Unity, ni un résultat de génération de sort joueur, ni une preuve du rendu atteint**. Seules les captures du moteur et le Player construit peuvent montrer ce qui est réellement exécuté. Cette image de référence n'ajoute aucun appel ni attente au parcours joueur.

## Données et compatibilité

`appearance.vfx` sélectionne `style`, `motif`, `density`, `aura_cm`, `charge_ms` et `impact`. Il ne contient aucun shader, code, chemin d'asset ou instruction à exécuter. Quand ce profil est absent, les anciennes palettes et formes déterminent une composition par défaut : les sorts déjà sauvegardés profitent aussi du nouveau rendu.

La charge est une mise en scène visuelle au point de lancement ; elle ne retarde pas le mécanisme de lancement. Les zones et barrières gardent les règles de collision du moteur existant. Les ondes et particules d'impact sont décoratives et ne déclenchent pas un deuxième dégât.

## Ressources bornées

- Quatre couches de particules au maximum, chacune limitée à 48 particules.
- Quatre lumières ponctuelles décoratives simultanées au maximum ; aucune ombre supplémentaire.
- 48 échantillons par filament de faisceau et deux filaments.
- Impact autonome supprimé après 1,25 seconde.
- Chaque composition possède ses maillages et ses matériaux et les détruit avec le sort.
- Aucun téléchargement, appel fournisseur ou génération d'image à l'exécution des effets.

## Sources consultées

- [Unity 6.3 — Particles Unlit, URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/particles-unlit-shader.html) : transparence, mélange additif, émission et rendu des faces. Les shaders du projet sont originaux et utilisent le pipeline URP.
- [Magic Explosion Spell breakdown — Real Time VFX](https://realtimevfx.com/t/magic-explosion-spell-breakdown/19039) : référence de décomposition temporelle et de couches d'un effet de magie. Aucun asset de cette présentation n'a été importé.
- Montages transmis par l'utilisateur : cercles magiques, filaments électriques, rubans, explosions et voiles. Références visuelles, sans extraction de ressources commerciales.

L'achat ou la redistribution d'un pack de l'Asset Store ne fait pas partie de cette implémentation. Les deux shaders, maillages procéduraux et compositions sont présents dans le dépôt.
