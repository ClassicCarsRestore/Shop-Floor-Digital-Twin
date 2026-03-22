param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('A','BStart','BEnd','C','D','All')]
    [string]$Block = 'All',

    [Parameter(Mandatory = $false)]
    [string]$ConfigPath = '.\tools\energy-study\config.json',

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = '',

    [Parameter(Mandatory = $false)]
    [int]$BlockADurationMinutes = 0,

    [Parameter(Mandatory = $false)]
    [int]$LatencyThresholdW = 0,

    [Parameter(Mandatory = $false)]
    [int]$LatencyTimeoutSec = 0,

    [Parameter(Mandatory = $false)]
    [string]$UnityCsvPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Ensure-Dir([string]$PathValue) {
    if (-not (Test-Path $PathValue)) {
        New-Item -ItemType Directory -Path $PathValue | Out-Null
    }
}

function New-RunFolder([string]$BaseDir) {
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmss')
    $target = Join-Path $BaseDir "run_$stamp"
    Ensure-Dir $target
    return $target
}

function Load-Config([string]$PathValue) {
    if (-not (Test-Path $PathValue)) {
        throw "Config file not found: $PathValue (copy config.sample.json to config.json and edit values)"
    }

    return Get-Content -Path $PathValue -Raw | ConvertFrom-Json
}

function Get-NowUtcIso() {
    return (Get-Date).ToUniversalTime().ToString('o')
}

function Write-CsvRow([string]$PathValue, [string[]]$Headers, [hashtable]$Row) {
    $exists = Test-Path $PathValue
    if (-not $exists) {
        ($Headers -join ',') | Out-File -FilePath $PathValue -Encoding utf8
    }

    $lineValues = foreach ($h in $Headers) {
        $value = if ($Row.ContainsKey($h)) { $Row[$h] } else { '' }
        if ($null -eq $value) { '' }
        else {
            $text = [string]$value
            if ($text.Contains(',') -or $text.Contains('"')) {
                '"' + ($text.Replace('"', '""')) + '"'
            }
            else { $text }
        }
    }

    ($lineValues -join ',') | Out-File -FilePath $PathValue -Append -Encoding utf8
}

function To-DoubleOrNull($value) {
    if ($null -eq $value) { return $null }
    $tmp = 0.0
    if ([double]::TryParse([string]$value, [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$tmp)) {
        return $tmp
    }
    return $null
}

function Resolve-InfluxCliPath() {
    $cmd = Get-Command influx -ErrorAction SilentlyContinue
    if ($null -ne $cmd) {
        return 'influx'
    }

    $fallbacks = @(
        "$env:LOCALAPPDATA\\Microsoft\\WinGet\\Links\\influx.exe",
        "$env:LOCALAPPDATA\\Microsoft\\WinGet\\Packages\\InfluxData.InfluxDB.CLI_Microsoft.Winget.Source_8wekyb3d8bbwe\\influx.exe",
        "$env:LOCALAPPDATA\\Programs\\InfluxData\\influx\\influx.exe",
        "$env:ProgramFiles\\InfluxData\\influx\\influx.exe"
    )

    foreach ($path in $fallbacks) {
        if (Test-Path $path) { return $path }
    }

    return $null
}

function Assert-InfluxCliAvailable() {
    $resolved = Resolve-InfluxCliPath
    if ($null -eq $resolved) {
        throw "Influx CLI not found in PATH. Install 'influx' on this machine or run the scripts on a machine/VM where 'influx' is installed."
    }

    $script:InfluxCli = $resolved
}

function Get-InfluxArgs($cfg) {
    $args = @()

    if ($cfg.influx.host) {
        $args += '--host'
        $args += [string]$cfg.influx.host
    }

    if ($cfg.influx.org) {
        $args += '--org'
        $args += [string]$cfg.influx.org
    }

    if ($cfg.influx.token) {
        $args += '--token'
        $args += [string]$cfg.influx.token
    }

    return $args
}

function Parse-InfluxCsv([string]$raw) {
    if ([string]::IsNullOrWhiteSpace($raw)) { return @() }

    $lines = @( $raw -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith('#') } )
    if ($lines.Count -eq 0) { return @() }

    if ($lines[0].StartsWith(',')) {
        $lines[0] = 'unused' + $lines[0]
    }

    $csv = $lines -join "`n"
    try {
        return @( $csv | ConvertFrom-Csv )
    }
    catch {
        return @()
    }
}

