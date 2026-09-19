# Migrations du laboratoire local, 20 septembre 2026

La base locale `palimpseste_lab` a été sauvegardée avant mutation dans le
dossier opérateur privé
`E:\Palimpseste\.runtime\operator-staging\backup-pre-migration-2026-09-20-01`.
`ops/backup-lab.ps1` a quitté avec le code 0. Le dump PostgreSQL compte
46 126 octets ; son SHA-256 recalculé égale celui du manifeste :
`1f3b92d6ab69beb49e64770cf950b4406e0403fb895749aaa013759955b44d82`.
Le manifeste contient zéro fichier d'artefact correspondant au format de
stockage, et l'ACL du dossier réserve le contrôle total à l'opérateur,
Administrateurs et SYSTEM.

`psql --no-password -v ON_ERROR_STOP=1` a appliqué successivement
`003_player_invitations.sql` (SHA-256
`74FD1FF2C0981847F94751B843388FA0361C95A430B673A229772F3825D813A6`)
et `004_generation_quota.sql` (SHA-256
`B6EB7741808F86B18C503BDD3D1084D291C68CB38315308C0917824F9ED06EC7`).
Les deux commandes ont quitté avec le code 0. Une requête de contrôle
indépendante a retourné `t|t|0` pour la présence de
`public.lab_invitations`, de `public.jobs_created_at_quota_idx` et le
nombre de jobs. Aucun serveur API ni modèle n'a été démarré pour cet essai.
