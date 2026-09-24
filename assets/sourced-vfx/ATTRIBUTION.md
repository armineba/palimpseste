# Auteurs, licences et provenance

## Surfaces TinyPlay et Keijiro — D19

Les profils plasma, force_field et toxic du renderer sont des portages adaptés des graphes **TinyPlay / URPShadersCollection**, commit `6e663fffccd00a4cce837644a29f6e8f82a6e372`, sous [MIT, copyright TinyPlay 2022](licenses/TinyPlay-URPShadersCollection-MIT.txt). Les images `jellyPlasmaTexture.jpg` et `Noise.png` sont copiées sans modification dans `game/Assets/Palimpseste/Resources/SourcedVfx/`, sous les noms `tinyplay_plasma.jpg` et `tinyplay_noise.png`.

Le profil spectral_flow réutilise l'opérateur HLSL Divergence Free Noise 3D et adapte les opérations de Textureless Strip de **Keijiro / VfxGraphAssets**, commit `5013c195305288fab61cd71f72a4628dbe9d0ea4`, sous [Unlicense](licenses/Keijiro-VfxGraphAssets-Unlicense.txt). La dépendance SimplexNoiseGrad conserve sa licence MIT ci-dessous. Les notices sont livrées dans les ressources Unity et dans ThirdPartyNotices du Player.

Voir [les chemins, empreintes et adaptations](../../docs/D19_SOURCED_SURFACES.md). Aucun code xtaja sans licence, aucun package Magic Effects FREE non acquis, aucun graphe HDRP des Samples Unity n'est redistribué comme partie de ces profils.

## Textures Kenney — CC0 1.0

Les 12 textures de `textures/kenney-particle/` viennent de **Particle Pack**, créé par **Kenney Vleugels / Kenney.nl**. La licence incluse nomme la version **1.1** ; la page web indique la publication 1.0. Le SHA de l'archive identifie précisément la source utilisée. Crédits additionnels des modèles de filtres conservés : Indigo Ray, Craig Nisbet, Zoltan Erdokovy, Heliagon, ThreeDee, Killst4r et Tim2501.

Les quatre textures de `textures/kenney-smoke/` viennent de **Smoke Particles**, créé par **Kenney Vleugels / Kenney.nl**. `poison_puff_00.png` est le fichier original `PNG/Fart/fart00.png`, renommé sans modifier ses octets ; les autres correspondances exactes figurent dans le catalogue.

Les pages officielles et les licences incluses autorisent l'usage personnel et commercial sous **CC0**. Le crédit Kenney est conservé volontairement ; il n'est pas une obligation de ces packs. Sources : [Particle Pack](https://kenney.nl/assets/particle-pack), [Smoke Particles](https://kenney.nl/assets/smoke-particles), [CC0](https://creativecommons.org/publicdomain/zero/1.0/). Licences originales : [Particle Pack](licenses/Kenney-Particle-Pack-CC0.txt), [Smoke Particles](licenses/Kenney-Smoke-Particles-CC0.txt).

## Bruit HLSL — MIT

Les trois fichiers de `reference-code/noise-shader/` proviennent de [Keijiro / NoiseShader](https://github.com/keijiro/NoiseShader/tree/550100d4a74de1ba90eb1b8e90f25f9dbeec28d2), commit `550100d4a74de1ba90eb1b8e90f25f9dbeec28d2` :

- `Common.hlsl` : fonctions auxiliaires.
- `SimplexNoise2D.hlsl` et `SimplexNoise3D.hlsl` : bruit et gradient analytique.

Auteurs originaux : **Ian McEwan / Ashima Arts**, maintenance et autres fonctions **Stefan Gustavson**, adaptation HLSL pour Unity **Keijiro Takahashi**. Les en-têtes d'origine sont intacts. La [licence MIT originale](licenses/NoiseShader-MIT.txt) doit accompagner toute copie ou portion substantielle redistribuée ; la version épinglée renonce aux droits sur les modifications du port tout en conservant les conditions amont.

Ces fichiers sont fournis pour revue du développeur. Leur présence ne signifie ni installation du package, ni exécution, ni compatibilité Unity 6.3 déjà vérifiée.