function Invoke-InfluxFlux([string]$flux, $cfg) {
    $tmpFluxFile = Join-Path $env:TEMP ("flux_" + [guid]::NewGuid().ToString("N") + ".flux")
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($tmpFluxFile, $flux, $utf8NoBom)

    try {
        $args = @('query', '--file', $tmpFluxFile, '--raw') + (Get-InfluxArgs $cfg)
        $output = & $script:InfluxCli @args 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Influx query failed. Flux: $flux`nOutput: $output"
        }
        return [string]::Join("`n", $output)
    }
    finally {
        if (Test-Path $tmpFluxFile) { Remove-Item $tmpFluxFile -Force -ErrorAction SilentlyContinue }
    }
}

function Get-InfluxLatestPower($cfg) {
    $bucketRaw = [string]$cfg.influx.bucketRaw
    $meterId = [string]$cfg.meterId

    $flux = @"
from(bucket: "$bucketRaw")
  |> range(start: -5m)
  |> filter(fn: (r) => r._measurement == "$meterId" and r._field == "power")
    |> last()
"@

    $raw = Invoke-InfluxFlux -flux $flux -cfg $cfg
    $rows = @(Parse-InfluxCsv $raw)
    if ($rows.Count -eq 0) {
        return [pscustomobject]@{ Time = $null; Value = $null }
    }

    $row = $rows[0]
    return [pscustomobject]@{
        Time = $row._time
        Value = To-DoubleOrNull $row._value
    }
}

function Get-InfluxMeanPower($cfg, [string]$startUtc, [string]$stopUtc) {
    $bucketRaw = [string]$cfg.influx.bucketRaw
    $meterId = [string]$cfg.meterId

    $flux = @"
from(bucket: "$bucketRaw")
  |> range(start: time(v: "$startUtc"), stop: time(v: "$stopUtc"))
  |> filter(fn: (r) => r._measurement == "$meterId" and r._field == "power")
  |> mean()
"@

    $raw = Invoke-InfluxFlux -flux $flux -cfg $cfg
    $rows = @(Parse-InfluxCsv $raw)
    if ($rows.Count -eq 0) { return $null }
    return To-DoubleOrNull $rows[0]._value
}

function Get-InfluxRawEnergy24hWh($cfg, [string]$startUtc, [string]$stopUtc) {
    $bucketRaw = [string]$cfg.influx.bucketRaw
    $meterId = [string]$cfg.meterId

    $flux = @"
from(bucket: "$bucketRaw")
    |> range(start: time(v: "$startUtc"), stop: time(v: "$stopUtc"))
    |> filter(fn: (r) => r._measurement == "$meterId" and r._field == "total")
    |> filter(fn: (r) => not exists r.url)
    |> difference(nonNegative: true)
  |> sum()
"@

    $raw = Invoke-InfluxFlux -flux $flux -cfg $cfg
    $rows = @(Parse-InfluxCsv $raw)
    if ($rows.Count -eq 0) { return $null }
    return To-DoubleOrNull $rows[0]._value
}

function Get-InfluxAggEnergy24h($cfg, [string]$startUtc, [string]$stopUtc) {
    $bucket1h = [string]$cfg.influx.bucket1h
    $meterId = [string]$cfg.meterId

    $flux = @"
from(bucket: "$bucket1h")
    |> range(start: time(v: "$startUtc"), stop: time(v: "$stopUtc"))
  |> filter(fn: (r) => r._measurement == "$meterId" and r._field == "total")
  |> filter(fn: (r) => not exists r.url)
  |> difference(nonNegative: true)
  |> sum()
"@

    $raw = Invoke-InfluxFlux -flux $flux -cfg $cfg
    $rows = @(Parse-InfluxCsv $raw)
    if ($rows.Count -eq 0) { return $null }
    return To-DoubleOrNull $rows[0]._value
}

