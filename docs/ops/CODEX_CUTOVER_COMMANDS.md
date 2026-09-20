# Commandes opérateur pour la bascule CLI et doctor

Ce protocole complète CODEX_CUTOVER.md. Il n'a pas été exécuté. Le 20 septembre
2026, le fichier release\codex.exe du build patché n'existait pas encore ;
il faut d'abord son build, ses tests, son diff revu et son SHA-256 attesté.
Le worker reste arrêté. Toutes les commandes ci-dessous s'exécutent dans une
console PowerShell élevée de l'opérateur, jamais sous PalRuntimeSvc. Elles ne
lancent aucun modèle. Ne pas lancer le doctor actif dans cette phase.

## 1. Préflight et sauvegardes

Renseigner le hash du CLI issu de l'attestation de build. La valeur de doctor
ci-dessous a été observée dans le staging et doit être revérifiée à l'instant
de la bascule. Les chemins de source et de destination doivent être des fichiers
ordinaires, sans jonction NTFS. Interrompre si un processus Codex ou worker
utilise le runtime, si le candidat ou son hash manque, ou si le dossier de
sauvegarde n'est pas réservé aux Administrateurs et à SYSTEM.

    $repo = 'E:\Palimpseste\Palimpseste_Unity_Dossier_Luna_Codex\Palimpseste_Unity_Dossier'
    $runtime = 'E:\PalimpsesteRuntime'
    $staging = 'E:\Palimpseste\.runtime\operator-staging'
    $cliSource = 'E:\Palimpseste\.runtime\codex-target-rust-v0.154.0-alpha.6.2\release\codex.exe'
    $doctorSource = Join-Path $staging 'doctor\ProviderDoctor.exe'
    $cliExpectedSha = '<SHA-256 CLI patché attesté>'
    $doctorExpectedSha = '4135407E2366FACC20F5528AE4ED11733DF1F54D0486121CEDF4DD98F1550E6E'
    $cliDest = Join-Path $runtime 'bin\codex.exe'
    $doctorDest = Join-Path $runtime 'bin\ProviderDoctor.exe'
    $serviceChild = Join-Path $runtime 'bin\ProviderDoctor.Service.ps1'
    if ($cliExpectedSha -notmatch '^[a-fA-F0-9]{64}$') { throw 'CLI hash non renseigné' }
    foreach ($p in @($cliSource, $doctorSource, $cliDest, $doctorDest, $serviceChild)) {
        if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Fichier manquant: $p" }
        if ((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Jonction: $p" }
    }
    if ((Get-FileHash -LiteralPath $cliSource -Algorithm SHA256).Hash -ne $cliExpectedSha) { throw 'SHA CLI' }
    if ((Get-FileHash -LiteralPath $doctorSource -Algorithm SHA256).Hash -ne $doctorExpectedSha) { throw 'SHA doctor' }
    $busy = @(Get-CimInstance Win32_Process -Filter "Name = 'codex.exe' OR Name = 'Palimpseste.Worker.exe'" |
        Where-Object { $_.ExecutablePath -like "$runtime\bin\*" })
    if ($busy.Count -ne 0) { throw 'Processus runtime encore actif' }
    if ((Get-FileHash -LiteralPath $serviceChild -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath (Join-Path $repo 'ops\doctor-service-child.ps1') -Algorithm SHA256).Hash) {
        throw 'Lanceur enfant doctor différent de la source revue'
    }
    $backup = Join-Path $staging ('cutover-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup -ErrorAction Stop | Out-Null
    icacls.exe $backup
    $backupAcl = Get-Acl -LiteralPath $backup
    $allowedSids = @('S-1-5-18', 'S-1-5-32-544')
    if ($backupAcl.Owner -notmatch 'Administrateurs|Administrators|Système|SYSTEM') { throw 'Propriétaire sauvegarde' }
    foreach ($rule in $backupAcl.Access) {
        $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if ($sid -notin $allowedSids) { throw 'ACE non opérateur sur la sauvegarde' }
    }

La sortie ACL du dossier de sauvegarde doit donner FullControl seulement à
Administrateurs et SYSTEM, sans ACE service ni groupe large. Le dossier source
operator-staging a été mesuré avec cette ACL protégée ; ne pas supposer que
cette mesure est toujours vraie. Le SHA historique observé du CLI live est
2271526227B06CA13AB2B975B88546460FC61B2A29225B6DDA0FDC803024CCC9.
Une divergence demande une nouvelle revue avant remplacement.

## 2. Installer chaque exécutable avec sauvegarde et ACL vérifiée

Effectuer l'appel CLI puis l'appel doctor, avant le doctor local de la section 3.
La fonction copie le binaire ancien et son SDDL dans le staging privé, prépare
un fichier adjacent avec l'ACL exacte de l'ancien, puis échange uniquement des
fichiers nommés. Elle refuse un fichier temporaire ou de sauvegarde préexistant.
Elle tente de restaurer l'ancien fichier si l'échange échoue. Aucun déplacement
récursif de dossier n'est utilisé.

    function Install-CheckedFile([string]$source, [string]$destination, [string]$expectedSha, [string]$backupRoot) {
        $name = [IO.Path]::GetFileName($destination)
        $next = $destination + '.next'
        $copy = Join-Path $backupRoot ($name + '.backup')
        $moved = Join-Path $backupRoot ($name + '.active-before')
        $failed = Join-Path $backupRoot ($name + '.failed')
        foreach ($p in @($next, $copy, $moved, $failed)) {
            if (Test-Path -LiteralPath $p) { throw "Chemin déjà présent: $p" }
        }
        $oldSha = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        $oldAcl = Get-Acl -LiteralPath $destination
        if (-not $oldAcl.AreAccessRulesProtected) { throw 'ACL ancienne non protégée' }
        Copy-Item -LiteralPath $destination -Destination $copy -ErrorAction Stop
        [IO.File]::WriteAllText((Join-Path $backupRoot ($name + '.sddl')), $oldAcl.Sddl)
        if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $oldSha) { throw 'Sauvegarde divergente' }
        Copy-Item -LiteralPath $source -Destination $next -ErrorAction Stop
        Set-Acl -LiteralPath $next -AclObject $oldAcl -ErrorAction Stop
        if ((Get-FileHash -LiteralPath $next -Algorithm SHA256).Hash -ne $expectedSha -or
            (Get-Acl -LiteralPath $next).Sddl -ne $oldAcl.Sddl) { throw 'Candidat ou ACL divergents' }
        $oldMoved = $false
        $newMoved = $false
        try {
            Move-Item -LiteralPath $destination -Destination $moved -ErrorAction Stop
            $oldMoved = $true
            Move-Item -LiteralPath $next -Destination $destination -ErrorAction Stop
            $newMoved = $true
            if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $expectedSha -or
                (Get-Acl -LiteralPath $destination).Sddl -ne $oldAcl.Sddl) { throw 'Contrôle final divergent' }
        } catch {
            if ($newMoved) { Move-Item -LiteralPath $destination -Destination $failed -ErrorAction Stop }
            if ($oldMoved) { Move-Item -LiteralPath $moved -Destination $destination -ErrorAction Stop }
            throw
        }
    }
    Install-CheckedFile $cliSource $cliDest $cliExpectedSha $backup
    Install-CheckedFile $doctorSource $doctorDest $doctorExpectedSha $backup
    icacls.exe $cliDest
    icacls.exe $doctorDest

Comparer les ACL finales aux anciennes : propriétaire Administrateurs, DACL
protégée, Administrateurs/SYSTEM FullControl, PalRuntimeSvc ReadAndExecute,
aucune autre ACE d'écriture. La fonction copie l'ACL ancienne exactement ;
elle n'assainit pas une ACL ancienne déjà incorrecte. Vérifier par un
AccessCheck Windows sous PalRuntimeSvc que FILE_WRITE_DATA et DELETE sont
refusés sur les deux fichiers. La sonde locale suivante prouve en plus que le
service peut exécuter le doctor et le CLI. Si une ACL diffère, arrêter et
restaurer depuis les copies privées avant tout doctor actif.

## 2 bis. Contrôle d'accès effectif sous PalRuntimeSvc

Le script revu ops/probe-runtime-bin-acl.ps1 demande au noyau Windows
READ_DATA, EXECUTE, WRITE_DATA et DELETE sur les deux exécutables, sans les
modifier ni lancer de modèle. Sa copie runtime/bin/ProbeRuntimeBinAcl.ps1
doit avoir exactement le même SHA-256 et une ACL protégée Administrateurs/
SYSTEM FullControl, service ReadAndExecute. La sonde a été exécutée sous
PalRuntimeSvc sur les anciens exécutables : 8 contrôles, code 0, lecture/
exécution autorisées, écriture/suppression refusées. La relancer après la
bascule avec une preuve nouvelle.

    $probeSource = Join-Path $repo 'ops\probe-runtime-bin-acl.ps1'
    $probeChild = Join-Path $runtime 'bin\ProbeRuntimeBinAcl.ps1'
    if ((Get-FileHash -LiteralPath $probeSource -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $probeChild -Algorithm SHA256).Hash) {
        throw 'Sonde ACL différente de la source revue'
    }
    $credential = Import-Clixml -LiteralPath 'C:\ProgramData\Palimpseste\operator-credentials\PalRuntimeSvc.credential.xml'
    if ($credential.UserName -ne "$env:COMPUTERNAME\PalRuntimeSvc") { throw 'Credential wrong account' }
    $stem = Join-Path $runtime ('evidence\pending\bin-access-' + [Guid]::NewGuid().ToString('N'))
    $process = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',('"' + $probeChild + '"')) -Credential $credential -LoadUserProfile -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput ($stem + '.json') -RedirectStandardError ($stem + '.stderr.txt')
    if ($process.ExitCode -ne 0) { throw 'Sonde ACL sous service échouée' }
    $checks = Get-Content -LiteralPath ($stem + '.json') -Raw | ConvertFrom-Json
    if ($checks.Count -ne 8) { throw 'Sonde ACL incomplète' }
    foreach ($check in $checks) {
        $expected = $check.right -in @('read','execute')
        if ($check.allowed -ne $expected) { throw "Droit inattendu: $($check.file) $($check.right)" }
    }
## 3. Doctor local, preuve approuvée et trois clés de configuration

Le lanceur opérateur importe la credential DPAPI privée sans imprimer son
contenu. Son script enfant déjà installé doit avoir le même hash que la source
revue. La sortie complète, stdout et stderr restent sous evidence\pending,
accessible en écriture au service. Le mode local ne fait aucun appel modèle.

    $credential = 'C:\ProgramData\Palimpseste\operator-credentials\PalRuntimeSvc.credential.xml'
    $pending = Join-Path $runtime ('evidence\pending\doctor-local-cutover-' + [Guid]::NewGuid().ToString('N') + '.json')
    $result = & (Join-Path $repo 'ops\run-service-doctor.ps1') -Mode local -CredentialFile $credential -RuntimeRoot $runtime -EvidencePath $pending
    if ($result.exit_code -ne 0 -or -not $result.evidence_created) { throw 'Doctor local échoué' }
    $proof = Get-Content -LiteralPath $pending -Raw | ConvertFrom-Json
    if ($proof.cli_executable_sha256 -ne $cliExpectedSha -or
        $proof.service_identity -ne 'PalRuntimeSvc' -or
        $proof.dedicated_auth -ne 'authenticated' -or
        $proof.requested_model -ne 'gpt-5.6-luna' -or
        $proof.requested_effort -ne 'max' -or
        $proof.feature_list_effective_disable_observed -ne $true -or
        $proof.model_calls_executed -ne $false) { throw 'Preuve locale insuffisante' }

Revoir aussi feature_list_gate_observations : chaque capacité exposée demandée
doit être false. Ne diffuser ni JSON intégral, ni stdout/stderr bruts, ni image,
ni prompt. Copier les octets approuvés dans un nouveau fichier sous
approved-evidence, vérifier le SHA et l'ACL protégée du fichier et de son
parent. La preuve pending seule n'est pas immuable.

    $approved = Join-Path $runtime ('approved-evidence\doctor-local-cutover-' + [Guid]::NewGuid().ToString('N') + '.json')
    if (Test-Path -LiteralPath $approved) { throw 'Preuve approuvée préexistante' }
    Copy-Item -LiteralPath $pending -Destination $approved -ErrorAction Stop
    Set-Acl -LiteralPath $approved -AclObject (Get-Acl -LiteralPath $doctorDest) -ErrorAction Stop
    if ((Get-Acl -LiteralPath $approved).Sddl -ne (Get-Acl -LiteralPath $doctorDest).Sddl) {
        throw 'ACL preuve approuvée divergente'
    }
    $pendingSha = (Get-FileHash -LiteralPath $pending -Algorithm SHA256).Hash
    $approvedSha = (Get-FileHash -LiteralPath $approved -Algorithm SHA256).Hash
    if ($pendingSha -ne $approvedSha) { throw 'Preuve copiée divergente' }
    icacls.exe $approved
    icacls.exe (Split-Path -Parent $approved)

Le fichier runtime.env est privé : ne jamais afficher son texte ni le copier
dans le dépôt. Avant édition, vérifier qu'il reste protégé et que
PALIMPSESTE_EFFORT_VERIFIED est false. Modifier uniquement les trois clés
ci-dessous, en conservant les autres lignes et leurs fins de ligne. Garder
en privé les trois valeurs précédentes pour un retour arrière.

    $envPath = Join-Path $runtime 'runtime.env'
    $envAcl = (Get-Acl -LiteralPath $envPath).Sddl
    $text = [IO.File]::ReadAllText($envPath)
    if ($text -notmatch '(?im)^PALIMPSESTE_EFFORT_VERIFIED=false\r?$') { throw 'Effort gate déjà ouverte' }
    $updates = [ordered]@{
        PALIMPSESTE_RUNTIME_FEATURES_VERIFIED = 'true'
        PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_PATH = $approved
        PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_SHA256 = $approvedSha.ToLowerInvariant()
    }
    $previous = [ordered]@{}
    foreach ($key in $updates.Keys) {
        $pattern = '(?m)^' + [regex]::Escape($key) + '=[^\r\n]*'
        $matches = [regex]::Matches($text, $pattern)
        if ($matches.Count -ne 1) { throw "Clé absente ou dupliquée: $key" }
        $previous[$key] = $matches[0].Value.Substring($key.Length + 1)
        $replacement = $key + '=' + $updates[$key]
        $text = $text.Remove($matches[0].Index, $matches[0].Length).Insert($matches[0].Index, $replacement)
    }
    $previous | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'feature-keys-before.json') -Encoding UTF8
    [IO.File]::WriteAllText($envPath, $text, (New-Object System.Text.UTF8Encoding($false)))
    if ((Get-Acl -LiteralPath $envPath).Sddl -ne $envAcl) { throw 'ACL runtime.env modifiée' }

