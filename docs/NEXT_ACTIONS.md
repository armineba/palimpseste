# Point de reprise immédiat

Mis à jour le 20 septembre 2026.

Etat additionnel du 20 septembre : les ACL du runtime vivant ont ete
verifiees sous PalRuntimeSvc (configuration/preuves/binaires RX,
dossiers de travail inscriptibles, parent E: sans DeleteChild). Le script
ops/provision-runtime.ps1 est corrige mais non rejoue sur ce runtime.
Les candidats .NET API/doctor/worker sont publies dans un staging
operateur, non installes. Le correctif source Codex tag + patch local
pour attester modele/effort serveur passe des tests cibles; sa revue a
revele un chemin de compaction corrige dans la source en cours de
validation. Aucun nouveau tour Luna ni B reel. Voir
docs/ops/CODEX_CUTOVER.md et evidence/public/backend/runtime-acl-live-2026-09-20.md.

Point du soir : le doctor local a de nouveau quitté avec le code 0 sous
`PalRuntimeSvc`, sans appel modèle, au moyen du lanceur
`ops/run-service-doctor.ps1`. Le patch Codex a passé ses tests ciblés
d'app-server mais son binaire Release est encore en construction. L'API
inclut un plafond de 3 nouvelles générations par joueur et 12 globalement
sur 24 heures, testé sous concurrence dans `palimpseste_test`. Sa
publication candidate est en staging opérateur, non installée. Le worker
reste arrêté. Voir `docs/GENERATION_QUOTA.md` et la preuve publique du
doctor.

1. Connexion Codex dédiée réussie sous `PalRuntimeSvc` et doctor local gratuit réussi. Ne pas copier `auth.json` du profil personnel. Après correction des 17 `const` sans `type` des schémas stricts A/B, une passe réelle `codex exec` A a produit un JSON conforme (2 225 octets, SHA-256 `5e938a495f7199d58c7460ebcd734b0490d099f0bac7afd93ff821d02ce5afad`) et consommé 13 662 jetons d'entrée et 3 648 en sortie. Le doctor l'a refusé sur `reported_model_missing` : ses événements ne donnaient ni modèle ni effort effectifs. B n'a pas été lancé. Ne pas présenter A comme accepté ni un sort comme généré.
2. L'inspection locale sans nouvel appel n'a trouvé aucune preuve rétroactive du modèle `gpt-5.6-luna` et de l'effort `max` effectivement appliqués : `codex exec --json` ne les expose pas dans la tentative A conservée, et `--ephemeral` n'a laissé aucun rollout. La télémétrie OTel documentée pourrait attester la requête envoyée et sa réussite de transport, sans attester à elle seule les valeurs serveur. Chercher une sortie officielle ou un correctif vérifiable de la CLI pour ces champs ; garder le refus de production tant que la preuve manque. Les seuls quota et crédits déjà présents sont autorisés, sans achat/recharge, modèle de secours ni substitution d'effort. Ne pas confondre doctor avec la recette d'un dessin inédit dans Unity.
3. L'exposition propriétaire de la description A et l'UI Unity dessin → texte → labo sont codées. Le contrat API a passé le smoke 18/18 avant l'ajout de `principal_id` ; la nouvelle réponse `capabilities` compile et passe QA documentaire 61/61, dont deux contrôles statiques des schémas de transport. Après filtrage du cache par propriétaire : EditMode 11/11, PlayMode 2/2, build IL2CPP réussi et Player brut lancé hors ligne. L'essai connecté avec une vraie description A reste à faire.
4. Les migrations `003_player_invitations.sql` et
   `004_generation_quota.sql` ont été appliquées à `palimpseste_lab` après
   sauvegarde vérifiée. L'échange d'invitation a été testé sur
   `palimpseste_test` ; le contrôle concurrent de 004 a été testé dans
   un schéma isolé de cette même base. Déployer ensuite l'API candidate,
   vérifier la route de santé et l'invitation sur le laboratoire. Un nouvel
   invité ne doit jamais être ouvert sans invitation privée.
5. Le ZIP joueur actualisé a été refusé avant exécution par la revue automatique (`blocked by policy`) ; les archives dans `deliverables/` sont historiques. Tester le Player brut reconstruit, mettre à jour `docs/IMPLEMENTATION_STATUS.md` et `MANIFEST.sha256`. Réaliser un dessin humain neuf avec les vrais appels A/B et vérifier son sort dans le Player ; enregistrer séparément le verdict humain.

Le dépôt contient les sources et un Player IL2CPP brut après reconstruction de l'isolation par propriétaire. Les archives ZIP restent antérieures au nouveau parcours joueur.
