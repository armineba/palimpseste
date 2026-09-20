# Capture en attente après le retour du laboratoire

Observation du 20 septembre 2026 sur le Player privé du propriétaire. Un
parchemin dessiné dans le Player était affiché comme « transmission de la capture
en attente » sans bouton « Transmettre ». Ce délai n'était pas un traitement Luna.

Le Player courant (PID `35804`) avait démarré pendant l'arrêt volontaire de
l'API. Son `Player.log` signale `Curl error 7` vers `127.0.0.1:18080`. Après
restauration de l'API (`/health/ready` HTTP 200, worker actif), le Player ne
réessayait pas la connexion : `BootstrapSession` avait effacé sa configuration
active et l'écran de traitement masquait « Transmettre » quand cette
configuration était absente. L'API ne contenait aucun nouveau job pour ce
dessin.

Le parchemin était toujours conservé localement : `state=capture_pending`,
`needs_begin=true`, `needs_capture=true`, `job_id` vide, fermeture
`all_regions_locked` et 151 entrées de journal. Aucun nouveau dessin n'était
nécessaire. Fermer puis rouvrir l'ancien Player avec l'API disponible relance
son initialisation et devrait reprendre cette capture ; cette reprise précise
n'avait pas encore été observée au moment de cette preuve.

Le correctif de `PalimpsesteApp` affiche désormais l'état hors ligne et un
bouton « Reconnecter » pour une capture ou un job en attente. Il recharge la
session, retrouve le même parchemin parmi les données autorisées pour
l'identité authentifiée, puis appelle `Open`, qui déclenche la transmission ou
le suivi. Il ne supprime ni ne remplace le journal local.

Unity 6000.3.24f1 a produit le nouveau build brut
`game/Build/WindowsPlayerReconnectReady/`. Son journal contient
`PALIMPSESTE_BUILD_OK`, zéro erreur `CS` et « Application will terminate with
return code 0 » ; SHA-256 du journal privé
`46AEEE27A7CE810FA399FC4BA3017B92F5043D17B85065327268EAA1EE1AEBC8`.
La commande parente `Start-Process -Wait` a été interrompue après disparition
du processus Unity : son propre code de sortie n'a pas été capturé.

Le dossier jouable `game/Build/WindowsPlayerReconnectPlayable/` exclut les deux
dossiers Unity marqués `DoNotShip`. Il contient 29 fichiers, 122 095 741 octets,
et ses 29 empreintes correspondent au build brut. `Palimpseste.exe` SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2` ;
`GameAssembly.dll` SHA-256
`E379AD0EC9CB79533E319BDEB97826E480BE55D614697A66F4183AB81669D75F`.
Le Player corrigé n'avait pas encore été lancé ni son bouton « Reconnecter »
essayé sur le parchemin du propriétaire au moment de la rédaction.
