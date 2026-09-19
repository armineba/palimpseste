# Preuve dâ€™isolation runtime

Le worker joueur et lâ€™agent de dÃ©veloppement sont deux profils distincts.
`CodexProcessRunner` reÃ§oit uniquement un prompt et des chemins produits par le
backend; aucun champ HTTP ne devient un argument de processus. Les chemins de
schÃ©ma et dâ€™image doivent Ãªtre sous les racines approuvÃ©es, sans point de
jonction/reparse point. Chaque tentative reÃ§oit un dossier neuf sous
`PALIMPSESTE_ATTEMPT_ROOT`.

Avant lancement, la configuration bloque :

- un `CODEX_HOME` personnel ou inclus dans le profil utilisateur courant;
- une racine runtime placÃ©e dans lâ€™arbre de dÃ©veloppement;
- une spÃ©cification restÃ©e dans lâ€™arbre de dÃ©veloppement ou modifiable par le
  compte de service;
- un arbre de dÃ©veloppement auquel le compte de service conserve la lecture;
- lâ€™hÃ©ritage dâ€™un outil dâ€™exÃ©cution, dâ€™un fallback de modÃ¨le ou dâ€™une baisse de
  raisonnement;
- une identitÃ© Windows diffÃ©rente du compte de service;
- un modÃ¨le autre que `gpt-5.6-luna` ou un effort non documentÃ©;
- un schÃ©ma hors de la spÃ©cification dÃ©ployÃ©e et une image hors de lâ€™espace
  dâ€™entrÃ©es contrÃ´lÃ©.

Lâ€™environnement du processus est vidÃ© puis reconstruit avec le `CODEX_HOME`
dÃ©diÃ©, le rÃ©pertoire de tentative comme `TEMP`, un `PATH` minimal et les
contrÃ´les Git sans fichier utilisateur. Les options `--ignore-user-config`,
`--ignore-rules`, `--sandbox read-only`, `--ephemeral`, `--config approval_policy="never"`
et les dÃ©sactivations dâ€™outils sont passÃ©es sans shell interactif. Le compte de
service et les ACL du rÃ©pertoire runtime restent une condition de dÃ©ploiement;
`read-only` seul ne constitue pas une preuve de confidentialitÃ©. Le
provisionnement applique RX Ã  la copie `spec` et au binaire Codex, puis un deny
explicite au compte de service sur lâ€™arbre de dÃ©veloppement.

La commande `ProviderDoctor local` nâ€™appelle jamais le modÃ¨le. Elle conserve la
version, lâ€™aide de `exec`, les options prÃ©sentes, lâ€™Ã©tat dâ€™authentification
redactÃ© et les erreurs de configuration. `ProviderDoctor active` doit Ãªtre
lancÃ©e volontairement avec deux images et un JSON de gÃ©omÃ©trie; elle effectue
A puis B et produit des preuves datees. Dans cette livraison, le doctor actif
a ete lance sous PalRuntimeSvc mais a ete bloque avant A/B par
`dedicated_auth_not_confirmed`; `model_calls_executed=false`.
Le profil local a observe les capacites exposees desactivees et le seul
blocage de production restant est `effort_not_verified`. En production, le
booleen `PALIMPSESTE_EFFORT_VERIFIED` doit etre accompagne du chemin et du
SHA-256 de sa preuve active; le worker revalide le JSON A/B, le modele,
l'effort rapporte et l'identite du service avant de lancer un sort.

## Observation des capacites du CLI

Le doctor local execute aussi `codex --disable ... features list` sans modele.
Il compare les capacites exposees (`shell_tool`, navigateur, ordinateur,
applications, plugins, agents, `view_image` et autres entrees de la liste) a
`false` et conserve l observation complete dans le JSON. `unified_exec` et
`unified_exec_tty` sont enregistres comme implementation PTY observee; ils ne
sont pas comptes comme une preuve d outil expose desactive. Une valeur `true`
pour une capacite exposee, une ligne manquante ou un parseur non confirme fait
echouer le preflight. La cle legacy `web_search` est liee seulement a la ligne
explicitement observee `standalone_web_search=false`; cette correspondance est
conservee dans le JSON hashable.

Pour ouvrir la porte features, l operateur examine puis hash le JSON local
produit sous le compte de service. Il renseigne le chemin et le SHA-256 dans
`PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_PATH` et
`PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_SHA256`, puis
`PALIMPSESTE_RUNTIME_FEATURES_VERIFIED=true`. `CodexSettings.Check(true)`
recalcule le hash, verifie le compte, le modele, l effort et chaque valeur de
la table avant d autoriser le worker; un booleen isole ne suffit pas.
