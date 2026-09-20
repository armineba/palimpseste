# Auteurs, licences et provenance

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
