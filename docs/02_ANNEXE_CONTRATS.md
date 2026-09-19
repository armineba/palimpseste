# Annexe normative · Lecture des contrats SP-1.0

Cette annexe complète le cahier principal. Les JSON Schema décrivent la forme des données ; les règles ci-dessous décrivent les contraintes entre champs. Les exemples sont des fixtures manuelles et ne prouvent aucune interprétation ou exécution Unity.

## A · Autorité des fichiers

| Fichier | Producteur prévu | Consommateur |
|---|---|---|
| `drawing-capture.schema.json` | Client, puis vérification serveur | API de capture. |
| `spell-description.schema.json` | Modèle A | Géométrie, modèle B et revue. |
| `spell-plan.schema.json` | Modèle B | Compilateur. |
| `geometry.schema.json` | Résolveur géométrique contrôlé | Compilateur et Unity. |
| `compiled-spell.schema.json` | Compilateur contrôlé | Unity et fiche de règles. |
| `human-review.schema.json` | Interface de revue avec une action humaine | Historique de recette. |
| `capability-catalog.json` | Développeurs | A, B, validateur et runtime. |
| `model-a.response-format.json` / `model-b.response-format.json` | Configuration contrôlée | Format de réponse du fournisseur de référence. |
| `openapi.yaml` | Développeurs | Client, API et tests de service. |

La forme de contrat prime sur une abréviation dans la prose. Une contradiction découverte entre schéma, catalogue et texte bloque une release : elle doit être corrigée dans les trois, pas arbitrée silencieusement par l’agent.

## B · Description et traçabilité

Les identifiants d’observations et de clauses sont uniques. Toutes les références d’observation existent. Chaque sujet mécanique possède exactement un porteur principal dans le plan. Un sujet peut recevoir plusieurs effets, décrits par plusieurs faits `effect`. Un fait ne peut pas être satisfait simplement par l’existence d’un effet ailleurs dans le sort.

`relations` décrit les liens entre sujets : source, événement, cible et maximum d’activations. C’est la version contrôlable de « au premier contact, créer une zone ». Une racine n’a pas de relation entrante. SP-1.0 autorise au plus une relation entrante par sujet ; les fusions multi-parents sont exclues. B reproduit chaque relation dans `activation` sans en créer d’autre. Les sujets sans parent deviennent des racines.

La relation cite la clause qui la justifie. Un fait de `motion` vaut `stationary` pour une zone, une barrière ou un piège, `expanding` pour une onde, et une option explicite pour un projectile. Pour le faisceau, la notion de mouvement n’est pas nécessaire : la clause décrit ses événements et sa géométrie.

Un sujet géométrique doit avoir un `shape_request`. Son porteur principal utilise une géométrie appartenant à la région et au rôle demandés. Sa silhouette supplémentaire peut utiliser le dessin entier. Une clause purement visuelle n’autorise pas une nouvelle application d’effet.

## C · Événements disponibles et application des effets

Un porteur peut émettre plus d’événements qu’il ne possède de récepteurs d’effet immédiats. `spawn` et `expire` ne ciblent pas arbitrairement un personnage voisin. Le tableau suivant est contractuel pour `effects[].event` :

| Porteur | Événements autorisés pour un effet direct |
|---|---|
| Projectile | `hit` |
| Faisceau | `hit` ; les hits sont produits à chaque cadence pour un faisceau entretenu. |
| Champ | `enter`, `tick` |
| Onde | `hit` |
| Barrière | `block`, uniquement si l’événement possède un récepteur matériel admissible. |
| Piège | `trigger` |

Les enfants peuvent être déclenchés par les événements exposés dans `capability-catalog.json`. `spawn` n’est pas admis comme événement de relation dans cette version : placer plusieurs racines ou un délai de racine. Le modèle ne doit pas inventer une cible pour appliquer un dégât à `expire`. Il peut déclencher une onde sur `expire`, qui effectuera sa vraie recherche de cibles.

Un projectile détruit contre un mur émet `expire` mais pas `hit` sur un acteur inexistant. `hit` signifie ici rencontre d’un récepteur ; le filtre de ses effets détermine ensuite ce qui s’applique. Pour un champ enfant devant apparaître également contre un mur, la description doit prévoir la relation d’expiration appropriée dans une version permettant les deux conditions ou accepter que ce cas reste hors de cette recette. Ne pas prétendre que `hit` et `expire` sont interchangeables.

