# Passation du projet — D16 / Player 1.6.0

Passation actualisée pour D16, pour reprendre le développement sur un autre poste. Commencer par les premières sections de [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) et [NEXT_ACTIONS.md](NEXT_ACTIONS.md), puis [le prompt de développement](../prompts/00_AGENT_BUILD.md), [l'avenant fournisseur](05_OVERRIDE_LUNA_CODEX.md), [l'intégration](03_INTEGRATION_FOURNISSEUR.md) et [les décisions](DECISIONS.md).

**Attention aux sections historiques :** certaines parties de [SETUP.md](SETUP.md) décrivent des étapes antérieures. Pour l'état courant, consulter les sections D16, les index de livraison et leurs preuves datées. Les chemins, PID, comptes et preuves du poste d'origine ne sont pas des réglages transférables au nouveau poste.

## Ce qui est livré et ce qui reste à juger

La chaîne actuelle est **dessin libre → description A → atlas natif G → planche serveur 3 × 7 → recherche de références/ressources → construction B → compilation contrôlée → rendu et critique J → sort dans le laboratoire Unity**. Le dessin accepte plusieurs traits et se valide avec **Dessin terminé**. Les anciennes sauvegardes restent utilisables sans régénération automatique.

| Étape | Modèle et rôle réellement configurés |
| --- | --- |
| A | `gpt-5.6-sol`, effort `high`, description du dessin et comportement. |
| G | `gpt-6-astra`, effort `high`, atlas natif de 21 scènes depuis la description. |
| B | `gpt-6-astra`, effort `high`, construction depuis la description, la planche et les ressources. |
| J | `gpt-6-astra`, effort `high`, critique indépendante des captures de présentation. |

Les noms historiques « Luna » et « Astra » dans des classes ou anciens documents ne changent pas cette configuration. Le catalogue contient **141 recettes construites sur 24 effets primitifs** : [EFFECT_LIBRARY.md](EFFECT_LIBRARY.md). Les [16 textures CC0 et le bruit MIT](references-vfx-sources.md) alimentent le renderer. La planche présente APPARITION / STABLE / DISPARITION en sept cases par ligne ; l'atlas natif et la planche composée ont des artefacts/SHA distincts. Voir [D16](D16_ANIMATION_SHEET.md) et [le protocole Unity](UNITY_ANIMATION_SHEET_D16.md).

Le [manifeste Player courant](../evidence/public/unity/lifecycle-delivery.json) et l'[index backend](../evidence/public/backend/lifecycle-delivery.json) identifient les archives effectives. Le créateur veut essayer lui-même : un build ne prouve ni la fidélité graphique ni le gameplay. Aucun nouveau test ou appel modèle n'est revendiqué par cette passation.

### Résultats historiques D13

Le build Windows IL2CPP **1.3.0**, le déploiement local et la santé API ont été constatés sur le poste d'origine. CoreSmoke, DbSmoke et quatre contrôles Unity du validateur ont réussi. Une capture Unity du vrai paquet a vérifié l'impact, les dégâts, l'impulsion et le nettoyage. Le dernier réglage du verre a été inclus au build après cette capture, sans nouvelle capture.

Les sondes fournisseur ont exécuté A, G, un B rejeté puis un B corrigé et compilé. G utilisait une description figée antérieure : **ces sondes ne prouvent pas un nouveau parcours joueur D13 complet**. Restent à observer ou accepter : ce parcours, sa relecture hors ligne, la qualité artistique et sonore, la fidélité à l'image, la recette des 30 dessins, un second créateur, un autre PC et l'accès HTTPS/multijoueur distant. La fidélité « 1 pour 1 » n'est pas acquise.

Preuves : [fournisseur](../evidence/public/backend/image-reference-2026-09-20.json), [déploiement](../evidence/public/backend/image-reference-deployment-2026-09-20.json), [Unity et limites](../evidence/public/unity/image-reference-2026-09-20.md), [manifeste Player](../evidence/public/unity/image-reference-delivery.json).

## Où travailler

| Dossier | Contenu |
| --- | --- |
| `game/` | Projet Unity ; ouvrir ce dossier dans Unity Hub |
| `game/Assets/Palimpseste/SpellRuntime/` | Exécution des sorts, rendu et animation |
| `game/Assets/Palimpseste/Resources/` | Shaders, matériaux et ressources VFX |
| `backend/` | API, worker, transport Codex, stockage et ProviderDoctor |
| `backend/migrations/` | Migrations PostgreSQL ordonnées `001` à `011` |
| `shared/Palimpseste.Contracts/`, `shared/Palimpseste.Core/` | Contrats partagés et compilateur contrôlé |
| `contracts/`, `reference/`, `prompts/`, `assets/sourced-vfx/` | Schémas, recettes, ressources et prompts A/G/B/J |
| `ops/`, `tests/`, `qa/`, `evidence/public/` | Exploitation, contrôles et preuves conservées |

Conserver toute la structure du dépôt : Unity référence le code partagé par chemin relatif. Les caches Unity, les dossiers `bin/obj` et `game/Build/` sont ignorés par Git. Une archive distribuée doit être vérifiée contre son manifeste ; sa présence locale ne garantit pas qu'elle accompagne le clone.

## Ouvrir et construire

Installer **Unity 6000.3.24f1**, modèle **Universal 3D / URP**, avec le module Windows IL2CPP et ses prérequis de compilation. Garder les versions des paquets verrouillées dans `game/Packages/`. Le poste d'origine utilise .NET SDK **10.0.401** et PostgreSQL **17.11** ; voir les détails d'environnement dans [SETUP.md](SETUP.md).

