# Point de reprise immédiat

Mis à jour le 20 septembre 2026.

Le CLI Codex patché et le doctor publié sont maintenant installés dans le
runtime. Le doctor local a réussi sous `PalRuntimeSvc` sans appel modèle,
sa preuve de fonctionnalités a été approuvée et référencée par le runtime.
Une unique sonde active a ensuite réussi A puis B avec `gpt-5.6-luna` et
`max` rapportés pour chaque étape. Preuve :
`evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`.
La preuve active reste en attente de revue humaine, et
`PALIMPSESTE_EFFORT_VERIFIED=false`. Une vérification séparée a recalculé la
géométrie depuis A et l'encre réelle et compilé B en mémoire, sans appel
modèle supplémentaire. Le paquet de vérification n'a pas été publié ni chargé
dans le Player joueur. Un test Unity PlayMode isolé `-nographics` a depuis
chargé et lancé ce paquet : 1/1 passé, faisceau deux points, `AudioSource`,
zéro dégât et expiration après un tick. La rémanence graphique de 0,16 s
a ensuite passé les contrôles PlayMode et Direct3D 12 : 13 616 pixels rouges
dans une `RenderTexture` isolée après l'expiration logique, zéro après
nettoyage, sans prolonger le porteur physique. La caméra du Player et l'écoute
du son restent à vérifier. L'API actualisée et le worker recompilé sont en staging opérateur,
non installés ; le worker reste arrêté. Le worker candidat a pour SHA-256
`D04A81E54EF0FBFE8FFFCE6A121DABA1DCE0FB2C16F7B163CCF3EA1EA0E08CAA`.
Le nouveau Player IL2CPP/URP avec rémanence a été construit avec succès puis
lancé hors ligne 12 s, répondant ; copie jouable
`game/Build/WindowsPlayerLunaAfterimagePlayable/`, exécutable SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`.
Son `service.json` est vide et aucun sort Luna n'y a été reçu. Voir
`evidence/public/unity/real-luna-player-build-2026-09-20.md`.
Le dernier candidat API est `operator-staging/api-core-2026-09-20/Palimpseste.Api.exe`
(SHA-256 `F01901EBF2D1A71F670FC609F8696F322D8DD0489379B8935BA322FCEF1C9F52`) ;
l'ancien `operator-staging/api/` est périmé pour ce Core. L'API inclut
un plafond transactionnel de 3 nouvelles générations par joueur et 12 au
total par 24 heures, testé sous concurrence dans `palimpseste_test`.
Les migrations 003 et 004 sont appliquées au laboratoire local après
sauvegarde. HTTPS public et invitation joueur restent à préparer.

Avant toute ouverture à plusieurs joueurs, clarifier le type de compte et
l'autorisation d'un usage serveur partagé. La capture actuelle indique
« Usage personnel » ; les [conditions OpenAI Europe](https://openai.com/policies/eu-terms-of-use/)
interdisent de mettre un compte individuel à la disposition d'autrui. Notre
lecture selon laquelle la génération pour des tiers via ce backend pourrait
entrer dans cette interdiction est une **inférence à confirmer**, même si les
identifiants restent secrets. Les essais locaux du propriétaire ne constituent
pas une validation du déploiement public.

1. La vraie description A, le vrai plan B et l'encre du dessin ont passé `Palimpseste.Core.RealProbe` : deux géométries, un masque, validation B et compilation contrôlée réussies, paquet de vérification SHA-256 `856D1B37BBE41C6372B0319112053DFDCC9ADD6D8F1925AB2E5214336AE19C37B`. Ce paquet a ensuite passé un test Unity PlayMode isolé `-nographics` 1/1. Ses identifiants d'artefacts et sa provenance sont synthétiques. La durée mécanique d'un tick est conservée ; la rémanence graphique de 0,16 s a été validée sans collider, audio persistant ou dégât, puis ses pixels ont été mesurés sur une caméra URP isolée. La prochaine vérification du parcours joueur est un job réel avec artefacts publiés, paquet téléchargé et sort lancé depuis la caméra du Player. La fidélité de la description et du plan attend le verdict du créateur.
2. Revoir humainement la preuve active privée A/B SHA-256 `FD39E1220BF540C543184D56E3F3314FF0E2BF440BB65073EF219B031CD3E6F5`. Si elle est acceptée, en copier les octets dans `approved-evidence` avec ACL protégée, vérifier son hash puis ouvrir uniquement la porte d'effort prévue dans `runtime.env`. Ne pas copier `auth.json` du profil personnel ni les preuves privées dans Git. Le compte commun ne doit utiliser que son quota et ses crédits existants : aucun achat, aucune recharge, aucun modèle de secours ni effort inférieur. Le plafond applicatif de jobs ne constitue pas une limite monétaire OpenAI.
   À l'activation du worker, injecter `DATABASE_URL` dans son environnement privé puis vérifier sa lecture sous le compte de service : `runtime.env` ne contient pas cette clé, requise par `Palimpseste.Worker/Program.cs`. La chaîne est disponible dans le coffre privé `C:\ProgramData\Palimpseste\lab-db.env` ; elle ne doit pas être copiée dans le dépôt ni donnée au worker tant que la porte humaine demeure fermée.
3. L'exposition propriétaire de la description A et l'UI Unity dessin → texte → labo sont codées. Le contrat API a passé le smoke 18/18 avant l'ajout de `principal_id` ; la nouvelle réponse `capabilities` compile et passe QA documentaire 61/61, dont deux contrôles statiques des schémas de transport. Après filtrage du cache par propriétaire : EditMode 11/11, PlayMode standard 2/2, PlayMode Luna isolé 1/1, build IL2CPP avec rémanence réussi et copie jouable lancée hors ligne. L'essai connecté avec une vraie description A reste à faire.
4. Les migrations `003_player_invitations.sql` et
   `004_generation_quota.sql` ont été appliquées à `palimpseste_lab` après
   sauvegarde vérifiée. L'échange d'invitation a été testé sur
   `palimpseste_test` ; le contrôle concurrent de 004 a été testé dans
   un schéma isolé de cette même base. Déployer ensuite l'API candidate,
   vérifier la route de santé et l'invitation sur le laboratoire. Un nouvel
   invité ne doit jamais être ouvert sans invitation privée.
5. Le ZIP joueur actualisé a été refusé avant exécution par la revue automatique (`blocked by policy`) ; les archives dans `deliverables/` sont historiques. La nouvelle copie jouable IL2CPP a été construite et lancée hors ligne, sans trajet réseau. Après levée des portes de déploiement, réaliser un dessin humain neuf avec les vrais appels A/B, vérifier son sort dans ce Player et enregistrer séparément le verdict humain. Mettre à jour le manifeste après la dernière modification du dépôt.

Le dépôt contient les sources et un Player IL2CPP jouable hors ligne avec la rémanence visuelle. Les archives ZIP restent antérieures au nouveau parcours joueur.
