# Relecture indépendante du skill — preuve de développement

Proposition sur documents par un agent de développement distinct. Aucun appel du worker joueur, rendu Unity ou verdict artistique. Le premier passage ne disposait que du skill ; la révision a aussi reçu le contrat V2 et les règles numériques réellement fournis à B.

# Bouclier organique — proposition du worker de génération V2

Décision : proposer un unique bouclier `planar_field`, translucide et à bordure Fresnel via `forcefield`, avec une respiration géométrique `motion.deformation=pulse` et une réponse de contact contrôlée par `canonical_impact_v2`. Le schéma et les règles numériques autorisent ces choix. Leur présence dans le contrat ne prouve pas que chaque paramètre est consommé par le renderer ni qu'un mur déclenche effectivement cette réaction.

Le catalogue de méthodes n'est pas le contrat complet du moteur : l'absence d'une fiche de pulsation ou de collision n'établit pas leur indisponibilité. En revanche, sa limite explicite reste applicable au shader : le profil V2 `forcefield` ne propose pas de lueur d'intersection calculée par profondeur de scène.

## Plan technique et portée des paramètres

Un nœud `shield`, un élément, topologie fixe et coordonnées `local_cm_y_up_z_forward`. Cible : membrane frontale d'environ 160 × 200 × 12 cm, vert d'eau, intérieur lisible par transparence et bordure brillante. Les paramètres géométriques doivent être jugés dans le renderer : le mot `planar_field` ne prouve à lui seul ni une silhouette ovale ni une convexité.

Les hypothèses du carrier sont explicites : barrière stationnaire, durée 6 s et vitesse angulaire nulle. Cela impose dans le plan global `behavior.travel=stationary`, `options.lifetime_ticks=300` et `physics.angular_speed_mdeg_s=0`. Aucun carrier antérieur n'a été fourni ; si ses invariants diffèrent, ces valeurs devront être recalculées, sans seconde translation visuelle.

| Besoin | Paramètres proposés sous `/blueprint_v2/` | Conclusion et limite |
| --- | --- | --- |
| Membrane organique translucide | `rendering_layers/core_surface=forcefield`, `core_opacity_milli=300`, `core_emission_milli=1800` | Profil documenté Fresnel/bruit. L'opacité globale n'est pas la garantie d'un alpha pixel de 0,3 ; vérifier intérieur et contre-jour. |
| Pulsation | `motion/deformation=pulse`, `amplitude_cm=3`, `frequency_mhz=625` | Déformation du core admise, fréquence de 0,625 Hz selon les unités et la règle de cycles. Vérifier la forme exacte, la consommation de l'amplitude et la préservation de la silhouette. |
| Bordure brillante | Même profil `forcefield` et émission ci-dessus | Fresnel documenté ; le schéma ne donne pas de commande séparée de largeur/couleur du rim. La luminosité perçue reste à observer. |
| Contact gameplay | `physics/collider=plane`, `collision_response=block`, `visual_response=impact`, `motion/impact_transition=stop` | Correspondance prescrite pour un carrier barrier. Les collisions appartiennent à la racine. Vérifier qu'une collision avec un mur atteint le contrôleur d'impact. |
| Réaction visuelle au contact | `impact/contact_pose=preserve`, `hit_flash_milli=450`, `deformation=ripple`, `reaction_duration_ms=240`, `destruction_mode=persist` | Contrat de flash et déformation admis ; vérifier leur implémentation, leur éventuelle localisation et le retour au maintien. Aucun anneau au point de contact ou onde se propageant n'est garanti par le seul enum `ripple`. |
| Extinction | `disappearance/mode=dissolve`, `direction=longitudinal`, `residual_duration_ms=0` | Port conceptuel limité à la dissolution longitudinale et à l'enveloppe d'opacité existantes. Aucun bord de dissolution émissif arbitraire. |

L'aspect organique combine le bruit de surface TinyPlay et une respiration lente du core. Ces deux mécanismes ont des rôles distincts. Le plan ne revendique ni élasticité physique, ni réfraction, ni simulation fluide. Une pulsation lumineuse indépendante est également possible à étudier via l'enum contractuel `secondary_kind=pulse`, mais elle n'est pas sélectionnée ici : son rythme et son articulation avec la déformation ne sont pas établis dans les documents lus.

