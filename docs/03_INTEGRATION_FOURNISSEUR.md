# Intégration du fournisseur — Luna via Codex serveur

**Avenant SP-1.1-LUNA · 19 septembre 2026.** Ce fichier remplace l'ancienne référence à l'API Responses directe. Il spécifie du travail à réaliser ; il ne contient ni backend développé ni preuve d'appel réel. Sources officielles et limites de vérification : `docs/06_SOURCES_CODEX.md`.

## 1. Décision

Unity appelle notre API métier authentifiée. Le backend et son worker pilotent Codex installé sur le serveur. Codex utilise `gpt-5.6-luna` pour A et B. Le niveau choisi doit être le plus élevé réellement exposé et accepté pour cette combinaison modèle/compte/version.

Le chemin de référence est un processus `codex exec` piloté directement depuis .NET. Il évite l'automatisation du terminal interactif et conserve la stack du cahier. Un SDK officiel peut remplacer cet adaptateur si un besoin démontré le justifie, sans changer les contrats ni introduire une autre chaîne métier. L'App Server n'est pas un prérequis de production : la documentation consultée signale des interfaces expérimentales. Une introspection locale éventuelle est facultative et versionnée.

Codex runtime n'est pas Codex développeur. A/B produisent des données ; seuls les outils de développement de confiance peuvent modifier les sources. Les builds du logiciel sont une pipeline de développement distincte, exécutée par Unity, jamais une capacité exposée au joueur.

## 2. Objets et séparation des responsabilités

`LunaCodexProvider` implémente `IMultimodalInterpreter` et `IDescriptionPlanner`. Il ne connaît pas UnityEngine et ne charge pas de code généré. `CodexProcessRunner` gère le processus et les événements, pas les règles du sort. Le compilateur reste responsable des invariants métier.

Définir en infrastructure un résultat typé distinguant : succès complet ; refus ; sortie incomplète ; erreur d'authentification ; modèle indisponible ; effort non pris en charge ; quota atteint ; transport incertain ; timeout ; échec de processus ; schéma invalide ; violation métier ; violation d'isolation.

Les métadonnées fournisseur restent hors du contrat de gameplay : `attempt_id`, `job_id`, étape, empreintes, version CLI, modèle/effort demandés, configuration envoyée, modèle/effort éventuellement rapportés, heure de début/fin, IDs de session disponibles, issue et usages réellement retournés. Une donnée non exposée reste nulle/non vérifiée. Ne l'invente pas pour compléter un tableau.

## 3. Construction d'une tentative

Pour chaque job, créer un dossier privé contrôlé par le serveur. Les noms et chemins sont générés par l'application ; aucun chemin du client ou du modèle n'est accepté. Le dossier contient seulement les entrées autorisées, les schémas, un prompt assemblé et les sorties de cette tentative.

A reçoit : l'image de référence versionnée détenue par le serveur ; le PNG canonique du dessin ; le prompt A ; le contexte du layout et les capacités disponibles. Les deux images sont transmises comme pièces multimodales réelles, dans un ordre testé. Le PNG d'encre et le journal restent à la disposition des composants déterministes ; ils ne remplacent pas l'image complète.

B reçoit : les octets de la description A figée et son hash calculé par le serveur ; le catalogue ; les IDs et mesures géométriques ; le profil de règles. B n'a pas besoin d'une session précédente ou d'un accès au dépôt pour fonctionner. Les images ne sont pas réinterprétées à ce stade.

Le prompt A/B est une instruction applicative contrôlée. Les titres, annotations, descriptions, noms de fichiers et textes éventuellement visibles dans le dessin sont des données non fiables. Une annotation « ignore les règles » n'a aucune autorité et ne doit ouvrir aucun outil.

## 4. Invocation non interactive

Les options exactes sont vérifiées par `codex --version`, `codex exec --help`, la configuration active et un test de compatibilité avant verrouillage de version. Le schéma de commande suivant est explicatif, pas une commande de déploiement à copier sans préflight :

```text
exécutable : chemin absolu contrôlé vers codex
arguments : exec
            --model gpt-5.6-luna
            --config model_reasoning_effort="<VALEUR_VALIDÉE>"
            --sandbox read-only
            --json
            --image <REFERENCE_LOCALE>,<DESSIN_LOCAL>   # A seulement
            --output-schema <SCHEMA_JSON_DE_TRANSPORT>
            --output-last-message <SORTIE_TEMPORAIRE>
            --cd <DOSSIER_PRIVÉ_DE_TENTATIVE>
            --skip-git-repo-check
            -
stdin     : prompt contrôlé + données sérialisées
```

