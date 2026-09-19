# Isolation observée du worker — 19 septembre 2026

`PalRuntimeSvc` est un compte Windows local non administrateur, séparé du
profil de développement. La copie `spec` du runtime lui donne lecture et
exécution, et ne contient pas `prompts/00_AGENT_BUILD.md`. Le dépôt de
développement possède une ACE de refus héritable pour ce compte : lecture,
exécution, création, écriture, suppression et changement d'ACL/propriétaire.
L'ancienne ACE de lecture seule subsiste en plus sur la racine ; elle ne réduit
pas le nouveau refus. La nouvelle ACE a été ajoutée manuellement sans relancer
le provisionneur, ce qui explique les deux lignes visibles dans `icacls`.

Une sonde exécutée sous `PalRuntimeSvc` a obtenu `UnauthorizedAccessException`
pour l'énumération de la racine, la lecture et l'ouverture en écriture de
`prompts/00_AGENT_BUILD.md`. Le code de préflight vérifie en plus l'ACE
héritable complète et tente les mêmes ouvertures sur quatre fichiers sources
connus avant tout `codex exec`. La sonde effective ne couvre pas chaque fichier
enfant ni une modification ultérieure par un administrateur ; ces limites
restent des conditions d'exploitation.

La première tentative d'exécuter la sonde via une tâche S4U a échoué à
l'enregistrement (`0x80070005`), même après ajout temporaire du droit de
connexion par lot. La valeur exacte de ce droit a été restaurée et aucune
tâche résiduelle ne reste. Un identifiant Windows fort renouvelé, conservé dans
une `PSCredential` DPAPI privée et liée au profil opérateur, a permis le
lancement direct du processus sous le compte dédié. Aucun mot de passe ni
fichier d'authentification Codex n'a été copié dans le dépôt.

Le [doctor local renouvelé](provider-doctor-runtime-2026-09-19.md) a réussi sous
le compte de service avec le CLI SHA-256
`2271526227b06ca13ab2b975b88546460fc61b2a29225b6dda0fdc803024ccc9`.
Après configuration de la nouvelle preuve features hashée, son seul blocage
de production est `effort_not_verified`. L'authentification Codex dédiée est
absente ; aucun appel Luna n'a été exécuté. Le doctor actif antérieur est
historique, arrêté avant A/B avec `dedicated_auth_not_confirmed`.
