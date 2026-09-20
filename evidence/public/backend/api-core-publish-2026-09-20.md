# Publication API après correction Core — 20 septembre 2026

Commande exécutée, code de sortie **0** :

```powershell
dotnet publish backend/Palimpseste.Api/Palimpseste.Api.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o E:\Palimpseste\.runtime\operator-staging\api-core-2026-09-20 -v:q
```

| Contrôle | Résultat observé |
| --- | --- |
| Nouveau binaire privé | `E:\Palimpseste\.runtime\operator-staging\api-core-2026-09-20\Palimpseste.Api.exe` |
| Taille | 105 835 150 octets |
| SHA-256 nouveau | `F01901EBF2D1A71F670FC609F8696F322D8DD0489379B8935BA322FCEF1C9F52` |
| En-tête | Signature PE valide, machine `0x8664` (AMD64) |
| Dossier publié | 10 fichiers, 106 321 689 octets au total |
| ACL dossier et EXE | Propriétaire `BUILTIN\Administrateurs`; droits hérités `FullControl` seulement pour `BUILTIN\Administrateurs` et `AUTORITE NT\Système`, hérités du staging protégé |
| Source Core au moment de la publication | `shared/Palimpseste.Core/SpellCompiler.cs`, SHA-256 `76A94C0B2AFCCA18EB651127BF588BFC355F5A518CDE1840C2982607C529F038` |

L'ancien candidat `operator-staging/api/Palimpseste.Api.exe` reste distinct : 105 831 054 octets, SHA-256 `66B253C16D695F06658C96DE38F53F69C26EAB13911FCD55C531B55B185C39EB`. Le nouveau binaire diffère de 4 096 octets et a été publié après la correction Core. Le dossier ancien n'a pas été remplacé.

Cette preuve établit la publication et les contrôles statiques du binaire. **Aucun processus API n'a été démarré, aucune base n'a été ouverte, et aucun modèle n'a été appelé** pendant cette opération. Le staging privé n'est pas un déploiement du service.