`read-only` n'est qu'une couche de protection et ne signifie pas « aucun outil » ni « aucun accès en lecture aux secrets ». Les outils et permissions effectifs sont traités au §7. Les approvals runtime sont configurés pour ne jamais attendre une action interactive et ne jamais augmenter les droits. Sur la version retenue, établir explicitement le comportement équivalent à refus d'escalade.

Implémentation .NET : `ProcessStartInfo.ArgumentList`, `UseShellExecute=false`, stdin UTF-8, lecture asynchrone simultanée stdout/stderr, bornes de taille et de durée, propagation d'annulation et nettoyage. Les valeurs ne sont pas assemblées en une commande shell. Le chemin du binaire est configuré par l'opérateur et vérifié, pas reçu par HTTP.

Le worker ne réutilise pas `resume --last`. Une reprise éventuellement supportée emploie un ID exact du job et ne mélange jamais les sessions. La persistence applicative demeure la source de vérité, pas l'historique interactif de Codex.

## 5. Schémas : adapter l'enveloppe, pas les règles

Les fichiers SP-1.0 `model-a.response-format.json` et `model-b.response-format.json` contiennent une enveloppe adaptée à Responses. `codex exec --output-schema` attend un schéma JSON, pas cette enveloppe.

L'avenant fournit `contracts/codex/model-a.output-schema.json` et `model-b.output-schema.json`, extraits de leur propriété `schema`. Ils ont été extraits mécaniquement ; leur présence ne prouve pas que Luna accepte tous leurs mots-clés dans la version installée.

Au préflight, tester le sous-ensemble de schéma effectivement supporté. Si nécessaire, générer une projection de transport avec résolution locale de `$ref`, en conservant une correspondance tracée avec le schéma métier. Les contraintes non imposables par le transport restent contrôlées localement. Ne pas envoyer une réponse non conforme dans la pipeline sous prétexte que le fournisseur l'a produite.

La sortie finalisée est validée contre le schéma métier complet, les limites de taille, la profondeur, les nombres finis et l'absence de clés JSON dupliquées. La publication exige ensuite les validations sémantiques et géométriques du cahier.

## 6. Réglage maximal et préflight

La documentation consultée donne `gpt-5.6-luna` comme identifiant. La fiche modèle API publiée énumère aussi `max` parmi les valeurs d'effort ; la référence de configuration Codex et le binaire installé restent l'autorité pour le transport `codex exec`. `max` est donc le candidat initial du worker, à confirmer par le doctor actif, sans assimiler un mode produit Max/Ultra à l'enum runtime ni redescendre silencieusement vers `xhigh`.

Le résolveur de configuration doit : identifier Luna accessible pour l'authentification du worker ; lire les efforts proposés par les mécanismes disponibles ; choisir le plus élevé ; confirmer qu'il est accepté sur un appel contrôlé ; persister le choix et les preuves. Si le client dispose d'un mécanisme distinct de Max, le documenter et le tester avant de l'activer. Ne jamais inventer `gpt-5.6-luna-max`, `reasoning_effort=ultra` ou un nombre arbitraire de tokens de raisonnement.

Un appel qui accepte une configuration ne prouve pas toujours que le backend en renvoie le détail. Distinguer dans l'interface opérateur « demandé », « accepté par le transport » et « confirmé dans les métadonnées ». Quand les capacités ne peuvent pas être vérifiées, le service ne revendique pas l'exécution maximale.

