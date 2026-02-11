param(
    [string]$MinigameId,
    [string]$OutputRoot
)

. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "build_content"
$summaryPath = Join-Path $repoRoot ("logs\\build_content_results_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
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
    "-executeMethod", "Game.Editor.ContentCatalogBuilder.Build",
    "-logFile", (Join-Path $repoRoot "logs\\unity-content.log")
)

if ($MinigameId) {
    $args += @("-minigame", $MinigameId)
}

if ($OutputRoot) {
    $args += @("-outputRoot", $OutputRoot)
}

$exitCode = Invoke-LoggedProcess -FilePath $unityPath -Arguments $args -WorkingDirectory $repoRoot -LogPath $logPath -AllowFailure

Ensure-Dir (Split-Path -Parent $summaryPath)

function Write-Line([string]$Line) {
    $Line | Out-Host
    $Line | Out-File -FilePath $summaryPath -Encoding utf8 -Append
}

if ($exitCode -ne 0) {
    Write-Line "build_content Failed"
    Write-Line "finalizado"
    exit $exitCode
}

Write-Line "build_content Passed"
Write-Line "finalizado"
