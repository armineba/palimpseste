# D16 — Planche d’animation et présentation Unity 1.6

Les nouveaux sorts portent `visual_reference.animation_sheet` (`sp.animation-sheet/1.0`, 3 lignes, 7 colonnes) et le SHA-256 `source_atlas_sha256` du PNG généré avant cadrage serveur. Le client vérifie la planche finale de 1536 × 1152 pixels, son hash, la provenance et les valeurs du contrat avant le rejeu. Les archives sans ce profil gardent leur présentation précédente.

## Lecture dans le jeu

Le bouton **Agrandir l’image** ouvre la référence entière. La molette ou les commandes −/+ permettent un zoom jusqu’à ×7 ; maintenir et glisser déplace l’image. **Vue entière** réinitialise le cadrage et **Échap** ferme la vue. Le dessin, le sort sauvegardé et la caméra du laboratoire restent les mêmes.

## Capture Pro précompilée

Le mode opérateur existant `--palimpseste-visual-capture <cache absolu>` produit quatre bandes `appearance.png`, `active.png`, `contact.png`, `expiration.png` de 2048 × 320 pixels. Chaque bande contient sept vues carrées de 288 × 288 pixels, numérotées de 1 à 7. La ligne DISPARITION de la cible correspond à `ending_basis` (`contact` ou `expiration`) ; l’autre fin constitue une preuve distincte.

Les instants normalisés sont **0, 130, 290, 470, 640, 820 et 1000 millièmes**. APPARITION couvre la durée maximale d’entrée des sujets initiaux. STABLE couvre leur période active maximale. Chaque fin couvre la durée de sortie déclarée, ainsi que l’apparition puis la sortie décorative des enfants déclenchés par cet événement. Contact et expiration partent de rejeux indépendants. Aucun acteur, collision ou dégât n’est exécuté dans ce mode de présentation.

Les 28 images sources `appearance_0.png`…`expiration_6.png` sont de vrais rendus Unity de 1024 × 1024 pixels. Elles sont réduites sans déformation et sans changer de caméra pour composer les bandes. Le manifeste `capture.json` conserve quatre entrées `frames` et ajoute `phase_samples` : durée de phase et, pour chaque échantillon, index, instant normalisé, temps de simulation demandé/observé, fichier et SHA-256.

L’horloge `Time.captureDeltaTime` avance la présentation par pas d’au plus 1/120 s, réduits pour atteindre chaque instant demandé. Le coût d’encodage PNG n’escamote donc pas les poses d’une apparition courte. Ces temps sont des **temps de simulation de présentation**, pas des mesures du temps physique écoulé. Les FPS sont mesurés séparément à horloge normale, sur de vrais rendus URP `StandardRequest` avec lecture GPU complète synchrone. Les images actives et les poses intermédiaires doivent présenter du contenu ; seules les extrémités explicites des phases d’apparition/disparition peuvent être vides.

Les archives D15 gardent leurs quatre images et leur planche active 2 × 2. Le mode D16 ne lance aucun modèle, aucun build et n’accepte aucune commande ni chemin venant des données d’un sort. Le délai opérateur reste borné à 30 secondes de temps réel.

## État de vérification

Le Player Windows x64 IL2CPP **1.6.0 a été compilé avec succès**, en une compilation Unity 6000.3.24f1, code de sortie 0. Les fichiers construits sont dans `game/Build/WindowsAnimationSheetRelease`. Le journal réel `game/Logs/animation-sheet-build.log` contient `PALIMPSESTE_BUILD_OK` ; son SHA-256 est `773d9ce368182c0e8440b9699cf84a252457039a05d65a0a2b399fb6d40059cd`.

`GameAssembly.dll` : 51 350 528 octets, SHA-256 `4a43ae59f07a5d23db7f74cacf5edb4191df6e6220e67db46dc1059181e96e2a`. `global-metadata.dat` : 10 605 488 octets, SHA-256 `fcf945ed17107ef15e96160e73c703a6f17d84240acd2daa26e21d1b80ec0925`.

Aucun test de jeu, lancement du Player, capture de développement ou appel de génération n’a été exécuté pour cette modification. La lisibilité, le rendu, la fluidité et l’acceptation visuelle restent à vérifier dans le jeu par l’utilisateur.
