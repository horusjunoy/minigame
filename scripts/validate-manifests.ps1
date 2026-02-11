. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "validate_manifest"
$summaryPath = Join-Path $repoRoot ("logs\\validate_manifest_results_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
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
    "-executeMethod", "Game.Editor.ManifestValidator.Run",
    "-logFile", (Join-Path $repoRoot "logs\\unity-manifest.log")
)

$exitCode = Invoke-LoggedProcess -FilePath $unityPath -Arguments $args -WorkingDirectory $repoRoot -LogPath $logPath -AllowFailure

Ensure-Dir (Split-Path -Parent $summaryPath)

function Write-Line([string]$Line) {
    $Line | Out-Host
    $Line | Out-File -FilePath $summaryPath -Encoding utf8 -Append
}

if ($exitCode -ne 0) {
    Write-Line "manifest_validation Failed"
    Write-Line "finalizado"
    exit $exitCode
}

Write-Line "manifest_validation Passed"
Write-Line "finalizado"