## Phases et cohérence numérique

- Apparition : 0–600 ms, `appearance_end_milli=100`.
- Phase active : 600–5 400 ms, `active_end_milli=900`, `active_loop=true`.
- Extinction : 5 400–6 000 ms, dissolution longitudinale.
- Contact : réaction événementielle proposée de 240 ms, durée de survie déclarée 400 ms, `lifetime_behavior=until_duration` et `destruction_mode=persist` pour demander un maintien après l'impact. L'interprétation conjointe de ces champs demande vérification dans le contrôleur.

La phase active dure 4,8 s. `625 × 6000 × (900−100) = 3 000 000 000`, divisible par `1 000 000 000` : exactement trois cycles selon la contrainte du compilateur. La période demandée est 1,6 s. La vitesse angulaire nulle satisfait aussi la divisibilité imposée. `400 >= 240` respecte survie/réaction ; `300 × 20 = 6000` respecte la durée du carrier.

Ces relations assurent la cohérence des nombres proposés. Elles ne prouvent pas une boucle visuellement sans rupture : la phase du bruit de surface et la composition des enveloppes restent à observer. Une planche doit échantillonner ce mouvement continu.

## Extrait de blueprint proposé

Le schéma du blueprint est fourni, mais l'enveloppe globale du job et son carrier effectif ne le sont pas. Cet extrait contient les groupes utiles au choix technique ; il omet intentionnellement les groupes d'intention, les points de contrôle, les invariants détaillés et les budgets complets. Ce n'est pas un blueprint complet à soumettre tel quel.

```json
{
  "nodes": [{
    "id": "shield",
    "blueprint_v2": {
      "schema_version": "sp.blueprint/2.0",
      "archetype": ["shield"],
      "structural_core": {
        "kind": "planar_field",
        "size_cm": [160, 200, 12],
        "entity_count": 1,
        "inner_radius_milli": 0,
        "color_rgb": [92, 218, 184]
      },
      "motion": {
        "duration_ms": 6000,
        "trajectory": "stationary",
        "path_cm": [[0, 0, 0], [0, 0, 0]],
        "deformation": "pulse",
        "amplitude_cm": 3,
        "frequency_mhz": 625,
        "angular_speed_mdeg_s": 0,
        "acceleration_cm_s2": 0,
        "anticipation_milli": 0,
        "follow_through_milli": 0,
        "secondary_motion_milli": 0,
        "impact_transition": "stop",
        "dissipation": "dissolve"
      },
      "phases": {
        "appearance_end_milli": 100,
        "active_end_milli": 900,
        "active_loop": true
      },
      "rendering_layers": {
        "core_surface": "forcefield",
        "core_opacity_milli": 300,
        "core_emission_milli": 1800,
        "secondary_kind": "none",
        "secondary_intensity_milli": 0,
        "atmosphere_kind": "none",
        "atmosphere_particles": 0,
        "resource_id": "kpp_circle_01"
      },
      "physics": {
        "collider": "plane",
        "collision_owner": "root",
        "collision_response": "block",
        "visual_response": "impact",
        "affected_components": ["core"],
        "lifetime_behavior": "until_duration",
        "rigidbody_count": 0,
        "decorative_physics": false,
        "independent_entities": false
      },
      "impact": {
        "contact_pose": "preserve",
        "hit_flash_milli": 450,
        "deformation": "ripple",
        "reaction_duration_ms": 240,
        "secondary_emission": 0,
        "dissipation_direction": "normal",
        "surviving_core_duration_ms": 400,
        "destruction_mode": "persist"
      },
      "disappearance": {
        "mode": "dissolve",
        "direction": "longitudinal",
        "residual_duration_ms": 0
      },
      "unity_implementation": {
        "root": "Spell_ROOT",
        "core_renderer": "canonical_mesh",
        "deformation_system": "canonical_motion_v2",
        "motion_controller": "spell_runtime",
        "collider_strategy": "root_only",
        "vfx_graph_systems": 0,
        "particle_systems": 0,
        "shader": "Palimpseste/CanonicalSpellV2",
        "material_count": 1,
        "trails": "none",
        "audio_hook": "none",
        "impact_system": "canonical_impact_v2",
        "lifetime_controller": "spell_runtime"
      }
    }
  }]
}
```

