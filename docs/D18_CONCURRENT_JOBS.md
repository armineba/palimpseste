# D18 — Construction de plusieurs parchemins en parallèle

## État

La modification est compilée, publiée et installée. Le déploiement D18 s'est terminé le **24 septembre 2026 à 20:52:32 UTC**, avec les services redémarrés, `max_concurrent_jobs=0` et `claims_paused=false`. Le [rapport d'installation](../evidence/public/backend/parallel-jobs-d18-installation.json) consigne `completed=true` et les empreintes des exécutables installés. Le Player Unity reste en version `1.6` : la modification porte sur le service de génération.

Aucun test, sort de démonstration ni rendu de validation n'a été lancé par l'agent. Le créateur teste lui-même les sorts. Les preuves et le point de reprise doivent indiquer séparément la publication, l'installation réelle et l'acceptation humaine.

## Concurrence des générations

- `PALIMPSESTE_MAX_PROVIDER_CONCURRENCY=0` supprime le plafond applicatif du nombre de jobs actifs. Une valeur positive impose une borne choisie par l'opérateur ; une valeur invalide bloque le démarrage.
- Un seul processus worker admet plusieurs parchemins et exécute leurs traitements asynchrones indépendamment. Chaque parchemin conserve l'ordre de ses propres étapes.
- Chaque job possède son bail de `180 s`, son renouvellement toutes les `10 s`, son jeton de fencing et son annulation. Un incident sur un parchemin n'arrête pas les autres.
- Les tâches terminées sont retirées et leurs résultats observés. L'arrêt attend la fin de leur nettoyage ; le dispatcher n'accumule pas les tâches historiques.
- Les captures Unity partagent le GPU : une seule capture s'exécute à la fois dans le worker. Leur attente est annulable et reste hors du délai de capture de `120 s`. Les appels de génération des autres parchemins peuvent continuer pendant cette étape.

Le réglage enlève la file séquentielle imposée aux jobs par l'application. Il n'augmente ni le quota de l'abonnement Codex ni les capacités du fournisseur, du GPU, de la mémoire ou des connexions à la base. Les processus Codex natifs partagent leur `CODEX_HOME` dédié ; leur fonctionnement simultané n'a pas encore été démontré par une génération réelle.

Aucun achat, rechargement de crédits, changement de modèle ou de niveau d'effort n'est ajouté. Les limites de sortie, contrôles de données et protections d'isolation restent appliqués.

## Arrêt après les générations en cours

La nouvelle version peut recevoir le chemin opérateur `PALIMPSESTE_WORKER_DRAIN_FILE`, fixé au fichier `worker-drain.request` dans le répertoire de l'exécutable Codex — sur cette installation : `E:\PalimpsesteRuntime\bin\worker-drain.request`.

Le chemin doit être absolu, avoir ce nom exact et ce répertoire parent, sans reparse point. Le joueur ne peut pas le configurer. En l'absence de cette variable, cette commande de drainage est désactivée.

Lorsque le dispatcher voit le fichier, il cesse de réclamer de nouveaux jobs. Cette décision reste acquise même si le fichier est ensuite retiré. Les jobs déjà admis gardent leur bail et terminent normalement ; leur génération n'est pas annulée. Le worker attend toutes ses tâches puis quitte normalement. Le fichier doit être retiré par l'opérateur avant de redémarrer l'admission.

L'ancienne version D17 ne comprend pas ce fichier. Pour cette première installation, le script a suspendu temporairement ses nouvelles prises de jobs dans la base, puis attendu la fin naturelle des générations déjà engagées. Le mécanisme employé est décrit ci-dessous ; créer le fichier seul ne permet pas de drainer D17.

## Déploiement

Le script `ops/deploy-lifecycle.ps1` accepte `-JobConcurrency 0 -WaitForIdleSeconds 3600 -DrainExistingJobs`. Il configure la concurrence sans plafond et le chemin de drainage après remplacement. Son attente peut durer au maximum une heure ; si des jobs restent actifs à l'expiration, il échoue avant d'arrêter les services.

Avec `-DrainExistingJobs`, une fonction et un trigger PostgreSQL temporaires suspendent uniquement les mises à jour qui augmentent le `fence_token`, donc les nouvelles prises de jobs. Les heartbeats, les écritures des générations déjà engagées et les nouveaux envois HTTP restent possibles. Les jobs simplement en file ou en attente d'une reprise n'empêchent pas le remplacement ; toute tentative fournisseur encore `running` demeure comptée comme active.

Le script enregistre les OID du trigger et de sa fonction. Il refuse un mécanisme déjà présent et vérifie leur identité avant de les retirer. Le retrait intervient avant le démarrage D18, permettant au nouveau worker de reprendre la file. Un bloc `finally` tente également ce nettoyage si le déploiement échoue ; si l'identité ne correspond plus ou si le retrait échoue, l'opérateur doit examiner le rapport au lieu de supprimer aveuglément les objets. L'installation effectuée confirme `claims_paused=false`.

Le déploiement conserve un seul processus worker. Il n'exige pas de nouveau Player Unity. Consulter `docs/NEXT_ACTIONS.md` et les preuves de livraison pour connaître l'état effectivement installé.
