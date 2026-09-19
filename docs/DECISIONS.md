# Décisions de réalisation

Mis à jour le 19 septembre 2026. Les arbitrages P01–P10 du cahier restent des bases de réalisation, sans validation humaine implicite.

## D01 — Compte Codex partagé, identité joueur séparée

Le créateur a précisé qu'un seul abonnement Codex déjà utilisé sur le serveur doit alimenter tous les joueurs. Le worker `PalRuntimeSvc` utilise donc le **même compte d'abonnement**, après une connexion distincte dans son `CODEX_HOME` isolé. Aucune clé API OpenAI n'est demandée ou embarquée dans Unity. Cette connexion n'identifie pas les joueurs entre eux ; l'API conserve ses identités et ses contrôles de propriétaire.

## D02 — Parcours du jeu

L'écran joueur doit montrer le dessin, l'interprétation textuelle validée de Luna A dès qu'elle est disponible, puis le sort compilé dans le laboratoire. Les champs « Service » et « Jeton privé » sont retirés. Les fixtures locales de démonstration ne sont pas présentées comme des créations joueur. Les vrais sorts téléchargés restent mis en cache pour la réutilisation hors ligne demandée initialement.

## D03 — Accès privé sans secret dans le binaire

Un opérateur délivre une invitation aléatoire à usage unique et durée de 24 heures. L'installation la place dans le profil du joueur ; Unity l'échange sur `POST /v1/session/redeem` via HTTPS, puis range le jeton joueur obtenu dans Windows Credential Manager. Le code d'invitation et le jeton ne sont pas intégrés au build. Ce jeton n'est ni une connexion Codex, ni un droit d'exécuter un processus ou de lire le serveur. L'opérateur conserve le contrôle de l'admission au laboratoire et du coût du compte partagé.

## D04 — Limite de déploiement actuelle

Le laboratoire local utilise `127.0.0.1` pour ses essais. Un nom HTTPS de serveur public, la distribution privée des invitations et les politiques de quota/admission doivent être configurés et vérifiés sur le VPS réel avant ouverture à plusieurs joueurs. Aucun déploiement distant n'est déclaré par ce journal.

## D05 — Usage de l'abonnement existant seulement

Le créateur demande de consommer uniquement le quota inclus et les crédits déjà présents sur le compte partagé, sans clé API ni achat/recharge. Sa capture montre la fenêtre **d'activation** d'une recharge automatique, bouton désactivé faute de moyen de paiement sélectionné ; elle ne prouve pas à elle seule tous les paramètres du compte. Le serveur n'intègre aucun achat de crédits. Le worker s'arrête sur refus de quota/identité ou de crédits épuisés et ne relance pas un appel déjà démarré à l'issue incertaine. Codex peut employer des crédits existants après le quota inclus selon les réglages du compte ; l'absence de recharge automatique doit rester vérifiée dans les paramètres du compte par son titulaire. Selon [OpenAI](https://help.openai.com/fr-fr/articles/12642688-utilisation-de-cr%C3%A9dits-pour-une-consommation-flexible-dans-chatgpt-freegopluspro-et-sora), une tâche Codex commencée avec un solde positif peut finir avec un solde négatif si l'usage simultané épuise les crédits. Le service limite la concurrence à une demande et ne fait aucun achat, mais il ne peut pas garantir un plafonnement exact du coût d'un tour déjà lancé. Le doctor actif coûte lui-même des appels normaux : la dernière sonde A a rapporté 13 662 jetons d'entrée et 3 648 de sortie, sans preuve du montant imputé au quota ou aux crédits.

## D06 — Cache associé au joueur authentifié

La réponse authentifiée `GET /v1/capabilities` renvoie `principal_id`. Le Player
enregistre ce propriétaire avec chaque nouveau parchemin et filtre la bibliothèque
sur l'identité courante. Hors ligne, il ne retrouve cette identité qu'avec le jeton
du coffre Windows associé à l'URL et son empreinte locale. Les anciens parchemins
sans propriétaire prouvé restent sur disque mais sont masqués ; aucune attribution
automatique à un autre joueur n'est faite.
