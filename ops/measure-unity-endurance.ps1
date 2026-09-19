[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$OutputCsv,
    [ValidateRange(1, 86400)][int]$DurationSeconds = 660,
    [ValidateRange(1, 60)][int]$IntervalSeconds = 5
)

$ErrorActionPreference = 'Stop'
$OutputCsv = [IO.Path]::GetFullPath($OutputCsv)
if (-not (Test-Path -LiteralPath (Split-Path -Parent $OutputCsv) -PathType Container)) {
    throw 'Output directory does not exist.'
}
if (Test-Path -LiteralPath $OutputCsv) { throw 'Refusing to overwrite existing evidence.' }
$reportPath = $OutputCsv + '.json'
if (Test-Path -LiteralPath $reportPath) { throw 'Refusing to overwrite existing summary.' }
$process = Get-Process -Id $ProcessId -ErrorAction Stop
$startUtc = [DateTimeOffset]::UtcNow
$initialStart = $process.StartTime.ToUniversalTime()
$culture = [Globalization.CultureInfo]::InvariantCulture
$stream = [IO.File]::Open($OutputCsv, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
$writer = New-Object IO.StreamWriter($stream, (New-Object Text.UTF8Encoding($false)))
$samples = 0
$endedEarly = $false
try {
    $writer.WriteLine('utc,elapsed_s,pid,working_set_bytes,private_bytes,cpu_seconds,responding,handle_count')
    while ($true) {
        $elapsed = ([DateTimeOffset]::UtcNow - $startUtc).TotalSeconds
        try {
            $process = Get-Process -Id $ProcessId -ErrorAction Stop
            $process.Refresh()
            if ($process.StartTime.ToUniversalTime() -ne $initialStart) { throw 'PID reused during measurement.' }
        } catch {
            $endedEarly = $true
            break
        }
        $row = @(
            [DateTimeOffset]::UtcNow.ToString('o'),
            $elapsed.ToString('F3', $culture),
            $ProcessId,
            $process.WorkingSet64,
            $process.PrivateMemorySize64,
            ([double]$process.CPU).ToString('F3', $culture),
            $process.Responding.ToString().ToLowerInvariant(),
            $process.HandleCount
        ) -join ','
        $writer.WriteLine($row)
        $writer.Flush()
        $samples++
        if ($elapsed -ge $DurationSeconds) { break }
        Start-Sleep -Seconds $IntervalSeconds
    }
} finally {
    $writer.Dispose()
}
$endUtc = [DateTimeOffset]::UtcNow
$report = [ordered]@{
    process_id = $ProcessId
    process_start_utc = $initialStart.ToString('o')
    start_utc = $startUtc.ToString('o')
    end_utc = $endUtc.ToString('o')
    wall_seconds = [math]::Round(($endUtc - $startUtc).TotalSeconds, 3)
    requested_seconds = $DurationSeconds
    interval_seconds = $IntervalSeconds
    samples = $samples
    ended_early = $endedEarly
} | ConvertTo-Json -Depth 3
$reportStream = [IO.File]::Open($reportPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
$reportWriter = New-Object IO.StreamWriter($reportStream, (New-Object Text.UTF8Encoding($false)))
try { $reportWriter.WriteLine($report) } finally { $reportWriter.Dispose() }
Write-Output "samples=$samples wall_seconds=$([math]::Round(($endUtc-$startUtc).TotalSeconds,3)) ended_early=$endedEarly"
