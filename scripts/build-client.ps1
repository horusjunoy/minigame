. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "build_client"
$unityPath = Resolve-UnityPath
$metadata = New-BuildMetadata -RepoRoot $repoRoot -Target "client"

if (-not $unityPath) {
    $command = "Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1"
    Invoke-LoggedCommand -Command $command -WorkingDirectory $repoRoot -LogPath $logPath
}

$args = @(
    "-batchmode",
    "-quit",
    "-projectPath", $repoRoot,
    "-executeMethod", "Game.Editor.BuildScripts.BuildClient",
    "-logFile", (Join-Path $repoRoot "logs\\unity-build-client.log")
)

$buildInfoPath = Write-BuildMetadata -OutputDir (Join-Path $repoRoot "artifacts\\builds\\client") -Metadata $metadata
Write-Output "build_version=$($metadata.version)"
Write-Output "build_commit=$($metadata.commit)"
Write-Output "build_info=$buildInfoPath"
Invoke-LoggedProcess -FilePath $unityPath -Arguments $args -WorkingDirectory $repoRoot -LogPath $logPath
