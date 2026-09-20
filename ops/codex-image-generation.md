# Codex exec : référence visuelle native par sort

## Périmètre et état

L’étape G reçoit une description de sort figée. Elle demande au modèle de pilotage
`gpt-6-astra` avec l’effort `high` d’utiliser exactement une fois l’outil natif
`image_gen.imagegen`. Cet outil utilise `gpt-image-2` dans la version inspectée.
Le service n’utilise ni clé API, ni appel image HTTP implémenté par Palimpseste,
ni automatisation d’un terminal interactif.

Le runner efface les clés d’API de l’environnement et force
`forced_login_method="chatgpt"` pour A/B/G. Le contrôle natif d’authentification
du CLI s’exécute avant la requête. Cela empêche un passage accidentel au mode
clé API ; cela ne modifie pas les réglages de recharge du compte.

La présence de `image_generation` dans `features list` ne constitue pas une preuve
de génération. Une réussite exige un événement natif, un véritable fichier PNG
complet, son empreinte et une attestation du fournisseur. La mise en production
de G reste fermée tant que sa preuve active dédiée n’a pas été observée.

## Audit de la source installée

Base : source officielle `openai/codex`, tag `rust-v0.154.0-alpha.6.2`, commit
`b5bffd3ec4db487e7e3dec59663875b0ef7b72ca`, puis
[correctif d’attestation existant](codex-attestation.md).

- `codex-rs/exec/src/lib.rs` démarre un app-server embarqué.
- `codex-rs/app-server/src/extensions.rs` installe l’extension image native, avec
  le répertoire Codex comme racine des artefacts.
- `codex-rs/ext/image-generation/src/artifact.rs` construit exclusivement le chemin
  `CODEX_HOME/generated_images/<thread nettoyé>/<appel nettoyé>.png`.
- L’app-server expose déjà `ThreadItem::ImageGeneration` et son `saved_path`.
- La projection JSONL d’`exec` inspectée ignorait cet élément. Le correctif local
  l’expose sans inclure les données base64 de l’image ni le prompt réécrit.

## Correctif local additionnel

[codex-image-generation.patch](codex-image-generation.patch) est un delta appliqué
**après** `codex-attestation.patch`. Il modifie sept fichiers Rust et n’ajoute aucune
dépendance. L’application sur une copie du tag avec le correctif d’attestation a
été vérifiée ; les sept fichiers obtenus correspondent exactement au source.

Lorsque le processus est lancé avec `PALIMPSESTE_IMAGEGEN_TEXT_ONLY=1`, le garde
de l’outil natif rejette avant tout accès fournisseur :

- toute référence à un fichier image ;
- toute demande d’utiliser l’historique d’images ;
- une deuxième génération pour le même thread, même si l’outil est recréé.

Le registre d’exécution est également filtré dans `core/src/tools/spec_plan.rs` :
seul `image_gen.imagegen` reste enregistré, en mode direct. Les autres outils sont
retirés du registre, y compris `apply_patch`, les outils hébergés, code-mode et les
outils de gestion d’agents. Cette restriction va au-delà de `shell_tool=false`,
qui ne retire pas à lui seul l’outil `apply_patch` dans la source inspectée.

Le compteur est interne au processus et indexé par l’identifiant natif du thread.
Le processus G reçoit zéro image en entrée, et seul G active `image_generation`.
Le runner active le garde natif pour A/B également : comme la fonctionnalité image
y est désactivée, le registre A/B est entièrement vide. Les images passées par
`--image` restent des entrées multimodales et ne sont pas un outil.

Le binaire renforcé `codex-image.exe` sert donc à A/B/G. L’ancien `codex.exe` est
conservé sur disque, mais les preuves attachées à son hash ne sont pas transférées
au nouveau binaire. Le catalogue local du compte de service inspecté déclare
`apply_patch_tool_type=freeform` et `tool_mode=code_mode_only` pour Astra et Sol ;
le source privilégie ce mode annoncé par le modèle. Le retrait du registre est
nécessaire, même quand `features.code_mode=false` est observé.