`resource_id=kpp_circle_01` est une valeur admise et devra être identique dans `appearance`. Avec zéro atmosphère, zéro émission de contact et zéro énergie secondaire, les règles donnent zéro système de particules et un matériau. Ce resource_id ne prouve ni l'emploi de cette texture sur le core ni un import TinyPlay. Pour le plan complet, prévoir deux points de contrôle consécutifs distincts, des rayons positifs et les budgets du job ; les deux points identiques de `motion.path_cm` décrivent ici une trajectoire stationnaire et ne sont pas des points de contrôle de géométrie.

## Rationale et bindings de `method_design`

L'unique nœud `shield` utilise trois méthodes `runtime_selectable=true`, toutes avec une application autorisée `adapt`. Les nombres et textes des bindings sont exactement les scalaires de l'extrait. Tous les `runtime_requirements` sont couverts ; les contrôles moteur de pulsation/contact sont déclarés séparément de l'attribution aux bibliothèques.

| method_id | application et adaptation | bindings relatifs au nœud |
| --- | --- | --- |
| `tinyplay.forcefield_rim_contact` | `adapt` : port partiel Fresnel/bruit/translucidité pour la membrane et sa bordure ; sa branche de profondeur amont reste absente. | `/blueprint_v2/rendering_layers/core_surface` = `forcefield` ; `/blueprint_v2/rendering_layers/core_opacity_milli` = `300` ; `/blueprint_v2/rendering_layers/core_emission_milli` = `1800`. |
| `xtaja_independent_clocks` | `adapt` : séparer âge continu du matériau et enveloppes de vie normalisée dans l'implémentation V2 indépendante. La pulsation géométrique appartient au moteur, pas à du code xtaja importé. | `/blueprint_v2/motion/duration_ms` = `6000` ; `/blueprint_v2/phases/appearance_end_milli` = `100` ; `/blueprint_v2/phases/active_end_milli` = `900` ; `/blueprint_v2/phases/active_loop` = `true`. |
| `xtaja_lifetime_dissolve` | `adapt` : disparition progressive par dissolution longitudinale et opacité déjà implémentées. | `/blueprint_v2/disappearance/mode` = `dissolve` ; `/blueprint_v2/disappearance/direction` = `longitudinal` ; `/blueprint_v2/disappearance/residual_duration_ms` = `0` ; `/blueprint_v2/motion/dissipation` = `dissolve`. |

`keijiro.divergence_free_domain` n'est pas ajouté : son binding exige `core_surface=spectral` et `secondary_motion_milli>=1`, incompatibles avec les valeurs retenues pour ce nœud. Son port module des filaments ; il ne fournit ni une force physique ni une déformation de membrane. Créer une couche ou un nœud supplémentaire pour citer cette bibliothèque n'est pas nécessaire à la proposition.

## `source_review` : exactement cinq sources

| source_id | statut | raison |
| --- | --- | --- |
| `tinyplay_urp_shaders_collection` | `used` | Port partiel `forcefield` lié au blueprint. Révision `6e663fffccd00a4cce837644a29f6e8f82a6e372`, MIT ; notices de l'intégration conservées. Aucun nouveau graphe importé. |
| `xtaja_vfx_shader` | `used` | Deux concepts appliqués via les opérations V2 indépendantes. Révision `aa4d626cbea273caed0507a7c3dc4b5795d0fcb6`. Aucune licence explicite identifiée : aucune copie de code ou d'asset. |
| `hovl_magic_effects_free` | `unavailable` | Package non acquis, aucun intérieur de prefab inspecté ; aucune méthode construite à partir de cette offre commerciale. |
| `unity_visual_effect_graph_samples` | `not_applicable` | Révision `bcd800405c1b019654a0af3b4b8bd68fec590b4b`. Le choix sélectionnable Bonfire impose plasma et fumée, sans utilité pour ce bouclier ; aucun graphe original importé. |
| `keijiro_vfx_graph_assets` | `reference_only` | Révision `5013c195305288fab61cd71f72a4628dbe9d0ea4`, Unlicense avec dépendance de bruit sous notice MIT selon la fiche. Méthode organique examinée mais non sélectionnée. |

