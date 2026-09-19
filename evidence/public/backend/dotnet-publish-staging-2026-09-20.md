# Publication .NET isolée du 20 septembre 2026

Les trois commandes `dotnet publish` ont quitté avec le code 0, en Release,
`win-x64`, `--self-contained true`, `PublishSingleFile=true` et
`PublishTrimmed=false`. Les fichiers sont sous
`E:\Palimpseste\.runtime\operator-staging\`, dont l'ACL donne FullControl
uniquement à Administrateurs et SYSTEM. Les exécutables publiés héritent de
cette ACL ; le worker joueur n'y a pas accès.

| Exécutable | SHA-256 |
|---|---|
| `api/Palimpseste.Api.exe` | `66B253C16D695F06658C96DE38F53F69C26EAB13911FCD55C531B55B185C39EB` |
| `doctor/ProviderDoctor.exe` | `4135407E2366FACC20F5528AE4ED11733DF1F54D0486121CEDF4DD98F1550E6E` |
| `worker/Palimpseste.Worker.exe` | `E8F4F623A0C38884834B3E8965C35D602DA9A581371FF0820679B50936795FBA` |

Ce sont des candidats de déploiement. Aucun de ces fichiers n'a été copié
dans le service vivant, démarré ou testé contre un fournisseur réel après
publication. Le doctor local précédemment exécuté concerne l'ancien binaire
du runtime. L'API publique reste à déployer.

L'API a été republiée après l'ajout du plafond de génération le 20 septembre.
La commande `dotnet publish backend/Palimpseste.Api/Palimpseste.Api.csproj -c Release
-r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
-o E:\Palimpseste\.runtime\operator-staging\api -v:q` a quitté avec le code 0,
sans sortie de diagnostic. Le SHA-256 du nouvel exécutable est celui du tableau
et son ACL donne FullControl seulement à Administrateurs et SYSTEM. Les
Les migrations 003 et 004 ont été appliquées au laboratoire après cette
publication, voir `lab-migrations-2026-09-20.md`.
