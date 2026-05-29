param(
    [string]$BaseUrl = "http://localhost:8080",
    [int]$DeviceCount = 20,
    [string]$DevicePrefix = "TAB-",
    [string]$DeviceSecretPrefix = "dev-secret-",
    [int]$DevicePadWidth = 2,
    [int]$DurationSeconds = 120,
    [int]$PlaybackIntervalSeconds = 15,
    [int]$HeartbeatIntervalSeconds = 30,
    [int]$TimeoutSeconds = 10,
    [switch]$SkipHealthCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($DeviceCount -lt 1) { throw "DeviceCount must be greater than 0." }
if ($DurationSeconds -lt 1) { throw "DurationSeconds must be greater than 0." }
if ($PlaybackIntervalSeconds -lt 1) { throw "PlaybackIntervalSeconds must be greater than 0." }
if ($HeartbeatIntervalSeconds -lt 1) { throw "HeartbeatIntervalSeconds must be greater than 0." }

$BaseUrl = $BaseUrl.TrimEnd('/')
$startedAt = Get-Date
$deadline = $startedAt.AddSeconds($DurationSeconds)
$results = [System.Collections.Generic.List[object]]::new()

function New-DeviceCode {
    param([int]$Index)
    return "$DevicePrefix$($Index.ToString().PadLeft($DevicePadWidth, '0'))"
}

function Get-Percentile {
    param(
        [double[]]$Values,
        [double]$Percentile
    )

    if ($Values.Count -eq 0) { return 0 }

    $sorted = @($Values | Sort-Object)
    $rank = [Math]::Ceiling(($Percentile / 100.0) * $sorted.Count) - 1
    $rank = [Math]::Max(0, [Math]::Min($rank, $sorted.Count - 1))
    return [Math]::Round($sorted[$rank], 2)
}

function Add-Result {
    param(
        [string]$Endpoint,
        [string]$DeviceCode,
        [int]$StatusCode,
        [double]$LatencyMs,
        [string]$ErrorMessage = ""
    )

    $script:results.Add([pscustomobject]@{
        Endpoint = $Endpoint
        DeviceCode = $DeviceCode
        StatusCode = $StatusCode
        LatencyMs = $LatencyMs
        ErrorMessage = $ErrorMessage
        Timestamp = Get-Date
    })
}

function Invoke-Request {
    param(
        [string]$Endpoint,
        [string]$DeviceCode,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers,
        [string]$Body = $null
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $parameters = @{
            Method = $Method
            Uri = $Url
            Headers = $Headers
            TimeoutSec = $TimeoutSeconds
            UseBasicParsing = $true
        }

        if ($null -ne $Body -and $Method -ne "GET") {
            $parameters.Body = $Body
            $parameters.ContentType = "application/json"
        }

        $response = Invoke-WebRequest @parameters
        $watch.Stop()
        Add-Result -Endpoint $Endpoint -DeviceCode $DeviceCode -StatusCode ([int]$response.StatusCode) -LatencyMs $watch.Elapsed.TotalMilliseconds
    }
    catch {
        $watch.Stop()
        $statusCode = 0
        if (($_.Exception.PSObject.Properties.Name -contains "Response") `
            -and ($null -ne $_.Exception.Response) `
            -and ($null -ne $_.Exception.Response.StatusCode)) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        Add-Result -Endpoint $Endpoint -DeviceCode $DeviceCode -StatusCode $statusCode -LatencyMs $watch.Elapsed.TotalMilliseconds -ErrorMessage $_.Exception.Message
    }
}

if (-not $SkipHealthCheck) {
    Write-Host "Checking readiness: $BaseUrl/health/ready"
    Invoke-Request -Endpoint "health" -DeviceCode "health" -Method "GET" -Url "$BaseUrl/health/ready" -Headers @{}
}

Write-Host "Starting mobile fleet load test"
Write-Host "BaseUrl=$BaseUrl DeviceCount=$DeviceCount DurationSeconds=$DurationSeconds PlaybackIntervalSeconds=$PlaybackIntervalSeconds HeartbeatIntervalSeconds=$HeartbeatIntervalSeconds"
Write-Host "Device range: $(New-DeviceCode 1) -> $(New-DeviceCode $DeviceCount)"

$deviceStates = for ($index = 1; $index -le $DeviceCount; $index++) {
    $deviceCode = New-DeviceCode $index
    [pscustomobject]@{
        DeviceCode = $deviceCode
        DeviceSecret = "$DeviceSecretPrefix$deviceCode"
        NextPlaybackAt = Get-Date
        NextHeartbeatAt = Get-Date
    }
}

while ((Get-Date) -lt $deadline) {
    $now = Get-Date

    foreach ($device in $deviceStates) {
        $headers = @{ "X-Device-Secret" = $device.DeviceSecret }

        if ($now -ge $device.NextPlaybackAt) {
            Invoke-Request -Endpoint "playback" -DeviceCode $device.DeviceCode -Method "GET" -Url "$BaseUrl/api/mobile/playback-state/$($device.DeviceCode)" -Headers $headers
            $device.NextPlaybackAt = $now.AddSeconds($PlaybackIntervalSeconds)
        }

        if ($now -ge $device.NextHeartbeatAt) {
            $body = @{ deviceCode = $device.DeviceCode } | ConvertTo-Json -Compress
            Invoke-Request -Endpoint "heartbeat" -DeviceCode $device.DeviceCode -Method "POST" -Url "$BaseUrl/api/mobile/heartbeat" -Headers $headers -Body $body
            $device.NextHeartbeatAt = $now.AddSeconds($HeartbeatIntervalSeconds)
        }
    }

    $remaining = [Math]::Max(0, [int]($deadline - (Get-Date)).TotalSeconds)
    Write-Progress -Activity "Mobile fleet load test" -Status "$($results.Count) requests recorded, $remaining seconds remaining" -PercentComplete ([Math]::Min(100, (($DurationSeconds - $remaining) / $DurationSeconds) * 100))
    Start-Sleep -Milliseconds 200
}

Write-Progress -Activity "Mobile fleet load test" -Completed
$endedAt = Get-Date

Write-Host ""
Write-Host "=== Mobile Fleet Load Test Summary ==="
Write-Host "Started:  $startedAt"
Write-Host "Ended:    $endedAt"
Write-Host "Duration: $([Math]::Round(($endedAt - $startedAt).TotalSeconds, 2))s"
Write-Host "Devices:  $DeviceCount"
Write-Host "Requests: $($results.Count)"

foreach ($endpoint in @("health", "playback", "heartbeat")) {
    $endpointResults = @($results | Where-Object { $_.Endpoint -eq $endpoint })
    if ($endpointResults.Count -eq 0) { continue }

    $latencies = [double[]]@($endpointResults | ForEach-Object { [double]$_.LatencyMs })
    $success = @($endpointResults | Where-Object { $_.StatusCode -ge 200 -and $_.StatusCode -lt 300 })
    $notFound = @($endpointResults | Where-Object { $_.StatusCode -eq 404 })
    $unauthorized = @($endpointResults | Where-Object { $_.StatusCode -eq 401 })
    $serverErrors = @($endpointResults | Where-Object { $_.StatusCode -ge 500 -or $_.StatusCode -eq 0 })

    Write-Host ""
    Write-Host "[$endpoint]"
    Write-Host "  count:        $($endpointResults.Count)"
    Write-Host "  success 2xx:  $($success.Count)"
    Write-Host "  404:          $($notFound.Count)"
    Write-Host "  401:          $($unauthorized.Count)"
    Write-Host "  5xx/timeout:  $($serverErrors.Count)"
    Write-Host "  avg ms:       $([Math]::Round(($latencies | Measure-Object -Average).Average, 2))"
    Write-Host "  p50 ms:       $(Get-Percentile -Values $latencies -Percentile 50)"
    Write-Host "  p95 ms:       $(Get-Percentile -Values $latencies -Percentile 95)"
    Write-Host "  p99 ms:       $(Get-Percentile -Values $latencies -Percentile 99)"
}

$errors = @($results | Where-Object { $_.StatusCode -eq 401 -or $_.StatusCode -ge 500 -or $_.StatusCode -eq 0 })
if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Sample errors:"
    $errors | Select-Object -First 10 Endpoint, DeviceCode, StatusCode, ErrorMessage | Format-Table -AutoSize
}