Dans le cas illustratif, le champ naît sur le premier `hit` admissible du projectile. Son `contact_filter` vaut `hostile` : toucher un allié ou un mur n’émet donc pas cet événement. Le projectile peut y être arrêté matériellement et émettre `expire`, mais il n’y crée pas ce champ. Cette exclusion figure dans la fiche de règles.

## D · Acquisition et filtres de porteurs

Pour un projectile, `contact_filter` définit les récepteurs qui émettent `hit` et, en mode guidé, les cibles recherchées. Il vaut aussi pour les projectiles droits et courbes. Un récepteur exclu reste un obstacle si ses couches physiques le rendent solide ; il arrête alors le projectile et émet seulement `expire`. Les dégâts à une barrière restent traités par son adaptateur de collision.

Pour un faisceau, `chain_filter` définit le filtre des récepteurs visés, y compris le premier. Pour un piège, `trigger_filter` définit l’entrée admissible. Pour champ/onde, l’union des filtres d’effets et des besoins des enfants définit les récepteurs candidats. Lorsqu’un champ sans effet déclenche un enfant sur `enter`, ce déclenchement considère `all_actors` par convention de SP-1.0 ; cette règle doit être exposée dans la fiche finale. Une future garde par relation demanderait un nouveau champ de contrat.

Ces règles sont une raison de conserver des faits de ciblage dans A et de les comparer à B. Les filtres des effets ne doivent pas dépasser l’intention du sujet. Un champ peut combiner des effets ciblant différemment à condition que chaque filtre figure dans les faits de ce sujet.

## E · Contraintes par porteur

**Racines :** `parent_id = null`, `event = cast`, `max_activations = 1`; ancrage `caster` ou `aim_point`. **Enfants :** parent existant, événement effectivement émis par lui, ancrage `parent_event`. Le modèle ne produit pas un enfant à l’origine du monde par défaut.

**Copies :** autorisées de 1 à 8. L’éventail répartit les angles uniformément entre `−spread/2` et `+spread/2`; avec une copie, angle zéro et spread zéro. Les porteurs persistants superposés ne gagnent aucun effet implicite : les statuts suivent leur règle de cumul, les dégâts directs explicitement multiples s’appliquent. Une composition inutilement coûteuse peut échouer au budget.

**Projectile :** `turn_mdeg_s = 0` hors guidage ; une courbe exige une géométrie `path`. Vitesse et portée sont positives. Après le dernier point d’un chemin courbe, poursuivre sur sa tangente finale ; ne pas boucler. Le guidage est un mode de trajectoire séparé, qui ne garantit pas de suivre l’intégralité du chemin dessiné. Le premier terme atteint entre portée, durée et arrêt matériel termine l’instance. Le budget de `hit` est borné par `1 + pierces + bounces`, avec déduplication par récepteur.

**Faisceau :** si `lifetime_ticks = 1`, l’évaluation est instantanée. Sinon, `tick_interval ≥ 5`. `chain_radius_cm = 0` lorsque `chain_hops = 0`; rayon positif dans le cas inverse. Premier hit au tick de création, puis toutes les `tick_interval` unités jusqu’à expiration exclusive. La géométrie est un chemin de silhouette ; chaque segment effectif entre cibles conserve ses collisions propres.

**Champ :** première cadence à `spawn + tick_interval`; `enter` une fois par cible pour la vie de l’instance. À l’expiration, les effets sont traités avant retrait seulement lorsque le tick de cadence tombe exactement sur la limite. Maximum 32 récepteurs distincts sur la vie ; les suivants sont ignorés selon l’ordre déterministe, limite affichée.

**Onde :** sa durée est la durée d’expansion. Rayon contractuel du front en fonction du tick : `radius_cm × elapsed / lifetime_ticks`. Le masque est testé dans son repère dimensionné par `scale_cm`, puis intersecté avec la bande du front. Pour une empreinte non centrée, le front part de l’origine locale ; le rayon doit couvrir la portée utile voulue. La valeur `front_width_cm` ne dépasse pas `radius_cm`.

