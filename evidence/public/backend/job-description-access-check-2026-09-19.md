# Contrat description A — smoke API du 19 septembre 2026

Le test `tests/job_description_http_smoke.py` a été exécuté sur PostgreSQL
`palimpseste_test` avec l'API Release sur loopback. Il crée deux principals
joueur synthétiques, un job et un artefact JSON, puis supprime ses lignes et
son fichier.

```json
{"result":"passed","checks":18,"synthetic":true,"model_calls_executed":false,
 "stable_states":["resolving_geometry","planning","validating","waiting_retry","needs_operator","ready"],
 "ownership":"owner_only","hash_header":"X-Content-SHA256","cleanup":"completed"}
```

Les contrôles couvrent `null` avant A, l'ID public stable après la ligne
`interpretations`, la lecture JSON propriétaire avec hash recalculé, le refus
du job et de l'artefact par un autre principal, le refus sans bearer et
l'absence d'un artefact brut non lié dans le Job DTO. Cette fixture ne prouve
ni appel Codex/Luna, ni validation humaine, ni rendu Unity.
