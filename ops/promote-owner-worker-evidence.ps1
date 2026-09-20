[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# This promotes technical provider evidence for the private owner lab only.
# It does not record a human verdict on the artistic fidelity of a spell.
$runtime = 'E:\PalimpsesteRuntime'
$approved = Join-Path $runtime 'approved-evidence'
$pending = Join-Path $runtime 'evidence\pending'
$envFile = Join-Path $runtime 'runtime.env'
$manifest = Join-Path $approved 'doctor-composite-ab13-b11.json'
$reports = @(
    @{ Source = (Join-Path $pending 'doctor-active-ab13-b11-20260920.json'); Name = 'doctor-active-ab13-b11.json'; Sha = '256E564490E1A0A7E5BC50FBA216F4DC29F40EF496C867E03F3BE14291349610'; Field = 'active_a_report' },
    @{ Source = (Join-Path $pending 'doctor-plan-b11-from-a13-20260920.json'); Name = 'doctor-plan-b11-from-a13.json'; Sha = 'BFAFCA2DE085F60AF2A974C60D74CE02CAE58082E28E8A0A740B27EEB22E3787'; Field = 'plan_b_report' },
    @{ Source = (Join-Path $pending 'doctor-validate-ab13-b11-20260920.json'); Name = 'doctor-validate-ab13-b11.json'; Sha = 'D78451754EEBDC32624E4EF4771D6928CBC0D6A8FD162909B5208B047E35FEFA'; Field = 'offline_validation_report' }
)

function Assert-RegularPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetPathRoot($full)
    $cursor = $root
    foreach ($part in $full.Substring($root.Length).Split([char[]]@('\', '/'), [StringSplitOptions]::RemoveEmptyEntries)) {
        $cursor = Join-Path $cursor $part
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse point in $Path"
            }
        }
    }
}

function Assert-Hash([string]$Path, [string]$Expected) {
    Assert-RegularPath $Path
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -cne $Expected) {
        throw "SHA-256 mismatch: $Path"
    }
}

$adminSid = [Security.Principal.SecurityIdentifier]'S-1-5-32-544'
$systemSid = [Security.Principal.SecurityIdentifier]'S-1-5-18'
$serviceSid = ([Security.Principal.NTAccount]"$env:COMPUTERNAME\PalRuntimeSvc").Translate(
    [Security.Principal.SecurityIdentifier])

function Protect-ReadOnlyService([string]$Path) {
    $acl = New-Object Security.AccessControl.FileSecurity
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner($adminSid)
    $allow = [Security.AccessControl.AccessControlType]::Allow
    $full = [Security.AccessControl.FileSystemRights]::FullControl
    $read = [Security.AccessControl.FileSystemRights]::ReadAndExecute
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($adminSid, $full, $allow)))
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($systemSid, $full, $allow)))
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($serviceSid, $read, $allow)))
    Set-Acl -LiteralPath $Path -AclObject $acl
    $actual = Get-Acl -LiteralPath $Path
    if (-not $actual.AreAccessRulesProtected) { throw "ACL inheritance remains enabled: $Path" }
    foreach ($rule in $actual.Access) {
        $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if ($sid -notin @($adminSid.Value, $systemSid.Value, $serviceSid.Value)) {
            throw "Unexpected ACL principal: $Path"
        }
        if ($sid -eq $serviceSid.Value -and
            ($rule.FileSystemRights -band [Security.AccessControl.FileSystemRights]::WriteData) -ne 0) {
            throw "Service can write protected evidence: $Path"
        }
    }
}

foreach ($path in @($runtime, $approved, $pending, $envFile)) { Assert-RegularPath $path }
if (Test-Path -LiteralPath $manifest) { throw 'Composite manifest already exists; inspect it before retrying.' }
$envText = [IO.File]::ReadAllText($envFile, [Text.Encoding]::UTF8)
foreach ($line in @('PALIMPSESTE_EFFORT_VERIFIED=false', 'PALIMPSESTE_EFFORT_EVIDENCE_PATH=',
        'PALIMPSESTE_EFFORT_EVIDENCE_SHA256=')) {
    if (@($envText -split "`r?`n" | Where-Object { $_ -ceq $line }).Count -ne 1) {
        throw "Expected one closed gate entry: $line"
    }
}
foreach ($report in $reports) {
    Assert-Hash $report.Source $report.Sha
    if (Test-Path -LiteralPath (Join-Path $approved $report.Name)) {
        throw "Approved report already exists: $($report.Name)"
    }
}

$backup = Join-Path $approved ('runtime-env-before-owner-worker-' + [Guid]::NewGuid().ToString('N') + '.bak')
Copy-Item -LiteralPath $envFile -Destination $backup -ErrorAction Stop
Protect-ReadOnlyService $backup
$entries = [ordered]@{ kind = 'provider_doctor_composite'; format_version = 2 }
foreach ($report in $reports) {
    $dest = Join-Path $approved $report.Name
    Copy-Item -LiteralPath $report.Source -Destination $dest -ErrorAction Stop
    Assert-Hash $dest $report.Sha
    Protect-ReadOnlyService $dest
    $entries[$report.Field] = [ordered]@{ path = $dest; sha256 = $report.Sha }
}
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($manifest, ($entries | ConvertTo-Json -Depth 5), $utf8)
Protect-ReadOnlyService $manifest
$manifestSha = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash

$envText = $envText.Replace('PALIMPSESTE_EFFORT_EVIDENCE_PATH=',
    "PALIMPSESTE_EFFORT_EVIDENCE_PATH=$manifest")
$envText = $envText.Replace('PALIMPSESTE_EFFORT_EVIDENCE_SHA256=',
    "PALIMPSESTE_EFFORT_EVIDENCE_SHA256=$manifestSha")
$envText = $envText.Replace('PALIMPSESTE_EFFORT_VERIFIED=false',
    'PALIMPSESTE_EFFORT_VERIFIED=true')
[IO.File]::WriteAllText($envFile, $envText, $utf8)
Protect-ReadOnlyService $envFile
Assert-Hash $manifest $manifestSha

[pscustomobject]@{
    manifest = $manifest
    manifest_sha256 = $manifestSha
    runtime_env_backup = $backup
    technical_gate = 'open'
    human_artistic_verdict = 'pending'
}
