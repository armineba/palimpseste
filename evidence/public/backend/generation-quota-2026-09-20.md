# Test isolé du plafond de génération — 20 septembre 2026

- Compilations : `dotnet build Palimpseste.sln -c Release -v:q` et `dotnet build tests/Palimpseste.Api.GenerationQuota/Palimpseste.Api.GenerationQuota.csproj -c Release -v:q` ; code 0, 0 avertissement, 0 erreur pour chacune.
- Exécution : `dotnet run --project tests/Palimpseste.Api.GenerationQuota/Palimpseste.Api.GenerationQuota.csproj -c Release --no-build` avec `DATABASE_URL` fourni uniquement au processus depuis le fichier privé de la base locale `palimpseste_test` ; code 0.
- Après revue indépendante, les transactions des deux handlers ont été fixées explicitement à `READ COMMITTED`. Le projet de test a été reconstruit (`0` avertissement, `0` erreur), puis exécuté de nouveau sur la même base isolée avec le code `0`.
- Résultat exact de la dernière exécution : `Generation quota DB smoke: 20 concurrent admissions, 3 committed, per-principal <=2, individual and global overflow rejected, expired and rolled-back jobs ignored.`
- Le test a créé puis supprimé un schéma temporaire aléatoire. Il n'a appelé ni API HTTP ni modèle. Aucune chaîne de connexion ni aucun jeton n'est inclus dans cette preuve.
- Les plafonds de production par défaut sont 3/principal et 12/global sur 24 heures ; le test réduit les deux valeurs pour provoquer des refus concurrents.
