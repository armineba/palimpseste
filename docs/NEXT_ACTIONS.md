# Point de reprise immédiat

## Point de reprise courant — dessin libre Astra → Luna

Le job **joueur** `cd4fec7e-aae8-45ba-b5f3-16a807a9771e` du dessin humain à deux traits est `ready` : Astra et Luna ont réussi, la fiche « Estoc à crochet » a été vue dans le Player et un lancement a été capturé dans l'ancien build. Le [nouveau Player VFX](../game/Build/WindowsAppearanceVfxPlayable/Palimpseste.exe) a été relancé sans interaction manuelle : ouvrir le sort conservé et comparer son rendu à la description Astra ; aucun verdict visuel humain sur ce build n'est encore enregistré. Les clics manuels répétés sur les cibles sont arrêtés. Vérifier ensuite son, physique et relecture hors ligne sur ce build. Le build Windows IL2CPP/URP a terminé avec le code 0, contient 29 fichiers contrôlés et son test PlayMode VFX/palette a passé 1/1. Le [ZIP Player courant](../deliverables/Palimpseste-Windows-x64-IL2CPP.zip) et le [ZIP backend actuel](../deliverables/Palimpseste-Backend-Windows-x64.zip) ont été régénérés ; ce dernier est réservé à l'opérateur, avec installation sur un autre PC encore non testée. L'API et le worker avec prompts A `1.7` et B `1.3` sont déployés ; le doctor local ne signale aucun problème de production, mais aucune nouvelle génération réelle avec palette n'a encore été faite. Le job **synthétique** `769eea5bac2e414f8b9d0d3c17c397c1` demeure `needs_operator/plan_invalid` après deux réparations B rejetées ; il ne compte pas comme boucle réussie. La preuve Astra de calibration `A43D24B23AD0ABE1378B708C4DC47D6FB3C487451CB39CC10F56C47AD8519D77` est distincte du job joueur.

Les sections suivantes décrivent l'ancien dessin à trois régions et le build de reconnexion précédent ; elles sont conservées comme historique.

Mis à jour le 20 septembre 2026 après l'incident d'une nouvelle capture restée locale dans un Player lancé hors ligne.

Preuve détaillée : [essai joueur, build et relecture sans API](../evidence/public/unity/owner-player-end-to-end-2026-09-20.md).

## Incident du nouveau dessin et point de reprise

Le Player PID `35804` avait démarré pendant l'arrêt de l'API. Le nouveau dessin
fermé est conservé localement : `capture_pending`, `needs_begin=true`,
`needs_capture=true`, 151 entrées de journal. Malgré le retour de `/health/ready`
à 200 et le worker actif, aucun nouveau job n'existe pour cette capture.
L'écran « transmission de la capture en attente » n'est donc pas une longue
lecture Luna. Le code ajoute le bouton « Reconnecter », qui relance la session
du laboratoire puis rouvre ce même parchemin, et sa transmission peut alors
être retentée. Une relance du Player est également possible ; conserver le
même profil Windows et ses données locales. L'utilisateur accepte de
redessiner si nécessaire, mais la première action est de vérifier la reprise
de la trace conservée.

Unity a produit `game/Build/WindowsPlayerReconnectPlayable/` : 29 fichiers,
122 095 741 octets, hashes identiques au build brut, hors deux dossiers
`DoNotShip`. EXE SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2` ;
GameAssembly SHA-256
`E379AD0EC9CB79533E319BDEB97826E480BE55D614697A66F4183AB81669D75F`.
Le journal Unity SHA-256
`46AEEE27A7CE810FA399FC4BA3017B92F5043D17B85065327268EAA1EE1AEBC8`
contient `PALIMPSESTE_BUILD_OK` et « Application will terminate with return
code 0 », sans erreur `CS`. Le code de retour de la commande PowerShell
parente n'a pas été capturé. **Le nouveau Player et la reprise du dessin ne
sont pas encore testés.** Voir la [preuve de l'incident et du correctif](../evidence/public/unity/player-reconnect-pending-capture-2026-09-20.md).

## Chaîne exécutée

Le Player Windows a envoyé une vraie capture rouge-brun courbe, distincte du trait droit utilisé pour la calibration des prompts. Le job `production` de cette capture est `ready` : deux tentatives fournisseur réussies, une description A, un plan B et un `compiled_spell`, aucune tentative en échec. Le worker isolé sous `PalRuntimeSvc` a utilisé `codex exec` avec `gpt-5.6-luna` et l'effort `max` attesté par les contrôles préalables. Le Player a affiché A « Faisceau courbe de lave », téléchargé et vérifié la fiche, ouvert le laboratoire et compté un lancement (`Lancers : 1`, `Dégâts : 0`). Ce plan ne prévoit pas de dégâts.

Le correctif de visibilité du rayon à `0,6 s` a passé 2/2 tests PlayMode sur le **paquet du joueur**, puis Unity 6000.3.24f1 a terminé le build Windows IL2CPP/URP avec le code 0. Le build précédent testé est `game/Build/WindowsPlayerBeamVisibleReady/` : 29 fichiers, 122 093 149 octets ; EXE SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`, `GameAssembly.dll` SHA-256 `9FB651D09680CAF09B0A8668BEF2A914EFDE6B61F930EDBAE1CE90AF3B5C18C9`. Une capture de ce Player montre l'effet visible puis disparu.

