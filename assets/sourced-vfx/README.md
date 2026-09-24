# Socle VFX gratuit — sources conservées

**16 textures PNG originales** de deux packs Kenney CC0 et trois fichiers HLSL de NoiseShader sous MIT. Dans Unity **1.5.0**, les 16 textures sont importées et reliées aux matériaux et particules du renderer ; `Common.hlsl` et `SimplexNoise3D.hlsl` sont utilisés pour la turbulence. Le build et le packaging du Player ont réussi. Aucun test de jeu, appel de génération ou validation visuelle n'est revendiqué pour cette intégration.

- [catalogue.json](catalogue.json) : identifiants stables, familles, tags, usages, chemins, SHA-256 et provenance. `status_at_curation` conserve l'état initial ; `integration` décrit l'état Unity 1.5.0.
- [references.json](references.json) : cinq bibliothèques obligatoires pour reconstruire la planche (D17), en complément des six pages officielles Unity 6.3 et d'une référence HLSL MIT. Les nouveaux packages sont documentés comme non embarqués ; seuls les ingrédients du catalogue sont sélectionnables.
- [Consigne et liens complets](../../docs/BIBLIOTHEQUES_VFX_OBLIGATOIRES.txt) · [Inventaire des copies locales](../../reference/vfx-libraries.inventory.json).
- [SOURCE_MANIFEST.sha256](SOURCE_MANIFEST.sha256) : empreintes des fichiers de ce dossier, sources et métadonnées comprises ; le manifeste lui-même est exclu.
- [ATTRIBUTION.md](ATTRIBUTION.md) : auteurs et conditions de redistribution.
- [Notes d'intégration et recherche](../../docs/references-vfx-sources.md).
- [Preuve du build et du packaging](../../evidence/public/unity/lifecycle-delivery.json).

Les 22 fichiers provenant des auteurs totalisent **1 132 367 octets**. Les PNG ont été extraits directement des archives officielles, sans réencodage ; seuls les noms des quatre fichiers Smoke Particles ont été simplifiés. Les trois HLSL sont les octets du commit amont épinglé. Les archives complètes restent dans le cache de téléchargement opérateur, hors Git ; leurs URLs, tailles et SHA sont dans le catalogue.

## Utilisation dans Unity 1.5.0

Les copies des textures sont dans `game/Assets/Palimpseste/Resources/SourcedVfx/<resource_id>.png`. Le plan choisit `appearance.resource_id` ; les matériaux de construction, les couches VFX et les particules utilisent cette ressource. Les 16 ingrédients sont disponibles, sans imposer leur emploi simultané. Les paramètres de mouvement, couleur, densité et durée restent des données contrôlées du renderer.

Les PNG source sont **indexés avec palette**, et certains sprites de fumée sont de dimensions non carrées. Ils sont importés comme assets ordinaires avec transparence. Ce sont des sprites individuels, **pas une séquence flipbook validée**.

Les copies de `Common.hlsl` et `SimplexNoise3D.hlsl` sont dans `game/Assets/Palimpseste/Resources/SourcedNoise/`. `SpellImageConstruction.shader` utilise leur bruit pour la turbulence des matières animées ; l'include de Common est adapté au chemin relatif. La licence MIT accompagne les sources et le Player dans `ThirdPartyNotices/NoiseShader-MIT.txt`. `SimplexNoise2D.hlsl` reste une référence non intégrée. Les trois originaux de `reference-code/noise-shader/` conservent leurs octets amont. Aucun package ou exécutable tiers n'a été installé.

La compilation prouve la construction du Player. La qualité du rendu et sa fidélité à chaque image restent soumises à la génération du joueur et à son appréciation.
