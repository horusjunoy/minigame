. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "start_stack"
$summaryPath = Join-Path $repoRoot ("logs\\start_stack_results_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
Ensure-Dir (Split-Path -Parent $logPath)

$start = Get-Date
$commit = Get-GitCommit $repoRoot
"cmd=node services/matchmaker/index.js" | Out-File -FilePath $logPath -Encoding utf8
"cwd=$repoRoot" | Out-File -FilePath $logPath -Encoding utf8 -Append
"commit=$commit" | Out-File -FilePath $logPath -Encoding utf8 -Append
"start=$($start.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append

$matchmakerLog = Join-Path $repoRoot "logs\\matchmaker.log"
Ensure-Dir (Split-Path -Parent $matchmakerLog)

$process = New-Object System.Diagnostics.Process
$process.StartInfo.FileName = "node"
$process.StartInfo.WorkingDirectory = $repoRoot
$process.StartInfo.RedirectStandardOutput = $true
$process.StartInfo.RedirectStandardError = $true
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.CreateNoWindow = $true
$process.StartInfo.Arguments = "services/matchmaker/index.js"
$null = $process.Start()

Start-Sleep -Seconds 1

function Write-Result([string]$Line) {
    $Line | Out-Host
    $Line | Out-File -FilePath $summaryPath -Encoding utf8 -Append
}

function Invoke-Json([string]$Method, [string]$Url, [object]$Body) {
    $json = $null
    if ($Body -ne $null) {
        $json = ($Body | ConvertTo-Json -Depth 6)
    }
    return Invoke-RestMethod -Method $Method -Uri $Url -Body $json -ContentType "application/json"
}

$baseUrl = "http://localhost:8080"
$match = Invoke-Json "POST" "$baseUrl/matches" @{ minigame_id = "stub_v1"; max_players = 4; visibility = "public" }
Write-Result "create_match $($match.match_id)"

$list = Invoke-Json "GET" "$baseUrl/matches" $null
Write-Result "list_matches count=$($list.Count)"

$join = Invoke-Json "POST" "$baseUrl/matches/$($match.match_id)/join" $null
Write-Result "join_match endpoint=$($join.endpoint)"

$heartbeat = Invoke-Json "POST" "$baseUrl/matches/$($match.match_id)/heartbeat" $null
Write-Result "heartbeat status=$($heartbeat.status)"

$end = Invoke-Json "POST" "$baseUrl/matches/$($match.match_id)/end" @{ reason = "completed"; duration_s = 1 }
Write-Result "end_match status=$($end.status)"

$metrics = Invoke-RestMethod -Method Get -Uri "$baseUrl/metrics"
Write-Result "metrics ok"

$dashboard = Invoke-RestMethod -Method Get -Uri "$baseUrl/dashboard"
Write-Result "dashboard ok"
Write-Result "finalizado"

try { $process.Kill() } catch { }
try { $process.WaitForExit(2000) | Out-Null } catch { }

$stdout = $process.StandardOutput.ReadToEnd()
$stderr = $process.StandardError.ReadToEnd()
if ($stdout) { $stdout | Out-File -FilePath $matchmakerLog -Encoding utf8 -Append }
if ($stderr) { $stderr | Out-File -FilePath $matchmakerLog -Encoding utf8 -Append }

$endTime = Get-Date
$duration = New-TimeSpan -Start $start -End $endTime
"end=$($endTime.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append
"duration_ms=$([int]$duration.TotalMilliseconds)" | Out-File -FilePath $logPath -Encoding utf8 -Append
"exit_code=0" | Out-File -FilePath $logPath -Encoding utf8 -Append
