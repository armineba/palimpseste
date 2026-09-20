# Publication GitHub et historique

Dépôt cible : https://github.com/armineba/palimpseste, branche `main`.

La copie de publication a été préparée depuis le commit local `a858272`, après ajout de la passation D13. Le dépôt de développement d'origine a été conservé sans réécriture. Sur le poste d'origine, la copie destinée à GitHub se trouve dans `E:\Palimpseste\Palimpseste_GitHub` ; le projet Unity en cours d'utilisation reste dans son dossier initial.

Les 24 commits antérieurs sont conservés dans leur ordre, avec les archives `deliverables/*.zip` transférées vers **Git LFS**. Cette conversion change les identifiants de commit. La correspondance `ancien,nouveau` figure dans [git-lfs-commit-map.csv](git-lfs-commit-map.csv), sans en-tête. Les identifiants des preuves historiques désignent toujours les commits d'origine observés ; ils ne sont pas remplacés dans les rapports.

Après clonage :

```bash
git lfs install
git lfs pull
```

Les deux archives courantes sont des fichiers ZIP réels après récupération LFS :

| Archive | Octets | SHA-256 |
| --- | ---: | --- |
| Backend Windows | 210806064 | `6c29f85430cf1d7a9caeec02c8625343b811c754ac831a69bec60379e10f56a2` |
| Player Windows 1.3.0 | 44320872 | `0ca7d134f733632f23483726b70fbbeb1de750b046d1478efa94f18d4891ad7b` |

Les sources, assets, contrats, prompts, scripts, cahier et preuves publiques sont dans Git. Les caches de compilation Unity/.NET et les données privées du service restent hors dépôt. Aucun fichier d'authentification, mot de passe PostgreSQL ou jeton joueur n'est nécessaire au clonage ; le fonctionnement du backend sur un autre poste nécessite son propre provisionnement, décrit dans [PASSATION.md](PASSATION.md).

`MANIFEST.sha256` est régénéré pour la copie publiée avec les ZIP récupérés. Les anciens manifestes dans l'historique datent de leur livraison : la conversion LFS de `.gitattributes` n'en fait pas des manifestes de publication récents.
