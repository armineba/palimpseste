# Contrat canonique V2

Le plan conserve son enveloppe `sp.plan/1.0`, mais chaque nœud V2 contient obligatoirement `blueprint_v2.schema_version = sp.blueprint/2.0`. Le client minimal est 1.8.0. Un plan mélangeant des nœuds V1 et V2 est refusé. Le choix de version vient de l'admission persistée, pas du contenu d'un dessin.

Les schémas historiques restent inchangés. `ContractJson` sélectionne les nouveaux schémas avant validation ; la sérialisation d'un ancien nœud omet entièrement le nouveau champ nul.

## Sections

| Section | Responsabilité |
| --- | --- |
| intent | Sujet, action, matière, silhouette, cible, comportement attendu |
| archetype | Famille principale puis familles complémentaires |
| identity | Invariants, topologie fixe, éléments suivis, palette et repère logique |
| structural_core | Géométrie canonique continue, sections, branches attachées, résolution bornée |
| motion | Trajectoire gameplay et déformations continues ; durée exacte du carrier |
| phases | Apparition / actif / disparition dans le temps normalisé 0–1000 |
| rendering_layers | Core / énergie secondaire / atmosphère ; ressources livrées uniquement |
| physics | Collision racine possédée par le moteur ; aucune physique décorative |
| impact | Pose, réaction, survie du core et destruction contrôlée |
| disappearance | Mode, direction et résidu temporel borné |
| unity_implementation | Composants précompilés réellement disponibles ; aucun code généré |
| validation_rules | Gates obligatoires et budgets ; aucun verdict généré par le planner |

## Sélection du core

Le compilateur vérifie la compatibilité de la **famille principale**, première entrée d'archetype. Les autres familles décrivent les comportements combinés.

| Archétype principal | Cores autorisés |
| --- | --- |
| creature | swept_tube, branched_surface |
| projectile | radial_volume, swept_tube, ribbon, branched_surface |
| beam | beam, ribbon |
| ribbon | ribbon, swept_tube |
| orbital | controlled_swarm, ribbon, planar_field |
| area | planar_field, radial_volume, vortex_surface, ribbon |
| explosion | radial_volume, controlled_swarm |
| summon | branched_surface, swept_tube, radial_volume, controlled_swarm |
| shield | planar_field, radial_volume |
| portal | planar_field avec ouverture |
| environmental | vortex_surface, planar_field, radial_volume, ribbon, controlled_swarm |
| multi_projectile | controlled_swarm |
| abstract | Les huit représentations contrôlées |

Un essaim visuel est une entité gameplay collective. Des projectiles gameplay indépendants utilisent des nœuds/copies du moteur, pas des rigidbodies ajoutés aux détails visuels.

Le constructeur reçoit cette matrice dans `COMPILER_COMPATIBILITY_RULES`, généré par `SpellBlueprintV2Validator.BuildCompatibilityContext()` depuis la table réellement utilisée par le compilateur. Une famille secondaire n'étend pas les cores autorisés pour la première. Le contexte indique aussi les capacités et limites géométriques du renderer ; une correction doit choisir ensemble la famille, sa représentation et ses paramètres.

### Géométrie disponible

Les comportements ci-dessous sont ceux de [CanonicalCoreGeometryV2.cs](../game/Assets/Palimpseste/Presentation/V2/CanonicalCoreGeometryV2.cs). Ils sont transmis dans le contexte du constructeur ; ce document ne déclare aucune nouvelle capacité Unity. Une phrase dans `selection_reason`, un invariant ou un repère ne crée pas un contrôle géométrique supplémentaire.

