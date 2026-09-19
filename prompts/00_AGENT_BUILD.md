# PROMPT MAÎTRE — PALIMPSESTE / UNITY / LUNA AU MAXIMUM

Version SP-1.1-LUNA · 19 septembre 2026. Mission de développement, pas preuve d'un logiciel déjà réalisé.

## 0. Mission et niveau d'exigence

Réalise intégralement le sous-système final décrit dans ce dossier : dessin réel sur un parchemin → interprétation multimodale réelle → description structurée → plan de sort → compilation contrôlée → sort réellement lançable dans Unity → sauvegarde et relecture hors ligne.

Je ne demande ni une nouvelle étude, ni un MVP, ni une maquette, ni un squelette à compléter manuellement. Les jalons intermédiaires servent à construire et vérifier la version finale ; ils ne remplacent pas la livraison finale. Le périmètre est réduit à cette boucle, mais toutes ses couches doivent être terminées : interface, dessin, services, génération, physique, rendu, son, persistance, erreurs, tests et build autonome.

Le modèle demandé est GPT-5.6 Luna, identifiant documenté `gpt-5.6-luna`, au niveau de raisonnement maximal réellement disponible pour ce modèle, ce compte et la version installée de Codex. Cela concerne la génération des sorts et les sessions de développement/build confiées à Luna. N'utilise pas un autre modèle en remplacement silencieux. Une demande dans le prompt ne modifie pas magiquement la session actuelle : vérifie la configuration observable ; si elle n'est pas accessible, indique « configuration de session non vérifiée ».

« Au maximum » signifie profondeur, soin, vérifications et finition, pas permissions système illimitées, appels infinis ou dépassement des quotas. N'envoie pas littéralement `max` ou `ultra` comme valeur d'un paramètre qui ne les accepte pas. Consulte les capacités réelles : `xhigh` est un candidat si pris en charge ; un mode produit Max/Ultra ne doit pas être assimilé sans preuve à l'enum `model_reasoning_effort`.

## 1. Lire les sources, puis construire

Respecte d'abord les instructions applicables dans le dépôt et l'environnement. Ce projet est indépendant de tous nos autres jeux : aucun choix d'univers, de gameplay ou de stack ne doit en provenir.

Repère le dossier `Palimpseste_Unity_Dossier`. S'il reste compressé, extrais-le dans un répertoire distinct en validant les chemins ; ne remplace aucun travail existant. Lis entièrement les documents nécessaires avant d'implémenter leur lot :

- `docs/05_OVERRIDE_LUNA_CODEX.md`, puis `docs/03_INTEGRATION_FOURNISSEUR.md` : nouvelle décision de branchement, prioritaire sur l'ancienne API directe.
- `docs/01_CAHIER_DE_REALISATION.md`, `docs/02_ANNEXE_CONTRATS.md` et `docs/04_BACKLOG_DE_REALISATION.md`.
- Tous les schémas et le catalogue de `contracts/`, les prompts A/B/réparation, les fichiers `reference/`, `examples/README.md` et la QA documentaire.

Ordre de résolution des conflits produit : notre demande actuelle et cet avenant, puis SP-1.0, puis les exemples illustratifs. Les instructions système, autorisations et règles du dépôt restent applicables. Les anciens HTML/PDF sont conservés comme instantanés SP-1.0 ; leur branchement direct à Responses ne prévaut pas sur l'avenant Luna. Les anciens fichiers de transport sont dans `archive/` et ne sont pas des instructions actives.

Ne confonds jamais un exemple écrit à la main avec une génération réelle. Les scripts de QA fournis ne sont pas le moteur de jeu. Ne réutilise pas leurs résultats comme preuve que tu as développé ou exécuté Unity.

Conserve les arbitrages P01–P10 comme base de réalisation, sans les présenter comme des décisions déjà approuvées par les créateurs. Un conflit doit être documenté et résolu explicitement. Ne simplifie pas discrètement le catalogue, le dessin ou les critères pour faire passer un test.

## 2. Architecture obligatoire

Construis cette chaîne réelle :

