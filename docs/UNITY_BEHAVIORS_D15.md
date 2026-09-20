# Unity : comportements D15

Les nouveaux paquets `min_client=1.5.0` portent `node.behavior` (intention figée issue de la description), `node.physics` (paramètres bornés), `appearance.resource_id` et le hash de recherche de références. Les archives sans ces champs gardent leur branche de rendu et de placement.

## Placement et trajectoire

`RuntimeEngine` résout explicitement caster, muzzle, cible, sol ou événement parent. Une cible est bornée par `cast_range_cm`. La projection sol ignore les acteurs et les barrières, exige une surface praticable et ne se rabat pas silencieusement sur le lanceur. L'attachement autorisé au caster déplace ensemble l'état mécanique, les volumes et leur rendu. L'orientation du modèle est séparée de la rotation du trajet : un vortex peut rester vertical en se déplaçant vers la visée.

La trajectoire balistique utilise la vitesse initiale et l'angle déclarés, la gravité, un pas fixe de 20 ms et le balayage sphérique existant. Un rebond réfléchit la vitesse. Aucun effet ni collider décoratif n'est ajouté. Les anciens trajets conservent leur calcul.

## Animation et ressources

`SpellBehaviorMotion` applique spin, vortex, orbite, flux, flottement et turbulence à partir des champs validés. Le spin des pièces est une rotation continue en degrés/seconde. Le vortex possède un axe commun, un déphasage selon la hauteur, une advection de surface et des particules axiales/orbitales. Le déplacement décoratif reste borné ; il ne remplace pas les collisions du moteur.

Les 16 textures originales Kenney CC0 sont dans `Resources/SourcedVfx`, chargées uniquement par leurs identifiants autorisés. Elles servent réellement aux masques des particules et aux détails de surface. Les PNG indexés et les dimensions non carrées sont importés comme textures Unity, sans utiliser le validateur des images générées.

Le shader de construction réutilise `SimplexNoise3D.hlsl` et `Common.hlsl` de NoiseShader (Ashima/stegu, adaptation Unity Keijiro, MIT). L'unique modification du code amont est l'include relatif de Common. Source et licence sont conservées dans `Resources/SourcedNoise`; la distribution inclut une notice MIT lisible.

L'unité des vitesses orbitales ParticleSystem est traitée comme radians/seconde par l'adaptateur ; la vitesse visuelle effective de ce module reste à accepter lors de l'essai utilisateur. La rotation des meshes est explicitement calculée en degrés/seconde. Aucun essai physique ou visuel de D15 n'est prétendu ici.

## Capture temporelle Pro

Le Player conserve les requêtes URP explicites et les lectures GPU synchronisées. `active_0.png` à `active_3.png` sont des rendus distincts demandés à 0, 0,073, 0,191 et 0,347 seconde, avec au moins une avance de frame entre eux. Le manifeste enregistre les temps effectivement observés et leurs SHA256. `active.png` est leur planche 2 × 2, lue de gauche à droite puis de haut en bas. Les quatre phases de `frames` restent apparition, actif, contact, expiration.

La capture représente la présentation, avec positions de lancement figées. Elle n'est pas une preuve de collisions, de dégâts ni de vitesse du jeu. Aucun test, replay de capture ou appel modèle supplémentaire n'a été lancé pour préparer cette modification ; seul le build autorisé apporte une preuve de compilation.
