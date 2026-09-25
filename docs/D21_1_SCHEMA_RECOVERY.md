# D21.1 — interruption de construction après la description

## Cause observée

Le premier parchemin V2 « Tarière des strates », job `5e763870-284e-4167-9c6b-850b9b02ceff`, a conservé sa description et sa recherche. Les quatre tentatives B ont toutes reçu un HTTP 400 `invalid_json_schema` : la propriété `carrier` du schéma de sortie n'avait pas de `type`. Aucun plan, rendu ou verdict artistique n'avait été produit.

La boucle traitait cette panne de configuration comme quatre candidats refusés. Elle terminait avec `v2_validation_rejected`. L'interface affichait « Image du sort » parce que l'étape d'origine avait été remplacée par `needs_operator`.

## Correction générale

- Les constantes des deux schémas B V2 deviennent des valeurs uniques typées (`type` et `enum`), sans élargir les valeurs admises. Les contrats métier locaux restent identiques.
- Un contrôle structurel des trois schémas V2 précède désormais chaque publication. Il vérifie notamment types, objets fermés, champs requis et références. Il ne prétend pas prouver l'acceptation par le fournisseur. [Contraintes officielles consultées](https://developers.openai.com/api/docs/guides/structured-outputs).
- Une panne fournisseur arrête sa tentative et conserve son code exact ; elle ne consomme plus les quatre révisions artistiques. Refus, isolation et transport incertain restent protégés.
- L'étape de travail est conservée lors d'un incident.
- Le compteur technique V2 peut couvrir ses quatre constructions et leurs critiques (32 tentatives au total, historique inclus). V1 conserve sa borne de 10 ; le nombre de jobs simultanés reste sans plafond applicatif.
- La migration 014 autorise l'archivage des anciennes fausses passes de rejet sous `provider_failure`, sans supprimer leurs artifacts ni les tentatives fournisseur. Elle inclut l'évolution 013 ; le déploiement ne rejoue pas 013 sur ces nouvelles lignes.

## Reprise du parchemin

D21.1 est réellement installé ; publication et installation sont prouvées dans `evidence/public/backend/schema-recovery-d21-1.json` et `schema-recovery-d21-1-installation.json`.

La commande groupée qui prévoyait de reclasser les quatre anciennes passes a été refusée avant exécution par le contrôle automatique. Ces passes sont restées intactes. La reprise D21.2 conserve les lignes et artifacts initiaux : elle vérifie la cause native et ajoute une passe distincte par révision. Le parcours propriétaire de l’API autorise cette reprise seulement pour un refus de schéma confirmé et corrigé.

Aucun test de gameplay indépendant n’a été lancé. Les appels ultérieurs éventuels appartiendront au parchemin du créateur, pas à une génération de démonstration.
