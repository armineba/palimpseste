# Préflight Astra multimodal (appel explicite)

Le nouveau mode `ProviderDoctor astra` vérifie **un seul appel A réel** avec deux PNG distincts sous le compte Windows `PalRuntimeSvc`. Il demande `gpt-6-astra` et l'effort configuré, utilise le runner de production en mode probe, vérifie l'attestation du modèle et la description structurée. Il consomme l'usage normal du compte Codex. Cette commande ne construit aucun sort et ne valide pas artistiquement la lecture.

Installer d'abord le `ProviderDoctor.exe` et `ProviderDoctor.Service.ps1` reconstruits et contrôlés dans `E:\PalimpsesteRuntime\bin`, ainsi que `reference/layout-v2.json` et les prompts/schémas mis à jour dans le dossier de spécifications du runtime. Le doctor installé avant cette modification ne connaît pas `astra`.

Depuis la racine du dépôt, avec le fichier privé de credential déjà provisionné :

```powershell
.\ops\run-service-doctor.ps1 -Mode astra `
  -CredentialFile '<chemin privé du credential CLIXML PalRuntimeSvc>' `
  -ReferencePng 'E:\PalimpsesteRuntime\artifacts\<référence-1024.png>' `
  -DrawingPng 'E:\PalimpsesteRuntime\artifacts\<dessin-1024.png>'
```

Le wrapper choisit un nouveau rapport dans `evidence\pending`, exige deux fichiers sous `artifacts`, lance le child caché sous `PalRuntimeSvc` et affiche les chemins du rapport et des journaux. Cet appel consomme l'usage normal de l'abonnement Codex ; une inspection locale de la configuration ne déclenche aucun appel modèle : `-Mode local`.

Le rapport `kind=astra_multimodal_probe` doit indiquer `result=success`, `model_calls_executed=true`, `process_started=true`, `exit_code=0`, `output_schema_valid=true`, `requested_model=reported_model=gpt-6-astra`, ainsi que les empreintes du binaire Codex, des deux PNG distincts et du fichier final. Le fichier final doit contenir une description `sp.description/1.0` du dessin entier, avec observations `region=full` et `shape_requests=[]`.

Après revue, l'opérateur copie le rapport **inchangé** dans `E:\PalimpsesteRuntime\approved-evidence`, conserve les ACL du dossier et inscrit dans `runtime.env` `PALIMPSESTE_ASTRA_VERIFIED=true`, `PALIMPSESTE_ASTRA_EVIDENCE_PATH=<chemin approuvé>` et `PALIMPSESTE_ASTRA_EVIDENCE_SHA256=<SHA-256 des octets exacts>`. Le garde vérifie que le rapport approuvé est dans le même dossier que la preuve d'effort Luna, que son hash correspond et que le fichier final existe toujours sous `attempts`. Aucun de ces réglages ne doit être posé sur une sortie échouée ou sans revue.
