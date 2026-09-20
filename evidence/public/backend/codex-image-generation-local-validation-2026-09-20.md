# Image native Codex : audit et validation locale du 20 septembre 2026

## Observations sans appel fournisseur

- CLI installé : `codex-cli 0.154.0-alpha.6.2`, source tag officiel plus correctif
  local d’attestation déjà décrit dans `ops/codex-attestation.md`.
- Le source `exec` embarque l’app-server, qui installe déjà l’extension image native.
- L’extension sauvegarde un PNG sous le répertoire géré `generated_images` du
  compte Codex. La projection `exec --json` de cette version omettait son événement.
- Le catalogue local du compte de service a été lu en sélectionnant seulement
  `slug`, `apply_patch_tool_type`, `shell_type` et `tool_mode`. Pour
  `gpt-6-astra`, `gpt-5.6-sol` et `gpt-5.6-luna`, les valeurs observées sont
  `freeform`, `unified_exec`, `code_mode_only`.
- `shell_tool=false` et `code_mode=false` sont présents dans la configuration
  runtime. L’audit du source montre que le mode annoncé par le modèle a priorité
  et que `apply_patch` possède un enregistrement indépendant de `shell_tool`.
  Le correctif retire donc réellement tous les autres outils du registre.
- Aucune clé ou valeur de secret n’a été consultée. Aucun appel modèle, image,
  achat, recharge ou changement d’authentification n’a été effectué dans cet audit.

## Source et correctif

Le correctif image additionnel, appliqué après le correctif d’attestation, est
`ops/codex-image-generation.patch`.
SHA-256 : `3818af387f5b9fbf5f4f4b31b549630346e865936a068201fd5c8cd85e8ec6fe`.

Vérification locale observée : une copie des sept fichiers du commit officiel
`b5bffd3ec4db487e7e3dec59663875b0ef7b72ca` a reçu le correctif d’attestation,
puis le correctif image. `git apply --check` réussit et les sept fichiers
obtenus correspondent exactement au source de travail.

Le garde `PALIMPSESTE_IMAGEGEN_TEXT_ONLY=1` agit sur toutes les étapes :

- A/B : image désactivée, registre d’outils vide, mode direct.
- G : seul `image_gen.imagegen` est enregistré ; références fichiers et images
  historiques refusées ; une seule demande image par thread.

## Provider .NET

Commande réellement exécutée :
`dotnet build backend/Palimpseste.Provider/Palimpseste.Provider.csproj -c Release --no-restore`.

Dernière compilation après ajout du filtre dans toutes les étapes, de la preuve
B issue du doctor image et de `forced_login_method="chatgpt"` : code de sortie 0,
zéro avertissement, zéro erreur, 2,49 secondes rapportées par MSBuild.

## Compilation Rust et contrôles natifs

Le premier build Release a été interrompu volontairement après découverte de
l’outil `apply_patch` encore enregistré. Aucun binaire issu de cette première
tentative n’a été livré. Son journal est conservé dans le dossier de préparation.

Le build renforcé a d’abord utilisé le cache Cargo existant et la commande
`cargo +1.95.0 build -p codex-cli --bin codex --release --locked -j 3`.
Les dépendances ont été compilées, puis l’optimisation globale finale du binaire
est restée active plus de vingt minutes. La ligne de commande réellement observée
de cette cible contenait `-C lto=thin -C opt-level=3`.

Cette optimisation finale a été interrompue volontairement. La tentative suivante
a conservé les dépendances Release mais demandé `-C lto=off -C opt-level=1` à la
cible finale. Elle a réellement échoué avec le code de sortie 101, `LNK2001` et
`LNK1107` : les archives bitcode existantes exigent une étape LTO avant la liaison
native. Son journal est conservé sous
`E:\PalimpsesteBuildStage\codex-image-build-no-lto-failed.log`.

Le build suivant réutilise le même cache Release et conserve Thin LTO :
`cargo +1.95.0 rustc -p codex-cli --bin codex --release --locked -j 3 -- -C opt-level=1`.
La commande observée du compilateur contient bien `-C lto=thin` et la dernière
option `-C opt-level=1`. Seule la cible `codex-cli` est recompilée.

**Compilation réussie : code de sortie 0**, terminée le 20 septembre 2026 à
21:38 heure de Paris. Cargo rapporte `25m 08s`, profil Release optimisé avec
symboles. Le journal contient trois avertissements déjà présents : un
`unused_mut` dans `codex-app-server` et deux imports inutilisés dans
`codex-cloud-tasks`. Aucune erreur finale de compilation ou liaison.

- Binaire : `E:\Palimpseste\.runtime\codex-target-rust-v0.154.0-alpha.6.2\release\codex.exe`.
- Taille : `296616960` octets.
- SHA-256 : `0a38e51ceca23710d2ced5ed06c6822584aa8a8e418defe116a994ce384d3a4f`.
- `--version` exécuté localement après compilation, code 0 :
  `codex-cli 0.154.0-alpha.6.2`.
- Journal réel : [codex-image-generation-build-2026-09-20.log](codex-image-generation-build-2026-09-20.log).
- Journal de la tentative sans LTO échouée :
  [codex-image-generation-no-lto-failed-2026-09-20.log](codex-image-generation-no-lto-failed-2026-09-20.log).

Les trois tests Rust ciblés (garde image, projection JSONL sans base64, registre
image seul/vide) sont présents dans les sources mais **non exécutés**. Une
reconstruction supplémentaire des bibliothèques de tests n’est pas lancée avant
la sonde réelle du service. Aucun appel fournisseur n’a été effectué par le
travail d’audit, de modification ou de compilation décrit dans ce journal.

## Limites de cette preuve

Une compilation et des contrôles locaux ne prouvent pas un accès image réel avec
l’abonnement du compte. La sonde active sous l’identité du service doit encore
produire les rapports local, interpreter et image sur le hash du nouveau binaire.
Le compte de service et le moteur Unity ne doivent pas être déclarés validés par
ce seul document. La fidélité visuelle reste soumise au résultat 3D réellement
rendu et à l’acceptation humaine.