function Get-ShellyPower($cfg) {
    $url = [string]$cfg.shellyUrl

    $mode = 'single'
    if ($null -ne $cfg.shelly -and $null -ne $cfg.shelly.mode -and -not [string]::IsNullOrWhiteSpace([string]$cfg.shelly.mode)) {
        $mode = [string]$cfg.shelly.mode
    }

    if ($mode -eq 'sumEndpoints') {
        if ($null -eq $cfg.shelly -or $null -eq $cfg.shelly.urls) { return $null }

        $sum = 0.0
        foreach ($endpoint in $cfg.shelly.urls) {
            if ([string]::IsNullOrWhiteSpace([string]$endpoint)) { continue }
            $resp = Invoke-RestMethod -Uri ([string]$endpoint) -Method Get -TimeoutSec 10
            $pv = To-DoubleOrNull $resp.power
            if ($null -ne $pv) { $sum += $pv }
        }

        return $sum
    }

    $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 10

    if ($mode -eq 'sumEmeters') {
        if ($null -eq $response.emeters) { return $null }
        $sum = 0.0
        foreach ($em in $response.emeters) {
            $pv = To-DoubleOrNull $em.power
            if ($null -ne $pv) { $sum += $pv }
        }
        return $sum
    }

    if ($mode -eq 'sumSelectedEmeters') {
        if ($null -eq $response.emeters -or $null -eq $cfg.shelly.emeterIndices) { return $null }
        $sum = 0.0
        foreach ($idx in $cfg.shelly.emeterIndices) {
            $i = [int]$idx
            if ($i -ge 0 -and $i -lt $response.emeters.Count) {
                $pv = To-DoubleOrNull $response.emeters[$i].power
                if ($null -ne $pv) { $sum += $pv }
            }
        }
        return $sum
    }

    if ($null -ne $response.power) {
        return To-DoubleOrNull $response.power
    }

    if ($null -ne $response.emeters -and $response.emeters.Count -gt 0 -and $null -ne $response.emeters[0].power) {
        return To-DoubleOrNull $response.emeters[0].power
    }

    if ($null -ne $response.total_power) {
        return To-DoubleOrNull $response.total_power
    }

    return $null
}

function Get-ApiCurrentPower($cfg) {
    $meterId = [string]$cfg.meterId
    $url = ([string]$cfg.apiBaseUrl).TrimEnd('/') + '/energy/current'
    $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 10

    if ($null -ne $response.meters) {
        $metersObj = $response.meters

        function Get-MeterPowerValue($meterObj) {
            if ($null -eq $meterObj) { return $null }

            if ($null -ne $meterObj.power) {
                if ($meterObj.power -is [double] -or $meterObj.power -is [int] -or $meterObj.power -is [decimal]) {
                    return To-DoubleOrNull $meterObj.power
                }

                if ($null -ne $meterObj.power.value) {
                    return To-DoubleOrNull $meterObj.power.value
                }
            }

            if ($null -ne $meterObj.current_power_w) {
                return To-DoubleOrNull $meterObj.current_power_w
            }

            return $null
        }

        if ($metersObj -is [System.Collections.IDictionary]) {
            if ($metersObj.Contains($meterId)) {
                $meterValue = $metersObj[$meterId]
                $v = Get-MeterPowerValue $meterValue
                if ($null -ne $v) { return $v }
            }
        }
        elseif ($metersObj -is [pscustomobject]) {
            $prop = $metersObj.PSObject.Properties[$meterId]
            if ($null -ne $prop) {
                $meterValue = $prop.Value
                $v = Get-MeterPowerValue $meterValue
                if ($null -ne $v) { return $v }
            }
        }
        else {
            foreach ($m in $metersObj) {
                $hasId = $null -ne $m -and $null -ne $m.PSObject -and $m.PSObject.Properties.Match('id').Count -gt 0
                if ($hasId -and [string]$m.id -eq $meterId) {
                    $v = Get-MeterPowerValue $m
                    if ($null -ne $v) { return $v }
                }
            }
        }
    }

    if ($null -ne $response.total_power_w) {
        return To-DoubleOrNull $response.total_power_w
    }

    if ($null -ne $response.total -and $null -ne $response.total.current_power_w) {
        return To-DoubleOrNull $response.total.current_power_w
    }

    return $null
}

