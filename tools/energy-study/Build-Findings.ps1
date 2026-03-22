param(
    [Parameter(Mandatory = $true)]
    [string]$RunDir,

    [Parameter(Mandatory = $false)]
    [string]$OutputMarkdown = '',

    [Parameter(Mandatory = $false)]
    [string]$OutputCsv = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function To-DoubleOrNull($value) {
    if ($null -eq $value) { return $null }
    $s = ([string]$value).Trim()
    if ([string]::IsNullOrWhiteSpace($s)) { return $null }

    if ($s -match ',' -and $s -notmatch '\.') {
        $s = $s -replace ',', '.'
    }

    $tmp = 0.0
    if ([double]::TryParse($s, [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$tmp)) {
        return $tmp
    }

    if ([double]::TryParse($s, [System.Globalization.NumberStyles]::Any, [System.Globalization.CultureInfo]::CurrentCulture, [ref]$tmp)) {
        return $tmp
    }

    return $null
}

function Get-ItemCount($obj) {
    if ($null -eq $obj) { return 0 }
    return @($obj).Length
}

function Mean($arr) {
    $vals = @($arr | Where-Object { $null -ne $_ })
    if ((Get-ItemCount $vals) -eq 0) { return $null }
    return ($vals | Measure-Object -Average).Average
}

function Max($arr) {
    $vals = @($arr | Where-Object { $null -ne $_ })
    if ((Get-ItemCount $vals) -eq 0) { return $null }
    return ($vals | Measure-Object -Maximum).Maximum
}

if (-not (Test-Path $RunDir)) {
    throw "Run directory not found: $RunDir"
}

if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    $OutputMarkdown = Join-Path $RunDir 'Findings.md'
}

if ([string]::IsNullOrWhiteSpace($OutputCsv)) {
    $OutputCsv = Join-Path $RunDir 'findings_summary.csv'
}

$blockAPath = Join-Path $RunDir 'blockA_samples.csv'
$blockBPath = Join-Path $RunDir 'blockB_result.csv'
$blockCPath = Join-Path $RunDir 'blockC_latency.csv'
$blockDPath = Join-Path $RunDir 'blockD_consistency.csv'

$aRows = if (Test-Path $blockAPath) { @(Import-Csv $blockAPath) } else { @() }
$bRows = if (Test-Path $blockBPath) { @(Import-Csv $blockBPath) } else { @() }
$cRows = if (Test-Path $blockCPath) { @(Import-Csv $blockCPath) } else { @() }
$dRows = if (Test-Path $blockDPath) { @(Import-Csv $blockDPath) } else { @() }

$aErrApi = @($aRows | ForEach-Object { To-DoubleOrNull $_.err_shelly_vs_api_pct })
$aErrInflux = @($aRows | ForEach-Object { To-DoubleOrNull $_.err_shelly_vs_influx_pct })

$bLatest = if ((Get-ItemCount $bRows) -gt 0) { $bRows[(Get-ItemCount $bRows) - 1] } else { $null }
$bError = if ($null -ne $bLatest) { To-DoubleOrNull $bLatest.error_pct } else { $null }
$bDeltaReal = if ($null -ne $bLatest) { To-DoubleOrNull $bLatest.delta_real_kwh } else { $null }
$bDeltaTheo = if ($null -ne $bLatest) { To-DoubleOrNull $bLatest.delta_theoretical_kwh } else { $null }

$cLatest = if ((Get-ItemCount $cRows) -gt 0) { $cRows[(Get-ItemCount $cRows) - 1] } else { $null }
$cTotalLatency = if ($null -ne $cLatest) { To-DoubleOrNull $cLatest.latency_total_to_unity_s } else { $null }
$cApiLatency = if ($null -ne $cLatest) { To-DoubleOrNull $cLatest.latency_influx_to_api_s } else { $null }
$cUnityLatency = if ($null -ne $cLatest) { To-DoubleOrNull $cLatest.latency_api_to_unity_s } else { $null }

$dLatest = if ((Get-ItemCount $dRows) -gt 0) { $dRows[(Get-ItemCount $dRows) - 1] } else { $null }
$dDiff = if ($null -ne $dLatest) { To-DoubleOrNull $dLatest.difference_pct } else { $null }

$summaryRows = @(
    [pscustomobject]@{ metric = 'blockA_samples_n'; value = (Get-ItemCount $aRows) },
    [pscustomobject]@{ metric = 'blockA_mean_error_api_pct'; value = Mean $aErrApi },
    [pscustomobject]@{ metric = 'blockA_max_error_api_pct'; value = Max $aErrApi },
    [pscustomobject]@{ metric = 'blockA_mean_error_influx_pct'; value = Mean $aErrInflux },
    [pscustomobject]@{ metric = 'blockA_max_error_influx_pct'; value = Max $aErrInflux },
    [pscustomobject]@{ metric = 'blockB_delta_real_kwh'; value = $bDeltaReal },
    [pscustomobject]@{ metric = 'blockB_delta_theoretical_kwh'; value = $bDeltaTheo },
    [pscustomobject]@{ metric = 'blockB_error_pct'; value = $bError },
    [pscustomobject]@{ metric = 'blockC_total_latency_s'; value = $cTotalLatency },
    [pscustomobject]@{ metric = 'blockC_influx_to_api_latency_s'; value = $cApiLatency },
    [pscustomobject]@{ metric = 'blockC_api_to_unity_latency_s'; value = $cUnityLatency },
    [pscustomobject]@{ metric = 'blockD_raw_vs_1h_diff_pct'; value = $dDiff }
)

$summaryRows | Export-Csv -Path $OutputCsv -NoTypeInformation -Encoding utf8

$now = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss UTC')

$md = @"
# IV. Findings / Results

Generated at: $now

## A. Collected Data
- Block A samples: $(Get-ItemCount $aRows)
- Mean error Shelly vs API: $([string](Mean $aErrApi)) %
- Mean error Shelly vs Influx: $([string](Mean $aErrInflux)) %
- Block B real energy delta: $([string]$bDeltaReal) kWh
- Block B theoretical energy delta: $([string]$bDeltaTheo) kWh
- Block C total latency (event to Unity): $([string]$cTotalLatency) s
- Block D raw vs 1h difference: $([string]$dDiff) %

## B. Data Analysis
- Instantaneous consistency (A): mean API error = $([string](Mean $aErrApi)) %, max API error = $([string](Max $aErrApi)) %.
- Aggregated monthly consistency (B): relative error = $([string]$bError) %.
- End-to-end latency (C): total latency = $([string]$cTotalLatency) s, API segment = $([string]$cApiLatency) s, Unity segment = $([string]$cUnityLatency) s.
- Raw vs 1h consistency (D): difference = $([string]$dDiff) %.

## C. Suggested Interpretation for DSR
- If Block A mean error < 3%, data correctness at operational layer is supported.
- If Block B error < 5%, monthly aggregation integrity is supported.
- If Block C latency < 10 s, digital twin responsiveness is suitable for industrial monitoring.
- If Block D difference is near 0%, ETL/aggregation pipeline consistency is supported.
"@

$md | Out-File -FilePath $OutputMarkdown -Encoding utf8

Write-Host "Findings markdown: $OutputMarkdown"
Write-Host "Findings CSV: $OutputCsv"
