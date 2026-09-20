# Palimpseste — dessin vers sort dans Unity

Projet **Unity 6.3 / URP**, backend .NET/PostgreSQL et worker Codex isolé. Le parcours est : **dessin libre → description → image générée → composition 3D/VFX contrôlée → sort jouable dans le laboratoire**. Les sorts sont des données validées, jamais du code produit par le joueur.

## D14 livré localement — cycle de vie et critique visuelle

Le **Player `1.4.0` a été construit, empaqueté et ouvert sur le poste d'origine** ; son [manifeste](evidence/public/unity/lifecycle-delivery.json) atteste 29 fichiers et un ZIP de 44 366 062 octets. Le backend D14 est déployé, la migration `009` appliquée et le renderer protégé installé : [preuve de livraison locale](evidence/public/backend/lifecycle-2026-09-20.json). Le texte pilote quatre phases : apparition, activité, réaction au contact et disparition naturelle. L'image fixe la cible visuelle ; un renderer Unity précompilé réalise les captures runtime, puis une nouvelle session critique J guide les corrections bornées du plan. Les modèles n'ont aucun accès aux outils de code, aux builds ou au lancement du renderer.

Le CoreSmoke déjà réussi précède la dernière demande du créateur d'arrêter les tests. **Aucun parcours joueur, appel modèle, capture ou verdict visuel D14 n'est déclaré exécuté** : le créateur veut essayer lui-même. Ses 21 dossiers locaux de sorts et leur sauvegarde ont été supprimés sur demande ; l'historique serveur reste conservé. Voir [le point de reprise](docs/NEXT_ACTIONS.md).

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
| `ops/` | Construction, publication, provisionnement, diagnostics et correctifs du CLI Codex. |
| `docs/`, `evidence/public/` | Cahier, décisions, passation, résultats et limites des vérifications réalisées. |
| `deliverables/` | Archives Windows du Player et du backend ; fichiers volumineux via Git LFS. |

Les réglages actuels documentés sont **Sol/high pour A**, **Astra/high pour G et B**. Les noms historiques Luna/Astra dans certains fichiers ne remplacent pas ces réglages. Le transport est Unity → API du jeu → worker → `codex exec` non interactif, avec un binaire durci : aucun outil exécutable en A/B, uniquement l'outil image en G.

Les identifiants Codex, clés, jetons joueur, secrets PostgreSQL et données privées du service ne font pas partie du dépôt. L'opérateur provisionne son environnement séparément. Aucun achat ou rechargement n'est ajouté au code ; les réglages de facturation restent ceux du compte utilisé.

## Vérification et provenance

Les commandes et résultats déjà exécutés sont détaillés dans [IMPLEMENTATION_STATUS](docs/IMPLEMENTATION_STATUS.md). Ne pas confondre les tests automatisés et le verdict humain. Les contrôles documentaires existants se lancent ainsi :

```bash
python -m pip install -r qa/requirements.txt
python qa/validate_contracts.py
```

`MANIFEST.sha256` décrit les fichiers de livraison et se régénère avec `ops/update-manifest.ps1`. Avec Git LFS, vérifier les fichiers après `git lfs pull`. Les empreintes et identifiants de commit contenus dans les anciennes preuves désignent la réalisation locale observée ; ils restent des preuves historiques.

`archive/SP1.0/`, `site/index.html` et le PDF du cahier conservent les documents initiaux. Les instructions Markdown actuelles prévalent sur leur ancien branchement fournisseur.
