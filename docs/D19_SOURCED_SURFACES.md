# D19 — Surfaces VFX issues des bibliothèques

## Intégration dans le rendu

`game/Assets/Palimpseste/Resources/SpellImageConstruction.shader` propose quatre profils de surface supplémentaires. Les matériaux générés peuvent sélectionner chaque profil par partie ; ce choix ne remplace ni la géométrie décrite, ni les mouvements physiques, ni la composition des couches.

Les textures ci-dessous sont effectivement échantillonnées en RGB par `_SurfaceTex`. Ajouter une texture RGB au masque alpha historique `_ResourceTex` n'aurait pas apporté ce résultat. Les graphes TinyPlay ont été étudiés puis portés en HLSL dans le shader réellement chargé par le moteur. Les graphes eux-mêmes et leurs scènes de démonstration ne sont pas importés.

| Profil | `_SurfaceProfile` | Ressource Unity | Construction portée |
|---|---:|---|---|
| `plasma` | 1 | `SourcedVfx/tinyplay_plasma` | Deux défilements opposés du motif, contour Fresnel coloré, déformation légère, UV en spirale et réfraction de la scène opaque |
| `force_field` | 2 | `SourcedVfx/tinyplay_noise` | Motif animé, bord Fresnel, contact lumineux avec la profondeur de la scène, intérieur peu rempli |
| `toxic` | 3 | Aucune texture supplémentaire | Cellules Voronoi animées, relief issu de leur hauteur, lumière spéculaire et émission concentrée dans les cellules |
| `spectral_flow` | 4 | `SourcedVfx/tinyplay_noise` | Advection par gradients croisés, fins contours lumineux, vapeur peu opaque et couverture analytique des rubans |

Le profil 0 conserve les matériaux historiques. `_Opacity`, `_Emission`, `_Color`, `_Envelope`, `_Reveal`, `_Dissolve`, `_BehaviorAge` et `_BehaviorFlow` gardent leur rôle. Tous les profils passent par la même apparition, érosion et extinction que les autres surfaces. L'horloge explicite du sort pilote aussi leur mouvement de surface.

## Provenance TinyPlay

- Dépôt : <https://github.com/TinyPlay/URPShadersCollection>
- Commit étudié : `6e663fffccd00a4cce837644a29f6e8f82a6e372`.
- Licence : MIT, copyright TinyPlay 2022. Notice originale copiée sans réécriture dans `assets/sourced-vfx/licenses/TinyPlay-URPShadersCollection-MIT.txt` et `game/Assets/Palimpseste/Resources/SourcedVfx/TinyPlay-URPShadersCollection-MIT.txt`.
- Graphes portés : `Shaders/VFX/PlasmaShader.shadergraph`, `Shaders/VFX/ForceFieldShader.shadergraph`, `Shaders/Environment/ToxicShader.shadergraph`.
- Matériaux lus pour résoudre les vraies dépendances : `Materials/VFX/PlasmaShader.mat` et `Materials/VFX/ForceFieldShader.mat`. Les textures par défaut sérialisées dans ces deux graphes pointent vers des GUID absents du clone ; les matériaux livrés fournissent les références résolues utilisées ici.

| Fichier original | Copie livrée | SHA-256 des octets originaux conservés |
|---|---|---|
| `Textures/jellyPlasmaTexture.jpg`, RGB, 2682 × 1788 | `game/Assets/Palimpseste/Resources/SourcedVfx/tinyplay_plasma.jpg` | `e60137678e9caf011d113b42ae44aba05efb6393243005c1fa520c35d16554c5` |
| `Textures/Noise.png`, RGBA, 64 × 64 | `game/Assets/Palimpseste/Resources/SourcedVfx/tinyplay_noise.png` | `da338fcff419c98214a1723239a1f1234e9c4d57c795d1ac35e958e58cb3993e` |

Les paramètres d'import sont repris des sources avec de nouveaux GUID locaux. Les images ne sont ni régénérées ni retouchées. Les mipmaps et le filtrage restent actifs.

Adaptations : la palette vient du plan, les intensités et les déplacements sont bornés, les textures sont mélangées avec les valeurs HDR du sort, et la fin de vie reste pilotée par notre contrat. Le relief du plasma est volontairement limité à une petite variation de la surface. Le profil toxic adapte le réseau Voronoi et sa hauteur ; il ne reproduit pas le shader Lit original à l'identique. La profondeur de force_field traite les projections perspective et orthographique. La réfraction du plasma utilise uniquement la texture opaque URP ; elle ne réfracte pas les autres transparents.