L’événement `item.completed` porte `type=image_generation`, `call_id`, `status`,
`saved_path`, `failure`, `source=native_image_generation`,
`protocol=palimpseste.codex-image/1.0`, `text_only` et `one_shot`.
Le backend exige exactement un événement réussi et relie son chemin aux IDs
natifs du thread et de l’appel. Aucun chemin fourni dans le JSON du modèle
n’est accepté.

## Validation et transmission en B

Le PNG doit avoir au plus 8 Mio, une largeur et une hauteur comprises entre 512 et
2048, être RGB/RGBA 8 bits non entrelacé, avoir des CRC valides et toutes les
données de lignes attendues après décompression. Les PNG animés et tronqués sont
refusés. Les jonctions de fichiers sont interdites et le fichier doit être récent.
Le serveur conserve ses octets, dimensions et SHA-256, puis produit un chemin
d’entrée contrôlé pour B. B reçoit une seule image et son empreinte ; les anciens
appels B sans image continuent de fonctionner.

Les champs d’apparence produits par B sont des données de construction 3D
contrôlées. L’image sert de référence de conception ; elle ne remplace pas le sort
par une image plane. Les réparations B réutilisent la même image et la même
empreinte.

## Configuration et preuve

Variables supplémentaires : `PALIMPSESTE_IMAGE_CODEX_EXE`,
`PALIMPSESTE_IMAGE_GENERATION_VERIFIED`,
`PALIMPSESTE_IMAGE_GENERATION_EVIDENCE_PATH`,
`PALIMPSESTE_IMAGE_GENERATION_EVIDENCE_SHA256`.

La preuve `provider_image_generation_doctor` doit lier le hash du binaire G,
l’identité réelle du service, les fonctionnalités observées, l’attestation de
modèle/effort, le protocole natif, le hash de `final.json` de G et le PNG vérifié.
Le hash `stage_g.final_sha256` concerne le reçu JSON. `image.sha256` concerne le
PNG. La sonde d’établissement conserve tous les contrôles d’isolation.

## Reprise opérateur de B sans nouvelle image

Si G a réussi mais B a échoué ou reste au checkpoint d'attente, le mode `image`
du Doctor accepte explicitement `--reuse-visual-evidence <rapport-prive.json>`
et `--reuse-visual-evidence-sha256 <sha256-revu>`, avec un **nouveau** chemin
`--write`. Le script `image-doctor-child.ps1` expose les paramètres correspondants
`-ReuseVisualEvidencePath` et `-ReuseVisualEvidenceSha256`. La description figée
et son hash restent ceux de la sonde initiale ; le rapport précédent est conservé.

La reprise vérifie le rapport privé sous `evidence/pending`, l'identité, le hash
du binaire, le reçu G, le PNG complet et l'événement natif enregistré. Un journal
manquant/tronqué ou une autre instance Doctor/Codex encore active provoque un
refus. Aucun G de remplacement n'est déclenché automatiquement. Après validation,
seul B reçoit la même image et peut consommer un nouvel appel.

Le nouveau rapport conserve `stage_g` comme preuve historique et l'identifie avec
`stage_g_reuse`. `stage_g_model_call_executed=false` et
`new_model_calls_executed=0`, puis `1` si B démarre, distinguent la reprise d'une
nouvelle génération d'image. Un succès B ou une compilation réussie est inscrit
uniquement après son exécution réelle. Le 20 septembre, cette reprise a réellement
réussi : B `sp.prompt.b/2.1`, Astra/high, **204,725 s**, plan validé et compilation
réussie. Le PNG G historique est inchangé et un seul nouvel appel B a été exécuté.
Le premier rejet `turn_rate` reste archivé. Voir [la preuve publique](../evidence/public/backend/image-reference-2026-09-20.json).
Ce diagnostic opérateur ne prouve ni un job joueur de production, ni le rendu Unity,
ni l'acceptation artistique. Le service D13 a ensuite été déployé localement ; voir la preuve de déploiement publique.

## Reconstruction du binaire