Les fiches `tinyplay.depth_fade`, `tinyplay.bubble_triplanar_membrane` et `xtaja_depth_view_alpha` ont `runtime_selectable=false` et ne figurent pas dans les méthodes exécutables annoncées. L'existence historique du contact dans D19 ne prouve aucune capacité de shader V2.

## Ce qui reste à vérifier

Le contrat et les règles permettent de proposer sans nouveau code une pulsation du core et un impact racine avec flash/déformation. Ils ne permettent pas de certifier le comportement de tous les enums ni leur combinaison avec `planar_field` et `forcefield`. La vérification de développement devra examiner la consommation des paramètres par `canonical_motion_v2` et `canonical_impact_v2`, le déclenchement des contacts avec les murs, leur position/normale, la portée du flash et de `ripple`, puis la persistance et le retour au maintien.

Le shader ne dispose explicitement pas du contact par profondeur décrit dans la bibliothèque TinyPlay. Une ligne lumineuse épousant continuellement l'intersection du mur nécessiterait une capacité nouvelle de développement, distincte de la réaction d'impact déjà admise par le contrat. Ce dossier ne transforme pas cette limite ciblée en absence générale de contact.

Le rendu réel devra ensuite être jugé en mouvement et en `CORE_ONLY` : transparence, bordure, silhouette, pulsation, contact et extinction. Les huit gates obligatoires demeurent `A_structure`, `B_continuity`, `C_rendering`, `D_impact`, `E_game_camera`, `F_motion`, `semantic_blind` et `performance`. Aucun résultat ni mesure de gate n'est déclaré.

## Traçabilité

Provenances rapportées par les fiches, sans accès aux sources amont dans ce job :

- TinyPlay `Shaders/VFX/ForceFieldShader.shadergraph`, SHA256 `04cb9ff124063e162c8842363d29c18a46ded5042f37d365938b18e50705380a`.
- xtaja `Assets/HLSL/VFXShader.shader`, SHA256 `1cdf5c7f5cfe5fc8599ca48d2e3102106f35e987eb822d7744864f16a9bbada5`.

Fichiers lus : `skills/unity-god/SKILL.md`, `skills/unity-god/references/runtime.md`, `skills/unity-god/references/methods.json` (cinq sources, inventaire runtime et fiches utiles), `contracts/spell-blueprint-v2.schema.json`, `prompts/09_V2_NUMERIC_RULES.md`. Les deux instructions mises à jour ont été relues ; le hash du catalogue est identique à celui de la première lecture. Aucun autre rapport consulté.

`skill_version=1.0`. Empreintes locales observées :

| Fichier | SHA256 |
| --- | --- |
| `SKILL.md` | `518ff842b48af3786a073aca0365e9a52b1a671044d48ada77b8643cd7b02463` |
| `runtime.md` | `ef71e11cc024f94b6d968bb8fb8f7b220fe4eda4948c36c79881f7fece4c9670` |
| `methods.json` | `359f79e7bc91ebb341acb66c889cbfacdb474b8a052ffb64fad5f6e778f6ae3a` |
| `spell-blueprint-v2.schema.json` | `3aa2b4d0795489c539acd27c83b442ea6722b9dc50fe65b3915fb2ba7cedf49b` |
| `09_V2_NUMERIC_RULES.md` | `4662926e42684ad2651d9699b9f6b0b009976b6d552bfe292d122fe829aaf006` |

L'enveloppe globale de job n'est pas fournie : ses `skill_sha256` et `method_catalog_sha256` doivent être recopiés de cette enveloppe lors d'une soumission réelle. Les empreintes de fichiers ci-dessus sont des preuves locales, sans attribution inventée au job.

Travail effectué : dossier technique et examen documentaire des correspondances/contraintes. Aucun provider, téléchargement, import, code projet, build, lancement Unity ou validation de sort.
