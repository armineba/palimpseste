# Sources techniques de l'avenant Luna / Codex

Consultation : 19 septembre 2026. Références officielles consultées en ligne pour construire les consignes. Plusieurs pages `developers.openai.com/codex/` redirigent vers la documentation `learn.chatgpt.com`.

- Modèles : https://developers.openai.com/codex/models — identifiant `gpt-5.6-luna`, disponibilité dépendante du client/compte, distinction des réglages de raisonnement et des modes Max/Ultra.
- Fiche modèle API : https://developers.openai.com/api/docs/models/gpt-5.6-luna — la fiche publiée énumère `none`, `low`, `medium`, `high`, `xhigh` et `max` pour l’effort. Cette liste documente l’API et ne suffit pas à prouver que le binaire Codex ou le compte runtime accepte `max`.
- Configuration : https://developers.openai.com/codex/config-reference — `model_reasoning_effort`, permissions, contrôles d'outils ; `xhigh` dépend du modèle. La liste publiée de valeurs n'établit pas que Luna accepte chaque valeur.
- Mode non interactif : https://developers.openai.com/codex/non-interactive-mode — `codex exec`, JSONL, schéma de sortie, sortie finale, authentification et usages d'automatisation.
- Commandes : https://developers.openai.com/codex/developer-commands — images, `--output-schema`, `--output-last-message`, stdin, sandbox et statut des interfaces. Vérifier les arguments sur le binaire installé.
- Authentification : https://developers.openai.com/codex/auth — accès par abonnement/clé, stockage des identifiants, automatisation et avertissement contre l'exposition de l'exécution dans des environnements publics/non fiables.
- SDK : https://developers.openai.com/codex/codex-sdk — contrôle programmatique d'agents Codex, alternative éventuelle à l'adaptateur de processus.
- App Server : https://developers.openai.com/codex/app-server — protocole d'intégration, catalogue de modèles et interfaces dont certaines sont expérimentales ; pas de dépendance de production imposée par cet avenant.

## Limites de cette vérification

Aucun serveur de l'équipe n'a été interrogé. Le binaire installé, le niveau Luna réellement proposé, l'authentification disponible, la réception multimodale et les schémas doivent être testés au préflight. Les versions système/Unity du cahier sont des cibles de réalisation, pas un inventaire de l'environnement.

Ce document ne prétend pas obtenir un accès supplémentaire au compte, augmenter ses quotas, garantir un coût, certifier un droit d'exploitation publique ou démontrer que les futurs tests Unity réussiront.
