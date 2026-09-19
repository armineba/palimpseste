# PALIMPSESTE — Dossier de réalisation Unity + Luna/Codex

**SP-1.1-LUNA · état au 20 septembre 2026.** Le dossier documentaire fourni est conservé. Une implémentation Unity 6.3 URP, un backend .NET/PostgreSQL, un worker et une passerelle `codex exec` ont été ajoutés. L'état exact, les preuves exécutées et le point de reprise figurent dans [`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md). Un appel A réel a produit un JSON conforme mais a été refusé faute de preuve du modèle et de l'effort effectifs ; B, la boucle dans Unity et les validations humaines restent ouverts.

## Démarrer

Pour reprendre la réalisation, commence par [`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md), puis lis `prompts/00_AGENT_BUILD.md` et l'avenant `docs/05_OVERRIDE_LUNA_CODEX.md`. Pour ouvrir le jeu, utilise le projet [`game/`](game/) avec Unity 6000.3.24f1 et URP 17. Le Player Windows IL2CPP le plus récent est sous `game/Build/WindowsPlayerFlowOwnerFinal/` ; les ZIPs de [`deliverables/`](deliverables/) sont historiques et ne contiennent pas le parcours d'invitation et description A actuel.

L'installation vérifiée, l'usage du lecteur, la provenance des assets, la recette humaine à remplir et les hashes des archives sont décrits dans [`docs/SETUP.md`](docs/SETUP.md), [`docs/MANUEL_JOUEUR.md`](docs/MANUEL_JOUEUR.md), [`docs/ASSETS_ET_LICENCES.md`](docs/ASSETS_ET_LICENCES.md), [`docs/RECETTE_FINALE.md`](docs/RECETTE_FINALE.md) et [`evidence/public/release-2026-09-19.md`](evidence/public/release-2026-09-19.md).

Le transport a changé : Unity → API métier du jeu → worker → Codex non interactif sur le serveur, utilisant Luna → données validées → Unity. Ne construis pas l'ancien branchement Responses direct comme chemin principal.

Lire dans l'ordre :
1. `prompts/00_AGENT_BUILD.md`.
2. `docs/05_OVERRIDE_LUNA_CODEX.md` et `docs/03_INTEGRATION_FOURNISSEUR.md`.
3. Le cahier principal, l'annexe de contrats et le backlog B01–B30.
4. Contrats, catalogue, prompts A/B, références, exemples et QA.

L'avenant ajoute les tickets L01–L10. L'objectif reste M0–M7 : une version finale de la boucle complète, avec validation humaine distincte des essais automatisés.

## Logiciel et preuves présents

- `game/` : projet Unity 6000.3.24f1 URP et Player Windows x64 IL2CPP brut dans `game/Build/WindowsPlayerFlowOwnerFinal/` ; source, dessin, bibliothèque, scène d'épreuve et sorts de données.
- `backend/` et `shared/` : API ASP.NET, stockage, worker durable, passerelle `codex exec`, doctor et compilateur de contrats ; archive Windows autoportante historique dans `deliverables/`.
- `ops/` : provisionnement du compte runtime, diagnostics, sauvegarde, restauration, statut et packaging. Les fichiers de connexion et d'authentification restent hors Git.
- `evidence/public/` : résultats de build, API/PostgreSQL et Unity, avec leurs limites. Une sauvegarde privée du dessin capturé dans Unity attend la recette A/B réelle.

Le compte Codex partagé est connecté sous le profil du worker isolé, sans clé API ni mécanisme d'achat. Seuls le quota et les crédits déjà présents sont autorisés ; la désactivation de la recharge automatique reste un réglage du compte à vérifier par son titulaire. La boucle s'arrête encore avant la publication d'un sort Luna tant que le doctor actif A/B, l'essai intégré dans Unity et les validations humaines n'ont pas produit leur preuve. Les essais hors ligne du lecteur utilisent des fixtures manuelles et sont nommés comme tels.

## Ce qui a été ajouté ou remplacé

Le prompt maître et l'intégration fournisseur ont été remplacés. Un avenant prioritaire, les sources Codex, une configuration applicative illustrative et deux schémas de sortie Codex ont été ajoutés. Les contrats métier n'ont pas été modifiés. Les fichiers remplacés sont conservés dans `archive/SP1.0/` pour traçabilité.

`site/index.html` et `Palimpseste_Unity_Cahier_de_realisation.pdf` restent les instantanés SP-1.0 déjà livrés. Leurs exemples de fournisseur ne sont plus les instructions actives : l'avenant Markdown prévaut pour Codex/Luna. Aucun nouveau site ou PDF n'est présenté comme réédité dans cette mise à jour.

## Vérification

Les contrôles existants se lancent depuis ce dossier :

```sh
python -m pip install -r qa/requirements.txt
python qa/validate_contracts.py
```

`archive/SP1.1-source-manifest.sha256` conserve les hashes du dossier documentaire initial. `MANIFEST.sha256` est régénéré depuis l'index Git pour la livraison actuelle par `ops/update-manifest.ps1`. Les rapports de QA documentaire ne prouvent ni un appel Luna ni un build. Les preuves d'exécution ajoutées sont détaillées dans `docs/IMPLEMENTATION_STATUS.md` et `docs/UNITY_TEST_PROOF.md`; elles gardent les tests automatisés distincts des recettes humaines.

La configuration `.env.example` contient des noms applicatifs proposés et aucun secret. Elle ne constitue pas une configuration native de Codex prête à exécuter. Les secrets doivent être provisionnés hors du dépôt par l'opérateur.