```text
CLIENT UNITY WINDOWS
  dessin, journal, capture PNG, bibliothèque, scène d'épreuve
           │ HTTPS : API métier du jeu ; aucun accès direct à Codex
           ▼
BACKEND ASP.NET CORE + POSTGRESQL + STOCKAGE PRIVÉ
  identité, autorisations, captures, jobs durables, reprise
           ▼
WORKER DE GÉNÉRATION / ADAPTATEUR LUNA-CODEX
  processus Codex non interactifs isolés sur le serveur
           ├─ A : référence PNG + dessin PNG → SpellDescription
           ├─ résolution géométrique déterministe des pixels
           └─ B : description figée + géométries + catalogue → SpellPlan
           ▼
COMPILATEUR C# PARTAGÉ
  validation, clauses, unités, limites, géométries, manifeste
           ▼
COMPILED SPELL IMMUABLE → CLIENT UNITY
  exécution locale, collisions, effets, rendu et son
```

Le client parle exclusivement à notre API. Le backend construit les prompts à partir de modèles de prompt versionnés et de données validées. Un joueur ne choisit ni la commande, ni le modèle, ni un chemin serveur, ni les outils disponibles, ni les instructions du système.

Dans ce périmètre, un « build de spell » est la compilation de données vers `CompiledSpell`, pas une recompilation de Unity. Le build du logiciel est exécuté par Unity sur une machine de build compatible. Luna peut écrire et corriger les sources dans le workflow de développement, mais n'est pas le compilateur Unity.

## 3. Stack et audit initial

Conserve la base du cahier : Unity 6.3 LTS / URP / C#, cible Windows x64 ; API et worker ASP.NET Core .NET 10 ; contrats et noyau partagé compatibles .NET Standard 2.1 ; PostgreSQL et stockage d'artefacts derrière une interface.

Vérifie les versions disponibles, licences, plateformes de build, compatibilités de packages et sérialisation IL2CPP, puis verrouille les versions exactes. Ne suppose pas que le runtime Unity est .NET 10. Ne change pas de moteur. N'ajoute pas un backend Node complet uniquement pour lancer Codex : le chemin .NET → processus Codex est le choix de référence.

Audite les fichiers, l'état Git, les modifications existantes, les outils, Unity Editor, les modules Windows/IL2CPP, le SDK .NET, Codex et l'authentification observable. Enregistre les résultats sans afficher de secrets. Repère les moyens de test graphique réellement disponibles.

Sur un serveur sans la chaîne nécessaire au build Windows, continue les tâches réalisables et fournis un runner/script Windows pour le commit exact. Ne déclare pas un build Windows terminé sur la seule base d'un `dotnet build`. Ne touche pas aux autres services du serveur, au pare-feu ou aux accès SSH pour contourner une difficulté.

Crée une matrice exigences → fichiers → tests → preuves → état. Conserve des statuts distincts : prévu, implémenté, compilé, testé automatiquement, testé avec Luna réelle, exécuté dans Unity, accepté humainement.

## 4. Passerelle Luna/Codex réellement fonctionnelle

Implémente un adaptateur remplaçable derrière `IMultimodalInterpreter` et `IDescriptionPlanner`, par exemple `LunaCodexProvider` + `CodexProcessRunner`. Il doit lancer Codex sur le serveur, pas envoyer des frappes dans une conversation ouverte.

Chemin de référence : `codex exec`, avec les arguments documentés et vérifiés sur la version installée pour le modèle, les images, les sorties JSONL et le schéma de réponse. Utilise `ProcessStartInfo`, `UseShellExecute=false`, `ArgumentList`, stdin et des flux séparés. N'utilise ni `bash -c`, ni `cmd /c`, ni concaténation de prompt dans une commande shell. Aucun `tmux send-keys`, scraping de terminal, automatisation de VS Code ou injection dans notre session personnelle.

A doit recevoir les deux vrais fichiers image, dans l'ordre référence/dessin. Un chemin écrit dans du texte n'est pas une image multimodale. B reçoit les données structurées, pas les images ni le dépôt Unity entier. Chaque appel a un contexte isolé ; aucun historique d'un autre joueur ou d'une session de développement.

Pour `--output-schema`, passe un schéma JSON, pas l'enveloppe Responses `type/name/strict/schema`. Les fichiers extraits dans `contracts/codex/` sont un point de départ documentaire, à éprouver réellement. Si le transport refuse certains mots-clés, réalise une projection de transport documentée et valide toujours le résultat contre le contrat métier complet. Ne retire pas une contrainte métier pour rendre le fournisseur plus facile à appeler.

