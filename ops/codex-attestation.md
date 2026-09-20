# CLI Codex avec attestation des réponses du fournisseur

Ce correctif vise uniquement la source officielle Codex au tag
`rust-v0.154.0-alpha.6.2`, commit
`b5bffd3ec4db487e7e3dec59663875b0ef7b72ca`.
Le résultat est **« source OpenAI tag + patch local »**, et non un binaire officiel
inchangé. Conserver les fichiers `LICENSE` (Apache-2.0) et `NOTICE` de la source
officielle lors de toute distribution du binaire.

## Correctif et comportement

Le fichier [codex-attestation.patch](codex-attestation.patch) a pour SHA-256
`c4e13fe4f25773eb1873f9e9dab28e27037dd6888dcbca2b785b917205fbabf3`.
Il inclut la mise à jour du `Cargo.lock` : les packages locaux de la source
publiée portent `0.154.0-alpha.6.2` dans leurs manifestes, mais `0.0.0` dans
le lock d'origine. Aucune dépendance externe n'est ajoutée par cette mise à jour.

Le parseur SSE et WebSocket relève le modèle et l'effort seulement dans les
métadonnées de réponse fournies par le serveur. L'en-tête `OpenAI-Model` est
prioritaire pour le modèle ; si le corps fournit un autre modèle, la réponse
est marquée contradictoire. Un champ absent, un identifiant de réponse divergent
ou un effort contradictoire empêchent l'attestation. Les champs explicitement
vides ou non textuels sont également rejetés. Les options de requête et la
configuration locale ne servent jamais de valeur de remplacement.

Chaque `response.completed` transmet ces métadonnées à `codex exec`. Sur un
tour terminé avec au moins une réponse, si **toutes** les réponses contiennent
la même paire de valeurs sans conflit, la sortie `--json` contient exactement
un événement avant `turn.completed` :

```json
{"type":"provider.attested","source":"server_response","model":"gpt-5.6-luna","reasoning_effort":"max","response_count":2}
```

Le démarrage de thread de `codex exec` active `experimental_raw_events` dans
l'app-server embarqué. Sans ce drapeau, ce serveur filtre l'événement
`rawResponse/completed` avant que l'agrégateur puisse le voir. L'opt-in est
testé pour le démarrage normal et éphémère. La reprise de threads créés sans
cet opt-in reste fermée : aucun événement d'attestation n'est émis.

L'événement est absent si une réponse ne fournit pas les deux champs, si les
réponses divergent, si un identifiant se répète, si le compteur déborde ou si
le tour échoue. Les réponses de compaction locale et distante v2 traversent
le même contrôle. Cette absence doit rester un échec
fermé pour le backend. Les tests de reconstruction ci-dessous utilisent
des événements locaux construits pour les tests. Après installation, une
sonde réelle A/B sous le compte de service a aussi vérifié la présence du
modèle `gpt-5.6-luna` et de l'effort `max` dans les deux réponses ; voir la
preuve publique de bascule. Le modèle de
l'en-tête de connexion WebSocket initial est pris en compte pour la première
requête. Sur une connexion réutilisée, cet en-tête pourrait être périmé ;
seules les métadonnées de l'événement courant permettent alors l'attestation.

## Reconstruction sur Windows

Utiliser un emplacement de développement séparé du worker joueur. Dans le
laboratoire, la source est sous
`E:\Palimpseste\.runtime\codex-source-attestation` et la cible Cargo sous
`E:\Palimpseste\.runtime\codex-target-rust-v0.154.0-alpha.6.2` ; le compte
`PalRuntimeSvc` ne peut ni lire la source ni écrire dans la cible, contrôle
consigné dans
`evidence/public/backend/codex-source-build-readiness-2026-09-20.md`.

Préparer une source vierge et vérifier son identifiant exact avant le patch :

