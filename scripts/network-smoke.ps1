. "$PSScriptRoot\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "network_smoke"
$unityPath = Resolve-UnityPath

if (-not $unityPath) {
    $command = "Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1"
    Invoke-LoggedCommand -Command $command -WorkingDirectory $repoRoot -LogPath $logPath
}

$args = @(
    "-batchmode",
    "-quit",
    "-projectPath", $repoRoot,
    "-executeMethod", "Game.Editor.NetworkSmokeRunner.Run",
    "-logFile", (Join-Path $repoRoot "logs\unity-network-smoke.log")
)

Invoke-LoggedProcess -FilePath $unityPath -Arguments $args -WorkingDirectory $repoRoot -LogPath $logPath
