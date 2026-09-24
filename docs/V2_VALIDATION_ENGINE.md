# Moteur de validation V2

Ce moteur appartient au Player précompilé 1.8.0. Il reçoit un paquet compilé et
ses fichiers contrôlés dans un dossier de capture isolé. Il ne lit pas la
bibliothèque du joueur, n'appelle aucun fournisseur et n'exécute aucun code
proposé par le modèle.

## Deux invocations, séparées par la porte structurelle

```text
Palimpseste.exe -batchmode -force-d3d11
  --palimpseste-v2-validation DOSSIER_ABSOLU --v2-mode core

Palimpseste.exe -batchmode -force-d3d11
  --palimpseste-v2-validation AUTRE_DOSSIER_ABSOLU --v2-mode full
```

La seconde invocation est autorisée par le worker seulement après la lecture
aveugle et la validation structurelle. Chaque dossier contient `spell.json`,
`spell.json.sha256`, `description.json`, `blueprints.json` et `artifacts/`.
`blueprints.json` est le tableau exact des blueprints de chaque nœud du paquet.
Le renderer vérifie l'égalité de ces données avant d'allouer une géométrie.

Les chemins avec jonction ou lien symbolique sont refusés. Le worker vérifie
également les ACL, le manifeste SHA-256 et la version du Player installé. Le
verrou GPU est partagé avec le renderer V1, sans modifier le parcours V1.

## Preuves produites

- `core_gallery.png` : toutes les structures principales, y compris les nœuds
  enfants, dans une grille de 4 × 4 maximum. Chaque structure est montrée seule
  au milieu de sa phase active, sans inventer l'instant d'un événement parent.
  Les identifiants opaques `N1…N16` évitent de révéler le sujet au lecteur
  aveugle ; `core_gallery_nodes` conserve leur correspondance avec les nœuds.
  Chaque nœud reçoit aussi 121 mesures locales de continuité.
- `core_sheet.png` : 3 × 7 échantillons canoniques, couleur unie et opaque,
  aucune couche secondaire ou atmosphérique, aucun post-traitement.
- `normal_sheet.png` : les mêmes instants normalisés, avec les couches prévues
  par le blueprint. Les 21 poses sont calculées avec la même géométrie et la
  même fonction de mouvement ; aucune image indépendante n'invente une pose.
- `motion_000.png`, etc. : véritable lancer dans `SpellLab`, ticks gameplay à
  50 Hz, trajectoires, contacts et enfants déclenchés par les vrais événements.
  Les images sont horodatées ; les délais de rendu ou d'encodage ne sont pas
  soustraits à l'horloge du mouvement.
- `motion_sheet.png` : résumé des images réellement capturées jusqu'au dernier
  acteur visible et à la première image vide suivante. Les images brutes
  couvrent toute la durée déclarée et la survie terminale. Les instants exacts
  des cases figurent dans `motion_sheet_samples`, sans prétendre à un FPS fixe.
- `impact_ground.png`, `impact_wall.png`, `impact_target.png`,
  `impact_oblique.png` et leur composition `impact_sheet.png` : quatre scènes
  isolées avec de vrais colliders Unity et le moteur de gameplay du Player.
- `game_camera.png` : lancer via `SpellLab` et sa véritable caméra de jeu,
  avec ses réglages par défaut et son espace réservé à l'interface.
- `validation.json` : hashes des entrées et images, instants, topologie,
  contacts, budget observé, limites de mesure et état de chaque porte.

Les planches canoniques décrivent les racines avec une horloge commune : délai
d'activation et durée propres à chaque nœud. Elles n'inventent pas un instant
pour les enfants de contact ou de déclenchement. Ceux-ci apparaissent au moment
réel décidé par `RuntimeEngine`, dans les preuves F et D. Cette distinction
figure explicitement dans le manifeste.

L'ordre des premières images retournées au critique est fixé. La lecture
aveugle commence par la galerie complète ; la comparaison reçoit ensuite la
planche des racines et ses phases. Elle ne commence jamais par la case
volontairement vide d'apparition.

## Portes et portée exacte

| Porte | Mesure du Player | Décision complémentaire |
| --- | --- | --- |
| A structure | Capture `CORE_ONLY`, géométrie effectivement rendue | Critique aveugle puis comparaison à l'intention |
| B continuité | 121 instants, vertices finis, topologie stable, composantes continues, déplacement borné | Lecture du mouvement et des détails reconnaissables |
| C rendu | Planche complète issue du même modèle canonique | Les couches enrichissent la structure sans la cacher |
| D impact | Contacts réels et invariance de la topologie ; colliders exclusivement gameplay | Lisibilité du contact et de l'extinction |
| E caméra | Caméra réelle du laboratoire et lancer réel | Lisibilité à la distance de jeu |
| F mouvement | Vrai lancer gameplay, durée complète, contacts/enfants et images horodatées | Fluidité, caractère et cohérence du mouvement |
| Sémantique | La première lecture ne reçoit ni titre, ni dessin, ni description | Comparaison explicite du sujet observé à l'intention |
| Performance | Vertices, matériaux, particules, coût CPU de déformation, soumissions estimées | Les mesures GPU indisponibles restent non mesurées |

`completed: true` signifie que l'acquisition a terminé. Cela ne signifie pas
que le rendu est accepté. `technical_pass` concerne exclusivement les mesures
énoncées ; les portes visuelles restent `requires_visual_review` dans le
manifeste de capture. Le worker combine ces preuves avec la critique séparée
avant publication.

### Adaptation des scénarios d'impact au porteur

Un projectile ou rayon doit rencontrer une vraie surface ou cible. Une zone,
une impulsion ou un piège doit provoquer une interaction réelle avec un
receiver (`enter`, `tick`, `hit` ou `trigger`) dans les quatre contextes.
L'apparition d'un objet seule n'est pas une réussite.

Pour une barrière, le banc d'essai ajoute un projectile inoffensif et fixe à
une **copie privée du scénario**. Le blueprint et les nœuds du sort restent
inchangés. Ce projectile traverse le même `RuntimeEngine` et doit provoquer
son véritable événement `block`. Le manifeste distingue explicitement cet
appareillage du sort. Il n'est ni sauvegardé dans le paquet livré ni ajouté à
la bibliothèque.

Les ticks de physique sont espacés de 20 ms réelles. Cela empêche une simulation
accélérée d'atteindre un impact alors que l'animation d'apparition n'a avancé
que d'une seule frame.

## Mesures non disponibles

Les temps de rendu mesurés incluent une requête URP et une lecture complète
du GPU, à 512 × 512. Ils excluent l'encodage PNG. Ce nombre n'est ni le FPS du
jeu complet ni une mesure isolée du temps GPU.

Le surdessin transparent, le temps GPU propre au sort et les draw calls SRP
exacts exigent une instrumentation GPU supplémentaire. Le manifeste contient
`null` et une raison pour ces mesures. Le nombre de soumissions fourni par le
renderer est clairement présenté comme une estimation, pas comme une mesure
du batching GPU.

## Sort de contrôle séparé

`examples/v2-validation/` contient un météore de contrôle déterministe. La
compilation des tests le prépare dans `.runtime/v2-fixture-cache/`. Ce sort
sert exclusivement aux essais V2 et ne remplace aucun ancien sort. Les
preuves d'exécution réelles sont conservées sous `evidence/public/v2/` ; la
présence de la fixture ne constitue pas en elle-même une preuve de capture
ou d'acceptation artistique.
