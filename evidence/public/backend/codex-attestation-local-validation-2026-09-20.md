# Validation locale du patch Codex d'attestation

Date : 20 septembre 2026. Source officielle `rust-v0.154.0-alpha.6.2`,
commit `b5bffd3ec4db487e7e3dec59663875b0ef7b72ca`, puis patch
`ops/codex-attestation.patch` SHA-256
`143d78e3768896c8f150c62ce7ef0561372f8da69dd0caf28b6a059953c3c4bc`.
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

Les cas couverts comprennent paire modèle/effort concordante, absence
d'effort, divergence d'effort ou d'identifiant de réponse, valeurs vides ou
non textuelles, priorité de
`OpenAI-Model` et conflit avec le corps, propagation SSE, deux réponses
concordantes dans un tour, divergences entre réponses, identifiants répétés,
overflow du compteur, tour échoué, ordre
de l'attestation avant `turn.completed`, et filtre thread/tour. Le chemin
WebSocket et le chemin de compaction locale ont été compilés, mais aucun flux
WebSocket simulé ni test de compaction locale ciblé n'a été exécuté. Aucune
réponse réelle Luna n'a été soumise au binaire patché.

Un premier build Release avec `-j 8` a été interrompu avant OOM : la mémoire
physique libre était tombée à environ 0,5 Go pendant cinq compilations Rust
simultanées. Aucun binaire n'a été produit par cette tentative. Le build
final avec `cargo +1.95.0 build -p codex-cli --bin codex --release
--locked -j 3` est la prochaine étape. Son résultat et le SHA-256 du
binaire seront consignés ici lorsqu'il sera terminé. Aucun exécutable du
runtime joueur n'a été remplacé.
