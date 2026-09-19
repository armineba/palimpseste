# Doctor fournisseur et liaison au binaire — 19 septembre 2026

## Doctor local sous le compte de service

Le `ProviderDoctor.exe` reconstruit a été lancé par un processus Windows dont
l'identité observée est `PalRuntimeSvc`. Il a terminé avec le code 0, sans
requête modèle. `codex --version`, `exec --help`, le parseur des options de
désactivation, `features list` et `login status` ont répondu. Le CLI observé
est `codex-cli 0.154.0-alpha.6.2` ; son SHA-256 est :

`2271526227b06ca13ab2b975b88546460fc61b2a29225b6dda0fdc803024ccc9`

Le modèle demandé est `gpt-5.6-luna` et l'effort demandé est `max`. Les
capacités exposées exigées par la porte runtime ont été observées à `false`,
dont shell, ordinateur, navigateur, applications, plugins, agents, recherche
réseau et vue d'image. `unified_exec=true` est conservé comme observation du
mécanisme interne PTY ; cette observation n'établit pas à elle seule l'absence
d'outil utilisable par un modèle. La clé `web_search` est liée uniquement à la
ligne observée `standalone_web_search=false`, et cette correspondance est
consignée dans le JSON privé.

Le premier JSON local, revu puis conservé immuable hors Git comme preuve
features, a le SHA-256 :

`32341ca51c9bc7c2db959f81700a939ecf184cdbe3a765a5947da61f346b2f8f`

La configuration runtime privée pointe vers ce fichier et ce hash. Une seconde
exécution du doctor, sous la même identité et sur le même CLI SHA-256, a le
SHA-256 `30c85771fb9237c75908e234210c5c0a61fe1b90f6690039822455705dc6edc1`.
Elle indique `runtime_features_compatibility_verified=true`, aucune erreur
locale et `production_issues=["effort_not_verified"]`. Son statut d'authentification
est `not_authenticated` et `model_calls_executed=false`. Le
[résumé expurgé](provider-local-2026-09-19.json) reprend ces valeurs.

La preuve locale antérieure `c50647f63efda97ea989374bef7b6b3a6e3bdce6e00d0446758be6db99968b51`
ne contenait pas le SHA-256 du binaire. Elle est historique et ne satisfait
plus la nouvelle porte `Check(true)`.

## Doctor actif

Le doctor actif antérieur a été invoqué sous `PalRuntimeSvc` avec les entrées
préparées, puis arrêté avant A/B par `dedicated_auth_not_confirmed` ;
`model_calls_executed=false`. Son ancien JSON est conservé comme historique
dans [le résumé de blocage](provider-active-blocked-2026-09-19.json). Aucun
doctor actif avec liaison SHA-256 du CLI et aucun appel Luna A/B n'ont été
exécutés. L'acceptation du modèle et de l'effort `max` par le compte dédié
reste à établir. Le worker refuse la génération tant que la preuve active
hashée et `PALIMPSESTE_EFFORT_VERIFIED=true` ne sont pas présents.

Le secret du compte Windows utilisé pour relancer le doctor local est conservé
hors dépôt dans une `PSCredential` protégée par DPAPI et une ACL privée. Il
dépend du profil de l'opérateur Windows actuel. Aucun secret ni chemin du
fichier de credential ne figure dans cette preuve.