## Provenance Keijiro

- Dépôt : <https://github.com/keijiro/VfxGraphAssets>
- Commit étudié : `5013c195305288fab61cd71f72a4628dbe9d0ea4` ; package `jp.keijiro.vfxgraphassets` 3.10.1.
- Licence : Unlicense. Notice originale copiée dans `assets/sourced-vfx/licenses/Keijiro-VfxGraphAssets-Unlicense.txt` et dans `game/Assets/Palimpseste/Resources/SourcedVfx/Keijiro-VfxGraphAssets-Unlicense.txt`.
- Sources : `Packages/jp.keijiro.vfxgraphassets/Subgraph/Divergence Free Noise 3D.vfxoperator` et `Packages/jp.keijiro.vfxgraphassets/Shader/Textureless Strip.shadergraph`.
- Code exécuté : `game/Assets/Palimpseste/Resources/SourcedSurfaces/KeijiroDivergenceFreeNoise.hlsl`, inclus par `SpellImageConstruction.shader`.

Le corps HLSL de l'opérateur `cross(SimplexNoiseGrad(p1).xyz, SimplexNoiseGrad(p2).xyz)` est conservé avec un nom préfixé. La fonction SimplexNoiseGrad et ses dépendances MIT sont déjà embarquées dans `Resources/SourcedNoise`. La couverture du ruban est portée depuis les opérations du graphe Textureless Strip avec une protection contre la division par zéro au centre.

Le shader utilise ce champ pour déformer les coordonnées de surface ; son amplitude est bornée pour garder la silhouette. Ce traitement visuel ne constitue pas un solveur de fluide et ne remplace pas la rotation ou le déplacement prescrits dans le plan. Le profil spectral garde une faible couverture de matière et réserve sa luminosité aux fins filaments.

Importer tout le package amont aurait demandé `com.unity.visualeffectgraph` 17.0.0 et `jp.keijiro.shadergraphassets` 2.5.2, avec la dépendance de bruit utilisée par l'opérateur. L'intégration ciblée utilise les calculs nécessaires et le bruit déjà livré ; aucun package HDRP, binder ou script tiers n'est exécuté ni installé.

## Autres bibliothèques examinées

- `xtaja/VFX-Shader` : README et options de distorsion, dissolution, profondeur et durée de vie examinés. Aucun code ou asset copié en l'absence de licence explicite dans le clone.
- `Unity-Technologies/VisualEffectGraph-Samples` : README et inventaire local des exemples Portal, RibbonPack, Bonfire et Meteorite examinés. Aucun graphe HDRP ou fichier LFS incomplet importé pour ces quatre surfaces.
- `Magic Effects FREE` : fiche locale d'acquisition et de licence relue dans `assets/sourced-vfx/references.json`. Package non acquis ; aucune texture, aucun prefab et aucune construction interne de ce package ne sont présentés comme intégrés.

## État et limites

Les sources, textures, licences et branchements du shader sont ajoutés pour les futurs plans. **Backend compilé et publié avec le code 0 ; Player 1.7.0 compilé et empaqueté**, avec sortie Unity 0 et absence d'erreur de shader dans le journal final. Le premier build Unity avait pourtant indiqué Success avec une erreur HLSL sur le nom réservé `point` ; ce build a été écarté, le nom corrigé et le packaging refuse désormais toute ligne `Shader error`. La première compilation backend a également échoué sur une déclaration locale en double, corrigée avant publication. Les tentatives restent consignées dans les preuves.

**Installation terminée le 24 septembre à 23:20:40 Paris**, après la génération déjà admise, sans l'annuler. Les services ont redémarré, la suspension des prises de jobs est retirée et la concurrence reste à `0`. **Player 1.7.0 ouvert à 23:20:41, PID 33920.** [Installation](../evidence/public/backend/sourced-surfaces-d19-installation.json) · [Preuves de compilation et limites](../evidence/public/backend/sourced-surfaces-d19.json). Aucun test, aucune génération de démonstration et aucune capture de validation n'ont été lancés par cet audit et ce portage. La relecture indépendante était statique, sans note Dream-loop visuelle inventée.

La disponibilité d'un profil n'assure pas à elle seule la ressemblance avec les 21 cases : le plan doit sélectionner une forme, un emplacement, des couches, des vitesses, une densité et des transitions adaptés. Les anciennes données de sorts ne sont pas réécrites pour forcer un nouveau profil.
