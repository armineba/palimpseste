# Palimpseste — dessin vers sort dans Unity

Projet **Unity 6.3 / URP**, backend .NET/PostgreSQL et worker Codex isolé. La nouvelle pipeline V2 est : **dessin → intention → blueprint canonique → mouvement continu → contrôles structure/physique/rendu → planche échantillonnée et sort jouable**. Les sorts sont des données validées, jamais du code produit par le joueur.

## D21.3 — interruption du constructeur corrigée

Deux incohérences du schéma Codex corrigées ; backend installé le 25 septembre à 11:07 Paris. Le parchemin interrompu a repris sa construction par l’API, avec le dessin et l’historique conservés. Publication finale du sort et acceptation visuelle encore attendues. [État actuel](docs/NEXT_ACTIONS.md) · [Preuves](evidence/public/backend/schema-recovery-d21-3.json).

## D21 — UNITY GOD, construire depuis les méthodes sources

[UNITY GOD](skills/unity-god/SKILL.md) étudie la construction réelle des bibliothèques, puis impose une adaptation liée aux paramètres du sort : **20 méthodes documentées, sept disponibles dans le Player actuel**. La boucle V2 vérifie leur emploi et fournit le dossier à la critique visuelle. [Intégration et limites](docs/D21_UNITY_GOD.md) · [Preuves](evidence/public/unity-god/).

**Backend D21 installé le 25 septembre 2026 à 10:13 Paris**, migrations 012 et 013 appliquées, API et worker redémarrés. La santé API a répondu HTTP 200 `ready` à 10:15 Paris. Player 1.8.0 réutilisé. [Installation réelle](evidence/public/backend/unity-god-d21-installation.json) · [Disponibilité et ouverture non exécutée](evidence/public/backend/unity-god-d21-availability.json).

L'ouverture automatique du Player et la mise à jour du raccourci ont été refusées avant exécution par le contrôle automatique (`blocked by policy`, sans motif détaillé) ; aucun contournement effectué. Ouvrir directement [Palimpseste.exe](game/Build/WindowsBlueprintV2Playable/Palimpseste.exe), puis essayer un nouveau parchemin. Aucun nouveau sort ou test de gameplay lancé par l'agent. Les anciens sorts restent inchangés ; le rendu attend votre verdict. Le ZIP reste l'instantané de publication avant déploiement ; la preuve d'installation ci-dessus décrit l'état actuel.

## Historique — D20 / Player 1.8.0, Pipeline V2 construite

Refonte dédiée aux nouveaux sorts, huit représentations structurelles et gates A à F. Les anciens sorts conservent leurs données, assets et parcours V1. [Architecture et différences V1/V2](docs/D20_SPELL_PIPELINE_V2.md) · [État réel et limites](docs/IMPLEMENTATION_STATUS.md) · [Tests exécutés](evidence/public/v2/).

Backend et Player construits et empaquetés ; la première tentative d'installation D20 avait été annulée dans Windows UAC. **Le déploiement D21 ci-dessus installe désormais la pipeline V2 avec ce Player 1.8.0.** Les 27 contrôles V2 et la fixture Unity ont été exécutés, mais aucun nouveau parcours Codex V2 complet. La fixture n'est pas acceptée artistiquement.

## Historique — backend D19 / Player 1.7.0

**D19 installé, jeu rouvert le 24 septembre à 23:20 Paris.** Le moteur exécute quatre nouveaux profils par partie, issus de TinyPlay et Keijiro : plasma, champ de force, matière toxique et flux spectral. Les calculs et les deux textures originales sont réellement livrés dans le Player ; le constructeur B **2.6** peut les sélectionner depuis la recherche liée à la planche. [Provenance et limites](docs/D19_SOURCED_SURFACES.md) · [Compilation et livraison](evidence/public/backend/sourced-surfaces-d19.json) · [Installation](evidence/public/backend/sourced-surfaces-d19-installation.json).

Pour essayer, créer **un nouveau parchemin**. Le créateur teste lui-même : aucun essai de jeu ou de fidélité visuelle n'a été lancé par l'agent. Les bibliothèques complètes ne sont pas importées ; les autres sources gardent leur état explicite. Les livraisons précédentes ci-dessous constituent l'historique.