**Barrière :** géométrie `path`, épaisseur et hauteur positives ; structure positive ; expiration au premier terme durée, structure nulle ou maximum de blocages. Le nombre de segments utilisé n’excède pas 64. Le contrôle d’orientation et de placement refuse une création imbriquée dans le lanceur ou un autre volume bloquant ; c’est un refus de cast, pas un changement du parchemin.

**Piège :** `arm_ticks < lifetime_ticks`, `rearm_ticks ≥ 1`; compteur de déclenchements global par instance. Une cible déjà présente à l’armement déclenche si le filtre le permet. Après le maximum, l’instance expire sans second événement parasite.

## F · Contraintes des effets

`damage`, `heal`, `impulse` ont `duration_ticks = 0`. Les statuts ont une durée strictement positive. `burn` utilise un multiple de 50 ticks. `wet.amount = 0`. `slow.amount ≤ 750`. `impulse.direction` est autre que `none`; tous les autres effets ont `direction = none`.

Les plafonds `amount` sont ceux du catalogue : dégâts/soin 100 000 milli-points, impulsion 200 000 milli-N·s, brûlure 10 000 milli-points par seconde, Mouillé zéro, ralentissement 750 millièmes. Ce sont des bornes de laboratoire, pas des valeurs équilibrées pour un RPG.

Une impulsion vers l’extérieur utilise la direction normalisée de la source de l’événement au centre du récepteur. Si les positions coïncident, prendre la direction avant du cast ; jamais diviser par zéro. Pour un effet intérieur, inverser cette direction. `up` est l’axe vertical. Les acteurs sans Rigidbody implémentent l’impulsion dans leur adaptateur, sans modifier arbitrairement leur transform par la couche VFX.

## G · Géométrie et fichiers référencés

Les points de géométrie sont stockés sur deux axes en entiers de −10 000 à 10 000. La dimension maximale de l’asset correspond à `scale_cm`, donc un point normalisé `x` devient `x × scale_cm / 20 000` centimètres, avant rotation et translation. Les coordonnées de chemin de test ne sont pas des coordonnées de pointeur et ne sont pas envoyées à A à la place du dessin.

`mask_file` dans le contrat de géométrie désigne uniquement un nom de fichier relatif produit par le résolveur dans un paquet d’asset contrôlé. Il ne vient pas du modèle B. Interdire séparateurs, chemin absolu et `..`; la liste des fichiers du paquet est manifeste et hashée. À l’API, les fichiers sont résolus par identifiants d’artefacts et soumis à l’autorisation, pas servis via le nom brut.

Le paquet compilé contient `geometry_manifest` pour les JSON de géométrie et `binary_assets` pour leurs PNG. Chaque `mask_file` correspond exactement à un `binary_assets.file_name` unique ; son `artifact_id` permet le téléchargement autorisé et son SHA-256 vérifie les octets. Le masque n’est pas implicitement fiable parce que le JSON qui le référence est hashé.

`provenance` conserve le mode, les empreintes de capture/référence et les versions de modèles et prompts. Dans les fixtures manuelles, les modèles et identifiants de réponse restent `null`, avec `mode=fixture` ; ils ne doivent pas imiter des identifiants de vraies requêtes. En production, les données viennent des réponses archivées et non d’un texte inventé par B.

Une empreinte exige un masque existant et borné. Un chemin exige au moins deux points distincts et une longueur non nulle. Une silhouette peut référencer un masque sans collision. La banque doit compter également les fichiers de masque dans le budget mémoire, pas seulement le JSON.

## H · Bornes et extinction complète

La borne temporelle du cast inclut le dernier statut persistant appliqué, même s’il survit au champ qui l’a créé. Pour le cas de référence, une borne conservatrice est 125 ticks de projectile + 1 tick de passage à l’enfant + 200 ticks de champ + 150 ticks de brûlure, soit **476 ticks**. Une borne à 326 oublierait le statut résiduel.

Le calcul du nombre d’effets compte chaque application de statut ainsi que son nombre maximal de ticks. Il peut surestimer, puisque les statuts se rafraîchissent au lieu de s’empiler, mais jamais sous-estimer. Une fois un statut transféré au `StatusSystem`, celui-ci conserve source et identité du cast sans maintenir un GameObject de projectile disparu.

Le script QA livré vérifie des formes et quelques invariants. Il ne remplace pas ce calcul pessimiste complet ni les tests du runtime à développer.