```powershell
git clone --depth 1 --filter=blob:none --branch rust-v0.154.0-alpha.6.2 https://github.com/openai/codex.git E:\Palimpseste\.runtime\codex-source-attestation
git -C E:\Palimpseste\.runtime\codex-source-attestation rev-parse HEAD
git -C E:\Palimpseste\.runtime\codex-source-attestation apply --check E:\Palimpseste\Palimpseste_Unity_Dossier_Luna_Codex\Palimpseste_Unity_Dossier\ops\codex-attestation.patch
git -C E:\Palimpseste\.runtime\codex-source-attestation apply E:\Palimpseste\Palimpseste_Unity_Dossier_Luna_Codex\Palimpseste_Unity_Dossier\ops\codex-attestation.patch
```

La commande `rev-parse HEAD` doit retourner exactement le commit ci-dessus.
Installer Rust 1.95.0, MSVC x64, Windows SDK, LLVM/Clang, CMake et Ninja comme
indiqué par l'audit de préparation. Dans un **Developer Command Prompt** x64,
exécuter :

```bat
call "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64
set "LIBCLANG_PATH=C:\Program Files\LLVM\bin"
set "CC=C:\Program Files\LLVM\bin\clang.exe"
set "CXX=C:\Program Files\LLVM\bin\clang++.exe"
set "PATH=C:\Program Files\LLVM\bin;C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin;C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja;%PATH%"
set "CARGO_TARGET_DIR=E:\Palimpseste\.runtime\codex-target-rust-v0.154.0-alpha.6.2"
set "LIBSQLITE3_FLAGS=SQLITE_DISABLE_INTRINSIC"
cd /d E:\Palimpseste\.runtime\codex-source-attestation\codex-rs
cargo +1.95.0 test -p codex-api --lib provider_metadata --locked
cargo +1.95.0 test -p codex-api --lib provider_header --locked
cargo +1.95.0 test -p codex-api --lib sse_completion_carries_server_response_metadata --locked
cargo +1.95.0 test -p codex-core --lib collect_compaction_output_accepts_additional_output_items --locked -j 3
cargo +1.95.0 test -p codex-exec --lib attestation --locked
cargo +1.95.0 test -p codex-exec --lib compaction_then_generation_attests_both_distinct_responses --locked
cargo +1.95.0 test -p codex-exec --lib raw_response_filter_requires_matching_thread_and_turn --locked
cargo +1.95.0 test -p codex-exec --lib thread_start_params_match_history_to_persistence --locked -j 3
cargo +1.95.0 test -p codex-app-server --test all turn_start_emits_raw_response_completed_with_upstream_usage --locked -j 3
cargo +1.95.0 build -p codex-cli --bin codex --release --locked -j 3
```

Tests locaux observés le 20 septembre 2026 : parseur modèle/effort/ID
`5/5`, priorité d'en-tête `1/1`, propagation SSE `1/1`, compaction core
`1/1`, agrégation, refus et tour échoué `4/4`, compaction suivie de génération
`1/1`, filtre thread/tour `1/1`, opt-in `codex exec` `1/1`, app-server local
avec serveur simulé `8/8`.
Le build Release s'est terminé avec le code 0 en 40 min 16 s. Son exécutable
`E:\Palimpseste\.runtime\codex-target-rust-v0.154.0-alpha.6.2\release\codex.exe`
fait 300 902 912 octets ; SHA-256
`8AA8BF5CC27C55331C29C1050CD666174076E3C83D9AE84C6AE2BD54D9C7A72D`.
La commande locale `codex.exe --version` rend
`codex-cli 0.154.0-alpha.6.2`. Le détail des tests et du build figure dans
`evidence/public/backend/codex-attestation-local-validation-2026-09-20.md`.
Ce binaire a ensuite été installé dans `E:\PalimpsesteRuntime\bin` après
sauvegarde et contrôle d'accès sous `PalRuntimeSvc`. Une sonde active réelle a
obtenu A et B avec `gpt-5.6-luna` et l'effort `max` rapportés pour les deux.
La preuve et ses limites figurent dans
`evidence/public/backend/codex-cutover-active-ab-2026-09-20.md`.
La preuve active attend encore la revue humaine et le worker demeure arrêté.
