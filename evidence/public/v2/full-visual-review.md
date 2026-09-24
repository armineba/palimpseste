# Revue visuelle indépendante — fixture météore V2

Revue des captures existantes le 25 septembre 2026. Aucun lancement de jeu,
test supplémentaire, appel fournisseur ou génération d'image effectué pour
cette revue. Ce document décrit **l'acquisition avant correction du banc de
validation**, identifiée par les hashes ci-dessous.

## Verdict

**Le rendu complet n'est pas accepté artistiquement.** La structure présente
une silhouette de comète ou projectile allongé reconnaissable. La traduction
visuelle de « roche incandescente » reste faible : masse orange très régulière,
translucide, surface lisse, filaments peu contrastés et absence d'un foyer chaud
visuellement dominant. Le résultat convient à une fixture structurelle, mais
n'atteint pas le niveau de VFX demandé par le créateur. Aucun score de 8/10 ou
d'acceptation humaine n'est attribué.

| Porte | Verdict de cette revue | Observation |
| --- | --- | --- |
| C — rendu normal | **Non accepté** | La silhouette reste lisible et continue. Les couches supplémentaires apportent peu de profondeur, de matière ou d'énergie. L'objet ressemble davantage à une forme de comète en gel orange qu'à une roche incandescente. |
| D — impact visuel | **Preuve visuelle inutilisable en l'état** | Les quatre contacts sont réellement mesurés, mais leurs décors sont magenta. Ce défaut du banc d'acquisition empêche de juger correctement contraste, flash, pose de contact et émission secondaire. |
| E — caméra du jeu | **Lisibilité élémentaire observée, porte complète non acceptée** | Le projectile se distingue du sol sombre à la distance de jeu. Sa matière et son impact restent discrets. Une bande magenta contamine la partie gauche de `game_camera.png`. |
| F — mouvement | **Fonctionnement observé, qualité artistique non acceptée** | Le projectile avance réellement, atteint la cible puis perd son opacité sans éclatement involontaire visible. L'animation de forme est très faible et l'impact manque de force. Les images horodatées confirment le déroulement ; une planche statique ne démontre pas à elle seule une fluidité élevée en jeu. |

## Ce que montrent effectivement les preuves

- `normal_sheet.png` montre la même masse continue qui apparaît, reste stable
  puis s'efface. Aucun empilement apparent de sphères ou de pièces détachées
  n'est visible sur cette fixture.
- `normal_sheet_1_3.png`, examinée à sa résolution d'origine, confirme une
  surface orange semi-transparente avec quelques filaments. La matière de
  roche, l'incandescence et la densité ne sont pas convaincantes.
- `motion_sheet.png`, `motion_010.png` et `motion_013.png` montrent un vrai
  déplacement jusqu'au mannequin central, suivi d'une disparition progressive.
  Le « compress then dissolve » est partiellement perceptible ; ni son poids
  ni son énergie ne sont fortement exprimés.
- Le manifeste contient **78 images temporelles**, jusqu'à **6,629 s**,
  **330 ticks**, zéro porteur actif à la fin et une touche gameplay.
- Sol, mur, cible et oblique ont chacun `contact_count = 1`, avec des normales
  différentes et `core_topology_preserved = true`. Cela confirme les scénarios
  mécaniques, pas leur réussite artistique.
- Le pic déclaré dans les mesures est de 48 particules, 2 147 vertices,
  3 matériaux et 797 microsecondes pour la mise à jour CPU observée. Les draw
  calls sont une estimation. Le temps GPU et l'overdraw restent non mesurés.
- Les **86,20 images/s** du manifeste concernent les requêtes URP à 512 × 512
  avec lecture GPU et sans encodage PNG. Ce nombre n'est pas le FPS du jeu.

## Défauts à traiter

1. **Banc de validation :** attribuer des matériaux URP explicites aux surfaces
   de test et nettoyer le RenderTexture avant chaque caméra. La bande magenta
   hors viewport est compatible avec une image précédente conservée dans la
   cible ; c'est un diagnostic probable, pas une preuve issue d'un débogueur GPU.
2. **Interprétation visuelle du blueprint :** la couche de matière doit rendre
   une roche chauffée reconnaissable, sans perdre le core validé. La simple
   couleur orange et des filaments de surface ne suffisent pas.
