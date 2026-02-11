param(
    [string]$MatchmakerUrl = "http://127.0.0.1:8080",
    [int]$HealthPort = 18080,
    [int]$ServerHealthTimeoutSeconds = 120
)

. "$PSScriptRoot\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "smoke"
Ensure-Dir (Join-Path $repoRoot "logs")

function Wait-ForHealth {
    param(
        [Parameter(Mandatory=$true)][string]$Url,
        [Parameter(Mandatory=$true)][string]$Label,
        [Parameter(Mandatory=$true)][datetime]$Deadline,
        [Parameter()][System.Diagnostics.Process]$Process
    )

    while ((Get-Date) -lt $Deadline) {
        if ($Process -and $Process.HasExited) {
            throw "$Label encerrou antes do health responder."
        }
        try {
            return Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 5
        } catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "$Label nao respondeu health a tempo."
}

try {
    $start = Get-Date
    $commit = Get-GitCommit $repoRoot

    "cmd=Unity RuntimeSmokeRunner.Run" | Out-File -FilePath $logPath -Encoding utf8
    "cwd=$repoRoot" | Out-File -FilePath $logPath -Encoding utf8 -Append
    "commit=$commit" | Out-File -FilePath $logPath -Encoding utf8 -Append
    "start=$($start.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append

    $mmLog = New-LogPath $repoRoot "matchmaker_smoke"
    $mmErr = $mmLog -replace "\.log$", "_err.log"
    $mm = Start-Process -FilePath "node" `
        -ArgumentList "$repoRoot\services\matchmaker\index.js" `
        -WorkingDirectory $repoRoot `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $mmLog `
        -RedirectStandardError $mmErr

    $mmHealth = Wait-ForHealth -Url "$MatchmakerUrl/health" -Label "Matchmaker" -Deadline (Get-Date).AddSeconds(20) -Process $mm

    $unityPath = Resolve-UnityPath
    if (-not $unityPath) {
        Write-Error "Unity Editor nao encontrado. Defina UNITY_PATH."
        exit 1
    }

    $env:SERVER_HEALTH_ENABLE = "1"
    $env:SERVER_HEALTH_PORT = "$HealthPort"

    $unityArgs = @(
        "-batchmode",
        "-quit",
        "-projectPath", $repoRoot,
        "-executeMethod", "Game.Editor.RuntimeSmokeRunner.Run",
        "-logFile", (Join-Path $repoRoot "logs\unity-smoke.log")
    )

    $unityErr = $logPath -replace "\.log$", "_err.log"
    $unity = Start-Process -FilePath $unityPath `
        -ArgumentList $unityArgs `
        -WorkingDirectory $repoRoot `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $logPath `
        -RedirectStandardError $unityErr

    $serverHealth = Wait-ForHealth -Url "http://127.0.0.1:$HealthPort/" -Label "Server" -Deadline (Get-Date).AddSeconds($ServerHealthTimeoutSeconds) -Process $unity

    $unity.WaitForExit()
    $unityExit = $unity.ExitCode

    $mmHealth = Invoke-WebRequest -UseBasicParsing -Uri "$MatchmakerUrl/health" -TimeoutSec 5

    Write-Output "matchmaker_health=$($mmHealth.StatusCode)"
    Write-Output "server_health=$($serverHealth.StatusCode)"

    $end = Get-Date
    $duration = New-TimeSpan -Start $start -End $end
    "end=$($end.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append
    "duration_ms=$([int]$duration.TotalMilliseconds)" | Out-File -FilePath $logPath -Encoding utf8 -Append
    "exit_code=$unityExit" | Out-File -FilePath $logPath -Encoding utf8 -Append

    if ($unityExit -ne 0) {
        exit $unityExit
    }
}
finally {
    if ($mm -and -not $mm.HasExited) {
        $mm | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    Write-Output "finalizado"
}
