. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "scale_smoke"
$summaryPath = Join-Path $repoRoot ("logs\\scale_smoke_results_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
Ensure-Dir (Split-Path -Parent $logPath)

function Write-Result([string]$Line) {
    $Line | Out-Host
    $Line | Out-File -FilePath $summaryPath -Encoding utf8 -Append
}

$start = Get-Date
$commit = Get-GitCommit $repoRoot
"cmd=scale-smoke" | Out-File -FilePath $logPath -Encoding utf8
"cwd=$repoRoot" | Out-File -FilePath $logPath -Encoding utf8 -Append
"commit=$commit" | Out-File -FilePath $logPath -Encoding utf8 -Append
"start=$($start.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append

$env:MATCHMAKER_SERVER_POOL = "127.0.0.1:7770=2;127.0.0.1:7771=2"
$env:MATCHMAKER_MAX_MATCHES = "10"

$mmLog = New-LogPath $repoRoot "matchmaker_scale"
$mmErr = $mmLog -replace "\.log$", "_err.log"
$mm = Start-Process -FilePath "node" `
    -ArgumentList "$repoRoot\services\matchmaker\index.js" `
    -WorkingDirectory $repoRoot `
    -PassThru `
    -NoNewWindow `
    -RedirectStandardOutput $mmLog `
    -RedirectStandardError $mmErr

function Wait-ForHealth {
    param(
        [Parameter(Mandatory=$true)][string]$Url,
        [Parameter(Mandatory=$true)][datetime]$Deadline,
        [Parameter()][System.Diagnostics.Process]$Process
    )

    while ((Get-Date) -lt $Deadline) {
        if ($Process -and $Process.HasExited) {
            throw "Matchmaker encerrou antes do health responder."
        }
        try {
            return Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 5
        } catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "Matchmaker nao respondeu health a tempo."
}

try {
    $baseUrl = "http://127.0.0.1:8080"
    $mmHealth = Wait-ForHealth -Url "$baseUrl/health" -Deadline (Get-Date).AddSeconds(20) -Process $mm

    $endpoints = @{}
    $matches = @()
    for ($i = 1; $i -le 5; $i++) {
        $body = @{ minigame_id = "stub_v1"; max_players = 4 } | ConvertTo-Json -Depth 4
        try {
            $resp = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$baseUrl/matches" -Body $body -ContentType "application/json" -TimeoutSec 5
            $payload = $resp.Content | ConvertFrom-Json
            $matches += $payload
            if (-not $endpoints.ContainsKey($payload.endpoint)) {
                $endpoints[$payload.endpoint] = 0
            }
            $endpoints[$payload.endpoint] += 1
            Write-Result "create_match ok endpoint=$($payload.endpoint)"
        } catch {
            if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
                $status = [int]$_.Exception.Response.StatusCode
                $bodyText = $null
                try {
                    if ($_.Exception.Response.Content) {
                        $bodyText = $_.Exception.Response.Content.ReadAsStringAsync().Result
                    }
                } catch {}
                Write-Result "create_match fail status=$status body=$bodyText"
            } else {
                Write-Result "create_match fail error=$($_.Exception.Message)"
            }
        }
    }

    foreach ($key in $endpoints.Keys) {
        Write-Result "endpoint_count $key=$($endpoints[$key])"
    }

    if ($endpoints["127.0.0.1:7770"] -gt 2 -or $endpoints["127.0.0.1:7771"] -gt 2) {
        throw "Distribuicao excedeu capacidade."
    }

    if ($matches.Count -ne 4) {
        throw "Esperado 4 matches alocados, obtido $($matches.Count)."
    }

    $metrics = Invoke-RestMethod -Method Get -Uri "$baseUrl/metrics"
    Write-Result "metrics ok"

    $dashboard = Invoke-RestMethod -Method Get -Uri "$baseUrl/dashboard"
    Write-Result "dashboard ok"

    Write-Result "finalizado"
}
finally {
    if ($mm -and -not $mm.HasExited) {
        $mm | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

$end = Get-Date
$duration = New-TimeSpan -Start $start -End $end
"end=$($end.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append
"duration_ms=$([int]$duration.TotalMilliseconds)" | Out-File -FilePath $logPath -Encoding utf8 -Append
"exit_code=0" | Out-File -FilePath $logPath -Encoding utf8 -Append
