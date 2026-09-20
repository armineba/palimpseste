# Génération réelle de formes 3D — 20 septembre 2026

## Changement livré

D09 remplace la projection systématique des contours d'encre par une forme 3D choisie dans l'interprétation d'Astra. Le contrat propose 21 formes ; elles se combinent aux porteurs et aux 141 recettes d'effets existantes. Astra A `2.1` choisit `visual_form`, Luna B `1.8` recopie `appearance.form` et le compilateur vérifie leur correspondance. Une géométrie contrôlée `sp.geometry.resolver/2.0.semantic` règle la trajectoire ou l'emprise. Les nouveaux objets ne portent aucune `signature_geometry_id` issue du dessin.

Les anciens paquets conservent leur rendu et leur cache. Les nouveaux paquets à forme demandent le client `1.1.0`. Aucun code modèle, build ou outil d'exécution n'est accepté dans un sort.

## Appels réellement exécutés

Diagnostic `active` lancé sous `PalRuntimeSvc`, contrôle horodaté `2026-09-20T14:36:33Z`, code final 0. Il a utilisé un dessin existant (SHA-256 `18b46d38be981f7221115176fa3cd0c695db202d080abef96b7e3c9d52afe96c`) et son encre (SHA-256 `32ca65a531cb0e3e54870a6cd89c4931a245c7bc0c1a843f24268403474692f9`). C'est un diagnostic opérateur ; aucun ancien parchemin joueur n'a été réécrit.

| Étape | Modèle / effort demandés et rapportés | Résultat |
|---|---|---|
| A2.1 réel | `gpt-6-astra` / `max` | « Ruée du golem au poing-bélier », `golem`, `stone`, `projectile`, `r_masse_repulsive` |
| B1.7 initial réel | `gpt-5.6-luna` / `max` | Plan compilé, mais rayon 1 cm repéré trop petit pour le rendu |
| B1.8 réel, A précédente figée | `gpt-5.6-luna` / `max` | Rayon 55 cm, minimum golem 45 cm ; validation et compilation réussies |

La règle de taille corrige une lacune du contrat initial : pour chaque forme de projectile, un rayon minimum explicite est publié dans le catalogue et vérifié avec `semantic_projectile_size`. B1.8 explique cette règle. La description d'Astra n'a pas été régénérée pour cette correction technique.

L'appel B1.8 (`plan`, contrôle `2026-09-20T14:46:33Z`, code final 0) a produit : `scale_cm=180`, `radius_cm=55`, `speed_cm_s=1000`, `range_cm=1800`, `lifetime_ticks=180`, `motion=straight`, `contact_filter=hostile`, palette `stone`, forme `golem`, signature `null`. Les compteurs rapportés : A initial 28 623 jetons entrée / 2 891 sortie ; B initial 16 644 / 1 665 ; B corrigé 17 002 / 1 393. Aucun montant facturé ni répartition abonnement/crédits n'est déduit de ces compteurs.

| Pièce privée immuable | SHA-256 |
|---|---|
| `doctor-semantic-active-20260920.json` | `431005f1c068b8424a9f94437747b174d8187e9583a6ea880caa2a38d83a9a5f` |
| A `c835b12143874581ad3acee61caab3a4/final.json` | `497e2c6092448b35e125b8aee9abfd0a23e749be9ec42348d6841b6fc34e4d4f` |
| B initial `f88e114285274cf1897fda22803afc29/final.json` | `0c076f36da553a692a1ee34fc4fed3328da631b0e7bc8856d2f2c5f3972c6750` |
| `doctor-semantic-size-plan-20260920.json` | `905d844e6eebb43313daf28e80491c7853d8d8563fd6c0495ecebeea4880a273` |
| B1.8 `453c4a0f913a442fab10345e48e97e04/final.json` | `a871d855f1044ad09f64bba624e6e17170652f335292a9a864c4b0af9c669bfe` |
| Paquet diagnostic compilé B1.8 (identités synthétiques de sonde) | `919166f67635480171b90d116180e9a7871ef3253f338befa1b9046e15370d44` |

Les fichiers doctor sont conservés sous `E:\PalimpsesteRuntime\evidence\pending` et les sorties A/B sous `E:\PalimpsesteRuntime\attempts`. Les journaux privés et les identités de compte ne sont pas publiés.

## Déploiement observé

API PID `19592`, worker PID `5532`, lancés avec les scripts vérifiant identité, ACL et empreintes. Santé API HTTP 200. Doctor local final code 0, sans problème de production ni appel modèle. L'observation CLI est `codex-cli 0.154.0-alpha.6.2` ; les étapes réelles rapportent séparément le modèle et l'effort. Les outils d'exécution du worker restent désactivés.

| Fichier installé | SHA-256 |
|---|---|
| API | `e98d511270ac2e6722f1946a368730968f640efe569b476073dc33c521f9e194` |
| Worker | `19dbb0856f0504b4a51230e35086d4d44c9026135211b979a375890a9dcd7eda` |
| ProviderDoctor | `c4a516c5257dcc48bafea6266c38c6e4d8d2b25838e5c7b65f7e233dc8733ef9` |
| WorkerService.Child.ps1 | `cd0a11697fa3dcb8cbf59774a9c8c357e22f4be18a0999fa20df869ce7607fe8` |
| Prompt A2.1 | `f0ec2deb6d5488ddfc7b573be439ba7ec1806e83685fcc153cb1e61f7a017132` |
| Prompt B1.8 | `c28dbc7bf178e9a0b7eba783c9fc2d7a20b94d0498cdb0a899ccaf3aedf3491e` |
| Catalogue capacités et minima de taille | `409f327500c33a60ecb9d9f80727a8c09982879ece8c88d5c240b3d58730c1a3` |
| Doctor local final | `1b6fa7178da61f2d2a5d65665656d567cff9c454a940e3f892661c25196d4dfe` |

## Vérifications et limites

L'[archive backend Windows](../../../deliverables/Palimpseste-Backend-Windows-x64.zip) finale contient **107 fichiers**, **111 439 621 octets**, SHA-256 `44d25c9986b4fda4853750dbd6922698a8d1187de956bddf6a393348ee15e764`. Les 107 entrées ont été comparées par empreinte aux fichiers publiés : zéro écart. Les empreintes des exécutables et scripts enfants référencées par les lanceurs de l'archive correspondent à cette archive. A2.1/B1.8, les contrats et l'état de livraison du Player sont inclus ; aucun fichier de clé, `auth.json`, `.env` privé ou `.pdb` n'y figure. Le présent paragraphe a été ajouté après construction de l'archive ; sa copie interne de cette preuve s'arrête aux résultats antérieurs au packaging.

- Smoke Core réussi : 141 recettes et 705 couples recette/porteur ; indépendance du contour brut, provenance sémantique, rejet des altérations et rayon golem 1 rejeté / 45 accepté ; parité des 21 minima catalogue/code.
- QA documentaire 61/61 ; tests de sécurité provider réussis (arguments, environnement nettoyé, racines, injection, bornes d'entrée). Ces commandes ne font aucun appel modèle.
- [Preuve Unity](../unity/semantic-visuals-2026-09-20.md) : tests et build séparés des appels ci-dessus.
- Aucun nouveau parcours joueur complet après D09 ni verdict humain sur ce rendu n'est déclaré ici. Un diagnostic compilé ne prouve pas son affichage dans le Player. La variété sur 30 dessins, l'écoute du son, le ressenti VFX et les essais sur un autre PC restent à accepter humainement.
- Le déploiement constaté reste le laboratoire local ; aucune ouverture HTTPS publique du VPS n'est revendiquée.