Parse les événements de la version verrouillée, gère sortie finale, échec, réponse vide, troncature, refus, quota, auth expirée, timeout et arrêt de processus. La réussite exige terminaison réussie, réponse finale complète et validation métier. Ne considère pas le premier message ou un JSON partiel comme le résultat.

Fournis un diagnostic `provider doctor` réel : version, chemin de l'exécutable, statut d'authentification sans secret, présence de Luna, effort sélectionné, essai à deux images, sortie A, sortie B, validation des schémas et isolation. Le diagnostic local ne facture aucun appel ; un test actif explicite effectue les appels de contrôle. Une erreur de préflight bloque seulement la génération réelle concernée, pas les travaux indépendants.

## 5. Luna au maximum, sans faux réglage

Configure A et B sur `gpt-5.6-luna`. Découvre le niveau maximal accepté par le modèle et le client installés à partir de leurs capacités, puis éprouve-le. Une introspection facultative ne doit pas rendre le chemin de production dépendant d'une interface expérimentale.

Conserve séparément : modèle demandé, effort demandé, configuration effectivement envoyée, valeurs éventuellement confirmées par le fournisseur, version Codex et résultat du test de compatibilité. Si Codex ne retourne pas le modèle ou l'effort effectif, stocke « non exposé », pas une confirmation inventée.

Aucun fallback automatique vers un autre modèle, aucune baisse d'effort après une erreur, aucune température ou seed supposée compatible. Si le niveau maximal n'est pas exploitable, laisse la tâche en incident identifié ; documente la cause plutôt que présenter une génération moins exigeante comme conforme.

Le timeout historique de 120 secondes n'est pas imposé à ce transport agentique. Implémente des limites séparées et configurables pour attente en file, durée totale d'une tentative et heartbeat du worker. Ne tue pas une tâche simplement parce qu'aucun texte n'arrive pendant le raisonnement. Le plafond de départ proposé pour une tentative est 1 800 secondes ; il est ajustable et ne constitue pas une promesse de latence.

Respecte les quotas du compte et les budgets du service. Préserve les bornes de reprises du cahier, dont dix appels fournisseur maximum par job, en comptant A, B, réparations et reprises. Commence avec une génération simultanée par worker isolé ; augmente après mesure. Le maximum de raisonnement ne signifie pas une concurrence illimitée.

## 6. Droits de développement et droits du jeu séparés

Le workflow de développement peut modifier les fichiers autorisés du projet, créer les assets, tester et préparer les builds. Ses branches, dossiers, sessions et autorisations sont distincts du worker qui interprète les parchemins.

Le worker runtime ne doit pas pouvoir exécuter du shell à la demande du modèle, modifier le jeu, lancer un build, lire les secrets du serveur, parcourir d'autres données utilisateur, utiliser nos plugins/MCP personnels ou accéder au socket Docker. Désactive les outils non requis par une configuration testée et applique une isolation OS réelle. `read-only` seul n'est pas une preuve de confidentialité.

Utilise un compte système et un `CODEX_HOME` dédiés, une configuration minimale, des répertoires par job et aucune découverte involontaire d'instructions du dépôt. N'hérite pas de nos clés SSH, de nos sessions interactives, de notre historique ou des services connectés personnels.

Le déploiement initial est un laboratoire privé authentifié. Ne publie pas un endpoint générique `/prompt` ou un proxy Codex ouvert. Une future ouverture publique exige une revue d'authentification et d'usage adaptée, pas une simple suppression du jeton. Ne prétends pas qu'un abonnement personnel est une capacité de service illimitée.

## 7. Dessin et expérience finale

Implémente le parcours complet du cahier : bibliothèque, ouverture du support, dessin, traitement, fiche du sort, scène d'épreuve et reprise. L'interface est en français, cohérente, lisible et utilisable dans le build autonome.

Le modèle lit l'image entière et sa référence. Le pinceau influence le résultat par sa trace visible. Pas de bouton « attaque/soin » qui remplace l'interprétation du dessin. Pas de dictionnaire de sorts complets ni de sélection cachée de prefab en guise de génération.

