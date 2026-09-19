# PALIMPSESTE — Dossier de construction Unity

**SP-1.0 · 19 septembre 2026 · Documentation de réalisation.**

Objectif du logiciel à construire : dessin réel sur parchemin → interprétation multimodale → description → recette contrôlée → sort lançable dans Unity → sauvegarde et relecture sans nouvelle génération.

## Lire et démarrer

Ouvrir `site/index.html` pour le book autonome, ou `Palimpseste_Unity_Cahier_de_realisation.pdf` pour le document imprimable. Le fichier HTML fonctionne localement, sans serveur et sans clé d’API. Le site documentaire n’appelle aucun modèle et ne simule pas le futur jeu.

Le cahier principal est `docs/01_CAHIER_DE_REALISATION.md`. L’annexe `docs/02_ANNEXE_CONTRATS.md` précise les règles entre champs. `docs/03_INTEGRATION_FOURNISSEUR.md` décrit le branchement réel de A et B. `docs/04_BACKLOG_DE_REALISATION.md` donne les trente tickets de construction.

Pour confier le développement à un agent, lui fournir ce dossier et `prompts/00_AGENT_BUILD.md`. La cible est l’ensemble des lots M0 à M7, sans s’arrêter à une description affichée. Les arbitrages P01–P10 sont des propositions de réalisation identifiées ; les U01–U09 proviennent de votre demande et des échanges.

## Contenu

- `contracts/` : six schémas métier, catalogue de capacités, deux enveloppes de sortie structurée, OpenAPI.
- `prompts/` : consigne du développeur, prompt A, prompt B et réparation technique bornée.
- `examples/` : description, plan, paquet et six porteurs illustratifs, avec masques et chemins.
- `reference/` : guide spatial de trois régions et définition de son layout.
- `qa/` : contrôles documentaires locaux, rapport reproductible et dépendances.

Les exemples sont manuels et synthétiques. Ils ne sont ni des sorties de modèle observées, ni des sorts joués dans Unity. Les géométries d’exemple ne sont pas issues d’un résolveur de production. Aucun binaire Unity, service déployé ou avis humain validé n’est livré dans ce dossier de documentation.

## Vérifier les contrats documentaires

Depuis la racine du dossier, avec Python 3.10 ou ultérieur :

```sh
python -m pip install -r qa/requirements.txt
python qa/validate_contracts.py
```

Le script contrôle la forme des schémas, les fixtures, leurs hashes, certains invariants métier et des mutations négatives. Il **ne** remplace ni le futur compilateur C#, ni une validation complète du fournisseur, ni les tests IL2CPP/physiques/performance/artistiques. Le rapport livré détaille ce qui n’a pas été testé.

## Autorité et confidentialité

Les sources originales sont référencées par nom et horodatage dans le cahier ; leurs transcripts bruts ne sont pas redistribués dans ce pack. Ce document ne fixe pas le reste du jeu : monde, progression, économie, PvP et système de combat restent hors périmètre.

Un conflit entre texte, schéma et catalogue se corrige explicitement et bloque la release logicielle. Ne pas remplacer silencieusement une contrainte par une version plus commode à programmer.
