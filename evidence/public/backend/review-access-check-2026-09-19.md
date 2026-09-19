# Revue à deux comptes creator — 19 septembre 2026

La migration incrémentale `backend/migrations/002_review_access.sql` a été
appliquée sur les bases locales `palimpseste_test` et `palimpseste_lab` après
`001_initial.sql`. Elle est idempotente ; l'archive backend doit fournir les
deux fichiers dans l'ordre.

Le smoke `tests/review_access_http_smoke.py` a démarré l'API Release sur
loopback et a créé deux principals `creator` et deux jetons synthétiques dans
`palimpseste_test`. Les secrets ont été générés en mémoire et ne sont pas
présents dans cette preuve ni dans le dépôt. Le scénario HTTP/DB a observé :

- refus de la soumission du second compte avant délégation ;
- rejet d'un `reviewer_id` usurpé, d'une délégation par le non-propriétaire et
  d'un `case_id` mal formé ;
- délégation du couple exact sort/cas par le propriétaire ;
- soumission et replay idempotent par le second compte ;
- lecture du sort toujours refusée au second compte ;
- révocation de la délégation, vérification DB à zéro ligne et refus d'une
  nouvelle soumission ;
- soumission du propriétaire encore possible après révocation et nettoyage
  des deux principals, jetons, grants et artefacts.

Sortie observée :

```json
{"result": "passed", "checks": 13, "synthetic": true, "distinct_creators": true, "grant_case": "two-creators-review", "read_scope": "owner_only", "cleanup": "completed"}
```

Le test existant `tests/review_http_smoke.py` a aussi repassé avec 6 contrôles.
`reviewer_id` est maintenant l'identifiant hexadécimal du principal
authentifié ; il ne peut pas être choisi pour usurper un autre créateur.
`token-create creator` affiche le `principal_id` pour permettre un échange
privé avant la délégation.

Cette preuve est synthétique : elle ne constitue aucun verdict humain, ne lit
aucun sort d'un autre compte et n'effectue aucun appel Codex ou Luna.