function Get-ApiMonthlyTotalKwh($cfg) {
    $url = ([string]$cfg.apiBaseUrl).TrimEnd('/') + '/energy/summary/month'
    $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 10

    if ($null -ne $response.total_kwh) {
        return To-DoubleOrNull $response.total_kwh
    }

    if ($null -ne $response.total -and $null -ne $response.total.month_energy_kwh) {
        return To-DoubleOrNull $response.total.month_energy_kwh
    }

    return $null
}

function Get-UnityFirstAboveThreshold([string]$unityCsv, [datetime]$eventUtc, [double]$thresholdValue) {
    if ([string]::IsNullOrWhiteSpace($unityCsv) -or -not (Test-Path $unityCsv)) {
        return $null
    }

    $rows = Import-Csv -Path $unityCsv
    foreach ($row in $rows) {
        if (-not $row.timestamp_utc) { continue }

        [datetime]$ts = [datetime]::MinValue
        if (-not [datetime]::TryParse([string]$row.timestamp_utc, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind, [ref]$ts)) { continue }
        $tsUtc = $ts.ToUniversalTime()
        if ($tsUtc -lt $eventUtc) { continue }

        $value = To-DoubleOrNull $row.total_power_w
        if ($null -eq $value) { continue }
        if ($value -ge $thresholdValue) {
            return [pscustomobject]@{
                TimestampUtc = $tsUtc
                Value = $value
            }
        }
    }

    return $null
}

function Run-BlockA($cfg, [string]$runDir, [int]$durationMinutes) {
    $intervalSec = [int]$cfg.samplingIntervalSec
    if ($intervalSec -le 0) { $intervalSec = 10 }

    $csvPath = Join-Path $runDir 'blockA_samples.csv'
    $headers = @('timestamp_utc','shelly_w','influx_w','influx_time_utc','api_w','err_shelly_vs_api_pct','err_shelly_vs_influx_pct')

    $start = (Get-Date).ToUniversalTime()
    $end = $start.AddMinutes($durationMinutes)

    while ((Get-Date).ToUniversalTime() -lt $end) {
        $ts = Get-NowUtcIso
        $shelly = $null
        $influxObj = $null
        $api = $null

        try { $shelly = Get-ShellyPower $cfg } catch { Write-Warning "Shelly read failed: $($_.Exception.Message)" }
        try { $influxObj = Get-InfluxLatestPower $cfg } catch { Write-Warning "Influx read failed: $($_.Exception.Message)" }
        try { $api = Get-ApiCurrentPower $cfg } catch { Write-Warning "API read failed: $($_.Exception.Message)" }

        $influxValue = $null
        $influxTime = $null
        if ($null -ne $influxObj -and $null -ne $influxObj.PSObject -and $influxObj.PSObject.Properties.Match('Value').Count -gt 0) {
            $influxValue = $influxObj.Value
        }
        if ($null -ne $influxObj -and $null -ne $influxObj.PSObject -and $influxObj.PSObject.Properties.Match('Time').Count -gt 0) {
            $influxTime = $influxObj.Time
        }

        $errApi = $null
        if ($null -ne $shelly -and $shelly -ne 0 -and $null -ne $api) {
            $errApi = [math]::Abs($shelly - $api) / [math]::Abs($shelly) * 100.0
        }

        $errInflux = $null
        if ($null -ne $shelly -and $shelly -ne 0 -and $null -ne $influxValue) {
            $errInflux = [math]::Abs($shelly - $influxValue) / [math]::Abs($shelly) * 100.0
        }

        Write-CsvRow -PathValue $csvPath -Headers $headers -Row @{
            timestamp_utc = $ts
            shelly_w = $shelly
            influx_w = $influxValue
            influx_time_utc = $influxTime
            api_w = $api
            err_shelly_vs_api_pct = $errApi
            err_shelly_vs_influx_pct = $errInflux
        }

        Start-Sleep -Seconds $intervalSec
    }

    return $csvPath
}

