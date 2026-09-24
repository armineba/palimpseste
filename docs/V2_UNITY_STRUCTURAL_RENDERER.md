# Rendu Unity de la pipeline V2

## Isolation et structure

La branche `SpellNode.blueprint_v2` utilise `CanonicalSpellVisualV2`. Les autres
nodes passent toujours par les branches historiques de `CarrierVisual`.
Aucun shader, matériau, prefab, scène ou animation historique n'est remplacé.
Le nouveau shader réside dans `Presentation/V2/Resources/CanonicalSpellCoreV2.shader`
et s'appelle `Palimpseste/CanonicalSpellV2`. Son emplacement Resources permet son
inclusion dans le Player sans modifier les matériaux existants.

Une seule géométrie canonique est construite avant l'animation. Les indices des
triangles restent identiques pendant chaque échantillonnage et le jeu réel.
Les coutures UV sont soudées logiquement pour vérifier les composantes connexes ;
une structure discontinue contraire au nombre d'entités déclaré est refusée.

| StructuralCore | Réalisation précompilée |
| --- | --- |
| swept_tube | Surface continue autour d'une spline Catmull-Rom avec section variable et repère transporté |
| beam | Même continuité, déformée sur le trajet réellement résolu par les collisions du moteur |
| ribbon | Ruban continu avec largeur variable, sans segments GameObject |
| radial_volume | Masse fermée suivant le véritable chemin canonique ; profils de demi-hauteur et largeur, fermeture douce aux extrémités |
| vortex_surface | Colonne continue profilée, spirale et rotation explicite en degrés/seconde |
| planar_field | Surface radiale ou anneau à ouverture déclarée, pour champs, boucliers et portails |
| controlled_swarm | Nombre fixé de sous-entités dans un mesh commun ; identités stables et orbites continues |
| branched_surface | Champ de distance fusionné extrait par tétraèdres ; branches reliées aux points déclarés, un seul mesh soudé |

La résolution du champ de distance est bornée à 30 cellules par axe. Une annexe
trop fine peut donc ne pas être représentée : le contrôle de silhouette et le
jugement sémantique doivent le refuser, jamais le déclarer accepté automatiquement.
Ce renderer ne prétend pas représenter toute anatomie possible avec cette précision.

## Temps, couches et physique

`SampleNormalized(t)` appelle la même fonction d'animation que `Update`. Aucun
tirage aléatoire ou nouveau mesh n'est produit entre deux frames. Le shader reçoit
le temps canonique explicite, pas `_Time`. Les fonctions couvrent ondulation,
torsion, pulsation, expansion, vortex, flottement, anticipation, retard progressif
et mouvement secondaire. Le déplacement gameplay utilise uniquement RuntimeEngine.

Si `phases.active_loop` est vrai, l'intervalle entre `appearance_end_milli` et
`active_end_milli` constitue une période complète. La validation partagée exige
un nombre entier de tours et de cycles d'oscillation aux vitesses déclarées.
Elle refuse une expansion monotone présentée comme boucle. Le moteur ne modifie
pas secrètement les fréquences pour obtenir une boucle : un blueprint incohérent
retourne à MotionSpec. Les couches shader emploient des UV périodiques et un domaine
de bruit parcouru en cercle ; les particules secondaires reviennent à leur état
initial en fin de période. La trajectoire gameplay de la racine n'est jamais
rembobinée. La gate B compare numériquement les mêmes vertices locaux aux deux
extrémités de l'intervalle demandé.

`SetCoreOnly(true)` rend une silhouette opaque de couleur uniforme en phase active et désactive
SecondaryVFX, AtmosphericVFX et Impact. Le rendu normal ajoute une surface magique
non éclairée avec Fresnel, filaments, coordonnées longitudinales/radiales et textures.
Les particules restent secondaires et leur nombre est fixé dans le blueprint.
L'apparition et l'extinction CORE_ONLY montrent l'enveloppe alpha uniforme du
cycle de vie, sans bruit ni décoration. Un fade déclaré n'est pas remplacé par
une contraction arbitraire. La correction suit le premier contrôle indépendant :
le volume radial imposait initialement un axe Y et ignorait les positions du
chemin canonique ; cette divergence est supprimée dans la construction générale.

