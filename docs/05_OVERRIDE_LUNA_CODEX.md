# Avenant prioritaire — SP-1.1-LUNA

> **Décision ultérieure D09, 20 septembre 2026 :** la forme 3D du sort doit représenter l'objet imaginé par Astra (rocher, arme, créature, énergie), sans copie systématique du contour du dessin. Les passages historiques imposant une silhouette ou une emprise issue de l'encre sont remplacés pour les nouveaux sorts par le rendu contrôlé issu de la description. Voir [D09](DECISIONS.md).

> **Décision utilisateur ultérieure, 20 septembre 2026 :** pour les nouveaux parchemins, l'auteur a remplacé le découpage en trois régions par un dessin libre sur tout le carré, terminé uniquement par un bouton « Dessin terminé ». Il a ensuite choisi `gpt-6-astra` pour interpréter l'image entière et concevoir la description, puis `gpt-5.6-luna` pour construire le plan du sort sous ses consignes. Voir [D07–D08](DECISIONS.md). Les passages ci-dessous qui imposent Luna pour l'étape A ou la fermeture au relâchement sont historiques ; le transport `codex exec`, l'isolation du worker et la compilation de données contrôlées restent applicables.

## Décision utilisateur

Le modèle pour générer les sorts et les tâches de développement/build confiées à Codex est Luna, au niveau maximal réellement disponible. L'appel passe par une API propre au jeu qui pilote Codex sur le serveur.

Cette décision ne change ni Unity, ni les contrats de sorts, ni le catalogue, ni l'irréversibilité du parchemin, ni le périmètre final de la boucle. Les contrats métier restent versionnés SP-1.0 tant que leur structure ne change pas.

## Ce que l'avenant remplace

La référence de transport de SP-1.0 était l'API Responses appelée directement par le backend. Elle est remplacée par `LunaCodexProvider`, exécutant Codex non interactif sur le serveur. Unity ne connaît que notre API métier.

L'ancien `prompts/00_AGENT_BUILD.md` est remplacé par le prompt maître Luna. `docs/03_INTEGRATION_FOURNISSEUR.md` est remplacé par le branchement Codex complet. Les originaux sont conservés dans `archive/` pour traçabilité et ne sont pas des consignes actives.

Le HTML et le PDF déjà livrés sont conservés sans réédition, comme instantanés SP-1.0. Pour l'implémentation, les fichiers Markdown de cet avenant prévalent sur leurs exemples de fournisseur, de clé et de timeout. Ne pas exiger une clé API Responses directe uniquement parce que l'ancien livre la mentionne.

## Paramètres de réalisation proposés

L'identifiant documenté est `gpt-5.6-luna`. Le niveau maximal n'est pas codé aveuglément : découverte et test de compatibilité, sans substitution silencieuse. Les paramètres applicatifs d'exemple ne sont pas des variables natives de Codex.

Les requêtes passent par `codex exec` piloté par .NET, sans injection dans une session ouverte. Le timeout de départ proposé est 1 800 secondes par tentative, configurable, et non 120 secondes. Aucun délai de génération ne modifie le cooldown ou la puissance du sort. Les plafonds de reprises du cahier sont conservés.

Les permissions sont séparées entre développement et runtime. Le niveau maximal de raisonnement ne justifie pas un serveur joueur avec accès complet au système. La génération runtime doit être confinée, le laboratoire privé et le fournisseur remplaçable derrière une interface stable.

Les routes métier existantes sont conservées. Aucun endpoint générique pour exécuter un prompt, une commande ou un build demandé par un joueur n'est ajouté. Le « build de spell » reste une compilation de données contrôlées, distincte du build Unity.

## État de cette livraison

Ce dossier est une documentation et un prompt de réalisation. Les nouveaux schémas de transport sont extraits des schémas existants et vérifiés localement ; leur compatibilité effective avec Luna/Codex reste à tester sur le serveur. Aucun appel Codex réel, build Unity, déploiement ou verdict humain nouveau n'est prétendu ici.