L'API précédente (PID `25848`) a été arrêtée. Le Player BeamVisibleReady précédent a été relancé (PID `35804`) et la bibliothèque, la fiche, le laboratoire ainsi qu'un lancement du même sort sont restés accessibles **sans API**. L'API a ensuite redémarré sous `PalRuntimeSvc` (PID `16420`) ; `http://127.0.0.1:18080/health/ready` a répondu 200. Le worker PID `8180` est resté actif. Cet essai démontre la relecture locale lors d'une indisponibilité de l'API ; il ne mesure pas la qualité artistique ni l'écoute du son.

Une commande de confort pour fermer puis relancer le Player avec l'API restaurée a été refusée **avant exécution** par la revue automatique (`blocked by policy`, sans motif détaillé). Elle n'a pas été réessayée par un moyen équivalent. Le Player PID `35804` reste ouvert dans la session issue de l'essai sans API ; l'API PID `16420` répond 200 et le worker PID `8180` reste actif. Aucune relance du Player en ligne après cette restauration n'est revendiquée.

## Sécurité et preuves de génération

Le Doctor installé a pour SHA-256 `AAAAB2686CD9A33ADB6130B205D10596A63AF8207CBAE580ACD723C862196811`. Son diagnostic local après correction du BOM a réussi sous le compte de service, sans appel modèle ni `production_issues`. Le worker installé a pour SHA-256 `9243F9600973B1AAF5F97F07AD0983FF60A7EFA2EB0A7F7D42D142C05015847D` et son script enfant `03CE0108D67A4AC27B332A41F36F1F12B2B362FA9A2AEBCF25EB21E8BDB4C104`.

Le verrou technique `PALIMPSESTE_EFFORT_VERIFIED=true` référence le manifeste composite v2 SHA-256 `56F349016A5B63D352DD37225AF9253889D496C2399F58F97F70D0DAAC353E3F` dans `E:\PalimpsesteRuntime\approved-evidence`. Il lie trois rapports intégraux : appel A réel, B réel repris sur l'A figée et géométrie issue de l'encre, puis validation et compilation hors ligne de leurs octets. Les fichiers A/B/encre originaux et leurs hashes restent nécessaires au démarrage. Les ACL donnent au service la lecture/exécution des preuves et binaires, sans modification. `runtime.env` est en UTF-8 sans BOM ; `DATABASE_URL` est lue dans le coffre privé par le processus enfant, sans secret dans le dépôt, le Player ou la ligne de commande.

La première lecture A « faisceau de feu visuel » du trait de calibration a été rejetée par l'auteur. A `1.3` décrit un faisceau de lave sans cible ni dégâts ; B `1.1` repris sur la géométrie réelle a été validé et compilé. Ces diagnostics servent à la compatibilité technique et restent distincts du job joueur. Le verdict humain de fidélité sur A `1.3`, puis celui sur la nouvelle capture courbe et son sort, ne sont **pas enregistrés**. Le passage du job à `ready` n'est pas un accord artistique.

Le laboratoire reste privé sur `127.0.0.1` avec invitation propriétaire ; aucun proxy Codex public n'est ouvert. Les migrations 003 à 006 et les contrôles d'accès sont en place. Le compte Codex commun n'a déclenché aucun achat ou rechargement par le code ; l'état des réglages de recharge du compte n'est pas vérifié. Les [conditions OpenAI Europe](https://openai.com/fr-FR/policies/eu-terms-of-use/) et la [documentation Codex](https://developers.openai.com/fr-FR/docs/auth) demandent une clarification de l'usage de ce compte individuel pour des tiers avant toute ouverture à plusieurs joueurs. Cette lecture de leur application au backend partagé est une inférence à confirmer.

## Travaux restants

1. Lancer le build `WindowsPlayerReconnectPlayable` dans le même profil Windows, ouvrir le dessin local en attente et essayer « Reconnecter », puis « Transmettre » si proposé. Vérifier qu'un nouveau job est créé et que la lecture A apparaît. En cas d'échec, conserver le journal local et relever le message exact avant de redessiner. Aucune réussite de cette reprise n'est encore attestée.
2. Faire écouter le son et recueillir les verdicts des créateurs sur le texte A, le rendu, l'intérêt du sort et l'expérience de dessin ; enregistrer ces décisions séparément de la preuve technique.
3. Exécuter la recette finale sur les 30 dessins inédits, avec le second créateur et une machine Windows propre. Le seul job propriétaire réussi ne satisfait pas M7.
4. Vérifier l'installation et le parcours sur un autre poste, puis décider de la livraison publique, de l'HTTPS et du cadre d'usage du compte avant ouverture à des joueurs externes.
5. Livrer le dossier Windows actuel. La création du ZIP actualisé a été refusée avant exécution par la revue automatique (`blocked by policy`) ; les ZIP existants restent historiques. Le démarrage de l'API, lui, fonctionne via `ops/start-owner-api.ps1` sans secret dans l'appel de l'outil.

Les détails du worker et les hashes des preuves sont dans [OWNER_WORKER_CUTOVER.md](ops/OWNER_WORKER_CUTOVER.md). Le parcours sur ce PC est dans [TESTER_MAINTENANT.md](TESTER_MAINTENANT.md). Les statuts par ticket et limites sont dans [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md).