Respecte le journal durable, les budgets d'encre et la règle de verrouillage par région du cahier. Pas de brouillons, gomme, annulation, presets de sorts, essai gratuit avant engagement, choix entre candidats ou confirmation après découverte. Les guides de layout restent permis : ils ne sont pas des sorts prédessinés.

Une fermeture après inscription préserve le parchemin engagé et les traces durables. Une interruption système n'autorise pas le retour à un support vierge. Un dessin raté peut donner un sort médiocre ; une panne de serveur ne doit jamais être déguisée en mauvais sort.

## 8. Interprétation, plan et compilation

A fournit `SpellDescription` avec observations visuelles, clauses mécaniques, sujets, relations et éléments de style. Archive la première sortie techniquement acceptée avec les hashes exacts de ses entrées. La présence de JSON valide ne vaut pas validation humaine de la lecture.

Le résolveur géométrique extrait les chemins, masques et silhouettes utiles des vrais pixels. Il ne décide pas qu'une forme géométrique signifie automatiquement un effet tactique. Préserve trous, composantes et courbures selon les contrats.

B traduit la description figée avec le catalogue réellement disponible, les unités, les bornes et les références de géométrie. Il ne peut ajouter du soin, changer les cibles ou supprimer une relation pour simplifier le travail. Les réparations sont techniques, bornées et journalisées ; elles ne servent pas à obtenir plusieurs tirages artistiques.

Le compilateur valide les schémas, les clauses, les IDs, le graphe, les limites de propagation, les valeurs finies, les unités, les budgets et les assets. Il publie un paquet immuable ; les formules ou hashes ne sont pas confiés à un calcul supposé du modèle. Le texte factuel final découle des règles réellement compilées, avec séparation claire de l'interprétation initiale.

Aucun C#, DLL, shader, script, programme ou commande généré par le modèle ne s'exécute à partir d'un parchemin. Les matériaux et shaders sont développés et testés dans le projet, puis sélectionnés/paramétrés par des données autorisées.

## 9. Sorts Unity, physique et finition

Livre les six porteurs du catalogue : projectile, faisceau, champ, onde, barrière et piège. Livre tous les effets prévus : dégâts, soins, impulsion, brûlure, Mouillé et ralentissement, ainsi que les réactions documentées et les événements composés autorisés. Une capacité exposée au modèle doit exister et être testée dans le runtime.

Implémente ciblage, contacts, occlusion, déduplication, trajectoires, propagation bornée, durée de vie, accumulation de statuts, collision et extinction. Une valeur de santé ou de force doit réellement changer ; une animation ne remplace pas un effet mécanique.

La géométrie du dessin doit agir sur une propriété perceptible majeure : trajectoire, empreinte, silhouette, distribution ou structure. Changer seulement le titre, la couleur ou une graine ne satisfait pas la demande. N'invente pas une garantie d'unicité mécanique absolue ; différencie nouvelle création et reprise du même support.

Crée réellement les scènes, prefabs de composants, matériaux, shaders autorisés, textures, particules et sons. Les composants réutilisables sont nécessaires ; les sorts joueur prédéfinis ne le sont pas. Les outils Editor peuvent générer les assets, mais le projet livré doit s'ouvrir et jouer sans reconstruire la scène à la main.

Le rendu doit conserver la lisibilité des volumes de collision, les phases de déclenchement, les impacts et l'extinction. Pas de sphère/particule universelle recolorée en remise finale. N'étends pas le scope vers monde ouvert, quêtes, classes, économie, PvP ou multijoueur.

## 10. API, stockage, reprise et observation

Conserve les routes métier et les états du contrat OpenAPI. Ne reconstruis pas une API concurrente incompatible. Les contrôles de propriétaire sont serveur ; les références de fichiers sont des IDs contrôlés, pas des chemins ou URL arbitraires.

Les requêtes sont durables : capture immuable, idempotence par support/propriétaire, job persistant, lease renouvelé et token d'exclusion des workers obsolètes. Persiste A avant B ; une reprise de B ne doit pas réinterpréter le dessin. Une tâche déjà prête rend le paquet existant.

Distingue une seule publication locale et un appel externe exactement une fois : si une réponse a été perdue, Codex peut avoir consommé du quota. Ne promets pas l'absence de double consommation fournisseur sans mécanisme démontré. Archive et rapproche les tentatives incertaines avant de relancer.

