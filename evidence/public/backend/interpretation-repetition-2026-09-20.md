# Répétition perçue des lectures Astra — 20 septembre 2026

## Constat sur les deux jobs joueur

| | Premier dessin | Second dessin |
|---|---|---|
| Job | `997d0b50-602a-4668-981e-a39108334bf4` | `e6d656f0-4a13-4ba5-9f44-3b4e7d2f6069` |
| État observé | `ready` | `ready` |
| SHA-256 du dessin | `CE03892A4C8E8491027E2BD6D01608BAA3742AC4D966E4737B002DE264081204` | `18B46D38BE981F7221115176FA3CD0C695DB202D080ABEF96B7E3C9D52AFE96C` |
| Titre Astra | « Nœud de cuivre » | « Nœud de cuivre mordant » |
| SHA-256 de la description, préfixe | `F1E12E…` | `16EEC8…` |
| Porteur | `projectile` | `trap` |
| Recette | `r_crochet_entravant` | `r_estoc_saignant_lien` |

Les empreintes différentes attestent que les deux dessins et les deux descriptions ne sont pas identiques. Les recettes et les porteurs diffèrent également : le système n'a pas rejoué **à l'identique** le même sort. Le créateur observe néanmoins une proximité forte des titres et de leur thème cuivre/nœud ; ce retour est valide même si les données techniques diffèrent. Seules les empreintes des descriptions sont abrégées dans ce relevé public ; ne pas les utiliser comme empreintes de vérification d'un fichier.

## Hypothèse et correction en cours

Le prompt A `1.9` disait qu'`ember` rend un « rouge brique ou cuivre » et donnait l'exemple « un trait rouge brun peut t'inspirer un rayon de lave ». Cette formulation pouvait orienter Astra vers une matière ou une palette récurrente lorsque les dessins utilisent la même encre. Deux sorties ne prouvent pas à elles seules que cet exemple a causé le thème « cuivre » ; la composition des images, le catalogue de recettes ou le hasard de génération peuvent aussi contribuer.

Le prompt A `2.0` supprime cet exemple coloré, demande deux ou trois traits distinctifs de **chaque image** avant l'interprétation, et exige des liens explicites entre au moins deux particularités visibles, le titre, le porteur, les mécaniques ou le VFX. Le correctif garde la liberté créative d'Astra et le contrat de données contrôlées pour Luna. Il ne modifie pas les descriptions déjà enregistrées.

**Déploiement A `2.0` observé :** worker sous `PalRuntimeSvc`, PID `26964`, exécutable SHA-256 `0674C437841003410C80DDBA2E55E633B0A0355C8EB7076AF206E226B1E7AB55`, prompt installé SHA-256 `13DF5FF446FB0390F0A655A35EEB4312F8A5E7C81D3EC0AE149EA061E8ED4FAA`. Le doctor local du 20 septembre à 14:10:44 UTC a terminé avec code 0 et sans problème de production A/B. Ce doctor n'a effectué aucun appel modèle. La commande groupée de maintenance avait été refusée avant exécution ; les opérations de maintenance ciblées, sauvegarde puis remplacement des seuls fichiers du worker, ont ensuite réussi.

Après ce relevé, un troisième job joueur réel A `2.0`, `7851777c-ddb1-49eb-84f2-641463b15364`, a été observé `ready` : « Le Nœud filant », porteur `projectile`, recette `r_crochet_entravant`, palette `arcane`. Description SHA-256 `fee3ab9390f28baa810432855f383dee39a2d701669b533376ebd144d00f0106`. Le thème « nœud » persiste ; A `2.0` n'a donc pas démontré la résolution du problème créatif.

La décision D09 conduit au prompt A `2.1` : formes 3D issues de l'interprétation et consigne de ne pas assimiler automatiquement les croisements à des cordes ou des nœuds. Un diagnostic réel sur le second dessin a produit **« Ruée du golem au poing-bélier »**, forme `golem`, palette `stone`, recette `r_masse_repulsive`. La [preuve A2.1/B1.8](semantic-visual-generation-2026-09-20.md) distingue cet appel opérateur des jobs joueur. Ce résultat montre une lecture différente sur cette image, sans prouver la variété sur tous les dessins. Les trois anciens résultats joueur restent immuables. Le verdict humain de fidélité et de variété reste ouvert.
