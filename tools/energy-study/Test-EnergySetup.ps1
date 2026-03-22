param(
    [Parameter(Mandatory = $false)]
    [string]$ConfigPath = '.\tools\energy-study\config.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Load-Config([string]$PathValue) {
    if (-not (Test-Path $PathValue)) {
        throw "Config file not found: $PathValue"
    }
    return Get-Content -Path $PathValue -Raw | ConvertFrom-Json
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
        throw "Influx CLI not found in PATH. Install 'influx' on this Windows machine or run this smoke test from the Linux VM where Influx CLI exists."
    }

    $script:InfluxCli = $resolved
}

function Get-InfluxArgs($cfg) {
    $args = @()
    if ($cfg.influx.host) { $args += '--host'; $args += [string]$cfg.influx.host }
    if ($cfg.influx.org) { $args += '--org'; $args += [string]$cfg.influx.org }
    if ($cfg.influx.token) { $args += '--token'; $args += [string]$cfg.influx.token }
    return $args
}

function Check-Api($cfg) {
    $base = ([string]$cfg.apiBaseUrl).TrimEnd('/')
    $currentUrl = $base + '/energy/current'
    $monthUrl = $base + '/energy/summary/month'

    $current = Invoke-RestMethod -Uri $currentUrl -Method Get -TimeoutSec 10
    $month = Invoke-RestMethod -Uri $monthUrl -Method Get -TimeoutSec 10

    $totalCurrent = $null
    if ($null -ne $current.total_power_w) { $totalCurrent = To-DoubleOrNull $current.total_power_w }
    elseif ($null -ne $current.total -and $null -ne $current.total.current_power_w) { $totalCurrent = To-DoubleOrNull $current.total.current_power_w }

    $monthKwh = $null
    if ($null -ne $month.total_kwh) { $monthKwh = To-DoubleOrNull $month.total_kwh }
    elseif ($null -ne $month.total -and $null -ne $month.total.month_energy_kwh) { $monthKwh = To-DoubleOrNull $month.total.month_energy_kwh }

    return [pscustomobject]@{
        currentUrl = $currentUrl
        monthUrl = $monthUrl
        totalCurrentW = $totalCurrent
        monthTotalKwh = $monthKwh
    }
}

function Check-Shelly($cfg) {
    $mode = 'single'
    if ($null -ne $cfg.shelly -and $null -ne $cfg.shelly.mode) { $mode = [string]$cfg.shelly.mode }

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

    $response = Invoke-RestMethod -Uri ([string]$cfg.shellyUrl) -Method Get -TimeoutSec 10

    if ($mode -eq 'sumEmeters') {
        $sum = 0.0
        foreach ($em in $response.emeters) {
            $pv = To-DoubleOrNull $em.power
            if ($null -ne $pv) { $sum += $pv }
        }
        return $sum
    }

    if ($mode -eq 'sumSelectedEmeters') {
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

    if ($null -ne $response.power) { return To-DoubleOrNull $response.power }
    if ($null -ne $response.emeters -and $response.emeters.Count -gt 0) { return To-DoubleOrNull $response.emeters[0].power }
    return $null
}

function Check-Influx($cfg) {
    $bucketRaw = [string]$cfg.influx.bucketRaw
    $meterId = [string]$cfg.meterId

    $flux = @"
from(bucket: "$bucketRaw")
  |> range(start: -10m)
  |> filter(fn: (r) => r._measurement == "$meterId" and r._field == "power")
    |> last()
"@

    $tmpFluxFile = Join-Path $env:TEMP ("flux_" + [guid]::NewGuid().ToString("N") + ".flux")
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($tmpFluxFile, $flux, $utf8NoBom)

    try {
        $args = @('query', '--file', $tmpFluxFile, '--raw') + (Get-InfluxArgs $cfg)
        $output = & $script:InfluxCli @args 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Influx query failed: $output"
        }
    }
    finally {
        if (Test-Path $tmpFluxFile) { Remove-Item $tmpFluxFile -Force -ErrorAction SilentlyContinue }
    }

    $lines = [string]::Join("`n", $output) -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith('#') }
    if ($lines.Count -lt 2) {
        return [pscustomobject]@{ time = $null; value = $null }
    }

    if ($lines[0].StartsWith(',')) {
        $lines[0] = 'unused' + $lines[0]
    }

    $rows = @(($lines -join "`n") | ConvertFrom-Csv)
    if ($rows.Count -eq 0) {
        return [pscustomobject]@{ time = $null; value = $null }
    }

    return [pscustomobject]@{
        time = $rows[0]._time
        value = To-DoubleOrNull $rows[0]._value
    }
}

$config = Load-Config $ConfigPath
Assert-InfluxCliAvailable

Write-Host '=== Energy Setup Smoke Test ==='
Write-Host "Config: $ConfigPath"

$api = Check-Api $config
Write-Host "[OK] API current: $($api.currentUrl) -> total_power_w=$($api.totalCurrentW)"
Write-Host "[OK] API month:   $($api.monthUrl) -> total_kwh=$($api.monthTotalKwh)"

$shellyW = Check-Shelly $config
Write-Host "[OK] Shelly:      $($config.shellyUrl) -> power_w=$shellyW"

$influx = Check-Influx $config
Write-Host "[OK] Influx raw:  bucket=$($config.influx.bucketRaw), meter=$($config.meterId), value=$($influx.value), time=$($influx.time)"

Write-Host ''
Write-Host 'Smoke test passed. Ready to run protocol blocks.'
