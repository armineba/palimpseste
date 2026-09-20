# Avenant prioritaire — SP-1.1-LUNA

> **Priorité actuelle D13, 20 septembre 2026 :** l'auteur demande pour chaque nouveau sort une vraie image générée depuis la description, puis sa reconstruction 3D/VFX. A reste `gpt-5.6-sol` / `high`, prompt `2.2` ; le nouveau stade G utilise `gpt-6-astra` / `high` et l'outil natif `image_generation` de Codex avec l'abonnement connecté, sans clé API ; B reste `gpt-6-astra` / `high`, reçoit cette image et utilise le prompt `2.1`. Voir [D13](DECISIONS.md). Aucun changement implicite des modèles A/B n'est autorisé par le nom courant donné à l'interprète. La révision B `2.1` précise `turn_mdeg_s=0` hors `homing`, après le rejet réel `turn_rate` ; la reprise de B seul a ensuite validé le plan et compilé le paquet avec le même PNG G.
>
> Cette décision remplace les restrictions D10–D12 interdisant une image générée par joueur et les anciens passages réservant B au texte. La référence est figée et liée par hash à la description et au plan ; `appearance.construction` décrit des parties bornées et exige le client `1.3.0`. L'audit a trouvé `apply_patch` encore exposé dans l'ancien CLI : **A/B/G doivent utiliser le même candidat durci `codex-image.exe`**, avec registre d'outils vide en A/B et limité à l'image native en G. `codex.exe` et ses preuves restent archivés ; trois nouvelles preuves liées au SHA durci sont nécessaires : locale, A seul, G+B. L'isolation, les données contrôlées, la sauvegarde hors ligne et l'absence de tests/builds dans la génération joueur restent obligatoires. D13 est déployé sur le service local et le Player `1.3.0` est ouvert pour essai ; voir [l'état de réalisation](IMPLEMENTATION_STATUS.md) et [le point de reprise](NEXT_ACTIONS.md).

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