**D18 publié et installé le 24 septembre à 22:52 Paris :** plusieurs parchemins peuvent progresser en parallèle, sans plafond applicatif de jobs (`PALIMPSESTE_MAX_PROVIDER_CONCURRENCY=0`). Les captures Unity partagent le GPU à tour de rôle. [Fonctionnement](docs/D18_CONCURRENT_JOBS.md) · [Installation](evidence/public/backend/parallel-jobs-d18-installation.json). Aucun test ni génération de démonstration lancé ; l'essai simultané après installation reste au créateur.

**D17 publié et déployé le 24 septembre à 21:59 Paris :** la reconstruction de la planche doit examiner [cinq bibliothèques VFX obligatoires](docs/BIBLIOTHEQUES_VFX_OBLIGATOIRES.txt) avant de composer et d'itérer avec Dream-loop Pro. Prompt B **2.5** et index de recherche sont installés ; API disponible et worker démarré. [Preuve d'installation](evidence/public/backend/vfx-libraries-d17-installation.json) · [Inventaire local](reference/vfx-libraries.inventory.json). Le Player 1.6.0 a été rouvert sans modification ; les nouveaux packages ne sont pas intégrés au Player. Aucun nouveau sort, test ou capture lancé par l'agent pour cette livraison.

**Correctif backend D16.1 installé** : une critique finale au hash invalide avait bloqué « Foudre sous l’Enclume ». Le sort est récupéré depuis une version déjà évaluée, sans nouvel appel ni capture ; fidélité encore à améliorer. [Preuve et reprise](evidence/public/backend/visual-judge-fix-d16-1.json) · [Mécanisme général](docs/D16_JUDGE_RECOVERY.md).

Le **Player 1.6.0 est construit et empaqueté**, selon son [manifeste réel](evidence/public/unity/lifecycle-delivery.json). Le backend, le déploiement et le jeu ouvert sont suivis dans [IMPLEMENTATION_STATUS](docs/IMPLEMENTATION_STATUS.md) et [NEXT_ACTIONS](docs/NEXT_ACTIONS.md). Aucun nouveau parcours joueur, test ou verdict visuel réussi n'est revendiqué ici : le créateur souhaite essayer lui-même.

