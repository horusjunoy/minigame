param(
    [int]$DurationSeconds = 3600,
    [int]$BotCount = 8,
    [int]$TickRate = 30,
    [int]$TimeScale = 1,
    [int]$ScoreToWin = -1,
    [string]$MinigameId = "arena_v1",
    [int]$MaxRestarts = 0,
    [int]$RestartDelaySeconds = 5
)

. "$PSScriptRoot\_common.ps1"

$repoRoot = Get-RepoRoot
$summaryPath = Join-Path $repoRoot "logs\soak_summary.log"
$unityPath = Resolve-UnityPath

if (-not $unityPath) {
    $command = "Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1"
    Invoke-LoggedCommand -Command $command -WorkingDirectory $repoRoot -LogPath $logPath
}

Ensure-Dir (Join-Path $repoRoot "artifacts")

$args = @(
    "-batchmode",
    "-quit",
    "-projectPath", $repoRoot,
    "-executeMethod", "Game.Editor.SoakBatchRunner.Run",
    "-minigame", $MinigameId,
    "-bots", $BotCount,
    "-duration", $DurationSeconds,
    "-tickRate", $TickRate,
    "-timeScale", $TimeScale,
    "-scoreToWin", $ScoreToWin,
    "-summaryPath", $summaryPath,
    "-logFile", (Join-Path $repoRoot "logs\unity-soak.log")
)

$attempt = 0
$exitCode = 1
while ($attempt -le $MaxRestarts) {
    $attempt += 1
    $logPath = if ($MaxRestarts -gt 0) { New-LogPath $repoRoot ("soak_attempt_" + $attempt) } else { New-LogPath $repoRoot "soak" }
    $exitCode = Invoke-LoggedProcess -FilePath $unityPath -Arguments $args -WorkingDirectory $repoRoot -LogPath $logPath -AllowFailure
    if ($exitCode -eq 0) {
        break
    }
    if ($attempt -le $MaxRestarts) {
        "soak_restart attempt=$attempt exit_code=$exitCode" | Out-Host
        "soak_restart attempt=$attempt exit_code=$exitCode" | Out-File -FilePath $summaryPath -Encoding utf8 -Append
        Start-Sleep -Seconds $RestartDelaySeconds
    }
}

if ($exitCode -ne 0) {
    exit $exitCode
}

"finalizado" | Out-Host
"finalizado" | Out-File -FilePath $summaryPath -Encoding utf8 -Append