Dans Unity Hub, ajouter `game/`, puis ouvrir `Assets/Palimpseste/Scenes/Bootstrap.unity`. Le menu **Palimpseste → Construire Windows x64 IL2CPP** appelle la vraie méthode de build. Pour produire aussi le journal attendu par le script de packaging, depuis la racine du dépôt :

```powershell
$repo = (Get-Location).Path
$unityEditor = 'C:\ProgramData\6000.3.24f1-x86_64\Editor\Unity.exe' # adapter au poste
$env:PALIMPSESTE_BUILD_DIR = Join-Path $repo 'game\Build\WindowsAnimationSheetRelease'
New-Item -ItemType Directory -Force -Path (Join-Path $repo 'game\Logs') | Out-Null
$unityArgs = @(
    '-batchmode', '-quit',
    '-projectPath', ('"' + (Join-Path $repo 'game') + '"'),
    '-executeMethod', 'Palimpseste.Game.Editor.BuildPalimpseste.BuildWindows',
    '-logFile', ('"' + (Join-Path $repo 'game\Logs\animation-sheet-build.log') + '"')
)
$buildProcess = Start-Process -FilePath $unityEditor -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
$buildProcess.WaitForExit()
if ($buildProcess.ExitCode -ne 0) { throw 'Build Unity échoué ; consulter le journal.' }
.\ops\package-lifecycle-player.ps1 -UpdateDesktopShortcut
```

Fermer l'éditeur de ce projet avant le build en ligne de commande. Le packaging vérifie la réussite, la fraîcheur et les fichiers, sauvegarde la livraison précédente et n'ouvre jamais le jeu. Sorties : `game/Build/WindowsAnimationSheetPlayable/` et `deliverables/Palimpseste-Windows-x64-IL2CPP.zip`.

Pour compiler le backend et produire son archive :

```powershell
dotnet build Palimpseste.sln -c Release -v:q
.\ops\package-backend.ps1
```

Sans paramètres, ce packaging **n'inclut pas le binaire Codex natif durci**. L'inclusion exige `-CodexExecutable` et son `-CodexSha256` vérifié. Les sources amont, les deux correctifs, l'ordre d'application et la compilation sont décrits dans [ops/codex-image-generation.md](../ops/codex-image-generation.md) et [ops/codex-attestation.md](../ops/codex-attestation.md). Un CLI Codex standard ne remplace pas automatiquement ce binaire.

## Remettre le service en place sur un nouveau poste

Le clone ne contient ni authentification Codex, ni identifiants PostgreSQL, ni jetons joueur, ni base de production, ni bibliothèque privée de sorts. **Ne pas transmettre `auth.json`, un profil Codex connecté ou les fichiers privés du poste d'origine.** L'opérateur doit configurer son propre environnement et une authentification autorisée sous une identité de service dédiée.

1. Lire [ops/README.md](../ops/README.md) et [07_RUNTIME_ISOLATION.md](07_RUNTIME_ISOLATION.md). Préparer un runtime hors du dépôt, un compte non administrateur et des ACL séparant développement, exploitation et génération joueur. `ops/provision-runtime.ps1` est le provisionneur existant.
2. Créer une base PostgreSQL dédiée, appliquer **toutes les migrations `001` à `011` dans l'ordre**, puis configurer les secrets hors dépôt. Le script de mise à jour D16 exige le schéma `010` et applique seulement `011` ; ne pas rejouer les anciennes contraintes `009`/`010` sur des jobs plus récents.
3. Publier API/worker/diagnostics et déployer contrats, références, prompts A/G/B/J, historiques A 2.3/G 1.2 et `assets/sourced-vfx`. Vérifier les copies installées : le provisionneur historique ne remplace pas toutes les étapes D16. Le worker ne reçoit jamais le prompt de développement ni le dépôt. Le renderer doit être une copie protégée du Player 1.6.0 avec manifeste SHA complet.
4. Installer le CLI durci pour A/B/G/J, authentifier le compte de service et établir les preuves locales puis actives avec ProviderDoctor. Les diagnostics actifs consomment l'usage du compte ; le diagnostic local ne fait aucun appel modèle. Les attestations du poste d'origine ne valident pas une nouvelle installation.
5. Adapter et revoir les lanceurs opérateur avant utilisation : `ops/start-owner-api.ps1`, `ops/start-owner-worker.ps1` et `ops/deploy-lifecycle.ps1` verrouillent des chemins, identités et empreintes du laboratoire d'origine. Ce sont des scripts réels mais **pas un installateur portable prêt à lancer**. Conserver leurs contrôles et établir les valeurs correspondant au nouveau poste.
6. Démarrer les services configurés, contrôler `/health/ready`, puis créer un accès joueur avec les scripts d'invitation. Pour tester depuis un autre PC, configurer l'URL HTTPS et l'admission : `127.0.0.1` désigne toujours le PC du joueur, pas le serveur d'origine.

La passerelle utilise **`codex exec` avec connexion ChatGPT**, sans clé API. Le worker produit uniquement des données bornées ; il ne peut pas lancer de build, modifier du code ou exécuter du C# issu des dessins. Les réglages d'abonnement/recharge restent ceux du compte de l'opérateur ; le logiciel ne les active pas.

## Première reprise conseillée

Après installation effective, suivre [TESTER_MAINTENANT.md](TESTER_MAINTENANT.md) : **Dessiner un parchemin → Dessin terminé → description → planche → sort → Lancer dans le laboratoire**. Recueillir le retour visuel du créateur, qui souhaite essayer lui-même. Noter chaque correction et sa preuve dans les documents d'état ; ne pas transformer les preuves locales existantes en succès sur le nouveau poste.