function Run-BlockBStart($cfg, [string]$runDir) {
    $now = (Get-Date).ToUniversalTime()
    $e0 = Get-ApiMonthlyTotalKwh $cfg

    $state = [pscustomobject]@{
        t0_utc = $now.ToString('o')
        e0_kwh = $e0
        meter_id = [string]$cfg.meterId
    }

    $statePath = Join-Path $runDir 'blockB_state.json'
    $state | ConvertTo-Json -Depth 10 | Out-File -FilePath $statePath -Encoding utf8

    return $statePath
}

function Run-BlockBEnd($cfg, [string]$runDir) {
    $statePath = Join-Path $runDir 'blockB_state.json'
    if (-not (Test-Path $statePath)) {
        throw "blockB_state.json not found in $runDir. Run BStart first."
    }

    $state = Get-Content -Path $statePath -Raw | ConvertFrom-Json
    $t0 = [datetime]::Parse([string]$state.t0_utc).ToUniversalTime()
    $t1 = (Get-Date).ToUniversalTime()

    $e0 = To-DoubleOrNull $state.e0_kwh
    $e1 = Get-ApiMonthlyTotalKwh $cfg
    $deltaReal = if ($null -ne $e0 -and $null -ne $e1) { $e1 - $e0 } else { $null }

    $meanW = Get-InfluxMeanPower -cfg $cfg -startUtc $t0.ToString('o') -stopUtc $t1.ToString('o')
    $hours = ($t1 - $t0).TotalHours
    $deltaTheoretical = if ($null -ne $meanW) { ($meanW / 1000.0) * $hours } else { $null }

    $pctError = $null
    if ($null -ne $deltaTheoretical -and $deltaTheoretical -ne 0 -and $null -ne $deltaReal) {
        $pctError = [math]::Abs($deltaReal - $deltaTheoretical) / [math]::Abs($deltaTheoretical) * 100.0
    }

    $csvPath = Join-Path $runDir 'blockB_result.csv'
    $headers = @('t0_utc','t1_utc','duration_h','e0_kwh','e1_kwh','delta_real_kwh','mean_power_w','delta_theoretical_kwh','error_pct')
    Write-CsvRow -PathValue $csvPath -Headers $headers -Row @{
        t0_utc = $t0.ToString('o')
        t1_utc = $t1.ToString('o')
        duration_h = $hours
        e0_kwh = $e0
        e1_kwh = $e1
        delta_real_kwh = $deltaReal
        mean_power_w = $meanW
        delta_theoretical_kwh = $deltaTheoretical
        error_pct = $pctError
    }

    return $csvPath
}

