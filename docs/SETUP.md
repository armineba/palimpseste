# Installation et reprise du laboratoire Palimpseste

Ce document décrit le laboratoire Windows privé utilisé les 19–20 septembre 2026. L'état de chaque porte et les preuves se trouvent dans [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md). L'authentification ChatGPT du compte de service est terminée ; une description A réelle conforme a été produite, mais refusée faute de métadonnées modèle/effort, et la recette Luna A/B ne l'est pas.

## Versions observées

| Composant | Version ou chemin observé |
|---|---|
| Unity Editor | 6000.3.24f1, `C:\ProgramData\6000.3.24f1-x86_64\Editor\Unity.exe` |
| Modèle Unity | Universal 3D, URP 17.0.1 |
| Build Windows | x64 IL2CPP, module installé avec cet éditeur |
| .NET SDK | 10.0.401 |
| PostgreSQL | 17.11 local |
| Codex CLI | 0.154.0-alpha.6.2 observé sur cette machine |

Utiliser l'installation Unity avec suffixe `-x86_64`. L'autre chemin observé sur cette machine était incomplet. Les paquets Unity sont verrouillés dans `game/Packages/manifest.json` et `packages-lock.json`. Une licence Unity active est nécessaire à l'import et au build. Le déploiement Windows d'un lecteur IL2CPP peut aussi dépendre des composants système requis par Unity sur la machine cible ; l'installation sur une autre machine n'a pas été vérifiée.

## Vérifier et construire les sources

Depuis la racine de ce dépôt :

```powershell
python qa/validate_contracts.py
dotnet build Palimpseste.sln -c Release -v:q
dotnet run --project tests/Palimpseste.Core.Smoke -c Release
dotnet run --project tests/Palimpseste.Provider.Security -c Release
```

`tests/Palimpseste.Worker.DbSmoke` exige une base PostgreSQL dédiée et une variable `DATABASE_URL`; il modifie cette base. Les scripts et preuves de l'essai isolé sont décrits dans `evidence/public/backend/`. Le test fournisseur utilise un faux processus **uniquement pour tester le transport** et ne prouve aucun appel Luna.

Pour le client, ouvrir `game/` dans l'éditeur ci-dessus. L'assemblage partagé `shared/Palimpseste.Contracts` est référencé par chemin relatif : conserver la structure du dépôt. Les commandes de tests EditMode, PlayMode et de build vérifiées sont dans [UNITY_TEST_PROOF.md](UNITY_TEST_PROOF.md). Le dernier Player construit est dans `game/Build/WindowsPlayerFlowOwnerFinal/` ; `deliverables/Palimpseste-Windows-x64-IL2CPP.zip` est une archive antérieure à l'interface d'invitation et de description A.

Pour publier l'API, le worker et le doctor en exécutables Windows autonomes depuis les sources courantes (l'archive backend présente est historique) :

```powershell
.\ops\package-backend.ps1
```

## API privée et stockage

Créer une base PostgreSQL dédiée. Appliquer `backend/migrations/001_initial.sql` avec un administrateur avant le lancement ; sur une base ayant déjà reçu cette migration, appliquer ensuite `backend/migrations/002_review_access.sql`, `backend/migrations/003_player_invitations.sql` puis `backend/migrations/004_generation_quota.sql`. Après extraction de l'archive et définition de `DATABASE_URL`, délivrer les accès joueur par invitation à usage unique, comme indiqué ci-dessous. `api/Palimpseste.Api.exe token-create creator <libellé>` reste réservé aux comptes créateurs et diagnostics de revue ; sa sortie est un secret à conserver dans un coffre privé. Garder la chaîne `DATABASE_URL`, les jetons et les dumps hors du dépôt et hors des archives distribuées.

Extraire le ZIP backend dans un dossier réservé à l'opérateur, inaccessible au compte worker. Copier les exécutables publiés `api`, `worker` et `doctor` vers leurs emplacements de service selon les ACL décrites dans [ops/README.md](../ops/README.md) ; ne pas lancer le worker depuis le dossier extrait contenant `ops`. Définir `DATABASE_URL`, `ARTIFACT_ROOT` et `PALIMPSESTE_SPEC_ROOT` (copie runtime du dossier `spec`), puis lancer l'API publiée. `GET /health/ready` doit répondre 200 lorsque la base et les ressources sont accessibles. L'API exige HTTPS hors de localhost ; le laboratoire local a été testé sur loopback. Le worker est un processus séparé de l'API. Les parchemins soumis par les joueurs ne donnent accès à aucune commande système, aucun fichier du serveur ni aucune compilation C#.