| Core | Construction réellement exécutée | Conséquences et limites |
| --- | --- | --- |
| `swept_tube` | Un tube continu suivant le chemin XYZ Catmull–Rom, avec sections circulaires de rayon `radius_cm` et bouchons plans aux extrémités. | `width_cm` n'aplatit pas le tube et `size_cm` ne redimensionne pas son maillage. Courbure et effilement doivent figurer dans les points et rayons. |
| `beam` | Le même tube circulaire fermé aux extrémités. Quand le carrier fournit un trajet de rayon, le renderer y répartit la coordonnée longitudinale U en conservant les décalages X/Y de section. | Ce nom ne sélectionne ni shader spécial ni branches d'éclair. `width_cm` et `size_cm` ne sculptent pas son maillage. Ne pas encoder une deuxième trajectoire dans le core. |
| `radial_volume` | Un volume fermé suivant le chemin XYZ, avec sections elliptiques de demi-axes `width_cm / 2` et `radius_cm`. Les sections terminales se referment progressivement. | Ce n'est ni une sphère automatique, ni une révolution autour de Y, ni un polyèdre arbitraire. `size_cm` ne redimensionne pas le maillage. Un volume aplati peut être continu ; il ne possède pas de dalles orientables indépendamment. |
| `vortex_surface` | Une paroi continue sur 360°, construite à partir du profil `radius_cm`. `size_cm[1]` contrôle la hauteur uniforme ; X/Z des points déplacent l'axe ; le rapport `size_cm[2] / size_cm[0]` règle l'aplatissement transversal. | Les positions Y du chemin et `width_cm` ne sculptent pas les sections. La modulation spiralée existante et les étranglements ne créent pas des intervalles entre tours ou dalles. Une rotation anime cette paroi sans ouvrir sa topologie. |
| `ribbon` | Une bande ouverte sans épaisseur, rendue sur ses deux faces. Elle suit le chemin XYZ Catmull–Rom, avec largeur `width_cm` et orientation transportée le long du chemin. | Un chemin hélicoïdal peut laisser des intervalles si pas et largeur le permettent. `radius_cm`, `size_cm` et `radial_segments` ne sculptent pas ce maillage. Aucune inclinaison indépendante de section ; aucune histoire de positions enregistrée derrière une tête mobile. |
| `planar_field` | Un disque ou anneau elliptique sans épaisseur, centré sur la racine. La plus petite dimension de `size_cm` détermine sa normale ; les deux autres donnent ses diamètres extérieurs. `inner_radius_milli` donne le rapport intérieur/extérieur. | Les points et leurs profils ne sculptent pas le plan. Il ne devient pas un mur extrudé, un dôme, plusieurs anneaux ou un contour libre. En cas d'égalité des dimensions, Y est prioritaire, puis Z avant X. |
| `controlled_swarm` | `entity_count` copies **intégrales du même tube circulaire** défini par tous les points et rayons. Les centres sont répartis à angles égaux selon une ellipse avec décalage vertical prédéfini. | Le nombre d'entités ne découpe jamais le chemin en segments d'un corps. Les copies partagent forme, profil et orientation initiale ; aucune forme, taille, disposition, phase ou collision individuelle n'est exposée. `width_cm` ne sculpte pas les copies. |
| `branched_surface` | Une seule surface fusionnée issue d'un champ de distance lissé autour des chemins à rayon circulaire. Chaque branche se raccorde au chemin parent à `parent_t_milli`. | Les points de branche sont dans le repère racine, pas relatifs à l'attache. `width_cm` et `size_cm` ne sculptent pas cette surface. Extraction sur une grille fixe de 30 cellules par axe : augmenter les segments déclarés ne la raffine pas. Des détails fins peuvent disparaître et des branches proches fusionner. Aucun squelette ni contrôle de facettes planes n'est exposé. |

Pour les chemins interpolés, la distance entre points ne définit pas une répartition proportionnelle du temps ou des échantillons en longueur. Les profils sont interpolés en douceur ; aucun champ ne définit une rotation indépendante de chaque section. Les modèles de surface n'acceptent aucun mesh, graphe ou asset arbitraire supplémentaire.

Pour l'essaim, avec `a = 2π × indice / entity_count`, le décalage d'une copie en centimètres est `(0,5 × size_cm[0] × cos(a), 0,2 × size_cm[1] × sin(2a), 0,5 × size_cm[2] × sin(a))`. `identity.element_count` reste égal à `entity_count`, et le renderer exige autant de composantes connexes. L'essaim sert à un groupe intentionnel de formes complètes répétées ; les autres cores exigent une seule entité connexe.

