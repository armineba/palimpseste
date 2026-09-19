# Échange d'invitation joueur — essai local du 19 septembre 2026

Base : `palimpseste_test` après application de `backend/migrations/003_player_invitations.sql` (`BEGIN`, `CREATE TABLE`, `CREATE INDEX`, `COMMIT`). Aucun identifiant ni invitation en clair n'est publié ici.

Commande exécutée : `powershell -NoProfile -ExecutionPolicy Bypass -File tests/player_invitation_http_smoke.ps1` contre `Palimpseste.Api.exe` Release sur `127.0.0.1:18111`. Résultat observé : **4/4** — échange du code à usage unique (200), deuxième échange refusé (401), accès anonyme à `/v1/parchments` refusé (401), accès avec le jeton joueur nouvellement créé accepté (200). Le test nettoie son invitation, son jeton et son principal dans la seule base de test. Aucune génération Codex n'est déclenchée par ce test.

`dotnet build backend/Palimpseste.Api/Palimpseste.Api.csproj -c Release -v:q` a réussi : 0 avertissement, 0 erreur. L'analyse syntaxique PowerShell de `ops/issue-player-invitation.ps1` a réussi. Un essai supplémentaire d'exécution du script opérateur avec suppression de sa fixture a été rejeté **avant exécution** par la revue automatique de l'outil (`blocked by policy`) ; aucune preuve d'exécution de ce script n'est revendiquée.

Après le smoke HTTP, le parseur JSON de la route a été durci pour refuser les clés dupliquées et les propriétés supplémentaires. La nouvelle version a recompilé avec 0 avertissement/0 erreur ; son smoke HTTP n'a pas été relancé, car le démarrage supplémentaire de l'API avec l'environnement privé a été rejeté avant exécution par la revue automatique de l'outil (`blocked by policy`). Le résultat 4/4 concerne donc le comportement principal avant ce durcissement limité.

La migration n'a pas encore été appliquée sur `palimpseste_lab` ou le VPS par cet essai. Aucun joueur distant ni distribution privée de fichier d'invitation n'a été testé.
