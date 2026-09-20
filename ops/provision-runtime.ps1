[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServiceUser,
    [string]$RuntimeRoot = 'E:\PalimpsesteRuntime',
    [string]$ProjectRoot = '',
    [string]$DevelopmentRoot = '',
    [string]$SourceSpecRoot = '',
    [string]$SpecRoot = '',
    [string]$ArtifactRoot = '',
    [string]$CodexExecutable = '',
    [switch]$ApplyAcl
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { throw 'Un chemin vide est interdit.' }
    return [IO.Path]::GetFullPath($PathValue)
}

function Test-PathInside([string]$Candidate, [string]$Root) {
    $candidateFull = (Resolve-FullPath $Candidate).TrimEnd('\', '/')
    $rootFull = (Resolve-FullPath $Root).TrimEnd('\', '/')
    if ($candidateFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    return $candidateFull.StartsWith($rootFull + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparsePath([string]$PathValue) {
    $current = Resolve-FullPath $PathValue
    $driveRoot = [IO.Path]::GetPathRoot($current)
    while ($true) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse point interdit avant provisionnement : $current"
            }
        }
        if ([string]::Equals($current, $driveRoot, [StringComparison]::OrdinalIgnoreCase)) { break }
        $current = [IO.Path]::GetDirectoryName($current.TrimEnd('\', '/'))
        if ([string]::IsNullOrWhiteSpace($current)) { break }
    }
}

function Assert-NoReparseTree([string]$RootPath) {
    if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) { return }
    foreach ($item in Get-ChildItem -LiteralPath $RootPath -Force -Recurse) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Reparse point interdit dans l'arbre à copier/protéger : $($item.FullName)"
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ProjectRoot) -and -not [string]::IsNullOrWhiteSpace($DevelopmentRoot) -and
    -not [string]::Equals((Resolve-FullPath $ProjectRoot), (Resolve-FullPath $DevelopmentRoot), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ProjectRoot et DevelopmentRoot désignent deux arbres différents.'
}
if ([string]::IsNullOrWhiteSpace($DevelopmentRoot)) { $DevelopmentRoot = $ProjectRoot }
if ([string]::IsNullOrWhiteSpace($DevelopmentRoot)) {
    throw 'DevelopmentRoot doit être fourni explicitement; le dépôt ou le déploiement ne peut pas être déduit.'
}
$DevelopmentRoot = Resolve-FullPath $DevelopmentRoot
$ProjectRoot = $DevelopmentRoot
$RuntimeRoot = Resolve-FullPath $RuntimeRoot
if (Test-Path -LiteralPath (Join-Path $RuntimeRoot 'ops') -PathType Container) {
    throw 'RuntimeRoot contient ops : garder les scripts opérateur hors de l’arbre accessible au worker.'
}
if ((Test-Path -LiteralPath (Join-Path $DevelopmentRoot 'spec') -PathType Container) -and
    (Test-Path -LiteralPath (Join-Path $DevelopmentRoot 'worker') -PathType Container)) {
    throw 'DevelopmentRoot ressemble à un paquet runtime déployé; fournir l’arbre source séparé.'
}
if ([string]::IsNullOrWhiteSpace($SourceSpecRoot)) {
    $SourceSpecRoot = if (Test-Path -LiteralPath (Join-Path $DevelopmentRoot 'spec') -PathType Container) {
        Join-Path $DevelopmentRoot 'spec'
    } else { $DevelopmentRoot }
} else {
    $SourceSpecRoot = Resolve-FullPath $SourceSpecRoot
}
foreach ($directory in @('contracts', 'prompts', 'reference')) {
    if (-not (Test-Path -LiteralPath (Join-Path $SourceSpecRoot $directory) -PathType Container)) {
        throw "SourceSpecRoot incomplet : $(Join-Path $SourceSpecRoot $directory)"
    }
}
if ((Test-PathInside $SourceSpecRoot $RuntimeRoot) -or (Test-PathInside $RuntimeRoot $SourceSpecRoot)) {
    throw 'SourceSpecRoot ne doit pas recouvrir la racine runtime.'
}
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path $RuntimeRoot 'artifacts'
} else {
    $ArtifactRoot = Resolve-FullPath $ArtifactRoot
}
if (Test-PathInside $ArtifactRoot $ProjectRoot) { throw "ARTIFACT_ROOT doit être hors du dépôt : $ArtifactRoot" }
if (-not (Test-PathInside $ArtifactRoot $RuntimeRoot)) {
    throw "ARTIFACT_ROOT doit être sous la racine runtime pour recevoir son ACL dédiée : $ArtifactRoot"
}
if ([string]::Equals($ArtifactRoot.TrimEnd('\', '/'), $RuntimeRoot.TrimEnd('\', '/'),
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ARTIFACT_ROOT ne peut pas être la racine runtime : celle-ci doit rester en lecture seule pour le worker.'
}

if ((Test-PathInside $RuntimeRoot $ProjectRoot) -or (Test-PathInside $ProjectRoot $RuntimeRoot)) {
    throw "La racine runtime et l'arbre de développement ne doivent pas se contenir : $RuntimeRoot"
}

if ([string]::IsNullOrWhiteSpace($SpecRoot)) {
    $SpecRoot = Resolve-FullPath (Join-Path $RuntimeRoot 'spec')
} else {
    $SpecRoot = Resolve-FullPath $SpecRoot
}
if (Test-PathInside $SpecRoot $ProjectRoot) {
    throw "SPEC_ROOT must be deployed outside the development tree: $SpecRoot"
}
if (-not (Test-PathInside $SpecRoot $RuntimeRoot)) {
    throw "SPEC_ROOT doit être sous la racine runtime pour recevoir son ACL dédiée : $SpecRoot"
}
if ((Test-PathInside $SpecRoot $RuntimeRoot) -and -not (Test-PathInside $RuntimeRoot $SpecRoot)) {
    # A runtime-local specification is staged from known non-secret contract
    # directories and receives a read-only service ACL below.
    $stageSpec = $true
} elseif (Test-Path -LiteralPath $SpecRoot -PathType Container) {
    $stageSpec = $false
} else {
    throw "SPEC_ROOT is absent: provide a deployed specification or use the runtime-local default."
}

if ([string]::IsNullOrWhiteSpace($CodexExecutable)) {
    $codexCommand = Get-Command codex -ErrorAction SilentlyContinue
    if ($null -eq $codexCommand) { throw 'Codex est introuvable ; fournir -CodexExecutable avec un chemin absolu.' }
    $CodexExecutable = $codexCommand.Source
}
$CodexExecutable = Resolve-FullPath $CodexExecutable
if (-not (Test-Path -LiteralPath $CodexExecutable -PathType Leaf)) {
    throw "Exécutable Codex absent : $CodexExecutable"
}
if (-not (Test-PathInside $CodexExecutable (Join-Path $RuntimeRoot 'bin'))) {
    throw "Le binaire Codex doit être dans runtime/bin pour recevoir une ACL en lecture seule : $CodexExecutable"
}

$codexHome = Join-Path $RuntimeRoot 'codex-home'
$attemptRoot = Join-Path $RuntimeRoot 'attempts'
$evidenceRoot = Join-Path $RuntimeRoot 'evidence'
$pendingEvidenceRoot = Join-Path $evidenceRoot 'pending'
$approvedEvidenceRoot = Join-Path $RuntimeRoot 'approved-evidence'
$inputRoot = Join-Path $RuntimeRoot 'inputs'
$logsRoot = Join-Path $RuntimeRoot 'logs'
$configPath = Join-Path $codexHome 'config.toml'
$envPath = Join-Path $RuntimeRoot 'runtime.env'
$manifestPath = Join-Path $RuntimeRoot 'provisioning-manifest.json'

if (Test-PathInside $SpecRoot $RuntimeRoot) {
    if ([string]::Equals($SpecRoot.TrimEnd('\', '/'), $RuntimeRoot.TrimEnd('\', '/'),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'SPEC_ROOT ne peut pas être la racine runtime.'
    }
    foreach ($reserved in @($codexHome, $attemptRoot, $evidenceRoot, $approvedEvidenceRoot, $inputRoot, $logsRoot, $ArtifactRoot, (Join-Path $RuntimeRoot 'bin'))) {
        if ((Test-PathInside $SpecRoot $reserved) -or (Test-PathInside $reserved $SpecRoot)) {
            throw "SPEC_ROOT recouvre un dossier runtime réservé : $reserved"
        }
    }
}
if (Test-PathInside $ArtifactRoot $RuntimeRoot) {
    foreach ($reserved in @($codexHome, $attemptRoot, $evidenceRoot, $approvedEvidenceRoot, $inputRoot, $logsRoot, (Join-Path $RuntimeRoot 'bin'))) {
        if ((Test-PathInside $ArtifactRoot $reserved) -or (Test-PathInside $reserved $ArtifactRoot)) {
            throw "ARTIFACT_ROOT recouvre un dossier runtime réservé : $reserved"
        }
    }
}
$codexDirectory = Split-Path -Parent $CodexExecutable
if (Test-PathInside $codexDirectory $RuntimeRoot) {
    if ([string]::Equals($codexDirectory.TrimEnd('\', '/'), $RuntimeRoot.TrimEnd('\', '/'),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Le binaire Codex doit être dans un sous-dossier runtime en lecture seule.'
    }
    foreach ($writable in @($codexHome, $attemptRoot, $pendingEvidenceRoot, $logsRoot, $ArtifactRoot)) {
        if ((Test-PathInside $codexDirectory $writable) -or (Test-PathInside $writable $codexDirectory)) {
            throw "Le dossier du binaire Codex recouvre un dossier runtime inscriptible : $writable"
        }
    }
}

# Reject junctions and symlinks before creating directories, copying the
# specification or writing runtime.env. These paths are operator inputs, not
# player data, but following one could overwrite a file outside runtime.
foreach ($pathToCheck in @($RuntimeRoot, $DevelopmentRoot, $SourceSpecRoot,
        $SpecRoot, $ArtifactRoot, $CodexExecutable, $codexHome, $attemptRoot,
        $evidenceRoot, $pendingEvidenceRoot, $approvedEvidenceRoot, $inputRoot,
        $logsRoot, $configPath, $envPath, $manifestPath)) {
    Assert-NoReparsePath $pathToCheck
}
foreach ($directory in @('contracts', 'prompts', 'reference')) {
    Assert-NoReparseTree (Join-Path $SourceSpecRoot $directory)
}
Assert-NoReparseTree $SpecRoot
foreach ($child in Get-ChildItem -LiteralPath $DevelopmentRoot -Force -Recurse) {
    if (($child.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Reparse point interdit sous DevelopmentRoot : $($child.FullName)"
    }
    if ((Get-Acl -LiteralPath $child.FullName).AreAccessRulesProtected) {
        throw "DACL protégée sous DevelopmentRoot : le deny hérité n'atteindrait pas $($child.FullName)"
    }
}

foreach ($directory in @($RuntimeRoot, $codexHome, $attemptRoot, $evidenceRoot, $pendingEvidenceRoot, $approvedEvidenceRoot, $inputRoot, $logsRoot, $ArtifactRoot)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

if ($stageSpec) {
    New-Item -ItemType Directory -Path $SpecRoot -Force | Out-Null
    foreach ($directory in @('contracts', 'prompts', 'reference')) {
        $source = Join-Path $SourceSpecRoot $directory
        if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Specification directory missing: $source" }
        $destination = Join-Path $SpecRoot $directory
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        if ($directory -eq 'prompts') {
            $allowedPrompts = @('01_MODEL_A_INTERPRETE.md', '02_MODEL_B_TRADUCTEUR.md', '03_REPARATION_TECHNIQUE.md')
            foreach ($existing in Get-ChildItem -LiteralPath $destination -Force) {
                if ($existing.Name -in $allowedPrompts) { continue }
                if (($existing.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $existing.PSIsContainer) {
                    throw "Prompt non autorisé ou reparse dans SPEC_ROOT : $($existing.FullName)"
                }
                if (Test-PathInside $existing.FullName $destination) { [IO.File]::Delete($existing.FullName) }
            }
        }
        foreach ($child in Get-ChildItem -LiteralPath $source -Force) {
            if ($directory -eq 'prompts' -and $child.Name -notin @(
                '01_MODEL_A_INTERPRETE.md', '02_MODEL_B_TRADUCTEUR.md', '03_REPARATION_TECHNIQUE.md')) { continue }
            Copy-Item -LiteralPath $child.FullName -Destination $destination -Recurse -Force
        }
    }
}

@'
# Codex runtime profile. The .NET runner also passes these controls as command
# line flags and starts with --ignore-user-config.
approval_policy = "never"
sandbox_mode = "read-only"

[features]
shell_tool = false
unified_exec = false
computer_use = false
browser_use = false
browser_use_external = false
browser_use_full_cdp_access = false
apps = false
plugins = false
hooks = false
multi_agent = false
skill_mcp_dependency_install = false
shell_snapshot = false
web_search = false
standalone_web_search = false
web_search_cached = false
web_search_request = false
remote_plugin = false
goals = false
memories = false
personality = false
code_mode = false
in_app_browser = false
in_app_chat = false
in_app_dictation = false
in_app_local_automation = false
in_app_updates = false
image_generation = false
skill_search = false
tool_suggest = false
view_image = false
workspace_dependencies = false
sleep_tool = false
tool_call_mcp_elicitation = false
auth_elicitation = false
code_mode_host = false
unified_exec_tty = false
'@ | Set-Content -LiteralPath $configPath -Encoding UTF8

@"
# Generated by ops/provision-runtime.ps1. No secret is stored here.
# Keep this file outside Git and load it into the dedicated worker process.
PALIMPSESTE_PROVIDER=codex_exec
PALIMPSESTE_INTERPRETER_MODEL=gpt-5.6-sol
PALIMPSESTE_INTERPRETER_EFFORT=high
PALIMPSESTE_PLANNER_MODEL=gpt-6-astra
PALIMPSESTE_PLANNER_EFFORT=high
PALIMPSESTE_INTERPRETER_VERIFIED=false
PALIMPSESTE_INTERPRETER_EVIDENCE_PATH=
PALIMPSESTE_INTERPRETER_EVIDENCE_SHA256=
PALIMPSESTE_EFFORT_VERIFIED=false
PALIMPSESTE_RUNTIME_FEATURES_VERIFIED=false
PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_PATH=
PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_SHA256=
PALIMPSESTE_EFFORT_EVIDENCE_PATH=
PALIMPSESTE_EFFORT_EVIDENCE_SHA256=
PALIMPSESTE_CODEX_EXE=$CodexExecutable
PALIMPSESTE_CODEX_EXECUTABLE=$CodexExecutable
PALIMPSESTE_CODEX_HOME=$codexHome
PALIMPSESTE_ATTEMPT_ROOT=$attemptRoot
PALIMPSESTE_CODEX_JOB_ROOT=$attemptRoot
PALIMPSESTE_SPEC_ROOT=$SpecRoot
PALIMPSESTE_INPUT_ROOT=$ArtifactRoot
PALIMPSESTE_ARTIFACT_ROOT=$ArtifactRoot
ARTIFACT_ROOT=$ArtifactRoot
PALIMPSESTE_DEVELOPMENT_ROOT=$DevelopmentRoot
PALIMPSESTE_SERVICE_USER=$ServiceUser
PALIMPSESTE_PROVIDER_ATTEMPT_TIMEOUT_SECONDS=1800
PALIMPSESTE_ATTEMPT_SECONDS=1800
PALIMPSESTE_MAX_OUTPUT_BYTES=2000000
PALIMPSESTE_MAX_PROVIDER_CONCURRENCY=1
PALIMPSESTE_ALLOW_MODEL_FALLBACK=false
PALIMPSESTE_ALLOW_REASONING_DOWNGRADE=false
PALIMPSESTE_RUNTIME_EXECUTION_TOOLS=false
PALIMPSESTE_DEPLOYMENT_MODE=private_lab
"@ | Set-Content -LiteralPath $envPath -Encoding UTF8

if ($ApplyAcl) {
    $principal = try {
        ([Security.Principal.NTAccount]$ServiceUser).Translate([Security.Principal.SecurityIdentifier]).Value
    } catch {
        throw "Le compte de service '$ServiceUser' n'est pas résolu. Créer/provisionner ce compte avant -ApplyAcl."
    }
    function Set-StrictRuntimeAcl([string]$PathValue, [string]$ServiceSid, [string]$ServiceMode) {
        $item = Get-Item -LiteralPath $PathValue -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refus ACL: reparse point at $PathValue"
        }
        $acl = Get-Acl -LiteralPath $item.FullName
        $acl.SetAccessRuleProtection($true, $false)
        # /grant:r replaces only the named principal; another explicit group
        # grant could still give the service write access. Rebuild this DACL
        # from exactly three SIDs instead.
        foreach ($rule in @($acl.Access)) {
            $acl.PurgeAccessRules($rule.IdentityReference)
        }
        $inheritance = if ($item.PSIsContainer) {
            [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit
        } else { [Security.AccessControl.InheritanceFlags]::None }
        $rights = switch ($ServiceMode) {
            'read' { [Security.AccessControl.FileSystemRights]::ReadAndExecute }
            'write' { [Security.AccessControl.FileSystemRights]::Modify }
            default { throw "Unknown service ACL mode: $ServiceMode" }
        }
        foreach ($entry in @(
            @('S-1-5-18', [Security.AccessControl.FileSystemRights]::FullControl),
            @('S-1-5-32-544', [Security.AccessControl.FileSystemRights]::FullControl),
            @($ServiceSid, $rights)
        )) {
            $sid = [Security.Principal.SecurityIdentifier]::new([string]$entry[0])
            $accessRule = [Security.AccessControl.FileSystemAccessRule]::new(
                $sid, [Security.AccessControl.FileSystemRights]$entry[1], $inheritance,
                [Security.AccessControl.PropagationFlags]::None,
                [Security.AccessControl.AccessControlType]::Allow)
            $acl.AddAccessRule($accessRule)
        }
        if ($ServiceMode -eq 'read') {
            # An owner can normally rewrite the DACL even without WRITE_DAC.
            $acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
        }
        Set-Acl -LiteralPath $item.FullName -AclObject $acl
    }
    # The top-level directory must not grant Modify/DeleteChild: otherwise a
    # worker could replace runtime.env or an operator-approved evidence file
    # even when that individual file has a read-only ACL.
    Set-StrictRuntimeAcl $RuntimeRoot $principal 'read'

    function Set-ReadOnlyRuntimeTree([string]$RootPath, [string]$ServiceSid) {
        $rootItem = Get-Item -LiteralPath $RootPath -Force
        if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refus ACL read-only: reparse point at $RootPath"
        }
        $items = @($rootItem) + @(Get-ChildItem -LiteralPath $rootItem.FullName -Force -Recurse)
        foreach ($item in $items) {
            if (-not (Test-PathInside $item.FullName $rootItem.FullName) -or
                ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refus ACL read-only: path outside tree or reparse point at $($item.FullName)"
            }
            Set-StrictRuntimeAcl $item.FullName $ServiceSid 'read'
        }
    }
    function Set-WritableRuntimeTree([string]$RootPath, [string]$ServiceSid) {
        if (-not (Test-PathInside $RootPath $RuntimeRoot) -or
            [string]::Equals((Resolve-FullPath $RootPath), $RuntimeRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refus ACL writable: outside runtime or runtime root at $RootPath"
        }
        $rootItem = Get-Item -LiteralPath $RootPath -Force
        if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refus ACL writable: reparse point at $RootPath"
        }
        $items = @($rootItem) + @(Get-ChildItem -LiteralPath $rootItem.FullName -Force -Recurse)
        foreach ($item in $items) {
            if (-not (Test-PathInside $item.FullName $rootItem.FullName) -or
                ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refus ACL writable: path outside tree or reparse point at $($item.FullName)"
            }
            Set-StrictRuntimeAcl $item.FullName $ServiceSid 'write'
        }
    }
    function Set-ReadOnlyRuntimeFile([string]$FilePath, [string]$ServiceSid) {
        $full = Resolve-FullPath $FilePath
        if (-not (Test-PathInside $full $RuntimeRoot) -or
            -not (Test-Path -LiteralPath $full -PathType Leaf)) {
            throw "Refus ACL read-only: file outside runtime or absent at $full"
        }
        $item = Get-Item -LiteralPath $full -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refus ACL read-only: reparse point at $full"
        }
        Set-StrictRuntimeAcl $full $ServiceSid 'read'
    }
    # Seal every pre-existing child, including an unexpected file at the
    # runtime root, before reopening only the named working subtrees.
    Set-ReadOnlyRuntimeTree $RuntimeRoot $principal
    Set-ReadOnlyRuntimeTree $SpecRoot $principal
    Set-ReadOnlyRuntimeTree $approvedEvidenceRoot $principal
    Set-ReadOnlyRuntimeTree $evidenceRoot $principal
    Set-ReadOnlyRuntimeTree $inputRoot $principal
    foreach ($writableRoot in @($codexHome, $attemptRoot, $pendingEvidenceRoot, $logsRoot)) {
        Set-WritableRuntimeTree $writableRoot $principal
    }
    if (Test-PathInside $ArtifactRoot $RuntimeRoot) {
        Set-WritableRuntimeTree $ArtifactRoot $principal
    }
    Set-ReadOnlyRuntimeFile $envPath $principal
    if (Test-PathInside $codexDirectory $RuntimeRoot) {
        Set-ReadOnlyRuntimeTree $codexDirectory $principal
    }
    # The worker must run from the staged deployment, while the Codex child
    # receives an explicit deny on the development tree. This deny remains
    # effective even if broad Users/Authenticated Users grants are inherited.
    # Keep READ_CONTROL available so the worker can audit this deny ACL. Deny
    # read, execute, create, modify, delete and ACL/owner changes to the child.
    & icacls.exe $DevelopmentRoot /remove:d ('*{0}' -f $principal) /C | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls development deny removal failed: $DevelopmentRoot" }
    & icacls.exe $DevelopmentRoot /deny ('*{0}:(OI)(CI)(RD,RA,REA,X,WD,AD,WA,WEA,D,DC,WDAC,WO)' -f $principal) /C | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls development deny failed: $DevelopmentRoot" }
}

$manifest = [ordered]@{
    kind = 'palimpseste_codex_runtime_provisioning'
    generated_at = [DateTimeOffset]::UtcNow
    runtime_root = $RuntimeRoot
    service_user = $ServiceUser
    codex_executable = $CodexExecutable
    codex_home = $codexHome
    attempt_root = $attemptRoot
    evidence_root = $evidenceRoot
    pending_evidence_root = $pendingEvidenceRoot
    approved_evidence_root = $approvedEvidenceRoot
    operator_input_root = $inputRoot
    artifact_root = $ArtifactRoot
    specification_root = $SpecRoot
    source_spec_root = $SourceSpecRoot
    development_root = $DevelopmentRoot
    interpreter_model = 'gpt-5.6-sol'
    interpreter_effort = 'high'
    model = 'gpt-6-astra'
    requested_effort = 'high'
    effort_compatibility_verified = $false
    runtime_features_compatibility_verified = $false
    runtime_feature_evidence = 'operator_review_required_with_hash'
    runtime_execution_tools = $false
    acl_applied = [bool]$ApplyAcl
    specification_service_write_access = -not [bool]$ApplyAcl
    runtime_root_service_read_only = [bool]$ApplyAcl
    approved_evidence_service_read_only = [bool]$ApplyAcl
    pending_evidence_service_writable = [bool]$ApplyAcl
    runtime_codex_binary_read_only = [bool]($ApplyAcl -and (Test-PathInside (Split-Path -Parent $CodexExecutable) $RuntimeRoot))
    development_tree_denied_to_service = [bool]$ApplyAcl
    auth_provisioning = 'operator_action_required_in_dedicated_CODEX_HOME'
    active_doctor_required = $true
    secrets_copied = $false
    personal_codex_home_reused = $false
    files = @(
        [ordered]@{ path = $configPath; sha256 = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash.ToLowerInvariant() },
        [ordered]@{ path = $envPath; sha256 = (Get-FileHash -LiteralPath $envPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    )
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
if ($ApplyAcl) { Set-ReadOnlyRuntimeFile $manifestPath $principal }

Write-Output ("Runtime provisionné dans {0}. Env sans secret : {1}." -f $RuntimeRoot, $envPath)
if (-not $ApplyAcl) {
    Write-Warning 'ACL non appliquées. Utiliser -ApplyAcl après création et vérification du compte de service.'
}
Write-Warning 'Le doctor local puis le doctor actif doivent être exécutés avant de passer EFFORT_VERIFIED à true.'
