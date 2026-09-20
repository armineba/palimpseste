[CmdletBinding()]
param([string]$RuntimeRoot = 'E:\PalimpsesteRuntime')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not [string]::Equals([Security.Principal.WindowsIdentity]::GetCurrent().Name,
        "$env:COMPUTERNAME\PalRuntimeSvc", [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ACL probe must run as PalRuntimeSvc.'
}
$runtime = [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\', '/')
$files = @((Join-Path $runtime 'bin\codex.exe'),
    (Join-Path $runtime 'bin\ProviderDoctor.exe'))
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw 'Runtime executable missing.' }
}
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class PalimpsesteAclProbe {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern SafeFileHandle CreateFile(
        string path, uint access, uint share, IntPtr security, uint creation,
        uint flags, IntPtr template);
}
'@
function Probe([string]$Path, [string]$Right, [uint32]$Mask) {
    $handle = [PalimpsesteAclProbe]::CreateFile(
        $Path, $Mask, 7, [IntPtr]::Zero, 3, 0, [IntPtr]::Zero)
    $code = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    $allowed = -not $handle.IsInvalid
    if ($allowed) { $handle.Dispose() }
    return [pscustomobject]@{
        file = [IO.Path]::GetFileName($Path)
        right = $Right
        allowed = $allowed
        win32_error = if ($allowed) { 0 } else { $code }
    }
}
$checks = foreach ($file in $files) {
    Probe $file 'read' 1
    Probe $file 'execute' 32
    Probe $file 'write' 2
    Probe $file 'delete' 65536
}
$checks | ConvertTo-Json -Compress
if ($checks.Count -ne 8) { exit 1 }
foreach ($check in $checks) {
    $expected = $check.right -in @('read', 'execute')
    if ($check.allowed -ne $expected) { exit 1 }
}
