# Retour de lecture A dans le laboratoire — 20 septembre 2026

## Ce qui est écrit

`POST /v1/jobs/{id}/interpretation-feedback` accepte un retour du propriétaire du job de production après la persistance de A. La requête porte l'empreinte SHA-256 exacte de la description A, un verdict `correct` ou `incorrect` et une note humaine de 1 à 2 000 caractères. La route vérifie propriétaire, hash et clé d'idempotence, puis insère au plus un retour par job. Elle ne modifie ni la description, ni le job, ni le parchemin, ni le sort, et ne lance pas Luna. Le texte humain reste une donnée privée non utilisée comme prompt ou instruction serveur.

La migration `005_interpretation_feedback.sql` a d'abord été appliquée à
`palimpseste_test` après vérification explicite du nom de la base, puis
rejouée sans erreur : table, trois index (clé primaire, unicité, index
propriétaire) et sept contraintes vérifiés. Elle a ensuite été appliquée à
`palimpseste_lab` après sauvegarde vérifiée dans le dossier privé
`backup-pre-feedback-20260920-01` (manifest SHA-256
`C68080C1DC05CE55E1A3D0BE3A5BBB3470B5FE03458B4C61B350BC7D3CAE56A6`).
Le résultat de la migration sur le laboratoire inclut `BEGIN`, `CREATE TABLE`,
`CREATE INDEX` et `COMMIT` ; le contrôle de la table y relève huit colonnes.
Cette migration n'a démarré ni l'API ni le worker.

## Contrôles exécutés

| Contrôle | Résultat |
|---|---|
| `dotnet build backend/Palimpseste.Api/Palimpseste.Api.csproj -c Release --no-restore` | Réussi, 0 avertissement, 0 erreur |
| `python qa/validate_contracts.py` | 61/61 contrôles documentaires réussis ; aucun appel modèle ni test Unity |
| `python -m py_compile tests/interpretation_feedback_http_smoke.py` | Réussi |
| `psql -f backend/migrations/005_interpretation_feedback.sql` sur `palimpseste_test`, puis seconde exécution | Réussi et idempotent |
| Sauvegarde vérifiée puis migration 005 sur `palimpseste_lab` | Réussi ; huit colonnes contrôlées |
| Contrôle SQL `to_regclass`, `pg_indexes`, `pg_constraint` | `true|3|7` |
| `dotnet publish` API vers `E:\Palimpseste\.runtime\operator-staging\api-feedback-2026-09-20` | Candidat publié, exécutable SHA-256 `B9A5AD2EF920C1955333CD2F21B47E567DCBE7E3396CA123C971B4F8D2219545` ; non installé et non démarré |
| `tests/interpretation_feedback_http_smoke.py` | **Non exécuté** : il exige une API de test en marche ; un démarrage d'API avec l'environnement privé a déjà reçu `blocked by policy` de la revue automatique avant exécution. Aucun contournement tenté. |

Le premier contrôle de métadonnées supposait deux index et a échoué ; PostgreSQL en crée trois avec la clé primaire et l'unicité. La vérification a été corrigée à trois et a réussi. Cette erreur de comptage ne concernait pas la migration elle-même.

## Empreintes SHA-256

| Fichier | SHA-256 |
|---|---|
| `backend/migrations/005_interpretation_feedback.sql` | `B29F28C331884A188C933AFEC5EE2CA896251DA0A109EE1C5E506182788F1044` |
| `backend/Palimpseste.Api/bin/Release/net10.0/Palimpseste.Api.dll` | `872CA385E66A17FFAAEBCE07B9EE1D6CF704E589F20287BAA5C16BB93151E2C9` |
| `qa/rapport_controles_documentaires.json` | `6677748A56E6F41EC5B1FAC8303AF8B69BCB6B4EDA4AA8891B7B1CEE987E2FA5` |
| `tests/interpretation_feedback_http_smoke.py` | `B9931AF616932D59E196660B07924DF4B9C2563A4B3CE98EC286713751744007` |

La fidélité d'une correction humaine et le parcours du Player connecté restent à tester. Le prompt A `sp.prompt.a/1.1` reste inchangé tant que l'intention exacte du dessin refusé n'est pas connue ; une nouvelle version de prompt se valide sur un corpus de mise au point distinct et conserve le verdict humain séparé.
