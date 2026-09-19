# Publication .NET isolée du 20 septembre 2026

Les trois commandes `dotnet publish` ont quitté avec le code 0, en Release,
`win-x64`, `--self-contained true`, `PublishSingleFile=true` et
`PublishTrimmed=false`. Les fichiers sont sous
`E:\Palimpseste\.runtime\operator-staging\`, dont l'ACL donne FullControl
uniquement à Administrateurs et SYSTEM. Les exécutables publiés héritent de
cette ACL ; le worker joueur n'y a pas accès.

| Exécutable | SHA-256 |
|---|---|
| `api/Palimpseste.Api.exe` | `4FD29F3530534606D15F246755FBF97B852613846405EA05297758CE94C3E173` |
| `doctor/ProviderDoctor.exe` | `4135407E2366FACC20F5528AE4ED11733DF1F54D0486121CEDF4DD98F1550E6E` |
| `worker/Palimpseste.Worker.exe` | `E8F4F623A0C38884834B3E8965C35D602DA9A581371FF0820679B50936795FBA` |

Ce sont des candidats de déploiement. Aucun de ces fichiers n'a été copié
dans le service vivant, démarré ou testé contre un fournisseur réel après
publication. Le doctor local précédemment exécuté concerne l'ancien binaire
du runtime. La migration 003 et l'API publique restent à déployer.
