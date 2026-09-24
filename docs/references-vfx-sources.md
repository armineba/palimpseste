# Recherche et ressources VFX réutilisables

## D17 — bibliothèques obligatoires pour reconstruire la planche

Depuis la décision du **24 septembre 2026**, le passage **planche générée → recherche → construction B** doit consulter les cinq [bibliothèques imposées](BIBLIOTHEQUES_VFX_OBLIGATOIRES.txt). L'[inventaire local](../reference/vfx-libraries.inventory.json) conserve commits, licences, exemples et fichiers réellement récupérés. Ces fiches orientent la reconstruction des 21 cases d'un même sort animé ; elles servent également aux révisions B de Dream-loop Pro, qui reprennent le dossier immuable lié à la description et à la planche.

Le code D17 inclut systématiquement ces cinq fiches et une référence complémentaire classée selon le sort. Les pages GitHub autorisées et la référence complémentaire sont lues en parallèle, avec les limites réseau existantes. Magic Effects FREE reste une fiche factuelle locale, sans acquisition, extraction automatisée ni contenu propriétaire fourni au modèle. Chaque fiche distingue consultation, licence, compatibilité et présence réelle dans le Player. Les échecs de lecture en ligne conservent un statut explicite et les notes examinées.

Le prompt B **2.5** impose leur examen avant la composition et l'emploi des techniques pertinentes dans les limites du moteur. Il ne donne pas accès à des prefabs absents : les nouveaux packages ne sont pas encore intégrés au Player. L'import des ressources et graphes compatibles appartient au développement ; il précède leur exposition sous forme d'identifiants contrôlés. **Backend D17 publié et installé le 24 septembre à 21:59 Paris**, avec le resolver, le prompt B 2.5 et l'index cohérents : [preuve d'installation](../evidence/public/backend/vfx-libraries-d17-installation.json). Le Player 1.6.0 reste inchangé. Aucun test, appel modèle, capture, rendu identique ou parcours joueur D17 n'est revendiqué pour cette livraison.

## Historique — socle livré D15

Recherche et téléchargements effectués le **21 septembre 2026, heure de Paris**, à la demande du créateur. Le [catalogue exploitable](../assets/sourced-vfx/catalogue.json) relie chaque ingrédient à sa source, sa licence et son SHA. La sélection ne nécessite aucun achat, abonnement supplémentaire, API payante ou installation de plugin.

## Ressources réellement récupérées

| Source primaire | Fichiers retenus | Licence et usage dans Unity 1.5.0 |
| --- | --- | --- |
| [Kenney Particle Pack](https://kenney.nl/assets/particle-pack) | 12 PNG 512 × 512 : anneaux, sceau, volute, fumée, feu, flamme, deux éclairs, étoile, balayage, traînée. | CC0 ; couches de particules et masques lumineux réutilisables. |
| [Kenney Smoke Particles](https://kenney.nl/assets/smoke-particles) | 4 PNG : vapeur blanche, fumée sombre, explosion orange, nuage vert. | CC0 ; apparition, contact, gaz et dissipation. |
| [Keijiro NoiseShader, commit épinglé](https://github.com/keijiro/NoiseShader/tree/550100d4a74de1ba90eb1b8e90f25f9dbeec28d2) | `Common.hlsl`, `SimplexNoise2D.hlsl`, `SimplexNoise3D.hlsl`. | MIT ; Common et SimplexNoise3D intégrés à la turbulence du shader. SimplexNoise2D reste une référence. |

Les licences ont été lues sur les sources primaires, puis celles présentes dans les archives avant extraction des textures. [Licences et attributions conservées](../assets/sourced-vfx/ATTRIBUTION.md). Les téléchargements ont conservé leurs octets ; les archives sont identifiées par SHA et les HLSL par commit et SHA. Aucun ancien `.unitypackage` du pack n'a été importé.

## Association avec les sorts

Ces associations sont des choix de conception, pas des résultats de tests :

| Famille recherchée | Identifiants disponibles à combiner |
| --- | --- |
| Électricité / tempête | `kpp_spark_01`, `kpp_spark_05`, `kpp_trace_01` |
| Feu / lave / explosion | `kpp_fire_01`, `kpp_flame_01`, `ksp_explosion_00`, `ksp_black_smoke_00` |
| Soin / aura / rituel | `kpp_circle_03`, `kpp_magic_01`, `kpp_star_01` |
| Spectre / vent / dissolution | `kpp_twirl_01`, `kpp_smoke_01`, `ksp_white_puff_00` |
| Poison / gaz | `ksp_poison_puff_00`, `kpp_smoke_01` |
| Bouclier / impact / frappe | `kpp_circle_01`, `kpp_circle_03`, `kpp_slash_01` |

Le resolver D15 consulte le catalogue **avant la construction du plan** pour choisir des ingrédients appropriés à la description, puis alimenter la boucle de comparaison à l'image. Les 16 PNG sont copiés dans `game/Assets/Palimpseste/Resources/SourcedVfx/` ; `appearance.resource_id` relie le choix du plan aux matériaux de construction, couches VFX et particules. Chaque sort choisit sa ressource parmi les 16 disponibles. Un sprite de particule n'impose pas la forme du sort entier. L'image cible, les quatre phases textuelles et les mécaniques contrôlées gardent leurs rôles.

## Repères Unity et revue des sources

Le [catalogue des techniques](../assets/sourced-vfx/references.json) contient sept références primaires, avec `id`, `url`, `technique`, `tags`, familles et ingrédients associés. Les six pages suivantes ont été réellement récupérées dans leur version **6000.3** :

- [Vitesses orbitales et radiales](https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysVelOverLifeModule.html) : spirales, couronnes et attraction visuelle.
- [Bruit de mouvement](https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysNoiseModule.html) : turbulence pour fumée, feu et voiles.
- [Traînées](https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysTrailsModule.html) : historique dans l'espace et extinction indépendante.
- [Émetteurs secondaires](https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysSubEmitModule.html) : bouffées distinctes à la naissance ou à l'extinction, avec déclenchement manuel possible.
- [Particules Unlit URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/particles-unlit-shader.html) : mélange alpha/additif et adoucissement des intersections avec profondeur activée.
- [Bloom URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/post-processing-bloom.html) : contrôle de la diffusion lumineuse.

Les recommandations `suggested_blend` des textures sont des points de départ de conception : alpha pour fumée sombre et nuages, additif pour éclairs et éclats. Le catalogue conserve des résumés originaux et des liens, pas des copies des manuels Unity. La récupération effectuée pendant cette préparation ne prouve pas la récupération lors d'un futur job : le resolver doit conserver son propre résultat et ses empreintes.

**Périmètre de recherche livré D15/D16 : bibliothèque de sources primaires sélectionnées.** Le resolver de cette version choisit jusqu'à quatre références selon la description et les tags, puis lit leurs pages HTTPS autorisées en parallèle, avec délai borné à sept secondes. Les notes de catalogue servent de repli explicite si une lecture échoue. Le dossier de recherche conserve le résultat de chaque lecture et ses empreintes. Ce fonctionnement est une recherche dans cette bibliothèque, pas une exploration générale de tout le Web. La réutilisation des textures sélectionnées reste liée aux 16 identifiants contrôlés. Aucun appel de génération n'a été lancé pour valider cette intégration. D17 remplace désormais cette sélection par les cinq fiches obligatoires et une référence complémentaire pour les nouvelles recherches ; les dossiers déjà persistés restent inchangés.

Les [sources Simplex de Keijiro](https://github.com/keijiro/NoiseShader/blob/550100d4a74de1ba90eb1b8e90f25f9dbeec28d2/Packages/jp.keijiro.noiseshader/Shader/SimplexNoise3D.hlsl) fournissent le bruit analytique de `SpellImageConstruction.shader`. Les copies relues de Common et SimplexNoise3D sont dans `Resources/SourcedNoise/`, avec adaptation de l'include de Common au chemin relatif ; les originaux du catalogue restent inchangés. La licence MIT est conservée et livrée dans `ThirdPartyNotices/NoiseShader-MIT.txt`. SimplexNoise2D n'est pas intégré. Le worker reçoit le renderer précompilé ; aucun shader téléchargé n'est exécuté automatiquement à la demande d'un joueur.

## État initial et intégration livrée

**Sélection initiale :** recherche web primaire, lecture des licences, téléchargement, extraction de 16 PNG et trois sources HLSL, observation des aperçus et de six textures, inventaire des dimensions/empreintes, catalogue et notices. Le champ `status_at_curation` conserve cet état historique.

**Intégration 1.5.0 :** les 16 textures sont importées et reliées au rendu ; Common et SimplexNoise3D sont utilisés par le shader. Le build Unity 6000.3.24f1 et le packaging du Player ont réussi, selon la [preuve de livraison](../evidence/public/unity/lifecycle-delivery.json). Le champ `integration` et les entrées individuelles du catalogue décrivent ces usages.

**Non exécuté pour cette intégration :** test de jeu, capture de gameplay, appel de génération et démonstration réelle de recherche par job. L'acceptation humaine de la qualité visuelle et de la fidélité reste en attente.