La référence comporte **APPARITION / STABLE / DISPARITION**, sept cases numérotées par ligne. G génère un atlas ; le compositeur serveur ajoute titres et cadres pour produire la planche **1536 × 1152**. Atlas natif et planche finale conservent des artefacts et SHA distincts. Le texte fixe mécaniques et chronologie ; B construit le VFX depuis la planche et les ressources gratuites sélectionnées. J compare ensuite les bandes temporelles du renderer précompilé. Voir [l'architecture D16](docs/D16_ANIMATION_SHEET.md), [le protocole Unity](docs/UNITY_ANIMATION_SHEET_D16.md) et [comment essayer](docs/TESTER_MAINTENANT.md).

## Historique D14 / 1.4.0 — cycle de vie et critique visuelle

Le Player et le backend D14 ont été construits et installés sur le poste d'origine : [preuve datée](evidence/public/backend/lifecycle-2026-09-20.json). Le correctif 1.4.1 a ensuite permis de reprendre un job joueur après les captures noires : [preuve de correction](evidence/public/backend/lifecycle-capture-fix-2026-09-20.json). Ces résultats restent historiques ; `lifecycle-delivery.json` indexe maintenant la livraison courante.

Les tests historiques consignés précèdent la consigne d'arrêter les essais de développement. Les captures et appels ultérieurs de la correction D14 appartenaient à la reprise du job joueur. Ses 21 dossiers locaux et leur sauvegarde avaient été supprimés sur demande ; l'historique serveur reste conservé. Les validations actuelles sont suivies dans [le point de reprise](docs/NEXT_ACTIONS.md).

## Reprendre le projet

Commencer par **[la passation](docs/PASSATION.md)**, puis [l'état de réalisation](docs/IMPLEMENTATION_STATUS.md) et [le point de reprise](docs/NEXT_ACTIONS.md). Ces documents distinguent les résultats réellement observés des validations encore ouvertes.

Installer Git LFS avant le clonage pour récupérer les archives exécutables :

```bash
git lfs install
git clone https://github.com/armineba/palimpseste.git
cd palimpseste
git lfs pull
```

Ouvrir le dossier **`game/`** depuis Unity Hub avec **Unity 6000.3.24f1**, URP 17 et le module Windows IL2CPP. Le dépôt contient les assets et paramètres du projet ; Unity recrée `Library/` à l'ouverture.

Les scripts courants ciblent `WindowsAnimationSheetRelease` et `WindowsAnimationSheetPlayable`, journal `animation-sheet-build.log` : [exploitation D16](ops/README.md).

Pour un agent de développement, lire d'abord `prompts/00_AGENT_BUILD.md`, puis `docs/05_OVERRIDE_LUNA_CODEX.md` et `docs/03_INTEGRATION_FOURNISSEUR.md`. Les avenants et décisions actuels prévalent sur les anciens instantanés du cahier.

## Livraison précédente — D13 / 1.3.0

Le **Player Windows 1.3.0** a été réellement construit et ouvert sur le poste d'origine. Le backend D13 y a été déployé et son diagnostic local est prêt. Les [instructions de jeu](docs/TESTER_MAINTENANT.md), la [preuve du déploiement](evidence/public/backend/image-reference-deployment-2026-09-20.json) et les [preuves Unity](evidence/public/unity/image-reference-2026-09-20.md) sont conservées.

Les ZIP dans **[deliverables/](deliverables/README.md)** contiennent le Player et le backend Windows publiés. Un clone n'installe pas le backend sur une autre machine : les scripts de service actuels comportent des chemins, identités et empreintes propres au poste d'origine. Voir la passation avant de les adapter.

Restent à constater ou accepter : nouveau parcours joueur D13 complet, relecture hors ligne de ce parcours, fidélité artistique et son, recette des 30 dessins, autre poste et accès distant/multijoueur. Le dernier réglage du matériau verre est inclus dans le build, mais postérieur à la dernière capture visuelle. Aucun rendu « 1 pour 1 » n'est annoncé comme accepté.

## Organisation

| Dossier | Contenu |
| --- | --- |
| `game/` | Projet Unity, dessin, bibliothèque, laboratoire, rendu et exécution des sorts. |
| `backend/` | API, worker durable, passerelle `codex exec`, diagnostic fournisseur et migrations SQL. |
| `shared/` | Contrats C# et compilation contrôlée. |
| `contracts/`, `prompts/`, `reference/` | Schémas, catalogue de capacités, consignes A/G/B/J et références. |
| `assets/sourced-vfx/` | Textures CC0, bruit MIT, licences et catalogue de références primaires. |
| `ops/` | Construction, publication, provisionnement, diagnostics et correctifs du CLI Codex. |
| `docs/`, `evidence/public/` | Cahier, décisions, passation, résultats et limites des vérifications réalisées. |
| `deliverables/` | Archives Windows du Player et du backend ; fichiers volumineux via Git LFS. |

Les réglages actuels documentés sont **Sol/high pour A**, **Astra/high pour G/B/J**. Les noms historiques Luna/Astra dans certains fichiers ne remplacent pas ces réglages. Le transport est Unity → API du jeu → worker → `codex exec` non interactif, avec un binaire durci : aucun outil en A/B/J, uniquement l'outil image en G. Le compositeur, la recherche et les captures sont des actions fixes du serveur.

Les identifiants Codex, clés, jetons joueur, secrets PostgreSQL et données privées du service ne font pas partie du dépôt. L'opérateur provisionne son environnement séparément. Aucun achat ou rechargement n'est ajouté au code ; les réglages de facturation restent ceux du compte utilisé.

## Vérification et provenance

Les commandes et résultats déjà exécutés sont détaillés dans [IMPLEMENTATION_STATUS](docs/IMPLEMENTATION_STATUS.md). Ne pas confondre les tests automatisés et le verdict humain. Les contrôles documentaires existants se lancent ainsi :

```bash
python -m pip install -r qa/requirements.txt
python qa/validate_contracts.py
```

`MANIFEST.sha256` décrit les fichiers de livraison et se régénère avec `ops/update-manifest.ps1`. Avec Git LFS, vérifier les fichiers après `git lfs pull`. Les empreintes et identifiants de commit contenus dans les anciennes preuves désignent la réalisation locale observée ; ils restent des preuves historiques.

`archive/SP1.0/`, `site/index.html` et le PDF du cahier conservent les documents initiaux. Les instructions Markdown actuelles prévalent sur leur ancien branchement fournisseur.
