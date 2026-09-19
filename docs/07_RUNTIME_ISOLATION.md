# Isolation du worker de génération

Le worker joueur et l'agent de développement utilisent des identités, des
répertoires et des fonctions distincts. L'API ne transmet au worker que des
données de capture et des identifiants de tâche. `CodexProcessRunner` construit
les arguments de `codex exec` sans shell interactif ; les chemins d'image et de
schéma sont limités aux racines approuvées et les jonctions NTFS sont refusées.
Chaque tentative reçoit un dossier neuf sous `PALIMPSESTE_ATTEMPT_ROOT`.

Le processus enfant reçoit un environnement reconstruit, un `CODEX_HOME`
dédié, un `TEMP` de tentative et un `PATH` minimal. Il utilise
`--ignore-user-config`, `--ignore-rules`, `--sandbox read-only`, `--ephemeral`,
`approval_policy="never"` et les désactivations d'outils demandées. Le worker
refuse un modèle différent de `gpt-5.6-luna`, un effort inférieur à `max`
(`effort_below_documented_max`), le repli de modèle, une baisse d'effort et
l'activation des outils d'exécution.

La copie runtime de `spec` est en lecture seule pour le compte de service. Le
dépôt de développement porte un refus héritable de lecture, exécution, création,
écriture, suppression et modification d'ACL pour ce compte, tout en laissant
`READ_CONTROL` disponible pour l'audit. Avant chaque lancement, la porte
production exige cette ACE complète sur la racine, un refus effectif de lister
la racine et un refus d'ouvrir en lecture **et** écriture quatre fichiers
sources connus. Ces ouvertures n'écrivent aucun octet. Le contrôle effectif a
été exécuté sous `PalRuntimeSvc` : liste, lecture et écriture ont été refusées.
Il couvre la racine et ces sentinelles ; il ne constitue pas une énumération
exhaustive de chaque enfant ni une protection contre un administrateur qui
modifierait l'ACL après le contrôle.

`ProviderDoctor local` inspecte le binaire sans appel modèle : version, aide,
parseur des options, `features list` et statut d'authentification expurgé.
Les capacités exposées demandées sont observées à `false` ; `unified_exec`
reste signalé comme mécanisme interne PTY et n'est pas compté comme outil
exposé. La preuve locale inclut le SHA-256 du binaire Codex réellement inspecté.
`CodexSettings.Check(true)` recalcule ce SHA-256 et exige le même dans la preuve
features hashée. Il applique la même liaison à la preuve du doctor actif pour
l'effort, avec le modèle, l'effort retourné aux étapes A/B et l'identité de
service. Une preuve produite pour un autre binaire est rejetée.

Le nouveau doctor local a réussi sous `PalRuntimeSvc`, sans appel modèle.
Après mise à jour de la preuve features dans la configuration privée, sa seconde
exécution ne rapporte que `effort_not_verified` comme blocage de production.
L'authentification Codex du compte dédié reste absente. Le nouveau doctor a
été invoqué en mode actif avec les entrées préparées et le même SHA-256 du
CLI ; il s'est arrêté avant A/B avec `dedicated_auth_not_confirmed`, sans
appel modèle. Sa preuve de blocage ne satisfait pas la porte de compatibilité
de l'effort, qui exige A et B réussis. L'ancienne preuve active reste
historique. Les hashes et limites figurent dans
[la preuve publique](../evidence/public/backend/provider-doctor-runtime-2026-09-19.md).

Un identifiant fort du compte Windows a été renouvelé pour permettre la sonde
locale. La `PSCredential` est protégée par DPAPI et par une ACL privée hors du
dépôt ; elle dépend du profil Windows de l'opérateur courant. Aucun mot de
passe, jeton fournisseur ou fichier d'authentification n'est publié.