Pour remettre un acces joueur sans embarquer de jeton dans Unity, l'operateur lance `api/Palimpseste.Api.exe invite-create <libelle>` hors du depot et remet le code `invitation_code` par un canal prive. Le client poste ensuite `{ "invitation_code": "<64 caracteres base64url>" }` a `POST /v1/session/redeem` (seule route sans bearer, HTTPS obligatoire hors loopback). La reponse 200 `{"token":"...","principal_id":"<32 hex>"}` est `Cache-Control: no-store`; le code expire apres 24 heures et ne peut etre consomme qu'une fois. Ce mecanisme ne recoit jamais de secret Codex et ne donne aucun acces au compte ou aux fichiers du worker. Le jeton obtenu reste dans le coffre de session du client et n'est pas imprime dans l'interface ni dans une archive.

Une reponse `GET /v1/jobs/{id}` contient `description_artifact_id: null` jusqu'a la persistance reussie de A. Apres cette transaction, l'identifiant public (`a` suivi de 32 chiffres hexadecimaux minuscules) reste le meme pendant `resolving_geometry`, `planning`, `validating`, `waiting_retry`, `needs_operator` et `ready`. Le proprietaire authentifie telecharge le JSON `sp.description/1.0` par `GET /v1/artifacts/{id}` ; l'API limite l'acces au proprietaire, verifie les octets contre le SHA-256 enregistre et renvoie `X-Content-SHA256`. Le Job DTO ne renvoie pas le texte brut et ne reference aucun artefact non valide.

`ops/issue-player-invitation.ps1` automatise la création de ce code et écrit un `access.json` privé hors du dépôt avec une ACL locale (compte courant, Administrateurs, SYSTEM). Le remettre par un canal privé. Le joueur peut l'ouvrir par le bouton « Ouvrir mon invitation » du jeu, ou l'installer avec `ops/install-player-access.ps1 -InvitationFile <chemin>` ; la copie opérateur reste privée. Le client conserve le jeton de session dans Windows Credential Manager ; il n'affiche ni URL, ni code, ni bearer et ne demande jamais de connexion Codex au joueur. Le seul abonnement Codex partagé reste authentifié dans le `CODEX_HOME` du worker.

## Worker Luna/Codex

Le worker utilise la connexion ChatGPT du compte de service avec `codex exec`, sans
clé API ni fonctionnalité d'achat. Les appels A/B consomment le quota Codex du compte
et peuvent consommer ses crédits **déjà présents** selon les réglages du compte.
Le serveur ne peut ni acheter des crédits ni modifier le paramètre de recharge
automatique du compte ; son titulaire doit le laisser désactivé dans l'interface
ChatGPT. Si Codex refuse un appel pour quota ou crédits épuisés, le job est arrêté
sans substitution de modèle ni répétition d'un appel déjà démarré. La capture du
créateur montre la fenêtre d'activation avec bouton désactivé, mais ne constitue
pas une lecture directe des paramètres courants du compte.

Lire [ops/README.md](../ops/README.md) et [07_RUNTIME_ISOLATION.md](07_RUNTIME_ISOLATION.md). Le profil worker utilise un compte Windows non administrateur, un `CODEX_HOME` dédié, un dépôt de développement refusé par ACL, une spécification déployée en lecture seule et des répertoires de tentatives privés. `ops/provision-runtime.ps1` prépare ce profil sans copier l'authentification personnelle. Le worker doit tourner sous l'identité de service configurée et être lancé depuis la copie du binaire publié dans `runtime/bin`, hors du dépôt et du dossier opérateur extrait.

Le doctor local vérifie les options et les chemins sans appel modèle. Ses observations `features list`, exécutées sous `PalRuntimeSvc`, ont été revues et hashées ; leur JSON inclut le SHA-256 du binaire Codex exact et configure `PALIMPSESTE_RUNTIME_FEATURES_VERIFIED=true`. Le préflight vérifie une ACE héritée complète sur la source et refuse effectivement son énumération ainsi que l'ouverture en lecture et écriture de quatre fichiers connus. L'authentification dédiée a réussi le 19 septembre 2026, puis le doctor local gratuit a réussi. Le doctor actif doit exécuter A et B avec des entrées 1024 × 1024 autorisées, constater le modèle et l'effort retournés et écrire une preuve liée au même binaire. Le worker refuse de démarrer sans cette preuve active hashée et `PALIMPSESTE_EFFORT_VERIFIED=true`. La présence des options CLI seule ne vaut pas preuve d'efficacité des restrictions d'outils ou d'acceptation du modèle.

## Sauvegarde, restauration et exploitation

`ops/README.md` contient les commandes de sauvegarde et restauration, l'inspection des tâches et le ramassage prudent des artefacts. Arrêter les écritures avant une sauvegarde cohérente. Une restauration sur une base vide distincte et un stockage vide a été vérifiée pour le laboratoire ; voir [api-restauration-2026-09-19.md](../evidence/public/backend/api-restauration-2026-09-19.md).
