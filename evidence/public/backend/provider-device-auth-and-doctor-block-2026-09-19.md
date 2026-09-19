# Authentification dédiée et première passe active du doctor

Contrôle effectué le 19 septembre 2026 sous `PalRuntimeSvc`, avec le `CODEX_HOME` isolé du runtime : `codex login status` a quitté avec le code 0 et indiqué une connexion ChatGPT. Le statut ne donne pas le détail du flux, mais cette connexion a été établie par `codex login --device-auth`. Les variables héritées `OPENAI_API_KEY`, `CODEX_API_KEY`, `CODEX_ACCESS_TOKEN` et `CHATGPT_TOKEN` étaient absentes. Aucun identifiant ni secret d'authentification n'est conservé dans cette preuve publique.

Le doctor local a réussi : modèle demandé `gpt-5.6-luna`, effort demandé `max`, authentification dédiée confirmée. Sa seule réserve était `effort_not_verified`, à résoudre par le doctor actif.

Une première invocation du doctor actif a échoué à l'étape A avec `IsolationViolation` / `effort_evidence_missing`, avant la création d'un répertoire de tentative et avant le lancement de `codex exec`. L'étape B n'a pas été tentée. Le champ `model_calls_executed: true` dans son JSON est un **faux positif** : il est positionné après le retour de la méthode fournisseur même quand celle-ci a refusé la tentative avant lancement. L'échec est dû à une dépendance circulaire : la vérification de production demande déjà le rapport actif que cette invocation doit créer. Aucun appel Luna réel n'est prouvé par cette passe. Il faut employer la voie de vérification `ProbeAsync` pour le doctor actif, en gardant le contrôle strict de production du worker.

Empreinte SHA-256 du rapport privé de cette première passe : `f4102addcd740cda609c399395ca551a0bf483b98dda07e9f0f41cc91f47dd0c`. La preuve brute et le statut d'authentification sont conservés hors dépôt dans `E:\PalimpsesteRuntime\evidence`.

## Passe avec le doctor corrigé

Le doctor corrigé (`ProviderDoctor.exe`, SHA-256 `c84e841406d1857604fa8ef780200a8d6aa54d5168b1ae1a0aaab773a1a3f0b1`) a été lancé une seule fois le 19 septembre 2026 à 21:57 UTC sous `PalRuntimeSvc`, avec la même référence, le vrai dessin Unity et la géométrie privée. Cette fois, `process_started: true` à l'étape A : le processus Codex a réellement démarré avec `gpt-5.6-luna` et l'effort demandé `max`. Il s'est terminé presque immédiatement avec `ProcessFailure` / `codex_exit_nonzero`. L'étape B n'a pas été lancée. Aucun modèle ni effort effectif, usage ou JSON final n'a été rapporté, donc **aucun succès Luna A/B n'est établi**. Le code de sortie exact et la sortie d'erreur ne figurent pas dans le rapport doctor : le runner les lit en mémoire, puis ne les conserve pas pour cette issue.

Les options CLI employées figurent dans `codex exec --help`. Sous l'environnement réduit identique à celui du runner, `codex login status` quitte avec le code 0 et indique ChatGPT, et `codex exec --help` quitte avec le code 0. Ces contrôles locaux ne déterminent pas la cause de `codex_exit_nonzero` ; aucun second appel génératif n'a été tenté. SHA-256 du rapport privé de cette passe : `2de534b3f840acc75c98a848a2be13b3d796346e1e2be689f85b9cf423d2881c`.

## Passe diagnostique du 20 septembre

Après ajout d'une télémétrie privée bornée au doctor (`ProviderDoctor.exe`, SHA-256 `6c7fe6d35b3dc451e380588ce65f0872625e1381807b3454297ddd25890dbd01`), une nouvelle passe unique a été exécutée sur les mêmes entrées. L'étape A a lancé Codex (`process_started: true`) avec `gpt-5.6-luna` et effort demandé `max`, puis Codex a quitté avec le **code 1**. Résultat : `ProcessFailure` / `codex_exit_nonzero`; aucun modèle ou effort effectif, usage, ou JSON final rapporté. B n'a pas été tenté.