function Run-BlockC($cfg, [string]$runDir, [int]$thresholdW, [int]$timeoutSec, [string]$unityCsv) {
    $latencyCsv = Join-Path $runDir 'blockC_latency.csv'
    $headers = @('event_utc','baseline_influx_w','baseline_api_w','threshold_w','influx_detected_utc','api_detected_utc','unity_detected_utc','latency_shelly_to_influx_s','latency_influx_to_api_s','latency_api_to_unity_s','latency_total_to_unity_s')

    $baselineInfluxObj = Get-InfluxLatestPower $cfg
    $baselineApi = Get-ApiCurrentPower $cfg
    $baselineInflux = To-DoubleOrNull $baselineInfluxObj.Value

    Write-Host ''
    Write-Host 'Press Enter exactly when the physical machine is switched ON.'
    [void](Read-Host)

    $eventUtc = (Get-Date).ToUniversalTime()

    $influxDetected = $null
    $apiDetected = $null

    $stopAt = $eventUtc.AddSeconds($timeoutSec)
    while ((Get-Date).ToUniversalTime() -lt $stopAt) {
        if ($null -eq $influxDetected) {
            $curInfluxObj = Get-InfluxLatestPower $cfg
            $curInflux = To-DoubleOrNull $curInfluxObj.Value
            if ($null -ne $curInflux -and $null -ne $baselineInflux -and ($curInflux - $baselineInflux) -ge $thresholdW) {
                $influxDetected = [datetime]::Parse([string]$curInfluxObj.Time).ToUniversalTime()
            }
        }

        if ($null -eq $apiDetected) {
            $curApi = Get-ApiCurrentPower $cfg
            if ($null -ne $curApi -and $null -ne $baselineApi -and ($curApi - $baselineApi) -ge $thresholdW) {
                $apiDetected = (Get-Date).ToUniversalTime()
            }
        }

        if ($null -ne $influxDetected -and $null -ne $apiDetected) {
            break
        }

        Start-Sleep -Seconds 1
    }

    $unityDetected = $null
    if (-not [string]::IsNullOrWhiteSpace($unityCsv) -and (Test-Path $unityCsv)) {
        $thresholdForUnity = if ($null -ne $baselineApi) { $baselineApi + $thresholdW } else { $thresholdW }
        $unityHit = Get-UnityFirstAboveThreshold -unityCsv $unityCsv -eventUtc $eventUtc -thresholdValue $thresholdForUnity
        if ($null -ne $unityHit) {
            $unityDetected = $unityHit.TimestampUtc
        }
    }

    $latShellyToInflux = if ($null -ne $influxDetected) { ($influxDetected - $eventUtc).TotalSeconds } else { $null }
    $latInfluxToApi = if ($null -ne $influxDetected -and $null -ne $apiDetected) { ($apiDetected - $influxDetected).TotalSeconds } else { $null }
    $latApiToUnity = if ($null -ne $apiDetected -and $null -ne $unityDetected) { ($unityDetected - $apiDetected).TotalSeconds } else { $null }
    $latTotalUnity = if ($null -ne $unityDetected) { ($unityDetected - $eventUtc).TotalSeconds } else { $null }

    Write-CsvRow -PathValue $latencyCsv -Headers $headers -Row @{
        event_utc = $eventUtc.ToString('o')
        baseline_influx_w = $baselineInflux
        baseline_api_w = $baselineApi
        threshold_w = $thresholdW
        influx_detected_utc = if ($null -ne $influxDetected) { $influxDetected.ToString('o') } else { '' }
        api_detected_utc = if ($null -ne $apiDetected) { $apiDetected.ToString('o') } else { '' }
        unity_detected_utc = if ($null -ne $unityDetected) { $unityDetected.ToString('o') } else { '' }
        latency_shelly_to_influx_s = $latShellyToInflux
        latency_influx_to_api_s = $latInfluxToApi
        latency_api_to_unity_s = $latApiToUnity
        latency_total_to_unity_s = $latTotalUnity
    }

    return $latencyCsv
}