Reprendre le tag, le correctif d’attestation et l’environnement MSVC/LLVM/Rust
1.95.0 documentés dans [codex-attestation.md](codex-attestation.md). À la racine
de cette source, appliquer ensuite `ops/codex-image-generation.patch` avec
`git apply --check`, puis `git apply`. Dans `codex-rs`, conserver le même cache
Cargo. Les dépendances conservent leur profil Release et leur bitcode Thin LTO.
La commande de livraison conserve Thin LTO et réduit seulement le niveau
d’optimisation demandé à la cible finale :

```bat
cargo +1.95.0 rustc -p codex-cli --bin codex --release --locked -j 3 -- -C opt-level=1
```

La précédente commande `cargo build --release` a été interrompue pendant son
optimisation Thin LTO finale, après compilation des dépendances. Une tentative
de désactivation de LTO pour la cible finale a échoué avec le code 101 : les
archives bitcode existantes ne sont pas directement exploitables par `link.exe`
(`LNK2001`, `LNK1107`). Thin LTO reste donc requis avec ce cache. Aucun contrôle
d’isolation n’est désactivé par le changement du niveau d’optimisation.

La sortie Cargo est `release/codex.exe`. Après vérification du code de sortie et
du SHA-256, le déploiement la copie sous le nom distinct `codex-image.exe` ; il ne
remplace pas l’exécutable historique. Conserver les fichiers `LICENSE` et `NOTICE`
de la source officielle avec le binaire redistribué. Ce binaire est une source
OpenAI corrigée localement, pas un binaire officiel inchangé.

Les trois filtres de tests locaux disponibles sont `palimpseste_image_guard`
dans `codex-image-generation-extension`, `palimpseste_image_event` dans
`codex-exec` et `palimpseste_image_router` dans `codex-core` (commande Cargo
`test -p <package> --lib <filtre> --locked -j 3`). Leur exécution ou non-exécution
effective est consignée dans le journal de validation ; leur présence dans le
source ne constitue pas un résultat de test.

## Renouvellement des preuves

`unified_exec` peut être activé en interne par le CLI sans exposer un outil shell.
Il doit être enregistré tel qu’observé ; son état n’est pas falsifié pour la preuve.
Les contrôles bloquants conservent la liste de fonctionnalités existante du worker.

Le parcours minimal de renouvellement des preuves du nouveau binaire comprend :

1. `ProviderDoctor local` : observation des fonctionnalités, zéro appel modèle.
2. `ProviderDoctor interpreter` : une lecture A réelle, rapport
   `interpreter_multimodal_probe`, hash du nouveau binaire, Sol/high attestés,
   schéma valide, fichiers d’entrée et de sortie liés par empreintes.
3. `ProviderDoctor image` : une génération G et une traduction B réelles, depuis
   une description figée dont le hash est vérifié. Son rapport
   `provider_image_generation_doctor` prouve G et peut servir de preuve B si la
   validation et la compilation réussissent. Le contrôle relie les fichiers
   exacts du reçu G et du plan B à la même description et au même PNG.

Cela requiert trois appels modèle si chacun réussit. Aucun succès de A n’est
déduit de la réutilisation de son ancien JSON. Le rapport image n’est pas accepté
comme preuve d’interprétation A. Les limites ou échecs image ne déclenchent pas
une nouvelle génération automatique dans ce parcours.

## Sources officielles consultées

La documentation décrit l’outil image intégré et sa consommation du quota Codex,
avec une consommation supérieure à une interaction texte comparable. Elle ne
garantit pas à elle seule sa projection JSONL dans cette version du CLI :
[génération d’images dans Codex](https://learn.chatgpt.com/docs/image-generation).

Le login ChatGPT donne accès à l’abonnement ; une clé API relève d’un mode
d’authentification et de facturation différent :
[authentification Codex](https://learn.chatgpt.com/docs/auth).

Le mode d’automatisation utilisé est documenté ici :
[mode non interactif](https://learn.chatgpt.com/docs/non-interactive-mode).

Cet audit n’a pas vérifié les paramètres de recharge du compte, n’a consulté aucun
secret et n’a lancé aucune génération réelle. Les résultats réels de compilation,
des trois tests locaux ciblés et de la sonde active sont consignés séparément.