La sortie d'erreur privée ne contient qu'une indication informative de lecture du prompt depuis l'entrée standard. Son SHA-256 est `3e75d28a6681c31400a3f0fcb564c7613fd42796fb83294e4d53fea86bcbd401`. Elle n'explique pas le code 1. Le flux d'événements stdout et son message d'erreur éventuel ne sont pas conservés par cette version du runner. Aucun autre appel n'a suivi. Rapport doctor privé SHA-256 : `1b5cafe9451dfcc8be9838966d8453880bf917754cbfedd31424159bd1447bb2`.

## Passe diagnostique avec conservation privée du JSONL

Un doctor doté d'une capture privée bornée du JSONL (`ProviderDoctor.exe`, SHA-256 `9bff9c8ddb72307821aeb3311fac4246eafb8bf50c12cb5ba491308a660846e4`) a été exécuté une seule fois sur les mêmes entrées. Codex a démarré pour A avec `gpt-5.6-luna` et l'effort demandé `max`, puis a quitté avec le code 1; B n'a pas été lancé. Les événements privés établissent un rejet **HTTP 400 `invalid_json_schema`** du format de sortie demandé : dans `properties.schema_version` du schéma A, la clé `type` est absente. Le contrat contient une valeur `const` seule à cet emplacement. Le rejet est antérieur à une réponse du modèle; aucun modèle ou effort effectif ni usage n'est rapporté. Le JSONL et le message d'erreur bruts restent hors dépôt.

Le contrôle statique des deux contrats relève huit nœuds `const` sans `type` dans A et neuf dans B. Ce relevé n'établit pas que chaque nœud serait rejeté séparément. Aucune relance n'a été faite après ce diagnostic. SHA-256 du rapport doctor privé : `eb7ebf894d1aa79010d37284850a04dd3cf88ca4d254ba44ccdf8ab3f7004eee`.

## Passe après correction des schémas

Les schémas A et B corrigés ont été vérifiés identiques entre source et runtime, SHA-256 respectifs `c7da9399771c2fceaa95f1ea101a5dbf74dd4de0bf1a6851bc634a16594946e6` et `f61afc701d25c297514d750e8ebe2b2274806910c9ab26c38b7fb7537286c97c`. Une seule passe active du doctor a été exécutée sous le compte de service, sur les mêmes entrées privées, en demandant `gpt-5.6-luna` avec effort `max`.

À l'étape A, Codex a quitté avec le code 0 et produit un `final.json` privé, JSON valide et conforme au schéma A corrigé selon `jsonschema` Draft 2020-12 (zéro erreur). SHA-256 du fichier final : `5e938a495f7199d58c7460ebcd734b0490d099f0bac7afd93ff821d02ce5afad`. Usage rapporté : **13 662** jetons d'entrée, **3 648** jetons de sortie dont **3 106** de raisonnement; zéro jeton d'entrée en cache. Ces nombres sont les compteurs renvoyés par Codex, sans estimation de coût.

Le doctor a refusé d'accepter A avec `ModelUnavailable` / `reported_model_missing` : les champs de modèle et d'effort effectifs sont nuls dans la tentative. Le JSONL de cette sortie réussie n'a pas été conservé par le runner; aucun fichier de session locale nouveau n'a été observé. Le fichier final ne contient que les données contractuelles de description. Il est donc impossible de prouver depuis cette passe quel modèle et quel effort ont effectivement traité l'entrée. B n'a pas été lancé; aucun sort issu de Luna n'est accepté. SHA-256 du rapport doctor privé : `93173bd105ae00c84db8ff0fac3148424b2f0741971d76da22b53317a05f40f5`.