Préserve la confidentialité des prompts, images, sorties et identifiants. N'expose pas les événements de raisonnement ou les traces brutes de Codex dans le client ; affiche uniquement des états utiles et vrais. Pas de pourcentage inventé.

Une fois téléchargé et validé, le sort se lance sans Codex, sans réseau et sans nouvelle génération. Vérifie les paquets et références au chargement. Une panne du fournisseur ne doit pas casser les sorts déjà possédés.

## 11. Tests, preuves et validation humaine

Réalise tests de contrat, unitaires C#, intégration API/base/stockage, tests du runner Codex, EditMode, PlayMode, build autonome et essais à appels réels. Les fixtures et mocks restent limités aux tests identifiés ; ils ne peuvent pas rendre le chemin produit artificiellement vert.

Couvre au minimum : deux vraies images reçues par A ; description réelle traitée par B ; conservation des clauses ; six porteurs et tous les effets ; dessins aux géométries contrastées ; coupure après A et pendant B ; reprise après redémarrage ; requête dupliquée ; sortie partielle ; quota/auth/timeout ; tentatives d'injection écrites dans l'image ; absence d'accès aux fichiers, outils et sessions non autorisés ; relance hors ligne du même paquet.

Mesure les performances sur du matériel identifié et respecte les objectifs du cahier. Conserve captures, journaux opérationnels, rapports, version du build et commandes reproductibles. N'annonce pas « 60 FPS » ou « fonctionne en IL2CPP » sans mesure/exécution correspondante.

Le corpus de recette est inédit et distinct du corpus de mise au point. Les créateurs humains jugent fidélité du dessin, qualité visuelle, intérêt du sort et expérience de dessin. Tu ne peux pas signer leur verdict ou déclarer le jeu approuvé en leur nom. Continue les tâches indépendantes quand une revue est en attente ; n'adopte pas comme validé un choix dont elles dépendent.

## 12. Ordre de travail et continuité

Commence par l'audit, les contrats et le préflight Luna, puis réalise M0 à M7 et B01 à B30. Ajoute explicitement les tickets L01 à L10 de l'avenant. Avance jusqu'au résultat demandé plutôt que terminer après un plan ou demander une confirmation à chaque fichier.

Découpe les tâches difficiles en unités vérifiables. Les sous-agents éventuellement disponibles doivent rester sur Luna au niveau réellement accepté, avoir des responsabilités et espaces de travail séparés et une concurrence bornée. Ne crée pas de délégation récursive sans contrôle. Ne suppose pas qu'un sous-agent utilise automatiquement le même modèle que l'orchestrateur.

Maintiens `docs/IMPLEMENTATION_STATUS.md`, `docs/DECISIONS.md`, `docs/BLOCKERS.md` et `docs/NEXT_ACTIONS.md`. Note fichiers modifiés, commandes, résultats réels et prochaines actions concrètes. Une interruption ou limite de contexte doit laisser un point de reprise exact, pas une mention « terminé ».

Si un outil, une licence ou un accès manque, ne fabrique pas le résultat. Décris le blocage précis, fournis le script/protocole d'exécution sur la machine adéquate et poursuis les éléments indépendants. Ne requalifie pas cette livraison incomplète comme version finale acceptée.

## 13. Livrables et définition de terminé

Livre le dépôt complet, les scènes et assets, le client Windows autonome construit lorsque l'environnement le permet, le backend installable, le runner Luna réel, la configuration d'exemple sans secret, les migrations, les scripts d'installation/build/test, la sauvegarde/restauration, la provenance des assets, le manuel et le dossier de preuves.

La démonstration finale doit pouvoir être reproduite ainsi : installation → connexion au service privé → nouveau dessin humain → vrais appels Luna A/B au réglage vérifié → paquet compilé → sort visible et mesurable → arrêt complet → lancement hors ligne du même sort → panne de génération puis reprise sans second support ni deuxième publication.

Tout point non réalisé, non testé ou non validé humainement reste explicitement ouvert. « Sources écrites » n'est pas « build exécuté », et « tests automatiques réussis » n'est pas « qualité approuvée ».

Ta première réponse doit donner l'état concret de l'environnement, l'emplacement des spécifications, le paramétrage Luna vérifiable et le premier lot que tu commences réellement. Ensuite, implémente. Ne remplace pas le travail par un nouveau document de conception.
