# Diagnostic local lancé sous le compte de service

Le 20 septembre 2026, `ops/run-service-doctor.ps1 -Mode local` a démarré
`ProviderDoctor.exe` sous `PalRuntimeSvc` au moyen d'une credential DPAPI privée
de l'opérateur. Son script enfant est installé dans le runtime avec une ACL
protégée : Administrateurs et SYSTEM en contrôle total, `PalRuntimeSvc` en
lecture et exécution. Le SHA-256 du script source et de sa copie est
`2D34E409AF68A1810900D3F1B1E95B7C6F6C517C84AE87DF1758FA1B17590701`.
Les deux scripts PowerShell ont été analysés sans erreur de syntaxe.

Le processus a quitté avec le code 0 et a produit une preuve privée de 7 225
octets sous `E:\PalimpsesteRuntime\evidence\pending`, SHA-256
`ECE711FF0A12EA3DBC389D8C750F93A05877974CC5E77410F1F464F7A2F941D5`.
Les champs observés sont : identité `PalRuntimeSvc`, authentification dédiée
`authenticated`, modèle demandé `gpt-5.6-luna`, effort demandé `max`,
`model_calls_executed=false`. La seule réserve de production est
`effort_not_verified`. Le binaire Codex encore installé avait pour SHA-256
`2271526227B06CA13AB2B975B88546460FC61B2A29225B6DDA0FDC803024CCC9`.

Ce test ne prouve pas un appel Luna ni un montant de crédits. Le nouveau CLI
attestant le modèle et l'effort n'était pas encore installé.
