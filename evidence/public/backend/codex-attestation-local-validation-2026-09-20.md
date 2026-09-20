# Validation locale du patch Codex d'attestation

Date : 20 septembre 2026. Source officielle `rust-v0.154.0-alpha.6.2`,
commit `b5bffd3ec4db487e7e3dec59663875b0ef7b72ca`, puis patch
`ops/codex-attestation.patch` SHA-256
`c4e13fe4f25773eb1873f9e9dab28e27037dd6888dcbca2b785b917205fbabf3`.
La source et les sorties Cargo sont sous `E:\Palimpseste\.runtime`, hors du
dépôt livré et inaccessibles en écriture au compte `PalRuntimeSvc` selon
l'audit de préparation. Aucun appel au modèle n'a été lancé dans cette
validation locale.

| Contrôle exécuté | Résultat observé |
| --- | --- |
| `git apply --check --cached ops/codex-attestation.patch` sur l'index vierge du tag | code 0 |
| `git diff --check` dans le clone patché | code 0 |
| `cargo +1.95.0 test -p codex-api --lib provider_metadata --locked -j 3` | 5 passés, 0 échec |
| `cargo +1.95.0 test -p codex-api --lib provider_header --locked -j 3` | 1 passé, 0 échec |
| `cargo +1.95.0 test -p codex-api --lib sse_completion_carries_server_response_metadata --locked -j 3` | 1 passé, 0 échec |
| `cargo +1.95.0 test -p codex-core --lib collect_compaction_output_accepts_additional_output_items --locked -j 3` | 1 passé, 0 échec |
| `cargo +1.95.0 test -p codex-exec --lib attestation --locked -j 3` | 4 passés, 0 échec |
| `cargo +1.95.0 test -p codex-exec --lib compaction_then_generation_attests_both_distinct_responses --locked -j 3` | 1 passé, 0 échec |
| `cargo +1.95.0 test -p codex-exec --lib raw_response_filter_requires_matching_thread_and_turn --locked -j 3` | 1 passé, 0 échec |
| `cargo +1.95.0 test -p codex-exec --lib thread_start_params_match_history_to_persistence --locked -j 3` | 1 passé, 0 échec |
| `cargo +1.95.0 test -p codex-app-server --test all turn_start_emits_raw_response_completed_with_upstream_usage --locked -j 3` | 8 passés avec serveur simulé local, 0 échec |

Les cas couverts comprennent paire modèle/effort concordante, absence
d'effort, divergence d'effort ou d'identifiant de réponse, valeurs vides ou
non textuelles, priorité de
`OpenAI-Model` et conflit avec le corps, propagation SSE, deux réponses
concordantes dans un tour, divergences entre réponses, identifiants répétés,
overflow du compteur, tour échoué, ordre
de l'attestation avant `turn.completed`, filtre thread/tour, opt-in des
événements raw par `codex exec` et émission réelle de `rawResponse/completed`
par l'app-server sur un serveur simulé local. Le chemin
WebSocket et le chemin de compaction locale ont été compilés, mais aucun flux
WebSocket simulé ni test de compaction locale ciblé n'a été exécuté. Aucune
réponse réelle Luna n'a été soumise au binaire patché.

Un premier build Release avec `-j 8` a été interrompu avant OOM : la mémoire
physique libre était tombée à environ 0,5 Go pendant cinq compilations Rust
simultanées. Un deuxième build a été interrompu après la découverte de
l'opt-in raw manquant dans `codex exec`. Aucun binaire n'a été produit par
ces tentatives.

Le build final `cargo +1.95.0 build -p codex-cli --bin codex --release
--locked -j 3` s'est terminé avec le code 0 en 40 min 16 s. Le fichier final
`E:\Palimpseste\.runtime\codex-target-rust-v0.154.0-alpha.6.2\release\codex.exe`
fait 300 902 912 octets et a pour SHA-256
`8AA8BF5CC27C55331C29C1050CD666174076E3C83D9AE84C6AE2BD54D9C7A72D`.
La commande locale `codex.exe --version` retourne
`codex-cli 0.154.0-alpha.6.2` avec le code 0. Les avertissements de build
observés portent sur `unused_mut` dans `codex-app-server` et des imports
inutilisés dans `codex-cloud-tasks` ; aucun avertissement n'a empêché le build.
Cette section décrit la validation locale avant la bascule. Le binaire a
ensuite été installé dans le runtime et utilisé pour une sonde réelle A/B sous
`PalRuntimeSvc`. Les deux étapes ont rapporté `gpt-5.6-luna` et l'effort
`max` ; les résultats observés et leurs limites figurent dans
`evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`.
