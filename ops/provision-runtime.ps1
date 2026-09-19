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
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path $RuntimeRoot 'artifacts'
} else {
    $ArtifactRoot = Resolve-FullPath $ArtifactRoot
}
if (Test-PathInside $ArtifactRoot $ProjectRoot) { throw "ARTIFACT_ROOT doit être hors du dépôt : $ArtifactRoot" }

if (Test-PathInside $RuntimeRoot $ProjectRoot) {
    throw "La racine runtime doit être hors du dépôt : $RuntimeRoot"
}

if ([string]::IsNullOrWhiteSpace($SpecRoot)) {
    $SpecRoot = Resolve-FullPath (Join-Path $RuntimeRoot 'spec')
} else {
    $SpecRoot = Resolve-FullPath $SpecRoot
}
if (Test-PathInside $SpecRoot $ProjectRoot) {
    throw "SPEC_ROOT must be deployed outside the development tree: $SpecRoot"
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

$codexHome = Join-Path $RuntimeRoot 'codex-home'
$attemptRoot = Join-Path $RuntimeRoot 'attempts'
$evidenceRoot = Join-Path $RuntimeRoot 'evidence'
$inputRoot = Join-Path $RuntimeRoot 'inputs'
$logsRoot = Join-Path $RuntimeRoot 'logs'
$configPath = Join-Path $codexHome 'config.toml'
$envPath = Join-Path $RuntimeRoot 'runtime.env'
$manifestPath = Join-Path $RuntimeRoot 'provisioning-manifest.json'

foreach ($directory in @($RuntimeRoot, $codexHome, $attemptRoot, $evidenceRoot, $inputRoot, $logsRoot, $ArtifactRoot)) {
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
PALIMPSESTE_LUNA_MODEL=gpt-5.6-luna
PALIMPSESTE_LUNA_MODEL_A=gpt-5.6-luna
PALIMPSESTE_LUNA_MODEL_B=gpt-5.6-luna
PALIMPSESTE_LUNA_REASONING_POLICY=highest_supported
PALIMPSESTE_LUNA_EFFORT=max
PALIMPSESTE_LUNA_RESOLVED_EFFORT=max
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
    # Prefix a raw SID with '*' so icacls does not try to resolve it as a
    # localized account name.
    $serviceGrant = '*{0}:(OI)(CI)(M)' -f $principal
    & icacls.exe $RuntimeRoot /inheritance:r /grant:r $serviceGrant '*S-1-5-18:(OI)(CI)(F)' '*S-1-5-32-544:(OI)(CI)(F)' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls a échoué avec le code $LASTEXITCODE" }

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
            $serviceRead = if ($item.PSIsContainer) { '*{0}:(OI)(CI)(RX)' -f $ServiceSid } else { '*{0}:(RX)' -f $ServiceSid }
            $adminFull = if ($item.PSIsContainer) { '*S-1-5-32-544:(OI)(CI)(F)' } else { '*S-1-5-32-544:F' }
            $systemFull = if ($item.PSIsContainer) { '*S-1-5-18:(OI)(CI)(F)' } else { '*S-1-5-18:F' }
            & icacls.exe $item.FullName /inheritance:r /grant:r $systemFull $adminFull $serviceRead /C | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "icacls read-only setup failed: $($item.FullName)" }
        }
    }
    Set-ReadOnlyRuntimeTree $SpecRoot $principal
    $codexDirectory = Split-Path -Parent $CodexExecutable
    if (Test-PathInside $codexDirectory $RuntimeRoot) {
        Set-ReadOnlyRuntimeTree $codexDirectory $principal
    }
    # The worker must run from the staged deployment, while the Codex child
    # receives an explicit deny on the development tree. This deny remains
    # effective even if broad Users/Authenticated Users grants are inherited.
    # Keep READ_CONTROL available so the worker can audit this deny ACL while
    # denying data, attributes and traversal to the Codex child.
    & icacls.exe $DevelopmentRoot /remove:d ('*{0}' -f $principal) /C | Out-Null
    & icacls.exe $DevelopmentRoot /deny ('*{0}:(OI)(CI)(RD,RA,REA,X)' -f $principal) /C | Out-Null
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
    operator_input_root = $inputRoot
    artifact_root = $ArtifactRoot
    specification_root = $SpecRoot
    source_spec_root = $SourceSpecRoot
    development_root = $DevelopmentRoot
    model = 'gpt-5.6-luna'
    requested_effort = 'max'
    effort_compatibility_verified = $false
    runtime_features_compatibility_verified = $false
    runtime_feature_evidence = 'operator_review_required_with_hash'
    runtime_execution_tools = $false
    acl_applied = [bool]$ApplyAcl
    specification_service_write_access = -not [bool]$ApplyAcl
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

Write-Output ("Runtime provisionné dans {0}. Env sans secret : {1}." -f $RuntimeRoot, $envPath)
if (-not $ApplyAcl) {
    Write-Warning 'ACL non appliquées. Utiliser -ApplyAcl après création et vérification du compte de service.'
}
Write-Warning 'Le doctor local puis le doctor actif doivent être exécutés avant de passer EFFORT_VERIFIED à true.'
