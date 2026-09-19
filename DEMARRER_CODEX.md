# À coller dans Codex

Place ce dossier décompressé dans l'espace de travail du projet. Sélectionne Luna et le niveau maximal disponible dans ton client Codex avant de lancer la tâche. Le texte d'un prompt ne change pas à lui seul le modèle ou le réglage de la session qui le reçoit.

```text
Réalise entièrement le projet spécifié dans Palimpseste_Unity_Dossier.

Lis et applique d'abord prompts/00_AGENT_BUILD.md, puis
 docs/05_OVERRIDE_LUNA_CODEX.md et docs/03_INTEGRATION_FOURNISSEUR.md
à l'intérieur de ce dossier. Lis ensuite le cahier, les annexes, le backlog,
les contrats, les prompts A/B et les critères de recette.

La cible est la version finale de cette seule boucle : dessin réel →
Luna multimodale → description → Luna traductrice → compilation contrôlée
→ sort réellement exécutable dans Unity, avec rendu, son, physique,
sauvegarde et réutilisation hors ligne. Pas un prototype ni un simple plan.

Luna, gpt-5.6-luna, doit être utilisée au niveau maximal réellement accepté.
Notre API backend doit envoyer les demandes à Codex sur le serveur :
implémente cette passerelle réellement avec codex exec, pas un mock,
pas l'automatisation du terminal interactif, pas un appel direct depuis Unity.
L'avenant Luna prévaut sur l'ancien branchement Responses du livre.

Sépare strictement l'agent de développement du worker de génération joueur.
Aucun parchemin ne peut faire modifier du code, lancer un build ou accéder
aux fichiers/secrets du serveur. Les sorts sont des données contrôlées,
pas du C# généré à exécuter.

Audite l'environnement, vérifie les réglages observables et commence à coder.
Réalise M0 à M7, B01 à B30 et L01 à L10. Ne t'arrête pas après la description
ou une scène sommaire. Préserve les portes de validation humaine sans
bloquer les travaux indépendants. N'invente aucun test, appel réel ou build.

Conserve un état de réalisation et un point de reprise dans le dépôt.
Livre le projet, les assets, les services, les scripts, les builds exécutables
quand réellement construits et les preuves des tests. Signale exactement
ce qui reste non exécuté ou non accepté humainement.
```
