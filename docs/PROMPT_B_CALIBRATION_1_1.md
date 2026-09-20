# Calibration de traduction B · candidat `sp.prompt.b/1.1`

Le diagnostic A/B réalisé après la proposition A `1.2` a exposé deux erreurs
distinctes du plan B : `carrier_target`/`target_fact` lorsque B a choisi
`hostile` sans fait `target` dans A, et `beam_interval` pour un faisceau
entretenu de 50 ticks avec `tick_interval = 1`. Le compilateur contrôlé a
rejeté ce plan ; il n'a pas été publié comme sort joueur. Ces codes désignent
des vérifications métier, pas une panne Unity.

Le prompt B `1.1` précise la règle générale : les filtres des porteurs et des
effets dérivent des faits `target` du même sujet et de leurs clauses. Il fixe
la cadence d'un faisceau entretenu à au moins 5 ticks ; un faisceau d'un tick
reste instantané. Il interdit d'ajouter des sauts de chaîne absents de A.
L'ancien texte exact reste dans `prompts/history/02_MODEL_B_TRADUCTEUR_1_0.md`.

**Limite de cette correction :** la description A `1.2` de cette sonde
contient un effet `damage`/`hit`, mais aucun fait `target`. B ne peut pas
choisir `hostile` ou `all_actors` sans source, et ne peut pas retirer les
dégâts pour obtenir un plan valide. Un nouveau A doit produire une description
cohérente, après la décision humaine sur ce que le dessin justifie. Le
validateur continuera à refuser un plan qui invente sa cible. La correction de
cadence, seule, ne rend donc pas ce cas compilable.

Le provider vérifie l'en-tête de B `1.1` au chargement et calcule le SHA-256 du
texte chargé. Le worker inclut ce hash dans l'empreinte d'entrée des nouvelles
tentatives B et enregistre la version de B dans `spell_plans.prompt_version`.
La migration `006_plan_prompt_version.sql` attribue `1.0` aux plans déjà
figés, car le worker précédent n'avait qu'un prompt B `1.0`. Lors d'une
reprise de compilation, le paquet reprend la version du plan enregistré,
sans l'étiqueter à tort `1.1`. Cette migration doit précéder le nouveau
worker. Le dossier de spécification privé déployé contient B `1.1` (SHA-256
`2900DD88FC657C85868C0F37F63B6DCFFE7C6110BA34241CD175E2F1B70AD279`) ;
une version différente serait refusée au chargement.

Vérifications locales de ce changement : compilation de la solution .NET
Release sans avertissement ; migration 006 d'abord exécutée sur une **table
temporaire isolée** de `palimpseste_test`, avec ancien plan réétiqueté `1.0`,
nouveau plan `1.1` accepté et insertion sans version rejetée. La migration a
ensuite été appliquée aux bases `palimpseste_lab` et `palimpseste_test` après
sauvegarde du laboratoire ; voir la [preuve](../evidence/public/backend/migration006-owner-lab-2026-09-20.md).

Une première sonde A `1.3`/B `1.1` réelle a obtenu un faisceau sans effet et
une géométrie de signature provisoire. `RealProbe` a rejeté cette sortie B,
car sa silhouette n'existait pas dans la résolution réelle. Le doctor a été
corrigé pour dériver les géométries d'A et du PNG d'encre, comme le worker.
Un nouvel appel **B seul** a repris l'A figée par SHA-256, sans la réinterpréter.
Il a rapporté `gpt-5.6-luna`/`max`, et le validateur du doctor puis `RealProbe`
ont accepté le plan et compilé un paquet de sonde. Voir les [deux](../evidence/public/backend/doctor-ab13-b11-2026-09-20.md)
[preuves](../evidence/public/backend/doctor-plan-b11-resolved-2026-09-20.md).
Ce paquet a ensuite passé les deux essais Unity PlayMode du rendu final, 2/2,
dont le contrôle de pixels Direct3D 12. Aucun job joueur ni verdict humain n'est attesté ;
le worker reste fermé.