Deux diagnostics sont exigés : un diagnostic local gratuit en appels modèle (versions, état d'auth, chemins, configuration, outils et isolation) ; un test actif explicitement déclenché, avec les deux images, A puis B, qui consomme l'usage normal du compte. Aucun appel modèle ne doit être déclenché par un healthcheck public régulier.

## 7. Isolation et authentification

Prévoir un utilisateur de service et un `CODEX_HOME` dédiés au runtime. L'autorisation est provisionnée par l'opérateur selon les méthodes officiellement supportées ; ne jamais recopier des secrets dans Git, un exemple `.env`, une image de conteneur, Unity ou un rapport de test.

Le worker n'hérite pas des sessions, plugins, MCP, hooks, agents ou instructions personnelles du développeur. Son contexte de travail et ses répertoires parents sont maîtrisés. Le test de configuration doit détecter les outils hérités involontairement. Il ne suffit pas d'écrire dans le prompt « n'utilise pas le terminal ».

Désactiver les outils d'exécution et autres capacités non requises au moyen de la configuration compatible avec la version retenue, puis éprouver l'absence d'accès. La référence documente notamment des contrôles `features.shell_tool` et `features.unified_exec`, mais ces deux réglages ne sont pas une preuve suffisante à eux seuls : vérifier tous les outils effectivement exposés, les permissions de lecture, les MCP/plugins et le confinement OS.

Utiliser une isolation de processus/filesystem sans privilège, sans montage du dépôt de développement, d'autres fichiers utilisateur, des clés SSH ni du socket Docker. L'accès réseau nécessaire à Codex n'autorise pas un accès arbitraire depuis des outils du modèle. Les secrets nécessaires au transport doivent rester hors d'un espace lisible via les outils ; une politique empêchant ces lectures doit être testée.

La première livraison reste un laboratoire privé avec autorisation par ressource. La documentation officielle d'authentification déconseille d'exposer l'exécution Codex dans un environnement public/non fiable et distingue accès par abonnement et par clé API. La présence d'un accès Codex personnel ne démontre ni capacité de production publique ni quota illimité. Cette architecture garde l'interface fournisseur remplaçable pour une future décision d'exploitation sans fabriquer aujourd'hui un fournisseur de secours.

## 8. Durabilité, temps et quotas

Conserver la file PostgreSQL, les leases, heartbeats et tokens d'exclusion du cahier. Séparer la durée d'attente avant traitement de la durée d'une tentative. Le worker renouvelle son lease indépendamment de l'émission de texte par Codex. Un silence de génération n'est pas automatiquement un processus mort.

Valeur de départ proposée : 1 800 secondes par tentative, configurable. Ce nombre remplace la proposition HTTP de 120 secondes pour ce transport seulement ; il n'est ni une estimation de vitesse ni un objectif de gameplay. Tester explicitement les tâches longues.

Garder au plus deux réparations structurelles et deux reprises de transport par étape, sous le plafond global de dix appels fournisseur par job. Les incidents de politique/refus, d'identifiant ou de permission ne sont pas corrigés par une boucle de retries. Une limite d'usage conduit à une attente documentée ou à `needs_operator`, avec temporisation réelle quand disponible.

Une seule publication canonique est imposée par la base. A est persisté avant B. Les tentatives incertaines sont conservées : un process tué peut avoir déjà consommé de l'usage. Le bridge ne promet pas « exactly once » chez le fournisseur. Les bornes de coût restent pessimistes jusqu'à rapprochement. Aucun changement de modèle ni reroll esthétique ne sert de reprise.

## 9. Intégration API et interface opérateur

Les routes OpenAPI existantes restent la surface du client : capture de parchemin, lecture du job, artefacts et sort compilé. Aucun endpoint de prompt libre. `POST /v1/authoring/plan` demeure réservé aux créateurs et au diagnostic de B, hors inventaire joueur.

Les diagnostics provider sont une commande opérateur privée ou une route admin ajoutée explicitement au contrat, avec contrôle d'accès. Ils ne doivent pas apparaître dans le build joueur sous forme de contrôle de modèle ou de commande serveur.

Les logs publics n'incluent ni données d'autres joueurs, ni secrets, ni prompts complets, ni événements de raisonnement. Archiver les entrées et sorties finales autorisées dans les dossiers de preuve protégés. Conserver uniquement les événements techniques nécessaires et purgés de secrets ; les métriques inconnues restent inconnues.

## 10. Nouveaux tickets de réalisation

| Ticket | Travail | Critère vérifiable |
|---|---|---|
| L01 | Configuration Luna, versions et isolation des profils | Modèle et effort documentés, aucun héritage personnel indésirable |
| L02 | `CodexProcessRunner` .NET | Arguments typés, stdin, flux, sortie finale et arrêt contrôlé testés |
| L03 | A multimodal réel | Les deux images distinctes sont réellement transmises et la sortie archivée |
| L04 | B structuré réel | Description figée traduite sans ajout/perte de clauses |
| L05 | Schémas de transport et validation locale | Projection tracée, refus des violations métier |
| L06 | `provider doctor` local et actif | Preuves datées, pas d'appel modèle de healthcheck |
| L07 | Tâches longues, quota, auth et reprises incertaines | État durable sans deuxième publication ni faux résultat |
| L08 | Confinement runtime et tests d'injection | Dessin hostile incapable d'ouvrir fichiers/outils/builds interdits |
| L09 | Build développeur distinct du runtime | Scripts reproductibles, aucune route joueur vers le code ou le compilateur |
| L10 | Recette réelle au niveau vérifié | Dessin inédit → A/B Luna → Unity → redémarrage hors ligne, preuves humaines séparées |

Ces tickets s'ajoutent à B01–B30 ; ils ne remplacent ni le rendu final ni les essais Unity.