La hiérarchie créée est `Spell_ROOT/{Gameplay,Core,SecondaryVFX,AtmosphericVFX,Impact}`.
Les enfants graphiques n'ont ni Collider ni Rigidbody. Les projectiles et rayons
utilisent les balayages de collision du moteur. Les champs et pièges possèdent une
empreinte logique de boîte déclarée ; les barrières ont un BoxCollider de gameplay
unique. Les contacts réels transmettent normale et position à la présentation.
La pose, compression/ondulation/expansion, flash, émission, maintien et extinction
sont bornés par ImpactSpec. Aucune force ne sépare les pièces visuelles.

## Bibliothèques consultées et ressources réellement utilisées

Les cinq sources imposées ont été explorées à travers les clones épinglés,
README, exemples et fiches contrôlées de `assets/sourced-vfx/references.json`.

1. **TinyPlay URPShadersCollection**, commit
   `6e663fffccd00a4cce837644a29f6e8f82a6e372`, MIT : consultation de
   `Shaders/VFX/PlasmaShader.shadergraph` et des exemples ForceField ; reprise
   du principe de deux textures RGB défilant en sens opposés. Réutilisation réelle
   des textures déjà embarquées `SourcedVfx/tinyplay_plasma.jpg` et
   `SourcedVfx/tinyplay_noise.png`. Les originaux et notices restent inchangés.
2. **xtaja/VFX-Shader**, commit inventorié
   `aa4d626cbea273caed0507a7c3dc4b5795d0fcb6` : README et sections durée de vie,
   déplacement UV et dissolution consultés. Aucune licence explicite : aucun code
   ou asset copié. Le temps explicite V2 est notre propre implémentation.
3. **Magic Effects FREE**, fiche Asset Store 247933 : référence de composition et
   distinction apparition/activité/extinction consultée via fiche contrôlée.
   Package non acquis et non importé. Aucun élément propriétaire redistribué.
4. **Unity VisualEffectGraph-Samples** : README Ribbon Pack, Portal et structures
   `Assets/Samples/SmokePortal/VFXGraphs` consultés. Réemploi des principes de
   continuité des strips et de séparation des couches ; aucun graphe HDRP ni asset
   tiers importé. Le projet reste URP. Les pointeurs LFS ne sont pas des assets utilisables.
5. **Keijiro VfxGraphAssets**, commit
   `5013c195305288fab61cd71f72a4628dbe9d0ea4`, Unlicense : opérateur
   `Subgraph/Divergence Free Noise 3D.vfxoperator` inspecté. Le nouveau shader inclut
   le port déjà livré `SourcedSurfaces/KeijiroDivergenceFreeNoise.hlsl` ; le bruit
   enrichit la surface sans déplacer sa structure. Dépendance NoiseShader MIT
   existante, notices conservées.

Les sprites Kenney CC0 déjà livrés sont réellement lus par le shader des particules
suivant `rendering_layers.resource_id`. Aucun téléchargement, import ni exécution
de code tiers n'est accessible au worker de génération joueur.

## Mesures et limites

`ReadMetrics` expose vertices, triangles, particules, matériaux, systèmes VFX,
composants physiques décoratifs et temps CPU de la mise à jour mesuré par Stopwatch.
Le nombre de draw calls est **une estimation des soumissions de renderers** ;
l'overdraw réel et le temps GPU par sort sont explicitement indisponibles.
Ils ne sont pas enregistrés comme zéro ou acceptés comme mesurés.

Cette note décrit l'implémentation. Les résultats effectifs de compilation,
captures, gates et acceptation visuelle sont consignés séparément par la livraison
V2 ; la présence des fichiers ne constitue pas une preuve de test réussi.
