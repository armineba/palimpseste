# Essai de reprise du worker après panne contrôlée — 19 septembre 2026

Le smoke PostgreSQL `tests/Palimpseste.Worker.DbSmoke` a été recompilé en
Release puis exécuté sur `palimpseste_test` avec la chaîne privée
`C:\ProgramData\Palimpseste\test-db.env`. Le secret n'a pas été écrit dans le
dépôt, la ligne de commande ou cette preuve.

Résultat observé :

```text
Worker DB smoke passed: claim/lease, heartbeat, durable repair count, stale fence, uncertain incident, scheduled retry backoff, controlled crash after A and during B.
```

Le scénario ajoute une production isolée et vérifie les points suivants :

- après la fin durable de A, le lease expire ; une nouvelle clôture reprend
  avec le fence suivant et retrouve la description figée ;
- l'ancien worker ne peut plus changer l'état après la reprise ;
- pendant B, le lease expire et la nouvelle clôture retrouve la tentative
  `running` incertaine, la description de A et le fence suivant ;
- l'ancien worker ne peut plus terminer B ; l'incident est arrêté en
  `needs_operator` avec le nombre durable de tentatives attendu ;
- le scénario existant couvre aussi le backoff d'une reprise planifiée et la
  limite de deux réparations persistées.

Le test est une panne contrôlée au niveau de la base et du fencing. Il ne lance
aucun processus Codex, n'effectue aucun appel Luna et ne simule pas une coupure
réelle du serveur PostgreSQL ou une mort forcée de `JobProcessor`.

Le test de sécurité provider Release a également été recompilé et exécuté. Il
utilise exclusivement l'exécutable enfant synthétique déclaré dans le test ;
il vérifie désormais le refus fail-closed d'une sortie réussie dépourvue du
modèle rapporté (`reported_model_missing`) ou de l'effort rapporté
(`reported_effort_missing`). Aucun appel modèle n'a été effectué.