Relancer ensuite le même doctor en mode local avec un AUTRE nom de preuve sous
pending. Exiger code 0, SHA CLI égal, identité/service/authentification égales,
production_issues limité à effort_not_verified. Ce second doctor est gratuit
mais ne démontre toujours pas le modèle ou l'effort effectifs : la sonde active
A/B et l'approbation humaine restent les portes suivantes. Ne pas installer
ni démarrer le nouveau worker à cette étape.

## Risques et limites

- Le candidat CLI release et son hash sont absents au moment de la rédaction ;
  aucun remplacement n'est autorisé avant leur attestation.
- Le staging opérateur est inaccessible au service. Un binaire laissé là ne
  peut pas être utilisé par le worker ; une copie sous runtime\bin avec ACL RX
  contrôlée est nécessaire.
- Le remplacement du CLI invalide les anciennes preuves locales et actives
  liées à l'ancien SHA. Ne jamais réutiliser une preuve d'un autre binaire.
- La preuve pending est écrivable par le service ; seule la copie approuvée,
  hashée et non remplaçable doit être référencée par runtime.env.
- Une erreur de hash, ACL, authentification, options ou preuve impose l'arrêt
  de la bascule et le maintien de PALIMPSESTE_EFFORT_VERIFIED=false. Le doctor
  local n'autorise jamais à lui seul un appel joueur.
- Le doctor actif consomme le quota existant ; il est hors de cette procédure.
  API, migration et HTTPS ont des portes de déploiement distinctes.