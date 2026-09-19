# Compilation et contrôles du cœur — 19 septembre 2026

Machine Windows observée : AMD Ryzen 7 5800X, 16 processeurs logiques ; NVIDIA GeForce RTX 3070 ; 34 300 878 848 octets de RAM physique. SDK .NET 10.0.401.

Depuis la racine du dépôt, les commandes suivantes ont été exécutées avec leurs résultats observés :

| Commande | Résultat |
|---|---|
| `dotnet build Palimpseste.sln -c Release -v:q` | succès, 0 avertissement, 0 erreur à 18:33 UTC après les correctifs provider/runtime |
| `dotnet run --project tests/Palimpseste.Core.Smoke/Palimpseste.Core.Smoke.csproj -c Release` | succès : PNG, corruption, géométrie avec trou imbriqué, compilation et bornes, rejet sémantique et JSON dupliqué/commenté |
| `dotnet run --project tests/Palimpseste.Worker.DbSmoke/Palimpseste.Worker.DbSmoke.csproj -c Release` avec `DATABASE_URL` de la base `palimpseste_test` | succès : claim, lease, heartbeat, nombre durable de réparations, fencing, tentative incertaine et incident, temporisation puis reprise planifiée avec nouveau jeton |
| `dotnet run --project tests/Palimpseste.Provider.Security/Palimpseste.Provider.Security.csproj -c Release` | succès : arguments de processus, environnement nettoyé, racines autorisées, métacaractères du prompt, limites A/B et rejet d'un modèle/effort rapporté divergent ; exécutable de transport simulé uniquement pour ce test, aucun appel Luna |
| `python qa/validate_contracts.py` | `59/59` contrôles documentaires, 0 échec |
| `python tests/review_http_smoke.py` avec API publiée sur `127.0.0.1:18104`, jeton créateur et base `palimpseste_test` | six contrôles HTTP/DB réussis sur fixture synthétique ; une seule revue écrite puis supprimée ; aucune validation humaine ni appel Luna |
| `ops/package-backend.ps1` puis lancement de `api/Palimpseste.Api.exe` extrait avec `PALIMPSESTE_SPEC_ROOT` pointant vers `spec/` et la base test | ZIP Windows x64 autoportant construit ; `/health/ready` a répondu `200` sur `127.0.0.1:18101`, puis l'API a été arrêtée ; archive inspectée : API, worker, doctor, schémas et prompts A/B/réparation présents, aucun `auth.json` ni prompt de développement |

Le premier build global a échoué uniquement parce que le serveur de smoke `Palimpseste.Api.exe` exécuté depuis `bin/Release` verrouillait le fichier. Le serveur a été arrêté, puis le build global a réussi. Lors de la consolidation finale, un build global et le lancement du test fournisseur démarrés simultanément ont aussi accédé au même `Palimpseste.Provider.dll` ; le test a échoué sur ce verrou de fichier. Relancés **séquentiellement**, le build global a réussi avec 0 avertissement et 0 erreur, puis le test fournisseur avec `--no-build` a réussi. Les commandes ci-dessus ne prouvent ni un appel Luna A/B, ni une recette humaine ou une performance de production.

Après la migration de revue et le refus explicite d'un effort configuré sous `max`, nouvelle passe à 19:31–19:32 UTC : `dotnet build Palimpseste.sln -c Release -v:q` a rendu 0 avertissement et 0 erreur ; `dotnet run --no-build` a réussi pour Core.Smoke, Provider.Security (transport simulé, incluant le rejet de `xhigh`) et Worker.DbSmoke sur `palimpseste_test`. `python qa/validate_contracts.py` a rendu 59/59 et l'analyse syntaxique des 13 scripts PowerShell a rendu 0 erreur. Les contrôles HTTP à deux créateurs sont détaillés dans `review-access-check-2026-09-19.md`.

Une tentative supplémentaire de relancer `tests/api_http_smoke.py` avec les identifiants privés de la base test a été rejetée par le contrôle automatique de la commande avant le lancement du serveur. Aucune requête HTTP ni résultat nouveau ne lui est attribué ; les 16 contrôles indiqués plus haut proviennent de l'essai antérieur effectivement exécuté.
