# Migration 006 du laboratoire local — 20 septembre 2026

Avant modification, `ops/backup-lab.ps1` a sauvegardé `palimpseste_lab` et 4 artefacts privés dans `E:\Palimpseste\.runtime\operator-staging\backup-before-migration006-20260920`. Le manifeste et le dump ont la même empreinte SHA-256 `41467EC802436B99926AF7A5422C1F3F980FE8469D28D04180313D3C4DB72D96` pour le dump. Aucun mot de passe n'a été journalisé.

`psql -X -v ON_ERROR_STOP=1 -f backend/migrations/006_plan_prompt_version.sql` a terminé avec le code 0 sur `palimpseste_lab` : `BEGIN`, `ALTER TABLE`, `UPDATE 0`, `ALTER TABLE`, `COMMIT`. Le `UPDATE 0` signifie qu'aucun plan existant n'exigeait le backfill dans cette base. Une requête `information_schema.columns` a ensuite retourné `is_nullable=NO` pour `public.spell_plans.prompt_version` (une colonne correspondante). Cette migration prépare la persistance de la version B ; elle ne démarre pas le worker ni ne démontre de sort joueur.

La même migration a terminé avec le code 0 sur `palimpseste_test`, avec `UPDATE 0` et `is_nullable=NO`. `dotnet run --project tests/Palimpseste.Worker.DbSmoke -c Release --no-restore`, exécuté avec la connexion de cette seule base test, a quitté avec le code 0 : claim/lease, heartbeat, reprise et fencing, réparations durables, incidents incertains et temporisation de retry sont passés. Ce smoke ne lance pas Codex ni Unity.