function Run-BlockD($cfg, [string]$runDir) {
    $nowUtc = (Get-Date).ToUniversalTime()
    $stopAlignedUtc = [DateTime]::new($nowUtc.Year, $nowUtc.Month, $nowUtc.Day, $nowUtc.Hour, 0, 0, [DateTimeKind]::Utc)
    $stopUtc = $stopAlignedUtc.AddHours(-1)
    $startUtc = $stopUtc.AddHours(-24)

    $startUtcIso = $startUtc.ToString('o')
    $stopUtcIso = $stopUtc.ToString('o')

    $rawWh = Get-InfluxRawEnergy24hWh $cfg $startUtcIso $stopUtcIso
    $aggValue = Get-InfluxAggEnergy24h $cfg $startUtcIso $stopUtcIso

    $rawKwh = if ($null -ne $rawWh) { $rawWh / 1000.0 } else { $null }
    $aggKwh = if ($null -ne $aggValue) { $aggValue / 1000.0 } else { $null }

    $diffPct = $null
    if ($null -ne $rawKwh -and $rawKwh -ne 0 -and $null -ne $aggKwh) {
        $diffPct = [math]::Abs($rawKwh - $aggKwh) / [math]::Abs($rawKwh) * 100.0
    }

    $csvPath = Join-Path $runDir 'blockD_consistency.csv'
    @([pscustomobject]@{
        timestamp_utc = Get-NowUtcIso
        window_start_utc = $startUtcIso
        window_stop_utc = $stopUtcIso
        raw_24h_kwh = $rawKwh
        agg_24h_kwh = $aggKwh
        difference_pct = $diffPct
    }) | Export-Csv -Path $csvPath -NoTypeInformation -Encoding utf8 -Force

    return $csvPath
}

$config = Load-Config $ConfigPath
Assert-InfluxCliAvailable

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    Ensure-Dir '.\tools\energy-study\results'
    $OutputDir = New-RunFolder '.\tools\energy-study\results'
}
else {
    Ensure-Dir $OutputDir
}

$duration = if ($BlockADurationMinutes -gt 0) { $BlockADurationMinutes } else { [int]$config.defaults.blockADurationMinutes }
if ($duration -le 0) { $duration = 20 }

$threshold = if ($LatencyThresholdW -gt 0) { $LatencyThresholdW } else { [int]$config.defaults.blockCLatencyThresholdW }
if ($threshold -le 0) { $threshold = 500 }

$timeout = if ($LatencyTimeoutSec -gt 0) { $LatencyTimeoutSec } else { [int]$config.defaults.blockCLatencyTimeoutSec }
if ($timeout -le 0) { $timeout = 300 }

$executed = @()

switch ($Block) {
    'A' {
        $path = Run-BlockA -cfg $config -runDir $OutputDir -durationMinutes $duration
        $executed += "Block A -> $path"
    }
    'BStart' {
        $path = Run-BlockBStart -cfg $config -runDir $OutputDir
        $executed += "Block BStart -> $path"
    }
    'BEnd' {
        $path = Run-BlockBEnd -cfg $config -runDir $OutputDir
        $executed += "Block BEnd -> $path"
    }
    'C' {
        $path = Run-BlockC -cfg $config -runDir $OutputDir -thresholdW $threshold -timeoutSec $timeout -unityCsv $UnityCsvPath
        $executed += "Block C -> $path"
    }
    'D' {
        $path = Run-BlockD -cfg $config -runDir $OutputDir
        $executed += "Block D -> $path"
    }
    'All' {
        $a = Run-BlockA -cfg $config -runDir $OutputDir -durationMinutes $duration
        $executed += "Block A -> $a"

        $b0 = Run-BlockBStart -cfg $config -runDir $OutputDir
        $executed += "Block BStart -> $b0"

        Write-Host 'Block B is split in two phases. Run machine load, then execute BEnd later.'

        $c = Run-BlockC -cfg $config -runDir $OutputDir -thresholdW $threshold -timeoutSec $timeout -unityCsv $UnityCsvPath
        $executed += "Block C -> $c"

        $d = Run-BlockD -cfg $config -runDir $OutputDir
        $executed += "Block D -> $d"
    }
}

$meta = [pscustomobject]@{
    utc_finished = Get-NowUtcIso
    block = $Block
    output_dir = $OutputDir
    meter_id = [string]$config.meterId
    executed = $executed
}

$metaPath = Join-Path $OutputDir 'run_meta.json'
$meta | ConvertTo-Json -Depth 10 | Out-File -FilePath $metaPath -Encoding utf8

Write-Host ''
Write-Host 'Completed:'
$executed | ForEach-Object { Write-Host " - $_" }
Write-Host "Metadata: $metaPath"
