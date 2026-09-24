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
| area | planar_field, radial_volume, vortex_surface |
| explosion | radial_volume, controlled_swarm |
| summon | branched_surface, swept_tube, radial_volume, controlled_swarm |
| shield | planar_field, radial_volume |
| portal | planar_field avec ouverture |
| environmental | vortex_surface, planar_field, radial_volume, ribbon, controlled_swarm |
| multi_projectile | controlled_swarm |
| abstract | Les huit représentations contrôlées |

Un essaim visuel est une entité gameplay collective. Des projectiles gameplay indépendants utilisent des nœuds/copies du moteur, pas des rigidbodies ajoutés aux détails visuels.

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
