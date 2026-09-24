# BlueprintV2Doctor

Outil opérateur pour un essai V2 isolé. Il utilise les appels de production `codex exec`, l'identité de service, les justificatifs existants et le renderer Unity protégé. Il ne modifie ni authentification, ni base de données, ni bibliothèque joueur.

## Exécution

Le programme doit être publié puis installé par l'opérateur sous l'identité de service, avec les variables d'environnement de production. Toutes les entrées doivent déjà se trouver sous `TrustedInputRoot`, et `--spec` doit correspondre exactement à `TrustedSpecificationRoot`.

```text
BlueprintV2Doctor.exe --spec <spec-protégée> --drawing <fixture-1024.png> --description <description-fixture.json> --report <evidence/pending/nouveau-rapport.json> --mode plan --stop-after core
```

- `--mode plan` utilise une description figée et l'indique expressément dans le rapport : ce n'est pas un résultat A. Le dessin reste un PNG de fixture de 1024×1024, lié par SHA au dossier de recherche.
- `--mode drawing` remplace `--description` par `--reference <reference-1024.png>` et effectue réellement l'interprétation A.
- `--stop-after core` est la valeur par défaut : un B, compilation, capture CORE_ONLY, critique aveugle J, puis critique structurelle J. Aucun nouvel essai automatique après échec.
- `--stop-after full` ajoute rendu normal, essais d'impact, caméra/mouvement et critique J complète **uniquement après acceptation du core**.

Au maximum : trois appels modèles en mode plan/core, quatre en plan/full ou drawing/core, cinq en drawing/full. Aucune génération d'image G et aucun appel si le précontrôle de production échoue. Les appels réels consomment le quota du compte de production.

## Preuves

Le rapport privé est mis à jour avant et après chaque appel ; il enregistre les processus réellement démarrés, modèle/effort observés, usage communiqué, résultats, hashes d'entrées et sorties. Un appel resté en attente après interruption n'est jamais déclaré gratuit ou rejouable.

Les fichiers de travail sont écrits dans un nouveau répertoire `TrustedInputRoot/blueprint-v2-doctor/<GUID>`. Les captures utilisent le runner de production. Le rapport distingue réussite du core, validation complète, et acceptation humaine non évaluée.

Cet exécutable n'est jamais invoqué depuis un parchemin ni proposé comme outil à un modèle. Sa compilation seule ne constitue pas la preuve d'un appel modèle réussi.