Une zone gameplay peut utiliser `ribbon` comme support visuel : sa collision reste celle du carrier contrôlé. `size_cm` définit l'emprise gameplay de `field` / `trap`, même lorsqu'il ne redimensionne pas le maillage ; les dimensions et la géométrie doivent donc rester cohérentes.

`inner_radius_milli` vaut zéro sauf pour `planar_field`. Un portail principal exige un champ plan avec une ouverture positive. Ce paramètre ne sert pas à ouvrir une hélice dans un autre core.

### Mouvement réellement disponible

[CanonicalSpellVisualV2.cs](../game/Assets/Palimpseste/Presentation/V2/CanonicalSpellVisualV2.cs) anime le même maillage de topologie fixe. La trajectoire déplace le carrier ; elle n'ajoute pas une seconde translation visuelle.

- Hors essaim, la vitesse angulaire fait tourner le core autour de Y local. Pour un essaim, elle fait orbiter ses centres ; amplitude et fréquence ajoutent une oscillation verticale par copie. Ce n'est pas une rotation locale indépendante de chaque élément.
- `vortex` ajoute une rotation globale autour de Y avec torsion et oscillation selon la hauteur. Sur un essaim, cette déformation s'applique après le déplacement orbital des centres ; elle ne fournit pas un paramètre de spin local.
- `undulate` déplace les sommets selon X, `flutter` selon Y, `twist` oscille autour de Z local, `pulse` change l'échelle autour du centre des bounds et `expand` agrandit la géométrie existante. Aucun de ces noms ne crée de squelette, d'articulations ou de parties anatomiques.
- L'apparition actuelle fait grandir le core existant de 8 % à sa taille complète et révèle sa surface. Le contact déforme ses sommets ; l'extinction fait disparaître, dissout, contracte ou disperse cette même topologie. Une extinction ne découpe pas automatiquement le volume en nouveaux fragments.

Une technique de bibliothèque étudiée ne devient pas exécutable par son seul nom : le contexte doit préciser les contrôles qui l'appliquent. Une représentation ou animation absente de ces contrôles demande un travail de développement distinct. Les gates de structure, mouvement et qualité visuelle restent obligatoires ; cette description des capacités ne constitue pas leur réussite.

## Conservation et limites

- Coordonnées locales en centimètres, +Y haut, +Z avant. Les points et branches ont des identités et une topologie fixes.
- 2–32 points principaux, 12 branches au maximum, 2–16 points par branche, 16 entités visuelles au maximum.
- 8–128 segments longitudinaux, 6–48 segments radiaux. Le précontrôle partagé vérifie aussi leur produit et le budget sommets.
- Chaque branche se raccorde à l'axe principal ou à une branche antérieure ; aucun cycle.
- La durée d'animation égale exactement la durée gameplay à 50 ticks/s. Trajectoire, sens et vitesse angulaire concordent avec behavior/physics.
- Aucun `appearance.construction`, masque ou contour du dessin. `geometry_id` vaut `canonical.v2` ; la géométrie provient du blueprint.
- Aucun asset externe arbitraire, code, shader source, commande ou chemin dans le contrat. Les noms d'implémentation sont des valeurs fermées.
- La planche finale capturée peut être jointe comme `binary_assets` sous `v2-animation-sheet.png` : elle ne redéfinit pas le core et ne crée pas de dépendance circulaire dans le hash du plan.

`SpellBlueprintV2Safety.Validate` est le précontrôle d'allocation partagé avec Unity. `SpellBlueprintV2Validator` ajoute le schéma strict, les compatibilités sémantiques et la correspondance au gameplay. Ces contrôles ne déclarent jamais une réussite visuelle : celle-ci appartient aux captures et aux critiques indépendantes.

## Fixture distincte

`examples/v2-validation/` contient un météore V2 déterministe, étiqueté `fixture`. Il est produit par `tests/Palimpseste.BlueprintV2.Tests`, sans appel modèle et sans écrire dans la bibliothèque du joueur. Le cache de capture est `.runtime/v2-fixture-cache`.

Le journal `evidence/public/v2/contracts-tests.json` décrit uniquement les contrôles effectivement exécutés. Le programme de test ne prétend pas mesurer la fidélité visuelle, le GPU ou la recette humaine.
