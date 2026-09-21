# D16.1 — Une critique invalide ne perd plus le travail déjà évalué

## Incident observé le 21 septembre 2026

Le parcours joueur « Foudre sous l’Enclume » a réellement réussi A, G, B, deux critiques J et deux raffinements B. La troisième critique a renvoyé un `plan_sha256` différent du plan fourni. Son score arithmétique était cohérent ; son rattachement au plan ne l’était pas. Le fournisseur a rejeté le verdict avec `visual_judgement_invalid`, et la boucle a immédiatement bloqué le job en `visual_judge_failed` après **8 appels**, sans atteindre sa sélection habituelle du meilleur candidat déjà évalué.

La planche 1536 × 1152, l’atlas natif et les trois versions du plan sont conservés. Les deux critiques admissibles donnent **3420/10000** et **4190/10000**, avec `lifecycle_faithful=false`. Le troisième verdict n’est pas admis et ne contribue ni score ni acceptation. Aucun de ces scores ne signifie une fidélité artistique satisfaisante.

## Correction générale

- Les trois empreintes attendues par J sont fixées comme valeurs uniques dans le schéma privé de chaque tentative, depuis les entrées immuables du serveur. Le schéma source reste intact ; aucun hash n’est extrait du texte du modèle ou corrigé silencieusement dans sa sortie.
- Le contrôle du verdict distingue une erreur de rattachement, de somme, de bornes ou de contenu, avec des codes précis.
- Après un J définitivement invalide, la boucle peut finaliser son meilleur candidat **déjà évalué avec succès**. Le verdict rejeté reste rejeté. Le score, la provenance et les données du candidat choisi restent ceux de son historique.
- Une reprise peut effectuer cette sélection avant capture ou appel modèle, uniquement si la dernière tentative est ce J invalide, après la dernière révision validée, sans tentative en cours ou incertaine. La finalisation recompile et vérifie le candidat choisi.
- Une violation d’isolation, un refus ou un transport incertain ne devient jamais une autorisation de poursuivre par cette branche.

Le Player 1.6.0 et les assets restent identiques ; seul le backend est republié. Aucun nouveau test, génération ni capture indépendante n’est demandé. Les prochains J avec schéma fixé restent à observer lors d’une future création joueur.

## Livraison et reprise

Le backend est publié et installé avec le code 0. Le job incident est **ready depuis 07:33:56 Paris**, avec la révision 1, **toujours 8 appels**, et **toujours 3 dossiers de capture**. Le paquet publié conserve le plan choisi, avec seulement l’omission habituelle par le compilateur d’un champ nul. Son score admissible reste 4190/10000 et `lifecycle_faithful=false`.

État réel dans [NEXT_ACTIONS](NEXT_ACTIONS.md) et [la preuve du correctif](../evidence/public/backend/visual-judge-fix-d16-1.json). Le dessin, la planche et les plans n’ont pas été recréés. La reprise ne valide pas par un appel réel la nouvelle contrainte de schéma J : aucun modèle n’a été appelé. La fidélité visuelle reste à juger par le créateur.
