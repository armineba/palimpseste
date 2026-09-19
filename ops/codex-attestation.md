# CLI Codex avec attestation des réponses du fournisseur

Ce correctif vise uniquement la source officielle Codex au tag
`rust-v0.154.0-alpha.6.2`, commit
`b5bffd3ec4db487e7e3dec59663875b0ef7b72ca`.
Le résultat est **« source OpenAI tag + patch local »**, et non un binaire officiel
inchangé. Conserver les fichiers `LICENSE` (Apache-2.0) et `NOTICE` de la source
officielle lors de toute distribution du binaire.

## Correctif et comportement

Le fichier [codex-attestation.patch](codex-attestation.patch) a pour SHA-256
`143d78e3768896c8f150c62ce7ef0561372f8da69dd0caf28b6a059953c3c4bc`.
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

L'événement est absent si une réponse ne fournit pas les deux champs, si les
réponses divergent, si un identifiant se répète, si le compteur déborde ou si
le tour échoue. Les réponses de compaction locale et distante v2 traversent
le même contrôle. Cette absence doit rester un échec
fermé pour le backend. La présence des champs dans les réponses réelles Luna
n'a pas encore été vérifiée avec ce binaire ; les tests ci-dessous utilisent
seulement des événements locaux construits pour les tests. Le modèle de
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
cargo +1.95.0 build -p codex-cli --bin codex --release --locked -j 3
```

Tests locaux observés le 20 septembre 2026 : parseur modèle/effort/ID
`5/5`, priorité d'en-tête `1/1`, propagation SSE `1/1`, compaction core
`1/1`, agrégation, refus et tour échoué `4/4`, compaction suivie de génération
`1/1`, filtre thread/tour `1/1`. Le build Release, son hachage et les
tests avec une réponse réelle doivent être ajoutés à la preuve avant usage
dans le worker. Ne pas remplacer l'exécutable du runtime avant la revue du
correctif, des tests et du hachage du binaire.
