# Sonde des droits sur les exécutables du runtime

Le 20 septembre 2026, le script `ops/probe-runtime-bin-acl.ps1` a été analysé
sans erreur de syntaxe et copié dans `E:\PalimpsesteRuntime\bin` avec une
ACL protégée donnant Administrateurs/SYSTEM FullControl et `PalRuntimeSvc`
ReadAndExecute. Source et copie ont le SHA-256
`6B76D96C74BBD3D6FB7B3A15F1082BB0D4F395BD577180DC5BD3DCA4E8D2E8B1`.

La sonde a été lancée sous l'identité réelle `PalRuntimeSvc` par
`Start-Process -Credential`, sans appel modèle. Code de sortie 0 et huit
contrôles : pour `codex.exe` et `ProviderDoctor.exe`, lecture et exécution
autorisées, écriture et suppression refusées. Le JSON privé de sortie est
dans `evidence/pending`, SHA-256
`F0B5FCFAE805DA61E3649AA01755745303A0A458127049D419676EE59B7A3EAA`.
Ce contrôle concerne les anciens binaires ; il doit être répété après
installation des candidats.
