# Installation et reprise du laboratoire Palimpseste

Ce document décrit le laboratoire Windows privé effectivement utilisé le 19 septembre 2026. L'état de chaque porte et les preuves se trouvent dans [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md). L'authentification du compte Codex de service et la recette Luna A/B ne sont pas terminées.

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

Pour le client, ouvrir `game/` dans l'éditeur ci-dessus. L'assemblage partagé `shared/Palimpseste.Contracts` est référencé par chemin relatif : conserver la structure du dépôt. Les commandes de tests EditMode, PlayMode et de build vérifiées sont dans [UNITY_TEST_PROOF.md](UNITY_TEST_PROOF.md). Le build produit dans `game/Build/` est archivé dans `deliverables/Palimpseste-Windows-x64-IL2CPP.zip` par le script d'éditeur.

Pour publier l'API, le worker et le doctor en exécutables Windows autonomes :

```powershell
.\ops\package-backend.ps1
```

## API privée et stockage

Créer une base PostgreSQL dédiée. Appliquer `backend/migrations/001_initial.sql` avec un administrateur avant le lancement. Après extraction de l'archive et définition de `DATABASE_URL`, `api/Palimpseste.Api.exe token-create player mon-labo` crée un jeton joueur ; remplacer `player` par `creator` pour le rôle d'écriture et de revue humaine. La commande affiche le secret une seule fois sur la console : le conserver dans un coffre privé. Garder la chaîne `DATABASE_URL`, les jetons et les dumps hors du dépôt et hors des archives distribuées.

Extraire le ZIP backend dans un dossier réservé à l'opérateur, inaccessible au compte worker. Copier les exécutables publiés `api`, `worker` et `doctor` vers leurs emplacements de service selon les ACL décrites dans [ops/README.md](../ops/README.md) ; ne pas lancer le worker depuis le dossier extrait contenant `ops`. Définir `DATABASE_URL`, `ARTIFACT_ROOT` et `PALIMPSESTE_SPEC_ROOT` (copie runtime du dossier `spec`), puis lancer l'API publiée. `GET /health/ready` doit répondre 200 lorsque la base et les ressources sont accessibles. L'API exige HTTPS hors de localhost ; le laboratoire local a été testé sur loopback. Le worker est un processus séparé de l'API. Les parchemins soumis par les joueurs ne donnent accès à aucune commande système, aucun fichier du serveur ni aucune compilation C#.

## Worker Luna/Codex

Lire [ops/README.md](../ops/README.md) et [07_RUNTIME_ISOLATION.md](07_RUNTIME_ISOLATION.md). Le profil worker utilise un compte Windows non administrateur, un `CODEX_HOME` dédié, un dépôt de développement refusé par ACL, une spécification déployée en lecture seule et des répertoires de tentatives privés. `ops/provision-runtime.ps1` prépare ce profil sans copier l'authentification personnelle. Le worker doit tourner sous l'identité de service configurée et être lancé depuis la copie du binaire publié dans `runtime/bin`, hors du dépôt et du dossier opérateur extrait.

Le doctor local vérifie les options et les chemins sans appel modèle. Ses observations `features list`, exécutées sous `PalRuntimeSvc`, ont été revues, hashées et utilisées pour configurer `PALIMPSESTE_RUNTIME_FEATURES_VERIFIED=true`. Le doctor actif doit exécuter A et B avec des entrées 1024 × 1024 autorisées, constater le modèle et l'effort retournés et écrire une preuve. Le worker refuse encore de démarrer sans cette preuve active hashée et `PALIMPSESTE_EFFORT_VERIFIED=true`. Cette porte n'a pas été franchie. La présence des options CLI seule ne vaut pas preuve d'efficacité des restrictions d'outils ou d'acceptation du modèle.

## Sauvegarde, restauration et exploitation

`ops/README.md` contient les commandes de sauvegarde et restauration, l'inspection des tâches et le ramassage prudent des artefacts. Arrêter les écritures avant une sauvegarde cohérente. Une restauration sur une base vide distincte et un stockage vide a été vérifiée pour le laboratoire ; voir [api-restauration-2026-09-19.md](../evidence/public/backend/api-restauration-2026-09-19.md).
