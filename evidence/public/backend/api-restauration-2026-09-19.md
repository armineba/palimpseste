# API, capture et restauration — 19 septembre 2026

## Smoke HTTP et base de test

Le script [`tests/api_http_smoke.py`](../../../tests/api_http_smoke.py) a été exécuté contre l'API ASP.NET sur `127.0.0.1`, PostgreSQL `palimpseste_test` et le stockage local. Résultat observé : `passed`, **16 contrôles sur 16**, `cleanup: completed`. Il envoie un vrai multipart avec PNG RGBA, journal gzip chaîné et empreintes de pixels. Les contrôles comprennent l'authentification, les capacités et la référence, l'allocation, `begin`, le chargement de capture, la lecture du job, l'idempotence et ses conflits `409`, ainsi que le rejet `422` d'une composition dessin/encre/référence divergente.

Après ce passage et le nettoyage prévu par le script, une requête SQL sur `palimpseste_test` a retourné `jobs|captures|parchments|artifacts = 0|0|0|1`. L'unique artefact restant est la référence installée. Le stockage `.local-artifacts` contient 19 fichiers à ce moment, dont des fichiers orphelins issus de passages de développement antérieurs ; le smoke récent n'en a pas ajouté après nettoyage. Le ramasse-miettes fourni applique un délai de grâce et doit être exécuté séparément.

Un parcours Unity séparé avec dessin à la souris a obtenu les capacités et l'image de référence (`200`), alloué un parchemin, transmis `begin`, envoyé la capture et reçu un job `queued` (`df5b9540775d4ba489f2524c307a8a21`). La première tentative avait été refusée `422` car le client transmettait une mauvaise empreinte du premier événement ; le client a été corrigé et une nouvelle capture a été acceptée. Ce parcours s'arrête **avant** un appel Luna ou une compilation de sort.

## Sauvegarde et restauration à blanc

Les scripts [`ops/backup-lab.ps1`](../../../ops/backup-lab.ps1) et [`ops/restore-lab.ps1`](../../../ops/restore-lab.ps1) ont été exécutés avec PostgreSQL 17.11. La sauvegarde source provenait de `palimpseste_backup_source`, qui contenait une capture synthétique et un job en attente. La restauration a créé la base distincte et initialement absente `palimpseste_restore_test` et un stockage privé vide. Les empreintes du dump, de chaque fichier sauvegardé et des quatre artefacts référencés par la base restaurée ont été vérifiées. Une API connectée à cette restauration a ensuite lu le job en attente et servi la référence PNG (`200`, empreinte conforme).

| Preuve observée | Valeur |
|---|---|
| Sauvegarde créée (UTC) | `2026-09-19T17:37:13.0342617Z` |
| SHA-256 du dump | `76d984030ef90bddd402631dbc5173f1451872075b3417c402bfc186f7536c12` |
| Fichiers dans la sauvegarde | 10, dont 4 référencés par la base |
| Rapport de restauration (UTC) | `2026-09-19T17:37:22.3583092Z`, `result: passed` |
| Lignes restaurées | `parchments|captures|jobs|spells = 1|1|1|0` |
| Artefacts référencés vérifiés | 4 sur 4 |

Le dossier privé de reprise du **vrai dessin Unity** est `C:\ProgramData\Palimpseste\backups\unity-capture-20260919`. Il contient le dump, les fichiers et le manifeste daté `2026-09-19T17:38:35.9955342Z`, SHA-256 du dump `4b92736c507ded9315faa54c7037b66e315917bc973fc3f621b1b69a6064c58d`. Le manifeste liste 22 fichiers, y compris des orphelins de développement. Ce dossier n'est pas versionné. L'accès à la base de test utilise le fichier privé `C:\ProgramData\Palimpseste\test-db.env` ; aucune valeur de connexion ni jeton n'est publiée ici.

## Point de reprise de la recette A/B

La capture Unity acceptée est conservée dans cette sauvegarde avant son nettoyage de la base `palimpseste_test`. Pour reprendre une recette, restaurer dans **une nouvelle base et un stockage vide**, provisionner un worker avec un profil Codex dédié, vérifier l'authentification et le niveau d'effort réellement acceptés, puis traiter le job conservé. Conserver les artefacts A/B, leurs empreintes, les états de tentative et le sort compilé. Une reprise directe du job peut dépendre de l'état de bail et de la configuration worker ; aucun traitement Luna ni sort jouable n'est attesté par ce rapport.