3. **Énergie et contact :** rendre lisibles le foyer lumineux, le mouvement
   secondaire et la réaction à l'impact. Les particules comptées par le moteur
   ne sont pas automatiquement perceptibles dans l'image.
4. **Vérification ultérieure :** réexaminer les captures propres après correction
   du banc. La suppression du magenta ne vaut pas acceptation du VFX.

Ces remarques concernent la fixture et les preuves de la nouvelle pipeline.
Elles ne demandent aucune modification d'un sort historique.

## Identité des fichiers examinés

Racine : `.runtime/v2-full-runtime/output/`.

| Fichier | SHA-256 |
| --- | --- |
| `normal_sheet.png` | `813ff249b8f49b906ce10226dd2384feb87a8d7864481331892794ad99932907` |
| `motion_sheet.png` | `9d559ac68127499ac89e9bf4495b9b026c9dcc1b30135d3654a26e0cfc101201` |
| `game_camera.png` | `11d44f1b1bc8162025224b5b774acbae21c602e01c7ab77b3cdf8faa62290899` |
| `impact_sheet.png` | `45ebaaa4be0bb2d7f36f946bd33b2d60f0107dfc7c7d512a824eb95fbbb78936` |
| `validation.json` | `42ae2358fc9e25ae891e3bdc70b1f6ea0dff492d873011d8025fb83e1986af35` |

Paquet : `733a2e5a496f592d39cca1c9ad0fdef15d1211a7cb3e3660694fea3ba461a443`.
Blueprints : `48f0972929c7eb9b44b6ae8e1c129b2e47e8154a4777e1b99704785e8c25a14e`.

Référence sémantique lue : `examples/v2-validation/description.json` et
`examples/v2-validation/blueprint.json`. Cette revue n'est donc pas une
nouvelle lecture aveugle et ne remplace pas les revues aveugles déjà séparées.

## Réinspection de l'acquisition finale après correction du banc

Fichiers relus : `.runtime/v2-full-final/output/impact_sheet.png`,
`game_camera.png` et `validation.json`. Aucune nouvelle exécution n'a été
lancée pour cette réinspection.

**Le magenta a disparu.** Les quatre surfaces de contact possèdent maintenant
un rendu neutre et lisible. Le hors-viewport de la caméra du jeu est propre et
noir. Les images D et E sont désormais exploitables. Le core reste cohérent au
contact, mais sa matière translucide et sa faible réaction visuelle restent
apparentes. L'emplacement du projectile se lit dans le labo ; cela ne suffit
pas à accepter le niveau artistique demandé.

**L'acceptation artistique C/E/F reste refusée ou non démontrée.** La planche
normale est strictement identique à l'acquisition précédente, avec le même
SHA-256 `813ff249b8f49b906ce10226dd2384feb87a8d7864481331892794ad99932907`.
La correction du banc ne constitue donc pas une amélioration du VFX, ni une
nouvelle validation de sa fluidité. Aucun score d'acceptation n'est ajouté.

Le manifeste final confirme un contact pour chacun des quatre scénarios,
la topologie conservée et zéro physique décorative. Il contient **82 images
temporelles**, jusqu'à **6,627 s**, **329 ticks**, une touche et zéro porteur
actif à la fin. Les pics mesurés sont de 48 particules, 2 147 vertices,
3 matériaux et 480 microsecondes CPU. Les **68,40 images/s** concernent
uniquement les requêtes URP avec lecture GPU, selon la portée déjà précisée.
Le temps GPU et l'overdraw restent non mesurés.

| Fichier final | SHA-256 |
| --- | --- |
| `impact_sheet.png` | `a07c81235204774d95200b7cbe5f6da8fe99bf5591e052f44597c99447611762` |
| `game_camera.png` | `dd59ad06f789820ddc3c50293d6313e3efb5eea79abeaf98e2eb0d4a26567aa4` |
| `motion_sheet.png` | `fe3f381dc368902764da72f21649020e24231e99a94de973197460895a58cc7e` |
| `validation.json` | `b0f0530cfba8a6dc04e90db7f641e7e7b76cebfe328c97e865098baf6cefbb6a` |

Les hashes du paquet et des blueprints sont inchangés par rapport à la première
acquisition. L'acceptation humaine reste à recueillir.
