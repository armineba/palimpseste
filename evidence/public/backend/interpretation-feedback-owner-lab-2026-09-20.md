# Retour de lecture A : smoke HTTP sur API locale du propriétaire

Le 20 septembre 2026, l'API installée sous `PalRuntimeSvc` sur `127.0.0.1:18080` a répondu `200` à `GET /health/ready` avant et après l'essai. Son exécutable `E:\PalimpsesteRuntime\api\Palimpseste.Api.exe` avait le SHA-256 `B9A5AD2EF920C1955333CD2F21B47E567DCBE7E3396CA123C971B4F8D2219545`.

Le test source `tests/interpretation_feedback_http_smoke.py` (SHA-256 `B9931AF616932D59E196660B07924DF4B9C2563A4B3CE98EC286713751744007`) est resté inchangé et limité à `palimpseste_test`. Une copie privée temporaire, limitée explicitement à `palimpseste_lab`, a servi à l'essai contre l'API locale ; son SHA-256 était `EE6E63508ED9F42F752E828D56B9F8CFA5660D5C5F43B20603FA76CB3F518D8C`. La copie se trouve hors du dépôt dans le staging opérateur. Les identifiants, jetons synthétiques et accès PostgreSQL sont restés privés.

Résultat exact : `{"result":"passed","synthetic":true,"model_calls_executed":false,"checks":9,"owner_only":true,"job_unchanged":true,"cleanup":"completed"}` ; sortie du processus `0`. Les neuf contrôles couvrent la description A absente, l'accès d'un autre propriétaire, le hash erroné, l'enregistrement du retour, la répétition idempotente, le conflit d'idempotence, le second retour refusé, la valeur persistée et l'absence de modification du job. Une requête PostgreSQL indépendante après l'essai a trouvé **0** compte synthétique résiduel (`feedback-smoke-owner` et `feedback-smoke-other`).

Cet essai n'a appelé ni Luna ni le worker et n'a pas testé l'envoi depuis le Player Unity. Il ne vaut ni acceptation de la lecture artistique, ni validation du trajet dessin → sort.
