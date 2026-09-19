param(
    [string]$DatabaseEnvFile = 'C:\ProgramData\Palimpseste\test-db.env',
    [string]$ApiExecutable = 'backend\Palimpseste.Api\bin\Release\net10.0\Palimpseste.Api.exe',
    [int]$Port = 18111
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
. (Join-Path $PSScriptRoot '..\ops\DbEnv.ps1')
$db = Read-PalimpsesteConnection -EnvFile $DatabaseEnvFile
if ($db.Database -ne 'palimpseste_test') { throw 'Smoke limité à palimpseste_test.' }
$apiPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\$ApiExecutable"))
$psql = 'C:\Program Files\PostgreSQL\17\bin\psql.exe'
$baseUrl = "http://127.0.0.1:$Port"
$oldDatabaseUrl = $env:DATABASE_URL
$oldPgPassword = $env:PGPASSWORD
$oldAspNetUrls = $env:ASPNETCORE_URLS
$process = $null
$invitationId = $null
$principalId = $null
$client = [System.Net.Http.HttpClient]::new()

try {
    $env:DATABASE_URL = (Get-Content -LiteralPath $DatabaseEnvFile -Encoding utf8 |
        Where-Object { $_.StartsWith('DATABASE_URL=') } | Select-Object -First 1).Substring(13)
    $env:PGPASSWORD = $db.Password
    $issued = @(& $apiPath invite-create "smoke-$([Guid]::NewGuid().ToString('N'))")
    if ($LASTEXITCODE -ne 0) { throw 'Invite creation failed.' }
    $idLine = @($issued | Where-Object { $_ -match '^invitation_id=[0-9a-f]{32}$' })
    $codeLine = @($issued | Where-Object { $_ -match '^invitation_code=[A-Za-z0-9_-]{64}$' })
    if ($idLine.Count -ne 1 -or $codeLine.Count -ne 1) { throw 'Unexpected invite output.' }
    $invitationId = $idLine[0].Substring(14)
    $code = $codeLine[0].Substring(16)

    $env:ASPNETCORE_URLS = $baseUrl
    $process = Start-Process -FilePath $apiPath -PassThru -WindowStyle Hidden
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        if ($process.HasExited) { throw 'API exited before healthcheck.' }
        try {
            $health = $client.GetAsync("$baseUrl/health/live").Result
            if ([int]$health.StatusCode -eq 200) { $ready = $true; break }
        } catch { }
    }
    if (-not $ready) { throw 'API healthcheck timeout.' }

    $payload = @{ invitation_code = $code } | ConvertTo-Json -Compress
    $content = [System.Net.Http.StringContent]::new($payload, [Text.Encoding]::UTF8, 'application/json')
    $response = $client.PostAsync("$baseUrl/v1/session/redeem", $content).Result
    if ([int]$response.StatusCode -ne 200) { throw "Redeem status $([int]$response.StatusCode)." }
    $session = ($response.Content.ReadAsStringAsync().Result | ConvertFrom-Json)
    if ($session.token -notmatch '^[A-Za-z0-9_-]{64}$' -or $session.principal_id -notmatch '^[0-9a-f]{32}$') {
        throw 'Invalid session response.'
    }
    $principalId = $session.principal_id
    if ($response.Headers.CacheControl.ToString() -notmatch 'no-store') { throw 'Token response may be cached.' }

    $duplicateContent = [System.Net.Http.StringContent]::new($payload, [Text.Encoding]::UTF8, 'application/json')
    $duplicate = $client.PostAsync("$baseUrl/v1/session/redeem", $duplicateContent).Result
    if ([int]$duplicate.StatusCode -ne 401) { throw 'Invitation reused.' }

    $anonymous = $client.GetAsync("$baseUrl/v1/parchments").Result
    if ([int]$anonymous.StatusCode -ne 401) { throw 'Anonymous player access.' }
    $request = [System.Net.Http.HttpRequestMessage]::new('GET', "$baseUrl/v1/parchments")
    $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $session.token)
    $authorized = $client.SendAsync($request).Result
    if ([int]$authorized.StatusCode -ne 200) { throw 'Player token rejected.' }
    Write-Output 'Invitation HTTP smoke: 4/4 (redeem, one-use, anonymous deny, player access)'
}
finally {
    $client.Dispose()
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if ($invitationId -and $invitationId -match '^[0-9a-f]{32}$') {
        $sql = "BEGIN; DELETE FROM lab_invitations WHERE id='$invitationId'::uuid;"
        if ($principalId -and $principalId -match '^[0-9a-f]{32}$') {
            $sql += " DELETE FROM lab_tokens WHERE principal_id='$principalId'::uuid; DELETE FROM lab_principals WHERE id='$principalId'::uuid;"
        }
        $sql += ' COMMIT;'
        & $psql -w -h $db.Host -p $db.Port -U $db.Username -d $db.Database -v ON_ERROR_STOP=1 -c $sql | Out-Null
    }
    $env:DATABASE_URL = $oldDatabaseUrl
    $env:PGPASSWORD = $oldPgPassword
    $env:ASPNETCORE_URLS = $oldAspNetUrls
}
