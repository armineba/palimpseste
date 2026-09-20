# API du laboratoire propriétaire démarrée localement — 20 septembre 2026

`ops/start-owner-api.ps1` a lancé `Palimpseste.Api.exe` depuis `E:\PalimpsesteRuntime\api` par un enfant PowerShell masqué exécuté sous `DESKTOP-457AH3U\PalRuntimeSvc`. Le lancement lit le secret de base dans l'enfant depuis le fichier privé `C:\ProgramData\Palimpseste\lab-db.env` ; ni le mot de passe ni une chaîne de connexion ne figurent dans les arguments du processus ou dans cette preuve. L'API installée avait pour SHA-256 `B9A5AD2EF920C1955333CD2F21B47E567DCBE7E3396CA123C971B4F8D2219545`.

Le contrôle Windows a vu `Palimpseste.Api.exe` PID 25848 et son parent PowerShell PID 22372 sous cette identité, avec une écoute sur `127.0.0.1:18080` uniquement. `/health/live` et `/health/ready` ont répondu HTTP 200 ; un nouveau contrôle de `/health/ready` a encore répondu 200 pendant que le lecteur propriétaire était ouvert. Le lanceur réexécuté a renvoyé `already_running=True`, `process_id=25848`, sans créer une seconde instance.

Les journaux restent dans `E:\PalimpsesteRuntime\evidence\pending\owner-api-6c26c8fda0bd49fab3905291e8735401.{stdout,stderr}.txt`. L'API n'a pas été exposée sur le réseau public. Ce contrôle ne démontre pas que le worker Luna joueur tourne ; il est resté fermé.

Le précédent démarrage qui plaçait l'environnement privé dans l'appel de l'outil avait été refusé avant exécution avec `blocked by policy`, sans motif plus détaillé. Le lanceur enfant évite de placer la valeur privée dans cet appel ; il a réellement démarré et été contrôlé. L'archive ZIP du Player courant avait subi un refus distinct et n'a pas été créée par ce travail.

Une revue du lanceur a ensuite trouvé que certaines variables API pouvaient
encore être héritées de l'environnement. Le script enfant installé a été
durci : il fixe maintenant les deux chemins d'assets déjà contrôlés, les
versions de catalogue et de règles, la version client et les plafonds de 3/12
jobs par 24 heures. Sa source et sa copie installée ont le même SHA-256
`E5E73543F951D65EE76493EA68ACDA3DFC7944520F1D6988D99355FDEC3C09E8` ;
la copie garde les droits lecture/exécution du service et contrôle total des
administrateurs et du système. Le lanceur renvoie toujours le PID réel de
l'API lorsqu'elle écoute. Sa relance idempotente a encore rapporté PID 25848
et `ready=True`. Le processus API déjà en marche a démarré avec le script
précédent ; le script durci sera appliqué lors de son prochain démarrage.
Le lanceur opérateur fixe désormais aussi le répertoire runtime du laboratoire,
refuse les points de réanalyse sur les chemins de l'API et de son script enfant,
et compare leurs SHA-256 aux versions relues. L'analyse syntaxique PowerShell
a réussi et une nouvelle relance idempotente a confirmé PID 25848, prêt. Ces
contrôles de lancement n'ont pas redémarré l'API actuelle.
Un essai avec `-RuntimeRoot` pointant vers un autre dossier a été refusé avant
toute lecture de script enfant (`Owner lab runtime root differs from the
reviewed path.`, code 1).
