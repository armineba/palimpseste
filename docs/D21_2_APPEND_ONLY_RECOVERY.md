# D21.2 — reprise après un refus du schéma fournisseur

## Pourquoi D21.1 ne suffisait pas au parchemin déjà arrêté

D21.1 corrige le schéma et le classement des erreurs futures. Le job du créateur possède déjà quatre passes `blueprint` refusées par l'ancien worker, alors que les appels natifs n'avaient produit aucun candidat. Les remplacer ou les reclasser effacerait leur signification historique. La première commande qui prévoyait ce reclassement a été refusée par le contrôle automatique avant exécution ; elle n'a pas été relancée.

## Reprise sans réécrire les échecs

D21.2 prévoit une passe `blueprint_recovery` distincte. Les passes, artifacts, tentatives et compteurs initiaux sont conservés. La migration 015 étend les types de passes et reprend les contraintes de 014 ; elle ne modifie aucun job ni artifact. L'installation applique 012 puis 015, sans rejouer les contraintes plus étroites de 013/014.

La reprise passe par l'API propriétaire existante. Elle exige une description conservée, l'absence de sort/plan et d'appel actif ou incertain, puis une preuve de refus HTTP 400 `invalid_json_schema` et un schéma déployé différent de celui refusé. Un simple `codex_exit_nonzero` ou un verdict artistique négatif ne suffit pas. Le worker vérifie à nouveau la preuve avant de construire et ajoute sa réponse dans la passe de récupération.

Les vérifications du contrat, de compilation, de structure, de mouvement, d'impact et de rendu restent requises. Aucun échec artistique ne devient une réussite ; aucune publication n'est forcée. Le compteur de tentatives historiques est conservé. Les secrets de session restent dans le stockage Windows et les appels à l'API locale.

## État de livraison

Voir `NEXT_ACTIONS.md` et les preuves `evidence/public/backend/schema-recovery-d21-2*.json` lorsqu'elles existent. La compilation, l'installation, la reprise effective et le résultat du parchemin sont quatre états distincts. Aucun test de gameplay indépendant n'est lancé ; la qualité finale reste à juger par le créateur.

## Reprise réellement observée et complément D21.3

D21.2 publié (code 0), installé le 25 septembre à 11:01:46 Paris. La reprise propriétaire a répondu HTTP 202. L’unique nouvelle tentative B `571b8b7cb38845a4a7faad255747bef5` a révélé un second refus HTTP 400 : `rendering_layers.resource_id` contenait `null` dans son enum alors que son type est `string`. Elle s’est arrêtée immédiatement avec `codex_output_schema_rejected`, sans nouvelle passe artistique ni altération des quatre anciennes.

D21.3 retire cette valeur déjà impossible dans les cinq schémas V2 concernés. Cela conserve exactement l’ensemble des valeurs valides : le type `string` excluait déjà `null`. Le contrôle local vérifie désormais aussi la cohérence enum/const/type. Les exécutables D21.2 sont réutilisés, sans compilation logicielle ni build Unity supplémentaire ; seuls schémas, documents et preuve de livraison sont réempaquetés. L’état de déploiement et de reprise D21.3 est consigné séparément.
